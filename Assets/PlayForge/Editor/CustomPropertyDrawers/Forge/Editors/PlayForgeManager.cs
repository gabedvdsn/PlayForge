using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class PlayForgeManager : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Constants & Static Data
        // ═══════════════════════════════════════════════════════════════════════════
        
        private const string WINDOW_TITLE = "PlayForge Manager";
        private const string PREFS_PREFIX = "PlayForge_";
        private const int COLOR_INDICATOR_WIDTH = 3;
        private const string TAGS_VIEW_MARKER = "__TAGS__";
        
        // Default paths (configurable in settings)
        private static readonly Dictionary<Type, string> DefaultPaths = new Dictionary<Type, string>
        {
            { typeof(Attribute), "Assets/Data/Attributes" },
            { typeof(AttributeSet), "Assets/Data/AttributeSets" },
            { typeof(GameplayEffect), "Assets/Data/Effects" },
            { typeof(EntityIdentity), "Assets/Data/Entities" },
            { typeof(Ability), "Assets/Data/Abilities" }
        };
        
        // Asset type metadata for UI
        public static readonly List<AssetTypeInfo> AssetTypes = new List<AssetTypeInfo>
        {
            new AssetTypeInfo(typeof(Ability), "Ability", "⚡", Colors.AccentOrange, "Ability definition", true),
            new AssetTypeInfo(typeof(GameplayEffect), "Effect", "✦", Colors.AccentRed, "Gameplay effect definition", true),
            new AssetTypeInfo(typeof(Attribute), "Attribute", "📈", Colors.AccentBlue, "Base stat definition", true),
            new AssetTypeInfo(typeof(AttributeSet), "Attribute Set", "📊", Colors.AccentGreen, "Collection of attributes", true),
            new AssetTypeInfo(typeof(EntityIdentity), "Entity", "👤", Colors.AccentPurple, "Entity configuration", true),
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private int currentTab = 0;
        private readonly string[] tabNames = { "Create", "View", "Settings" };
        
        // View tab state
        private string searchFilter = "";
        private Type selectedTypeFilter = null;
        private bool showTagsView = false;
        private List<ScriptableObject> cachedAssets = new List<ScriptableObject>();
        private Dictionary<Tag, TagUsageInfo> cachedTagUsage = new Dictionary<Tag, TagUsageInfo>();
        
        // UI References
        private VisualElement root;
        private VisualElement contentContainer;
        private List<Button> tabButtons = new List<Button>();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Window Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        [MenuItem("Tools/PlayForge/Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayForgeManager>();
            window.titleContent = new GUIContent(WINDOW_TITLE, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(600, 400);
        }
        
        /// <summary>
        /// Opens the Visualizer window for the specified asset.
        /// </summary>
        public static void OpenVisualizer(ScriptableObject asset)
        {
            if (asset == null) return;
            
            var window = GetWindow<PlayForgeVisualizer>();
            window.SetAsset(asset);
            window.Show();
        }
        
        /// <summary>
        /// Gets the display name for an asset.
        /// </summary>
        public static string GetAssetDisplayName(ScriptableObject asset)
        {
            return asset switch
            {
                Ability a => !string.IsNullOrEmpty(a.GetName()) ? a.GetName() : a.name,
                GameplayEffect e => !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name,
                Attribute attr => !string.IsNullOrEmpty(attr.Name) ? attr.Name : attr.name,
                EntityIdentity ent => !string.IsNullOrEmpty(ent.GetName()) ? ent.GetName() : ent.name,
                _ => asset.name
            };
        }
        
        private void OnEnable()
        {
            RefreshAssetCache();
            
            // Restore last selected type filter if enabled
            if (EditorPrefs.GetBool(PREFS_PREFIX + "RememberTypeFilter", true))
            {
                var lastType = EditorPrefs.GetString(PREFS_PREFIX + "LastTypeFilter", "");
                if (lastType == TAGS_VIEW_MARKER)
                {
                    showTagsView = true;
                    selectedTypeFilter = null;
                }
                else if (!string.IsNullOrEmpty(lastType))
                {
                    selectedTypeFilter = AssetTypes.FirstOrDefault(t => t.Type.Name == lastType)?.Type;
                }
            }
        }
        
        private void CreateGUI()
        {
            root = rootVisualElement;
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            BuildHeader();
            BuildTabBar();
            BuildContentArea();
            
            ShowTab(currentTab);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = Colors.HeaderBackground;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;
            root.Add(header);
            
            var titleLabel = new Label("⚒ PLAYFORGE");
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.letterSpacing = 2;
            header.Add(titleLabel);
            
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            
            var statsContainer = new VisualElement();
            statsContainer.style.flexDirection = FlexDirection.Row;
            statsContainer.style.alignItems = Align.Center;
            header.Add(statsContainer);
            
            foreach (var typeInfo in AssetTypes.Take(4))
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var stat = CreateCompactStat(typeInfo.Icon, count.ToString(), typeInfo.Color);
                stat.tooltip = $"{count} {typeInfo.DisplayName}(s)";
                statsContainer.Add(stat);
            }
            
            // Tags stat
            var tagStat = CreateCompactStat("🏷", cachedTagUsage.Count.ToString(), Colors.AccentCyan);
            tagStat.tooltip = $"{cachedTagUsage.Count} unique tag(s)";
            statsContainer.Add(tagStat);
            
            var refreshBtn = CreateButton("↻", RefreshAll, "Refresh asset cache");
            refreshBtn.style.marginLeft = 12;
            refreshBtn.style.width = 28;
            refreshBtn.style.height = 28;
            ApplyButtonStyle(refreshBtn);
            header.Add(refreshBtn);
        }
        
        private VisualElement CreateCompactStat(string icon, string value, Color color)
        {
            var stat = new VisualElement();
            stat.style.flexDirection = FlexDirection.Row;
            stat.style.alignItems = Align.Center;
            stat.style.marginLeft = 8;
            stat.style.paddingLeft = 6;
            stat.style.paddingRight = 6;
            stat.style.paddingTop = 2;
            stat.style.paddingBottom = 2;
            stat.style.backgroundColor = new Color(color.r, color.g, color.b, 0.2f);
            stat.style.borderTopLeftRadius = 4;
            stat.style.borderTopRightRadius = 4;
            stat.style.borderBottomLeftRadius = 4;
            stat.style.borderBottomRightRadius = 4;
            
            var iconLabel = new Label(icon);
            iconLabel.style.fontSize = 10;
            iconLabel.style.marginRight = 4;
            stat.Add(iconLabel);
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = color;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            stat.Add(valueLabel);
            
            return stat;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tab Bar
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTabBar()
        {
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            tabBar.style.paddingLeft = 8;
            tabBar.style.paddingTop = 4;
            tabBar.style.paddingBottom = 0;
            root.Add(tabBar);
            
            tabButtons.Clear();
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;
                var btn = CreateButton(tabNames[i], () => ShowTab(tabIndex));
                btn.style.paddingLeft = 16;
                btn.style.paddingRight = 16;
                btn.style.paddingTop = 8;
                btn.style.paddingBottom = 8;
                btn.style.marginRight = 2;
                btn.style.borderTopLeftRadius = 4;
                btn.style.borderTopRightRadius = 4;
                btn.style.borderBottomLeftRadius = 0;
                btn.style.borderBottomRightRadius = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.fontSize = 12;
                
                tabButtons.Add(btn);
                tabBar.Add(btn);
            }
        }
        
        private void ShowTab(int index)
        {
            currentTab = index;
            
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool isActive = i == currentTab;
                tabButtons[i].style.backgroundColor = isActive 
                    ? new Color(0.25f, 0.25f, 0.25f, 1f) 
                    : new Color(0.15f, 0.15f, 0.15f, 1f);
                tabButtons[i].style.color = isActive 
                    ? Colors.HeaderText 
                    : Colors.HintText;
            }
            
            contentContainer.Clear();
            
            switch (currentTab)
            {
                case 0: BuildCreateTab(); break;
                case 1: BuildViewTab(); break;
                case 2: BuildSettingsTab(); break;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Content Area
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildContentArea()
        {
            contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            contentContainer.style.paddingLeft = 12;
            contentContainer.style.paddingRight = 12;
            contentContainer.style.paddingTop = 12;
            contentContainer.style.paddingBottom = 12;
            root.Add(contentContainer);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CREATE TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildCreateTab()
        {
            var titleRow = CreateSectionHeader("Quick Create", "Create new PlayForge assets");
            contentContainer.Add(titleRow);
            
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 12;
            contentContainer.Add(grid);
            
            foreach (var typeInfo in AssetTypes.Where(t => t.CanCreate))
            {
                var card = CreateAssetTypeCard(typeInfo);
                grid.Add(card);
            }
            
            contentContainer.Add(CreateDivider(16));
            
            var recentTitle = CreateSectionHeader("Recently Created", "Your latest assets");
            contentContainer.Add(recentTitle);
            
            var recentList = new VisualElement();
            recentList.style.marginTop = 8;
            contentContainer.Add(recentList);
            
            var recentAssets = cachedAssets
                .OrderByDescending(a => File.GetLastWriteTime(AssetDatabase.GetAssetPath(a)))
                .Take(5);
            
            foreach (var asset in recentAssets)
            {
                var row = CreateRecentAssetRow(asset);
                recentList.Add(row);
            }
            
            if (!recentAssets.Any())
            {
                var emptyLabel = new Label("No assets created yet");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.paddingLeft = 8;
                recentList.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateAssetTypeCard(AssetTypeInfo typeInfo)
        {
            var card = new VisualElement();
            card.style.width = 140;
            card.style.height = 100;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 12;
            card.style.paddingBottom = 8;
            card.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = typeInfo.Color;
            
            card.RegisterCallback<MouseEnterEvent>(_ => 
                card.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 1f));
            card.RegisterCallback<MouseLeaveEvent>(_ => 
                card.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f));
            
            var icon = new Label(typeInfo.Icon);
            icon.style.fontSize = 24;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(icon);
            
            var name = new Label(typeInfo.DisplayName);
            name.style.fontSize = 12;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = Colors.HeaderText;
            name.style.marginTop = 4;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(name);
            
            var count = CountAssetsOfType(typeInfo.Type);
            var countBadge = new Label($"{count} existing");
            countBadge.style.fontSize = 9;
            countBadge.style.color = Colors.HintText;
            countBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            countBadge.style.marginTop = 2;
            card.Add(countBadge);
            
            card.RegisterCallback<ClickEvent>(_ => ShowCreateDialog(typeInfo));
            
            return card;
        }
        
        private VisualElement CreateRecentAssetRow(ScriptableObject asset)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 2;
            row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            
            row.RegisterCallback<MouseEnterEvent>(_ => 
                row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f));
            row.RegisterCallback<MouseLeaveEvent>(_ => 
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f));
            
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 12;
            icon.style.width = 20;
            icon.style.color = typeInfo?.Color ?? Colors.HintText;
            row.Add(icon);
            
            var nameLabel = new Label(GetAssetDisplayName(asset));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = Colors.LabelText;
            row.Add(nameLabel);
            
            var path = AssetDatabase.GetAssetPath(asset);
            var time = File.GetLastWriteTime(path);
            var timeLabel = new Label(GetRelativeTime(time));
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = Colors.HintText;
            timeLabel.style.marginRight = 8;
            row.Add(timeLabel);
            
            row.RegisterCallback<ClickEvent>(_ => 
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });
            
            return row;
        }
        
        private void ShowCreateDialog(AssetTypeInfo typeInfo)
        {
            var dialog = CreateInstance<CreateAssetDialog>();
            dialog.Initialize(typeInfo, GetPathForType(typeInfo.Type), () =>
            {
                RefreshAssetCache();
                ShowTab(currentTab);
            });
            dialog.ShowUtility();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW TAB - Spreadsheet Style
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string currentSortColumn = "Name";
        private bool sortAscending = true;
        private List<Button> typeFilterButtons = new List<Button>();
        
        private static readonly Color ColumnBorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        
        // Column definitions per type
        private static readonly Dictionary<Type, List<ColumnDef>> ColumnDefinitions = new Dictionary<Type, List<ColumnDef>>
        {
            { typeof(Ability), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetAbilityName((Ability)a)),
                new ColumnDef("Policy", 120, a => ((Ability)a).Definition.ActivationPolicy.ToString()),
                new ColumnDef("Start Lvl", 70, a => ((Ability)a).StartingLevel.ToString()),
                new ColumnDef("Max Lvl", 70, a => ((Ability)a).MaxLevel.ToString()),
                new ColumnDef("Cost", 100, a => GetAbilityCostSummary((Ability)a), true),
                new ColumnDef("Cooldown", 100, a => GetAbilityCooldownSummary((Ability)a), true),
                new ColumnDef("Stages", 60, a => ((Ability)a).Proxy?.Stages?.Count.ToString() ?? "0"),
            }},
            { typeof(GameplayEffect), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetEffectName((GameplayEffect)a)),
                new ColumnDef("Duration", 120, a => GetEffectDuration((GameplayEffect)a)),
                new ColumnDef("Impact", 120, a => GetEffectImpact((GameplayEffect)a)),
                new ColumnDef("Workers", 70, a => ((GameplayEffect)a).Workers?.Count.ToString() ?? "0"),
                new ColumnDef("Visibility", 100, a => ((GameplayEffect)a).Definition?.Visibility.ToString() ?? "-"),
            }},
            { typeof(Attribute), new List<ColumnDef> {
                new ColumnDef("Name", 200, a => GetAttributeName((Attribute)a)),
                new ColumnDef("Description", 400, a => GetAttributeDescription((Attribute)a)),
            }},
            { typeof(AttributeSet), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => a.name),
                new ColumnDef("Attributes", 90, a => ((AttributeSet)a).Attributes?.Count.ToString() ?? "0"),
                new ColumnDef("Subsets", 90, a => ((AttributeSet)a).SubSets?.Count.ToString() ?? "0"),
                new ColumnDef("Unique", 90, a => ((AttributeSet)a).GetUnique()?.Count.ToString() ?? "0"),
                new ColumnDef("Collision", 140, a => ((AttributeSet)a).CollisionResolutionPolicy.ToString()),
            }},
            { typeof(EntityIdentity), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetEntityName((EntityIdentity)a)),
                new ColumnDef("Policy", 120, a => ((EntityIdentity)a).ActivationPolicy.ToString()),
                new ColumnDef("Max Abilities", 100, a => ((EntityIdentity)a).MaxAbilities.ToString()),
                new ColumnDef("Starting", 80, a => ((EntityIdentity)a).StartingAbilities?.Count.ToString() ?? "0"),
                new ColumnDef("Duplicates", 90, a => ((EntityIdentity)a).AllowDuplicateAbilities ? "Yes" : "No"),
            }},
        };
        
        private void BuildViewTab()
        {
            // Type selector chips
            var typeBar = new VisualElement();
            typeBar.style.flexDirection = FlexDirection.Row;
            typeBar.style.flexWrap = Wrap.Wrap;
            typeBar.style.marginBottom = 8;
            contentContainer.Add(typeBar);
            
            typeFilterButtons.Clear();
            
            // "All" button
            var allBtn = CreateTypeFilterButton("All", null, null, false);
            typeBar.Add(allBtn);
            typeFilterButtons.Add(allBtn);
            
            // Asset type buttons
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var btn = CreateTypeFilterButton($"{typeInfo.Icon} {typeInfo.DisplayName}", typeInfo.Type, typeInfo.Color, false, count);
                typeBar.Add(btn);
                typeFilterButtons.Add(btn);
            }
            
            // Tags button (special)
            var tagsBtn = CreateTypeFilterButton("🏷 Tags", null, Colors.AccentCyan, true, cachedTagUsage.Count);
            typeBar.Add(tagsBtn);
            typeFilterButtons.Add(tagsBtn);
            
            UpdateTypeFilterButtons();
            
            // Search bar
            var searchBar = new VisualElement();
            searchBar.style.flexDirection = FlexDirection.Row;
            searchBar.style.alignItems = Align.Center;
            searchBar.style.marginBottom = 8;
            contentContainer.Add(searchBar);
            
            var searchIcon = new Label("Search");
            searchIcon.style.marginRight = 4;
            searchIcon.style.color = Colors.HintText;
            searchBar.Add(searchIcon);
            
            var searchField = new TextField();
            searchField.style.flexGrow = 1;
            searchField.style.maxWidth = 300;
            searchField.value = searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                RefreshViewList();
            });
            searchBar.Add(searchField);
            
            var resultsLabel = new Label();
            resultsLabel.name = "ResultsCount";
            resultsLabel.style.marginLeft = 12;
            resultsLabel.style.fontSize = 11;
            resultsLabel.style.color = Colors.HintText;
            searchBar.Add(resultsLabel);
            
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            searchBar.Add(spacer);
            
            var sortLabel = new Label();
            sortLabel.name = "SortIndicator";
            sortLabel.style.fontSize = 10;
            sortLabel.style.color = Colors.HintText;
            searchBar.Add(sortLabel);
            
            // Main content area
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.name = "AssetListScroll";
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            contentContainer.Add(scrollView);
            
            var listContainer = new VisualElement();
            listContainer.name = "AssetList";
            listContainer.style.minWidth = 700;
            scrollView.Add(listContainer);
            
            RefreshViewList();
        }
        
        private Button CreateTypeFilterButton(string text, Type type, Color? color, bool isTagsView, int count = -1)
        {
            var displayText = count >= 0 ? $"{text} ({count})" : text;
            var btn = CreateButton(displayText, () =>
            {
                showTagsView = isTagsView;
                selectedTypeFilter = isTagsView ? null : type;
                currentSortColumn = isTagsView ? "Tag" : "Name";
                sortAscending = true;
                
                // Save selection
                EditorPrefs.SetString(PREFS_PREFIX + "LastTypeFilter", isTagsView ? TAGS_VIEW_MARKER : (type?.Name ?? ""));
                
                UpdateTypeFilterButtons();
                RefreshViewList();
            });
            
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 6;
            btn.style.paddingBottom = 6;
            btn.style.borderTopLeftRadius = 12;
            btn.style.borderTopRightRadius = 12;
            btn.style.borderBottomLeftRadius = 12;
            btn.style.borderBottomRightRadius = 12;
            btn.style.fontSize = 11;
            btn.userData = isTagsView ? TAGS_VIEW_MARKER : (object)type;
            
            if (color.HasValue)
            {
                btn.style.borderLeftWidth = 2;
                btn.style.borderLeftColor = color.Value;
            }
            
            return btn;
        }
        
        private void UpdateTypeFilterButtons()
        {
            foreach (var btn in typeFilterButtons)
            {
                bool isSelected;
                if (btn.userData is string marker && marker == TAGS_VIEW_MARKER)
                {
                    isSelected = showTagsView;
                }
                else
                {
                    var btnType = btn.userData as Type;
                    isSelected = !showTagsView && btnType == selectedTypeFilter;
                }
                
                btn.style.backgroundColor = isSelected 
                    ? new Color(0.35f, 0.35f, 0.35f, 1f) 
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
                btn.style.color = isSelected ? Colors.HeaderText : Colors.LabelText;
            }
        }
        
        private void RefreshViewList()
        {
            var listContainer = contentContainer.Q<VisualElement>("AssetList");
            var resultsLabel = contentContainer.Q<Label>("ResultsCount");
            var sortLabel = contentContainer.Q<Label>("SortIndicator");
            if (listContainer == null) return;
            
            listContainer.Clear();
            
            // Tags view
            if (showTagsView)
            {
                var filteredTags = cachedTagUsage.Values.AsEnumerable();
                
                if (!string.IsNullOrEmpty(searchFilter))
                    filteredTags = filteredTags.Where(t => t.Tag.ToString().IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
                var tagResults = filteredTags.ToList();
                
                if (resultsLabel != null)
                    resultsLabel.text = $"{tagResults.Count} tag(s)";
                
                if (sortLabel != null)
                    sortLabel.text = $"Sort: {currentSortColumn} {(sortAscending ? "↑" : "↓")}";
                
                BuildTagsSpreadsheetView(listContainer, tagResults);
                return;
            }
            
            // Asset view
            var filtered = cachedAssets.AsEnumerable();
            
            if (selectedTypeFilter != null)
                filtered = filtered.Where(a => a.GetType() == selectedTypeFilter);
            
            if (!string.IsNullOrEmpty(searchFilter))
                filtered = filtered.Where(a => MatchesSearch(a, searchFilter));
            
            var results = filtered.ToList();
            
            if (resultsLabel != null)
                resultsLabel.text = $"{results.Count} asset(s)";
            
            if (sortLabel != null)
                sortLabel.text = $"Sort: {currentSortColumn} {(sortAscending ? "↑" : "↓")}";
            
            if (selectedTypeFilter != null)
            {
                BuildSpreadsheetView(listContainer, results, selectedTypeFilter);
            }
            else
            {
                BuildGroupedListView(listContainer, results);
            }
        }
        
        private bool MatchesSearch(ScriptableObject asset, string search)
        {
            if (asset.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            
            var displayName = GetAssetDisplayName(asset);
            if (displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            
            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAGS SPREADSHEET VIEW
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagsSpreadsheetView(VisualElement container, List<TagUsageInfo> tags)
        {
            // Sort tags
            tags = SortTags(tags);
            
            int totalWidth = 250 + 80 + 400; // Tag, Count, Used By
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = Colors.BorderLight;
            headerRow.style.borderBottomWidth = 2;
            headerRow.style.borderBottomColor = Colors.BorderDark;
            headerRow.style.minWidth = totalWidth;
            headerRow.style.height = 28;
            container.Add(headerRow);
            
            headerRow.Add(CreateTagColumnHeader("Tag", 250 + COLOR_INDICATOR_WIDTH, true));
            headerRow.Add(CreateTagColumnHeader("Count", 80, false));
            headerRow.Add(CreateTagColumnHeader("Used By", 400, false));
            
            // Data rows
            for (int i = 0; i < tags.Count; i++)
            {
                var tagInfo = tags[i];
                var row = CreateTagSpreadsheetRow(tagInfo, i % 2 == 1, totalWidth);
                container.Add(row);
            }
            
            if (tags.Count == 0)
            {
                var emptyLabel = new Label("No tags found");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                container.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateTagColumnHeader(string name, int width, bool isFirst)
        {
            var header = new VisualElement();
            header.style.width = width;
            header.style.height = 28;
            header.style.paddingLeft = 8 + (isFirst ? COLOR_INDICATOR_WIDTH : 0);
            header.style.paddingRight = 4;
            header.style.borderRightWidth = 1;
            header.style.borderRightColor = ColumnBorderColor;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            
            var label = new Label(name);
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = currentSortColumn == name ? Colors.HeaderText : Colors.LabelText;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(label);
            
            if (currentSortColumn == name)
            {
                var arrow = new Label(sortAscending ? " ↑" : " ↓");
                arrow.style.fontSize = 10;
                arrow.style.color = Colors.AccentBlue;
                header.Add(arrow);
            }
            
            // Click handler
            header.RegisterCallback<ClickEvent>(_ =>
            {
                if (currentSortColumn == name)
                    sortAscending = !sortAscending;
                else
                {
                    currentSortColumn = name;
                    sortAscending = true;
                }
                RefreshViewList();
            });
            
            // Hover effect
            header.RegisterCallback<MouseEnterEvent>(_ => 
            {
                label.style.color = Colors.HeaderText;
                header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            });
            header.RegisterCallback<MouseLeaveEvent>(_ => 
            {
                label.style.color = currentSortColumn == name ? Colors.HeaderText : Colors.LabelText;
                header.style.backgroundColor = Color.clear;
            });
            
            return header;
        }
        
        private VisualElement CreateTagSpreadsheetRow(TagUsageInfo tagInfo, bool alternate, int totalWidth)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.minHeight = 26;
            row.style.minWidth = totalWidth;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            
            if (alternate)
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.3f);
            
            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 0.6f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = alternate ? new Color(0.18f, 0.18f, 0.18f, 0.3f) : Color.clear);
            
            // Tag name cell
            var tagCell = new VisualElement();
            tagCell.style.width = 250 + COLOR_INDICATOR_WIDTH;
            tagCell.style.paddingLeft = 8;
            tagCell.style.paddingRight = 4;
            tagCell.style.borderRightWidth = 1;
            tagCell.style.borderRightColor = ColumnBorderColor;
            tagCell.style.borderLeftWidth = COLOR_INDICATOR_WIDTH;
            tagCell.style.borderLeftColor = Colors.AccentCyan;
            tagCell.style.justifyContent = Justify.Center;
            
            var tagLabel = new Label(tagInfo.Tag.ToString());
            tagLabel.style.fontSize = 11;
            tagLabel.style.color = Colors.HeaderText;
            tagLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tagLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            tagLabel.style.overflow = Overflow.Hidden;
            tagLabel.style.textOverflow = TextOverflow.Ellipsis;
            tagLabel.tooltip = tagInfo.Tag.ToString();
            tagCell.Add(tagLabel);
            row.Add(tagCell);
            
            // Count cell
            var countCell = new VisualElement();
            countCell.style.width = 80;
            countCell.style.paddingLeft = 8;
            countCell.style.paddingRight = 4;
            countCell.style.borderRightWidth = 1;
            countCell.style.borderRightColor = ColumnBorderColor;
            countCell.style.justifyContent = Justify.Center;
            
            var countLabel = new Label(tagInfo.UsageCount.ToString());
            countLabel.style.fontSize = 11;
            countLabel.style.color = Colors.LabelText;
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            countCell.Add(countLabel);
            row.Add(countCell);
            
            // Used By cell
            var usedByCell = new VisualElement();
            usedByCell.style.width = 400;
            usedByCell.style.paddingLeft = 8;
            usedByCell.style.paddingRight = 4;
            usedByCell.style.borderRightWidth = 1;
            usedByCell.style.borderRightColor = ColumnBorderColor;
            usedByCell.style.flexDirection = FlexDirection.Row;
            usedByCell.style.alignItems = Align.Center;
            usedByCell.style.flexWrap = Wrap.NoWrap;
            usedByCell.style.overflow = Overflow.Hidden;
            
            // Show first few assets as clickable chips
            var assetsToShow = tagInfo.UsedByAssets.Take(3).ToList();
            foreach (var asset in assetsToShow)
            {
                var chip = CreateAssetChip(asset);
                usedByCell.Add(chip);
            }
            
            if (tagInfo.UsedByAssets.Count > 3)
            {
                var moreLabel = new Label($"+{tagInfo.UsedByAssets.Count - 3} more");
                moreLabel.style.fontSize = 9;
                moreLabel.style.color = Colors.HintText;
                moreLabel.style.marginLeft = 4;
                usedByCell.Add(moreLabel);
            }
            
            row.Add(usedByCell);
            
            return row;
        }
        
        private VisualElement CreateAssetChip(ScriptableObject asset)
        {
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
            
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 6;
            chip.style.paddingTop = 2;
            chip.style.paddingBottom = 2;
            chip.style.marginRight = 4;
            chip.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            chip.style.borderTopLeftRadius = 8;
            chip.style.borderTopRightRadius = 8;
            chip.style.borderBottomLeftRadius = 8;
            chip.style.borderBottomRightRadius = 8;
            
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 9;
            icon.style.marginRight = 2;
            icon.style.color = typeInfo?.Color ?? Colors.HintText;
            chip.Add(icon);
            
            var nameLabel = new Label(GetAssetDisplayName(asset));
            nameLabel.style.fontSize = 9;
            nameLabel.style.color = Colors.LabelText;
            nameLabel.style.maxWidth = 80;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            chip.Add(nameLabel);
            
            chip.tooltip = $"{typeInfo?.DisplayName}: {GetAssetDisplayName(asset)}";
            
            // Click to select
            chip.RegisterCallback<ClickEvent>(evt =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                evt.StopPropagation();
            });
            
            // Hover
            chip.RegisterCallback<MouseEnterEvent>(_ =>
                chip.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f));
            chip.RegisterCallback<MouseLeaveEvent>(_ =>
                chip.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f));
            
            return chip;
        }
        
        private List<TagUsageInfo> SortTags(List<TagUsageInfo> tags)
        {
            return currentSortColumn switch
            {
                "Tag" => sortAscending 
                    ? tags.OrderBy(t => t.Tag.ToString()).ToList()
                    : tags.OrderByDescending(t => t.Tag.ToString()).ToList(),
                "Count" => sortAscending
                    ? tags.OrderBy(t => t.UsageCount).ToList()
                    : tags.OrderByDescending(t => t.UsageCount).ToList(),
                "Used By" => sortAscending
                    ? tags.OrderBy(t => t.UsedByAssets.FirstOrDefault()?.name ?? "").ToList()
                    : tags.OrderByDescending(t => t.UsedByAssets.FirstOrDefault()?.name ?? "").ToList(),
                _ => tags
            };
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASSET SPREADSHEET VIEW
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSpreadsheetView(VisualElement container, List<ScriptableObject> assets, Type type)
        {
            if (!ColumnDefinitions.TryGetValue(type, out var columns))
            {
                columns = new List<ColumnDef> {
                    new ColumnDef("Name", 200, GetAssetDisplayName),
                    new ColumnDef("Asset", 200, a => a.name),
                };
            }
            
            assets = SortAssets(assets, columns);
            
            int totalWidth = columns.Sum(c => c.Width) + 80 + COLOR_INDICATOR_WIDTH;
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = Colors.BorderLight;
            headerRow.style.borderBottomWidth = 2;
            headerRow.style.borderBottomColor = Colors.BorderDark;
            headerRow.style.minWidth = totalWidth;
            headerRow.style.height = 28;
            container.Add(headerRow);
            
            bool isFirstCol = true;
            foreach (var col in columns)
            {
                var headerCell = CreateColumnHeader(col.Name, col.Width, isFirstCol);
                headerRow.Add(headerCell);
                isFirstCol = false;
            }
            
            var actionsHeader = CreateStaticHeaderCell("Actions", 80);
            headerRow.Add(actionsHeader);
            
            // Data rows
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                var row = CreateSpreadsheetRow(asset, columns, i % 2 == 1, totalWidth);
                container.Add(row);
            }
            
            if (assets.Count == 0)
            {
                var emptyLabel = new Label("No assets found");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                container.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateStaticHeaderCell(string name, int width)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.height = 28;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 4;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = ColumnBorderColor;
            
            var label = new Label(name);
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Colors.LabelText;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            cell.Add(label);
            
            return cell;
        }
        
        private VisualElement CreateColumnHeader(string name, int width, bool isFirst)
        {
            var header = new VisualElement();
            header.style.width = width + (isFirst ? COLOR_INDICATOR_WIDTH : 0);
            header.style.height = 28;
            header.style.paddingLeft = 8 + (isFirst ? COLOR_INDICATOR_WIDTH : 0);
            header.style.paddingRight = 4;
            header.style.borderRightWidth = 1;
            header.style.borderRightColor = ColumnBorderColor;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            
            var label = new Label(name);
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = currentSortColumn == name ? Colors.HeaderText : Colors.LabelText;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(label);
            
            if (currentSortColumn == name)
            {
                var arrow = new Label(sortAscending ? " ↑" : " ↓");
                arrow.style.fontSize = 10;
                arrow.style.color = Colors.AccentBlue;
                header.Add(arrow);
            }
            
            header.RegisterCallback<ClickEvent>(_ =>
            {
                if (currentSortColumn == name)
                    sortAscending = !sortAscending;
                else
                {
                    currentSortColumn = name;
                    sortAscending = true;
                }
                RefreshViewList();
            });
            
            header.RegisterCallback<MouseEnterEvent>(_ => 
            {
                label.style.color = Colors.HeaderText;
                header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            });
            header.RegisterCallback<MouseLeaveEvent>(_ => 
            {
                label.style.color = currentSortColumn == name ? Colors.HeaderText : Colors.LabelText;
                header.style.backgroundColor = Color.clear;
            });
            
            return header;
        }
        
        private VisualElement CreateSpreadsheetRow(ScriptableObject asset, List<ColumnDef> columns, bool alternate, int totalWidth)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.minHeight = 26;
            row.style.minWidth = totalWidth;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            
            if (alternate)
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.3f);
            
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
            
            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 0.6f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = alternate ? new Color(0.18f, 0.18f, 0.18f, 0.3f) : Color.clear);
            
            bool isFirst = true;
            foreach (var col in columns)
            {
                var value = col.GetValue(asset);
                var cell = CreateDataCell(value, col.Width, isFirst, col.IsAssetReference, asset, typeInfo?.Color);
                row.Add(cell);
                isFirst = false;
            }
            
            // Actions cell
            var actionsCell = new VisualElement();
            actionsCell.style.width = 80;
            actionsCell.style.flexDirection = FlexDirection.Row;
            actionsCell.style.alignItems = Align.Center;
            actionsCell.style.justifyContent = Justify.Center;
            actionsCell.style.borderRightWidth = 1;
            actionsCell.style.borderRightColor = ColumnBorderColor;
            
            var selectBtn = CreateButton("→", () =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }, "Select in Project");
            selectBtn.style.width = 26;
            selectBtn.style.height = 20;
            selectBtn.style.fontSize = 12;
            actionsCell.Add(selectBtn);
            
            var visualizeBtn = CreateButton("◎", () => OpenVisualizer(asset), "Open Visualizer");
            visualizeBtn.style.width = 26;
            visualizeBtn.style.height = 20;
            visualizeBtn.style.fontSize = 10;
            visualizeBtn.style.marginLeft = 2;
            actionsCell.Add(visualizeBtn);
            
            row.Add(actionsCell);
            
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    bool visualizeOnDoubleClick = EditorPrefs.GetBool(PREFS_PREFIX + "DoubleClickVisualize", true);
                    if (visualizeOnDoubleClick)
                        OpenVisualizer(asset);
                    else
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            });
            
            return row;
        }
        
        private VisualElement CreateDataCell(string value, int width, bool isFirst, bool isAssetRef, ScriptableObject parentAsset, Color? typeColor)
        {
            var cell = new VisualElement();
            cell.style.width = width + (isFirst ? COLOR_INDICATOR_WIDTH : 0);
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 4;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = ColumnBorderColor;
            cell.style.justifyContent = Justify.Center;
            
            if (isFirst && typeColor.HasValue)
            {
                cell.style.borderLeftWidth = COLOR_INDICATOR_WIDTH;
                cell.style.borderLeftColor = typeColor.Value;
            }
            
            if (isAssetRef && !string.IsNullOrEmpty(value) && value != "-" && value != "0")
            {
                var link = new Label(value ?? "-");
                link.style.fontSize = 11;
                link.style.color = Colors.AccentCyan;
                link.style.unityTextAlign = TextAnchor.MiddleLeft;
                link.style.overflow = Overflow.Hidden;
                link.style.textOverflow = TextOverflow.Ellipsis;
                link.tooltip = $"Click to visualize: {value}";
                
                link.RegisterCallback<MouseEnterEvent>(_ => link.style.color = Colors.HeaderText);
                link.RegisterCallback<MouseLeaveEvent>(_ => link.style.color = Colors.AccentCyan);
                
                link.RegisterCallback<ClickEvent>(evt =>
                {
                    if (parentAsset != null)
                        OpenVisualizer(parentAsset);
                    evt.StopPropagation();
                });
                
                cell.Add(link);
            }
            else
            {
                var label = new Label(value ?? "-");
                label.style.fontSize = 11;
                label.style.color = isFirst ? Colors.HeaderText : Colors.LabelText;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.tooltip = value;
                
                if (isFirst)
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                
                cell.Add(label);
            }
            
            return cell;
        }
        
        private List<ScriptableObject> SortAssets(List<ScriptableObject> assets, List<ColumnDef> columns)
        {
            var sortCol = columns.FirstOrDefault(c => c.Name == currentSortColumn) ?? columns.FirstOrDefault();
            if (sortCol == null) return assets;
            
            return sortAscending
                ? assets.OrderBy(a => sortCol.GetValue(a) ?? "").ToList()
                : assets.OrderByDescending(a => sortCol.GetValue(a) ?? "").ToList();
        }
        
        private void BuildGroupedListView(VisualElement container, List<ScriptableObject> assets)
        {
            var grouped = assets
                .OrderBy(a => a.GetType().Name)
                .ThenBy(a => GetAssetDisplayName(a))
                .GroupBy(a => a.GetType());
            
            foreach (var group in grouped)
            {
                var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == group.Key);
                
                var groupHeader = new VisualElement();
                groupHeader.style.flexDirection = FlexDirection.Row;
                groupHeader.style.alignItems = Align.Center;
                groupHeader.style.marginTop = 12;
                groupHeader.style.marginBottom = 6;
                groupHeader.style.paddingLeft = 8;
                groupHeader.style.paddingTop = 6;
                groupHeader.style.paddingBottom = 6;
                groupHeader.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
                groupHeader.style.borderTopLeftRadius = 4;
                groupHeader.style.borderTopRightRadius = 4;
                groupHeader.style.borderBottomLeftRadius = 4;
                groupHeader.style.borderBottomRightRadius = 4;
                groupHeader.style.borderLeftWidth = 3;
                groupHeader.style.borderLeftColor = typeInfo?.Color ?? Colors.AccentGray;
                container.Add(groupHeader);
                
                groupHeader.RegisterCallback<ClickEvent>(_ =>
                {
                    showTagsView = false;
                    selectedTypeFilter = group.Key;
                    EditorPrefs.SetString(PREFS_PREFIX + "LastTypeFilter", group.Key.Name);
                    UpdateTypeFilterButtons();
                    RefreshViewList();
                });
                
                groupHeader.RegisterCallback<MouseEnterEvent>(_ =>
                    groupHeader.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.9f));
                groupHeader.RegisterCallback<MouseLeaveEvent>(_ =>
                    groupHeader.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f));
                
                var groupIcon = new Label(typeInfo?.Icon ?? "?");
                groupIcon.style.fontSize = 14;
                groupIcon.style.marginRight = 8;
                groupIcon.style.color = typeInfo?.Color ?? Colors.HintText;
                groupHeader.Add(groupIcon);
                
                var groupTitle = new Label($"{typeInfo?.DisplayName ?? group.Key.Name}");
                groupTitle.style.fontSize = 12;
                groupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                groupTitle.style.color = Colors.HeaderText;
                groupHeader.Add(groupTitle);
                
                var groupCount = new Label($"({group.Count()})");
                groupCount.style.fontSize = 11;
                groupCount.style.color = Colors.HintText;
                groupCount.style.marginLeft = 8;
                groupHeader.Add(groupCount);
                
                var expandHint = new Label("Click to expand →");
                expandHint.style.fontSize = 9;
                expandHint.style.color = Colors.HintText;
                expandHint.style.marginLeft = 12;
                groupHeader.Add(expandHint);
                
                var previewItems = group.Take(5).ToList();
                foreach (var asset in previewItems)
                {
                    var row = CreateCompactAssetRow(asset, typeInfo);
                    container.Add(row);
                }
                
                if (group.Count() > 5)
                {
                    var moreLabel = new Label($"  ... and {group.Count() - 5} more");
                    moreLabel.style.fontSize = 10;
                    moreLabel.style.color = Colors.HintText;
                    moreLabel.style.paddingLeft = 24;
                    moreLabel.style.marginTop = 2;
                    moreLabel.style.marginBottom = 4;
                    container.Add(moreLabel);
                }
            }
            
            if (!assets.Any())
            {
                var emptyLabel = new Label("No assets found");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                container.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateCompactAssetRow(ScriptableObject asset, AssetTypeInfo typeInfo)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 24;
            row.style.paddingRight = 8;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.marginBottom = 1;
            
            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = Color.clear);
            
            var nameLabel = new Label(GetAssetDisplayName(asset));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = Colors.LabelText;
            row.Add(nameLabel);
            
            var displayName = GetAssetDisplayName(asset);
            bool showFileNames = EditorPrefs.GetBool(PREFS_PREFIX + "ShowFileNames", true);
            if (showFileNames && displayName != asset.name)
            {
                var fileLabel = new Label($"({asset.name})");
                fileLabel.style.fontSize = 9;
                fileLabel.style.color = Colors.HintText;
                fileLabel.style.marginRight = 8;
                row.Add(fileLabel);
            }
            
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount >= 1)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            });
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Data Extraction Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static string GetAbilityName(Ability a) => 
            !string.IsNullOrEmpty(a.GetName()) ? a.GetName() : a.name;
        
        private static string GetAbilityCostSummary(Ability a)
        {
            var cost = a.Cost;
            if (cost == null) return "-";
            var effectName = cost.GetName();
            if (string.IsNullOrEmpty(effectName) || effectName == cost.GetType().Name)
                return "-";
            return effectName;
        }
        
        private static string GetAbilityCooldownSummary(Ability a)
        {
            var cooldown = a.Cooldown;
            if (cooldown == null) return "-";
            var effectName = cooldown.GetName();
            if (string.IsNullOrEmpty(effectName) || effectName == cooldown.GetType().Name)
                return "-";
            return effectName;
        }
        
        private static string GetEffectName(GameplayEffect e) => 
            !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name;
        
        private static string GetEffectDuration(GameplayEffect e)
        {
            var spec = e.DurationSpecification;
            if (spec == null) return "-";
            var str = spec.ToString();
            if (string.IsNullOrEmpty(str) || str == spec.GetType().Name)
                return "-";
            return str;
        }
        
        private static string GetEffectImpact(GameplayEffect e)
        {
            var spec = e.ImpactSpecification;
            if (spec == null) return "-";
            var str = spec.ToString();
            if (string.IsNullOrEmpty(str) || str == spec.GetType().Name)
                return "-";
            return str;
        }
        
        private static string GetAttributeName(Attribute a) => 
            !string.IsNullOrEmpty(a.Name) ? a.Name : a.name;
        
        private static string GetAttributeDescription(Attribute a)
        {
            var desc = a.Description;
            if (string.IsNullOrEmpty(desc)) return "-";
            return desc.Length > 60 ? desc.Substring(0, 57) + "..." : desc;
        }
        
        private static string GetEntityName(EntityIdentity e) => 
            !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Column Definition Helper
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class ColumnDef
        {
            public string Name { get; }
            public int Width { get; }
            public Func<ScriptableObject, string> GetValue { get; }
            public bool IsAssetReference { get; }
            
            public ColumnDef(string name, int width, Func<ScriptableObject, string> getValue, bool isAssetRef = false)
            {
                Name = name;
                Width = width;
                GetValue = getValue;
                IsAssetReference = isAssetRef;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SETTINGS TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSettingsTab()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            contentContainer.Add(scrollView);
            
            // Asset Paths Section
            var pathsSection = CreateSettingsSection("Asset Paths", "Default folders for new assets", Colors.AccentBlue);
            scrollView.Add(pathsSection);
            
            foreach (var typeInfo in AssetTypes.Where(t => t.CanCreate))
            {
                var pathRow = CreatePathSettingRow(typeInfo);
                pathsSection.Add(pathRow);
            }
            
            // View Settings Section
            var viewSection = CreateSettingsSection("View Settings", "Customize the View tab behavior", Colors.AccentGreen);
            scrollView.Add(viewSection);
            
            var rememberFilterToggle = new Toggle("Remember last type filter");
            rememberFilterToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "RememberTypeFilter", true);
            rememberFilterToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "RememberTypeFilter", evt.newValue));
            rememberFilterToggle.style.marginTop = 8;
            viewSection.Add(rememberFilterToggle);
            
            var doubleClickToggle = new Toggle("Double-click opens Visualizer (vs Select)");
            doubleClickToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "DoubleClickVisualize", true);
            doubleClickToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "DoubleClickVisualize", evt.newValue));
            doubleClickToggle.style.marginTop = 4;
            viewSection.Add(doubleClickToggle);
            
            var showFileNamesToggle = new Toggle("Show file names in grouped view");
            showFileNamesToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "ShowFileNames", true);
            showFileNamesToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "ShowFileNames", evt.newValue));
            showFileNamesToggle.style.marginTop = 4;
            viewSection.Add(showFileNamesToggle);
            
            // General Settings Section
            var generalSection = CreateSettingsSection("General", "Manager preferences", Colors.AccentPurple);
            scrollView.Add(generalSection);
            
            var autoRefreshToggle = new Toggle("Auto-refresh on window focus");
            autoRefreshToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "AutoRefresh", true);
            autoRefreshToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "AutoRefresh", evt.newValue));
            autoRefreshToggle.style.marginTop = 8;
            generalSection.Add(autoRefreshToggle);
            
            var notifyToggle = new Toggle("Show creation notifications");
            notifyToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "ShowNotifications", true);
            notifyToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "ShowNotifications", evt.newValue));
            notifyToggle.style.marginTop = 4;
            generalSection.Add(notifyToggle);
            
            // Analysis Section
            var analysisSection = CreateSettingsSection("Analysis", "Asset validation & statistics", Colors.AccentOrange);
            scrollView.Add(analysisSection);
            
            var analyzeBtn = CreateButton("Run Full Analysis", RunAnalysis);
            analyzeBtn.style.alignSelf = Align.FlexStart;
            analyzeBtn.style.marginTop = 8;
            ApplyButtonStyle(analyzeBtn);
            analysisSection.Add(analyzeBtn);
            
            // Danger Zone
            var dangerSection = CreateSettingsSection("Danger Zone", "Destructive operations", Colors.AccentRed);
            scrollView.Add(dangerSection);
            
            var clearCacheBtn = CreateButton("Clear Asset Cache", () =>
            {
                if (EditorUtility.DisplayDialog("Clear Cache", "Clear the asset cache and reload?", "Yes", "Cancel"))
                {
                    cachedAssets.Clear();
                    cachedTagUsage.Clear();
                    RefreshAssetCache();
                }
            });
            clearCacheBtn.style.alignSelf = Align.FlexStart;
            clearCacheBtn.style.marginTop = 8;
            dangerSection.Add(clearCacheBtn);
            
            var resetSettingsBtn = CreateButton("Reset All Settings", () =>
            {
                if (EditorUtility.DisplayDialog("Reset Settings", "Reset all PlayForge Manager settings to defaults?", "Yes", "Cancel"))
                {
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "RememberTypeFilter");
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "LastTypeFilter");
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "DoubleClickVisualize");
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "ShowFileNames");
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "AutoRefresh");
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "ShowNotifications");
                    foreach (var t in AssetTypes)
                        EditorPrefs.DeleteKey(PREFS_PREFIX + "Path_" + t.Type.Name);
                    ShowTab(2);
                }
            });
            resetSettingsBtn.style.alignSelf = Align.FlexStart;
            resetSettingsBtn.style.marginTop = 4;
            dangerSection.Add(resetSettingsBtn);
        }
        
        private VisualElement CreateSettingsSection(string title, string description, Color accentColor)
        {
            var section = new VisualElement();
            section.style.marginTop = 12;
            section.style.marginBottom = 8;
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.paddingTop = 10;
            section.style.paddingBottom = 12;
            section.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            section.style.borderTopLeftRadius = 6;
            section.style.borderTopRightRadius = 6;
            section.style.borderBottomLeftRadius = 6;
            section.style.borderBottomRightRadius = 6;
            section.style.borderLeftWidth = 3;
            section.style.borderLeftColor = accentColor;
            
            var titleLabel = new Label(title.ToUpper());
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = accentColor;
            titleLabel.style.letterSpacing = 1;
            section.Add(titleLabel);
            
            var descLabel = new Label(description);
            descLabel.style.fontSize = 10;
            descLabel.style.color = Colors.HintText;
            descLabel.style.marginTop = 2;
            section.Add(descLabel);
            
            return section;
        }
        
        private VisualElement CreatePathSettingRow(AssetTypeInfo typeInfo)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label($"{typeInfo.Icon} {typeInfo.DisplayName}");
            label.style.width = 120;
            label.style.fontSize = 11;
            label.style.color = Colors.LabelText;
            row.Add(label);
            
            var pathField = new TextField();
            pathField.style.flexGrow = 1;
            pathField.value = GetPathForType(typeInfo.Type);
            pathField.RegisterValueChangedCallback(evt => 
                SetPathForType(typeInfo.Type, evt.newValue));
            row.Add(pathField);
            
            var browseBtn = CreateButton("...", () =>
            {
                var newPath = EditorUtility.OpenFolderPanel($"Select {typeInfo.DisplayName} Folder", "Assets", "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    if (newPath.StartsWith(Application.dataPath))
                        newPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                    
                    pathField.value = newPath;
                    SetPathForType(typeInfo.Type, newPath);
                }
            });
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            row.Add(browseBtn);
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateButton(string text, Action onClick, string tooltip = null)
        {
            var btn = new Button(onClick) { text = text };
            btn.focusable = false;
            if (!string.IsNullOrEmpty(tooltip))
                btn.tooltip = tooltip;
            return btn;
        }
        
        private VisualElement CreateSectionHeader(string title, string subtitle)
        {
            var container = new VisualElement();
            
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            container.Add(titleLabel);
            
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 10;
            subtitleLabel.style.color = Colors.HintText;
            subtitleLabel.style.marginTop = 2;
            container.Add(subtitleLabel);
            
            return container;
        }
        
        private VisualElement CreateDivider(int marginVertical = 8)
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = marginVertical;
            divider.style.marginBottom = marginVertical;
            return divider;
        }
        
        private void ApplyButtonStyle(Button btn)
        {
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        private void RefreshAssetCache()
        {
            cachedAssets.Clear();
            cachedTagUsage.Clear();
            
            // Load all assets
            foreach (var typeInfo in AssetTypes)
            {
                var guids = AssetDatabase.FindAssets($"t:{typeInfo.Type.Name}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null)
                        cachedAssets.Add(asset);
                }
            }
            
            // Build tag usage cache from BaseForgeObjects
            foreach (var asset in cachedAssets)
            {
                if (asset is BaseForgeObject forgeObj)
                {
                    var tags = forgeObj.GetAllTags();
                    if (tags != null)
                    {
                        foreach (var tag in tags)
                        {
                            if (!cachedTagUsage.TryGetValue(tag, out var usageInfo))
                            {
                                usageInfo = new TagUsageInfo(tag);
                                cachedTagUsage[tag] = usageInfo;
                            }
                            usageInfo.AddAsset(asset);
                        }
                    }
                }
            }
        }
        
        private void RefreshAll()
        {
            RefreshAssetCache();
            ShowTab(currentTab);
        }
        
        private int CountAssetsOfType(Type type)
        {
            return cachedAssets.Count(a => a.GetType() == type);
        }
        
        private string GetPathForType(Type type)
        {
            var key = PREFS_PREFIX + "Path_" + type.Name;
            if (EditorPrefs.HasKey(key))
                return EditorPrefs.GetString(key);
            
            return DefaultPaths.TryGetValue(type, out var path) ? path : "Assets/Data";
        }
        
        private void SetPathForType(Type type, string path)
        {
            EditorPrefs.SetString(PREFS_PREFIX + "Path_" + type.Name, path);
        }
        
        private string GetRelativeTime(DateTime time)
        {
            var diff = DateTime.Now - time;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return time.ToString("MMM d");
        }
        
        private void RunAnalysis()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PlayForge Asset Analysis");
            sb.AppendLine("========================\n");
            
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                sb.AppendLine($"{typeInfo.Icon} {typeInfo.DisplayName}: {count}");
            }
            
            sb.AppendLine($"\nTotal: {cachedAssets.Count} assets");
            
            sb.AppendLine("\n--- Tag Usage ---");
            sb.AppendLine($"Unique tags: {cachedTagUsage.Count}");
            
            var topTags = cachedTagUsage.Values
                .OrderByDescending(t => t.UsageCount)
                .Take(10);
            
            sb.AppendLine("\nTop 10 most used tags:");
            foreach (var tagInfo in topTags)
            {
                sb.AppendLine($"  • {tagInfo.Tag}: {tagInfo.UsageCount} usage(s)");
            }
            
            EditorUtility.DisplayDialog("Analysis Results", sb.ToString(), "OK");
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Classes
        // ═══════════════════════════════════════════════════════════════════════════
        
        public class AssetTypeInfo
        {
            public Type Type { get; }
            public string DisplayName { get; }
            public string Icon { get; }
            public Color Color { get; }
            public string Description { get; }
            public bool CanCreate { get; }
            
            public AssetTypeInfo(Type type, string displayName, string icon, Color color, string description, bool canCreate = true)
            {
                Type = type;
                DisplayName = displayName;
                Icon = icon;
                Color = color;
                Description = description;
                CanCreate = canCreate;
            }
        }
        
        /// <summary>
        /// Tracks usage information for a single Tag.
        /// </summary>
        public class TagUsageInfo
        {
            public Tag Tag { get; }
            public List<ScriptableObject> UsedByAssets { get; } = new List<ScriptableObject>();
            public int UsageCount => UsedByAssets.Count;
            
            public TagUsageInfo(Tag tag)
            {
                Tag = tag;
            }
            
            public void AddAsset(ScriptableObject asset)
            {
                if (!UsedByAssets.Contains(asset))
                    UsedByAssets.Add(asset);
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Create Asset Dialog
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class CreateAssetDialog : EditorWindow
    {
        private PlayForgeManager.AssetTypeInfo typeInfo;
        private string assetPath;
        private string assetName = "";
        private Action onCreated;
        
        public void Initialize(PlayForgeManager.AssetTypeInfo info, string path, Action callback)
        {
            typeInfo = info;
            assetPath = path;
            onCreated = callback;
            
            titleContent = new GUIContent($"Create {typeInfo.DisplayName}");
            minSize = maxSize = new Vector2(350, 140);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            
            var header = new Label($"{typeInfo.Icon} New {typeInfo.DisplayName}");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = typeInfo.Color;
            header.style.marginBottom = 12;
            root.Add(header);
            
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 8;
            root.Add(nameRow);
            
            var nameLabel = new Label("Name");
            nameLabel.style.width = 50;
            nameRow.Add(nameLabel);
            
            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.RegisterValueChangedCallback(evt => assetName = evt.newValue);
            nameRow.Add(nameField);
            
            var pathLabel = new Label($"Path: {assetPath}/");
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = Colors.HintText;
            pathLabel.style.marginBottom = 16;
            root.Add(pathLabel);
            
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            root.Add(btnRow);
            
            var cancelBtn = new Button(Close) { text = "Cancel", focusable = false };
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);
            
            var createBtn = new Button(CreateAsset) { text = "Create", focusable = false };
            createBtn.style.backgroundColor = typeInfo.Color;
            btnRow.Add(createBtn);
            
            nameField.Focus();
        }
        
        private void CreateAsset()
        {
            if (string.IsNullOrEmpty(assetName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name for the asset.", "OK");
                return;
            }
            
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                Directory.CreateDirectory(assetPath);
                AssetDatabase.Refresh();
            }
            
            var asset = ScriptableObject.CreateInstance(typeInfo.Type);
            var fullPath = $"{assetPath}/{assetName}.asset";
            
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            onCreated?.Invoke();
            Close();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Visualizer Window (Placeholder)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class PlayForgeVisualizer : EditorWindow
    {
        private ScriptableObject targetAsset;
        
        public void SetAsset(ScriptableObject asset)
        {
            targetAsset = asset;
            
            if (asset != null)
            {
                var typeInfo = PlayForgeManager.AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
                titleContent = new GUIContent($"{typeInfo?.Icon ?? "?"} {PlayForgeManager.GetAssetDisplayName(asset)}");
                minSize = new Vector2(400, 300);
            }
            
            Rebuild();
        }
        
        private void CreateGUI()
        {
            Rebuild();
        }
        
        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            if (targetAsset == null)
            {
                var noAsset = new Label("No asset selected");
                noAsset.style.color = Colors.HintText;
                root.Add(noAsset);
                return;
            }
            
            var typeInfo = PlayForgeManager.AssetTypes.FirstOrDefault(t => t.Type == targetAsset.GetType());
            
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;
            root.Add(header);
            
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 28;
            icon.style.marginRight = 12;
            icon.style.color = typeInfo?.Color ?? Colors.LabelText;
            header.Add(icon);
            
            var titleContainer = new VisualElement();
            header.Add(titleContainer);
            
            var title = new Label(PlayForgeManager.GetAssetDisplayName(targetAsset));
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Colors.HeaderText;
            titleContainer.Add(title);
            
            var subtitle = new Label($"{typeInfo?.DisplayName ?? "Asset"} • {targetAsset.name}");
            subtitle.style.fontSize = 10;
            subtitle.style.color = Colors.HintText;
            titleContainer.Add(subtitle);
            
            // Placeholder content
            var placeholder = new Label("Visualizer content coming soon...\n\nThis window will display:\n• Key asset properties\n• Relationship graph\n• Tag overview\n• Quick actions");
            placeholder.style.color = Colors.LabelText;
            placeholder.style.whiteSpace = WhiteSpace.Normal;
            placeholder.style.marginTop = 20;
            root.Add(placeholder);
            
            // Tag summary if BaseForgeObject
            if (targetAsset is BaseForgeObject forgeObj)
            {
                var tags = forgeObj.GetAllTags();
                if (tags != null && tags.Count > 0)
                {
                    var tagSection = new VisualElement();
                    tagSection.style.marginTop = 20;
                    tagSection.style.paddingLeft = 8;
                    tagSection.style.borderLeftWidth = 2;
                    tagSection.style.borderLeftColor = Colors.AccentCyan;
                    root.Add(tagSection);
                    
                    var tagTitle = new Label($"🏷 Tags ({tags.Count})");
                    tagTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    tagTitle.style.color = Colors.AccentCyan;
                    tagTitle.style.marginBottom = 4;
                    tagSection.Add(tagTitle);
                    
                    foreach (var tag in tags.Take(10))
                    {
                        var tagLabel = new Label($"  • {tag}");
                        tagLabel.style.fontSize = 10;
                        tagLabel.style.color = Colors.LabelText;
                        tagSection.Add(tagLabel);
                    }
                    
                    if (tags.Count > 10)
                    {
                        var moreLabel = new Label($"  ... and {tags.Count - 10} more");
                        moreLabel.style.fontSize = 10;
                        moreLabel.style.color = Colors.HintText;
                        tagSection.Add(moreLabel);
                    }
                }
            }
        }
    }
}