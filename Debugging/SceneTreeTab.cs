using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoUint.Debugging;

/// <summary>
/// Scene 对象树 Tab。用 WPF TreeView 显示所有 GameObject 及其组件。
/// </summary>
internal sealed class SceneTreeTab
{
    private TreeView? _treeView;
    private bool _autoRefresh = true;

    /// <summary>选中 GameObject 时触发。</summary>
    public event Action<GameObject?>? OnGameObjectSelected;

    public UIElement Build()
    {
        var panel = new DockPanel();

        // 工具栏
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 4, 4, 2),
        };

        var refreshBtn = new Button
        {
            Content = "↻ Refresh",
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 70)),
            Foreground = Brushes.LightGray,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 100)),
            Cursor = Cursors.Hand,
        };
        refreshBtn.Click += (_, _) => Refresh();
        toolbar.Children.Add(refreshBtn);

        var autoChk = new CheckBox
        {
            Content = "Auto",
            IsChecked = true,
            FontSize = 11,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        autoChk.Checked += (_, _) => _autoRefresh = true;
        autoChk.Unchecked += (_, _) => _autoRefresh = false;
        toolbar.Children.Add(autoChk);

        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);

        // TreeView
        _treeView = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.LightGray,
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 2),
        };

        _treeView.SelectedItemChanged += (_, e) =>
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is GameObject go)
                OnGameObjectSelected?.Invoke(go);
            else if (e.NewValue is TreeViewItem cvi && cvi.Tag is Component)
            {
                // 点击组件时选择其父 GameObject
                var parentGo = (cvi.Parent as TreeViewItem)?.Tag as GameObject;
                OnGameObjectSelected?.Invoke(parentGo);
            }
        };

        panel.Children.Add(_treeView);
        return panel;
    }

    /// <summary>重建场景对象树。</summary>
    public void Refresh()
    {
        if (_treeView == null) return;

        // 保存当前展开状态
        var expandedNames = new HashSet<string>();
        foreach (TreeViewItem item in _treeView.Items)
        {
            if (item.IsExpanded && item.Tag is GameObject go)
                expandedNames.Add(go.Name);
        }

        var selectedName = (_treeView.SelectedItem as TreeViewItem)?.Tag switch
        {
            GameObject go => go.Name,
            Component comp => comp.GameObject?.Name,
            _ => null,
        };

        _treeView.Items.Clear();

        var scene = SceneManager.ActiveScene;
        if (scene == null)
        {
            _treeView.Items.Add(new TreeViewItem
            {
                Header = "(no scene)",
                Foreground = Brushes.Gray,
            });
            return;
        }

        foreach (var go in scene.RootObjects)
        {
            if (go.IsDestroyed) continue;
            var item = BuildGameObjectNode(go);

            if (expandedNames.Contains(go.Name))
                item.IsExpanded = true;

            _treeView.Items.Add(item);

            if (go.Name == selectedName)
                item.IsSelected = true;
        }
    }

    /// <summary>GameObject → TreeViewItem。</summary>
    private static TreeViewItem BuildGameObjectNode(GameObject go)
    {
        var activeIcon = go.ActiveSelf ? "●" : "○";
        var compCount = go.Components.Count;
        var header = $"{activeIcon} {go.Name}  [{go.Transform.SortingOrder}] ({compCount} comps)";

        var item = new TreeViewItem
        {
            Header = header,
            Tag = go,
            Foreground = go.ActiveSelf ? Brushes.White : Brushes.Gray,
        };

        // Transform 简略显示
        var t = go.Transform;
        var tItem = new TreeViewItem
        {
            Header = $"📌 Transform  X={t.X:F2}  Y={t.Y:F2}  α={t.Opacity:F2}  FlipX={t.FlipX}  Z={t.SortingOrder}",
            Tag = t,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 180, 120)),
            FontSize = 11,
        };
        item.Items.Add(tItem);

        // 其他组件
        foreach (var comp in go.Components)
        {
            if (comp is Transform) continue;
            var compType = comp.GetType().Name;
            var compSummary = GetComponentSummary(comp);

            var cItem = new TreeViewItem
            {
                Header = $"  {compType}{(comp.Enabled ? "" : " [OFF]")}  {compSummary}",
                Tag = comp,
                Foreground = comp.Enabled
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200))
                    : Brushes.DimGray,
                FontSize = 11,
            };
            item.Items.Add(cItem);
        }

        return item;
    }

    /// <summary>组件的简要摘要（显示在树节点上）。</summary>
    private static string GetComponentSummary(Component comp)
    {
        return comp switch
        {
            SpriteRenderer sr => sr.Sprite != null ? $"→ {sr.Sprite.Name}" : "(no sprite)",
            BackgroundRenderer bg => bg.Sprite != null ? $"→ {bg.Sprite.Name}" : "(no bg)",
            TextRenderer tr => $"\"{Truncate(tr.Content, 30)}\"",
            DialogueBox db => $"\"{Truncate(db.SpeakerName, 15)}: {Truncate(db.Text, 20)}\"",
            ChoiceGroup cg => cg.ChoiceCount > 0 ? $"[{cg.ChoiceCount} options]" : "(hidden)",
            VideoPlayer vp => vp.IsPlaying ? "▶ playing" : "■ stopped",
            AudioSource a => a.Clip != null ? $"→ {a.Clip.Name}" : "(no clip)",
            _ => "",
        };
    }

    private static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= maxLen ? s : s[..(maxLen - 3)] + "...";
    }

    /// <summary>每帧自动刷新（由 GameLoop 调用）。</summary>
    public void AutoRefresh()
    {
        if (_autoRefresh)
            Refresh();
    }
}
