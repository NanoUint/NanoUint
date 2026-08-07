using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer ObjectExplorerPanel 1:1 复刻。</summary>
internal sealed class ObjectExplorerPanel : UEPanel
{
    private StackPanel _sceneRows = null!;
    private StackPanel _searchRows = null!;
    private Grid _sceneContent;
    private Grid _searchContent;
    private Button _sceneTabBtn;
    private Button _searchTabBtn;
    private TextBox _filterInput;
    private TextBox _searchInput;
    private TextBlock _sceneNameText;
    private readonly HashSet<GameObject> _expanded = new();
    private readonly DispatcherTimer _autoRefresh;
    private string _filter = "";
    private string _searchQuery = "";

    /// <summary>树节点/搜索结果被点击(选中 GameObject)。</summary>
    public event Action<GameObject>? OnGameObjectSelected;

    public ObjectExplorerPanel(Canvas parentCanvas)
        : base(parentCanvas, "ObjectExplorer", "ObjectExplorer", UEPalette.TreeScroll)
    {
        DefaultLeft = 0.125;
        DefaultTop = 0.175;
        DefaultWidth = 0.20;
        DefaultHeight = 0.75;
        MinPanelWidth = 350;
        MinPanelHeight = 200;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        #region TabBar: Scene Explorer / Object Search
        var tabBar = new Grid { Background = new SolidColorBrush(UEPalette.TabBarBackground) };
        tabBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tabBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sceneTabBtn = UEFactory.Button("Scene Explorer", double.NaN, 25, UEPalette.ButtonEnabled, 12);
        _sceneTabBtn.Click += (_, _) => SwitchTab(0);
        UEFactory.SetCell(_sceneTabBtn, tabBar, 0);

        _searchTabBtn = UEFactory.Button("Object Search", double.NaN, 25, UEPalette.ButtonDisabled, 12);
        _searchTabBtn.Click += (_, _) => SwitchTab(1);
        UEFactory.SetCell(_searchTabBtn, tabBar, 1);

        Grid.SetRow(tabBar, 0);
        root.Children.Add(tabBar);

        #endregion

        #region Scene Explorer 内容
        _sceneContent = BuildSceneExplorer();
        Grid.SetRow(_sceneContent, 1);
        root.Children.Add(_sceneContent);

        #endregion

        #region Object Search 内容
        _searchContent = BuildObjectSearch();
        Grid.SetRow(_searchContent, 1);
        root.Children.Add(_searchContent);

        ContentHost.Children.Add(root);

        // 每秒自动刷新
        _autoRefresh = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _autoRefresh.Tick += (_, _) =>
        {
            if (IsVisible) RebuildTree();
        };
        _autoRefresh.Start();
        #endregion
    }

    #region Scene Explorer

    private Grid BuildSceneExplorer()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Filter
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // 列头
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 树
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // SceneLoader

