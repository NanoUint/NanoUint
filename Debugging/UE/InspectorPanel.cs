using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NanoUint.Diagnostics;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer InspectorPanel 1:1 复刻。</summary>
internal sealed class InspectorPanel : UEPanel
{
    private sealed class InspectorTab
    {
        public required string Label;
        public required object Target;
        public required UIElement View;
        public required Border TabButton;
        public required Button CloseButton;
    }

    private readonly StackPanel _tabBar;
    private readonly Grid _viewHost;
    private readonly List<InspectorTab> _tabs = new();
    private InspectorTab? _activeTab;

    /// <summary>Inspector 的 "Show in Explorer" 请求(由宿主接到 ObjectExplorer)。</summary>
    public event Action? ShowInExplorerRequested;

    public InspectorPanel(Canvas parentCanvas)
        : base(parentCanvas, "Inspector", "Inspector", UEPalette.PanelContent)
    {
        DefaultLeft = 0.35;
        DefaultTop = 0.175;
        DefaultWidth = 0.45;
        DefaultHeight = 0.75;
        MinPanelWidth = 810;
        MinPanelHeight = 350;

        #region 标题栏右侧: Mouse Inspect 下拉 + Close All
        var mouseInspect = new UEDropdown(140, 25, 13)
        {
            Margin = new Thickness(0, 0, 4, 0),
        };
        mouseInspect.Items = new[] { "Mouse Inspect", "World", "UI" };
        mouseInspect.SelectedIndex = 1; // World
        mouseInspect.SelectionChanged += _ => Logger.Trace("UE", "Mouse Inspect mode changed");
        TitleRightControls.Children.Add(mouseInspect);

        var closeAll = UEFactory.Button("Close All", 80, 25, UEPalette.DestroyButton, 12);
        closeAll.Margin = new Thickness(0, 0, 4, 0);
        closeAll.Click += (_, _) => CloseAll();
        TitleRightControls.Children.Add(closeAll);

        #endregion

        #region tab 条 + 视图区
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _tabBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(UEPalette.TabBarBackground),
            Height = 26,
        };
        Grid.SetRow(_tabBar, 0);
        root.Children.Add(_tabBar);

        _viewHost = new Grid { Background = new SolidColorBrush(UEPalette.PanelContent) };
        Grid.SetRow(_viewHost, 1);
        root.Children.Add(_viewHost);

