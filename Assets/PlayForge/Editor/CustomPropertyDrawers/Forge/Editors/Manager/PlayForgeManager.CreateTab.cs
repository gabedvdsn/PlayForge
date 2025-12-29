using System;
using System.Collections.Generic;
using System.IO;
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
        // CREATE TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildCreateTab()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            contentContainer.Add(scrollView);
            
            var content = new VisualElement();
            scrollView.Add(content);
            
            // Quick Create Section
            var titleRow = CreateSectionHeader("Quick Create", "Create new PlayForge assets");
            content.Add(titleRow);
            
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 12;
            content.Add(grid);
            
            foreach (var typeInfo in AssetTypes.Where(t => t.CanCreate))
            {
                var card = CreateAssetTypeCard(typeInfo);
                grid.Add(card);
            }
            
            content.Add(CreateDivider(16));
            
            // Recently Created Section
            int takeAmount = EditorPrefs.GetInt(PREFS_PREFIX + "RecentlyCreatedRows", 10);
            if (takeAmount > 0)
            {
                var recentTitle = CreateSectionHeader("Recently Created", "Your latest assets");
                content.Add(recentTitle);
            
                var recentList = new VisualElement();
                recentList.style.marginTop = 8;
                content.Add(recentList);
            
                var recentAssets = cachedAssets
                    .OrderByDescending(a => File.GetLastWriteTime(AssetDatabase.GetAssetPath(a)))
                    .Take(takeAmount);
            
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
            
            content.Add(CreateDivider(16));
            
            // Project Statistics Section
            BuildStatisticsSection(content);
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
        // STATISTICS SECTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildStatisticsSection(VisualElement parent)
        {
            var statsTitle = CreateSectionHeader("Project Statistics", "Overview of your PlayForge assets");
            parent.Add(statsTitle);
            
            var statsContainer = new VisualElement();
            statsContainer.style.marginTop = 12;
            statsContainer.style.flexDirection = FlexDirection.Row;
            statsContainer.style.flexWrap = Wrap.Wrap;
            parent.Add(statsContainer);
            
            // Asset Distribution Bar Chart
            var assetDistChart = CreateBarChartPanel("Asset Distribution", GetAssetDistributionData());
            statsContainer.Add(assetDistChart);
            
            // Tag Usage Chart
            var tagUsageChart = CreateBarChartPanel("Top Tags", GetTopTagsData());
            statsContainer.Add(tagUsageChart);
            
            // Quick Stats Cards
            var quickStats = CreateQuickStatsPanel();
            statsContainer.Add(quickStats);
            
            // Complexity Metrics
            var complexityChart = CreateBarChartPanel("Complexity", GetComplexityData());
            statsContainer.Add(complexityChart);
        }
        
        private List<(string label, int value, Color color)> GetAssetDistributionData()
        {
            var data = new List<(string, int, Color)>();
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                data.Add((typeInfo.DisplayName, count, typeInfo.Color));
            }
            return data;
        }
        
        private List<(string label, int value, Color color)> GetTopTagsData()
        {
            var tagRecords = TagRegistry.GetAllTagRecords()
                .OrderByDescending(r => r.TotalUsageCount)
                .Take(5)
                .ToList();
            
            var data = new List<(string, int, Color)>();
            var colors = new[] { Colors.AccentCyan, Colors.AccentBlue, Colors.AccentPurple, Colors.AccentGreen, Colors.AccentOrange };
            
            for (int i = 0; i < tagRecords.Count; i++)
            {
                var record = tagRecords[i];
                var tagName = record.Tag.ToString();
                if (tagName.Length > 12) tagName = tagName.Substring(0, 10) + "..";
                data.Add((tagName, record.TotalUsageCount, colors[i % colors.Length]));
            }
            
            if (data.Count == 0)
                data.Add(("No tags", 0, Colors.HintText));
            
            return data;
        }
        
        private List<(string label, int value, Color color)> GetComplexityData()
        {
            var data = new List<(string, int, Color)>();
            
            // Average stages per ability
            var abilities = cachedAssets.OfType<Ability>().ToList();
            int avgStages = abilities.Count > 0 
                ? (int)abilities.Average(a => a.Proxy?.Stages?.Count ?? 0) 
                : 0;
            data.Add(("Avg Stages", avgStages, Colors.AccentOrange));
            
            // Average workers per effect
            var effects = cachedAssets.OfType<GameplayEffect>().ToList();
            int avgWorkers = effects.Count > 0 
                ? (int)effects.Average(e => e.Workers?.Count ?? 0) 
                : 0;
            data.Add(("Avg Workers", avgWorkers, Colors.AccentRed));
            
            // Average attributes per set
            var sets = cachedAssets.OfType<AttributeSet>().ToList();
            int avgAttrs = sets.Count > 0 
                ? (int)sets.Average(s => s.Attributes?.Count ?? 0) 
                : 0;
            data.Add(("Avg Attrs/Set", avgAttrs, Colors.AccentGreen));
            
            // Abilities with costs
            int withCosts = abilities.Count(a => a.Cost != null);
            data.Add(("With Cost", withCosts, Colors.AccentBlue));
            
            // Abilities with cooldowns
            int withCooldowns = abilities.Count(a => a.Cooldown != null);
            data.Add(("With CD", withCooldowns, Colors.AccentPurple));
            
            return data;
        }
        
        private VisualElement CreateBarChartPanel(string title, List<(string label, int value, Color color)> data)
        {
            var panel = new VisualElement();
            panel.style.width = 280;
            panel.style.minHeight = 160;
            panel.style.marginRight = 12;
            panel.style.marginBottom = 12;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 12;
            panel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.marginBottom = 10;
            panel.Add(titleLabel);
            
            int maxValue = data.Count > 0 ? data.Max(d => d.value) : 1;
            if (maxValue == 0) maxValue = 1;
            
            foreach (var (label, value, color) in data)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;
                panel.Add(row);
                
                var labelText = new Label(label);
                labelText.style.width = 80;
                labelText.style.fontSize = 10;
                labelText.style.color = Colors.LabelText;
                labelText.style.overflow = Overflow.Hidden;
                labelText.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(labelText);
                
                var barContainer = new VisualElement();
                barContainer.style.flexGrow = 1;
                barContainer.style.height = 16;
                barContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                barContainer.style.borderTopLeftRadius = 3;
                barContainer.style.borderTopRightRadius = 3;
                barContainer.style.borderBottomLeftRadius = 3;
                barContainer.style.borderBottomRightRadius = 3;
                barContainer.style.overflow = Overflow.Hidden;
                row.Add(barContainer);
                
                float percentage = (float)value / maxValue;
                var bar = new VisualElement();
                bar.style.width = Length.Percent(percentage * 100);
                bar.style.height = Length.Percent(100);
                bar.style.backgroundColor = color;
                bar.style.borderTopLeftRadius = 3;
                bar.style.borderBottomLeftRadius = 3;
                barContainer.Add(bar);
                
                var valueText = new Label(value.ToString());
                valueText.style.width = 35;
                valueText.style.fontSize = 10;
                valueText.style.color = Colors.HintText;
                valueText.style.unityTextAlign = TextAnchor.MiddleRight;
                valueText.style.marginLeft = 6;
                row.Add(valueText);
            }
            
            return panel;
        }
        
        private VisualElement CreateQuickStatsPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 280;
            panel.style.minHeight = 160;
            panel.style.marginRight = 12;
            panel.style.marginBottom = 12;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 12;
            panel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            
            var titleLabel = new Label("Quick Stats");
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.marginBottom = 10;
            panel.Add(titleLabel);
            
            var statsGrid = new VisualElement();
            statsGrid.style.flexDirection = FlexDirection.Row;
            statsGrid.style.flexWrap = Wrap.Wrap;
            panel.Add(statsGrid);
            
            // Total Assets
            AddQuickStatCard(statsGrid, "Total", cachedAssets.Count.ToString(), Colors.HeaderText);
            
            // Unique Tags
            AddQuickStatCard(statsGrid, "Tags", TagRegistry.GetAllTags().Count().ToString(), Colors.AccentCyan);
            
            // Contexts
            AddQuickStatCard(statsGrid, "Contexts", TagRegistry.GetAllContextKeys().Count().ToString(), Colors.AccentPurple);
            
            // Effects with Duration
            var effectsWithDuration = cachedAssets.OfType<GameplayEffect>()
                .Count(e => e.DurationSpecification != null);
            AddQuickStatCard(statsGrid, "Timed FX", effectsWithDuration.ToString(), Colors.AccentRed);
            
            // Multi-level Abilities
            var multiLevel = cachedAssets.OfType<Ability>()
                .Count(a => a.MaxLevel > 1);
            AddQuickStatCard(statsGrid, "Multi-Lvl", multiLevel.ToString(), Colors.AccentOrange);
            
            // Entities with starting abilities
            var entitiesWithAbilities = cachedAssets.OfType<EntityIdentity>()
                .Count(e => e.StartingAbilities?.Count > 0);
            AddQuickStatCard(statsGrid, "Equipped", entitiesWithAbilities.ToString(), Colors.AccentGreen);
            
            return panel;
        }
        
        private void AddQuickStatCard(VisualElement parent, string label, string value, Color color)
        {
            var card = new VisualElement();
            card.style.width = 75;
            card.style.height = 50;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 6;
            card.style.paddingRight = 6;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.borderLeftWidth = 2;
            card.style.borderLeftColor = color;
            parent.Add(card);
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 16;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = color;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(valueLabel);
            
            var labelText = new Label(label);
            labelText.style.fontSize = 9;
            labelText.style.color = Colors.HintText;
            labelText.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(labelText);
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
        private string filePrefix = "";
        private string fileSuffix = "";
        private Action onCreated;
        private Label previewLabel;
        
        public void Initialize(PlayForgeManager.AssetTypeInfo info, string path, Action callback)
        {
            typeInfo = info;
            assetPath = path;
            onCreated = callback;
            
            // Get prefix/suffix settings
            filePrefix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Prefix_" + info.Type.Name, "");
            fileSuffix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Postfix_" + info.Type.Name, "");
            
            titleContent = new GUIContent($"Create {typeInfo.DisplayName}");
            minSize = maxSize = new Vector2(350, 160);
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
            nameRow.style.marginBottom = 4;
            root.Add(nameRow);
            
            var nameLabel = new Label("Name");
            nameLabel.style.width = 50;
            nameRow.Add(nameLabel);
            
            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.RegisterValueChangedCallback(evt => 
            {
                assetName = evt.newValue;
                UpdateFilePreview();
            });
            nameRow.Add(nameField);
            
            // File name preview
            previewLabel = new Label();
            previewLabel.style.fontSize = 10;
            previewLabel.style.color = Colors.HintText;
            previewLabel.style.marginBottom = 4;
            previewLabel.style.marginLeft = 50;
            root.Add(previewLabel);
            
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
            UpdateFilePreview();
        }
        
        private void UpdateFilePreview()
        {
            if (previewLabel == null) return;
            
            if (string.IsNullOrEmpty(assetName))
            {
                previewLabel.text = "File: (enter name)";
            }
            else
            {
                var fileName = GetFileName(assetName);
                previewLabel.text = $"File: {fileName}.asset";
            }
        }
        
        private string GetFileName(string name)
        {
            return $"{filePrefix}{name}{fileSuffix}";
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
            
            // Set the internal name field on the asset (uses display name, not file name)
            SetAssetName(asset, assetName);
            
            // File name includes prefix/suffix
            var fileName = GetFileName(assetName);
            var fullPath = $"{assetPath}/{fileName}.asset";
            
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            onCreated?.Invoke();
            Close();
        }
        
        /// <summary>
        /// Sets the internal name field on the asset based on its type.
        /// Handles Definition.Name pattern (Ability, Effect) and direct Name fields.
        /// </summary>
        private void SetAssetName(ScriptableObject asset, string name)
        {
            if (asset == null || string.IsNullOrEmpty(name)) return;
            
            var assetType = asset.GetType();
            var flags = System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic;
            
            // Try Definition.Name pattern (used by Ability, GameplayEffect, etc.)
            var defField = assetType.GetField("Definition", flags);
            if (defField != null)
            {
                var def = defField.GetValue(asset);
                if (def != null)
                {
                    var nameField = def.GetType().GetField("Name", flags);
                    if (nameField != null && nameField.FieldType == typeof(string))
                    {
                        nameField.SetValue(def, name);
                        return;
                    }
                }
            }
            
            // Try direct Name field (used by some asset types)
            var directNameField = assetType.GetField("Name", flags);
            if (directNameField != null && directNameField.FieldType == typeof(string))
            {
                directNameField.SetValue(asset, name);
                return;
            }
            
            // Try DisplayName field as fallback
            var displayNameField = assetType.GetField("DisplayName", flags);
            if (displayNameField != null && displayNameField.FieldType == typeof(string))
            {
                displayNameField.SetValue(asset, name);
            }
        }
    }
}