        #region Toolbar (#262626): "Scene:" cyan + 场景下拉
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(UEPalette.Toolbar),
            Height = 26,
        };
        var sceneLabel = UEFactory.Label("Scene:", 15, UEPalette.TextCyan);
        sceneLabel.Margin = new Thickness(4, 0, 4, 0);
        sceneLabel.Width = 60;
        toolbar.Children.Add(sceneLabel);

        _sceneNameText = UEFactory.Label("(none)", 13, UEPalette.TextDefault);
        toolbar.Children.Add(_sceneNameText);
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        #endregion

        #region 过滤输入框
        _filterInput = UEFactory.Input(double.NaN, 25, "", UEPalette.FilterNormal, UEPalette.FilterNormal, 12);
        var filterHost = new Grid { Background = new SolidColorBrush(UEPalette.FilterNormal) };
        filterHost.Children.Add(_filterInput);
        _filterInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _filter = _filterInput.Text.Trim();
                RebuildTree();
            }
        };
        _filterInput.ToolTip = "Search and press enter...";
        Grid.SetRow(filterHost, 1);
        root.Children.Add(filterHost);

        #endregion

        #region 列头行: Name / Sibling Index
        var header = new Grid { Background = new SolidColorBrush(UEPalette.Toolbar) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        var nameHeader = UEFactory.Label("Name", 12, UEPalette.TextInactive);
        nameHeader.Margin = new Thickness(6, 0, 0, 0);
        UEFactory.SetCell(nameHeader, header, 0);
        var siblingHeader = UEFactory.Label("Sibling Index", 12, UEPalette.TextInactive);
        siblingHeader.HorizontalAlignment = HorizontalAlignment.Right;
        siblingHeader.Margin = new Thickness(0, 0, 6, 0);
        UEFactory.SetCell(siblingHeader, header, 1);
        Grid.SetRow(header, 2);
        root.Children.Add(header);

        #endregion

        #region 树
        _sceneRows = new StackPanel();
        var scroller = new ScrollViewer
        {
            Content = _sceneRows,
            Background = new SolidColorBrush(UEPalette.TreeScroll),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        UEFactory.ApplyScrollbarStyle(scroller);
        Grid.SetRow(scroller, 3);
        root.Children.Add(scroller);

        #endregion

        #region SceneLoader (标题 + 下拉 + Load 按钮, NanoUint 无场景文件加载 → 禁用)
        var loader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(UEPalette.Toolbar),
            Height = 26,
        };
        var loaderTitle = UEFactory.Label("SceneLoader", 14, UEPalette.TextDefault);
        loaderTitle.Margin = new Thickness(4, 0, 6, 0);
        loader.Children.Add(loaderTitle);

        var loaderDropdown = new UEDropdown(120, 20, 12);
        loaderDropdown.Items = new[] { SceneManager.ActiveScene?.Name ?? "(none)" };
        loaderDropdown.DisplayText = SceneManager.ActiveScene?.Name ?? "(none)";
        loaderDropdown.Margin = new Thickness(0, 0, 6, 0);
        loader.Children.Add(loaderDropdown);

        var loadBtn = UEFactory.Button("Load (Single)", 150, 25, UEPalette.LoadButton, 12);
        loadBtn.IsEnabled = false;
        loader.Children.Add(loadBtn);
        Grid.SetRow(loader, 4);
        root.Children.Add(loader);

        return root;
        #endregion
    }

    #endregion

    #region Object Search

    private Grid BuildObjectSearch()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) }); // 搜索框
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 结果

        _searchInput = UEFactory.Input(double.NaN, 26, "", UEPalette.FilterNormal, UEPalette.FilterNormal, 12);
        _searchInput.ToolTip = "Search by name or type...";
        _searchInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _searchQuery = _searchInput.Text.Trim();
                RebuildSearch();
            }
        };
        Grid.SetRow(_searchInput, 0);
        root.Children.Add(_searchInput);

        _searchRows = new StackPanel();
        var scroller = new ScrollViewer
        {
            Content = _searchRows,
            Background = new SolidColorBrush(UEPalette.TreeScroll),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        UEFactory.ApplyScrollbarStyle(scroller);
        Grid.SetRow(scroller, 1);
        root.Children.Add(scroller);

        return root;
    }

    #endregion

    #region Tab 切换

    private void SwitchTab(int index)
    {
        _sceneContent.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchContent.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        _sceneTabBtn.Background = new SolidColorBrush(index == 0 ? UEPalette.ButtonEnabled : UEPalette.ButtonDisabled);
        _searchTabBtn.Background = new SolidColorBrush(index == 1 ? UEPalette.ButtonEnabled : UEPalette.ButtonDisabled);
        if (index == 0) RebuildTree();
    }

    #endregion

    #region 树构建

    /// <summary>重建场景树(Scene 加载后由宿主调用)。</summary>
    public void Refresh()
    {
        RebuildTree();
    }

    private void RebuildTree()
    {
        var scene = SceneManager.ActiveScene;
        if (scene == null)
        {
            _sceneNameText.Text = "(none)";
            _sceneRows.Children.Clear();
            _sceneRows.Children.Add(UEFactory.Label("(no scene)", 12, UEPalette.TextInactive, FontStyles.Italic));
            return;
        }

        _sceneNameText.Text = scene.Name;
        _sceneRows.Children.Clear();

        foreach (var go in scene.RootObjects)
            AddNode(go, 0, scene.RootObjects.Count);
    }

    private void AddNode(GameObject go, int depth, int siblingCount)
    {
        if (!MatchesFilter(go)) return;

        _sceneRows.Children.Add(BuildRow(go, depth, siblingCount));

        if (_expanded.Contains(go) && go.Transform.Children.Count > 0)
        {
            foreach (var child in go.Transform.Children)
            {
                if (child.GameObject != null)
                    AddNode(child.GameObject, depth + 1, go.Transform.Children.Count);
            }
        }
    }

    private bool MatchesFilter(GameObject go)
    {
        if (string.IsNullOrWhiteSpace(_filter)) return true;
        if (go.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var comp in go.Components)
        {
            if (comp.GetType().Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private Border BuildRow(GameObject go, int depth, int siblingCount)
    {
        var row = new Border
        {
            Height = 25,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(depth * 15) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        // 展开箭头
        var hasChildren = go.Transform.Children.Count > 0;
        var isExpanded = _expanded.Contains(go);
        var arrow = UEFactory.Label(
            !hasChildren ? "▪" : isExpanded ? "▼" : "►",
            10,
            hasChildren
                ? isExpanded ? UEPalette.ArrowExpanded : UEPalette.ArrowCollapsed
                : UEPalette.ArrowCollapsed);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        arrow.Cursor = Cursors.Hand;
        if (hasChildren)
        {
            arrow.MouseLeftButtonDown += (_, _) =>
            {
                if (!_expanded.Remove(go)) _expanded.Add(go);
                RebuildTree();
            };
        }
        UEFactory.SetCell(arrow, grid, 1);

        // active toggle
        var toggle = UEFactory.Check(go.ActiveSelf, v => go.ActiveSelf = v,
            UEPalette.BehaviourToggleGraphic);
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.HorizontalAlignment = HorizontalAlignment.Center;
        UEFactory.SetCell(toggle, grid, 2);

        // 名称
        var nameColor = go.IsDestroyed
            ? UEPalette.TextDestroyed
            : go.ActiveSelf ? UEPalette.TextDefault : UEPalette.TextInactive;
        var nameText = go.IsDestroyed ? $"{go.Name} [Destroyed]" : go.Name;
        var nameLabel = UEFactory.Label(nameText, 12, nameColor);
        nameLabel.Margin = new Thickness(4, 0, 0, 0);
        nameLabel.Cursor = Cursors.Hand;
        nameLabel.MouseLeftButtonDown += (_, _) => OnGameObjectSelected?.Invoke(go);
        UEFactory.SetCell(nameLabel, grid, 3);

        // sibling index
        var siblingIndex = GetSiblingIndex(go);
        var siblingBox = UEFactory.Input(35, 20, siblingIndex.ToString(),
            UEPalette.SiblingInputBackground, UEPalette.InputBorder, 11, readOnly: true);
        siblingBox.HorizontalAlignment = HorizontalAlignment.Right;
        siblingBox.Margin = new Thickness(0, 0, 6, 0);
        siblingBox.VerticalAlignment = VerticalAlignment.Center;
        siblingBox.ToolTip = "Sibling Index";
        UEFactory.SetCell(siblingBox, grid, 4);

        // 子计数 [n] 灰色后缀(附加到名称后面)
        if (hasChildren)
        {
            var countRun = new Run($" [{go.Transform.Children.Count}]")
            {
                Foreground = new SolidColorBrush(UEPalette.TextInactive),
                FontSize = 11,
            };
            nameLabel.Inlines.Add(countRun);
        }

        row.Child = grid;

        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(UEPalette.TreeNodeHover);
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

        _ = siblingCount;
        return row;
    }

    private static int GetSiblingIndex(GameObject go)
    {
        var t = go.Transform;
        if (t.Parent == null)
        {
            var scene = SceneManager.ActiveScene;
            if (scene == null) return 0;
            for (int i = 0; i < scene.RootObjects.Count; i++)
            {
                if (scene.RootObjects[i] == go) return i;
            }
            return 0;
        }
        for (int i = 0; i < t.Parent.Children.Count; i++)
        {
            if (t.Parent.Children[i] == t) return i;
        }
        return 0;
    }

    #endregion

    #region Object Search

    private void RebuildSearch()
    {
        _searchRows.Children.Clear();

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchRows.Children.Add(UEFactory.Label("Type to search...", 12, UEPalette.TextInactive, FontStyles.Italic));
            return;
        }

        var scene = SceneManager.ActiveScene;
        if (scene == null) return;

        var results = new List<(GameObject Go, string Type)>();
        void Walk(GameObject go)
        {
            if (MatchesSearch(go, out var type)) results.Add((go, type));
            foreach (var child in go.Transform.Children)
            {
                if (child.GameObject != null) Walk(child.GameObject);
            }
        }

        foreach (var root in scene.RootObjects)
            Walk(root);

        foreach (var (go, type) in results)
        {
            var row = new Border { Height = 25, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameLabel = UEFactory.Label(go.Name, 12,
                go.IsDestroyed ? UEPalette.TextDestroyed : go.ActiveSelf ? UEPalette.TextDefault : UEPalette.TextInactive);
            nameLabel.Margin = new Thickness(6, 0, 0, 0);
            nameLabel.MouseLeftButtonDown += (_, _) => OnGameObjectSelected?.Invoke(go);
            UEFactory.SetCell(nameLabel, grid, 0);

            var typeLabel = UEFactory.Label(type, 11, UEPalette.TextInactive);
            typeLabel.Margin = new Thickness(0, 0, 6, 0);
            UEFactory.SetCell(typeLabel, grid, 1);

            row.Child = grid;
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(UEPalette.TreeNodeHover);
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            _searchRows.Children.Add(row);
        }

        if (results.Count == 0)
        {
            _searchRows.Children.Add(UEFactory.Label("(no results)", 12, UEPalette.TextInactive, FontStyles.Italic));
        }
    }

    private bool MatchesSearch(GameObject go, out string typeName)
    {
        typeName = "";
        if (go.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
        {
            typeName = "GameObject";
            return true;
        }
        foreach (var comp in go.Components)
        {
            var name = comp.GetType().Name;
            if (name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
            {
                typeName = name;
                return true;
            }
        }
        return false;
    }
    #endregion
}
