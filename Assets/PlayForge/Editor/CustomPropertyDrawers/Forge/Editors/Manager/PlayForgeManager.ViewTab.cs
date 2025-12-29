using System;
using System.Collections.Generic;
using System.Linq;
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
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string currentSortColumn = "Name";
        private bool sortAscending = true;
        private List<Button> typeFilterButtons = new List<Button>();
        
        // Track which tags are expanded
        private HashSet<string> expandedTags = new HashSet<string>();
        
        private static readonly Color ColumnBorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildViewTab()
        {
            // Type selector chips
            var typeBar = new VisualElement();
            typeBar.style.flexDirection = FlexDirection.Row;
            typeBar.style.flexWrap = Wrap.Wrap;
            typeBar.style.marginBottom = 8;
            contentContainer.Add(typeBar);
            
            typeFilterButtons.Clear();
            
            var allBtn = CreateTypeFilterButton("All", null, null, false);
            typeBar.Add(allBtn);
            typeFilterButtons.Add(allBtn);
            
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var btn = CreateTypeFilterButton($"{typeInfo.Icon} {typeInfo.DisplayName}", typeInfo.Type, typeInfo.Color, false, count);
                typeBar.Add(btn);
                typeFilterButtons.Add(btn);
            }
            
            var tagCount = TagRegistry.GetAllTags().Count();
            var tagsBtn = CreateTypeFilterButton("🏷 Tags", null, Colors.AccentCyan, true, tagCount);
            typeBar.Add(tagsBtn);
            typeFilterButtons.Add(tagsBtn);
            
            UpdateTypeFilterButtons();
            
            // Context filter bar (only shown in Tags view)
            if (showTagsView)
            {
                BuildTagContextFilterBar();
            }
            
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
            
            if (showTagsView && TagRegistry.IsCacheValid)
            {
                var cacheInfo = new Label($"Last scan: {TagRegistry.LastScanTime:HH:mm:ss}");
                cacheInfo.style.fontSize = 9;
                cacheInfo.style.color = Colors.HintText;
                cacheInfo.style.marginRight = 8;
                searchBar.Add(cacheInfo);
            }
            
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
        
        private void BuildTagContextFilterBar()
        {
            var contextBar = new VisualElement();
            contextBar.style.flexDirection = FlexDirection.Row;
            contextBar.style.flexWrap = Wrap.Wrap;
            contextBar.style.marginBottom = 8;
            contextBar.style.paddingLeft = 8;
            contextBar.style.paddingTop = 4;
            contextBar.style.paddingBottom = 4;
            contextBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            contextBar.style.borderTopLeftRadius = 4;
            contextBar.style.borderTopRightRadius = 4;
            contextBar.style.borderBottomLeftRadius = 4;
            contextBar.style.borderBottomRightRadius = 4;
            contentContainer.Add(contextBar);
            
            var contextLabel = new Label("Context Filter:");
            contextLabel.style.fontSize = 10;
            contextLabel.style.color = Colors.HintText;
            contextLabel.style.marginRight = 8;
            contextLabel.style.alignSelf = Align.Center;
            contextBar.Add(contextLabel);
            
            var allContextBtn = CreateContextFilterChip("All", null);
            contextBar.Add(allContextBtn);
            
            var contexts = TagRegistry.GetAllContextKeys().OrderBy(c => c).ToList();
            foreach (var contextKey in contexts)
            {
                var chip = CreateContextFilterChip(GetContextFriendlyName(contextKey), contextKey);
                contextBar.Add(chip);
            }
        }
        
        private VisualElement CreateContextFilterChip(string text, string contextKey)
        {
            var isSelected = selectedTagContextFilter == contextKey;
            
            var chip = new Button(() =>
            {
                selectedTagContextFilter = contextKey;
                RefreshViewList();
                ShowTab(currentTab);
            });
            chip.text = text;
            chip.focusable = false;
            chip.style.fontSize = 9;
            chip.style.paddingLeft = 6;
            chip.style.paddingRight = 6;
            chip.style.paddingTop = 2;
            chip.style.paddingBottom = 2;
            chip.style.marginRight = 4;
            chip.style.marginBottom = 2;
            chip.style.borderTopLeftRadius = 8;
            chip.style.borderTopRightRadius = 8;
            chip.style.borderBottomLeftRadius = 8;
            chip.style.borderBottomRightRadius = 8;
            chip.style.backgroundColor = isSelected 
                ? Colors.AccentCyan 
                : new Color(0.25f, 0.25f, 0.25f, 0.8f);
            chip.style.color = isSelected ? Colors.HeaderBackground : Colors.LabelText;
            
            return chip;
        }
        
        private string GetContextFriendlyName(string contextKey)
        {
            if (string.IsNullOrEmpty(contextKey)) return "Uncontextualized";
            
            var parts = contextKey.Split('+');
            var friendlyParts = parts.Select(p =>
            {
                if (p.StartsWith("FT_")) p = p.Substring(3);
                return p;
            });
            return string.Join(" / ", friendlyParts);
        }
        
        private Button CreateTypeFilterButton(string text, Type type, Color? color, bool isTagsView, int count = -1)
        {
            var displayText = count >= 0 ? $"{text} ({count})" : text;
            var btn = CreateButton(displayText, () =>
            {
                showTagsView = isTagsView;
                selectedTypeFilter = isTagsView ? null : type;
                selectedTagContextFilter = null;
                currentSortColumn = isTagsView ? "Tag" : "Name";
                sortAscending = true;
                
                EditorPrefs.SetString(PREFS_PREFIX + "LastTypeFilter", isTagsView ? TAGS_VIEW_MARKER : (type?.Name ?? ""));
                
                UpdateTypeFilterButtons();
                ShowTab(currentTab);
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
            
            if (showTagsView)
            {
                var tagRecords = TagRegistry.GetAllTagRecords().ToList();
                
                if (!string.IsNullOrEmpty(selectedTagContextFilter))
                {
                    tagRecords = tagRecords
                        .Where(r => r.UsageByContext.ContainsKey(selectedTagContextFilter))
                        .ToList();
                }
                
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    tagRecords = tagRecords
                        .Where(r => r.Tag.ToString().IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }
                
                if (resultsLabel != null)
                    resultsLabel.text = $"{tagRecords.Count} tag(s)";
                
                if (sortLabel != null)
                    sortLabel.text = $"Sort: {currentSortColumn} {(sortAscending ? "↑" : "↓")}";
                
                BuildTagsSpreadsheetView(listContainer, tagRecords);
                return;
            }
            
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
        
        private void BuildTagsSpreadsheetView(VisualElement container, List<TagRegistry.TagUsageRecord> tagRecords)
        {
            tagRecords = SortTagRecords(tagRecords);
            
            int totalWidth = 220 + 60 + 300 + 200;
            
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = Colors.BorderLight;
            headerRow.style.borderBottomWidth = 2;
            headerRow.style.borderBottomColor = Colors.BorderDark;
            headerRow.style.minWidth = totalWidth;
            headerRow.style.height = 28;
            container.Add(headerRow);
            
            headerRow.Add(CreateTagColumnHeader("Tag", 220 + COLOR_INDICATOR_WIDTH, true));
            headerRow.Add(CreateTagColumnHeader("Count", 60, false));
            headerRow.Add(CreateTagColumnHeader("Contexts", 300, false));
            headerRow.Add(CreateStaticHeaderCell("Assets", 200));
            
            for (int i = 0; i < tagRecords.Count; i++)
            {
                var record = tagRecords[i];
                var rowContainer = CreateTagSpreadsheetRowContainer(record, i % 2 == 1, totalWidth);
                container.Add(rowContainer);
            }
            
            if (tagRecords.Count == 0)
            {
                var emptyLabel = new Label("No tags found. Tags are discovered from assets with [ForgeTagContext] attributes.");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
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
        
        private VisualElement CreateTagSpreadsheetRowContainer(TagRegistry.TagUsageRecord record, bool alternate, int totalWidth)
        {
            var tagKey = record.Tag.ToString();
            var isExpanded = expandedTags.Contains(tagKey);
            
            var container = new VisualElement();
            container.name = $"TagRow_{tagKey}";
            
            // Summary row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.minHeight = 32;
            row.style.minWidth = totalWidth;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            //row.style.cursor = new Cursor { texture = null }; // Hand cursor would be nice but UIToolkit doesn't have it easily
            
            if (alternate)
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.3f);
            
            if (isExpanded)
                row.style.backgroundColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.15f);
            
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!isExpanded)
                    row.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 0.6f);
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (isExpanded)
                    row.style.backgroundColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.15f);
                else
                    row.style.backgroundColor = alternate ? new Color(0.18f, 0.18f, 0.18f, 0.3f) : Color.clear;
            });
            
            // Tag name cell
            var tagCell = new VisualElement();
            tagCell.style.width = 220 + COLOR_INDICATOR_WIDTH;
            tagCell.style.paddingLeft = 8;
            tagCell.style.paddingRight = 4;
            tagCell.style.borderRightWidth = 1;
            tagCell.style.borderRightColor = ColumnBorderColor;
            tagCell.style.borderLeftWidth = COLOR_INDICATOR_WIDTH;
            tagCell.style.borderLeftColor = Colors.AccentCyan;
            tagCell.style.justifyContent = Justify.Center;
            tagCell.style.flexDirection = FlexDirection.Row;
            tagCell.style.alignItems = Align.Center;
            
            // Expand/collapse indicator
            var expandIndicator = new Label(isExpanded ? "▼" : "▶");
            expandIndicator.style.fontSize = 9;
            expandIndicator.style.color = Colors.HintText;
            expandIndicator.style.marginRight = 6;
            expandIndicator.style.width = 12;
            tagCell.Add(expandIndicator);
            
            // Tag name or placeholder
            var tagName = record.Tag.ToString();
            var isEmptyTag = string.IsNullOrWhiteSpace(tagName);
            
            var tagLabel = new Label(isEmptyTag ? "(Unnamed Tag)" : tagName);
            tagLabel.style.fontSize = 11;
            tagLabel.style.color = isEmptyTag ? Colors.HintText : Colors.HeaderText;
            tagLabel.style.unityFontStyleAndWeight = isEmptyTag ? FontStyle.Italic : FontStyle.Bold;
            tagLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            tagLabel.style.overflow = Overflow.Hidden;
            tagLabel.style.textOverflow = TextOverflow.Ellipsis;
            tagLabel.style.flexGrow = 1;
            tagLabel.tooltip = isEmptyTag ? "This tag has no name" : tagName;
            tagCell.Add(tagLabel);
            
            row.Add(tagCell);
            
            // Get filtered contexts based on context filter - used for count and display
            var filteredContexts = GetFilteredContexts(record);
            var filteredUsageCount = filteredContexts.Sum(c => c.Assets.Count);
            
            // Count cell - show filtered count when filter is active
            var countCell = new VisualElement();
            countCell.style.width = 60;
            countCell.style.paddingLeft = 8;
            countCell.style.paddingRight = 4;
            countCell.style.borderRightWidth = 1;
            countCell.style.borderRightColor = ColumnBorderColor;
            countCell.style.justifyContent = Justify.Center;
            
            var countLabel = new Label(filteredUsageCount.ToString());
            countLabel.style.fontSize = 11;
            countLabel.style.color = Colors.LabelText;
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            countCell.Add(countLabel);
            row.Add(countCell);
            
            // Contexts cell - filter by selectedTagContextFilter if set
            var contextsCell = new VisualElement();
            contextsCell.style.width = 300;
            contextsCell.style.paddingLeft = 8;
            contextsCell.style.paddingRight = 4;
            contextsCell.style.paddingTop = 4;
            contextsCell.style.paddingBottom = 4;
            contextsCell.style.borderRightWidth = 1;
            contextsCell.style.borderRightColor = ColumnBorderColor;
            contextsCell.style.flexDirection = FlexDirection.Row;
            contextsCell.style.flexWrap = Wrap.Wrap;
            contextsCell.style.alignItems = Align.Center;
            contextsCell.style.alignContent = Align.FlexStart;
            
            // Use filteredContexts from above
            var contextsList = filteredContexts
                .OrderByDescending(c => c.Assets.Count)
                .Take(3)
                .ToList();
            
            foreach (var contextUsage in contextsList)
            {
                var contextChip = CreateContextChip(contextUsage);
                contextsCell.Add(contextChip);
            }
            
            if (filteredContexts.Count > 3)
            {
                var moreLabel = new Label($"+{filteredContexts.Count - 3}");
                moreLabel.style.fontSize = 9;
                moreLabel.style.color = Colors.HintText;
                moreLabel.style.marginLeft = 4;
                moreLabel.style.alignSelf = Align.Center;
                contextsCell.Add(moreLabel);
            }
            
            row.Add(contextsCell);
            
            // Assets cell - use filtered contexts
            var assetsCell = new VisualElement();
            assetsCell.style.width = 200;
            assetsCell.style.paddingLeft = 8;
            assetsCell.style.paddingRight = 4;
            assetsCell.style.borderRightWidth = 1;
            assetsCell.style.borderRightColor = ColumnBorderColor;
            assetsCell.style.flexDirection = FlexDirection.Row;
            assetsCell.style.alignItems = Align.Center;
            assetsCell.style.flexWrap = Wrap.NoWrap;
            assetsCell.style.overflow = Overflow.Hidden;
            
            var filteredAssets = filteredContexts
                .SelectMany(c => c.Assets)
                .Distinct()
                .Take(2)
                .ToList();
            
            foreach (var asset in filteredAssets)
            {
                var chip = CreateAssetChip(asset);
                assetsCell.Add(chip);
            }
            
            var totalFilteredAssets = filteredContexts.SelectMany(c => c.Assets).Distinct().Count();
            if (totalFilteredAssets > 2)
            {
                var moreLabel = new Label($"+{totalFilteredAssets - 2}");
                moreLabel.style.fontSize = 9;
                moreLabel.style.color = Colors.HintText;
                moreLabel.style.marginLeft = 4;
                assetsCell.Add(moreLabel);
            }
            
            row.Add(assetsCell);
            container.Add(row);
            
            // Click handler to toggle expansion
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (expandedTags.Contains(tagKey))
                    expandedTags.Remove(tagKey);
                else
                    expandedTags.Add(tagKey);
                
                RefreshViewList();
                evt.StopPropagation();
            });
            
            // Expanded details section
            if (isExpanded)
            {
                var details = CreateTagExpandedDetails(record, totalWidth);
                container.Add(details);
            }
            
            return container;
        }
        
        private VisualElement CreateTagExpandedDetails(TagRegistry.TagUsageRecord record, int totalWidth)
        {
            var details = new VisualElement();
            details.style.minWidth = totalWidth;
            details.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            details.style.paddingLeft = 20 + COLOR_INDICATOR_WIDTH;
            details.style.paddingRight = 12;
            details.style.paddingTop = 12;
            details.style.paddingBottom = 12;
            details.style.borderBottomWidth = 2;
            details.style.borderBottomColor = Colors.AccentCyan;
            details.style.borderLeftWidth = COLOR_INDICATOR_WIDTH;
            details.style.borderLeftColor = Colors.AccentCyan;
            
            // Get filtered contexts
            var filteredContexts = GetFilteredContexts(record);
            var isFiltered = !string.IsNullOrEmpty(selectedTagContextFilter);
            
            // Section header - indicate if filtered
            var headerText = isFiltered 
                ? $"Filtered Contexts & Assets ({GetContextFriendlyName(selectedTagContextFilter)})"
                : "All Contexts & Assets";
            var headerLabel = new Label(headerText);
            headerLabel.style.fontSize = 10;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.color = Colors.AccentCyan;
            headerLabel.style.marginBottom = 10;
            details.Add(headerLabel);
            
            // Group by context - use filtered contexts
            var sortedContexts = filteredContexts
                .OrderByDescending(c => c.Assets.Count)
                .ToList();
            
            foreach (var contextUsage in sortedContexts)
            {
                var contextSection = new VisualElement();
                contextSection.style.marginBottom = 12;
                details.Add(contextSection);
                
                /*// Context header with field paths summary
                var contextHeader = new VisualElement();
                contextHeader.style.flexDirection = FlexDirection.Row;
                contextHeader.style.alignItems = Align.Center;
                contextHeader.style.marginBottom = 4;
                contextSection.Add(contextHeader);
                
                var contextChip = CreateContextChip(contextUsage);
                contextChip.style.marginRight = 8;
                contextHeader.Add(contextChip);
                
                var assetCountLabel = new Label($"{contextUsage.Assets.Count} asset(s)");
                assetCountLabel.style.fontSize = 9;
                assetCountLabel.style.color = Colors.HintText;
                contextHeader.Add(assetCountLabel);*/
                
                // Show unique field paths for this context
                var uniqueFields = contextUsage.GetUniqueFieldPaths().ToList();
                if (uniqueFields.Count > 0)
                {
                    var fieldPathsRow = new VisualElement();
                    fieldPathsRow.style.flexDirection = FlexDirection.Row;
                    fieldPathsRow.style.flexWrap = Wrap.Wrap;
                    fieldPathsRow.style.marginLeft = 8;
                    fieldPathsRow.style.marginBottom = 6;
                    contextSection.Add(fieldPathsRow);
                    
                    /*var fieldIcon = new Label("📍");
                    fieldIcon.style.fontSize = 9;
                    fieldIcon.style.marginRight = 4;
                    fieldIcon.style.color = Colors.HintText;
                    fieldPathsRow.Add(fieldIcon);*/
                    
                    var fieldLabel = new Label("Fields: ");
                    fieldLabel.style.fontSize = 9;
                    fieldLabel.style.color = Colors.HintText;
                    fieldPathsRow.Add(fieldLabel);
                    
                    foreach (var fieldPath in uniqueFields.Take(3))
                    {
                        var friendlyName = new TagRegistry.FieldUsageInfo(null, fieldPath).FriendlyFieldName;
                        var fieldChip = CreateFieldPathChip(friendlyName, fieldPath);
                        fieldPathsRow.Add(fieldChip);
                    }
                    
                    if (uniqueFields.Count > 3)
                    {
                        var moreFields = new Label($"+{uniqueFields.Count - 3} more");
                        moreFields.style.fontSize = 8;
                        moreFields.style.color = Colors.HintText;
                        moreFields.style.marginLeft = 4;
                        moreFields.style.alignSelf = Align.Center;
                        fieldPathsRow.Add(moreFields);
                    }
                }
                
                // Assets with their field info
                var assetsContainer = new VisualElement();
                assetsContainer.style.marginLeft = 8;
                contextSection.Add(assetsContainer);
                
                // Group usages by asset to show field paths per asset
                var usagesByAsset = contextUsage.GetUsagesByAsset();
                foreach (var kvp in usagesByAsset.OrderBy(x => GetAssetDisplayName(x.Key)))
                {
                    var assetRow = CreateDetailedAssetRowWithFields(kvp.Key, kvp.Value);
                    assetsContainer.Add(assetRow);
                }
            }
            
            // Summary - use filtered counts
            var totalFilteredAssets = filteredContexts.SelectMany(c => c.Assets).Distinct().Count();
            var totalFilteredUsages = filteredContexts.Sum(c => c.Usages.Count);
            var summaryText = isFiltered
                ? $"Filtered: {filteredContexts.Count} context(s), {totalFilteredAssets} unique asset(s), {totalFilteredUsages} usage(s)"
                : $"Total: {filteredContexts.Count} context(s), {totalFilteredAssets} unique asset(s), {totalFilteredUsages} usage(s)";
            var summaryLabel = new Label(summaryText);
            summaryLabel.style.fontSize = 9;
            summaryLabel.style.color = Colors.HintText;
            summaryLabel.style.marginTop = 8;
            summaryLabel.style.borderTopWidth = 1;
            summaryLabel.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            summaryLabel.style.paddingTop = 8;
            details.Add(summaryLabel);
            
            return details;
        }
        
        private VisualElement CreateFieldPathChip(string friendlyName, string rawPath)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.marginRight = 4;
            chip.style.backgroundColor = new Color(0.3f, 0.3f, 0.4f, 0.5f);
            chip.style.borderTopLeftRadius = 4;
            chip.style.borderTopRightRadius = 4;
            chip.style.borderBottomLeftRadius = 4;
            chip.style.borderBottomRightRadius = 4;
            
            var label = new Label(friendlyName);
            label.style.fontSize = 8;
            label.style.color = new Color(0.7f, 0.8f, 1f);
            chip.Add(label);
            
            chip.tooltip = $"Field Path: {rawPath}";
            
            return chip;
        }
        
        private VisualElement CreateDetailedAssetRowWithFields(ScriptableObject asset, List<TagRegistry.FieldUsageInfo> usages)
        {
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
            
            var container = new VisualElement();
            container.style.marginBottom = 4;
            
            // Main row with asset info
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            row.style.borderLeftWidth = 2;
            row.style.borderLeftColor = typeInfo?.Color ?? Colors.HintText;
            container.Add(row);
            
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 10;
            icon.style.color = typeInfo?.Color ?? Colors.HintText;
            icon.style.marginRight = 4;
            row.Add(icon);
            
            var nameLabel = new Label(GetAssetDisplayName(asset));
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = Colors.LabelText;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nameLabel);
            
            var fileLabel = new Label($"({asset.name})");
            fileLabel.style.fontSize = 8;
            fileLabel.style.color = Colors.HintText;
            fileLabel.style.marginLeft = 4;
            row.Add(fileLabel);
            
            // Show field paths for this asset (if multiple or if there are any)
            var fieldPaths = usages.Where(u => !string.IsNullOrEmpty(u.FieldPath)).ToList();
            if (fieldPaths.Count > 0)
            {
                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                row.Add(spacer);
                
                // Show compact field info in the row
                var fieldInfo = new Label($"in {fieldPaths.Count} field(s)");
                fieldInfo.style.fontSize = 8;
                fieldInfo.style.color = new Color(0.6f, 0.7f, 0.9f);
                fieldInfo.style.marginLeft = 8;
                row.Add(fieldInfo);
                
                // Build tooltip with all field paths
                var tooltipLines = new List<string>
                {
                    $"{typeInfo?.DisplayName}: {GetAssetDisplayName(asset)}",
                    "",
                    "Used in fields:"
                };
                foreach (var usage in fieldPaths.Take(10))
                {
                    tooltipLines.Add($"  • {usage.FriendlyFieldName}");
                }
                if (fieldPaths.Count > 10)
                    tooltipLines.Add($"  ... and {fieldPaths.Count - 10} more");
                
                row.tooltip = string.Join("\n", tooltipLines);
            }
            else
            {
                row.tooltip = $"{typeInfo?.DisplayName}: {GetAssetDisplayName(asset)}\nClick to select";
            }
            
            row.RegisterCallback<ClickEvent>(evt =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                evt.StopPropagation();
            });
            
            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f));
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            return container;
        }
        
        private VisualElement CreateContextChip(TagRegistry.ContextUsage contextUsage)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.paddingLeft = 4;
            chip.style.paddingRight = 4;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.marginRight = 4;
            chip.style.marginBottom = 2;
            chip.style.backgroundColor = GetContextColor(contextUsage.Context.ContextStrings);
            chip.style.borderTopLeftRadius = 6;
            chip.style.borderTopRightRadius = 6;
            chip.style.borderBottomLeftRadius = 6;
            chip.style.borderBottomRightRadius = 6;
            
            var label = new Label($"{contextUsage.Context.FriendlyName} ({contextUsage.Assets.Count})");
            label.style.fontSize = 8;
            label.style.color = Colors.HeaderText;
            chip.Add(label);
            
            chip.tooltip = $"Context: {contextUsage.Context.ContextKey}\nAssets: {contextUsage.Assets.Count}";
            
            chip.RegisterCallback<ClickEvent>(evt =>
            {
                selectedTagContextFilter = contextUsage.Context.ContextKey;
                ShowTab(currentTab);
                evt.StopPropagation();
            });
            
            chip.RegisterCallback<MouseEnterEvent>(_ =>
                chip.style.backgroundColor = GetContextColor(contextUsage.Context.ContextStrings, 0.3f));
            chip.RegisterCallback<MouseLeaveEvent>(_ =>
                chip.style.backgroundColor = GetContextColor(contextUsage.Context.ContextStrings));
            
            return chip;
        }
        
        private Color GetContextColor(string[] contextStrings, float alphaBoost = 0f)
        {
            if (contextStrings == null || contextStrings.Length == 0)
                return new Color(0.4f, 0.4f, 0.4f, 0.6f + alphaBoost);
            
            var first = contextStrings[0];
            
            if (first.Contains("Ability")) return new Color(1f, 0.6f, 0.2f, 0.4f + alphaBoost);
            if (first.Contains("Effect")) return new Color(1f, 0.3f, 0.3f, 0.4f + alphaBoost);
            if (first.Contains("Entity")) return new Color(0.7f, 0.4f, 1f, 0.4f + alphaBoost);
            if (first.Contains("Attribute")) return new Color(0.3f, 0.6f, 1f, 0.4f + alphaBoost);
            if (first.Contains("Visibility")) return new Color(0.3f, 0.8f, 0.8f, 0.4f + alphaBoost);
            if (first.Contains("Granted")) return new Color(0.4f, 0.9f, 0.4f, 0.4f + alphaBoost);
            if (first.Contains("Required")) return new Color(1f, 0.8f, 0.2f, 0.4f + alphaBoost);
            if (first.Contains("Blocked")) return new Color(1f, 0.4f, 0.4f, 0.4f + alphaBoost);
            if (first.Contains("Affiliation")) return new Color(0.9f, 0.5f, 0.7f, 0.4f + alphaBoost);
            
            return new Color(0.5f, 0.5f, 0.5f, 0.4f + alphaBoost);
        }
        
        private List<TagRegistry.TagUsageRecord> SortTagRecords(List<TagRegistry.TagUsageRecord> records)
        {
            return currentSortColumn switch
            {
                "Tag" => sortAscending 
                    ? records.OrderBy(r => r.Tag.ToString()).ToList()
                    : records.OrderByDescending(r => r.Tag.ToString()).ToList(),
                "Count" => sortAscending
                    ? records.OrderBy(r => r.TotalUsageCount).ToList()
                    : records.OrderByDescending(r => r.TotalUsageCount).ToList(),
                "Contexts" => sortAscending
                    ? records.OrderBy(r => r.UsageByContext.Count).ToList()
                    : records.OrderByDescending(r => r.UsageByContext.Count).ToList(),
                _ => records
            };
        }
        
        /// <summary>
        /// Gets the contexts for a tag record, filtered by selectedTagContextFilter if set.
        /// </summary>
        private List<TagRegistry.ContextUsage> GetFilteredContexts(TagRegistry.TagUsageRecord record)
        {
            if (string.IsNullOrEmpty(selectedTagContextFilter))
            {
                // No filter - return all contexts
                return record.UsageByContext.Values.ToList();
            }
            
            // Filter to only contexts that match the selected filter
            return record.UsageByContext
                .Where(kvp => kvp.Key == selectedTagContextFilter)
                .Select(kvp => kvp.Value)
                .ToList();
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
            nameLabel.style.maxWidth = 60;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            chip.Add(nameLabel);
            
            chip.tooltip = $"{typeInfo?.DisplayName}: {GetAssetDisplayName(asset)}";
            
            chip.RegisterCallback<ClickEvent>(evt =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                evt.StopPropagation();
            });
            
            chip.RegisterCallback<MouseEnterEvent>(_ =>
                chip.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f));
            chip.RegisterCallback<MouseLeaveEvent>(_ =>
                chip.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f));
            
            return chip;
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
            
            int totalWidth = columns.Sum(c => c.Width) + 104 + COLOR_INDICATOR_WIDTH;
            
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
            
            var actionsHeader = CreateStaticHeaderCell("Actions", 104);
            headerRow.Add(actionsHeader);
            
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
            
            var actionsCell = new VisualElement();
            actionsCell.style.width = 104;
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
            selectBtn.style.width = 42;
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