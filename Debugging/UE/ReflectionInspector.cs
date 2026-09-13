using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NanoUint.Diagnostics;

namespace NanoUint.Debugging.UE;

internal sealed class ReflectionInspector
{
    private enum MemberScope { All, Instance, Static }

    private readonly object _target;
    private readonly Type _type;

    private StackPanel? _list;
    private string _filter = "";
    private MemberScope _scope = MemberScope.All;
    private bool _showProperty = true;
    private bool _showField = true;
    private bool _showMethod = true;
    private bool _showCtor;
    private bool _autoUpdate = true;
    private DispatcherTimer? _autoTimer;
    private readonly Dictionary<MemberScope, Button> _scopeButtons = new();

    public ReflectionInspector(Component comp)
    {
        _target = comp;
        _type = comp.GetType();
    }

    public UIElement Build()
    {
        var root = new StackPanel { Background = new SolidColorBrush(UEPalette.InspectorRoot) };

        #region TopRow: type title 17px + Copy (yellow)
        var top = new Grid
        {
            Background = new SolidColorBrush(UEPalette.InspectorTopRow),
            Height = 30,
        };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var typeTitle = UEFactory.Label(SignatureHighlighter.TypeName(_type), 17, UEPalette.SigClass);
        typeTitle.Margin = new Thickness(6, 0, 0, 0);
        UEFactory.SetCell(typeTitle, top, 0);

        var copyBtn = UEFactory.Button("Copy", 120, 25, UEPalette.NormalButton, 12, UEPalette.TextYellow);
        copyBtn.Margin = new Thickness(0, 0, 4, 0);
        copyBtn.Click += (_, _) =>
        {
            try { System.Windows.Clipboard.SetText(SignatureHighlighter.TypeName(_type)); }
            catch (Exception ex) { Logger.Warning("UE", $"Clipboard failed: {ex.Message}"); }
        };
        UEFactory.SetCell(copyBtn, top, 1);
        root.Children.Add(top);

        #endregion

        #region AssemblyRow
        var asmRow = new Grid
        {
            Background = new SolidColorBrush(UEPalette.InspectorTopRow),
            Height = 26,
        };
        asmRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        asmRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var asmLabel = UEFactory.Label($"Assembly: {_type.Assembly.GetName().Name}", 12, UEPalette.TextInactive);
        asmLabel.Margin = new Thickness(6, 0, 0, 0);
        UEFactory.SetCell(asmLabel, asmRow, 0);

        var dnSpyBtn = UEFactory.Button("dnSpy", 120, 25, UEPalette.NormalButton, 12);
        dnSpyBtn.IsEnabled = false;
        dnSpyBtn.Margin = new Thickness(0, 0, 4, 0);
        UEFactory.SetCell(dnSpyBtn, asmRow, 1);
        root.Children.Add(asmRow);

        #endregion

        #region FirstRow: Filter names + Update displayed values + Auto-update
        var firstRow = new Grid
        {
            Background = new SolidColorBrush(UEPalette.InspectorTopRow),
            Height = 26,
        };
        firstRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        firstRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
        firstRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var filterInput = UEFactory.Input(double.NaN, 24, "", UEPalette.InputBackground, UEPalette.InputBorder, 12);
        filterInput.Margin = new Thickness(6, 0, 4, 0);
        filterInput.ToolTip = "Filter names...";
        filterInput.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _filter = filterInput.Text.Trim();
                RefreshMembers();
            }
        };
        UEFactory.SetCell(filterInput, firstRow, 0);

        var updateBtn = UEFactory.Button("Update displayed values", 185, 24, UEPalette.UpdateButton, 12);
        updateBtn.Margin = new Thickness(0, 0, 4, 0);
        updateBtn.Click += (_, _) => RefreshMembers();
        UEFactory.SetCell(updateBtn, firstRow, 1);

        var autoWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var autoCheck = UEFactory.Check(_autoUpdate, v => _autoUpdate = v, UEPalette.BehaviourToggleGraphic);
        autoCheck.Margin = new Thickness(4, 0, 4, 0);
        autoWrap.Children.Add(autoCheck);
        autoWrap.Children.Add(UEFactory.Label("Auto-update", 12));
        UEFactory.SetCell(autoWrap, firstRow, 2);
        root.Children.Add(firstRow);

        #endregion

        #region SecondRow: Scope + member type toggles
        var secondRow = new Grid
        {
            Background = new SolidColorBrush(UEPalette.InspectorTopRow),
            Height = 26,
        };
        secondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        secondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });
        secondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var scopeLabel = UEFactory.Label("Scope:", 12, UEPalette.TextInactive);
        scopeLabel.Margin = new Thickness(6, 0, 0, 0);
        UEFactory.SetCell(scopeLabel, secondRow, 0);

        var scopeWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (name, scope) in new[]
                 {
                     ("All", MemberScope.All),
                     ("Instance", MemberScope.Instance),
                     ("Static", MemberScope.Static),
                 })
        {
            var btn = UEFactory.Button(name, 70, 22, UEPalette.ScopeUnselected, 12);
            btn.Margin = new Thickness(0, 0, 3, 0);
            btn.Click += (_, _) =>
            {
                _scope = scope;
                RefreshMembers();
            };
            btn.Tag = scope;
            _scopeButtons[scope] = btn;
            scopeWrap.Children.Add(btn);
        }
        UEFactory.SetCell(scopeWrap, secondRow, 1);

        var memberWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        memberWrap.Children.Add(BuildMemberToggle("Property", UEPalette.SigProperty, v => { _showProperty = v; RefreshMembers(); }));
        memberWrap.Children.Add(BuildMemberToggle("Field", UEPalette.SigField, v => { _showField = v; RefreshMembers(); }));
        memberWrap.Children.Add(BuildMemberToggle("Method", UEPalette.SigMethod, v => { _showMethod = v; RefreshMembers(); }));
        memberWrap.Children.Add(BuildMemberToggle("Constructor", UEPalette.SigClass, v => { _showCtor = v; RefreshMembers(); }));
        UEFactory.SetCell(memberWrap, secondRow, 2);
        root.Children.Add(secondRow);

        #endregion

        #region Member list
        _list = new StackPanel { Background = new SolidColorBrush(UEPalette.InspectorScroll) };
        var scroller = new ScrollViewer
        {
            Content = _list,
            Background = new SolidColorBrush(UEPalette.InspectorScroll),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        UEFactory.ApplyScrollbarStyle(scroller);
        root.Children.Add(scroller);

        RefreshMembers();

        _autoTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _autoTimer.Tick += (_, _) =>
        {
            if (_autoUpdate) RefreshMembers();
        };
        _autoTimer.Start();

        return root;
        #endregion
    }

    #region Member collection and filtering

    private static bool IsStatic(MemberInfo m) => m switch
    {
        FieldInfo fi => fi.IsStatic,
        PropertyInfo pi => (pi.GetMethod ?? pi.SetMethod)?.IsStatic ?? false,
        MethodInfo mi => mi.IsStatic,
        _ => false,
    };

    private IEnumerable<MemberInfo> CollectMembers()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static;
        var list = new List<MemberInfo>();
        list.AddRange(_type.GetFields(flags));
        list.AddRange(_type.GetProperties(flags));
        list.AddRange(_type.GetMethods(flags).Where(m => !m.IsSpecialName)); // Exclude property accessors
        list.AddRange(_type.GetConstructors(flags));
        return list;
    }

    private bool Matches(MemberInfo m)
    {
        switch (m)
        {
            case FieldInfo when !_showField:
            case PropertyInfo when !_showProperty:
            case MethodInfo when !_showMethod:
            case ConstructorInfo when !_showCtor:
                return false;
        }

        var isStatic = IsStatic(m);
        if (_scope == MemberScope.Instance && isStatic) return false;
        if (_scope == MemberScope.Static && !isStatic) return false;

        if (!string.IsNullOrEmpty(_filter) &&
            !m.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (m is PropertyInfo pi && pi.GetIndexParameters().Length > 0) return false;

        return true;
    }

    private void RefreshMembers()
    {
        if (_list == null) return;

        foreach (var (scope, btn) in _scopeButtons)
        {
            btn.Background = new SolidColorBrush(
                scope == _scope ? UEPalette.ScopeSelected : UEPalette.ScopeUnselected);
        }

        _list.Children.Clear();

        var members = CollectMembers().Where(Matches).ToArray();
        if (members.Length == 0)
        {
            _list.Children.Add(UEFactory.Label("(no members)", 12, UEPalette.TextInactive, FontStyles.Italic));
            return;
        }

        foreach (var member in members)
            _list.Children.Add(BuildRow(member));
    }

    #endregion

    #region Row building

    private UIElement BuildRow(MemberInfo member)
    {
        var row = new Border
        {
            Background = Brushes.Transparent,
            MinHeight = 25,
            BorderBrush = new SolidColorBrush(UEPalette.InspectorBorder),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

        var sig = SignatureHighlighter.Signature(member, 12);
        sig.Margin = new Thickness(6, 0, 4, 0);
        UEFactory.SetCell(sig, grid, 0);

        var valueControl = BuildValueControl(member);
        if (valueControl != null)
        {
            if (valueControl is FrameworkElement valueFe)
                valueFe.Margin = new Thickness(0, 1, 6, 1);
            UEFactory.SetCell(valueControl, grid, 1);
        }

        row.Child = grid;
        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(UEPalette.TreeNodeHover);
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    private FrameworkElement? BuildValueControl(MemberInfo member)
    {
        Type type;
        Func<object?> get;
        Action<object?>? set;

        if (member is FieldInfo fi)
        {
            type = fi.FieldType;
            get = () => fi.GetValue(_target);
            set = fi.IsInitOnly || fi.IsLiteral ? null : v => fi.SetValue(_target, v);
        }
        else if (member is PropertyInfo pi)
        {
            if (pi.GetIndexParameters().Length > 0) return null;
            type = pi.PropertyType;
            get = () => pi.GetValue(_target);
            set = pi.CanWrite ? v => pi.SetValue(_target, v) : null;
        }
        else
        {
            return null;
        }

        return BuildValueEditor(type, get, set);
    }

    private FrameworkElement BuildValueEditor(Type type, Func<object?> get, Action<object?>? set)
    {
        object? value;
        try { value = get(); }
        catch (Exception ex) { Logger.Warning("UE", $"Value read failed: {ex.Message}"); value = null; }

        if (type == typeof(bool))
        {
            var check = UEFactory.Check(value is true,
                v => { try { set?.Invoke(v); } catch (Exception ex) { Logger.Warning("UE", ex.Message); } },
                UEPalette.BehaviourToggleGraphic);
            check.VerticalAlignment = VerticalAlignment.Center;
            check.IsEnabled = set != null;
            return check;
        }

        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var dropdown = new UEDropdown(160, 22, 12);
            dropdown.Items = names;
            dropdown.SelectedIndex = value != null && names.Length > 0 ? Array.IndexOf(names, value.ToString() ?? "") : -1;
            if (set != null)
            {
                dropdown.SelectionChanged += i =>
                {
                    try { set(Enum.Parse(type, names[i])); }
                    catch (Exception ex) { Logger.Warning("UE", ex.Message); }
                };
            }
            dropdown.IsEnabled = set != null;
            dropdown.HorizontalAlignment = HorizontalAlignment.Right;
            return dropdown;
        }

        if (IsPrimitiveEditable(type))
        {
            var display = value == null ? "(null)" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            var box = UEFactory.Input(180, 22, display, fontSize: 12);
            box.HorizontalAlignment = HorizontalAlignment.Right;

            void Commit()
            {
                if (set == null) return;
                if (TryParse(type, box.Text, out var parsed))
                {
                    try { set(parsed); }
                    catch (Exception ex) { Logger.Warning("UE", ex.Message); }
                }
                else
                {
                    Logger.Warning("UE", $"Cannot parse '{box.Text}' as {SignatureHighlighter.TypeName(type)}");
                }
            }

            box.LostFocus += (_, _) => Commit();
            box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
            box.IsReadOnly = set == null;
            return box;
        }

        var summary = value == null ? "(null)" : Truncate(value.ToString() ?? "", 60);
        var label = UEFactory.Label(summary, 11, UEPalette.TextInactive);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.Margin = new Thickness(0, 0, 4, 0);
        return label;
    }

    private static bool IsPrimitiveEditable(Type t)
    {
        return t == typeof(float) || t == typeof(double) || t == typeof(int) ||
               t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
               t == typeof(string) || t == typeof(char);
    }

    private static bool TryParse(Type t, string text, out object? value)
    {
        value = null;
        try
        {
            if (t == typeof(float)) { value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(double)) { value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(int)) { value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(long)) { value = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(short)) { value = short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(byte)) { value = byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); return true; }
            if (t == typeof(string)) { value = text; return true; }
            if (t == typeof(char) && text.Length == 1) { value = text[0]; return true; }
        }
        catch (Exception ex)
        {
            Logger.Warning("UE", $"Parse failed: '{text}' → {t.Name}: {ex.Message}");
        }
        return false;
    }

    private static UIElement BuildMemberToggle(string text, Color color, Action<bool> onChanged)
    {
        var wrap = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        var check = UEFactory.Check(true, onChanged, color);
        check.Margin = new Thickness(0, 0, 3, 0);
        wrap.Children.Add(check);
        wrap.Children.Add(UEFactory.Label(text, 12, color));
        return wrap;
    }

    private static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= maxLen ? s : s[..(maxLen - 3)] + "...";
    }
    #endregion
}