        ContentHost.Children.Add(root);
        #endregion
    }

    #region 公开 API

    /// <summary>查看 GameObject([G] tab)。</summary>
    public void Inspect(GameObject go)
    {
        if (go == null) return;
        foreach (var tab in _tabs)
        {
            if (tab.Target is GameObject g && g == go)
            {
                Activate(tab);
                return;
            }
        }

        var view = new GameObjectInfoPanel(go, InspectComponent, () => ShowInExplorerRequested?.Invoke());
        AddTab($"[G] {go.Name}", go, view.Build());
        if (!IsVisible) Show();
    }

    /// <summary>查看 Component([R] 反射 tab)。</summary>
    public void InspectComponent(Component comp)
    {
        if (comp == null) return;
        foreach (var tab in _tabs)
        {
            if (tab.Target is Component c && c == comp)
            {
                Activate(tab);
                return;
            }
        }

        var view = new ReflectionInspector(comp);
        AddTab($"[R] {comp.GetType().Name}", comp, view.Build());
        if (!IsVisible) Show();
    }

    /// <summary>关闭全部 tab 并隐藏面板 (Close All)。</summary>
    public void CloseAll()
    {
        foreach (var tab in _tabs.ToList())
            CloseTab(tab);
        Hide();
    }

    #endregion

    #region tab 管理

    private void AddTab(string label, object target, UIElement view)
    {
        var tab = new InspectorTab
        {
            Label = label,
            Target = target,
            View = view,
            TabButton = null!,
            CloseButton = null!,
        };

        // 单元 200x22: 173px 按钮 + 25x25 X
        var cell = new Grid { Width = 200, Height = 22, Margin = new Thickness(2, 2, 2, 2) };
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(173) });
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });

        var tabBtn = new Border
        {
            Background = new SolidColorBrush(UEPalette.TabUnselected),
            Cursor = Cursors.Hand,
        };
        var tabText = UEFactory.Label(label, 12, UEPalette.TextInactive);
        tabText.Margin = new Thickness(4, 0, 0, 0);
        tabText.TextTrimming = TextTrimming.CharacterEllipsis;
        tabBtn.Child = tabText;
        tabBtn.MouseLeftButtonDown += (_, _) => Activate(tab);
        UEFactory.SetCell(tabBtn, cell, 0);

        var closeBtn = UEFactory.Button("✕", 25, 22, UEPalette.TabCloseButton, 10, Colors.Red);
        closeBtn.Click += (_, _) => CloseTab(tab);
        UEFactory.SetCell(closeBtn, cell, 1);

        tab.TabButton = tabBtn;
        tab.CloseButton = closeBtn;

        _tabs.Add(tab);
        _tabBar.Children.Add(cell);
        _viewHost.Children.Add(view);
        view.Visibility = Visibility.Collapsed;

        Activate(tab);
    }

    private void Activate(InspectorTab tab)
    {
        if (_activeTab != null)
        {
            _activeTab.View.Visibility = Visibility.Collapsed;
            _activeTab.TabButton.Background = new SolidColorBrush(UEPalette.TabUnselected);
            if (_activeTab.TabButton.Child is TextBlock tb)
                tb.Foreground = new SolidColorBrush(UEPalette.TextInactive);
        }

        _activeTab = tab;
        tab.View.Visibility = Visibility.Visible;
        tab.TabButton.Background = new SolidColorBrush(UEPalette.TabSelected);
        if (tab.TabButton.Child is TextBlock activeTb)
            activeTb.Foreground = new SolidColorBrush(UEPalette.TextDefault);
    }

    private void CloseTab(InspectorTab tab)
    {
        _tabs.Remove(tab);
        _tabBar.Children.Remove(tab.TabButton.Parent as UIElement ?? tab.TabButton);
        _viewHost.Children.Remove(tab.View);

        if (_activeTab == tab)
        {
            _activeTab = null;
            if (_tabs.Count > 0)
                Activate(_tabs[^1]);
        }
    }

    /// <summary>关闭当前激活 tab。</summary>
    public void CloseCurrentTab()
    {
        if (_activeTab != null)
            CloseTab(_activeTab);
    }

    /// <summary>刷新当前 [G] 视图(对象改名/Instantiate 后)。</summary>
    public void RefreshActiveView()
    {
        if (_activeTab == null) return;
        var index = _tabs.IndexOf(_activeTab);
        var target = _activeTab.Target;

        UIElement newView;
        if (target is GameObject go)
        {
            newView = new GameObjectInfoPanel(go, InspectComponent, () => ShowInExplorerRequested?.Invoke()).Build();
            _activeTab.Label = $"[G] {go.Name}";
        }
        else if (target is Component comp)
        {
            newView = new ReflectionInspector(comp).Build();
            _activeTab.Label = $"[R] {comp.GetType().Name}";
        }
        else return;

        _viewHost.Children.Remove(_activeTab.View);
        _activeTab.View = newView;
        _viewHost.Children.Insert(index, newView);
        Activate(_activeTab);

        if (_activeTab.TabButton.Child is TextBlock tb)
            tb.Text = _activeTab.Label;
    }
    #endregion
}

/// <summary>UnityExplorer GameObjectInfoPanel 复刻（[G] 视图）。</summary>
internal sealed class GameObjectInfoPanel
{
    private readonly GameObject _go;
    private readonly Action<Component> _onOpenComponent;
    private readonly Action _onShowInExplorer;

    public GameObjectInfoPanel(GameObject go, Action<Component> onOpenComponent, Action onShowInExplorer)
    {
        _go = go;
        _onOpenComponent = onOpenComponent;
        _onShowInExplorer = onShowInExplorer;
    }

