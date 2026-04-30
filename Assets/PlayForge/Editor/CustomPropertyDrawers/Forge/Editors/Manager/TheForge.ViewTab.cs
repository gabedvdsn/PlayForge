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
    public partial class TheForge
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW TAB STATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string currentSortColumn = "Name";
        private bool sortAscending = true;
        private List<Button> typeFilterButtons = new List<Button>();
        private List<Button> secondaryFilterButtons = new List<Button>();
        private HashSet<string> expandedTags = new HashSet<string>();
        private HashSet<string> expandedRequirements = new HashSet<string>();

        private bool groupTagResults = true;
        private bool showFullTagPath = false;
        private string selectedTagParentFilter = null;

        // Tag list — sort & quality filters (new)
        private enum ETagSortMode { UsageDesc, AlphaAsc, DepthAsc }
        private ETagSortMode tagSortMode = ETagSortMode.UsageDesc;
        private string selectedTagSourceFilter = null; // null = All, "System", "Authored"
        private string selectedTagUsageFilter  = null; // null = All, "Used", "Orphan"

        // Secondary view modes
        private bool showScalersView = false;
        private bool showRequirementsView = false;

        // Requirements filter
        private string selectedRequirementFilter = null; // null = All, "Source", "Target", "Required", "Avoid"
        private bool requirementsNamedOnly = false;        // chip toggle: only records that belong to a named group

        // Scalers filter
        private string selectedScalerTypeFilter = null;        // null = All, or specific scaler type name
        private string selectedScalerContextFilter = null;     // null = All, "Duration", "Magnitude", etc.
        private Type selectedScalerAssetTypeFilter = null;     // null = All, or specific asset type
        private ELevelConfig? selectedScalerLevelConfigFilter = null; // null = All, or LockToLevelProvider/Unlocked/Clamped

        // All-assets view filter (no specific type selected)
        // null = None | "HasScalers" | "HasRequirements" | "HasTags" | "Leveled"
        private string selectedAllViewFilter = null;

        // Per-typed-asset contextual filter (driven by BaseForgeLevelProvider.IsLinked)
        // null = All | "Linked" | "Standalone"
        private string selectedTypedAssetFilter = null;
        
        // Cached scaler discovery
        private static List<ScalerRecord> _cachedScalerRecords = null;
        private static DateTime _lastScalerCacheTime = DateTime.MinValue;
        private const float SCALER_CACHE_LIFETIME_SECONDS = 60f;
        
        // Cached requirements discovery
        private static List<RequirementRecord> _cachedRequirementRecords = null;
        private static DateTime _lastRequirementCacheTime = DateTime.MinValue;
        private const float REQUIREMENT_CACHE_LIFETIME_SECONDS = 60f;
        
        // Styling constants
        private static readonly Color TableBorderColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color HeaderBgColor = new Color(0.18f, 0.18f, 0.2f, 1f);
        private static readonly Color RowAltColor = new Color(0.24f, 0.24f, 0.26f, 0.4f);
        private static readonly Color RowHoverColor = new Color(0.3f, 0.32f, 0.35f, 0.7f);
        private static readonly Color RequirementColor = new Color(0.95f, 0.6f, 0.2f); // Orange/amber
        private const int ROW_HEIGHT = 32;
        private const int ROW_BORDER_WIDTH = 3;
        private const int HEADER_HEIGHT = 36;
        private const int CELL_PADDING_H = 10;
        private const int ACTIONS_COLUMN_WIDTH = 120;
        
        private static readonly Dictionary<Type, List<ColumnDef>> ColumnDefinitions = new Dictionary<Type, List<ColumnDef>>
        {
            { typeof(Ability), new List<ColumnDef> {
                new ColumnDef("Name", 160, a => GetAbilityName((Ability)a)),
                new ColumnDef("Level Provider", 110, a => ((Ability)a).LinkedProvider?.GetProviderName() ?? "-"),
                new ColumnDef("Policy", 110, a => ((Ability)a).Definition.ActivationPolicy.ToString()),
                new ColumnDef("Start Lvl", 65, a => ((Ability)a).StartingLevel.ToString()),
                new ColumnDef("Max Lvl", 65, a => ((Ability)a).MaxLevel.ToString()),
                new ColumnDef("Cost", 85, a => GetAbilityCostSummary((Ability)a), true),
                new ColumnDef("Cooldown", 85, a => GetAbilityCooldownSummary((Ability)a), true),
                new ColumnDef("Stages", 55, a => ((Ability)a).Behaviour?.Stages?.Count.ToString() ?? "0"),
                new ColumnDef("Src Reqs", 60, a => GetAbilitySourceReqCount((Ability)a)),
                new ColumnDef("Tgt Reqs", 60, a => GetAbilityTargetReqCount((Ability)a)),
            }},
            { typeof(GameplayEffect), new List<ColumnDef> {
                new ColumnDef("Name", 160, a => GetEffectName((GameplayEffect)a)),
                new ColumnDef("Level Provider", 110, a => ((GameplayEffect)a).LinkedProvider?.GetProviderName() ?? "-"),                
                new ColumnDef("Duration", 110, a => GetEffectDuration((GameplayEffect)a)),
                new ColumnDef("Impact", 110, a => GetEffectImpact((GameplayEffect)a)),
                new ColumnDef("Workers", 60, a => ((GameplayEffect)a).Workers?.Count.ToString() ?? "0"),
                new ColumnDef("Visibility", 90, a => ((GameplayEffect)a).Definition?.Visibility.ToString() ?? "-"),
                new ColumnDef("Src Reqs", 60, a => GetEffectSourceReqCount((GameplayEffect)a)),
                new ColumnDef("Tgt Reqs", 60, a => GetEffectTargetReqCount((GameplayEffect)a)),
            }},
            { typeof(Item), new List<ColumnDef> {
                new ColumnDef("Name", 160, a => GetItemName((Item)a)),
                new ColumnDef("Level Provider", 110, a => ((Item)a).LinkedProvider?.GetProviderName() ?? "-"),                
                new ColumnDef("Start Lvl", 65, a => ((Item)a).StartingLevel.ToString()),
                new ColumnDef("Max Lvl", 65, a => ((Item)a).MaxLevel.ToString()),
                new ColumnDef("Effects", 60, a => ((Item)a).GrantedEffects?.Count.ToString() ?? "0"),
                new ColumnDef("Active", 60, a => ((Item)a).ActiveAbility != null ? "✓" : "-"),
                new ColumnDef("Visibility", 90, a => !string.IsNullOrEmpty(((Item)a).Definition.Visibility.Name) ? ((Item)a).Definition.Visibility.Name : "-"),
            }},
            { typeof(Attribute), new List<ColumnDef> {
                new ColumnDef("Name", 180, a => GetAttributeName((Attribute)a)),
                new ColumnDef("Description", 400, a => GetAttributeDescription((Attribute)a)),
            }},
            { typeof(AttributeSet), new List<ColumnDef> {
                new ColumnDef("Name", 160, a => GetAttributeSetName((AttributeSet)a)),
                new ColumnDef("Attributes", 80, a => ((AttributeSet)a).Attributes?.Count.ToString() ?? "0"),
                new ColumnDef("Subsets", 70, a => ((AttributeSet)a).SubSets?.Count.ToString() ?? "0"),
                new ColumnDef("Unique", 70, a => ((AttributeSet)a).GetUnique()?.Count.ToString() ?? "0"),
                new ColumnDef("Collision", 120, a => ((AttributeSet)a).CollisionResolutionPolicy.ToString()),
            }},
            { typeof(EntityIdentity), new List<ColumnDef> {
                new ColumnDef("Name", 160, a => GetEntityName((EntityIdentity)a)),
                new ColumnDef("Level Provider", 110, a => ((EntityIdentity)a).LinkedProvider?.GetProviderName() ?? "-"),                
                new ColumnDef("Policy", 100, a => ((EntityIdentity)a).ActivationPolicy.ToString()),
                new ColumnDef("Max Abilities", 90, a => ((EntityIdentity)a).MaxAbilitiesOperation.Magnitude.ToString()),
                new ColumnDef("Starting", 70, a => ((EntityIdentity)a).StartingAbilities?.Count.ToString() ?? "0"),
                new ColumnDef("Duplicates", 75, a => ((EntityIdentity)a).AllowDuplicateAbilities ? "Yes" : "No"),
            }},
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Record Classes
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class ScalerRecord
        {
            public ScriptableObject Asset;
            public string FieldPath;
            public AbstractScaler Scaler;
            public string ScalerTypeName;
            public string Context;
            public string AssetType; // "Ability", "Effect", "Item"
        }
        
        private class RequirementRecord
        {
            public ScriptableObject Asset;
            public string TagName;
            public Tag Tag;
            public string TargetType;   // "Source" or "Target"
            public string ReqType;      // "Required" or "Avoid"
            public string AssetType;    // "Ability" or "Effect"
            public string Context;      // "Activation" for abilities, "Application", "Ongoing", "Removal" for effects
            public string GroupName;    // Name from the AvoidRequireTagGroup or AbilityTagRequirements
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
            
            // ROW 2: Tags, Modifiers & Requirements
            var secondaryBar = new VisualElement();
            secondaryBar.style.flexDirection = FlexDirection.Row;
            secondaryBar.style.flexWrap = Wrap.Wrap;
            secondaryBar.style.marginBottom = 8;
            secondaryBar.style.paddingLeft = 4;
            contentContainer.Add(secondaryBar);
            
            secondaryFilterButtons.Clear();
            
            // Tags button
            var tagCount = ForgeTagRegistry.GetAllTags().Count();
            var tagsBtn = CreateSecondaryFilterButton("🏷 Tags", Colors.AccentCyan, showTagsView, tagCount);
            tagsBtn.clicked += () =>
            {
                showTagsView = !showTagsView;
                if (showTagsView) { showScalersView = false; showRequirementsView = false; }
                ShowTab(1);
            };
            secondaryBar.Add(tagsBtn);
            secondaryFilterButtons.Add(tagsBtn);
            
            // Scalers button (renamed from Modifiers)
            var scalerCount = GetScalerCache().Count;
            var scalersBtn = CreateSecondaryFilterButton("◆ Scalers", Colors.AccentPurple, showScalersView, scalerCount);
            scalersBtn.clicked += () =>
            {
                showScalersView = !showScalersView;
                if (showScalersView) { showTagsView = false; showRequirementsView = false; }
                ShowTab(1);
            };
            secondaryBar.Add(scalersBtn);
            secondaryFilterButtons.Add(scalersBtn);
            
            // Requirements button
            var reqRecords = GetRequirementsCache();
            var uniqueReqTags = reqRecords.Select(r => r.TagName).Distinct().Count();
            var requirementsBtn = CreateSecondaryFilterButton("⚡ Requirements", RequirementColor, showRequirementsView, uniqueReqTags);
            requirementsBtn.clicked += () =>
            {
                showRequirementsView = !showRequirementsView;
                if (showRequirementsView) { showTagsView = false; showScalersView = false; }
                ShowTab(1);
            };
            secondaryBar.Add(requirementsBtn);
            secondaryFilterButtons.Add(requirementsBtn);
            
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            secondaryBar.Add(spacer);
            
            if (showTagsView || showScalersView || showRequirementsView)
            {
                string browsingText = showTagsView ? "Browsing Tags" : (showScalersView ? "Browsing Scalers" : "Browsing Requirements");
                Color browsingColor = showTagsView ? Colors.AccentCyan : (showScalersView ? Colors.AccentPurple : RequirementColor);
                var infoLabel = new Label(browsingText);
                infoLabel.style.fontSize = 10;
                infoLabel.style.color = browsingColor;
                infoLabel.style.paddingRight = 8;
                infoLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                secondaryBar.Add(infoLabel);
            }
            
            UpdateTypeFilterButtons();
            
            // Context filter bars
            if (showTagsView)
                BuildTagContextFilterBar();
            if (showRequirementsView)
                BuildRequirementsFilterBar();
            if (showScalersView)
                BuildScalersFilterBar();
            if (!showTagsView && !showScalersView && !showRequirementsView)
            {
                if (selectedTypeFilter == null) BuildAllViewFilterBar();
                else                            BuildTypedAssetFilterBar();
            }
            
            // Search Bar
            var searchBar = new VisualElement();
            searchBar.style.flexDirection = FlexDirection.Row;
            searchBar.style.alignItems = Align.Center;
            searchBar.style.marginBottom = 8;
            contentContainer.Add(searchBar);
            
            var searchIcon = new Label("🔍");
            searchIcon.style.marginRight = 6;
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
            
            if (showTagsView && ForgeTagRegistry.IsCacheValid)
            {
                var cacheInfo = new Label($"Last scan: {ForgeTagRegistry.LastScanTime:HH:mm:ss}");
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
                showScalersView = false;
                showRequirementsView = false;
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
            bool isSelected = (type == selectedTypeFilter && !showTagsView && !showScalersView && !showRequirementsView);
            
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
            
            if (showRequirementsView)
            {
                BuildRequirementsListView(listContainer);
                return;
            }
            
            if (showScalersView)
            {
                BuildScalersListView(listContainer);
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
        
        private void BuildAllViewFilterBar()
        {
            var strip = CreateFilterStrip();
            contentContainer.Add(strip);

            strip.Add(CreateFilterSectionLabel("Filter:"));
            strip.Add(CreateFilterChip(() => "All",              () => selectedAllViewFilter == null,             Colors.AccentGray,   () => { selectedAllViewFilter = null;             ShowTab(1); }));
            strip.Add(CreateFilterChip(() => "Has Scalers",      () => selectedAllViewFilter == "HasScalers",     Colors.AccentPurple, () => { selectedAllViewFilter = "HasScalers";     ShowTab(1); }, tooltip: "Assets that author at least one scaler"));
            strip.Add(CreateFilterChip(() => "Has Requirements", () => selectedAllViewFilter == "HasRequirements", RequirementColor,    () => { selectedAllViewFilter = "HasRequirements";ShowTab(1); }, tooltip: "Assets that declare at least one tag requirement"));
            strip.Add(CreateFilterChip(() => "Has Tags",         () => selectedAllViewFilter == "HasTags",         Colors.AccentCyan,   () => { selectedAllViewFilter = "HasTags";        ShowTab(1); }, tooltip: "Assets that grant or reference any tag"));
            strip.Add(CreateFilterChip(() => "Leveled",          () => selectedAllViewFilter == "Leveled",         Colors.AccentBlue,   () => { selectedAllViewFilter = "Leveled";        ShowTab(1); }, tooltip: "Assets with their own level range"));
        }

        /// <summary>
        /// Per-typed-asset chip strip (shown when a specific asset type is selected).
        /// All asset types in the framework that participate in level provision share the
        /// same Linked / Standalone distinction via <see cref="BaseForgeLevelProvider.IsLinked"/>,
        /// so one strip serves Ability, Item, Effect, Entity, and AttributeSet uniformly.
        /// </summary>
        private void BuildTypedAssetFilterBar()
        {
            // Only meaningful for types that derive from BaseForgeLevelProvider.
            if (selectedTypeFilter == null) return;
            if (!typeof(BaseForgeLevelProvider).IsAssignableFrom(selectedTypeFilter)) return;

            var strip = CreateFilterStrip();
            contentContainer.Add(strip);

            strip.Add(CreateFilterSectionLabel("Link:"));
            strip.Add(CreateFilterChip(() => "All",        () => selectedTypedAssetFilter == null,         Colors.AccentGray,  () => { selectedTypedAssetFilter = null;         ShowTab(1); }));
            strip.Add(CreateFilterChip(() => "Standalone", () => selectedTypedAssetFilter == "Standalone", Colors.AccentGreen, () => { selectedTypedAssetFilter = "Standalone"; ShowTab(1); }, tooltip: "Assets that own their level range"));
            strip.Add(CreateFilterChip(() => "Linked",     () => selectedTypedAssetFilter == "Linked",     Colors.AccentBlue,  () => { selectedTypedAssetFilter = "Linked";     ShowTab(1); }, tooltip: "Assets with a Linked Provider"));
        }
        
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

            // ── All-view filter (no specific type selected) ──────────────────
            if (selectedTypeFilter == null && !string.IsNullOrEmpty(selectedAllViewFilter))
            {
                var scalerAssets = new HashSet<ScriptableObject>(GetScalerCache().Select(r => r.Asset));
                var reqAssets    = new HashSet<ScriptableObject>(GetRequirementsCache().Select(r => r.Asset));

                // "Has Tags" — any asset that contributes to or references the tag registry.
                // ITagSource.GetGrantedTags + AssetTag fields cover the bulk of authored tags.
                bool HasAnyTag(ScriptableObject so)
                {
                    if (so is ITagSource ts)
                    {
                        foreach (var t in ts.GetGrantedTags())
                            if (!string.IsNullOrEmpty(t.Name)) return true;
                    }
                    return false;
                }

                assets = selectedAllViewFilter switch
                {
                    "HasScalers"      => assets.Where(a => scalerAssets.Contains(a)).ToList(),
                    "HasRequirements" => assets.Where(a => reqAssets.Contains(a)).ToList(),
                    "HasTags"         => assets.Where(HasAnyTag).ToList(),
                    "Leveled"         => assets.Where(a => a is BaseForgeLevelProvider).ToList(),
                    _                 => assets
                };
            }

            // ── Per-type Linked/Standalone filter ────────────────────────────
            if (selectedTypeFilter != null
                && typeof(BaseForgeLevelProvider).IsAssignableFrom(selectedTypeFilter)
                && !string.IsNullOrEmpty(selectedTypedAssetFilter))
            {
                assets = assets.Where(a =>
                {
                    if (a is BaseForgeLevelProvider lp)
                        return selectedTypedAssetFilter == "Linked" ? lp.IsLinked : !lp.IsLinked;
                    return false;
                }).ToList();
            }

            // ── Search ───────────────────────────────────────────────────────
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
                    var assetType = asset switch
                    {
                        Ability => "Ability",
                        GameplayEffect => "Effect",
                        Item => "Item",
                        _ => asset.GetType().Name
                    };
                    
                    var so = new SerializedObject(asset);
                    var iter = so.GetIterator();
                    while (iter.NextVisible(true))
                    {
                        if (iter.propertyType == SerializedPropertyType.ManagedReference && iter.managedReferenceValue is AbstractScaler scaler)
                        {
                            var typeName = scaler.GetType().Name;
                            if (typeName.EndsWith("Scaler")) typeName = typeName.Substring(0, typeName.Length - 6);
                            records.Add(new ScalerRecord 
                            { 
                                Asset = asset, 
                                FieldPath = iter.propertyPath, 
                                Scaler = scaler, 
                                ScalerTypeName = typeName, 
                                Context = DeriveScalerContext(iter.propertyPath),
                                AssetType = assetType
                            });
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
        // Requirements Discovery (Cached)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private List<RequirementRecord> GetRequirementsCache()
        {
            if (_cachedRequirementRecords == null || (DateTime.Now - _lastRequirementCacheTime).TotalSeconds > REQUIREMENT_CACHE_LIFETIME_SECONDS)
            {
                _cachedRequirementRecords = DiscoverAllRequirements();
                _lastRequirementCacheTime = DateTime.Now;
            }
            return _cachedRequirementRecords;
        }
        
        private List<RequirementRecord> DiscoverAllRequirements()
        {
            var records = new List<RequirementRecord>();
            
            foreach (var asset in cachedAssets)
            {
                if (asset == null) continue;
                
                try
                {
                    if (asset is Ability ability)
                    {
                        var tags = ability.Tags;
                        if (tags?.TagRequirements != null)
                        {
                            var reqName = tags.TagRequirements.HasName ? tags.TagRequirements.Name : null;
                            CollectTagGroupTags(records, asset, "Ability", tags.TagRequirements.SourceRequirements, "Source", "Activation", reqName);
                            CollectTagGroupTags(records, asset, "Ability", tags.TagRequirements.TargetRequirements, "Target", "Activation", reqName);
                        }
                    }
                    else if (asset is GameplayEffect effect)
                    {
                        CollectRequirementTags(records, asset, "Effect", effect.SourceRequirements, "Source");
                        CollectRequirementTags(records, asset, "Effect", effect.TargetRequirements, "Target");
                    }
                }
                catch { }
            }
            
            return records;
        }
        
        private void CollectRequirementTags(List<RequirementRecord> records, ScriptableObject asset, string assetType, EffectTagRequirements reqs, string targetType)
        {
            if (reqs == null) return;
            
            if (reqs.ApplicationRequirements != null)
            {
                var groupName = reqs.ApplicationRequirements.HasName ? reqs.ApplicationRequirements.Name : null;
                CollectTagGroupTags(records, asset, assetType, reqs.ApplicationRequirements, targetType, "Application", groupName);
            }
            
            if (reqs.OngoingRequirements != null)
            {
                var groupName = reqs.OngoingRequirements.HasName ? reqs.OngoingRequirements.Name : null;
                CollectTagGroupTags(records, asset, assetType, reqs.OngoingRequirements, targetType, "Ongoing", groupName);
            }
            
            if (reqs.RemovalRequirements != null)
            {
                var groupName = reqs.RemovalRequirements.HasName ? reqs.RemovalRequirements.Name : null;
                CollectTagGroupTags(records, asset, assetType, reqs.RemovalRequirements, targetType, "Removal", groupName);
            }
        }

        private void CollectTagGroupTags(List<RequirementRecord> records, ScriptableObject asset, string assetType, AvoidRequireTagGroup group, string targetType, string context, string parentName)
        {
            if (group == null) return;
            
            var groupName = group.HasName ? group.Name : parentName;
            
            if (group.RequireTags != null)
            {
                records.AddRange(group.RequireTags.Where(t => t?.Tag != null).Select(tag => new RequirementRecord
                {
                    Asset = asset,
                    TagName = tag.Tag.Name,
                    Tag = tag.Tag,
                    TargetType = targetType,
                    ReqType = "Required",
                    AssetType = assetType,
                    Context = context,
                    GroupName = groupName
                }));
            }
            
            if (group.AvoidTags != null)
            {
                records.AddRange(group.AvoidTags.Where(t => t?.Tag != null).Select(tag => new RequirementRecord
                {
                    Asset = asset,
                    TagName = tag.Tag.Name,
                    Tag = tag.Tag,
                    TargetType = targetType,
                    ReqType = "Avoid",
                    AssetType = assetType,
                    Context = context,
                    GroupName = groupName
                }));
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements Filter Bar
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildRequirementsFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.flexWrap = Wrap.Wrap;
            filterBar.style.marginBottom = 8;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;
            filterBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            filterBar.style.borderTopLeftRadius = 4;
            filterBar.style.borderTopRightRadius = 4;
            filterBar.style.borderBottomLeftRadius = 4;
            filterBar.style.borderBottomRightRadius = 4;
            contentContainer.Add(filterBar);

            var label = CreateLabel("Filter:", 50, 10, Colors.HintText);
            label.style.alignSelf = Align.Center;
            filterBar.Add(label);
            
            filterBar.Add(CreateRequirementFilterButton("All", null));
            
            // Separator
            filterBar.Add(CreateFilterSeparator());
            
            // Target type filters
            filterBar.Add(CreateRequirementFilterButton("Source", "Source"));
            filterBar.Add(CreateRequirementFilterButton("Target", "Target"));
            
            // Separator
            filterBar.Add(CreateFilterSeparator());
            
            // Requirement type filters
            filterBar.Add(CreateRequirementFilterButton("✓ Required", "Required"));
            filterBar.Add(CreateRequirementFilterButton("✗ Avoid", "Avoid"));
            
            // Separator
            filterBar.Add(CreateFilterSeparator());
            
            // Context filters
            var contexts = GetRequirementsCache().Select(r => r.Context).Distinct().OrderBy(c => c).ToList();
            foreach (var ctx in contexts)
            {
                filterBar.Add(CreateRequirementFilterButton(ctx, $"ctx:{ctx}"));
            }

            filterBar.Add(CreateFilterSeparator());

            // Named-groups-only toggle — surfaces explicitly-authored groups so designers can
            // audit naming conventions without hunting through anonymous defaults.
            var namedBtn = new Button { text = requirementsNamedOnly ? "🏷 Named Only ✓" : "🏷 Named Only" };
            StyleSmallFilterButton(namedBtn, requirementsNamedOnly, Colors.AccentCyan);
            namedBtn.tooltip = "Show only requirements that belong to a named AvoidRequireTagGroup";
            namedBtn.clicked += () => { requirementsNamedOnly = !requirementsNamedOnly; ShowTab(1); };
            filterBar.Add(namedBtn);
        }
        
        private VisualElement CreateFilterSeparator()
        {
            var sep = new VisualElement();
            sep.style.width = 1;
            sep.style.height = 16;
            sep.style.backgroundColor = Colors.BorderDark;
            sep.style.marginLeft = 4;
            sep.style.marginRight = 4;
            sep.style.alignSelf = Align.Center;
            return sep;
        }
        
        private Button CreateRequirementFilterButton(string text, string filterValue)
        {
            bool isSelected = selectedRequirementFilter == filterValue;
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
            btn.style.backgroundColor = isSelected ? RequirementColor : Colors.ButtonBackground;
            btn.style.color = isSelected ? Colors.HeaderText : Colors.LabelText;
            btn.clicked += () => { selectedRequirementFilter = filterValue; ShowTab(1); };
            return btn;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements List View
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildRequirementsListView(VisualElement container)
        {
            var records = GetRequirementsCache().AsEnumerable();
            
            // Apply filters
            if (!string.IsNullOrEmpty(selectedRequirementFilter))
            {
                if (selectedRequirementFilter == "Source" || selectedRequirementFilter == "Target")
                    records = records.Where(r => r.TargetType == selectedRequirementFilter);
                else if (selectedRequirementFilter == "Required" || selectedRequirementFilter == "Avoid")
                    records = records.Where(r => r.ReqType == selectedRequirementFilter);
                else if (selectedRequirementFilter.StartsWith("ctx:"))
                {
                    var ctx = selectedRequirementFilter.Substring(4);
                    records = records.Where(r => r.Context == ctx);
                }
            }

            if (requirementsNamedOnly)
                records = records.Where(r => !string.IsNullOrEmpty(r.GroupName));
            
            // Apply search
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                records = records.Where(r => 
                    r.TagName.ToLower().Contains(filter) || 
                    r.Asset.name.ToLower().Contains(filter) ||
                    GetAssetDisplayName(r.Asset).ToLower().Contains(filter) ||
                    (!string.IsNullOrEmpty(r.GroupName) && r.GroupName.ToLower().Contains(filter)));
            }
            
            var recordList = records.ToList();
            
            // Group by tag name (encoded — used as identity); the display label is
            // projected from the first record's Tag so the user reads the friendly form.
            var grouped = recordList
                .GroupBy(r => r.TagName)
                .OrderByDescending(g => g.Count())
                .ToList();

            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null)
                resultsLabel.text = $"{grouped.Count} tags ({recordList.Count} total requirements)";

            foreach (var group in grouped)
            {
                var tagDisplay = GetTagDisplay(group.First().Tag);
                container.Add(CreateRequirementTagRow(group.Key, tagDisplay, group.ToList()));
            }

            if (!grouped.Any())
                container.Add(CreateEmptyLabel("No requirements found"));
        }

        private VisualElement CreateRequirementTagRow(string tagName, string tagDisplay, List<RequirementRecord> records)
        {
            var tagKey = $"req_{tagName}";
            bool isExpanded = expandedRequirements.Contains(tagKey);
            
            var tagContainer = new VisualElement();
            tagContainer.style.marginBottom = 2;
            
            // Stats
            int requiredCount = records.Count(r => r.ReqType == "Required");
            int avoidCount = records.Count(r => r.ReqType == "Avoid");
            int sourceCount = records.Count(r => r.TargetType == "Source");
            int targetCount = records.Count(r => r.TargetType == "Target");
            int namedCount = records.Count(r => !string.IsNullOrEmpty(r.GroupName));
            
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
            row.style.borderLeftColor = RequirementColor;
            tagContainer.Add(row);
            
            row.Add(CreateLabel(isExpanded ? "▼" : "▶", 16, 9, Colors.HintText));
            var tagLabel = CreateLabel(tagDisplay, 180, 11, Colors.LabelText, false, FontStyle.Bold);
            tagLabel.tooltip = tagName;
            row.Add(tagLabel);
            row.Add(CreateLabel($"{records.Count} uses", 70, 10, Colors.HintText));
            
            // Badges
            var badgeContainer = new VisualElement();
            badgeContainer.style.flexDirection = FlexDirection.Row;
            badgeContainer.style.flexGrow = 1;
            badgeContainer.style.flexWrap = Wrap.Wrap;
            
            if (requiredCount > 0)
                badgeContainer.Add(CreateBadge($"✓ Required ({requiredCount})", 0, Colors.AccentGreen));
            if (avoidCount > 0)
                badgeContainer.Add(CreateBadge($"✗ Avoid ({avoidCount})", 0, Colors.AccentRed));
            if (sourceCount > 0)
                badgeContainer.Add(CreateBadge($"Source ({sourceCount})", 0, Colors.AccentBlue));
            if (targetCount > 0)
                badgeContainer.Add(CreateBadge($"Target ({targetCount})", 0, Colors.AccentPurple));
            if (namedCount > 0)
                badgeContainer.Add(CreateBadge($"Named ({namedCount})", 0, Colors.AccentCyan));
            
            row.Add(badgeContainer);
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Colors.ButtonHover);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Colors.ItemBackground);
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (expandedRequirements.Contains(tagKey)) 
                    expandedRequirements.Remove(tagKey); 
                else 
                    expandedRequirements.Add(tagKey);
                RefreshViewList();
            });
            
            if (isExpanded)
                tagContainer.Add(CreateRequirementExpandedContent(records));
            
            return tagContainer;
        }
        
        private VisualElement CreateRequirementExpandedContent(List<RequirementRecord> records)
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
            content.style.borderLeftColor = new Color(RequirementColor.r, RequirementColor.g, RequirementColor.b, 0.5f);
            
            // Group by requirement type combination
            var groups = records
                .GroupBy(r => $"{r.ReqType} ({r.TargetType}) - {r.Context}")
                .OrderBy(g => g.Key);
            
            foreach (var group in groups)
            {
                content.Add(CreateSectionLabel(group.Key, content.childCount > 0 ? 8 : 0));
                
                foreach (var record in group)
                {
                    var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == record.Asset.GetType());

                    var assetRow = new VisualElement();
                    assetRow.style.flexDirection = FlexDirection.Row;
                    assetRow.style.alignItems = Align.Center;
                    assetRow.style.paddingTop = 2;
                    assetRow.style.paddingBottom = 2;
                    assetRow.style.marginBottom = 1;

                    assetRow.RegisterCallback<MouseEnterEvent>(_ => assetRow.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 0.5f));
                    assetRow.RegisterCallback<MouseLeaveEvent>(_ => assetRow.style.backgroundColor = Color.clear);
                    assetRow.RegisterCallback<ClickEvent>(_ => { Selection.activeObject = record.Asset; EditorGUIUtility.PingObject(record.Asset); });

                    assetRow.Add(CreateLabel(typeInfo?.Icon ?? "?", 20, 10, typeInfo?.Color ?? Colors.HintText));

                    if (!string.IsNullOrEmpty(record.GroupName))
                    {
                        assetRow.Add(CreateLabel($"\"{record.GroupName}\"", 120, 10, Colors.AccentCyan));
                        assetRow.Add(CreateLabel("—", 16, 10, Colors.HintText));
                    }

                    assetRow.Add(CreateLabel(GetAssetDisplayName(record.Asset), 0, 10, Colors.LabelText, true));
                    assetRow.Add(CreateBadge(record.AssetType, 50, typeInfo?.Color ?? Colors.HintText));

                    content.Add(assetRow);
                }
            }
            
            return content;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Scalers List View (renamed from Modifiers)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildScalersFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.flexWrap = Wrap.Wrap;
            filterBar.style.marginBottom = 8;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;
            filterBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            filterBar.style.borderTopLeftRadius = 4;
            filterBar.style.borderTopRightRadius = 4;
            filterBar.style.borderBottomLeftRadius = 4;
            filterBar.style.borderBottomRightRadius = 4;
            contentContainer.Add(filterBar);

            var label = CreateLabel("Type:", 40, 10, Colors.HintText);
            label.style.alignSelf = Align.Center;
            filterBar.Add(label);
            
            filterBar.Add(CreateScalerTypeFilterButton("All", null));
            
            // Get unique scaler types
            var types = GetScalerCache().Select(r => r.ScalerTypeName).Distinct().OrderBy(t => t).ToList();
            foreach (var type in types)
            {
                filterBar.Add(CreateScalerTypeFilterButton(type, type));
            }
            
            filterBar.Add(CreateFilterSeparator());
            
            var ctxLabel = CreateLabel("Context:", 50, 10, Colors.HintText);
            ctxLabel.style.alignSelf = Align.Center;
            filterBar.Add(ctxLabel);
            
            filterBar.Add(CreateScalerContextFilterButton("All", null));
            
            // Get unique contexts
            var contexts = GetScalerCache().Select(r => r.Context).Distinct().OrderBy(c => c).ToList();
            foreach (var ctx in contexts)
            {
                filterBar.Add(CreateScalerContextFilterButton(ctx, ctx));
            }
            
            filterBar.Add(CreateFilterSeparator());
            
            var assetLabel = CreateLabel("Asset:", 40, 10, Colors.HintText);
            assetLabel.style.alignSelf = Align.Center;
            filterBar.Add(assetLabel);
            
            filterBar.Add(CreateScalerAssetTypeFilterButton("All", null));
            filterBar.Add(CreateScalerAssetTypeFilterButton("⚡ Ability", typeof(Ability)));
            filterBar.Add(CreateScalerAssetTypeFilterButton("✦ Effect", typeof(GameplayEffect)));
            filterBar.Add(CreateScalerAssetTypeFilterButton("📦 Item", typeof(Item)));

            filterBar.Add(CreateFilterSeparator());

            // Level Config — surfaces which scalers lock to their host's level vs. self-managed.
            var lvlLabel = CreateLabel("Level Cfg:", 60, 10, Colors.HintText);
            lvlLabel.style.alignSelf = Align.Center;
            filterBar.Add(lvlLabel);

            filterBar.Add(CreateScalerLevelConfigChip("All",       null));
            filterBar.Add(CreateScalerLevelConfigChip("Lock",      ELevelConfig.LockToLevelProvider));
            filterBar.Add(CreateScalerLevelConfigChip("Unlocked",  ELevelConfig.Unlocked));
            filterBar.Add(CreateScalerLevelConfigChip("Clamped",   ELevelConfig.Clamped));
        }

        private Button CreateScalerLevelConfigChip(string text, ELevelConfig? value)
        {
            bool isSelected = selectedScalerLevelConfigFilter == value;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, Colors.AccentGreen);
            btn.clicked += () => { selectedScalerLevelConfigFilter = value; ShowTab(1); };
            return btn;
        }
        
        private Button CreateScalerTypeFilterButton(string text, string filterValue)
        {
            bool isSelected = selectedScalerTypeFilter == filterValue;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, Colors.AccentPurple);
            btn.clicked += () => { selectedScalerTypeFilter = filterValue; ShowTab(1); };
            return btn;
        }
        
        private Button CreateScalerContextFilterButton(string text, string filterValue)
        {
            bool isSelected = selectedScalerContextFilter == filterValue;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, Colors.AccentBlue);
            btn.clicked += () => { selectedScalerContextFilter = filterValue; ShowTab(1); };
            return btn;
        }
        
        private Button CreateScalerAssetTypeFilterButton(string text, Type filterValue)
        {
            bool isSelected = selectedScalerAssetTypeFilter == filterValue;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, Colors.AccentCyan);
            btn.clicked += () => { selectedScalerAssetTypeFilter = filterValue; ShowTab(1); };
            return btn;
        }
        
        private void StyleSmallFilterButton(Button btn, bool isSelected, Color accentColor)
        {
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
            btn.style.backgroundColor = isSelected ? accentColor : Colors.ButtonBackground;
            btn.style.color = isSelected ? Colors.HeaderText : Colors.LabelText;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Unified Filter Primitives
        //
        // All four view modes (Standard / Tags / Scalers / Requirements) used to roll
        // their own filter bars with subtly-different paddings, backgrounds, and chip
        // styles. These helpers funnel them into one consistent visual language so
        // the user can move between views without re-learning the layout.
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adds a filter "strip" container (rounded card, dark backdrop) to the content
        /// area and returns its root element so callers can append filter sections.
        /// </summary>
        private VisualElement CreateFilterStrip()
        {
            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.alignItems = Align.Center;
            strip.style.marginBottom = 8;
            strip.style.paddingLeft = 8;
            strip.style.paddingRight = 8;
            strip.style.paddingTop = 5;
            strip.style.paddingBottom = 5;
            strip.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            strip.style.borderTopLeftRadius = 4;
            strip.style.borderTopRightRadius = 4;
            strip.style.borderBottomLeftRadius = 4;
            strip.style.borderBottomRightRadius = 4;
            return strip;
        }

        /// <summary>
        /// Inline label that introduces a filter section ("Context:", "Sort:", …).
        /// </summary>
        private Label CreateFilterSectionLabel(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 10;
            lbl.style.color = Colors.HintText;
            lbl.style.marginRight = 6;
            lbl.style.alignSelf = Align.Center;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            return lbl;
        }

        /// <summary>
        /// One uniform filter chip — used by all view modes. Selected chips get a
        /// softly-tinted backdrop in the section's accent color; idle chips share
        /// the standard button background. Optional <paramref name="count"/> shows
        /// in muted parentheses next to the label.
        /// </summary>
        private Button CreateFilterChip(Func<string> text, Func<bool> isSelected, Color accentColor, Action onClick, int count = -1, string tooltip = null)
        {
            var btn = new Button { focusable = false };
            btn.tooltip = tooltip;
            btn.style.fontSize = 10;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.marginRight = 4;
            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;

            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;

            var label = new Label(text()) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 10;
            label.style.color = isSelected() ? accentColor : Colors.LabelText;
            content.Add(label);

            if (count >= 0)
            {
                var c = new Label($" ({count})") { pickingMode = PickingMode.Ignore };
                c.style.fontSize = 9;
                c.style.color = Colors.HintText;
                content.Add(c);
            }
            btn.Add(content);

            btn.RegisterCallback<MouseEnterEvent>(_ => { if (!isSelected()) btn.style.backgroundColor = Colors.ButtonHover; });
            btn.RegisterCallback<MouseLeaveEvent>(_ => Style());
            if (onClick != null) btn.clicked += onClick;
            btn.clicked += Style;
            
            Style();
            
            return btn;
            
            void Style()
            {
                if (isSelected())
                {
                    btn.style.backgroundColor = new Color(accentColor.r * 0.35f, accentColor.g * 0.35f, accentColor.b * 0.35f, 0.85f);
                    btn.style.color = accentColor;
                    btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    btn.style.backgroundColor = Colors.ButtonBackground;
                    btn.style.color = Colors.LabelText;
                    btn.style.unityFontStyleAndWeight = FontStyle.Normal;
                }

                label.text = text();
                label.style.color = isSelected() ? accentColor : Colors.LabelText;
            }
        }

        /// <summary>Vertical hairline used between filter sections inside a strip.</summary>
        private VisualElement CreateSectionDivider()
        {
            var sep = new VisualElement();
            sep.style.width = 1;
            sep.style.height = 18;
            sep.style.backgroundColor = Colors.BorderDark;
            sep.style.marginLeft = 6;
            sep.style.marginRight = 6;
            sep.style.alignSelf = Align.Center;
            return sep;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Display Helpers
        //
        // A Tag's `Name` may carry deterministic hash padding (e.g. "PositionXyZ12")
        // while its `DisplayName` is the original human-readable input (e.g. "Position").
        // Hierarchical tags mirror dot structure between the two fields, so display-side
        // segments can be obtained by splitting DisplayName, while identity / grouping
        // / filter comparisons must continue to use Name. These helpers funnel the
        // projection through one place so the View tab never accidentally renders a
        // padded name to the user.
        // ═══════════════════════════════════════════════════════════════════════════

        private static string GetTagDisplay(Tag tag)
            => string.IsNullOrEmpty(tag.DisplayName) ? tag.Name : tag.DisplayName;

        private static string GetTagLeafDisplay(Tag tag)
        {
            var s = GetTagDisplay(tag);
            int lastDot = s.LastIndexOf('.');
            return lastDot < 0 ? s : s.Substring(lastDot + 1);
        }

        private static string GetTagRootDisplay(Tag tag)
        {
            var s = GetTagDisplay(tag);
            int firstDot = s.IndexOf('.');
            return firstDot < 0 ? s : s.Substring(0, firstDot);
        }

        private static string GetTagParentDisplay(Tag tag)
        {
            var s = GetTagDisplay(tag);
            int lastDot = s.LastIndexOf('.');
            return lastDot < 0 ? null : s.Substring(0, lastDot);
        }

        private static string[] GetTagDisplaySegments(Tag tag)
            => GetTagDisplay(tag).Split('.');
        
        private void BuildScalersListView(VisualElement container)
        {
            var records = GetScalerCache().AsEnumerable();
            
            // Apply type filter
            if (!string.IsNullOrEmpty(selectedScalerTypeFilter))
                records = records.Where(r => r.ScalerTypeName == selectedScalerTypeFilter);
            
            // Apply context filter
            if (!string.IsNullOrEmpty(selectedScalerContextFilter))
                records = records.Where(r => r.Context == selectedScalerContextFilter);
            
            // Apply asset type filter
            if (selectedScalerAssetTypeFilter != null)
                records = records.Where(r => r.Asset.GetType() == selectedScalerAssetTypeFilter);

            // Apply level-config filter
            if (selectedScalerLevelConfigFilter.HasValue)
                records = records.Where(r => r.Scaler != null && r.Scaler.Configuration == selectedScalerLevelConfigFilter.Value);
            
            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                records = records.Where(r => 
                    r.Asset.name.ToLower().Contains(filter) || 
                    r.ScalerTypeName.ToLower().Contains(filter) || 
                    r.Context.ToLower().Contains(filter) ||
                    GetAssetDisplayName(r.Asset).ToLower().Contains(filter) ||
                    (!string.IsNullOrEmpty(r.Scaler.Name) && r.Scaler.Name.ToLower().Contains(filter)));
            }
            var recordList = records.ToList();
            
            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null) resultsLabel.text = $"{recordList.Count} scalers";
            
            foreach (var group in recordList.GroupBy(r => r.ScalerTypeName).OrderBy(g => g.Key))
            {
                var groupHeader = CreateGroupHeader("◆", group.Key, group.Count(), Colors.AccentPurple);
                container.Add(groupHeader);
                
                foreach (var record in group)
                    container.Add(CreateScalerRow(record));
            }
            
            if (!recordList.Any())
                container.Add(CreateEmptyLabel("No scalers found"));
        }
        
        private VisualElement CreateScalerRow(ScalerRecord record)
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
            
            // Show scaler name if available
            if (!string.IsNullOrEmpty(record.Scaler.Name))
            {
                row.Add(CreateLabel($"\"{record.Scaler.Name}\"", 100, 10, Colors.AccentCyan));
                row.Add(CreateLabel("—", 16, 10, Colors.HintText));
            }
            
            row.Add(CreateLabel(GetAssetDisplayName(record.Asset), 120, 11, Colors.LabelText, true));
            
            if (typeInfo != null)
                row.Add(CreateBadge(typeInfo.DisplayName, 60, typeInfo.Color));
            
            row.Add(CreateBadge(record.Context, 70, Colors.AccentBlue));
            
            var modeText = record.Scaler.Configuration switch { ELevelConfig.LockToLevelProvider => "Lock", ELevelConfig.Unlocked => "Unlk", ELevelConfig.Clamped => "Part", _ => "?" };
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tags List View with Expansion
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagContextFilterBar()
        {
            // ─── Strip 1: Context ────────────────────────────────────────────
            var ctxStrip = CreateFilterStrip();
            contentContainer.Add(ctxStrip);

            ctxStrip.Add(CreateFilterSectionLabel("Context:"));
            ctxStrip.Add(CreateFilterChip(() => "All", () => selectedTagContextFilter == null, Colors.AccentCyan,
                () => { selectedTagContextFilter = null; ShowTab(1); }));

            // Use TagContext.FriendlyName for display ("Effect / Cost") but key by
            // ContextKey ("Effect+Cost") so the existing filter pipeline keeps working.
            foreach (var ctx in ForgeTagRegistry.GetAllContexts().OrderBy(c => c.FriendlyName))
            {
                var capturedKey = ctx.ContextKey;
                ctxStrip.Add(CreateFilterChip(() => ctx.FriendlyName, () => selectedTagContextFilter == capturedKey, Colors.AccentCyan,
                    () => { selectedTagContextFilter = capturedKey; ShowTab(1); }));
            }

            // ─── Strip 2: Hierarchy / Quality / Sort / Display ───────────────
            var qStrip = CreateFilterStrip();
            contentContainer.Add(qStrip);

            // Root picker — popup menu when too many to fit as chips.
            // Group by encoded root name (used as filter key) but pick a friendly
            // display label from one of the tags whose root that is.
            qStrip.Add(CreateFilterSectionLabel("Root:"));

            var rootEntries = ForgeTagRegistry.GetAllTags()
                .GroupBy(t => t.GetRoot().Name)
                .Select(g => new { Key = g.Key, Display = GetTagRootDisplay(g.First()) })
                .OrderBy(x => x.Display)
                .ToList();

            qStrip.Add(CreateFilterChip(() => "All", () => selectedTagParentFilter == null, Colors.AccentPurple,
                () => { selectedTagParentFilter = null; ShowTab(1); }));

            const int rootInlineLimit = 6;
            foreach (var entry in rootEntries.Take(rootInlineLimit))
            {
                var capturedKey = entry.Key;
                qStrip.Add(CreateFilterChip(() => entry.Display, () => selectedTagParentFilter == capturedKey, Colors.AccentPurple,
                    () => { selectedTagParentFilter = capturedKey; ShowTab(1); }));
            }
            if (rootEntries.Count > rootInlineLimit)
            {
                var overflow = rootEntries.Skip(rootInlineLimit).ToList();
                var overflowSelectedEntry = overflow.FirstOrDefault(e => e.Key == selectedTagParentFilter);
                bool overflowSelected = overflowSelectedEntry != null;
                var label = overflowSelected ? overflowSelectedEntry.Display + " ▾" : $"More ({overflow.Count}) ▾";
                qStrip.Add(CreateFilterChip(() => label, () => overflowSelected, Colors.AccentPurple, () =>
                {
                    var menu = new GenericMenu();
                    foreach (var e in overflow)
                    {
                        var captured = e.Key;
                        menu.AddItem(new GUIContent(e.Display), selectedTagParentFilter == captured, () =>
                        {
                            selectedTagParentFilter = captured;
                            ShowTab(1);
                        });
                    }
                    menu.ShowAsContext();
                }, tooltip: "Select another root tag"));
            }

            qStrip.Add(CreateSectionDivider());

            // Source: System vs Authored
            qStrip.Add(CreateFilterSectionLabel("Source:"));
            qStrip.Add(CreateFilterChip(() => "All",      () => selectedTagSourceFilter == null,       Colors.PolicyPurple, () => { selectedTagSourceFilter = null;        ShowTab(1); }));
            qStrip.Add(CreateFilterChip(() => "System",   () => selectedTagSourceFilter == "System",   Colors.PolicyPurple, () => { selectedTagSourceFilter = "System";    ShowTab(1); }, tooltip: "Default system tags"));
            qStrip.Add(CreateFilterChip(() => "Authored", () => selectedTagSourceFilter == "Authored", Colors.PolicyPurple, () => { selectedTagSourceFilter = "Authored";  ShowTab(1); }, tooltip: "Tags introduced by user assets"));

            qStrip.Add(CreateSectionDivider());

            // Usage: Used vs Orphan
            qStrip.Add(CreateFilterSectionLabel("Usage:"));
            qStrip.Add(CreateFilterChip(() => "All",    () => selectedTagUsageFilter == null,     Colors.AccentOrange, () => { selectedTagUsageFilter = null;     ShowTab(1); }));
            qStrip.Add(CreateFilterChip(() => "Used",   () => selectedTagUsageFilter == "Used",   Colors.AccentOrange, () => { selectedTagUsageFilter = "Used";   ShowTab(1); }, tooltip: "Tags referenced by at least one asset"));
            qStrip.Add(CreateFilterChip(() => "Orphan", () => selectedTagUsageFilter == "Orphan", Colors.AccentOrange, () => { selectedTagUsageFilter = "Orphan"; ShowTab(1); }, tooltip: "Tags with zero usages"));

            qStrip.Add(CreateSectionDivider());

            // Sort
            qStrip.Add(CreateFilterSectionLabel("Sort:"));
            qStrip.Add(CreateFilterChip(() => "Most Used",    () => tagSortMode == ETagSortMode.UsageDesc, Colors.AccentBlue, () => { tagSortMode = ETagSortMode.UsageDesc; ShowTab(1); }));
            qStrip.Add(CreateFilterChip(() => "A → Z",        () => tagSortMode == ETagSortMode.AlphaAsc,  Colors.AccentBlue, () => { tagSortMode = ETagSortMode.AlphaAsc;  ShowTab(1); }));
            qStrip.Add(CreateFilterChip(() => "Depth",        () => tagSortMode == ETagSortMode.DepthAsc,  Colors.AccentBlue, () => { tagSortMode = ETagSortMode.DepthAsc;  ShowTab(1); }, tooltip: "Roots first, then nested"));

            qStrip.Add(CreateSectionDivider());

            // Display toggles
            qStrip.Add(CreateFilterSectionLabel("Display:"));
            var fullPathToggle = CreateFilterChip(() => showFullTagPath ? "Full Path ✓" : "Full Path",
                () => showFullTagPath, Colors.AccentGreen,
                () => { showFullTagPath = !showFullTagPath; RefreshViewList(); },
                tooltip: "Show full hierarchical path on each row");
            var groupResultsToggle = CreateFilterChip(() => groupTagResults ? "Group by Root ✓" : "Group by Root",
                () => groupTagResults, Colors.AccentGreen,
                () => { groupTagResults = !groupTagResults; RefreshViewList(); },
                tooltip: "Group tags under their root category");
            
            qStrip.Add(fullPathToggle);
            qStrip.Add(groupResultsToggle);
            
            void RefreshToggleOptions(Button button)
            {
                
            }
        }
        
        private void BuildTagsListView(VisualElement container)
        {
            var tagRecords = ForgeTagRegistry.GetAllTagRecords();

            // Search — full path OR leaf name (case-insensitive)
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filterLower = searchFilter.ToLower();
                tagRecords = tagRecords.Where(r =>
                    r.Tag.DisplayName.ToLower().Contains(filterLower) ||
                    r.Tag.GetLeafName().ToLower().Contains(filterLower));
            }

            // Context
            if (!string.IsNullOrEmpty(selectedTagContextFilter))
                tagRecords = tagRecords.Where(r => r.UsageByContext.ContainsKey(selectedTagContextFilter));

            // Root / parent
            if (!string.IsNullOrEmpty(selectedTagParentFilter))
            {
                tagRecords = tagRecords.Where(r =>
                    r.Tag.GetRoot().DisplayName == selectedTagParentFilter ||
                    r.Tag.DisplayName.StartsWith(selectedTagParentFilter + "."));
            }

            // Source: System / Authored
            if (selectedTagSourceFilter == "System")
                tagRecords = tagRecords.Where(r => r.IsSystemDefault);
            else if (selectedTagSourceFilter == "Authored")
                tagRecords = tagRecords.Where(r => !r.IsSystemDefault);

            // Usage: Used / Orphan
            if (selectedTagUsageFilter == "Used")
                tagRecords = tagRecords.Where(r => r.TotalUsageCount > 0);
            else if (selectedTagUsageFilter == "Orphan")
                tagRecords = tagRecords.Where(r => r.TotalUsageCount == 0);

            // Sort
            var sorted = tagSortMode switch
            {
                ETagSortMode.AlphaAsc  => tagRecords.OrderBy(r => r.Tag.DisplayName).ToList(),
                ETagSortMode.DepthAsc  => tagRecords.OrderBy(r => r.Tag.Depth).ThenBy(r => r.Tag.DisplayName).ToList(),
                _                      => tagRecords.OrderByDescending(r => r.TotalUsageCount).ThenBy(r => r.Tag.DisplayName).ToList(),
            };

            // Results readout
            var resultsLabel = rootVisualElement.Q<Label>("ResultsCount");
            if (resultsLabel != null)
            {
                var rootCount = sorted.Select(r => r.Tag.GetRoot().DisplayName).Distinct().Count();
                resultsLabel.text = $"{sorted.Count} tag{(sorted.Count == 1 ? "" : "s")} · {rootCount} root{(rootCount == 1 ? "" : "s")}";
            }

            // Render — Group by root respects the toggle even when searching/filtering.
            // Group key is the encoded root Name (correct uniqueness); the header label
            // pulls the friendly DisplayName from any tag in the group.
            if (groupTagResults)
            {
                var grouped = sorted
                    .GroupBy(r => r.Tag.GetRoot().DisplayName)
                    .OrderBy(g => GetTagRootDisplay(g.First().Tag));

                foreach (var group in grouped)
                {
                    var rootDisplay = GetTagRootDisplay(group.First().Tag);
                    container.Add(CreateTagRootHeader(rootDisplay, group.Count(), group.Sum(r => r.TotalUsageCount)));
                    foreach (var record in group)
                        container.Add(CreateTagRowWithExpansion(record));
                }
            }
            else
            {
                foreach (var record in sorted)
                    container.Add(CreateTagRowWithExpansion(record));
            }

            if (!sorted.Any())
                container.Add(CreateEmptyLabel("No tags match the current filters."));
        }
        
        private VisualElement CreateTagRootHeader(string rootName, int tagCount, int totalUsages)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 12;
            header.style.marginBottom = 4;
            header.style.paddingLeft = 8;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.22f, 0.8f);
            header.style.borderTopLeftRadius = 4;
            header.style.borderTopRightRadius = 4;
            header.style.borderBottomLeftRadius = 4;
            header.style.borderBottomRightRadius = 4;
            header.style.borderLeftWidth = 3;
            header.style.borderLeftColor = Colors.AccentPurple;
    
            header.Add(CreateLabel(Icons.Arrow, 20, 12, Colors.AccentPurple));
            header.Add(CreateLabel(rootName, 0, 11, Colors.HeaderText, true, FontStyle.Bold));
            header.Add(CreateLabel($"{tagCount} tags", 70, 10, Colors.HintText));
            header.Add(CreateLabel($"{totalUsages} uses", 70, 10, Colors.HintText));
    
            return header;
        }
        
        private VisualElement CreateTagRowWithExpansion(ForgeTagRegistry.TagUsageRecord record)
        {
            var tag = record.Tag;
            var tagKey = tag.Name;
            bool isExpanded = expandedTags.Contains(tagKey);
            bool isOrphan = record.TotalUsageCount == 0;
            int depth = Mathf.Max(1, tag.Depth);

            // Indent purely via padding — no glyph prefix on the leaf.
            // Depth-decayed left-border tint gives a subtle hierarchy cue.
            float depthFade = Mathf.Clamp01(1f - (depth - 1) * 0.15f);
            Color borderColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, depthFade);
            int indentPx = showFullTagPath ? 8 : 8 + (depth - 1) * 14;

            var tagContainer = new VisualElement();
            tagContainer.style.marginBottom = 2;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = indentPx;
            row.style.paddingRight = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.backgroundColor = Colors.ItemBackground;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = isExpanded ? 0 : 4;
            row.style.borderBottomRightRadius = isExpanded ? 0 : 4;
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = borderColor;
            tagContainer.Add(row);

            // Chevron
            var chevron = CreateLabel(isExpanded ? "▼" : "▶", 16, 9, Colors.HintText);
            chevron.style.marginRight = 4;
            row.Add(chevron);

            // Tag name — leaf bold, parent path muted prefix when not "Full Path".
            // Always render the user-facing DisplayName (ToString-equivalent) so
            // deterministic-hash padding never leaks into the UI.
            var displayPath   = GetTagDisplay(tag);
            var displayLeaf   = GetTagLeafDisplay(tag);
            var displayParent = GetTagParentDisplay(tag);

            var nameContainer = new VisualElement();
            nameContainer.style.flexDirection = FlexDirection.Row;
            nameContainer.style.alignItems = Align.Center;
            nameContainer.style.flexGrow = 1;
            nameContainer.style.minWidth = 180;
            nameContainer.tooltip = displayPath;
            row.Add(nameContainer);

            if (showFullTagPath || depth == 1)
            {
                var name = new Label(displayPath);
                name.style.fontSize = 11;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.color = Colors.LabelText;
                nameContainer.Add(name);
            }
            else
            {
                // "Parent.Path." in muted, leaf in bold — easier scan than "└ leaf".
                var prefix = new Label((displayParent ?? "") + ".");
                prefix.style.fontSize = 10;
                prefix.style.color = Colors.HintText;
                prefix.style.marginRight = 0;
                nameContainer.Add(prefix);

                var leaf = new Label(displayLeaf);
                leaf.style.fontSize = 11;
                leaf.style.unityFontStyleAndWeight = FontStyle.Bold;
                leaf.style.color = Colors.LabelText;
                nameContainer.Add(leaf);
            }

            // Usage count — colorize when zero (orphans) for quick scan
            var useLabel = new Label(isOrphan ? "unused" : $"{record.TotalUsageCount} use{(record.TotalUsageCount == 1 ? "" : "s")}");
            useLabel.style.width = 70;
            useLabel.style.fontSize = 10;
            useLabel.style.color = isOrphan ? Colors.HintText.Fade(0.7f) : Colors.LabelText;
            useLabel.style.unityFontStyleAndWeight = isOrphan ? FontStyle.Italic : FontStyle.Normal;
            useLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            useLabel.style.marginRight = 8;
            row.Add(useLabel);

            // All context badges (no truncation). Wrap is enabled so dense rows breathe
            // onto extra lines instead of clipping.
            var contextContainer = new VisualElement();
            contextContainer.style.flexDirection = FlexDirection.Row;
            contextContainer.style.alignItems = Align.Center;
            contextContainer.style.flexWrap = Wrap.Wrap;
            foreach (var ctx in record.UsageByContext.OrderByDescending(c => c.Value.Usages.Count))
                contextContainer.Add(CreateBadge($"{ctx.Value.Context.FriendlyName} {ctx.Value.Usages.Count}", 0, Colors.AccentBlue));
            row.Add(contextContainer);

            // Trailing badges: depth (when not Full Path), system, orphan
            row.Add(CreateSpacer());

            if (depth > 1 && !showFullTagPath)
            {
                var depthBadge = CreateBadge($"d{depth}", 0, Colors.AccentPurple);
                depthBadge.tooltip = $"Depth {depth} — parent: {displayParent}";
                row.Add(depthBadge);
            }

            if (record.IsSystemDefault)
            {
                var sysBadge = CreateBadge("System", 0, Colors.PolicyPurple);
                sysBadge.tooltip = "Default system tag";
                row.Add(sysBadge);
            }

            if (isOrphan && !record.IsSystemDefault)
            {
                var orphanBadge = CreateBadge("Orphan", 0, Colors.AccentOrange);
                orphanBadge.tooltip = "No assets currently reference this tag";
                row.Add(orphanBadge);
            }

            // Hover + click-to-expand
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
        
        private VisualElement CreateTagExpandedContent(ForgeTagRegistry.TagUsageRecord record)
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

            var tag = record.Tag;

            // ─── Breadcrumb path with copy button (also covers depth-1 tags) ─
            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.alignItems = Align.Center;
            pathRow.style.flexWrap = Wrap.Wrap;
            pathRow.style.marginBottom = 8;
            pathRow.style.paddingLeft = 6;
            pathRow.style.paddingRight = 6;
            pathRow.style.paddingTop = 4;
            pathRow.style.paddingBottom = 4;
            pathRow.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            pathRow.style.borderTopLeftRadius = 3;
            pathRow.style.borderTopRightRadius = 3;
            pathRow.style.borderBottomLeftRadius = 3;
            pathRow.style.borderBottomRightRadius = 3;
            content.Add(pathRow);

            var pathLabel = CreateLabel("Path:", 40, 9, Colors.HintText);
            pathLabel.style.alignSelf = Align.Center;
            pathRow.Add(pathLabel);

            // Segment arrays — `nameSegs` for filter routing (uses Tag.Name), `displaySegs`
            // for what the user reads (uses Tag.DisplayName). The two arrays are in lockstep
            // because hierarchical tags mirror dot structure between Name and DisplayName.
            var nameSegs    = tag.GetSegments();
            var displaySegs = GetTagDisplaySegments(tag);

            var accumulated = "";
            for (int i = 0; i < nameSegs.Length; i++)
            {
                if (i > 0)
                {
                    var sep = CreateLabel(" › ", 0, 10, Colors.HintText);
                    sep.style.marginRight = 0;
                    sep.style.alignSelf = Align.Center;
                    pathRow.Add(sep);
                }

                accumulated = i == 0 ? nameSegs[i] : accumulated + "." + nameSegs[i];
                string segPath = accumulated;                       // encoded — used for filtering
                string segDisplay = i < displaySegs.Length ? displaySegs[i] : nameSegs[i];
                bool isLast = i == nameSegs.Length - 1;
                bool isRoot = i == 0;

                var seg = new Button { text = segDisplay, focusable = false };
                seg.style.fontSize = 10;
                seg.style.paddingLeft = 6;
                seg.style.paddingRight = 6;
                seg.style.paddingTop = 2;
                seg.style.paddingBottom = 2;
                seg.style.marginRight = 0;
                seg.style.borderTopLeftRadius = 2;
                seg.style.borderTopRightRadius = 2;
                seg.style.borderBottomLeftRadius = 2;
                seg.style.borderBottomRightRadius = 2;
                seg.style.backgroundColor = isLast ? Colors.AccentCyan.Fade(0.3f) : new Color(0, 0, 0, 0);
                seg.style.color = isLast ? Colors.AccentCyan : Colors.LabelText;

                if (!isLast)
                {
                    // FIX: previously this always used `segPath.Split('.')[0]` so every
                    // breadcrumb segment filtered to the root regardless of which one
                    // was clicked. Drive the parent filter from the root segment, then
                    // pre-fill search with the full breadcrumb path so deep matches
                    // narrow down. Tooltip text uses display labels for readability.
                    var displayUpToHere = string.Join(".", displaySegs.Take(i + 1));
                    seg.tooltip = isRoot
                        ? $"Filter by root '{displaySegs[0]}'"
                        : $"Filter by root '{displaySegs[0]}' and narrow to '{displayUpToHere}'";
                    seg.clicked += () =>
                    {
                        selectedTagParentFilter = nameSegs[0];
                        searchFilter = isRoot ? "" : segPath;
                        ShowTab(1);
                    };
                    seg.RegisterCallback<MouseEnterEvent>(_ => seg.style.backgroundColor = Colors.ButtonHover);
                    seg.RegisterCallback<MouseLeaveEvent>(_ => seg.style.backgroundColor = new Color(0, 0, 0, 0));
                }
                else
                {
                    seg.tooltip = string.Join(".", displaySegs);
                }

                pathRow.Add(seg);
            }

            // Copy button — pinned to the right
            pathRow.Add(CreateSpacer());
            var copyBtn = new Button { text = "📋", tooltip = "Copy full path to clipboard", focusable = false };
            copyBtn.style.width = 24;
            copyBtn.style.height = 20;
            copyBtn.style.fontSize = 10;
            copyBtn.style.paddingLeft = 2;
            copyBtn.style.paddingRight = 2;
            copyBtn.style.marginLeft = 4;
            copyBtn.style.backgroundColor = Colors.ButtonBackground;
            copyBtn.style.borderTopLeftRadius = 3;
            copyBtn.style.borderTopRightRadius = 3;
            copyBtn.style.borderBottomLeftRadius = 3;
            copyBtn.style.borderBottomRightRadius = 3;
            copyBtn.clicked += () =>
            {
                // Copy the user-facing display path so the clipboard matches what they see.
                var copied = GetTagDisplay(tag);
                EditorGUIUtility.systemCopyBuffer = copied;
                Debug.Log($"Copied tag path: {copied}");
            };
            pathRow.Add(copyBtn);

            // Quick-stat strip: Depth · Parent · Source
            var metaRow = new VisualElement();
            metaRow.style.flexDirection = FlexDirection.Row;
            metaRow.style.alignItems = Align.Center;
            metaRow.style.flexWrap = Wrap.Wrap;
            metaRow.style.marginBottom = 8;
            content.Add(metaRow);

            metaRow.Add(CreateBadge($"Depth {tag.Depth}", 0, Colors.AccentPurple));
            if (tag.Depth > 1)
            {
                var parentBadge = CreateBadge($"Parent: {GetTagParentDisplay(tag)}", 0, Colors.HintText);
                parentBadge.tooltip = "Click breadcrumb above to filter to this branch";
                metaRow.Add(parentBadge);
            }
            metaRow.Add(CreateBadge(record.IsSystemDefault ? "System" : "Authored", 0, record.IsSystemDefault ? Colors.PolicyPurple : Colors.AccentGreen));
            if (record.TotalUsageCount == 0)
                metaRow.Add(CreateBadge("Orphan", 0, Colors.AccentOrange));
            
            // Usage by Context
            content.Add(CreateSectionLabel("Usage by Context", 8));
            foreach (var ctx in record.UsageByContext.OrderByDescending(c => c.Value.Usages.Count))
            {
                var ctxRow = new VisualElement();
                ctxRow.style.flexDirection = FlexDirection.Row;
                ctxRow.style.alignItems = Align.Center;
                ctxRow.style.marginBottom = 2;
                
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
            
            // Assets using this tag
            content.Add(CreateSectionLabel("Assets Using This Tag", 12));
            var assetsUsingTag = record.UsageByContext.Values
                .SelectMany(ctx => ctx.Assets)
                .Distinct()
                .ToList();

            if (assetsUsingTag.Count == 0)
            {
                content.Add(CreateLabel("No assets found", 140, 10, Colors.HintText));
            }

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

            return content;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Column List View (Improved Styling)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildColumnListView(VisualElement container, List<ScriptableObject> assets, List<ColumnDef> columns)
        {
            var tableContainer = new VisualElement();
            tableContainer.style.borderTopWidth = 1;
            tableContainer.style.borderBottomWidth = 1;
            tableContainer.style.borderLeftWidth = 1;
            tableContainer.style.borderRightWidth = 1;
            tableContainer.style.borderTopColor = TableBorderColor;
            tableContainer.style.borderBottomColor = TableBorderColor;
            tableContainer.style.borderLeftColor = TableBorderColor;
            tableContainer.style.borderRightColor = TableBorderColor;
            tableContainer.style.borderTopLeftRadius = 6;
            tableContainer.style.borderTopRightRadius = 6;
            tableContainer.style.borderBottomLeftRadius = 6;
            tableContainer.style.borderBottomRightRadius = 6;
            tableContainer.style.overflow = Overflow.Hidden;
            container.Add(tableContainer);
            
            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = HeaderBgColor;
            headerRow.style.minHeight = HEADER_HEIGHT;
            headerRow.style.borderBottomWidth = 2;
            headerRow.style.borderBottomColor = TableBorderColor;
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
            cell.style.borderRightColor = new Color(TableBorderColor.r, TableBorderColor.g, TableBorderColor.b, 0.5f);
            
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
                cell.RegisterCallback<MouseEnterEvent>(_ => cell.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.4f));
                cell.RegisterCallback<MouseLeaveEvent>(_ => cell.style.backgroundColor = Color.clear);
            }
            return cell;
        }
        
        private VisualElement CreateDataRow(ScriptableObject asset, List<ColumnDef> columns, bool alternate)
        {
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
            bool doubleClickVisualize = EditorPrefs.GetBool(PREFS_PREFIX + "DoubleClickVisualize", true);
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = ROW_HEIGHT;
            row.style.backgroundColor = alternate ? RowAltColor : Color.clear;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(TableBorderColor.r, TableBorderColor.g, TableBorderColor.b, 0.3f);
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = RowHoverColor);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = alternate ? RowAltColor : Color.clear);
            row.RegisterCallback<ClickEvent>(evt => 
            { 
                if (evt.clickCount == 2) 
                { 
                    if (doubleClickVisualize && typeInfo != null && typeInfo.CanVisualize)
                        OpenVisualizer(asset);
                    else
                    {
                        Selection.activeObject = asset; 
                        EditorGUIUtility.PingObject(asset); 
                    }
                } 
            });
            
            foreach (var col in columns)
                row.Add(CreateDataCell(col.GetValue(asset) ?? "-", col.Width, col.IsAssetReference));
            
            row.Add(CreateActionsCell(asset, typeInfo));
            return row;
        }
        
        // private void ConfigureDataCell(VisualElement cell, VisualElement row)
        
        private VisualElement CreateDataCell(string value, int width, bool isAssetRef)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.paddingLeft = CELL_PADDING_H;
            cell.style.paddingRight = CELL_PADDING_H;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = TableBorderColor.Fade(.2f);
            
            var label = new Label(value);
            label.style.fontSize = 11;
            label.style.color = isAssetRef ? Colors.AccentBlue : Colors.LabelText;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(label);
            return cell;
        }
        
        private VisualElement CreateActionsCell(ScriptableObject asset, AssetTypeInfo typeInfo)
        {
            var cell = new VisualElement();
            cell.style.width = ACTIONS_COLUMN_WIDTH;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.style.paddingLeft = 4;
            cell.style.paddingRight = 4;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = TableBorderColor.Fade(.2f);
            
            var selectBtn = new Button { text = "Select" };
            selectBtn.style.fontSize = 9;
            selectBtn.style.paddingLeft = 6;
            selectBtn.style.paddingRight = 6;
            selectBtn.style.paddingTop = 2;
            selectBtn.style.paddingBottom = 2;
            selectBtn.style.marginRight = 2;
            selectBtn.style.borderTopLeftRadius = 3;
            selectBtn.style.borderTopRightRadius = 3;
            selectBtn.style.borderBottomLeftRadius = 3;
            selectBtn.style.borderBottomRightRadius = 3;
            selectBtn.style.backgroundColor = Colors.ButtonBackground;
            selectBtn.clicked += () => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); };
            cell.Add(selectBtn);
            
            // Visualize button for supported types
            if (typeInfo != null && typeInfo.CanVisualize)
            {
                var vizBtn = new Button { text = "👁", tooltip = "Open Visualizer" };
                vizBtn.style.fontSize = 10;
                vizBtn.style.width = 22;
                vizBtn.style.paddingLeft = 0;
                vizBtn.style.paddingRight = 0;
                vizBtn.style.marginRight = 2;
                vizBtn.style.borderTopLeftRadius = 3;
                vizBtn.style.borderTopRightRadius = 3;
                vizBtn.style.borderBottomLeftRadius = 3;
                vizBtn.style.borderBottomRightRadius = 3;
                vizBtn.style.backgroundColor = new Color(typeInfo.Color.r * 0.4f, typeInfo.Color.g * 0.4f, typeInfo.Color.b * 0.4f, 0.6f);
                vizBtn.clicked += () => OpenVisualizer(asset);
                cell.Add(vizBtn);
            }
            
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
                if (typeInfo != null && typeInfo.CanVisualize)
                    menu.AddItem(new GUIContent("Open Visualizer"), false, () => OpenVisualizer(asset));
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
            bool showFileNames = EditorPrefs.GetBool(PREFS_PREFIX + "ShowFileNames", true);
            
            foreach (var group in assets.GroupBy(a => a.GetType()).OrderBy(g => g.Key.Name))
            {
                var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == group.Key);
                var header = CreateGroupHeader(typeInfo?.Icon ?? "?", typeInfo?.DisplayName ?? group.Key.Name, group.Count(), typeInfo?.Color ?? Colors.AccentGray);
                header.RegisterCallback<ClickEvent>(_ => { showTagsView = false; showScalersView = false; showRequirementsView = false; selectedTypeFilter = group.Key; ShowTab(1); });
                header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.9f));
                header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f));
                container.Add(header);
                
                foreach (var asset in group)
                    container.Add(CreateCompactAssetRow(asset, typeInfo, showFileNames));
            }
            
            if (!assets.Any())
                container.Add(CreateEmptyLabel("No assets found"));
        }
        
        private VisualElement CreateCompactAssetRow(ScriptableObject asset, AssetTypeInfo typeInfo, bool showFileNames)
        {
            bool doubleClickVisualize = EditorPrefs.GetBool(PREFS_PREFIX + "DoubleClickVisualize", true);
            
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
            row.RegisterCallback<ClickEvent>(evt => 
            { 
                if (evt.clickCount == 2 && doubleClickVisualize && typeInfo != null && typeInfo.CanVisualize)
                    OpenVisualizer(asset);
                else
                {
                    Selection.activeObject = asset; 
                    EditorGUIUtility.PingObject(asset); 
                }
            });
            
            row.Add(CreateLabel(GetAssetDisplayName(asset), 0, 11, Colors.LabelText, true));
            
            var displayName = GetAssetDisplayName(asset);
            if (showFileNames && displayName != asset.name)
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
        
        private Label CreateBadge(string text, int width, Color color)
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