using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class TheForge
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // CREATE TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        // Create-tab sub-tabs: Assets (asset creation) and Tags (declare project tags).
        private int currentCreateTab = 0;
        private static readonly string[] createTabNames = { "Assets", "Tags" };

        private void BuildCreateTab()
        {
            // Sub-tab bar
            var subBar = new VisualElement();
            subBar.style.flexDirection = FlexDirection.Row;
            subBar.style.marginBottom = 8;
            contentContainer.Add(subBar);

            for (int i = 0; i < createTabNames.Length; i++)
            {
                int idx = i;
                var btn = new Button(() => { currentCreateTab = idx; ShowTab(0); }) { text = createTabNames[i] };
                bool active = currentCreateTab == i;
                btn.style.marginRight = 4;
                btn.style.paddingLeft = 14;
                btn.style.paddingRight = 14;
                btn.style.paddingTop = 5;
                btn.style.paddingBottom = 5;
                btn.style.backgroundColor = active ? Colors.AccentBlue : Colors.ButtonBackground;
                btn.style.color = active ? Color.white : Colors.LabelText;
                btn.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                subBar.Add(btn);
            }

            if (currentCreateTab == 1)
            {
                BuildCreateTagsSubTab();
                return;
            }

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
            int takeAmount = PlayForgePreferences.RecentlyCreatedRows;
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
            
            var icon = new ForgeTypeIcon(typeInfo.Type);
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
            var icon = new ForgeTypeIcon(asset.GetType());
            icon.style.marginRight = 4;
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
            var tagCount = ForgeTagRegistry.GetAllTags().Count();
            var tagCard = CreateStatCard("Unique Tags", tagCount.ToString(), Colors.AccentCyan);
            tagCard.tooltip = $"{tagCount} unique tag(s) used across assets";
            statsContainer.Add(tagCard);
            
            // Context count  
            var contextCount = ForgeTagRegistry.GetAllContextKeys().Count();
            var contextCard = CreateStatCard("Tag Contexts", contextCount.ToString(), Colors.AccentPurple);
            contextCard.tooltip = $"{contextCount} tag context(s) detected";
            statsContainer.Add(contextCard);
        }
        
        private VisualElement CreateHomeStatCard(string label, string value, Color color)
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

        // ═══════════════════════════════════════════════════════════════════════════
        // CREATE TAB — TAGS SUB-TAB
        //
        // Declares project tags into a ForgeTagLibrary asset. Declared tags appear in
        // tag pickers (via ForgeTagRegistry scanning every library) under their chosen
        // contexts — even before any asset references them.
        // ═══════════════════════════════════════════════════════════════════════════

        private ForgeTagLibrary selectedTagLibrary;
        private string newTagName = "";
        private string newTagNote = "";
        private readonly HashSet<string> newTagContexts = new();

        /// <summary>Context constants offered when authoring a tag (from ForgeContext).</summary>
        private static readonly string[] TagContextOptions =
        {
            ForgeContext.Ability, ForgeContext.Effect, ForgeContext.Entity, ForgeContext.Attribute,
            ForgeContext.AttributeSet, ForgeContext.AssetIdentifier, ForgeContext.ContextIdentifier, ForgeContext.Granted,
            ForgeContext.Data, ForgeContext.Texture, ForgeContext.Modifier, ForgeContext.Condition,
            ForgeContext.Required, ForgeContext.Blocked, ForgeContext.Visibility, ForgeContext.Affiliation,
            ForgeContext.Category, ForgeContext.Targeting, ForgeContext.Impact, ForgeContext.Duration,
            ForgeContext.RetentionGroup, ForgeContext.Cost, ForgeContext.Cooldown, ForgeContext.Worker,
            ForgeContext.Trigger, ForgeContext.Stage, ForgeContext.Universal, ForgeContext.System, 
        };

        private void BuildCreateTagsSubTab()
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            contentContainer.Add(scrollView);
            var content = new VisualElement();
            scrollView.Add(content);

            content.Add(CreateSectionHeader("Declare Tag", "Add a project tag and assign its contexts"));

            EnsureTagLibrarySelection();

            // ── Target library ────────────────────────────────────────────────
            var libRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 10, marginBottom = 8 } };
            content.Add(libRow);

            var libLabel = new Label("Library:") { style = { width = 70, color = Colors.LabelText, fontSize = 11 } };
            libRow.Add(libLabel);

            var libField = new ObjectField { objectType = typeof(ForgeTagLibrary), value = selectedTagLibrary, allowSceneObjects = false };
            libField.style.flexGrow = 1;
            libField.RegisterValueChangedCallback(evt => selectedTagLibrary = evt.newValue as ForgeTagLibrary);
            libRow.Add(libField);

            var newLibBtn = new Button(CreateNewTagLibrary) { text = "New" };
            newLibBtn.style.marginLeft = 4;
            libRow.Add(newLibBtn);

            // ── Tag name ──────────────────────────────────────────────────────
            var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            content.Add(nameRow);
            nameRow.Add(new Label("Name:") { style = { width = 70, color = Colors.LabelText, fontSize = 11 } });
            var nameField = new TextField { value = newTagName };
            nameField.style.flexGrow = 1;
            nameField.tooltip = "Dot-delimited hierarchical name, e.g. Status.Debuff.Burn";
            nameField.RegisterValueChangedCallback(evt => newTagName = evt.newValue);
            nameRow.Add(nameField);

            // ── Note ──────────────────────────────────────────────────────────
            var noteRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            content.Add(noteRow);
            noteRow.Add(new Label("Note:") { style = { width = 70, color = Colors.LabelText, fontSize = 11 } });
            var noteField = new TextField { value = newTagNote };
            noteField.style.flexGrow = 1;
            noteField.RegisterValueChangedCallback(evt => newTagNote = evt.newValue);
            noteRow.Add(noteField);

            // ── Contexts (multi-select chips) ─────────────────────────────────
            content.Add(new Label("Contexts") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.HeaderText, marginBottom = 4 } });
            var hint = new Label("None selected = Universal (available everywhere).");
            hint.style.fontSize = 9;
            hint.style.color = Colors.HintText;
            hint.style.marginBottom = 6;
            content.Add(hint);

            var chipGrid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 10 } };
            content.Add(chipGrid);
            foreach (var context in TagContextOptions)
            {
                chipGrid.Add(CreateTagContextChip(context));
            }

            // ── Add button ────────────────────────────────────────────────────
            var addBtn = new Button(AddDeclaredTag) { text = "＋ Add Tag" };
            addBtn.style.marginTop = 4;
            addBtn.style.paddingTop = 6;
            addBtn.style.paddingBottom = 6;
            addBtn.style.backgroundColor = Colors.AccentGreen;
            addBtn.style.color = Color.white;
            addBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            content.Add(addBtn);

            content.Add(CreateDivider(16));

            // ── Existing declared tags in the selected library ────────────────
            content.Add(CreateSectionHeader("Declared Tags", selectedTagLibrary != null ? selectedTagLibrary.name : "No library selected"));

            if (selectedTagLibrary?.Entries is { Count: > 0 } entries)
            {
                foreach (var entry in entries.ToList())
                {
                    content.Add(CreateDeclaredTagRow(entry));
                }
            }
            else
            {
                var empty = new Label("No tags declared yet.");
                empty.style.color = Colors.HintText;
                empty.style.fontSize = 11;
                empty.style.marginTop = 6;
                content.Add(empty);
            }
        }

        private Button CreateTagContextChip(string context)
        {
            bool selected = newTagContexts.Contains(context);
            var chip = new Button { text = context };
            chip.style.marginRight = 4;
            chip.style.marginBottom = 4;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.paddingTop = 3;
            chip.style.paddingBottom = 3;
            chip.style.fontSize = 10;
            chip.style.backgroundColor = selected ? Colors.AccentBlue : Colors.ButtonBackground;
            chip.style.color = selected ? Color.white : Colors.LabelText;
            chip.clicked += () =>
            {
                if (!newTagContexts.Remove(context)) newTagContexts.Add(context);
                ShowTab(0);
            };
            return chip;
        }

        private VisualElement CreateDeclaredTagRow(ForgeTagLibrary.Entry entry)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingTop = 3, paddingBottom = 3, paddingLeft = 6, marginBottom = 2 } };
            row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
            row.style.borderTopLeftRadius = 3;
            row.style.borderBottomLeftRadius = 3;

            var name = new Label(entry.Name) { style = { width = 200, color = Colors.AccentCyan, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold } };
            row.Add(name);

            string contexts = entry.Contexts is { Count: > 0 } ? string.Join(", ", entry.Contexts) : "Universal";
            var ctx = new Label(contexts) { style = { flexGrow = 1, color = Colors.HintText, fontSize = 10 } };
            row.Add(ctx);

            var removeBtn = new Button(() =>
            {
                selectedTagLibrary.Entries.Remove(entry);
                EditorUtility.SetDirty(selectedTagLibrary);
                AssetDatabase.SaveAssets();
                ForgeTagRegistry.MarkDirty();
                ShowTab(0);
            }) { text = "✕" };
            removeBtn.style.fontSize = 10;
            removeBtn.tooltip = "Remove tag declaration";
            row.Add(removeBtn);

            return row;
        }

        private void EnsureTagLibrarySelection()
        {
            if (selectedTagLibrary != null) return;

            var guids = AssetDatabase.FindAssets("t:ForgeTagLibrary");
            if (guids.Length > 0)
            {
                selectedTagLibrary = AssetDatabase.LoadAssetAtPath<ForgeTagLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void CreateNewTagLibrary()
        {
            string folder = EditorPrefs.GetString(TheForge.PREFS_PREFIX + "DefaultCreateFolder", "Assets");
            if (!AssetDatabase.IsValidFolder(folder)) folder = "Assets";

            var library = ScriptableObject.CreateInstance<ForgeTagLibrary>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/TagLibrary.asset");
            AssetDatabase.CreateAsset(library, path);
            AssetDatabase.SaveAssets();

            selectedTagLibrary = library;
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            ShowTab(0);
        }

        private void AddDeclaredTag()
        {
            if (string.IsNullOrWhiteSpace(newTagName))
            {
                EditorUtility.DisplayDialog("Error", "Enter a tag name.", "OK");
                return;
            }

            if (selectedTagLibrary == null)
            {
                CreateNewTagLibrary();
                if (selectedTagLibrary == null) return;
            }

            var name = newTagName.Trim();
            if (selectedTagLibrary.Entries.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("Duplicate", $"'{name}' is already declared in this library.", "OK");
                return;
            }

            selectedTagLibrary.Entries.Add(new ForgeTagLibrary.Entry
            {
                Name = name,
                Contexts = newTagContexts.ToList(),
                Note = newTagNote,
            });

            EditorUtility.SetDirty(selectedTagLibrary);
            AssetDatabase.SaveAssets();

            // Register immediately so pickers surface the tag without a manual rescan.
            Tag.Generate(name);
            ForgeTagRegistry.MarkDirty();

            newTagName = "";
            newTagNote = "";
            newTagContexts.Clear();
            ShowTab(0);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Create Asset Dialog
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class CreateAssetDialog : EditorWindow
    {
        private TheForge.AssetTypeInfo typeInfo;
        private string assetPath;
        private string assetName = "";
        private string filePrefix = "";
        private string fileSuffix = "";
        private Action onCreated;
        private Label previewLabel;
        private Label templateLabel;
        private TextField nameField;
        private Button createButton;
        
        // Characters not allowed in asset file names
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { ' ', '\t', '\n', '\r' })
            .ToArray();
        
        public void Initialize(TheForge.AssetTypeInfo info, string path, Action callback)
        {
            typeInfo = info;
            assetPath = path;
            onCreated = callback;
            
            // Get prefix/suffix settings
            filePrefix = PlayForgeSettings.PrefixFor(info.Type);
            fileSuffix = PlayForgeSettings.SuffixFor(info.Type);
            
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
            
            var header = new Label($"New {typeInfo.DisplayName}");
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
            
            nameField = new TextField();
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
            
            createButton = new Button(CreateAsset) { text = "Create", focusable = false };
            createButton.style.backgroundColor = typeInfo.Color;
            btnRow.Add(createButton);
            
            nameField.Focus();
            UpdateFilePreview();
        }

        void OnGUI()
        {
            // Key detection
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    e.Use();
                    CreateAsset();
                }
            }
        }
        
        private void UpdateTemplateLabel()
        {
            if (templateLabel == null) return;
            
            var templateAsset = GetDefaultTemplate();
            if (templateAsset != null)
            {
                var templateName = TheForge.GetAssetDisplayName(templateAsset);
                templateLabel.text = $"Template: {templateName}";
                templateLabel.style.color = Colors.AccentCyan;
            }
            else
            {
                templateLabel.text = "Template: None (creating blank asset)";
                templateLabel.style.color = Colors.HintText;
            }
        }
        
        // The type's New Asset default, from Project Settings → PlayForge (ED-21)
        private ScriptableObject GetDefaultTemplate() => PlayForgeSettings.NewAssetDefault(typeInfo.Type);
        
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
            var sanitized = Regex.Replace(name, @"\s+", "_");
            
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
            var templateAsset = GetDefaultTemplate();
            if (templateAsset != null)
            {
                // Copy serialized data from template to new asset
                EditorUtility.CopySerialized(templateAsset, asset);

                if (asset is BaseForgeAsset bfa && templateAsset is BaseForgeAsset tbfa)
                {
                    bfa.SetTemplate(tbfa);
                }
            }
            
            // Set the display name on the asset (after template copy so it's not overwritten)
            SetAssetName(asset, assetName);
            
            // A template that gives levels gives them to the new asset
            LinkToTemplate(asset, templateAsset as BaseForgeLevelProvider);
            
            // Generate unique file path
            var fullPath = $"{assetPath}/{sanitizedFileName}.asset";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            // Show notification if enabled
            if (PlayForgePreferences.ShowNotifications.Value)
            {
                string templateNotification = "";
                if (templateAsset != null) templateNotification = $"(Template: {TheForge.GetAssetDisplayName(templateAsset)})";
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
        
        // A template that gives levels gives them to the new asset, as the Create dialog has done. The type settings it
        // applied from EditorPrefs went with the Settings tab: all but the Requirement's category named fields the types
        // don't have, so they did nothing (ED-21)
        private static void LinkToTemplate(ScriptableObject asset, BaseForgeLevelProvider provider)
        {
            if (provider == null) return;
            switch (asset)
            {
                case Ability ability when !ability.IsLinked: ability.LinkToProvider(provider); break;
                case GameplayEffect effect when !effect.IsLinked: effect.LinkToProvider(provider); break;
                case Item item when !item.IsLinked: item.LinkToProvider(provider); break;
                case EntityIdentity entity when !entity.IsLinked: entity.LinkToProvider(provider); break;
            }
        }
    }
}