    public UIElement Build()
    {
        var root = new StackPanel { Background = new SolidColorBrush(UEPalette.InspectorRoot) };

        if (_go.IsDestroyed)
        {
            root.Children.Add(UEFactory.Label("(object destroyed)", 14, UEPalette.TextDestroyed, FontStyles.Italic));
            return root;
        }

        #region Row 1: View Parent / Path / Copy
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var parentBtn = UEFactory.Button("◄ View Parent", 100, 25, UEPalette.NormalButton, 12);
        parentBtn.IsEnabled = _go.Transform.Parent != null;
        parentBtn.Click += (_, _) =>
        {
            var parentGo = _go.Transform.Parent?.GameObject;
            if (parentGo != null) InspectorManager.Inspect(parentGo);
        };
        UEFactory.SetCell(parentBtn, row1, 0);

        var pathInput = UEFactory.Input(double.NaN, 25, GetPath(_go), fontSize: 14, readOnly: true);
        pathInput.Margin = new Thickness(2, 0, 2, 0);
        UEFactory.SetCell(pathInput, row1, 1);

        var copyBtn = UEFactory.Button("Copy to Clipboard", 130, 25, UEPalette.NormalButton, 12, UEPalette.TextYellow);
        copyBtn.Click += (_, _) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(GetPath(_go));
            }
            catch (Exception ex)
            {
                Logger.Warning("UE", $"Clipboard failed: {ex.Message}");
            }
        };
        UEFactory.SetCell(copyBtn, row1, 2);
        root.Children.Add(row1);

        #endregion

        #region Row 2: 类型标题 17px + NameInput 15px
        var row2 = new Grid();
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var typeTitle = UEFactory.Label("GameObject", 17, UEPalette.SigClass);
        typeTitle.Margin = new Thickness(6, 0, 4, 0);
        UEFactory.SetCell(typeTitle, row2, 0);

        var nameInput = UEFactory.Input(200, 24, _go.Name, fontSize: 15);
        nameInput.Margin = new Thickness(0, 0, 4, 0);
        nameInput.HorizontalAlignment = HorizontalAlignment.Right;
        nameInput.LostFocus += (_, _) => _go.Name = nameInput.Text;
        nameInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) _go.Name = nameInput.Text;
        };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        UEFactory.SetCell(nameInput, row2, 1);
        root.Children.Add(row2);

        #endregion

        #region Row 3: ActiveSelf / IsStatic / Instance ID / Tag / Instantiate / Destroy
        var row3 = new Grid();
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        var activeToggle = UEFactory.Check(_go.ActiveSelf, v => _go.ActiveSelf = v, UEPalette.BehaviourToggleGraphic);
        activeToggle.Margin = new Thickness(6, 0, 4, 0);
        var activeLabel = UEFactory.Label("ActiveSelf", 12);
        var activeWrap = new StackPanel { Orientation = Orientation.Horizontal };
        activeWrap.Children.Add(activeToggle);
        activeWrap.Children.Add(activeLabel);
        UEFactory.SetCell(activeWrap, row3, 0);

        var staticToggle = UEFactory.Check(false, null);
        staticToggle.IsEnabled = false;
        staticToggle.Margin = new Thickness(6, 0, 4, 0);
        var staticLabel = UEFactory.Label("IsStatic", 12, UEPalette.TextInactive);
        var staticWrap = new StackPanel { Orientation = Orientation.Horizontal };
        staticWrap.Children.Add(staticToggle);
        staticWrap.Children.Add(staticLabel);
        UEFactory.SetCell(staticWrap, row3, 1);

        var idLabel = UEFactory.Label($"Instance ID: {RuntimeHelpers.GetHashCode(_go)}", 12);
        UEFactory.SetCell(idLabel, row3, 2);

        var tagLabel = UEFactory.Label("Tag: (none)", 12, UEPalette.TextInactive);
        UEFactory.SetCell(tagLabel, row3, 3);

        var instantiateBtn = UEFactory.Button("Instantiate", 120, 25, UEPalette.NormalButton, 12);
        instantiateBtn.Click += (_, _) =>
        {
            try
            {
                var clone = GameObject.Instantiate(_go, _go.Scene, _go.Name + " (Clone)");
                Logger.Info("UE", $"Instantiated '{clone.Name}'");
                InspectorManager.Inspect(clone);
            }
            catch (Exception ex)
            {
                Logger.Warning("UE", $"Instantiate failed: {ex.Message}");
            }
        };
        UEFactory.SetCell(instantiateBtn, row3, 4);

        var destroyBtn = UEFactory.Button("Destroy", 80, 25, UEPalette.DestroyButton, 12);
        destroyBtn.Click += (_, _) =>
        {
            var name = _go.Name;
            _go.Destroy();
            Logger.Info("UE", $"Destroyed '{name}'");
            InspectorManager.Panel?.CloseCurrentTab();
        };
        UEFactory.SetCell(destroyBtn, row3, 5);
        root.Children.Add(row3);

        #endregion

        #region Row 4: Show in Explorer / Scene / Layer / Flags
        var row4 = new Grid();
        row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var explorerBtn = UEFactory.Button("Show in Explorer", 130, 25, UEPalette.SubtleButton, 12);
        explorerBtn.Click += (_, _) => _onShowInExplorer();
        UEFactory.SetCell(explorerBtn, row4, 0);

        var sceneLabel = UEFactory.Label($"Scene: {_go.Scene?.Name ?? "(none)"}", 12, UEPalette.SigClass);
        UEFactory.SetCell(sceneLabel, row4, 1);

        var layerDropdown = new UEDropdown(120, 25, 12);
        layerDropdown.Items = new[] { "Default" };
        layerDropdown.SelectedIndex = 0;
        layerDropdown.IsEnabled = false;
        UEFactory.SetCell(layerDropdown, row4, 2);

        var flagsDropdown = new UEDropdown(150, 25, 12);
        flagsDropdown.Items = new[] { "(none)" };
        flagsDropdown.SelectedIndex = 0;
        flagsDropdown.IsEnabled = false;
        UEFactory.SetCell(flagsDropdown, row4, 3);
        root.Children.Add(row4);

        #endregion

        #region 分隔线 + 组件列表
        root.Children.Add(UEFactory.Separator(UEPalette.InspectorBorder));

        foreach (var comp in _go.Components)
            root.Children.Add(BuildComponentRow(comp));

        return root;
        #endregion
    }

    private UIElement BuildComponentRow(Component comp)
    {
        var isTransform = comp is Transform;
        var row = new Border
        {
            Height = 25,
            Background = Brushes.Transparent,
            Cursor = isTransform ? Cursors.Arrow : Cursors.Hand,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });

        // 行为 toggle (半透明绿)
        var toggle = UEFactory.Check(comp.Enabled, v => comp.Enabled = v, UEPalette.BehaviourToggleGraphic);
        toggle.HorizontalAlignment = HorizontalAlignment.Center;
        toggle.VerticalAlignment = VerticalAlignment.Center;
        UEFactory.SetCell(toggle, grid, 0);

        // 组件名 (签名着色)
        var nameLabel = UEFactory.Label(comp.GetType().Name, 12, UEPalette.SigClass);
        nameLabel.Margin = new Thickness(4, 0, 0, 0);
        if (!isTransform)
        {
            nameLabel.Cursor = Cursors.Hand;
            nameLabel.MouseLeftButtonDown += (_, _) => _onOpenComponent(comp);
        }
        UEFactory.SetCell(nameLabel, grid, 1);

        // Destroy X (Transform 无)
        if (!isTransform)
        {
            var xBtn = UEFactory.Button("✕", 25, 21, UEPalette.DestroyButton, 10, Colors.Red);
            xBtn.VerticalAlignment = VerticalAlignment.Center;
            xBtn.Click += (_, _) =>
            {
                var go = comp.GameObject;
                var typeName = comp.GetType().Name;
                if (go != null)
                {
                    go.RemoveComponent(comp);
                    Logger.Info("UE", $"Removed component {typeName} from '{go.Name}'");
                }
                InspectorManager.Panel?.RefreshActiveView();
            };
            UEFactory.SetCell(xBtn, grid, 2);
        }

        row.Child = grid;
        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(UEPalette.TreeNodeHover);
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    private static string GetPath(GameObject go)
    {
        var parts = new List<string>();
        var t = go.Transform;
        while (t != null)
        {
            parts.Add(t.GameObject?.Name ?? "?");
            t = t.Parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
