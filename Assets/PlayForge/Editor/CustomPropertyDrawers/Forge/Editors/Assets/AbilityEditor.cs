using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{ 
    [CustomEditor(typeof(Ability))]
    public class AbilityEditor : BasePlayForgeEditor
    {
        // Icon asset (assign in Inspector)
        [SerializeField] private Texture2D m_AbilityIcon;
        
        private Ability ability;
        
        // UI References for updates
        private Label assetTagValueLabel;
        private VisualElement levelSourceContent;
        private VisualElement levelingContent;
        
        // For object picker handling - using polling approach for reliability
        private int _pickerControlId;
        private bool _waitingForPicker;
        private Object _lastPickedObject;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(ability?.Definition.Name) ? ability.Definition.Name : "Unnamed Ability";
        }
        
        protected override string GetDisplayDescription()
        {
            if (ability == null) return "";
            var desc = ability.Definition.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            // First try to get from textures list
            if (ability?.Definition.Textures != null && ability.Definition.Textures.Count > 0)
            {
                var tex = ability.Definition.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_AbilityIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ABILITY";
        
        protected override Color GetAssetTypeColor() => new Color(0.4f, 0.7f, 1f); // Light blue
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/abilities";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        protected override void OnVisualize()
        {
            // TODO: Open ability visualizer
            Debug.Log($"Visualize ability: {ability.name}");
        }
        
        protected override void OnImport()
        {
            // Open asset picker for abilities
            var currentPath = AssetDatabase.GetAssetPath(ability);
            EditorGUIUtility.ShowObjectPicker<Ability>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _waitingForPicker = false;
        }
        
        /// <summary>
        /// Polls for object picker completion since UIElements doesn't reliably receive IMGUI events.
        /// </summary>
        private void OnEditorUpdate()
        {
            if (!_waitingForPicker) return;
            
            // Check if our picker is still active
            var currentControlId = EditorGUIUtility.GetObjectPickerControlID();
            
            if (currentControlId == 0)
            {
                // Picker closed - apply the last tracked selection
                _waitingForPicker = false;
                
                if (_lastPickedObject is BaseForgeLinkProvider provider)
                {
                    ability.LinkedProvider = provider;
                    serializedObject.Update();
                    MarkDirty(ability);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    Repaint();
                }
                _lastPickedObject = null;
            }
            else if (currentControlId == _pickerControlId)
            {
                // Picker still open - track current selection for when it closes
                _lastPickedObject = EditorGUIUtility.GetObjectPickerObject();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            ability = serializedObject.targetObject as Ability;
            if (ability == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // ScrollView for all sections (header is in OnHeaderGUI)
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            // Build all sections inside ScrollView
            BuildDefinitionSection(scrollView);
            BuildLevelSourceSection(scrollView);
            BuildTagsSection(scrollView);
            BuildRuntimeSection(scrollView);
            BuildLevelingSection(scrollView);
            
            BuildValidationSection(scrollView);
            BuildWorkersSection(scrollView);
            BuildLocalDataSection(scrollView);
            
            // Bottom padding
            scrollView.Add(CreateBottomPadding());
            
            // Bind all properties
            root.Bind(serializedObject);
            
            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Definition Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildDefinitionSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Definition",
                Title = "Definition",
                AccentColor = Colors.AccentGray,
                HelpUrl = "https://docs.playforge.dev/abilities/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Name field
            var nameField = CreateTextField("Name", "Name");
            nameField.value = ability.Definition.Name;
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ability.Definition.Name = nameField.value;
                UpdateAssetTagDisplay();
                MarkDirty(ability);
                Repaint(); // Refresh IMGUI header
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 24);
            descField.value = ability.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ability.Definition.Description = descField.value;
                MarkDirty(ability);
                Repaint(); // Refresh IMGUI header
            });
            content.Add(descField);
            
            // Activation Policy
            var activationPolicy = CreateEnumField("ActivationPolicy", "Activation Policy", ability.Definition.ActivationPolicy);
            activationPolicy.RegisterValueChangedCallback(evt =>
            {
                ability.Definition.ActivationPolicy = (EAbilityActivationPolicyExtended)evt.newValue;
                MarkDirty(ability);
            });
            content.Add(activationPolicy);
            
            // Activate Immediately
            var activateImmediately = CreateToggle("ActivateImmediately", "Activate Immediately");
            activateImmediately.value = ability.Definition.ActivateImmediately;
            activateImmediately.RegisterValueChangedCallback(evt =>
            {
                ability.Definition.ActivateImmediately = evt.newValue;
                MarkDirty(ability);
            });
            content.Add(activateImmediately);
            
            // Icons Subsection
            var iconsSubsection = CreateSubsection("IconsSubsection", "Icons", Colors.AccentBlue);
            content.Add(iconsSubsection);
            
            var iconsRow = CreateRow(4, wrap: true);
            iconsSubsection.Add(iconsRow);
            
            var defProp = serializedObject.FindProperty("Definition");
            var iconsProp = CreatePropertyField(defProp.FindPropertyRelative("Textures"), "Textures", "");
            iconsProp.style.flexGrow = 1;
            iconsProp.RegisterCallback<SerializedPropertyChangeEvent>(_ => Repaint());
            iconsSubsection.Add(iconsProp);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                AccentColor = Colors.AccentGreen,
                HelpUrl = "https://docs.playforge.dev/abilities/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tagsProp = serializedObject.FindProperty("Tags");
            
            // Asset Tag Display (read-only)
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            // Context Tags
            content.Add(CreatePropertyField(tagsProp.FindPropertyRelative("ContextTags"), "ContextTags", "Context Tags"));
            
            // Granted Tags Subsection
            var grantedSubsection = CreateSubsection("GrantedTagsSubsection", "Granted Tags", Colors.AccentGreen);
            content.Add(grantedSubsection);
            grantedSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("PassiveGrantedTags"), "PassiveTags", "Passive Tags"));
            grantedSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("ActiveGrantedTags"), "ActiveTags", "Active Tags"));
            
            // Tag Requirements - uses AbilityTagRequirements drawer
            var tagReqProp = tagsProp.FindPropertyRelative("TagRequirements");
            if (tagReqProp != null)
            {
                var tagReqField = new PropertyField(tagReqProp, "");
                tagReqField.style.marginTop = 8;
                tagReqField.BindProperty(tagReqProp);
                content.Add(tagReqField);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Runtime Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildRuntimeSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Runtime",
                Title = "Runtime Behaviour",
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/abilities/runtime"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var proxyProp = serializedObject.FindProperty("Behaviour");
            
            // Target Mode
            var targetMode = CreatePropertyField(proxyProp.FindPropertyRelative("Targeting"), "Targeting", "Targeting");
            content.Add(targetMode);
            
            // Use Implicit Targeting
            content.Add(CreateHintLabel("Automatically set Source as Target?"));
            var useImplicit = CreateToggle("UseImplicitTargeting", "Use Implicit Targeting");
            useImplicit.BindProperty(proxyProp.FindPropertyRelative("UseImplicitTargeting"));
            content.Add(useImplicit);
            
            // Stages
            content.Add(CreatePropertyField(proxyProp.FindPropertyRelative("Stages"), "Stages", "Stages"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                AccentColor = Colors.AccentPurple,
                HelpUrl = "https://docs.playforge.dev/abilities/leveling"
            });
            parent.Add(section.Section);
            
            levelingContent = section.Content;
            RebuildLevelingContent();
        }
        
        private void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            
            levelingContent.Clear();
            
            // Determine which state we're in:
            // 1. Standalone mode - show editable level fields
            // 2. LinkedToProvider mode with no provider - show disabled level fields + warning
            // 3. LinkedToProvider mode with provider - show read-only derived values
            
            if (ability.LinkMode == EAbilityLinkMode.LinkedToProvider)
            {
                if (ability.LinkedProvider != null)
                {
                    // Case 3: Fully linked - show read-only derived values
                    BuildLinkedLevelingDisplay();
                }
                else
                {
                    // Case 2: Link mode selected but no provider yet - show disabled fields + warning
                    BuildNoProviderSelectedDisplay();
                }
            }
            else
            {
                // Case 1: Standalone mode - show editable fields
                BuildStandaloneLevelingFields();
            }
            
            // Cost & Cooldown Subsection (always shown)
            BuildCostCooldownSubsection();
            
            // Re-bind the content after rebuilding to ensure PropertyFields work
            levelingContent.Bind(serializedObject);
        }
        
        private void BuildLinkedLevelingDisplay()
        {
            var provider = ability.LinkedProvider;
            
            // Info box showing levels are derived from provider
            var linkedBox = new VisualElement();
            linkedBox.style.backgroundColor = new Color(0.2f, 0.25f, 0.3f, 0.4f);
            linkedBox.style.borderTopLeftRadius = 4;
            linkedBox.style.borderTopRightRadius = 4;
            linkedBox.style.borderBottomLeftRadius = 4;
            linkedBox.style.borderBottomRightRadius = 4;
            linkedBox.style.borderLeftWidth = 3;
            linkedBox.style.borderLeftColor = Colors.AccentBlue;
            linkedBox.style.paddingLeft = 8;
            linkedBox.style.paddingRight = 8;
            linkedBox.style.paddingTop = 8;
            linkedBox.style.paddingBottom = 8;
            linkedBox.style.marginBottom = 8;
            levelingContent.Add(linkedBox);
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;
            
            var linkIcon = new Label("🔗");
            linkIcon.style.marginRight = 6;
            headerRow.Add(linkIcon);
            
            var headerLabel = new Label("Levels Derived from Provider");
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.color = Colors.AccentBlue;
            headerRow.Add(headerLabel);
            
            linkedBox.Add(headerRow);
            
            // Provider info
            var providerName = provider.GetProviderName();
            var typeName = provider is Item ? "Item" : "Provider";
            var typeColor = provider is Item ? new Color(1f, 0.8f, 0.3f) : Colors.AccentPurple;
            
            var sourceRow = new VisualElement();
            sourceRow.style.flexDirection = FlexDirection.Row;
            sourceRow.style.marginBottom = 4;
            
            var sourceLabel = new Label("Source:");
            sourceLabel.style.width = 100;
            sourceLabel.style.fontSize = 11;
            sourceLabel.style.color = Colors.HintText;
            sourceRow.Add(sourceLabel);
            
            var sourceValue = new Label($"{providerName} ({typeName})");
            sourceValue.style.fontSize = 11;
            sourceValue.style.color = typeColor;
            sourceRow.Add(sourceValue);
            
            linkedBox.Add(sourceRow);
            
            // Level display row
            var levelRow = CreateRow(4);
            levelRow.style.marginTop = 8;
            linkedBox.Add(levelRow);
            
            // Starting Level (read-only)
            var startingLevelContainer = new VisualElement();
            startingLevelContainer.style.flexGrow = 1;
            startingLevelContainer.style.marginRight = 8;
            levelRow.Add(startingLevelContainer);
            
            var startingLevelRow = new VisualElement();
            startingLevelRow.style.flexDirection = FlexDirection.Row;
            startingLevelRow.style.alignItems = Align.Center;
            startingLevelContainer.Add(startingLevelRow);
            
            var startingLevelLabel = new Label("Starting Level");
            startingLevelLabel.style.width = 90;
            startingLevelLabel.style.fontSize = 11;
            startingLevelLabel.style.color = Colors.LabelText;
            startingLevelRow.Add(startingLevelLabel);
            
            var startingLevelValue = new IntegerField();
            startingLevelValue.value = provider.GetStartingLevel();
            startingLevelValue.SetEnabled(false);
            startingLevelValue.style.flexGrow = 1;
            startingLevelValue.style.opacity = 0.8f;
            startingLevelRow.Add(startingLevelValue);
            
            // Max Level (read-only)
            var maxLevelContainer = new VisualElement();
            maxLevelContainer.style.flexGrow = 1;
            levelRow.Add(maxLevelContainer);
            
            var maxLevelRow = new VisualElement();
            maxLevelRow.style.flexDirection = FlexDirection.Row;
            maxLevelRow.style.alignItems = Align.Center;
            maxLevelContainer.Add(maxLevelRow);
            
            var maxLevelLabel = new Label("Max Level");
            maxLevelLabel.style.width = 70;
            maxLevelLabel.style.fontSize = 11;
            maxLevelLabel.style.color = Colors.LabelText;
            maxLevelRow.Add(maxLevelLabel);
            
            var maxLevelValue = new IntegerField();
            maxLevelValue.value = provider.GetMaxLevel();
            maxLevelValue.SetEnabled(false);
            maxLevelValue.style.flexGrow = 1;
            maxLevelValue.style.opacity = 0.8f;
            maxLevelRow.Add(maxLevelValue);
            
            // Hint text
            var hintLabel = CreateHintLabel("These values are controlled by the linked provider. Unlink to edit manually.");
            hintLabel.style.marginTop = 8;
            linkedBox.Add(hintLabel);
            
            // Ignore When Level Zero (still editable)
            var ignoreZero = CreateToggle("IgnoreWhenLevelZero", "Ignore When Level Zero");
            ignoreZero.BindProperty(serializedObject.FindProperty("IgnoreWhenLevelZero"));
            levelingContent.Add(ignoreZero);
        }
        
        private void BuildNoProviderSelectedDisplay()
        {
            // Warning box - linked mode but no provider selected
            var warningBox = new VisualElement();
            warningBox.style.backgroundColor = new Color(0.3f, 0.25f, 0.2f, 0.3f);
            warningBox.style.borderTopLeftRadius = 4;
            warningBox.style.borderTopRightRadius = 4;
            warningBox.style.borderBottomLeftRadius = 4;
            warningBox.style.borderBottomRightRadius = 4;
            warningBox.style.borderLeftWidth = 3;
            warningBox.style.borderLeftColor = Colors.AccentYellow;
            warningBox.style.paddingLeft = 8;
            warningBox.style.paddingRight = 8;
            warningBox.style.paddingTop = 8;
            warningBox.style.paddingBottom = 8;
            warningBox.style.marginBottom = 8;
            levelingContent.Add(warningBox);
            
            // Warning header
            var warningRow = new VisualElement();
            warningRow.style.flexDirection = FlexDirection.Row;
            warningRow.style.alignItems = Align.Center;
            warningRow.style.marginBottom = 8;
            
            var warningIcon = new Label("⚠");
            warningIcon.style.color = Colors.AccentYellow;
            warningIcon.style.marginRight = 6;
            warningRow.Add(warningIcon);
            
            var warningText = new Label("No Level Provider Selected");
            warningText.style.unityFontStyleAndWeight = FontStyle.Bold;
            warningText.style.color = Colors.AccentYellow;
            warningRow.Add(warningText);
            
            warningBox.Add(warningRow);
            
            var hintLabel = CreateHintLabel("Select an Item in the Level Source section above to link this ability's levels.");
            warningBox.Add(hintLabel);
            
            // Level display row (disabled fields showing ability's own values)
            var levelRow = CreateRow(4);
            levelRow.style.marginTop = 8;
            warningBox.Add(levelRow);
            
            // Starting Level (disabled)
            var startingLevelContainer = new VisualElement();
            startingLevelContainer.style.flexGrow = 1;
            startingLevelContainer.style.marginRight = 8;
            levelRow.Add(startingLevelContainer);
            
            var startingLevelRow = new VisualElement();
            startingLevelRow.style.flexDirection = FlexDirection.Row;
            startingLevelRow.style.alignItems = Align.Center;
            startingLevelContainer.Add(startingLevelRow);
            
            var startingLevelLabel = new Label("Starting Level");
            startingLevelLabel.style.width = 90;
            startingLevelLabel.style.fontSize = 11;
            startingLevelLabel.style.color = Colors.HintText;
            startingLevelRow.Add(startingLevelLabel);
            
            var startingLevelValue = new IntegerField();
            startingLevelValue.value = ability.StartingLevel;
            startingLevelValue.SetEnabled(false);
            startingLevelValue.style.flexGrow = 1;
            startingLevelValue.style.opacity = 0.6f;
            startingLevelRow.Add(startingLevelValue);
            
            // Max Level (disabled)
            var maxLevelContainer = new VisualElement();
            maxLevelContainer.style.flexGrow = 1;
            levelRow.Add(maxLevelContainer);
            
            var maxLevelRow = new VisualElement();
            maxLevelRow.style.flexDirection = FlexDirection.Row;
            maxLevelRow.style.alignItems = Align.Center;
            maxLevelContainer.Add(maxLevelRow);
            
            var maxLevelLabel = new Label("Max Level");
            maxLevelLabel.style.width = 70;
            maxLevelLabel.style.fontSize = 11;
            maxLevelLabel.style.color = Colors.HintText;
            maxLevelRow.Add(maxLevelLabel);
            
            var maxLevelValue = new IntegerField();
            maxLevelValue.value = ability.MaxLevel;
            maxLevelValue.SetEnabled(false);
            maxLevelValue.style.flexGrow = 1;
            maxLevelValue.style.opacity = 0.6f;
            maxLevelRow.Add(maxLevelValue);
            
            // Ignore When Level Zero (still editable)
            var ignoreZero = CreateToggle("IgnoreWhenLevelZero", "Ignore When Level Zero");
            ignoreZero.BindProperty(serializedObject.FindProperty("IgnoreWhenLevelZero"));
            levelingContent.Add(ignoreZero);
        }
        
        private void BuildStandaloneLevelingFields()
        {
            // Level row with editable fields
            var levelRow = CreateRow(4);
            levelingContent.Add(levelRow);
            
            var startingLevel = CreatePropertyField(serializedObject.FindProperty("StartingLevel"), "Level", "Starting Level");
            startingLevel.style.flexGrow = 1;
            startingLevel.style.marginRight = 8;
            levelRow.Add(startingLevel);
            
            var maxLevel = CreatePropertyField(serializedObject.FindProperty("MaxLevel"), "MaxLevel", "Max Level");
            maxLevel.style.flexGrow = 1;
            levelRow.Add(maxLevel);
            
            // Ignore When Level Zero
            var ignoreZero = CreateToggle("IgnoreWhenLevelZero", "Ignore When Level Zero");
            ignoreZero.BindProperty(serializedObject.FindProperty("IgnoreWhenLevelZero"));
            levelingContent.Add(ignoreZero);
        }
        
        private void BuildCostCooldownSubsection()
        {
            var costSubsection = CreateSubsection("CostCooldownSubsection", "", Colors.AccentOrange);
            levelingContent.Add(costSubsection);
            
            costSubsection.Add(CreateHintLabel("Ability Cost"));
            var costField = CreatePropertyField(serializedObject.FindProperty("Cost"), "Cost", "");
            costSubsection.Add(costField);
            
            costSubsection.Add(CreateHintLabel("Ability Cooldown"));
            var cooldownField = CreatePropertyField(serializedObject.FindProperty("Cooldown"), "Cooldown", "");
            costSubsection.Add(cooldownField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Source Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "LevelSource",
                Title = "Level Source",
                AccentColor = Colors.AccentGray,
                HelpUrl = "https://docs.playforge.dev/abilities/level-source"
            });
            parent.Add(section.Section);
            
            levelSourceContent = section.Content;
            RebuildLevelSourceContent();
        }
        
        private void RebuildLevelSourceContent()
        {
            levelSourceContent.Clear();
            
            var infoLabel = CreateHintLabel(
                "Link this ability to an Item to derive max level from the item instead of using its own level settings.");
            infoLabel.style.marginBottom = 8;
            levelSourceContent.Add(infoLabel);
            
            var linkModeField = new EnumField("Link Mode", ability.LinkMode);
            linkModeField.style.marginBottom = 6;
            linkModeField.RegisterValueChangedCallback(evt =>
            {
                ability.LinkMode = (EAbilityLinkMode)evt.newValue;
                serializedObject.Update();
                MarkDirty(ability);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            levelSourceContent.Add(linkModeField);
            
            if (ability.LinkMode == EAbilityLinkMode.LinkedToProvider)
            {
                BuildProviderSelector();
                BuildLinkStatusDisplay();
            }
            else
            {
                var standaloneInfo = new VisualElement();
                standaloneInfo.style.flexDirection = FlexDirection.Row;
                standaloneInfo.style.alignItems = Align.Center;
                standaloneInfo.style.paddingLeft = 8;
                standaloneInfo.style.paddingTop = 6;
                standaloneInfo.style.paddingBottom = 6;
                standaloneInfo.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f, 0.4f);
                standaloneInfo.style.borderTopLeftRadius = 3;
                standaloneInfo.style.borderTopRightRadius = 3;
                standaloneInfo.style.borderBottomLeftRadius = 3;
                standaloneInfo.style.borderBottomRightRadius = 3;
                
                var icon = new Label("○");
                icon.style.color = Colors.HintText;
                icon.style.marginRight = 6;
                standaloneInfo.Add(icon);
                
                var text = new Label("Ability operates independently with its own level tracking.");
                text.style.fontSize = 10;
                text.style.color = Colors.HintText;
                text.style.unityFontStyleAndWeight = FontStyle.Italic;
                standaloneInfo.Add(text);
                
                levelSourceContent.Add(standaloneInfo);
            }
            
            // Re-bind after rebuilding
            levelSourceContent.Bind(serializedObject);
        }
        
        private void BuildProviderSelector()
        {
            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;
            selectorRow.style.marginBottom = 6;
            
            var label = new Label("Level Provider");
            label.style.width = 100;
            label.style.color = Colors.LabelText;
            selectorRow.Add(label);
            
            var objectField = new ObjectField
            {
                objectType = typeof(BaseForgeLinkProvider),
                allowSceneObjects = false,
                value = ability.LinkedProvider
            };
            objectField.style.flexGrow = 1;
            
            objectField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue as BaseForgeLinkProvider;
                ability.LinkedProvider = newValue;
                serializedObject.Update();
                MarkDirty(ability);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            
            selectorRow.Add(objectField);
            
            var clearBtn = new Button(() =>
            {
                ability.LinkedProvider = null;
                serializedObject.Update();
                MarkDirty(ability);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            clearBtn.text = "×";
            clearBtn.tooltip = "Clear linked provider";
            clearBtn.style.width = 22;
            clearBtn.style.height = 18;
            clearBtn.style.marginLeft = 4;
            clearBtn.style.paddingLeft = 0;
            clearBtn.style.paddingRight = 0;
            clearBtn.style.fontSize = 14;
            ApplyButtonHoverStyle(clearBtn);
            selectorRow.Add(clearBtn);
            
            levelSourceContent.Add(selectorRow);
            
            // Quick select button for Items
            var quickSelectRow = new VisualElement();
            quickSelectRow.style.flexDirection = FlexDirection.Row;
            quickSelectRow.style.marginTop = 4;
            quickSelectRow.style.marginLeft = 100;
            
            var selectItemBtn = new Button(() => ShowProviderPicker<Item>());
            selectItemBtn.text = "Select Item...";
            selectItemBtn.style.fontSize = 10;
            selectItemBtn.style.height = 18;
            ApplyButtonHoverStyle(selectItemBtn);
            quickSelectRow.Add(selectItemBtn);
            
            levelSourceContent.Add(quickSelectRow);
        }
        
        private void ShowProviderPicker<T>() where T : BaseForgeLinkProvider
        {
            // Generate a unique control ID
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive) + 10000 + Random.Range(1, 1000);
            _waitingForPicker = true;
            _lastPickedObject = ability.LinkedProvider; // Start with current value
            EditorGUIUtility.ShowObjectPicker<T>(ability.LinkedProvider as T, false, "", _pickerControlId);
        }
        
        private void BuildLinkStatusDisplay()
        {
            var statusBox = new VisualElement();
            statusBox.style.marginTop = 8;
            statusBox.style.paddingLeft = 8;
            statusBox.style.paddingRight = 8;
            statusBox.style.paddingTop = 8;
            statusBox.style.paddingBottom = 8;
            statusBox.style.borderTopLeftRadius = 4;
            statusBox.style.borderTopRightRadius = 4;
            statusBox.style.borderBottomLeftRadius = 4;
            statusBox.style.borderBottomRightRadius = 4;
            statusBox.style.borderLeftWidth = 3;
            
            if (ability.IsLinked)
            {
                var provider = ability.LinkedProvider;
                var providerAsset = ability.LinkedProvider;
                
                statusBox.style.backgroundColor = new Color(0.2f, 0.3f, 0.2f, 0.3f);
                statusBox.style.borderLeftColor = Colors.AccentGreen;
                
                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.alignItems = Align.Center;
                headerRow.style.marginBottom = 6;
                
                var linkIcon = new Label("🔗");
                linkIcon.style.marginRight = 6;
                headerRow.Add(linkIcon);
                
                var linkedLabel = new Label("Linked to Level Provider");
                linkedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                linkedLabel.style.color = Colors.AccentGreen;
                headerRow.Add(linkedLabel);
                
                statusBox.Add(headerRow);
                
                var infoGrid = new VisualElement();
                infoGrid.style.marginLeft = 4;
                
                var typeName = providerAsset is Item ? "Item" : "Provider";
                var typeColor = providerAsset is Item ? new Color(1f, 0.8f, 0.3f) : Colors.AccentPurple;
                
                infoGrid.Add(CreateInfoRow("Type:", typeName, typeColor));
                infoGrid.Add(CreateInfoRow("Name:", provider.GetProviderName(), Colors.LabelText));
                infoGrid.Add(CreateInfoRow("Start Level:", provider.GetStartingLevel().ToString(), Colors.LabelText));
                infoGrid.Add(CreateInfoRow("Max Level:", provider.GetMaxLevel().ToString(), Colors.AccentGreen));
                
                statusBox.Add(infoGrid);
                
                var actionsRow = new VisualElement();
                actionsRow.style.flexDirection = FlexDirection.Row;
                actionsRow.style.marginTop = 8;
                actionsRow.style.justifyContent = Justify.FlexEnd;
                
                var gotoBtn = new Button(() =>
                {
                    Selection.activeObject = providerAsset;
                    EditorGUIUtility.PingObject(providerAsset);
                });
                gotoBtn.text = "Go to Provider";
                gotoBtn.style.fontSize = 10;
                gotoBtn.style.height = 20;
                ApplyButtonHoverStyle(gotoBtn);
                actionsRow.Add(gotoBtn);
                
                var unlinkBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog(
                        "Unlink Ability",
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe ability will become standalone.",
                        "Unlink", "Cancel"))
                    {
                        ability.Unlink();
                        serializedObject.Update();
                        MarkDirty(ability);
                        RebuildLevelSourceContent();
                        RebuildLevelingContent();
                        Repaint();
                    }
                });
                unlinkBtn.text = "Unlink";
                unlinkBtn.style.fontSize = 10;
                unlinkBtn.style.height = 20;
                unlinkBtn.style.marginLeft = 4;
                unlinkBtn.style.backgroundColor = new Color(0.5f, 0.3f, 0.3f);
                ApplyButtonHoverStyle(unlinkBtn);
                actionsRow.Add(unlinkBtn);
                
                statusBox.Add(actionsRow);
            }
            else
            {
                statusBox.style.backgroundColor = new Color(0.3f, 0.25f, 0.2f, 0.3f);
                statusBox.style.borderLeftColor = Colors.AccentYellow;
                
                var warningRow = new VisualElement();
                warningRow.style.flexDirection = FlexDirection.Row;
                warningRow.style.alignItems = Align.Center;
                
                var warningIcon = new Label("⚠");
                warningIcon.style.color = Colors.AccentYellow;
                warningIcon.style.marginRight = 6;
                warningRow.Add(warningIcon);
                
                var warningText = new Label("No provider selected. Select an Item above.");
                warningText.style.fontSize = 11;
                warningText.style.color = Colors.AccentYellow;
                warningRow.Add(warningText);
                
                statusBox.Add(warningRow);
            }
            
            levelSourceContent.Add(statusBox);
        }
        
        private VisualElement CreateInfoRow(string label, string value, Color valueColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            
            var labelElement = new Label(label);
            labelElement.style.width = 80;
            labelElement.style.fontSize = 10;
            labelElement.style.color = Colors.HintText;
            row.Add(labelElement);
            
            var valueElement = new Label(value);
            valueElement.style.fontSize = 10;
            valueElement.style.color = valueColor;
            row.Add(valueElement);
            
            return row;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Validation Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildValidationSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Validation",
                Title = "Validation Rules",
                AccentColor = Colors.AccentCyan,
                HelpUrl = "https://docs.playforge.dev/abilities/validation"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var sourceRules = CreatePropertyField(serializedObject.FindProperty("SourceActivationRules"), "SourceActivationRules", "Source Rules");
            sourceRules.tooltip = "Rules that validate the ability source (caster)";
            content.Add(sourceRules);
            
            var targetRules = CreatePropertyField(serializedObject.FindProperty("TargetActivationRules"), "TargetActivationRules", "Target Rules");
            targetRules.tooltip = "Rules that validate the ability target";
            content.Add(targetRules);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildWorkersSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                AccentColor = Colors.AccentOrange,
                HelpUrl = "https://docs.playforge.dev/abilities/workers"
            });
            parent.Add(section.Section);
    
            var content = section.Content;

            var hint = CreateHintLabel("Workers are subscribed to Ability owner when learned.");
            content.Add(hint);
    
            // Use StandardWorkerGroup instead of individual lists
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty("WorkerGroup"), 
                "WorkerGroup", 
                ""
            );
            content.Add(workerGroupField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Local Data Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLocalDataSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                AccentColor = Colors.AccentGray,
                HelpUrl = "https://docs.playforge.dev/abilities/localdata"
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty("LocalData"), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            var result = GenerateAssetTag(ability.Definition.Name, "Ability");
            assetTagValueLabel.text = result.result;
            
            // Sync the actual AssetTag.Name field
            if (!result.isUnknown)
            {
                var tags = ability.Tags;
                var assetTag = tags.AssetTag;
                assetTag.Name = result.result;
                tags.AssetTag = assetTag;
                ability.Tags = tags;
            }
        }
        
        private new void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            RebuildLevelSourceContent();
            RebuildLevelingContent();
            Repaint();
        }
    }
}