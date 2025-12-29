using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeManager
    {
        // Asset type tabs state
        private int currentAssetTypeTab = 0;
        private List<Button> assetTypeTabButtons = new List<Button>();
        private VisualElement assetTypeContentContainer;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SETTINGS TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSettingsTab()
        {
            // Settings sub-tab bar
            var settingsTabBar = new VisualElement();
            settingsTabBar.style.flexDirection = FlexDirection.Row;
            settingsTabBar.style.marginBottom = 12;
            settingsTabBar.style.paddingBottom = 8;
            settingsTabBar.style.borderBottomWidth = 1;
            settingsTabBar.style.borderBottomColor = Colors.BorderDark;
            contentContainer.Add(settingsTabBar);
            
            settingsTabButtons.Clear();
            
            for (int i = 0; i < settingsTabNames.Length; i++)
            {
                int tabIndex = i;
                var btn = CreateSettingsTabButton(settingsTabNames[i], () => ShowSettingsSubTab(tabIndex));
                settingsTabButtons.Add(btn);
                settingsTabBar.Add(btn);
            }
            
            // Content area for settings
            settingsContentContainer = new VisualElement();
            settingsContentContainer.style.flexGrow = 1;
            contentContainer.Add(settingsContentContainer);
            
            ShowSettingsSubTab(currentSettingsTab);
        }
        
        private Button CreateSettingsTabButton(string text, Action onClick)
        {
            var btn = CreateButton(text, onClick);
            btn.style.paddingLeft = 16;
            btn.style.paddingRight = 16;
            btn.style.paddingTop = 6;
            btn.style.paddingBottom = 6;
            btn.style.marginRight = 8;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.fontSize = 11;
            return btn;
        }
        
        private void ShowSettingsSubTab(int index)
        {
            currentSettingsTab = index;
            
            for (int i = 0; i < settingsTabButtons.Count; i++)
            {
                bool isActive = i == currentSettingsTab;
                settingsTabButtons[i].style.backgroundColor = isActive 
                    ? Colors.AccentBlue
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
                settingsTabButtons[i].style.color = isActive 
                    ? Colors.HeaderText 
                    : Colors.LabelText;
            }
            
            settingsContentContainer.Clear();
            
            switch (currentSettingsTab)
            {
                case 0: BuildGeneralSettingsTab(); break;
                case 1: BuildAssetsSettingsTab(); break;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GENERAL SETTINGS TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildGeneralSettingsTab()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            settingsContentContainer.Add(scrollView);
            
            // View Settings
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
            
            // Create Tab Settings
            var createSection = CreateSettingsSection("Create Tab Settings", "Customize the Create tab", Colors.AccentOrange);
            scrollView.Add(createSection);
            
            var recentRowsRow = new VisualElement();
            recentRowsRow.style.flexDirection = FlexDirection.Row;
            recentRowsRow.style.alignItems = Align.Center;
            recentRowsRow.style.marginTop = 8;
            createSection.Add(recentRowsRow);
            
            var recentRowsLabel = new Label("Recently created rows:");
            recentRowsLabel.style.width = 160;
            recentRowsLabel.style.fontSize = 11;
            recentRowsRow.Add(recentRowsLabel);
            
            var recentRowsField = new IntegerField();
            recentRowsField.style.width = 60;
            recentRowsField.value = EditorPrefs.GetInt(PREFS_PREFIX + "RecentlyCreatedRows", 10);
            recentRowsField.RegisterValueChangedCallback(evt => 
            {
                var clamped = Mathf.Clamp(evt.newValue, 0, 50);
                EditorPrefs.SetInt(PREFS_PREFIX + "RecentlyCreatedRows", clamped);
                if (clamped != evt.newValue) recentRowsField.value = clamped;
            });
            recentRowsRow.Add(recentRowsField);
            
            var recentRowsHint = new Label("(0-50, 0 to hide)");
            recentRowsHint.style.fontSize = 10;
            recentRowsHint.style.color = Colors.HintText;
            recentRowsHint.style.marginLeft = 8;
            recentRowsRow.Add(recentRowsHint);
            
            // Tag Registry Settings
            var tagSection = CreateSettingsSection("Tag Registry", "Tag scanning and context management", Colors.AccentPurple);
            scrollView.Add(tagSection);
            
            var refreshTagsBtn = CreateButton("Refresh Tag Registry", () =>
            {
                TagRegistry.RefreshCache();
                ShowTab(currentTab);
            });
            refreshTagsBtn.style.alignSelf = Align.FlexStart;
            refreshTagsBtn.style.marginTop = 8;
            ApplyButtonStyle(refreshTagsBtn);
            tagSection.Add(refreshTagsBtn);
            
            var tagStatsLabel = new Label($"Unique Tags: {TagRegistry.GetAllTags().Count()}\nContexts: {TagRegistry.GetAllContextKeys().Count()}\nLast Scan: {(TagRegistry.IsCacheValid ? TagRegistry.LastScanTime.ToString("HH:mm:ss") : "Never")}");
            tagStatsLabel.style.fontSize = 10;
            tagStatsLabel.style.color = Colors.HintText;
            tagStatsLabel.style.marginTop = 8;
            tagStatsLabel.style.whiteSpace = WhiteSpace.Normal;
            tagSection.Add(tagStatsLabel);
            
            // General Preferences
            var generalSection = CreateSettingsSection("General", "Manager preferences", Colors.AccentBlue);
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
            
            // Danger Zone
            var dangerSection = CreateSettingsSection("Danger Zone", "Destructive operations", Colors.AccentRed);
            scrollView.Add(dangerSection);
            
            var clearCacheBtn = CreateButton("Clear All Caches", () =>
            {
                if (EditorUtility.DisplayDialog("Clear Cache", "Clear asset and tag caches and reload?", "Yes", "Cancel"))
                {
                    cachedAssets.Clear();
                    TagRegistry.MarkDirty();
                    RefreshAssetCache();
                    ShowTab(currentTab);
                }
            });
            clearCacheBtn.style.alignSelf = Align.FlexStart;
            clearCacheBtn.style.marginTop = 8;
            dangerSection.Add(clearCacheBtn);
            
            var resetSettingsBtn = CreateButton("Reset All Settings", () =>
            {
                if (EditorUtility.DisplayDialog("Reset Settings", "Reset all PlayForge Manager settings to defaults?", "Yes", "Cancel"))
                {
                    ResetAllSettings();
                    ShowTab(2);
                }
            });
            resetSettingsBtn.style.alignSelf = Align.FlexStart;
            resetSettingsBtn.style.marginTop = 4;
            dangerSection.Add(resetSettingsBtn);
        }
        
        private void BuildTemplateTagsEditor(VisualElement parent)
        {
            var tagsContainer = new VisualElement();
            tagsContainer.style.marginTop = 8;
            parent.Add(tagsContainer);
            
            var tagsList = new VisualElement();
            tagsList.style.marginBottom = 8;
            tagsContainer.Add(tagsList);
            
            void RebuildTagsList()
            {
                tagsList.Clear();
                foreach (var tag in TemplateRegistry.GetAllTags())
                {
                    var row = CreateTemplateTagRow(tag, () =>
                    {
                        RebuildTagsList();
                        if (TemplateRegistry.IsDirty)
                            TemplateRegistry.Save();
                    });
                    tagsList.Add(row);
                }
            }
            
            RebuildTagsList();
            
            // Add new tag row
            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.Center;
            addRow.style.marginTop = 8;
            tagsContainer.Add(addRow);
            
            var newTagName = new TextField();
            newTagName.style.flexGrow = 1;
            newTagName.style.maxWidth = 150;
            newTagName.value = "";
            addRow.Add(newTagName);
            
            var newTagColor = new ColorField();
            newTagColor.style.width = 60;
            newTagColor.style.marginLeft = 4;
            newTagColor.value = new Color(0.5f, 0.7f, 1f);
            addRow.Add(newTagColor);
            
            var addTagBtn = CreateButton("+ Add Tag", () =>
            {
                var name = newTagName.value.Trim();
                if (string.IsNullOrEmpty(name)) return;
                if (TemplateRegistry.GetTag(name) != null)
                {
                    EditorUtility.DisplayDialog("Duplicate", $"Tag '{name}' already exists.", "OK");
                    return;
                }
                
                TemplateRegistry.AddTag(new TemplateTag(name, newTagColor.value));
                TemplateRegistry.Save();
                newTagName.value = "";
                RebuildTagsList();
            });
            addTagBtn.style.marginLeft = 8;
            addRow.Add(addTagBtn);
        }
        
        private VisualElement CreateTemplateTagRow(TemplateTag tag, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            
            var colorIndicator = new VisualElement();
            colorIndicator.style.width = 12;
            colorIndicator.style.height = 12;
            colorIndicator.style.backgroundColor = tag.GetColor();
            colorIndicator.style.borderTopLeftRadius = 2;
            colorIndicator.style.borderTopRightRadius = 2;
            colorIndicator.style.borderBottomLeftRadius = 2;
            colorIndicator.style.borderBottomRightRadius = 2;
            colorIndicator.style.marginRight = 8;
            row.Add(colorIndicator);
            
            var nameLabel = new Label(tag.Name);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = Colors.LabelText;
            row.Add(nameLabel);
            
            var editBtn = CreateButton("✎", () =>
            {
                ShowEditTagDialog(tag, onChanged);
            });
            editBtn.style.width = 24;
            editBtn.style.height = 20;
            editBtn.style.fontSize = 10;
            editBtn.tooltip = "Edit tag";
            row.Add(editBtn);
            
            var deleteBtn = CreateButton("✕", () =>
            {
                if (EditorUtility.DisplayDialog("Delete Tag", $"Delete template tag '{tag.Name}'?\nTemplates using this tag will become untagged.", "Delete", "Cancel"))
                {
                    TemplateRegistry.RemoveTag(tag.Name);
                    onChanged?.Invoke();
                }
            });
            deleteBtn.style.width = 24;
            deleteBtn.style.height = 20;
            deleteBtn.style.fontSize = 10;
            deleteBtn.style.marginLeft = 2;
            deleteBtn.tooltip = "Delete tag";
            row.Add(deleteBtn);
            
            return row;
        }
        
        private void ShowEditTagDialog(TemplateTag tag, Action onSaved)
        {
            var dialog = CreateInstance<EditTemplateTagDialog>();
            dialog.Initialize(tag, () =>
            {
                TemplateRegistry.Save();
                onSaved?.Invoke();
            });
            dialog.ShowUtility();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASSETS SETTINGS TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAssetsSettingsTab()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            settingsContentContainer.Add(scrollView);
            
            // Asset Paths Section
            var pathsSection = CreateSettingsSection("Asset Paths", "Default folders for creating new assets", Colors.AccentBlue);
            scrollView.Add(pathsSection);
            
            var pathsHint = new Label("These paths are used when creating new assets from the Create tab.");
            pathsHint.style.fontSize = 10;
            pathsHint.style.color = Colors.HintText;
            pathsHint.style.marginTop = 4;
            pathsHint.style.marginBottom = 8;
            pathsHint.style.whiteSpace = WhiteSpace.Normal;
            pathsSection.Add(pathsHint);
            
            foreach (var typeInfo in AssetTypes.Where(t => t.CanCreate))
            {
                var pathRow = CreatePathSettingRow(typeInfo);
                pathsSection.Add(pathRow);
            }
            
            var resetPathsBtn = CreateButton("Reset to Defaults", () =>
            {
                if (EditorUtility.DisplayDialog("Reset Paths", "Reset all asset paths to defaults?", "Yes", "Cancel"))
                {
                    foreach (var t in AssetTypes)
                        EditorPrefs.DeleteKey(PREFS_PREFIX + "Path_" + t.Type.Name);
                    ShowSettingsSubTab(currentSettingsTab);
                }
            });
            resetPathsBtn.style.alignSelf = Align.FlexStart;
            resetPathsBtn.style.marginTop = 12;
            pathsSection.Add(resetPathsBtn);
            
            // ═══════════════════════════════════════════════════════════════════════════
            // ASSET TYPE TABS SECTION
            // ═══════════════════════════════════════════════════════════════════════════
            
            var assetTypeSection = CreateSettingsSection("Asset Type Settings", "Templates and configuration per asset type", Colors.AccentPurple);
            scrollView.Add(assetTypeSection);
            
            // Asset type tab bar
            var assetTypeTabBar = new VisualElement();
            assetTypeTabBar.style.flexDirection = FlexDirection.Row;
            assetTypeTabBar.style.flexWrap = Wrap.Wrap;
            assetTypeTabBar.style.marginTop = 8;
            assetTypeTabBar.style.marginBottom = 12;
            assetTypeSection.Add(assetTypeTabBar);
            
            assetTypeTabButtons.Clear();
            
            for (int i = 0; i < AssetTypes.Count; i++)
            {
                int tabIndex = i;
                var typeInfo = AssetTypes[i];
                var btn = CreateAssetTypeTabButton(typeInfo, () => ShowAssetTypeTab(tabIndex));
                assetTypeTabButtons.Add(btn);
                assetTypeTabBar.Add(btn);
            }
            
            // Asset type content container
            assetTypeContentContainer = new VisualElement();
            assetTypeContentContainer.style.marginTop = 4;
            assetTypeContentContainer.style.paddingTop = 12;
            assetTypeContentContainer.style.borderTopWidth = 1;
            assetTypeContentContainer.style.borderTopColor = Colors.BorderDark;
            assetTypeSection.Add(assetTypeContentContainer);
            
            ShowAssetTypeTab(currentAssetTypeTab);
            
            // Template Tags Section
            var tagsSection = CreateSettingsSection("Template Tags", "Manage tags for categorizing templates", Colors.AccentCyan);
            scrollView.Add(tagsSection);
            
            BuildTemplateTagsEditor(tagsSection);
            
            // Asset Validation Section
            var validationSection = CreateSettingsSection("Asset Validation", "Check for potential issues", Colors.AccentOrange);
            scrollView.Add(validationSection);
            
            var validateBtn = CreateButton("Validate All Assets", RunValidation);
            validateBtn.style.alignSelf = Align.FlexStart;
            validateBtn.style.marginTop = 8;
            ApplyButtonStyle(validateBtn);
            validationSection.Add(validateBtn);
            
            var validationHint = new Label("Checks for missing references, empty names, and other common issues.");
            validationHint.style.fontSize = 10;
            validationHint.style.color = Colors.HintText;
            validationHint.style.marginTop = 8;
            validationHint.style.whiteSpace = WhiteSpace.Normal;
            validationSection.Add(validationHint);
        }
        
        private Button CreateAssetTypeTabButton(AssetTypeInfo typeInfo, Action onClick)
        {
            var btn = CreateButton($"{typeInfo.Icon} {typeInfo.DisplayName}", onClick);
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 5;
            btn.style.paddingBottom = 5;
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.fontSize = 10;
            btn.style.borderLeftWidth = 2;
            btn.style.borderLeftColor = typeInfo.Color;
            return btn;
        }
        
        private void ShowAssetTypeTab(int index)
        {
            currentAssetTypeTab = index;
            
            for (int i = 0; i < assetTypeTabButtons.Count; i++)
            {
                bool isActive = i == currentAssetTypeTab;
                var typeInfo = AssetTypes[i];
                assetTypeTabButtons[i].style.backgroundColor = isActive 
                    ? new Color(typeInfo.Color.r, typeInfo.Color.g, typeInfo.Color.b, 0.3f)
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
                assetTypeTabButtons[i].style.color = isActive 
                    ? Colors.HeaderText 
                    : Colors.LabelText;
            }
            
            assetTypeContentContainer.Clear();
            
            var selectedTypeInfo = AssetTypes[index];
            BuildAssetTypeSettings(selectedTypeInfo);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASSET TYPE SETTINGS (Templates + Additional Settings)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAssetTypeSettings(AssetTypeInfo typeInfo)
        {
            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 12;
            assetTypeContentContainer.Add(headerRow);
            
            var iconLabel = new Label(typeInfo.Icon);
            iconLabel.style.fontSize = 20;
            iconLabel.style.color = typeInfo.Color;
            iconLabel.style.marginRight = 8;
            headerRow.Add(iconLabel);
            
            var titleLabel = new Label($"{typeInfo.DisplayName} Settings");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            headerRow.Add(titleLabel);
            
            // Templates Section
            BuildTemplatesSection(typeInfo);
            
            // Additional Settings Section (infrastructure for future settings)
            BuildAdditionalSettingsSection(typeInfo);
        }
        
        private void BuildTemplatesSection(AssetTypeInfo typeInfo)
        {
            var templatesHeader = new VisualElement();
            templatesHeader.style.flexDirection = FlexDirection.Row;
            templatesHeader.style.alignItems = Align.Center;
            templatesHeader.style.marginTop = 8;
            templatesHeader.style.marginBottom = 8;
            assetTypeContentContainer.Add(templatesHeader);
            
            var templatesTitle = new Label("Templates");
            templatesTitle.style.fontSize = 11;
            templatesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            templatesTitle.style.color = Colors.AccentCyan;
            templatesHeader.Add(templatesTitle);
            
            var templatesHint = new Label("  — Default templates for new assets");
            templatesHint.style.fontSize = 10;
            templatesHint.style.color = Colors.HintText;
            templatesHeader.Add(templatesHint);
            
            var templatesList = new VisualElement();
            templatesList.name = "TemplatesList";
            templatesList.style.marginBottom = 8;
            assetTypeContentContainer.Add(templatesList);
            
            void RebuildTemplatesList()
            {
                templatesList.Clear();
                
                var templates = TemplateRegistry.GetTemplates(typeInfo.Type).Where(t => t.IsValid()).ToList();
                
                if (templates.Count == 0)
                {
                    var emptyLabel = new Label("No templates configured. Add an existing asset as a template below.");
                    emptyLabel.style.fontSize = 10;
                    emptyLabel.style.color = Colors.HintText;
                    emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    emptyLabel.style.marginBottom = 8;
                    templatesList.Add(emptyLabel);
                }
                else
                {
                    foreach (var template in templates)
                    {
                        var row = CreateTemplateRow(typeInfo, template, RebuildTemplatesList);
                        templatesList.Add(row);
                    }
                }
            }
            
            RebuildTemplatesList();
            
            // Add template controls
            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.Center;
            addRow.style.marginTop = 4;
            assetTypeContentContainer.Add(addRow);
            
            var assetField = new ObjectField();
            assetField.objectType = typeInfo.Type;
            assetField.style.flexGrow = 1;
            assetField.style.maxWidth = 200;
            assetField.label = "";
            addRow.Add(assetField);
            
            var tagDropdown = new PopupField<string>(
                TemplateRegistry.GetAllTags().Select(t => t.Name).Prepend("(No Tag)").ToList(),
                0
            );
            tagDropdown.style.width = 100;
            tagDropdown.style.marginLeft = 4;
            addRow.Add(tagDropdown);
            
            var addAsDefaultToggle = new Toggle("Default");
            addAsDefaultToggle.style.marginLeft = 8;
            addAsDefaultToggle.tooltip = "Set as default template for new assets";
            addRow.Add(addAsDefaultToggle);
            
            var addBtn = CreateButton("+ Add Template", () =>
            {
                var asset = assetField.value as ScriptableObject;
                if (asset == null)
                {
                    EditorUtility.DisplayDialog("No Asset", "Please select an asset to use as a template.", "OK");
                    return;
                }
                
                var tagName = tagDropdown.value == "(No Tag)" ? null : tagDropdown.value;
                TemplateRegistry.AddTemplate(typeInfo.Type, asset, tagName, addAsDefaultToggle.value);
                TemplateRegistry.Save();
                
                assetField.value = null;
                tagDropdown.index = 0;
                addAsDefaultToggle.value = false;
                
                RebuildTemplatesList();
            });
            addBtn.style.marginLeft = 8;
            ApplyButtonStyle(addBtn);
            addRow.Add(addBtn);
        }
        
        private VisualElement CreateTemplateRow(AssetTypeInfo typeInfo, AssetTemplate template, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 4;
            row.style.backgroundColor = template.IsDefault 
                ? new Color(typeInfo.Color.r, typeInfo.Color.g, typeInfo.Color.b, 0.15f)
                : new Color(0.15f, 0.15f, 0.15f, 0.5f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            
            if (template.IsDefault)
            {
                row.style.borderLeftWidth = 3;
                row.style.borderLeftColor = typeInfo.Color;
            }
            
            // Default indicator
            if (template.IsDefault)
            {
                var defaultBadge = new Label("DEFAULT");
                defaultBadge.style.fontSize = 8;
                defaultBadge.style.color = typeInfo.Color;
                defaultBadge.style.backgroundColor = new Color(typeInfo.Color.r, typeInfo.Color.g, typeInfo.Color.b, 0.2f);
                defaultBadge.style.paddingLeft = 4;
                defaultBadge.style.paddingRight = 4;
                defaultBadge.style.paddingTop = 1;
                defaultBadge.style.paddingBottom = 1;
                defaultBadge.style.borderTopLeftRadius = 3;
                defaultBadge.style.borderTopRightRadius = 3;
                defaultBadge.style.borderBottomLeftRadius = 3;
                defaultBadge.style.borderBottomRightRadius = 3;
                defaultBadge.style.marginRight = 6;
                row.Add(defaultBadge);
            }
            
            // Tag chip
            if (!string.IsNullOrEmpty(template.TemplateTagName))
            {
                var tag = TemplateRegistry.GetTag(template.TemplateTagName);
                if (tag != null)
                {
                    var tagChip = new Label(tag.Name);
                    tagChip.style.fontSize = 9;
                    tagChip.style.color = Colors.HeaderText;
                    tagChip.style.backgroundColor = tag.GetColor();
                    tagChip.style.paddingLeft = 5;
                    tagChip.style.paddingRight = 5;
                    tagChip.style.paddingTop = 1;
                    tagChip.style.paddingBottom = 1;
                    tagChip.style.borderTopLeftRadius = 6;
                    tagChip.style.borderTopRightRadius = 6;
                    tagChip.style.borderBottomLeftRadius = 6;
                    tagChip.style.borderBottomRightRadius = 6;
                    tagChip.style.marginRight = 6;
                    row.Add(tagChip);
                }
            }
            
            // Asset name
            var asset = template.GetAsset();
            var nameLabel = new Label(asset != null ? GetAssetDisplayName(asset) : "(Missing)");
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = asset != null ? Colors.LabelText : Colors.AccentRed;
            row.Add(nameLabel);
            
            // File name
            if (asset != null)
            {
                var fileLabel = new Label($"({asset.name})");
                fileLabel.style.fontSize = 9;
                fileLabel.style.color = Colors.HintText;
                fileLabel.style.marginRight = 8;
                row.Add(fileLabel);
            }
            
            // Tag dropdown
            var tagOptions = TemplateRegistry.GetAllTags().Select(t => t.Name).Prepend("(No Tag)").ToList();
            var currentTagIndex = string.IsNullOrEmpty(template.TemplateTagName) ? 0 : tagOptions.IndexOf(template.TemplateTagName);
            if (currentTagIndex < 0) currentTagIndex = 0;
            
            var tagDropdown = new PopupField<string>(tagOptions, currentTagIndex);
            tagDropdown.style.width = 80;
            tagDropdown.style.marginRight = 4;
            tagDropdown.RegisterValueChangedCallback(evt =>
            {
                var newTag = evt.newValue == "(No Tag)" ? null : evt.newValue;
                TemplateRegistry.SetTemplateTag(typeInfo.Type, template.AssetGUID, newTag);
                TemplateRegistry.Save();
                onChanged?.Invoke();
            });
            row.Add(tagDropdown);
            
            // Set as default button
            if (!template.IsDefault)
            {
                var setDefaultBtn = CreateButton("★", () =>
                {
                    TemplateRegistry.SetDefaultTemplate(typeInfo.Type, template.AssetGUID);
                    TemplateRegistry.Save();
                    onChanged?.Invoke();
                });
                setDefaultBtn.style.width = 24;
                setDefaultBtn.style.height = 20;
                setDefaultBtn.style.fontSize = 10;
                setDefaultBtn.tooltip = "Set as default template";
                row.Add(setDefaultBtn);
            }
            else
            {
                var clearDefaultBtn = CreateButton("☆", () =>
                {
                    TemplateRegistry.ClearDefaultTemplate(typeInfo.Type);
                    TemplateRegistry.Save();
                    onChanged?.Invoke();
                });
                clearDefaultBtn.style.width = 24;
                clearDefaultBtn.style.height = 20;
                clearDefaultBtn.style.fontSize = 10;
                clearDefaultBtn.tooltip = "Clear default";
                row.Add(clearDefaultBtn);
            }
            
            // Select button
            var selectBtn = CreateButton("→", () =>
            {
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            });
            selectBtn.style.width = 24;
            selectBtn.style.height = 20;
            selectBtn.style.fontSize = 10;
            selectBtn.style.marginLeft = 2;
            selectBtn.tooltip = "Select asset";
            row.Add(selectBtn);
            
            // Remove button
            var removeBtn = CreateButton("✕", () =>
            {
                TemplateRegistry.RemoveTemplate(typeInfo.Type, template.AssetGUID);
                TemplateRegistry.Save();
                onChanged?.Invoke();
            });
            removeBtn.style.width = 24;
            removeBtn.style.height = 20;
            removeBtn.style.fontSize = 10;
            removeBtn.style.marginLeft = 2;
            removeBtn.tooltip = "Remove template";
            row.Add(removeBtn);
            
            return row;
        }
        
        private void BuildAdditionalSettingsSection(AssetTypeInfo typeInfo)
        {
            var settingsHeader = new VisualElement();
            settingsHeader.style.flexDirection = FlexDirection.Row;
            settingsHeader.style.alignItems = Align.Center;
            settingsHeader.style.marginTop = 16;
            settingsHeader.style.marginBottom = 8;
            assetTypeContentContainer.Add(settingsHeader);
            
            var settingsTitle = new Label("Additional Settings");
            settingsTitle.style.fontSize = 11;
            settingsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsTitle.style.color = Colors.AccentGreen;
            settingsHeader.Add(settingsTitle);
            
            // Type-specific settings infrastructure
            var settingsContainer = new VisualElement();
            settingsContainer.style.paddingLeft = 4;
            assetTypeContentContainer.Add(settingsContainer);
            
            // Add type-specific settings here
            switch (typeInfo.Type.Name)
            {
                case "Ability":
                    BuildAbilitySettings(settingsContainer);
                    break;
                case "GameplayEffect":
                    BuildEffectSettings(settingsContainer);
                    break;
                case "Attribute":
                    BuildAttributeSettings(settingsContainer);
                    break;
                case "AttributeSet":
                    BuildAttributeSetSettings(settingsContainer);
                    break;
                case "EntityIdentity":
                    BuildEntitySettings(settingsContainer);
                    break;
                default:
                    var noSettings = new Label("No additional settings available.");
                    noSettings.style.fontSize = 10;
                    noSettings.style.color = Colors.HintText;
                    noSettings.style.unityFontStyleAndWeight = FontStyle.Italic;
                    settingsContainer.Add(noSettings);
                    break;
            }
        }
        
        // Type-specific settings (infrastructure - can be expanded)
        private void BuildAbilitySettings(VisualElement container)
        {
            var autoLevelToggle = new Toggle("Auto-set starting level to 1");
            autoLevelToggle.value = EditorPrefs.GetBool(PREFS_PREFIX + "Ability_AutoLevel", true);
            autoLevelToggle.RegisterValueChangedCallback(evt => 
                EditorPrefs.SetBool(PREFS_PREFIX + "Ability_AutoLevel", evt.newValue));
            container.Add(autoLevelToggle);
        }
        
        private void BuildEffectSettings(VisualElement container)
        {
            var placeholder = new Label("Effect-specific settings coming soon.");
            placeholder.style.fontSize = 10;
            placeholder.style.color = Colors.HintText;
            placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(placeholder);
        }
        
        private void BuildAttributeSettings(VisualElement container)
        {
            var placeholder = new Label("Attribute-specific settings coming soon.");
            placeholder.style.fontSize = 10;
            placeholder.style.color = Colors.HintText;
            placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(placeholder);
        }
        
        private void BuildAttributeSetSettings(VisualElement container)
        {
            var placeholder = new Label("AttributeSet-specific settings coming soon.");
            placeholder.style.fontSize = 10;
            placeholder.style.color = Colors.HintText;
            placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(placeholder);
        }
        
        private void BuildEntitySettings(VisualElement container)
        {
            var placeholder = new Label("Entity-specific settings coming soon.");
            placeholder.style.fontSize = 10;
            placeholder.style.color = Colors.HintText;
            placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(placeholder);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SETTINGS HELPERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreatePathSettingRow(AssetTypeInfo typeInfo)
        {
            var container = new VisualElement();
            container.style.marginTop = 8;
            container.style.marginBottom = 4;
            
            // Main path row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);
            
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
                // Open to actual configured path if it exists
                var currentPath = GetPathForType(typeInfo.Type);
                var startPath = AssetDatabase.IsValidFolder(currentPath) ? currentPath : "Assets";
                
                var newPath = EditorUtility.OpenFolderPanel($"Select {typeInfo.DisplayName} Folder", startPath, "");
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
            browseBtn.tooltip = "Browse for folder";
            row.Add(browseBtn);
            
            // Naming options row (prefix/suffix) - compact inline
            var namingRow = new VisualElement();
            namingRow.style.flexDirection = FlexDirection.Row;
            namingRow.style.alignItems = Align.Center;
            namingRow.style.marginLeft = 120; // Align with path field
            namingRow.style.marginTop = 4;
            container.Add(namingRow);
            
            // Preview label (declare early so we can reference it)
            var previewLabel = new Label();
            previewLabel.style.fontSize = 9;
            previewLabel.style.color = Colors.HintText;
            previewLabel.style.marginLeft = 12;
            previewLabel.style.flexGrow = 1;
            
            var prefixLabel = new Label("Prefix:");
            prefixLabel.style.fontSize = 9;
            prefixLabel.style.color = Colors.HintText;
            prefixLabel.style.width = 36;
            namingRow.Add(prefixLabel);
            
            var prefixField = new TextField();
            prefixField.style.width = 70;
            prefixField.style.fontSize = 10;
            prefixField.value = GetPrefixForType(typeInfo.Type);
            prefixField.RegisterValueChangedCallback(evt => 
            {
                SetPrefixForType(typeInfo.Type, evt.newValue);
                UpdateNamingPreview(typeInfo.Type, previewLabel);
            });
            namingRow.Add(prefixField);
            
            var suffixLabel = new Label("Suffix:");
            suffixLabel.style.fontSize = 9;
            suffixLabel.style.color = Colors.HintText;
            suffixLabel.style.marginLeft = 12;
            suffixLabel.style.width = 36;
            namingRow.Add(suffixLabel);
            
            var suffixField = new TextField();
            suffixField.style.width = 70;
            suffixField.style.fontSize = 10;
            suffixField.value = GetPostfixForType(typeInfo.Type);
            suffixField.RegisterValueChangedCallback(evt => 
            {
                SetPostfixForType(typeInfo.Type, evt.newValue);
                UpdateNamingPreview(typeInfo.Type, previewLabel);
            });
            namingRow.Add(suffixField);
            
            namingRow.Add(previewLabel);
            UpdateNamingPreview(typeInfo.Type, previewLabel);
            
            return container;
        }
        
        private void UpdateNamingPreview(Type assetType, Label previewLabel)
        {
            var prefix = GetPrefixForType(assetType);
            var postfix = GetPostfixForType(assetType);
            
            if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(postfix))
            {
                previewLabel.text = "";
            }
            else
            {
                previewLabel.text = $"→ {prefix}Name{postfix}.asset";
            }
        }
        
        private string GetPrefixForType(Type type)
        {
            return EditorPrefs.GetString(PREFS_PREFIX + "Prefix_" + type.Name, "");
        }
        
        private void SetPrefixForType(Type type, string prefix)
        {
            EditorPrefs.SetString(PREFS_PREFIX + "Prefix_" + type.Name, prefix ?? "");
        }
        
        private string GetPostfixForType(Type type)
        {
            return EditorPrefs.GetString(PREFS_PREFIX + "Postfix_" + type.Name, "");
        }
        
        private void SetPostfixForType(Type type, string postfix)
        {
            EditorPrefs.SetString(PREFS_PREFIX + "Postfix_" + type.Name, postfix ?? "");
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
        
        private void ResetAllSettings()
        {
            EditorPrefs.DeleteKey(PREFS_PREFIX + "RememberTypeFilter");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "LastTypeFilter");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "DoubleClickVisualize");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "ShowFileNames");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "AutoRefresh");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "ShowNotifications");
            EditorPrefs.DeleteKey(PREFS_PREFIX + "RecentlyCreatedRows");
            foreach (var t in AssetTypes)
            {
                EditorPrefs.DeleteKey(PREFS_PREFIX + "Path_" + t.Type.Name);
                EditorPrefs.DeleteKey(PREFS_PREFIX + "Prefix_" + t.Type.Name);
                EditorPrefs.DeleteKey(PREFS_PREFIX + "Postfix_" + t.Type.Name);
            }
        }
        
        private void RunValidation()
        {
            var issues = new List<string>();
            
            foreach (var asset in cachedAssets)
            {
                var displayName = GetAssetDisplayName(asset);
                if (string.IsNullOrWhiteSpace(displayName) || displayName == asset.GetType().Name)
                {
                    issues.Add($"• {asset.name} ({asset.GetType().Name}): Empty or default display name");
                }
            }
            
            foreach (var ability in cachedAssets.OfType<Ability>())
            {
                if (ability.Cost == null && ability.Cooldown == null)
                {
                    issues.Add($"• {GetAssetDisplayName(ability)} (Ability): No cost or cooldown defined");
                }
            }
            
            foreach (var effect in cachedAssets.OfType<GameplayEffect>())
            {
                if (effect.Workers == null || effect.Workers.Count == 0)
                {
                    issues.Add($"• {GetAssetDisplayName(effect)} (Effect): No workers defined");
                }
            }
            
            foreach (var set in cachedAssets.OfType<AttributeSet>())
            {
                if ((set.Attributes == null || set.Attributes.Count == 0) && 
                    (set.SubSets == null || set.SubSets.Count == 0))
                {
                    issues.Add($"• {set.name} (AttributeSet): Empty (no attributes or subsets)");
                }
            }
            
            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Complete", "No issues found! All assets look good.", "OK");
            }
            else
            {
                var message = $"Found {issues.Count} potential issue(s):\n\n" + 
                    string.Join("\n", issues.Take(15));
                if (issues.Count > 15)
                    message += $"\n\n... and {issues.Count - 15} more";
                
                EditorUtility.DisplayDialog("Validation Results", message, "OK");
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Edit Template Tag Dialog
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class EditTemplateTagDialog : EditorWindow
    {
        private TemplateTag targetTag;
        private string newName;
        private Color newColor;
        private Action onSaved;
        
        public void Initialize(TemplateTag tag, Action callback)
        {
            targetTag = tag;
            newName = tag.Name;
            newColor = tag.GetColor();
            onSaved = callback;
            
            titleContent = new GUIContent("Edit Template Tag");
            minSize = maxSize = new Vector2(300, 120);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            
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
            nameField.value = newName;
            nameField.RegisterValueChangedCallback(evt => newName = evt.newValue);
            nameRow.Add(nameField);
            
            var colorRow = new VisualElement();
            colorRow.style.flexDirection = FlexDirection.Row;
            colorRow.style.alignItems = Align.Center;
            colorRow.style.marginBottom = 16;
            root.Add(colorRow);
            
            var colorLabel = new Label("Color");
            colorLabel.style.width = 50;
            colorRow.Add(colorLabel);
            
            var colorField = new ColorField();
            colorField.style.flexGrow = 1;
            colorField.value = newColor;
            colorField.RegisterValueChangedCallback(evt => newColor = evt.newValue);
            colorRow.Add(colorField);
            
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            root.Add(btnRow);
            
            var cancelBtn = new Button(Close) { text = "Cancel", focusable = false };
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);
            
            var saveBtn = new Button(SaveTag) { text = "Save", focusable = false };
            saveBtn.style.backgroundColor = ForgeDrawerStyles.Colors.AccentBlue;
            btnRow.Add(saveBtn);
        }
        
        private void SaveTag()
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                EditorUtility.DisplayDialog("Error", "Tag name cannot be empty.", "OK");
                return;
            }
            
            // Check for duplicates (but allow same name if unchanged)
            if (newName != targetTag.Name && TemplateRegistry.GetTag(newName) != null)
            {
                EditorUtility.DisplayDialog("Duplicate", $"Tag '{newName}' already exists.", "OK");
                return;
            }
            
            TemplateRegistry.UpdateTag(targetTag.Name, new TemplateTag(newName, newColor));
            onSaved?.Invoke();
            Close();
        }
    }
}