using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Configuration for the Asset Browser Window.
    /// </summary>
    public class AssetBrowserConfig
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Window Configuration
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Window title shown in the title bar.</summary>
        public string Title = "Asset Browser";
        
        /// <summary>Subtitle shown below the title (optional).</summary>
        public string Subtitle;
        
        /// <summary>Description text shown below subtitle (optional).</summary>
        public string Description;
        
        /// <summary>Accent color for the window header.</summary>
        public Color AccentColor = Colors.AccentCyan;
        
        /// <summary>Minimum window size.</summary>
        public Vector2 MinSize = new(450, 400);
        
        /// <summary>Maximum window size.</summary>
        public Vector2 MaxSize = new(700, 700);
        
        /// <summary>Show as utility window (stays on top).</summary>
        public bool ShowAsUtility = true;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Filtering
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Asset types to include. If null or empty, searches for ScriptableObject.
        /// Multiple types will show assets of any matching type.
        /// </summary>
        public Type[] AssetTypes;
        
        /// <summary>
        /// Optional filter function. Return true to include the asset in the list.
        /// Called for each asset found of the specified type(s).
        /// </summary>
        public Func<ScriptableObject, bool> Filter;
        
        /// <summary>
        /// Asset to exclude from the list (useful for "import from other" scenarios).
        /// </summary>
        public UnityEngine.Object ExcludeAsset;
        
        /// <summary>
        /// Custom function to get display name for an asset. 
        /// If null, uses asset.name.
        /// </summary>
        public Func<ScriptableObject, string> GetDisplayName;
        
        /// <summary>
        /// Custom function to get icon for an asset.
        /// If null, uses default icon based on type.
        /// </summary>
        public Func<ScriptableObject, string> GetIcon;
        
        /// <summary>
        /// Custom function to get accent color for an asset row.
        /// If null, uses Colors.GetAssetColor.
        /// </summary>
        public Func<ScriptableObject, Color> GetRowColor;
        
        /// <summary>
        /// Initial sort order. If null, sorts by display name ascending.
        /// </summary>
        public Comparison<ScriptableObject> SortComparer;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Actions
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Action buttons to show for each asset row.
        /// </summary>
        public List<AssetAction> Actions = new();
        
        /// <summary>
        /// Called when an asset row is clicked (single click).
        /// If null, selects and pings the asset.
        /// </summary>
        public Action<ScriptableObject> OnRowClick;
        
        /// <summary>
        /// Called when an asset row is double-clicked.
        /// </summary>
        public Action<ScriptableObject> OnRowDoubleClick;
        
        /// <summary>
        /// Footer buttons shown at the bottom of the window.
        /// </summary>
        public List<FooterAction> FooterActions = new();
        
        /// <summary>
        /// Called when the window is closed.
        /// </summary>
        public Action OnClose;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Display Options
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Show search field.</summary>
        public bool ShowSearch = true;
        
        /// <summary>Show refresh button.</summary>
        public bool ShowRefreshButton = true;
        
        /// <summary>Show asset count in footer.</summary>
        public bool ShowAssetCount = true;
        
        /// <summary>Show asset type badge on each row.</summary>
        public bool ShowTypeBadge = false;
        
        /// <summary>Custom footer text (replaces default count).</summary>
        public Func<int, int, string> GetFooterText;
        
        /// <summary>Group assets by a category. If null, shows flat list.</summary>
        public Func<ScriptableObject, string> GetGroupKey;
        
        /// <summary>Color for group headers. If null, uses AccentColor.</summary>
        public Func<string, Color> GetGroupColor;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Builder Pattern Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Creates a new config with the specified title.</summary>
        public static AssetBrowserConfig Create(string title) => new() { Title = title };
        
        public AssetBrowserConfig WithSubtitle(string subtitle) { Subtitle = subtitle; return this; }
        public AssetBrowserConfig WithDescription(string desc) { Description = desc; return this; }
        public AssetBrowserConfig WithAccentColor(Color color) { AccentColor = color; return this; }
        public AssetBrowserConfig WithSize(Vector2 min, Vector2 max) { MinSize = min; MaxSize = max; return this; }
        
        public AssetBrowserConfig ForType<T>() where T : ScriptableObject 
        { 
            AssetTypes = new[] { typeof(T) }; 
            return this; 
        }
        
        public AssetBrowserConfig ForTypes(params Type[] types) { AssetTypes = types; return this; }
        public AssetBrowserConfig WithFilter(Func<ScriptableObject, bool> filter) { Filter = filter; return this; }
        public AssetBrowserConfig Excluding(UnityEngine.Object asset) { ExcludeAsset = asset; return this; }
        
        public AssetBrowserConfig WithDisplayName(Func<ScriptableObject, string> getter) { GetDisplayName = getter; return this; }
        public AssetBrowserConfig WithIcon(Func<ScriptableObject, string> getter) { GetIcon = getter; return this; }
        public AssetBrowserConfig WithRowColor(Func<ScriptableObject, Color> getter) { GetRowColor = getter; return this; }
        public AssetBrowserConfig WithSort(Comparison<ScriptableObject> comparer) { SortComparer = comparer; return this; }
        
        public AssetBrowserConfig WithAction(string label, Action<ScriptableObject> action, string tooltip = null, Color? color = null, bool closeOnClick = false)
        {
            Actions.Add(new AssetAction { Label = label, Action = action, Tooltip = tooltip, Color = color, CloseWindowOnClick = closeOnClick });
            return this;
        }
        
        public AssetBrowserConfig WithAction(AssetAction action) { Actions.Add(action); return this; }
        
        public AssetBrowserConfig OnClick(Action<ScriptableObject> action) { OnRowClick = action; return this; }
        public AssetBrowserConfig OnDoubleClick(Action<ScriptableObject> action) { OnRowDoubleClick = action; return this; }
        
        public AssetBrowserConfig WithFooterAction(string label, Action action, string tooltip = null)
        {
            FooterActions.Add(new FooterAction { Label = label, Action = action, Tooltip = tooltip });
            return this;
        }
        
        public AssetBrowserConfig GroupBy(Func<ScriptableObject, string> groupKey, Func<string, Color> groupColor = null)
        {
            GetGroupKey = groupKey;
            GetGroupColor = groupColor;
            return this;
        }
        
        public AssetBrowserConfig WithTypeBadges() { ShowTypeBadge = true; return this; }
        public AssetBrowserConfig HideSearch() { ShowSearch = false; return this; }
        public AssetBrowserConfig WithCustomFooter(Func<int, int, string> getter) { GetFooterText = getter; return this; }
    }
    
    /// <summary>
    /// Defines an action button shown on each asset row.
    /// </summary>
    public class AssetAction
    {
        /// <summary>Button label text.</summary>
        public string Label;
        
        /// <summary>Button tooltip.</summary>
        public string Tooltip;
        
        /// <summary>Action to perform when clicked. Receives the asset.</summary>
        public Action<ScriptableObject> Action;
        
        /// <summary>Optional: Only show this action if condition returns true.</summary>
        public Func<ScriptableObject, bool> ShowIf;
        
        /// <summary>Optional: Disable button if condition returns true.</summary>
        public Func<ScriptableObject, bool> DisableIf;
        
        /// <summary>Button background color (null for default).</summary>
        public Color? Color;
        
        /// <summary>Button width (0 for auto).</summary>
        public int Width = 0;
        
        /// <summary>Close the window after this action is performed.</summary>
        public bool CloseWindowOnClick = false;
        
        /// <summary>Icon to show instead of/before label (emoji or unicode).</summary>
        public string Icon;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Presets
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static AssetAction Select(Action<ScriptableObject> onSelect, bool closeWindow = true) => new()
        {
            Label = "Select",
            Tooltip = "Select this asset",
            Action = onSelect,
            Color = Colors.AccentGreen,
            CloseWindowOnClick = closeWindow
        };
        
        public static AssetAction Open() => new()
        {
            Label = "Open",
            Tooltip = "Open in Inspector",
            Action = asset => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
        };
        
        public static AssetAction Ping() => new()
        {
            Icon = "📍",
            Label = "",
            Tooltip = "Ping in Project",
            Action = asset => EditorGUIUtility.PingObject(asset),
            Width = 24
        };
        
        public static AssetAction Visualize(Action<ScriptableObject> visualizer = null) => new()
        {
            Icon = "🔍",
            Label = "",
            Tooltip = "Open in Visualizer",
            Action = visualizer ?? TheForge.OpenVisualizer,
            Width = 24
        };
        
        public static AssetAction Import(Action<ScriptableObject> importAction, bool closeWindow = true) => new()
        {
            Label = "Import",
            Tooltip = "Import data from this asset",
            Action = importAction,
            Color = Colors.AccentCyan,
            CloseWindowOnClick = closeWindow
        };
        
        public static AssetAction Delete(Action<ScriptableObject> deleteAction = null) => new()
        {
            Icon = "🗑",
            Label = "",
            Tooltip = "Delete this asset",
            Action = deleteAction ?? (asset =>
            {
                if (EditorUtility.DisplayDialog("Delete Asset", $"Delete '{asset.name}'?", "Delete", "Cancel"))
                {
                    var path = AssetDatabase.GetAssetPath(asset);
                    AssetDatabase.DeleteAsset(path);
                }
            }),
            Color = Colors.AccentRed,
            Width = 24
        };
        
        public static AssetAction Duplicate() => new()
        {
            Icon = "📋",
            Label = "",
            Tooltip = "Duplicate this asset",
            Action = asset =>
            {
                var path = AssetDatabase.GetAssetPath(asset);
                var newPath = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CopyAsset(path, newPath);
                AssetDatabase.Refresh();
            },
            Width = 24
        };
    }
    
    /// <summary>
    /// Defines a button shown in the footer area.
    /// </summary>
    public class FooterAction
    {
        public string Label;
        public string Tooltip;
        public Action Action;
        public Color? Color;
        public bool FlexGrow = false;
    }
    
    /// <summary>
    /// Generic window for browsing and acting on assets.
    /// Opened via static Show() method with configuration options.
    /// </summary>
    public class AssetBrowserWindow : EditorWindow
    {
        private AssetBrowserConfig _config;
        private List<ScriptableObject> _allAssets = new();
        private List<ScriptableObject> _filteredAssets = new();
        private string _searchFilter = "";
        private ScrollView _scrollView;
        private Label _footerLabel;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Static Show Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Opens the asset browser with the specified configuration.
        /// </summary>
        public static AssetBrowserWindow Show(AssetBrowserConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("AssetBrowserWindow: Config cannot be null");
                return null;
            }

            Debug.Log(config);
            var window = GetWindow<AssetBrowserWindow>(config.ShowAsUtility, config.Title);
            window._config = config;
            window.minSize = config.MinSize;
            window.maxSize = config.MaxSize;
            window.RefreshAssets();
            window.BuildUI();
            
            if (config.ShowAsUtility)
                window.ShowUtility();
            else
                window.Show();
            
            return window;
        }
        
        /// <summary>
        /// Quick show for a single asset type with common options.
        /// </summary>
        public static AssetBrowserWindow Show<T>(
            string title,
            Action<T> onSelect = null,
            Func<T, bool> filter = null,
            UnityEngine.Object exclude = null) where T : ScriptableObject
        {
            var config = AssetBrowserConfig.Create(title)
                .ForType<T>()
                .Excluding(exclude);
            
            if (filter != null)
                config.WithFilter(so => filter((T)so));
            
            if (onSelect != null)
                config.WithAction(AssetAction.Select(so => onSelect((T)so)));
            else
                config.WithAction(AssetAction.Open());
            
            return Show(config);
        }
        
        /// <summary>
        /// Quick picker dialog - returns selected asset via callback.
        /// </summary>
        public static void Pick<T>(string title, Action<T> onPicked, Func<T, bool> filter = null) where T : ScriptableObject
        {
            var config = AssetBrowserConfig.Create(title)
                .ForType<T>()
                .WithSubtitle($"Select a {typeof(T).Name}")
                .WithAction(AssetAction.Select(so => onPicked?.Invoke((T)so), closeWindow: true));
            
            if (filter != null)
                config.WithFilter(so => filter((T)so));
            
            Show(config);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void OnDestroy()
        {
            _config?.OnClose?.Invoke();
        }
        
        private void CreateGUI()
        {
            if (_config != null) BuildUI();
        }

        private void BuildUI()
        {
            if (_config == null) return;
            
            var root = rootVisualElement;
            root.Clear();
            
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            BuildHeader(root);
            BuildSearchBar(root);
            BuildAssetList(root);
            BuildFooter(root);
            
            // Initial refresh
            root.schedule.Execute(() =>
            {
                if (_allAssets.Count == 0) RefreshAssets();
                RebuildList();
            });
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Building
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildHeader(VisualElement root)
        {
            // Title
            var titleLabel = new Label(_config.Title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = _config.AccentColor;
            titleLabel.style.marginBottom = 2;
            root.Add(titleLabel);
            
            // Subtitle
            if (!string.IsNullOrEmpty(_config.Subtitle))
            {
                var subtitleLabel = new Label(_config.Subtitle);
                subtitleLabel.style.fontSize = 11;
                subtitleLabel.style.color = Colors.LabelText;
                subtitleLabel.style.marginBottom = 2;
                root.Add(subtitleLabel);
            }
            
            // Description
            if (!string.IsNullOrEmpty(_config.Description))
            {
                var descLabel = new Label(_config.Description);
                descLabel.style.fontSize = 10;
                descLabel.style.color = Colors.HintText;
                descLabel.style.marginBottom = 8;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                root.Add(descLabel);
            }
        }
        
        private void BuildSearchBar(VisualElement root)
        {
            if (!_config.ShowSearch && !_config.ShowRefreshButton) return;
            
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 8;
            
            if (_config.ShowSearch)
            {
                var searchIcon = new Label("🔍");
                searchIcon.style.alignSelf = Align.Center;
                searchIcon.style.marginRight = 4;
                searchIcon.style.color = Colors.HintText;
                searchRow.Add(searchIcon);
                
                var searchField = new TextField { value = _searchFilter };
                searchField.style.flexGrow = 1;
                searchField.RegisterValueChangedCallback(evt =>
                {
                    _searchFilter = evt.newValue;
                    ApplyFilter();
                    RebuildList();
                });
                searchRow.Add(searchField);
            }
            
            if (_config.ShowRefreshButton)
            {
                var refreshBtn = new Button(() => { RefreshAssets(); RebuildList(); }) 
                { 
                    text = "↻", 
                    tooltip = "Refresh asset list" 
                };
                refreshBtn.style.width = 24;
                refreshBtn.style.marginLeft = 4;
                searchRow.Add(refreshBtn);
            }
            
            root.Add(searchRow);
        }
        
        private void BuildAssetList(VisualElement root)
        {
            _scrollView = new ScrollView { name = "AssetList" };
            _scrollView.style.flexGrow = 1;
            _scrollView.style.backgroundColor = Colors.SubsectionBackground;
            _scrollView.style.borderTopLeftRadius = 4;
            _scrollView.style.borderTopRightRadius = 4;
            _scrollView.style.borderBottomLeftRadius = 4;
            _scrollView.style.borderBottomRightRadius = 4;
            _scrollView.style.paddingTop = 4;
            _scrollView.style.paddingBottom = 4;
            root.Add(_scrollView);
        }
        
        private void BuildFooter(VisualElement root)
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 8;
            
            // Asset count label
            if (_config.ShowAssetCount)
            {
                _footerLabel = new Label();
                _footerLabel.style.fontSize = 10;
                _footerLabel.style.color = Colors.HintText;
                _footerLabel.style.flexGrow = 1;
                footer.Add(_footerLabel);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                footer.Add(spacer);
            }
            
            // Footer action buttons
            foreach (var action in _config.FooterActions)
            {
                var btn = new Button(() => action.Action?.Invoke()) { text = action.Label };
                btn.tooltip = action.Tooltip;
                btn.style.marginLeft = 4;
                btn.style.paddingLeft = 12;
                btn.style.paddingRight = 12;
                
                if (action.Color.HasValue)
                    btn.style.backgroundColor = action.Color.Value;
                
                if (action.FlexGrow)
                    btn.style.flexGrow = 1;
                
                footer.Add(btn);
            }
            
            // Default close button if no footer actions
            if (_config.FooterActions.Count == 0)
            {
                var closeBtn = new Button(Close) { text = "Close" };
                closeBtn.style.paddingLeft = 16;
                closeBtn.style.paddingRight = 16;
                footer.Add(closeBtn);
            }
            
            root.Add(footer);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Loading
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void RefreshAssets()
        {
            _allAssets.Clear();
            
            var types = _config.AssetTypes;
            if (types == null || types.Length == 0)
                types = new[] { typeof(ScriptableObject) };
            
            foreach (var type in types)
            {
                var guids = AssetDatabase.FindAssets($"t:{type.Name}");
                
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
                    
                    if (asset == null) continue;
                    if (asset == _config.ExcludeAsset) continue;
                    if (_config.Filter != null && !_config.Filter(asset)) continue;
                    
                    // Avoid duplicates if asset matches multiple types
                    if (!_allAssets.Contains(asset))
                        _allAssets.Add(asset);
                }
            }
            
            // Sort
            if (_config.SortComparer != null)
            {
                _allAssets.Sort(_config.SortComparer);
            }
            else
            {
                _allAssets = _allAssets.OrderBy(a => GetDisplayName(a)).ToList();
            }
            
            ApplyFilter();
        }
        
        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(_searchFilter))
            {
                _filteredAssets = new List<ScriptableObject>(_allAssets);
            }
            else
            {
                var filterLower = _searchFilter.ToLowerInvariant();
                _filteredAssets = _allAssets
                    .Where(a => GetDisplayName(a).ToLowerInvariant().Contains(filterLower) ||
                                a.name.ToLowerInvariant().Contains(filterLower))
                    .ToList();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // List Building
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void RebuildList()
        {
            if (_scrollView == null) return;
            _scrollView.Clear();
            
            UpdateFooter();
            
            if (_filteredAssets.Count == 0)
            {
                var emptyLabel = new Label(_allAssets.Count == 0 ? "No assets found" : "No matches");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 30;
                emptyLabel.style.paddingBottom = 30;
                _scrollView.Add(emptyLabel);
                return;
            }
            
            // Grouped or flat list?
            if (_config.GetGroupKey != null)
            {
                BuildGroupedList();
            }
            else
            {
                BuildFlatList();
            }
        }
        
        private void BuildFlatList()
        {
            foreach (var asset in _filteredAssets)
            {
                _scrollView.Add(CreateAssetRow(asset));
            }
        }
        
        private void BuildGroupedList()
        {
            var groups = _filteredAssets
                .GroupBy(a => _config.GetGroupKey(a) ?? "(Ungrouped)")
                .OrderBy(g => g.Key);
            
            foreach (var group in groups)
            {
                // Group header
                var headerColor = _config.GetGroupColor?.Invoke(group.Key) ?? _config.AccentColor;
                var header = CreateGroupHeader(group.Key, group.Count(), headerColor);
                _scrollView.Add(header);
                
                // Assets in group
                foreach (var asset in group)
                {
                    _scrollView.Add(CreateAssetRow(asset));
                }
            }
        }
        
        private VisualElement CreateGroupHeader(string groupName, int count, Color color)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 4;
            header.style.marginTop = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = color.Fade(0.3f);
            
            var icon = new Label("▸");
            icon.style.color = color;
            icon.style.fontSize = 10;
            icon.style.marginRight = 4;
            header.Add(icon);
            
            var nameLabel = new Label(groupName);
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = color;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);
            
            var countLabel = new Label($"({count})");
            countLabel.style.fontSize = 10;
            countLabel.style.color = Colors.HintText;
            countLabel.style.marginRight = 8;
            header.Add(countLabel);
            
            return header;
        }
        
        private VisualElement CreateAssetRow(ScriptableObject asset)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.marginLeft = 4;
            row.style.marginRight = 4;
            row.style.marginBottom = 2;
            row.style.backgroundColor = Colors.ItemBackground;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            
            // Hover effect
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Colors.ButtonHover);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Colors.ItemBackground);
            
            // Click behavior
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2 && _config.OnRowDoubleClick != null)
                {
                    _config.OnRowDoubleClick(asset);
                }
                else if (evt.clickCount == 1)
                {
                    if (_config.OnRowClick != null)
                        _config.OnRowClick(asset);
                    else
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            });
            
            // Icon
            var iconText = _config.GetIcon?.Invoke(asset) ?? "◆";
            var iconColor = _config.GetRowColor?.Invoke(asset) ?? Colors.GetAssetColor(asset.GetType());
            var icon = new Label(iconText);
            icon.style.color = iconColor;
            icon.style.fontSize = 12;
            icon.style.width = 20;
            icon.style.marginRight = 6;
            row.Add(icon);
            
            // Display name
            var displayName = GetDisplayName(asset);
            var nameLabel = new Label(displayName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = Colors.LabelText;
            nameLabel.style.fontSize = 11;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.tooltip = $"{displayName}\n{AssetDatabase.GetAssetPath(asset)}";
            row.Add(nameLabel);
            
            // Type badge
            if (_config.ShowTypeBadge)
            {
                var typeName = asset.GetType().Name;
                var badge = new Label(typeName);
                badge.style.fontSize = 9;
                badge.style.color = iconColor;
                badge.style.backgroundColor = iconColor.Fade(0.15f);
                badge.style.paddingLeft = 4;
                badge.style.paddingRight = 4;
                badge.style.paddingTop = 1;
                badge.style.paddingBottom = 1;
                badge.style.marginRight = 8;
                badge.style.borderTopLeftRadius = 3;
                badge.style.borderTopRightRadius = 3;
                badge.style.borderBottomLeftRadius = 3;
                badge.style.borderBottomRightRadius = 3;
                row.Add(badge);
            }
            
            // Action buttons
            foreach (var action in _config.Actions)
            {
                if (action.ShowIf != null && !action.ShowIf(asset))
                    continue;
                
                var btnText = string.IsNullOrEmpty(action.Icon) ? action.Label : 
                              string.IsNullOrEmpty(action.Label) ? action.Icon : $"{action.Icon} {action.Label}";
                
                var btn = new Button(() =>
                {
                    action.Action?.Invoke(asset);
                    if (action.CloseWindowOnClick)
                        Close();
                })
                {
                    text = btnText,
                    tooltip = action.Tooltip
                };
                
                btn.style.marginLeft = 4;
                btn.style.height = 20;
                btn.style.fontSize = 10;
                
                if (action.Width > 0)
                    btn.style.width = action.Width;
                else
                {
                    btn.style.paddingLeft = 8;
                    btn.style.paddingRight = 8;
                }
                
                if (action.Color.HasValue)
                    btn.style.backgroundColor = action.Color.Value;
                
                if (action.DisableIf != null && action.DisableIf(asset))
                    btn.SetEnabled(false);
                
                row.Add(btn);
            }
            
            return row;
        }
        
        private void UpdateFooter()
        {
            if (_footerLabel == null) return;
            
            if (_config.GetFooterText != null)
            {
                _footerLabel.text = _config.GetFooterText(_filteredAssets.Count, _allAssets.Count);
            }
            else
            {
                var typeNames = _config.AssetTypes?.Select(t => t.Name).ToArray() ?? new[] { "asset" };
                var typeName = typeNames.Length == 1 ? typeNames[0] : "asset";
                
                if (_filteredAssets.Count == _allAssets.Count)
                    _footerLabel.text = $"{_allAssets.Count} {typeName}(s)";
                else
                    _footerLabel.text = $"{_filteredAssets.Count} of {_allAssets.Count} {typeName}(s)";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string GetDisplayName(ScriptableObject asset)
        {
            if (_config.GetDisplayName != null)
                return _config.GetDisplayName(asset);
            
            // Try common name patterns
            var so = new SerializedObject(asset);
            
            // Check Definition.Name
            var nameProp = so.FindProperty("Definition.Name");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                return nameProp.stringValue;
            
            // Check _name or Name directly
            nameProp = so.FindProperty("_name") ?? so.FindProperty("Name");
            if (nameProp != null && nameProp.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(nameProp.stringValue))
                return nameProp.stringValue;
            
            return asset.name;
        }
    }
}
