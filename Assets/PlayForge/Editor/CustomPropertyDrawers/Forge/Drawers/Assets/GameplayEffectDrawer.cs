using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Extended;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(GameplayEffect))]
    public class GameplayEffectDrawer : AbstractRefDrawer<GameplayEffect>
    {
        // Filter state - persisted per drawer instance
        private bool _showOnlyUnlinked = false;
        
        protected override GameplayEffect[] GetEntries()
        {
            var all = GetAllInstances<GameplayEffect>();
            
            if (_showOnlyUnlinked)
            {
                return all.Where(e => !e.IsLinked).ToArray();
            }
            
            return all;
        }
        
        protected override bool CompareTo(GameplayEffect value, GameplayEffect other)
        {
            return value == other;
        }
        
        protected override string GetStringValue(SerializedProperty prop, GameplayEffect value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        
        protected override void SetValue(SerializedProperty prop, GameplayEffect value)
        {
            prop.objectReferenceValue = value;
        }
        
        protected override GameplayEffect GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as GameplayEffect;
        }
        
        protected override Label GetLabel(SerializedProperty prop, GameplayEffect value)
        {
            if (prop.isArray) return null;
            return new Label(prop.displayName);
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement();
            
            // Try to find the parent ILevelProvider context
            var parentProvider = FindParentLevelProvider(prop);
            var parentAsset = GetParentProviderAsset(prop);
            
            // Check for template attribute
            var templateAttr = GetTemplateAttribute(prop);
            var templateKey = templateAttr?.TemplateKey;
            var hasTemplate = !string.IsNullOrEmpty(templateKey) && EffectTemplateRegistry.HasTemplate(templateKey);
            
            // Main row with picker and action buttons
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            root.Add(mainRow);
            
            // Add the base picker UI
            var baseUI = base.CreatePropertyGUI(prop);
            baseUI.style.flexGrow = 1;
            mainRow.Add(baseUI);
            
            // Action buttons container (toolbar style)
            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.marginLeft = 4;
            mainRow.Add(actionsContainer);
            
            // Get value button reference for updates
            var valueBtn = baseUI.Q<Button>("ValueButton");
            
            // Template import button (only show if template exists and field is empty)
            Button templateBtn = null;
            if (hasTemplate)
            {
                templateBtn = CreateActionButton("📥", $"Import from {templateAttr.DisplayName} template", 
                    new Color(0.4f, 0.5f, 0.6f));
                templateBtn.name = "TemplateBtn";
                templateBtn.clicked += () =>
                {
                    ImportFromTemplate(prop, templateKey, parentProvider, parentAsset, valueBtn, templateBtn);
                };
                actionsContainer.Add(templateBtn);
                
                // Hide if field already has value
                var currentEffect = GetCurrentValue(prop);
                templateBtn.style.display = currentEffect == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Filter toggle button
            var filterBtn = CreateActionButton(_showOnlyUnlinked ? "🔓" : "📋", 
                _showOnlyUnlinked ? "Showing unlinked only - Click to show all" : "Showing all - Click to show unlinked only",
                _showOnlyUnlinked ? new Color(0.4f, 0.5f, 0.3f) : Colors.ButtonBackground);
            filterBtn.clicked += () =>
            {
                _showOnlyUnlinked = !_showOnlyUnlinked;
                filterBtn.text = _showOnlyUnlinked ? "🔓" : "📋";
                filterBtn.tooltip = _showOnlyUnlinked ? "Showing unlinked only - Click to show all" : "Showing all - Click to show unlinked only";
                filterBtn.style.backgroundColor = _showOnlyUnlinked ? new Color(0.4f, 0.5f, 0.3f) : Colors.ButtonBackground;
            };
            actionsContainer.Add(filterBtn);
            
            // Create new effect button (only show if we have a parent provider to link to)
            if (parentProvider != null)
            {
                var createBtn = CreateActionButton("+", $"Create new effect linked to {parentProvider.GetProviderName()}",
                    new Color(0.3f, 0.5f, 0.3f));
                createBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                createBtn.clicked += () =>
                {
                    ShowCreateEffectDialog(prop, parentAsset, parentProvider, templateKey);
                };
                actionsContainer.Add(createBtn);
            }
            
            BuildGoButton(prop, actionsContainer);
            
            // Add link info section
            var linkSection = new VisualElement { name = "LinkSection" };
            root.Add(linkSection);
            
            // Build initial link info
            BuildLinkInfo(linkSection, prop, parentProvider);
            
            // Watch for changes
            root.RegisterCallback<ChangeEvent<UnityEngine.Object>>(evt =>
            {
                if (evt.target == root || root.Contains(evt.target as VisualElement))
                {
                    var newEffect = evt.newValue as GameplayEffect;
                    
                    // Update template button visibility
                    if (templateBtn != null)
                    {
                        templateBtn.style.display = newEffect == null ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                    
                    // Handle effect assignment with linking prompt
                    if (newEffect != null && parentProvider != null)
                    {
                        HandleEffectAssignment(prop, newEffect, parentProvider, parentAsset);
                    }
                    root.schedule.Execute(() =>
                    {
                        BuildGoButton(prop, actionsContainer);
                        BuildLinkInfo(linkSection, prop, parentProvider);
                    });
                }
            });
            
            // Refresh periodically
            root.schedule.Execute(() =>
            {
                BuildLinkInfo(linkSection, prop, parentProvider);
                BuildGoButton(prop, actionsContainer);
                
            }).Every(500);
            
            return root;
        }

        void BuildGoButton(SerializedProperty prop, VisualElement container)
        {
            var btn = container.Q("GoBtn");
            if (btn is not null) container.Remove(btn);
            
            var effect = GetCurrentValue(prop);
            if (effect is not null)
            {
                var goBtn = CreateActionButton(Icons.Arrow, $"Go to \"{effect.GetName()}\"", Colors.AccentBlue);
                goBtn.name = "GoBtn";
                goBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                goBtn.clicked += () =>
                {
                    Selection.activeObject = effect;
                    EditorGUIUtility.PingObject(effect);
                };
                container.Add(goBtn);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Action Button Helper
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateActionButton(string text, string tooltip, Color bgColor)
        {
            var btn = new Button();
            btn.text = text;
            btn.tooltip = tooltip;
            btn.focusable = false;
            btn.style.width = 22;
            btn.style.height = 18;
            btn.style.fontSize = 10;
            btn.style.marginLeft = 2;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = bgColor;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            
            var normalColor = bgColor;
            var hoverColor = new Color(
                Mathf.Min(bgColor.r + 0.1f, 1f),
                Mathf.Min(bgColor.g + 0.1f, 1f),
                Mathf.Min(bgColor.b + 0.1f, 1f),
                bgColor.a
            );
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = hoverColor);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = normalColor);
            
            return btn;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Import
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ImportFromTemplate(SerializedProperty prop, string templateKey, ILevelProvider parentProvider, 
            BaseForgeLinkProvider parentAsset, Button valueBtn, Button templateBtn)
        {
            var template = EffectTemplateRegistry.GetTemplate(templateKey);
            if (template == null)
            {
                EditorUtility.DisplayDialog("Template Not Found", 
                    $"The {templateKey} template is not configured or the asset was deleted.\n\n" +
                    "Configure it in PlayForge Manager > Settings > Assets > Effect Templates.", "OK");
                return;
            }
            
            var displayName = EffectTemplateRegistry.GetDisplayName(templateKey);
            
            // Ask user what to do
            int choice = EditorUtility.DisplayDialogComplex(
                $"Import {displayName}",
                $"How would you like to use the template '{template.GetName()}'?",
                $"Link to {parentProvider.GetProviderName()}",  // 0 - Create a copy and link it
                "Cancel",            // 1
                "Use Template Link"      // 2 - Just assign the template directly
            );
            
            if (choice == 1) return; // Cancel
            
            if (choice == 2)
            {
                // Just assign the template directly
                prop.objectReferenceValue = template;
                prop.serializedObject.ApplyModifiedProperties();
                
                if (valueBtn != null)
                    valueBtn.text = GetStringValue(prop, template);
                if (templateBtn != null)
                    templateBtn.style.display = DisplayStyle.None;
                    
                return;
            }
            
            // choice == 0: Duplicate & Link
            var providerName = parentProvider?.GetProviderName() ?? "Unknown";
            var newName = $"{providerName}_{templateKey}";
            
            // Create duplicate
            var duplicate = ScriptableObject.Instantiate(template);
            
            // Update the name
            if (duplicate.Definition != null)
            {
                duplicate.Definition.Name = newName;
            }
            
            // Link to provider if available
            if (parentAsset != null)
            {
                duplicate.LinkToProvider(parentAsset);
            }
            
            // Save the asset
            var templatePath = AssetDatabase.GetAssetPath(template);
            var directory = Path.GetDirectoryName(templatePath);
            var sanitizedName = SanitizeFileName(newName);
            var newPath = $"{directory}/{sanitizedName}.asset";
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
            
            AssetDatabase.CreateAsset(duplicate, newPath);
            AssetDatabase.SaveAssets();
            
            // Assign to property
            prop.objectReferenceValue = duplicate;
            prop.serializedObject.ApplyModifiedProperties();
            
            if (valueBtn != null)
                valueBtn.text = GetStringValue(prop, duplicate);
            if (templateBtn != null)
                templateBtn.style.display = DisplayStyle.None;
            
            Debug.Log($"[PlayForge] Created {templateKey} effect '{newName}' from template at {newPath}");
            
            // Ping the new asset
            EditorGUIUtility.PingObject(duplicate);
        }
        
        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Effect";
            var sanitized = Regex.Replace(name, @"\s+", "_");
            sanitized = Regex.Replace(sanitized, @"[<>:""/\\|?*]", "");
            return sanitized;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Attribute Detection
        // ═══════════════════════════════════════════════════════════════════════════
        
        private ForgeEffectTemplateAttribute GetTemplateAttribute(SerializedProperty prop)
        {
            return GetFieldAttributes<ForgeEffectTemplateAttribute>(prop).FirstOrDefault();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Create Dialog
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ShowCreateEffectDialog(SerializedProperty prop, BaseForgeLinkProvider parentAsset, 
            ILevelProvider parentProvider, string templateKey)
        {
            var dialog = ScriptableObject.CreateInstance<CreateLinkedEffectDialog>();
            dialog.Initialize(parentAsset, parentProvider, (newEffect) =>
            {
                // Assign the newly created effect to this property
                prop.objectReferenceValue = newEffect;
                prop.serializedObject.ApplyModifiedProperties();
            }, templateKey);
            dialog.ShowUtility();
        }
        
        private ILevelProvider FindParentLevelProvider(SerializedProperty prop)
        {
            var targetObject = prop.serializedObject.targetObject;
            return targetObject as ILevelProvider;
        }
        
        private BaseForgeLinkProvider GetParentProviderAsset(SerializedProperty prop)
        {
            return prop.serializedObject.targetObject as BaseForgeLinkProvider;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Effect Assignment & Linking
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void HandleEffectAssignment(SerializedProperty prop, GameplayEffect effect, ILevelProvider parentProvider, BaseForgeLinkProvider parentAsset)
        {
            if (parentAsset == null) return;
            
            if (effect.IsLinked)
            {
                if (effect.IsLinkedTo(parentAsset))
                {
                    return; // Already linked to this provider
                }
                
                var currentProviderName = effect.LinkedProvider?.GetProviderName() ?? "Unknown";
                var newProviderName = parentProvider.GetProviderName();
                
                var choice = EditorUtility.DisplayDialogComplex(
                    "Effect Already Linked",
                    $"'{effect.GetName()}' is already linked to:\n\n" +
                    $"  {currentProviderName}\n\n" +
                    $"Would you like to re-link it to '{newProviderName}'?",
                    "Re-link",
                    "Keep Current",
                    "Cancel"
                );
                
                switch (choice)
                {
                    case 0: // Re-link
                        effect.LinkToProvider(parentAsset);
                        EditorUtility.SetDirty(effect);
                        AssetDatabase.SaveAssets();
                        break;
                    case 2: // Cancel - clear the assignment
                        prop.objectReferenceValue = null;
                        prop.serializedObject.ApplyModifiedProperties();
                        break;
                    // case 1: Keep current - do nothing
                }
            }
            else
            {
                // Offer to link unlinked effect
                var choice = EditorUtility.DisplayDialog(
                    "Link Effect?",
                    $"Would you like to link '{effect.GetName()}' to '{parentProvider.GetProviderName()}'?\n\n" +
                    $"This will set the effect's level source to this {(parentAsset is Ability ? "Ability" : "Entity")}.",
                    "Link",
                    "Don't Link"
                );
                
                if (choice)
                {
                    effect.LinkToProvider(parentAsset);
                    EditorUtility.SetDirty(effect);
                    AssetDatabase.SaveAssets();
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Link Info Display
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLinkInfo(VisualElement container, SerializedProperty prop, ILevelProvider parentProvider)
        {
            container.Clear();
            
            var effect = GetCurrentValue(prop);
            if (effect == null) return;
            
            var infoRow = new VisualElement();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.alignItems = Align.Center;
            infoRow.style.marginTop = 2;
            infoRow.style.marginLeft = 124; // Align with field
            infoRow.style.paddingLeft = 4;
            container.Add(infoRow);
            
            if (effect.IsLinked)
            {
                var linkedProvider = effect.LinkedProvider;
                
                // Link icon
                var linkIcon = new Label("🔗");
                linkIcon.style.fontSize = 10;
                linkIcon.style.marginRight = 4;
                linkIcon.style.color = Colors.AccentGreen;
                infoRow.Add(linkIcon);
                
                // Provider info
                var providerLabel = new Label(linkedProvider?.GetProviderName() ?? "Unknown");
                providerLabel.style.fontSize = 10;
                providerLabel.style.color = Colors.AccentGreen;
                providerLabel.style.marginRight = 4;
                infoRow.Add(providerLabel);
                
                // Level info
                var levelLabel = new Label($"(Lv {linkedProvider?.GetMaxLevel() ?? 1})");
                levelLabel.style.fontSize = 9;
                levelLabel.style.color = Colors.HintText;
                levelLabel.style.marginRight = 8;
                infoRow.Add(levelLabel);
                
                // Navigate button
                if (linkedProvider != null)
                {
                    var navBtn = new Button(() =>
                    {
                        Selection.activeObject = linkedProvider;
                        EditorGUIUtility.PingObject(linkedProvider);
                    });
                    navBtn.text = Icons.Arrow;
                    navBtn.tooltip = "Go to linked provider";
                    navBtn.style.width = 18;
                    navBtn.style.height = 14;
                    navBtn.style.fontSize = 9;
                    navBtn.style.paddingLeft = 0;
                    navBtn.style.paddingRight = 0;
                    navBtn.style.paddingTop = 0;
                    navBtn.style.paddingBottom = 0;
                    navBtn.style.backgroundColor = Colors.ButtonBackground;
                    navBtn.style.borderTopLeftRadius = 2;
                    navBtn.style.borderTopRightRadius = 2;
                    navBtn.style.borderBottomLeftRadius = 2;
                    navBtn.style.borderBottomRightRadius = 2;
                    infoRow.Add(navBtn);
                }
            }
            else
            {
                // Not linked warning
                var warningIcon = new Label("⚠");
                warningIcon.style.fontSize = 10;
                warningIcon.style.marginRight = 4;
                warningIcon.style.color = Colors.AccentYellow;
                infoRow.Add(warningIcon);
                
                var warningLabel = new Label("Not linked");
                warningLabel.style.fontSize = 10;
                warningLabel.style.color = Colors.AccentYellow;
                warningLabel.style.marginRight = 8;
                infoRow.Add(warningLabel);
                
                // Offer to link if parent provider exists
                if (parentProvider != null)
                {
                    var linkBtn = new Button(() =>
                    {
                        var parentAsset = GetParentProviderAsset(prop);
                        if (parentAsset != null)
                        {
                            effect.LinkToProvider(parentAsset);
                            EditorUtility.SetDirty(effect);
                            AssetDatabase.SaveAssets();
                            BuildLinkInfo(container, prop, parentProvider);
                        }
                    });
                    linkBtn.text = "Link";
                    linkBtn.tooltip = $"Link to {parentProvider.GetProviderName()}";
                    linkBtn.style.height = 14;
                    linkBtn.style.fontSize = 9;
                    linkBtn.style.paddingLeft = 4;
                    linkBtn.style.paddingRight = 4;
                    linkBtn.style.paddingTop = 0;
                    linkBtn.style.paddingBottom = 0;
                    linkBtn.style.backgroundColor = new Color(0.3f, 0.4f, 0.3f);
                    linkBtn.style.borderTopLeftRadius = 2;
                    linkBtn.style.borderTopRightRadius = 2;
                    linkBtn.style.borderBottomLeftRadius = 2;
                    linkBtn.style.borderBottomRightRadius = 2;
                    infoRow.Add(linkBtn);
                }
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Create Linked Effect Dialog
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class CreateLinkedEffectDialog : EditorWindow
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        
        private BaseForgeLinkProvider _linkTarget;
        private ILevelProvider _provider;
        private Action<GameplayEffect> _onCreated;
        private string _templateKey;
        
        private string _effectName = "";
        private Label _previewLabel;
        private bool _useTemplate = false;
        private GameplayEffect _template;
        
        public void Initialize(BaseForgeLinkProvider linkTarget, ILevelProvider provider, Action<GameplayEffect> onCreated, string templateKey = null)
        {
            _linkTarget = linkTarget;
            _provider = provider;
            _onCreated = onCreated;
            _templateKey = templateKey;
            
            if (!string.IsNullOrEmpty(_templateKey))
            {
                _template = EffectTemplateRegistry.GetTemplate(_templateKey);
            }
            
            titleContent = new GUIContent("Create Linked Effect");
            minSize = maxSize = new Vector2(380, _template != null ? 240 : 200);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            
            // Header
            var header = new Label("Create New Effect");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.HeaderText;
            header.style.marginBottom = 12;
            root.Add(header);
            
            // Link preview
            var providerType = _linkTarget is Ability ? "Ability" : "Entity";
            var providerColor = _linkTarget is Ability ? Colors.AccentOrange : Colors.AccentPurple;
            
            var linkBox = new VisualElement();
            linkBox.style.flexDirection = FlexDirection.Row;
            linkBox.style.alignItems = Align.Center;
            linkBox.style.paddingLeft = 8;
            linkBox.style.paddingRight = 8;
            linkBox.style.paddingTop = 6;
            linkBox.style.paddingBottom = 6;
            linkBox.style.marginBottom = 12;
            linkBox.style.backgroundColor = new Color(0.2f, 0.25f, 0.2f, 0.5f);
            linkBox.style.borderTopLeftRadius = 4;
            linkBox.style.borderTopRightRadius = 4;
            linkBox.style.borderBottomLeftRadius = 4;
            linkBox.style.borderBottomRightRadius = 4;
            linkBox.style.borderLeftWidth = 3;
            linkBox.style.borderLeftColor = Colors.AccentGreen;
            root.Add(linkBox);
            
            var linkIcon = new Label("🔗");
            linkIcon.style.marginRight = 6;
            linkBox.Add(linkIcon);
            
            var linkText = new Label($"Will link to: ");
            linkText.style.color = Colors.LabelText;
            linkText.style.fontSize = 11;
            linkBox.Add(linkText);
            
            var providerBadge = new Label(providerType);
            providerBadge.style.fontSize = 9;
            providerBadge.style.color = providerColor;
            providerBadge.style.backgroundColor = new Color(providerColor.r, providerColor.g, providerColor.b, 0.2f);
            providerBadge.style.paddingLeft = 4;
            providerBadge.style.paddingRight = 4;
            providerBadge.style.paddingTop = 2;
            providerBadge.style.paddingBottom = 2;
            providerBadge.style.borderTopLeftRadius = 3;
            providerBadge.style.borderTopRightRadius = 3;
            providerBadge.style.borderBottomLeftRadius = 3;
            providerBadge.style.borderBottomRightRadius = 3;
            providerBadge.style.marginRight = 4;
            linkBox.Add(providerBadge);
            
            var providerName = new Label(_provider.GetProviderName());
            providerName.style.fontSize = 11;
            providerName.style.color = Colors.HeaderText;
            providerName.style.unityFontStyleAndWeight = FontStyle.Bold;
            linkBox.Add(providerName);
            
            var levelLabel = new Label($" (Max Lv{_provider.GetMaxLevel()})");
            levelLabel.style.fontSize = 10;
            levelLabel.style.color = Colors.AccentGreen;
            linkBox.Add(levelLabel);
            
            // Template option (if template exists)
            if (_template != null)
            {
                var templateRow = new VisualElement();
                templateRow.style.flexDirection = FlexDirection.Row;
                templateRow.style.alignItems = Align.Center;
                templateRow.style.marginBottom = 8;
                root.Add(templateRow);
                
                var templateToggle = new Toggle("Use template:");
                templateToggle.value = _useTemplate;
                templateToggle.style.marginRight = 8;
                templateToggle.RegisterValueChangedCallback(evt => _useTemplate = evt.newValue);
                templateRow.Add(templateToggle);
                
                var templateLabel = new Label(_template.GetName());
                templateLabel.style.fontSize = 10;
                templateLabel.style.color = Colors.AccentCyan;
                templateLabel.style.backgroundColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.15f);
                templateLabel.style.paddingLeft = 4;
                templateLabel.style.paddingRight = 4;
                templateLabel.style.paddingTop = 2;
                templateLabel.style.paddingBottom = 2;
                templateLabel.style.borderTopLeftRadius = 3;
                templateLabel.style.borderTopRightRadius = 3;
                templateLabel.style.borderBottomLeftRadius = 3;
                templateLabel.style.borderBottomRightRadius = 3;
                templateRow.Add(templateLabel);
            }
            
            // Name input
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 4;
            root.Add(nameRow);
            
            var nameLabel = new Label("Effect Name");
            nameLabel.style.width = 80;
            nameRow.Add(nameLabel);
            
            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.RegisterValueChangedCallback(evt =>
            {
                _effectName = evt.newValue;
                UpdatePreview();
            });
            nameRow.Add(nameField);
            
            // File preview
            _previewLabel = new Label();
            _previewLabel.style.fontSize = 10;
            _previewLabel.style.color = Colors.HintText;
            _previewLabel.style.marginLeft = 80;
            _previewLabel.style.marginBottom = 8;
            root.Add(_previewLabel);
            UpdatePreview();
            
            // Path info
            var pathLabel = new Label($"Path: {GetEffectPath()}/");
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = Colors.HintText;
            pathLabel.style.marginBottom = 16;
            root.Add(pathLabel);
            
            // Buttons
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            root.Add(btnRow);
            
            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);
            
            var createBtn = new Button(CreateEffect) { text = "Create & Link" };
            createBtn.style.backgroundColor = Colors.AccentGreen;
            btnRow.Add(createBtn);
            
            nameField.Focus();
        }
        
        private void UpdatePreview()
        {
            if (_previewLabel == null) return;
            
            if (string.IsNullOrEmpty(_effectName))
            {
                _previewLabel.text = "File: (enter name)";
            }
            else
            {
                var fileName = GetSanitizedFileName(_effectName);
                _previewLabel.text = $"File: {fileName}.asset";
            }
        }
        
        private string GetSanitizedFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            
            var sanitized = Regex.Replace(name, @"\s+", "");
            foreach (var c in InvalidFileNameChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }
            sanitized = Regex.Replace(sanitized, @"[<>:""/\\|?*]", "");
            
            // Get prefix/suffix from prefs
            var prefix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Prefix_GameplayEffect", "Effect_");
            var suffix = EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Postfix_GameplayEffect", "");
            
            return $"{prefix}{sanitized}{suffix}";
        }
        
        private string GetEffectPath()
        {
            return EditorPrefs.GetString(PlayForgeManager.PREFS_PREFIX + "Path_GameplayEffect", "Assets/Data/Effects");
        }
        
        private void CreateEffect()
        {
            if (string.IsNullOrEmpty(_effectName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name for the effect.", "OK");
                return;
            }
            
            var sanitizedFileName = GetSanitizedFileName(_effectName);
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                EditorUtility.DisplayDialog("Error", "The name contains only invalid characters.", "OK");
                return;
            }
            
            var path = GetEffectPath();
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
            
            // Create the effect (optionally from template)
            GameplayEffect effect;
            if (_useTemplate && _template != null)
            {
                effect = Instantiate(_template);
            }
            else
            {
                effect = CreateInstance<GameplayEffect>();
            }
            
            // Set the name
            effect.Definition = new GameplayEffectDefinition
            {
                Name = _effectName,
                Description = "",
                Textures = new System.Collections.Generic.List<TextureItem>()
            };
            
            // Link to the provider
            effect.LinkToProvider(_linkTarget);
            
            // Save the asset
            var fullPath = $"{path}/{sanitizedFileName}.asset";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(effect, fullPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[PlayForge] Created effect '{_effectName}' linked to {_provider.GetProviderName()} at {fullPath}");
            
            // Callback
            _onCreated?.Invoke(effect);
            
            // Select and ping
            Selection.activeObject = effect;
            EditorGUIUtility.PingObject(effect);
            
            Close();
        }
    }
}