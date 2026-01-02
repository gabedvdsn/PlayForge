using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            
            // Total Assets Card
            var totalCard = CreateStatCard("Total Assets", cachedAssets.Count.ToString(), Colors.AccentBlue);
            statsContainer.Add(totalCard);
            
            // Asset counts by type
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var card = CreateStatCard(typeInfo.DisplayName, count.ToString(), typeInfo.Color);
                card.tooltip = $"{count} {typeInfo.DisplayName}(s) in project";
                statsContainer.Add(card);
            }
            
            // Tag count
            var tagCount = TagRegistry.GetAllTags().Count();
            var tagCard = CreateStatCard("Unique Tags", tagCount.ToString(), Colors.AccentCyan);
            tagCard.tooltip = $"{tagCount} unique tag(s) used across assets";
            statsContainer.Add(tagCard);
            
            // Context count  
            var contextCount = TagRegistry.GetAllContextKeys().Count();
            var contextCard = CreateStatCard("Tag Contexts", contextCount.ToString(), Colors.AccentPurple);
            contextCard.tooltip = $"{contextCount} tag context(s) detected";
            statsContainer.Add(contextCard);
        }
        
        private VisualElement CreateStatCard(string label, string value, Color color)
        {
            var card = new VisualElement();
            card.style.width = 90;
            card.style.height = 60;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingTop = 8;
            card.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth = 2;
            card.style.borderTopColor = color;
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 18;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = color;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(valueLabel);
            
            var labelText = new Label(label);
            labelText.style.fontSize = 9;
            labelText.style.color = Colors.HintText;
            labelText.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(labelText);
            
            return card;
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
        private Label templateLabel;
        
        // Characters not allowed in asset file names
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { ' ', '\t', '\n', '\r' })
            .ToArray();
        
        public void Initialize(PlayForgeManager.AssetTypeInfo info, string path, Action callback)
        {
            typeInfo = info;
            assetPath = path;
            onCreated = callback;
            
            // Get prefix/suffix settings
            filePrefix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Prefix_" + info.Type.Name, "");
            fileSuffix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Postfix_" + info.Type.Name, "");
            
            titleContent = new GUIContent($"Create {typeInfo.DisplayName}");
            minSize = maxSize = new Vector2(380, 180);
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
            nameLabel.style.width = 60;
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
            previewLabel.style.marginLeft = 60;
            root.Add(previewLabel);
            
            var pathLabel = new Label($"Path: {assetPath}/");
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = Colors.HintText;
            pathLabel.style.marginBottom = 8;
            root.Add(pathLabel);
            
            // Template info
            templateLabel = new Label();
            templateLabel.style.fontSize = 10;
            templateLabel.style.marginBottom = 12;
            root.Add(templateLabel);
            UpdateTemplateLabel();
            
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
        
        private void UpdateTemplateLabel()
        {
            if (templateLabel == null) return;
            
            var defaultTemplate = GetDefaultTemplate();
            if (defaultTemplate != null)
            {
                var templateAsset = defaultTemplate.GetAsset();
                var templateName = templateAsset != null ? PlayForgeManager.GetAssetDisplayName(templateAsset) : "(Missing)";
                templateLabel.text = $"Template: {templateName}";
                templateLabel.style.color = Colors.AccentCyan;
            }
            else
            {
                templateLabel.text = "Template: None (creating blank asset)";
                templateLabel.style.color = Colors.HintText;
            }
        }
        
        private AssetTemplate GetDefaultTemplate()
        {
            var templates = TemplateRegistry.GetTemplates(typeInfo.Type);
            return templates?.FirstOrDefault(t => t.IsDefault && t.IsValid());
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
                var fileName = GetSanitizedFileName(assetName);
                previewLabel.text = $"File: {fileName}.asset";
            }
        }
        
        /// <summary>
        /// Sanitizes the input name for use as a file name.
        /// Removes whitespace and invalid characters, applies prefix/suffix.
        /// </summary>
        private string GetSanitizedFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            
            // Remove all whitespace
            var sanitized = Regex.Replace(name, @"\s+", "");
            
            // Remove invalid file name characters
            foreach (var c in InvalidFileNameChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }
            
            // Remove any remaining problematic characters
            sanitized = Regex.Replace(sanitized, @"[<>:""/\\|?*]", "");
            
            // Apply prefix and suffix
            return $"{filePrefix}{sanitized}{fileSuffix}";
        }
        
        private void CreateAsset()
        {
            if (string.IsNullOrEmpty(assetName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name for the asset.", "OK");
                return;
            }
            
            var sanitizedFileName = GetSanitizedFileName(assetName);
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                EditorUtility.DisplayDialog("Error", "The name contains only invalid characters. Please enter a valid name.", "OK");
                return;
            }
            
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                Directory.CreateDirectory(assetPath);
                AssetDatabase.Refresh();
            }
            
            // Create the asset instance
            var asset = ScriptableObject.CreateInstance(typeInfo.Type);
            
            // Apply default template if available
            var defaultTemplate = GetDefaultTemplate();
            if (defaultTemplate != null)
            {
                var templateAsset = defaultTemplate.GetAsset();
                if (templateAsset != null)
                {
                    // Copy serialized data from template to new asset
                    EditorUtility.CopySerialized(templateAsset, asset);
                }
            }
            
            // Set the display name on the asset (after template copy so it's not overwritten)
            SetAssetName(asset, assetName);
            
            // Generate unique file path
            var fullPath = $"{assetPath}/{sanitizedFileName}.asset";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            // Show notification if enabled
            if (EditorPrefs.GetBool(PlayForgeManager.PREFS_PREFIX + "ShowNotifications", true))
            {
                string templateNotification = "";
                if (defaultTemplate is not null) templateNotification = $"(Template: {defaultTemplate.DisplayName})";
                Debug.Log($"[PlayForge] Created {typeInfo.DisplayName}: {assetName} {templateNotification} at {fullPath}");
            }
            
            onCreated?.Invoke();
            Close();
        }
        
        /// <summary>
        /// Sets the internal name field on the asset based on its type.
        /// Handles Definition.Name pattern (Ability, Effect) and direct Name fields.
        /// The display name is set as-is (with spaces), not sanitized.
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
            
            // Try direct Name field (used by some asset types like Attribute)
            var directNameField = assetType.GetField("Name", flags);
            if (directNameField != null && directNameField.FieldType == typeof(string))
            {
                directNameField.SetValue(asset, name);
                return;
            }
            
            // Try _name field (backing field pattern)
            var backingField = assetType.GetField("_name", flags);
            if (backingField != null && backingField.FieldType == typeof(string))
            {
                backingField.SetValue(asset, name);
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