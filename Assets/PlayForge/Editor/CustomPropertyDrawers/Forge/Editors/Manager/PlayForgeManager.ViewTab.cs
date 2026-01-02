using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeManager
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW TAB STATE
        // Note: showTagsView is declared in PlayForgeManager.cs
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string currentSortColumn = "Name";
        private bool sortAscending = true;
        private List<Button> typeFilterButtons = new List<Button>();
        private List<Button> secondaryFilterButtons = new List<Button>();
        private HashSet<string> expandedTags = new HashSet<string>();
        
        // Secondary view mode for modifiers
        private bool showModifiersView = false;
        
        // Cached scaler discovery
        private static List<ScalerRecord> _cachedScalerRecords = null;
        private static DateTime _lastScalerCacheTime = DateTime.MinValue;
        private const float SCALER_CACHE_LIFETIME_SECONDS = 60f;
        
        // Styling constants
        private static readonly Color ColumnBorderColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color RowBorderColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        private const int ROW_HEIGHT = 28;
        private const int HEADER_HEIGHT = 32;
        private const int CELL_PADDING_H = 8;
        private const int ACTIONS_COLUMN_WIDTH = 80;
        
        private static readonly Dictionary<Type, List<ColumnDef>> ColumnDefinitions = new Dictionary<Type, List<ColumnDef>>
        {
            { typeof(Ability), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetAbilityName((Ability)a)),
                new ColumnDef("Policy", 100, a => ((Ability)a).Definition.ActivationPolicy.ToString()),
                new ColumnDef("Start Lvl", 70, a => ((Ability)a).StartingLevel.ToString()),
                new ColumnDef("Max Lvl", 70, a => ((Ability)a).MaxLevel.ToString()),
                new ColumnDef("Cost", 90, a => GetAbilityCostSummary((Ability)a), true),
                new ColumnDef("Cooldown", 90, a => GetAbilityCooldownSummary((Ability)a), true),
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
                new ColumnDef("Description", 350, a => GetAttributeDescription((Attribute)a)),
            }},
            { typeof(AttributeSet), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => a.name),
                new ColumnDef("Attributes", 90, a => ((AttributeSet)a).Attributes?.Count.ToString() ?? "0"),
                new ColumnDef("Subsets", 90, a => ((AttributeSet)a).SubSets?.Count.ToString() ?? "0"),
                new ColumnDef("Unique", 90, a => ((AttributeSet)a).GetUnique()?.Count.ToString() ?? "0"),
                new ColumnDef("Collision", 130, a => ((AttributeSet)a).CollisionResolutionPolicy.ToString()),
            }},
            { typeof(EntityIdentity), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetEntityName((EntityIdentity)a)),
                new ColumnDef("Policy", 110, a => ((EntityIdentity)a).ActivationPolicy.ToString()),
                new ColumnDef("Max Abilities", 90, a => ((EntityIdentity)a).MaxAbilities.ToString()),
                new ColumnDef("Starting", 80, a => ((EntityIdentity)a).StartingAbilities?.Count.ToString() ?? "0"),
                new ColumnDef("Duplicates", 80, a => ((EntityIdentity)a).AllowDuplicateAbilities ? "Yes" : "No"),
            }},
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Scaler Record for generic discovery
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class ScalerRecord
        {
            public ScriptableObject Asset;
            public string FieldPath;
            public AbstractScaler Scaler;
            public string ScalerTypeName;
            public string Context;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // BUILD VIEW TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildViewTab()
        {
            // ROW 1: Asset Type Filters
            var typeBar = new VisualElement();
            typeBar.style.flexDirection = FlexDirection.Row;
            typeBar.style.flexWrap = Wrap.Wrap;
            typeBar.style.marginBottom = 4;
            contentContainer.Add(typeBar);
            
            typeFilterButtons.Clear();
            
            var allBtn = CreateTypeFilterButton("All", null, null, -1);
            typeBar.Add(allBtn);
            typeFilterButtons.Add(allBtn);
            
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var btn = CreateTypeFilterButton($"{typeInfo.Icon} {typeInfo.DisplayName}", typeInfo.Type, typeInfo.Color, count);
                typeBar.Add(btn);
                typeFilterButtons.Add(btn);
            }
            
            // ROW 2: Tags & Modifiers
            var secondaryBar = new VisualElement();
            secondaryBar.style.flexDirection = FlexDirection.Row;
            secondaryBar.style.flexWrap = Wrap.Wrap;
            secondaryBar.style.marginBottom = 8;
            secondaryBar.style.paddingLeft = 4;
            contentContainer.Add(secondaryBar);
            
            secondaryFilterButtons.Clear();
            
            var tagCount = TagRegistry.GetAllTags().Count();
            var tagsBtn = CreateSecondaryFilterButton("🏷 Tags", Colors.AccentCyan, showTagsView, tagCount);
            tagsBtn.clicked += () =>
            {
                showTagsView = !showTagsView;
                if (showTagsView) showModifiersView = false;
                ShowTab(1);
            };
            secondaryBar.Add(tagsBtn);
            secondaryFilterButtons.Add(tagsBtn);
            
            var modifierCount = GetScalerCache().Count;
            var modifiersBtn = CreateSecondaryFilterButton("◆ Modifiers", Colors.AccentPurple, showModifiersView, modifierCount);
            modifiersBtn.clicked += () =>
            {
                showModifiersView = !showModifiersView;
                if (showModifiersView) showTagsView = false;
                ShowTab(1);
            };
            secondaryBar.Add(modifiersBtn);
            secondaryFilterButtons.Add(modifiersBtn);
            
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            secondaryBar.Add(spacer);
            
            if (showTagsView || showModifiersView)
            {
                var infoLabel = new Label(showTagsView ? "Browsing Tags" : "Browsing Modifiers");
                infoLabel.style.fontSize = 10;
                infoLabel.style.color = showTagsView ? Colors.AccentCyan : Colors.AccentPurple;
                infoLabel.style.paddingRight = 8;
                infoLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                secondaryBar.Add(infoLabel);
            }
            
            UpdateTypeFilterButtons();
            
            if (showTagsView)
                BuildTagContextFilterBar();
            
            // Search Bar
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
            
            var searchSpacer = new VisualElement();
            searchSpacer.style.flexGrow = 1;
            searchBar.Add(searchSpacer);
            
            if (showTagsView && TagRegistry.IsCacheValid)
            {
                var cacheInfo = new Label($"Last scan: {TagRegistry.LastScanTime:HH:mm:ss}");
                cacheInfo.style.fontSize = 9;
                cacheInfo.style.color = Colors.HintText;
                cacheInfo.style.marginRight = 8;
                searchBar.Add(cacheInfo);
            }
            
            // Main Content Area
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Filter Buttons
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateTypeFilterButton(string text, Type type, Color? accentColor, int count)
        {
            var btn = new Button();
            btn.userData = type;
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 4;
            btn.style.paddingBottom = 4;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.backgroundColor = Colors.ButtonBackground;
            
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = Colors.LabelText;
            content.Add(label);
            
            if (count >= 0)
            {
                var countLabel = new Label($" ({count})");
                countLabel.style.fontSize = 9;
                countLabel.style.color = Colors.HintText;
                content.Add(countLabel);
            }
            
            btn.Add(content);
            
            btn.clicked += () =>
            {
                showTagsView = false;
                showModifiersView = false;
                selectedTypeFilter = type;
                if (type != null)
                    EditorPrefs.SetString(PREFS_PREFIX + "LastTypeFilter", type.Name);
                else
                    EditorPrefs.DeleteKey(PREFS_PREFIX + "LastTypeFilter");
                ShowTab(1);
            };
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => UpdateTypeButtonStyle(btn, type, accentColor));
            
            return btn;
        }
        
        private Button CreateSecondaryFilterButton(string text, Color accentColor, bool isSelected, int count)
        {
            var btn = new Button();
            btn.style.marginRight = 4;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            
            SetSecondaryButtonStyle(btn, isSelected, accentColor);
            
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = isSelected ? accentColor : Colors.LabelText;
            content.Add(label);
            
            if (count >= 0)
            {
                var countLabel = new Label($" ({count})");
                countLabel.style.fontSize = 9;
                countLabel.style.color = Colors.HintText;
                content.Add(countLabel);
            }
            
            btn.Add(content);
            
            btn.RegisterCallback<MouseEnterEvent>(_ => { if (!isSelected) btn.style.backgroundColor = Colors.ButtonHover; });
            btn.RegisterCallback<MouseLeaveEvent>(_ => SetSecondaryButtonStyle(btn, isSelected, accentColor));
            
            return btn;
        }
        
        private void SetSecondaryButtonStyle(Button btn, bool isSelected, Color accentColor)
        {
            if (isSelected)
            {
                btn.style.backgroundColor = new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.8f);
                btn.style.borderTopColor = accentColor;
                btn.style.borderBottomColor = accentColor;
                btn.style.borderLeftColor = accentColor;
                btn.style.borderRightColor = accentColor;
            }
            else
            {
                btn.style.backgroundColor = Colors.ButtonBackground;
                btn.style.borderTopColor = Colors.BorderDark;
                btn.style.borderBottomColor = Colors.BorderDark;
                btn.style.borderLeftColor = Colors.BorderDark;
                btn.style.borderRightColor = Colors.BorderDark;
            }
        }
        
        private void UpdateTypeFilterButtons()
        {
            foreach (var btn in typeFilterButtons)
            {
                var type = btn.userData as Type;
                var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == type);
                UpdateTypeButtonStyle(btn, type, typeInfo?.Color);
            }
        }
        
        private void UpdateTypeButtonStyle(Button btn, Type type, Color? accentColor)
        {
            bool isSelected = (type == selectedTypeFilter && !showTagsView && !showModifiersView);
            
            if (isSelected)
            {
                var color = accentColor ?? Colors.AccentGray;
                btn.style.backgroundColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.6f);
                btn.style.borderLeftWidth = 2;
                btn.style.borderLeftColor = color;
            }
            else
            {
                btn.style.backgroundColor = Colors.ButtonBackground;
                btn.style.borderLeftWidth = 0;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Refresh View List
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void RefreshViewList()
        {
            var listContainer = rootVisualElement.Q<VisualElement>("AssetList");
            if (listContainer == null) return;
            
            listContainer.Clear();
            
            if (showModifiersView)
            {
                BuildModifiersListView(listContainer);
                return;
            }
            
            if (showTagsView)
            {
                BuildTagsListView(listContainer);
                return;
            }
            
            BuildStandardListView(listContainer);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Standard List View
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildStandardListView(VisualElement container)
        {
            var assets = LoadFilteredAssets();
            
            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null)
                resultsLabel.text = $"{assets.Count} assets";
            
            if (selectedTypeFilter == null)
                BuildGroupedListView(container, assets);
            else if (ColumnDefinitions.TryGetValue(selectedTypeFilter, out var columns))
                BuildColumnListView(container, assets, columns);
            else
                BuildGroupedListView(container, assets);
        }
        
        private List<ScriptableObject> LoadFilteredAssets()
        {
            var assets = selectedTypeFilter == null
                ? cachedAssets.ToList()
                : cachedAssets.Where(a => a.GetType() == selectedTypeFilter).ToList();
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                assets = assets.Where(a =>
                    a.name.ToLower().Contains(filter) ||
                    GetAssetDisplayName(a).ToLower().Contains(filter)
                ).ToList();
            }
            
            return SortAssets(assets);
        }
        
        private List<ScriptableObject> SortAssets(List<ScriptableObject> assets)
        {
            if (selectedTypeFilter != null && ColumnDefinitions.TryGetValue(selectedTypeFilter, out var columns))
            {
                var sortCol = columns.FirstOrDefault(c => c.Name == currentSortColumn) ?? columns.FirstOrDefault();
                if (sortCol != null)
                    return sortAscending ? assets.OrderBy(a => sortCol.GetValue(a) ?? "").ToList() : assets.OrderByDescending(a => sortCol.GetValue(a) ?? "").ToList();
            }
            return sortAscending ? assets.OrderBy(a => GetAssetDisplayName(a)).ToList() : assets.OrderByDescending(a => GetAssetDisplayName(a)).ToList();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Generic Scaler Discovery (Cached)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private List<ScalerRecord> GetScalerCache()
        {
            if (_cachedScalerRecords == null || (DateTime.Now - _lastScalerCacheTime).TotalSeconds > SCALER_CACHE_LIFETIME_SECONDS)
            {
                _cachedScalerRecords = DiscoverAllScalers();
                _lastScalerCacheTime = DateTime.Now;
            }
            return _cachedScalerRecords;
        }
        
        private List<ScalerRecord> DiscoverAllScalers()
        {
            var records = new List<ScalerRecord>();
            foreach (var asset in cachedAssets)
            {
                if (asset == null) continue;
                try
                {
                    var so = new SerializedObject(asset);
                    var iter = so.GetIterator();
                    while (iter.NextVisible(true))
                    {
                        if (iter.propertyType == SerializedPropertyType.ManagedReference && iter.managedReferenceValue is AbstractScaler scaler)
                        {
                            var typeName = scaler.GetType().Name;
                            if (typeName.EndsWith("Scaler")) typeName = typeName.Substring(0, typeName.Length - 6);
                            records.Add(new ScalerRecord { Asset = asset, FieldPath = iter.propertyPath, Scaler = scaler, ScalerTypeName = typeName, Context = DeriveScalerContext(iter.propertyPath) });
                        }
                    }
                    so.Dispose();
                }
                catch { }
            }
            return records;
        }
        
        private string DeriveScalerContext(string propertyPath)
        {
            var parts = propertyPath.Split('.');
            var fieldName = parts.Last().Replace("Scaler", "");
            var lower = propertyPath.ToLower();
            if (lower.Contains("duration")) return "Duration";
            if (lower.Contains("magnitude") || lower.Contains("impact")) return "Magnitude";
            if (lower.Contains("cost")) return "Cost";
            if (lower.Contains("cooldown")) return "Cooldown";
            if (lower.Contains("period") || lower.Contains("tick")) return "Period";
            if (lower.Contains("stack")) return "Stacking";
            return fieldName;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Modifiers List View
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildModifiersListView(VisualElement container)
        {
            var records = GetScalerCache().AsEnumerable();
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                records = records.Where(r => r.Asset.name.ToLower().Contains(filter) || r.ScalerTypeName.ToLower().Contains(filter) || r.Context.ToLower().Contains(filter));
            }
            var recordList = records.ToList();
            
            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null) resultsLabel.text = $"{recordList.Count} modifiers";
            
            foreach (var group in recordList.GroupBy(r => r.ScalerTypeName).OrderBy(g => g.Key))
            {
                var groupHeader = CreateGroupHeader("◆", group.Key, group.Count(), Colors.AccentPurple);
                container.Add(groupHeader);
                
                foreach (var record in group.Take(10))
                    container.Add(CreateModifierRow(record));
                
                if (group.Count() > 10)
                    container.Add(CreateMoreLabel(group.Count() - 10));
            }
            
            if (!recordList.Any())
                container.Add(CreateEmptyLabel("No modifiers found"));
        }
        
        private VisualElement CreateModifierRow(ScalerRecord record)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 24;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 1;
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f));
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
            row.RegisterCallback<ClickEvent>(_ => { Selection.activeObject = record.Asset; EditorGUIUtility.PingObject(record.Asset); });
            
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == record.Asset.GetType());
            row.Add(CreateLabel(typeInfo?.Icon ?? "?", 20, 10, typeInfo?.Color ?? Colors.HintText));
            row.Add(CreateLabel(GetAssetDisplayName(record.Asset), 150, 11, Colors.LabelText, true));
            
            if (typeInfo != null)
                row.Add(CreateBadge(typeInfo.DisplayName, 70, typeInfo.Color));
            
            row.Add(CreateBadge(record.Context, 70, Colors.AccentBlue));
            
            var modeText = record.Scaler.Configuration switch { ELevelConfig.LockToLevelProvider => "Lock", ELevelConfig.Unlocked => "Unlk", ELevelConfig.Partitioned => "Part", _ => "?" };
            row.Add(CreateBadge(modeText, 40, Colors.AccentPurple));
            
            var infoRow = GetScalerInfoRow(record.Scaler);
            infoRow.style.flexGrow = 1;
            infoRow.style.marginLeft = 8;
            row.Add(infoRow);
            
            return row;
        }
        
        private VisualElement GetScalerInfoRow(AbstractScaler scaler)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.fontSize = 10;
            row.style.color = Colors.HintText;
            
            if (scaler?.LevelValues == null || scaler.LevelValues.Length == 0) { row.Add(new Label("-")); return row; }
            var lvp = scaler.LevelValues;
            
            if (lvp.Length == 1)
            {
                row.Add(new Label("Value: "));
                row.Add(new Label($"{lvp[0]:F2}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            }
            else
            {
                row.Add(new Label("Lv1: "));
                row.Add(new Label($"{lvp[0]:F2}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                row.Add(new Label(" → "));
                row.Add(new Label($"Lv{lvp.Length}: "));
                row.Add(new Label($"{lvp[^1]:F2}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            }
            return row;
        }
        
        private static string GetLevelModeTooltip(ELevelConfig config) => config switch
        {
            ELevelConfig.LockToLevelProvider => "Uses the owning Ability/Entity's level range automatically.",
            ELevelConfig.Unlocked => "Uses its own MaxLevel setting, independent of the source.",
            ELevelConfig.Partitioned => "Uses min(MaxLevel, source's current level).",
            _ => "Level configuration"
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tags List View with Expansion
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagContextFilterBar()
        {
            var contextBar = new VisualElement();
            contextBar.style.flexDirection = FlexDirection.Row;
            contextBar.style.flexWrap = Wrap.Wrap;
            contextBar.style.marginBottom = 8;
            contextBar.style.paddingLeft = 8;
            contextBar.style.paddingTop = 4;
            contextBar.style.paddingBottom = 4;
            contextBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            contextBar.style.borderTopLeftRadius = 4;
            contextBar.style.borderTopRightRadius = 4;
            contextBar.style.borderBottomLeftRadius = 4;
            contextBar.style.borderBottomRightRadius = 4;
            contentContainer.Add(contextBar);
            
            contextBar.Add(CreateLabel("Context:", 60, 10, Colors.HintText));
            
            var allBtn = CreateContextButton("All", selectedTagContextFilter == null);
            allBtn.clicked += () => { selectedTagContextFilter = null; ShowTab(1); };
            contextBar.Add(allBtn);
            
            foreach (var ctx in TagRegistry.GetAllContextKeys().Take(8))
            {
                var context = ctx;
                var btn = CreateContextButton(ctx, selectedTagContextFilter == ctx);
                btn.clicked += () => { selectedTagContextFilter = context; ShowTab(1); };
                contextBar.Add(btn);
            }
        }
        
        private Button CreateContextButton(string text, bool isSelected)
        {
            var btn = new Button { text = text };
            btn.style.fontSize = 9;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.paddingTop = 2;
            btn.style.paddingBottom = 2;
            btn.style.marginRight = 4;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = isSelected ? Colors.AccentCyan : Colors.ButtonBackground;
            btn.style.color = isSelected ? Colors.HeaderText : Colors.LabelText;
            return btn;
        }
        
        private void BuildTagsListView(VisualElement container)
        {
            var tagRecords = TagRegistry.GetAllTagRecords();
            
            if (!string.IsNullOrEmpty(searchFilter))
                tagRecords = tagRecords.Where(r => r.Tag.Name.ToLower().Contains(searchFilter.ToLower()));
            
            if (!string.IsNullOrEmpty(selectedTagContextFilter))
                tagRecords = tagRecords.Where(r => r.UsageByContext.ContainsKey(selectedTagContextFilter));
            
            var sorted = tagRecords.OrderByDescending(r => r.TotalUsageCount).ToList();
            
            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null) resultsLabel.text = $"{sorted.Count} tags";
            
            foreach (var record in sorted)
                container.Add(CreateTagRowWithExpansion(record));
            
            if (!sorted.Any())
                container.Add(CreateEmptyLabel("No tags found"));
        }
        
        private VisualElement CreateTagRowWithExpansion(TagRegistry.TagUsageRecord record)
        {
            var tagKey = record.Tag.Name;
            bool isExpanded = expandedTags.Contains(tagKey);
            
            var tagContainer = new VisualElement();
            tagContainer.style.marginBottom = 2;
            
            // Main row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.backgroundColor = Colors.ItemBackground;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = isExpanded ? 0 : 4;
            row.style.borderBottomRightRadius = isExpanded ? 0 : 4;
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = Colors.AccentCyan;
            tagContainer.Add(row);
            
            row.Add(CreateLabel(isExpanded ? "▼" : "▶", 16, 9, Colors.HintText));
            row.Add(CreateLabel(record.Tag.Name, 180, 11, Colors.LabelText, false, FontStyle.Bold));
            row.Add(CreateLabel($"{record.TotalUsageCount} uses", 70, 10, Colors.HintText));
            
            var contextContainer = new VisualElement();
            contextContainer.style.flexDirection = FlexDirection.Row;
            contextContainer.style.flexGrow = 1;
            contextContainer.style.flexWrap = Wrap.Wrap;
            foreach (var ctx in record.UsageByContext.Take(4))
                contextContainer.Add(CreateBadge($"{ctx.Value.Context.FriendlyName} ({ctx.Value.Usages.Count})", 0, Colors.AccentBlue));
            if (record.UsageByContext.Count > 4)
                contextContainer.Add(CreateLabel($"+{record.UsageByContext.Count - 4}", 30, 9, Colors.HintText));
            row.Add(contextContainer);
            
            /*var selectBtn = new Button { text = "Select" };
            selectBtn.style.fontSize = 9;
            selectBtn.style.paddingLeft = 8;
            selectBtn.style.paddingRight = 8;
            selectBtn.style.paddingTop = 2;
            selectBtn.style.paddingBottom = 2;
            selectBtn.style.marginLeft = 8;
            selectBtn.style.borderTopLeftRadius = 3;
            selectBtn.style.borderTopRightRadius = 3;
            selectBtn.style.borderBottomLeftRadius = 3;
            selectBtn.style.borderBottomRightRadius = 3;
            selectBtn.style.backgroundColor = Colors.ButtonBackground;
            selectBtn.clicked += () => { Selection.activeObject = record.Tag; EditorGUIUtility.PingObject(record.Tag); };
            row.Add(selectBtn);*/
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Colors.ButtonHover);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Colors.ItemBackground);
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (expandedTags.Contains(tagKey)) expandedTags.Remove(tagKey); else expandedTags.Add(tagKey);
                RefreshViewList();
            });
            
            if (isExpanded)
                tagContainer.Add(CreateTagExpandedContent(record));
            
            return tagContainer;
        }
        
        private VisualElement CreateTagExpandedContent(TagRegistry.TagUsageRecord record)
        {
            var content = new VisualElement();
            content.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            content.style.paddingLeft = 24;
            content.style.paddingRight = 12;
            content.style.paddingTop = 8;
            content.style.paddingBottom = 8;
            content.style.borderBottomLeftRadius = 4;
            content.style.borderBottomRightRadius = 4;
            content.style.borderLeftWidth = 3;
            content.style.borderLeftColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.5f);
            
            // Usage by Context
            content.Add(CreateSectionLabel("Usage by Context"));
            foreach (var ctx in record.UsageByContext.OrderByDescending(c => c.Value.Usages.Count))
            {
                var ctxRow = new VisualElement();
                ctxRow.style.flexDirection = FlexDirection.Row;
                ctxRow.style.alignItems = Align.Center;
                ctxRow.style.marginBottom = 2;
                
                // Use the friendly name from the context
                ctxRow.Add(CreateLabel(ctx.Value.Context.FriendlyName, 120, 10, Colors.LabelText));
                
                int usageCount = ctx.Value.Usages.Count;
                float pct = record.TotalUsageCount > 0 ? (float)usageCount / record.TotalUsageCount : 0;
                var barContainer = new VisualElement();
                barContainer.style.width = 100;
                barContainer.style.height = 8;
                barContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                barContainer.style.borderTopLeftRadius = 2;
                barContainer.style.borderTopRightRadius = 2;
                barContainer.style.borderBottomLeftRadius = 2;
                barContainer.style.borderBottomRightRadius = 2;
                barContainer.style.marginRight = 8;
                var bar = new VisualElement();
                bar.style.width = new StyleLength(new Length(pct * 100, LengthUnit.Percent));
                bar.style.height = 8;
                bar.style.backgroundColor = Colors.AccentCyan;
                bar.style.borderTopLeftRadius = 2;
                bar.style.borderTopRightRadius = 2;
                bar.style.borderBottomLeftRadius = 2;
                bar.style.borderBottomRightRadius = 2;
                barContainer.Add(bar);
                ctxRow.Add(barContainer);
                
                ctxRow.Add(CreateLabel($"{usageCount}", 30, 10, Colors.AccentCyan, false, FontStyle.Bold));
                content.Add(ctxRow);
            }
            
            // Assets using this tag - collect from all contexts
            content.Add(CreateSectionLabel("Assets Using This Tag", 12));
            var assetsUsingTag = record.UsageByContext.Values
                .SelectMany(ctx => ctx.Assets)
                .Distinct()
                .Take(8)
                .ToList();
            
            foreach (var asset in assetsUsingTag)
            {
                var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
                var assetRow = new VisualElement();
                assetRow.style.flexDirection = FlexDirection.Row;
                assetRow.style.alignItems = Align.Center;
                assetRow.style.paddingTop = 2;
                assetRow.style.paddingBottom = 2;
                assetRow.style.marginBottom = 1;
                assetRow.RegisterCallback<MouseEnterEvent>(_ => assetRow.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 0.5f));
                assetRow.RegisterCallback<MouseLeaveEvent>(_ => assetRow.style.backgroundColor = Color.clear);
                assetRow.RegisterCallback<ClickEvent>(_ => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); });
                
                assetRow.Add(CreateLabel(typeInfo?.Icon ?? "?", 20, 10, typeInfo?.Color ?? Colors.HintText));
                assetRow.Add(CreateLabel(GetAssetDisplayName(asset), 0, 10, Colors.LabelText, true));
                assetRow.Add(CreateBadge(typeInfo?.DisplayName ?? "Asset", 0, typeInfo?.Color ?? Colors.HintText));
                content.Add(assetRow);
            }
            
            // Total unique assets using this tag
            var totalCount = record.UsageByContext.Values.SelectMany(ctx => ctx.Assets).Distinct().Count();
            if (totalCount > 8)
                content.Add(CreateLabel($"... and {totalCount - 8} more assets", 0, 9, Colors.HintText));
            
            return content;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Column List View (Fixed Styling)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildColumnListView(VisualElement container, List<ScriptableObject> assets, List<ColumnDef> columns)
        {
            var tableContainer = new VisualElement();
            tableContainer.style.borderTopWidth = 1;
            tableContainer.style.borderBottomWidth = 1;
            tableContainer.style.borderLeftWidth = 1;
            tableContainer.style.borderRightWidth = 1;
            tableContainer.style.borderTopColor = ColumnBorderColor;
            tableContainer.style.borderBottomColor = ColumnBorderColor;
            tableContainer.style.borderLeftColor = ColumnBorderColor;
            tableContainer.style.borderRightColor = ColumnBorderColor;
            tableContainer.style.borderTopLeftRadius = 4;
            tableContainer.style.borderTopRightRadius = 4;
            tableContainer.style.borderBottomLeftRadius = 4;
            tableContainer.style.borderBottomRightRadius = 4;
            tableContainer.style.overflow = Overflow.Hidden;
            container.Add(tableContainer);
            
            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = Colors.SectionHeaderBackground;
            headerRow.style.minHeight = HEADER_HEIGHT;
            headerRow.style.borderBottomWidth = 1;
            headerRow.style.borderBottomColor = ColumnBorderColor;
            tableContainer.Add(headerRow);
            
            foreach (var col in columns)
                headerRow.Add(CreateHeaderCell(col));
            
            headerRow.Add(CreateHeaderCell("Actions", ACTIONS_COLUMN_WIDTH, false));
            
            // Rows
            bool alt = false;
            foreach (var asset in assets)
            {
                tableContainer.Add(CreateDataRow(asset, columns, alt));
                alt = !alt;
            }
            
            if (!assets.Any())
            {
                var empty = new VisualElement();
                empty.style.paddingTop = 20;
                empty.style.paddingBottom = 20;
                empty.Add(CreateEmptyLabel("No assets found"));
                tableContainer.Add(empty);
            }
        }
        
        private VisualElement CreateHeaderCell(ColumnDef col) => CreateHeaderCell(col.Name, col.Width, true);
        
        private VisualElement CreateHeaderCell(string name, int width, bool sortable)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minHeight = HEADER_HEIGHT;
            cell.style.paddingLeft = CELL_PADDING_H;
            cell.style.paddingRight = CELL_PADDING_H;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = ColumnBorderColor;
            
            var label = new Label(name + (name == currentSortColumn ? (sortAscending ? " ▲" : " ▼") : ""));
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Colors.LabelText;
            cell.Add(label);
            
            if (sortable)
            {
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    if (currentSortColumn == name) sortAscending = !sortAscending;
                    else { currentSortColumn = name; sortAscending = true; }
                    RefreshViewList();
                });
                cell.RegisterCallback<MouseEnterEvent>(_ => cell.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f));
                cell.RegisterCallback<MouseLeaveEvent>(_ => cell.style.backgroundColor = Color.clear);
            }
            return cell;
        }
        
        private VisualElement CreateDataRow(ScriptableObject asset, List<ColumnDef> columns, bool alternate)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = ROW_HEIGHT;
            row.style.backgroundColor = alternate ? new Color(0.2f, 0.2f, 0.2f, 0.3f) : Color.clear;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = RowBorderColor;
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 0.6f));
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = alternate ? new Color(0.2f, 0.2f, 0.2f, 0.3f) : Color.clear);
            row.RegisterCallback<ClickEvent>(evt => { if (evt.clickCount == 2) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); } });
            
            foreach (var col in columns)
                row.Add(CreateDataCell(col.GetValue(asset) ?? "-", col.Width, col.IsAssetReference));
            
            row.Add(CreateActionsCell(asset));
            return row;
        }
        
        private VisualElement CreateDataCell(string value, int width, bool isAssetRef)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.paddingLeft = CELL_PADDING_H;
            cell.style.paddingRight = CELL_PADDING_H;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(ColumnBorderColor.r, ColumnBorderColor.g, ColumnBorderColor.b, 0.5f);
            
            var label = new Label(value);
            label.style.fontSize = 11;
            label.style.color = isAssetRef ? Colors.AccentBlue : Colors.LabelText;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(label);
            return cell;
        }
        
        private VisualElement CreateActionsCell(ScriptableObject asset)
        {
            var cell = new VisualElement();
            cell.style.width = ACTIONS_COLUMN_WIDTH;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.style.paddingLeft = 4;
            cell.style.paddingRight = 4;
            
            var selectBtn = new Button { text = "Select" };
            selectBtn.style.fontSize = 9;
            selectBtn.style.paddingLeft = 6;
            selectBtn.style.paddingRight = 6;
            selectBtn.style.paddingTop = 2;
            selectBtn.style.paddingBottom = 2;
            selectBtn.style.marginRight = 4;
            selectBtn.style.borderTopLeftRadius = 3;
            selectBtn.style.borderTopRightRadius = 3;
            selectBtn.style.borderBottomLeftRadius = 3;
            selectBtn.style.borderBottomRightRadius = 3;
            selectBtn.style.backgroundColor = Colors.ButtonBackground;
            selectBtn.clicked += () => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); };
            cell.Add(selectBtn);
            
            var menuBtn = new Button { text = "⋮", tooltip = "More options" };
            menuBtn.style.fontSize = 12;
            menuBtn.style.width = 20;
            menuBtn.style.paddingLeft = 0;
            menuBtn.style.paddingRight = 0;
            menuBtn.style.borderTopLeftRadius = 3;
            menuBtn.style.borderTopRightRadius = 3;
            menuBtn.style.borderBottomLeftRadius = 3;
            menuBtn.style.borderBottomRightRadius = 3;
            menuBtn.style.backgroundColor = Colors.ButtonBackground;
            menuBtn.clicked += () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Open in Inspector"), false, () => Selection.activeObject = asset);
                menu.AddItem(new GUIContent("Ping in Project"), false, () => EditorGUIUtility.PingObject(asset));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Duplicate"), false, () =>
                {
                    var path = AssetDatabase.GetAssetPath(asset);
                    AssetDatabase.CopyAsset(path, AssetDatabase.GenerateUniqueAssetPath(path));
                    AssetDatabase.Refresh();
                    RefreshAssetCache();
                    RefreshViewList();
                });
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Asset", $"Delete '{asset.name}'?", "Delete", "Cancel"))
                    {
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
                        RefreshAssetCache();
                        RefreshViewList();
                    }
                });
                menu.ShowAsContext();
            };
            cell.Add(menuBtn);
            return cell;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Grouped List View
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildGroupedListView(VisualElement container, List<ScriptableObject> assets)
        {
            foreach (var group in assets.GroupBy(a => a.GetType()).OrderBy(g => g.Key.Name))
            {
                var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == group.Key);
                var header = CreateGroupHeader(typeInfo?.Icon ?? "?", typeInfo?.DisplayName ?? group.Key.Name, group.Count(), typeInfo?.Color ?? Colors.AccentGray);
                header.RegisterCallback<ClickEvent>(_ => { showTagsView = false; showModifiersView = false; selectedTypeFilter = group.Key; ShowTab(1); });
                header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.9f));
                header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f));
                container.Add(header);
                
                foreach (var asset in group.Take(5))
                    container.Add(CreateCompactAssetRow(asset, typeInfo));
                
                if (group.Count() > 5)
                    container.Add(CreateMoreLabel(group.Count() - 5));
            }
            
            if (!assets.Any())
                container.Add(CreateEmptyLabel("No assets found"));
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
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f));
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
            row.RegisterCallback<ClickEvent>(_ => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); });
            
            row.Add(CreateLabel(GetAssetDisplayName(asset), 0, 11, Colors.LabelText, true));
            
            var displayName = GetAssetDisplayName(asset);
            if (EditorPrefs.GetBool(PREFS_PREFIX + "ShowFileNames", true) && displayName != asset.name)
                row.Add(CreateLabel($"({asset.name})", 0, 9, Colors.HintText));
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateGroupHeader(string icon, string title, int count, Color color)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 12;
            header.style.marginBottom = 6;
            header.style.paddingLeft = 8;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            header.style.borderTopLeftRadius = 4;
            header.style.borderTopRightRadius = 4;
            header.style.borderBottomLeftRadius = 4;
            header.style.borderBottomRightRadius = 4;
            header.style.borderLeftWidth = 3;
            header.style.borderLeftColor = color;
            
            header.Add(CreateLabel(icon, 0, 14, color));
            header.Add(CreateLabel(title, 0, 12, Colors.HeaderText, false, FontStyle.Bold));
            header.Add(CreateLabel($"({count})", 0, 11, Colors.HintText));
            header.Add(CreateLabel("→", 0, 10, Colors.HintText));
            return header;
        }
        
        private Label CreateLabel(string text, int width, int fontSize, Color color, bool flexGrow = false, FontStyle fontStyle = FontStyle.Normal)
        {
            var label = new Label(text);
            if (width > 0) label.style.width = width;
            if (flexGrow) label.style.flexGrow = 1;
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.marginRight = 8;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }
        
        private VisualElement CreateBadge(string text, int width, Color color)
        {
            var badge = new Label(text);
            if (width > 0) badge.style.width = width;
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.marginRight = 4;
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            return badge;
        }
        
        private Label CreateSectionLabel(string text, int marginTop = 0)
        {
            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = Colors.HeaderText;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = marginTop;
            label.style.marginBottom = 6;
            return label;
        }
        
        private Label CreateMoreLabel(int count)
        {
            var label = new Label($"  ... and {count} more");
            label.style.fontSize = 10;
            label.style.color = Colors.HintText;
            label.style.paddingLeft = 24;
            label.style.marginTop = 2;
            label.style.marginBottom = 4;
            return label;
        }
        
        private Label CreateEmptyLabel(string text)
        {
            var label = new Label(text);
            label.style.color = Colors.HintText;
            label.style.marginTop = 20;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Column Definition
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
    }
}