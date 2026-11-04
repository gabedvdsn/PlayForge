using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Extended.Editor
{ 
    [CustomEditor(typeof(Ability)), CanEditMultipleObjects]
    public class AbilityEditor : BasePlayForgeEditor
    {
        private Ability ability;

        // Its fields write through the SerializedObject, so it edits a multiple selection, and what acts on one ability
        // steps aside: the name, the asset tag, linking, a linked ability's levels, and the lists each ability keeps of
        // its own (ED-12)
        protected override bool SupportsMultipleObjects => true;
        
        // UI References for updates
        private Label assetTagValueLabel;
        private VisualElement levelSourceContent;
        private EAbilityLinkMode shownLinkMode;
        private VisualElement levelingContent;

        private const string Section_Definition = "Section_Definition";
        private const string Section_LevelSource = "Section_LevelSource";
        private const string Section_Tags = "Section_Tags";
        private const string Section_Runtime = "Section_Runtime";
        private const string Section_Leveling = "Section_Leveling";
        private const string Section_Validation = "Section_Validation";
        private const string Section_Workers = "Section_Workers";
        private const string Section_LocalData = "Section_LocalData";

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return ability;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(ability.GetName()) ? ability.GetName() : "Unnamed Ability";
        }
        
        protected override string GetDisplayDescription()
        {
            if (ability == null) return "";
            var desc = ability.Definition.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (ability.Definition.Textures != null && ability.Definition.Textures.Count > 0)
            {
                foreach (var text in ability.Definition.Textures)
                {
                    if (text.Tag == Tags.PRIMARY) return text.Texture;
                }
                
                var tex = ability.Definition.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return null;
        }
        
        protected override string GetAssetTypeLabel() => "ABILITY";
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(Ability));
        
        protected override string GetDocumentationTopic() => "abilities";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            ability = serializedObject.targetObject as Ability;
            return ability is not null;
        }

        protected override void BuildInspectorContent(VisualElement parent)
        {
            BuildDefinitionSection(parent);
            BuildLevelSourceSection(parent);
            BuildLevelingSection(parent);
            BuildRuntimeSection(parent);
            BuildTagsSection(parent);
            BuildValidationSection(parent);
            BuildWorkersSection(parent);
            BuildLocalDataSection(parent);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Definition Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildDefinitionSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Definition,
                Title = "Definition",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/definition",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.Definition), nameof(Ability.Cost), nameof(Ability.Cooldown)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(Ability.Definition) => new AbilityDefinition(),
                        nameof(Ability.Cooldown) => null,
                        nameof(Ability.Cost) => null,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Name field
            var nameField = CreateTextField("Name", "Name");
            nameField.value = ability.Definition.Name;
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                // The name and the tag it gives change in one undoable step (ED-12)
                Undo.RecordObject(ability, "Rename Ability");
                ability.Definition.Name = nameField.value;
                SyncAssetTag();
                UpdateHeader();
                MarkDirty(ability);
                Repaint(); // Refresh IMGUI header
            });
            // Each ability's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = targets.Select(target => ((Ability)target).Definition.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one ability at a time: each ability's tag comes from its own name.";
            }
            content.Add(nameField);
            
            // Description, Activation Policy and Activate Immediately bind to their properties, so an edit undoes. They
            // wrote straight into the asset, with no Undo (ED-12).
            content.Add(CreateBoundTextField($"{nameof(Ability.Definition)}.{nameof(AbilityDefinition.Description)}",
                "Description", multiline: true, minHeight: 18, name: "Description"));

            var activationPolicy = CreateEnumField("ActivationPolicy", "Activation Policy", ability.Definition.ActivationPolicy);
            activationPolicy.bindingPath = $"{nameof(Ability.Definition)}.{nameof(AbilityDefinition.ActivationPolicy)}";
            content.Add(activationPolicy);

            var activateImmediately = CreateToggle("ActivateImmediately", "Activate Immediately");
            activateImmediately.value = ability.Definition.ActivateImmediately;
            activateImmediately.bindingPath = $"{nameof(Ability.Definition)}.{nameof(AbilityDefinition.ActivateImmediately)}";
            content.Add(activateImmediately);
            
            var defProp = serializedObject.FindProperty(nameof(Ability.Definition));
            
            /*// Icons Subsection
            var iconsSubsection = CreateSubsection("IconsSubsection", "Icons", Colors.AccentBlue);
            content.Add(iconsSubsection);
            
            var iconsRow = CreateRow(4, wrap: true);
            iconsSubsection.Add(iconsRow);
            
            
            var iconsProp = CreatePropertyField(defProp.FindPropertyRelative(nameof(AbilityDefinition.Textures)), "Textures", "");
            iconsProp.style.flexGrow = 1;
            iconsProp.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                MarkDirty(ability);
                Repaint();
            });
            iconsSubsection.Add(iconsProp);*/

            var texturesField = CreatePropertyField(defProp.FindPropertyRelative(nameof(AbilityDefinition.Textures)), "Textures", "");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                MarkDirty(ability);
                Repaint();
            });
            content.Add(texturesField);
            
            BuildCostCooldownSubsection(content);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagsSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Tags,
                Title = "Tags",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/tags",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.Tags)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tagsProp = serializedObject.FindProperty(nameof(Ability.Tags));
            
            // Asset Tag Display (read-only). Each ability has its own, so a multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
                assetTagValueLabel = valueLabel;
                UpdateAssetTagDisplay();
                content.Add(assetTagContainer);
            }
            
            // Context Tags
            content.Add(CreatePropertyField(tagsProp.FindPropertyRelative(nameof(AbilityTags.ContextTags)), "ContextTags", "Context Tags"));
            
            // Granted Tags Subsection
            var grantedSubsection = CreateSubsection("GrantedTagsSubsection", "Granted Tags", Colors.AccentGreen);
            content.Add(grantedSubsection);
            grantedSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative(nameof(AbilityTags.PassiveGrantedTags)), "PassiveTags", "Passive Tags"));
            grantedSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative(nameof(AbilityTags.ActiveGrantedTags)), "ActiveTags", "Active Tags"));
            
            // Tag Requirements - uses AbilityTagRequirements drawer
            var tagReqProp = tagsProp.FindPropertyRelative(nameof(AbilityTags.TagRequirements));
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
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Runtime,
                Title = "Runtime Behaviour",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/runtime",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.Behaviour)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var proxyProp = serializedObject.FindProperty(nameof(Ability.Behaviour));
            
            // Target Mode. Targeting, the asset loaders and the stages are objects each ability keeps of its own, which a
            // multiple selection can't edit safely: a list of a different length would be cut. A note stands in (ED-12)
            if (IsMultiple)
            {
                content.Add(CreateHintLabel("Select one ability to edit its targeting, asset loaders and stages."));
            }
            else
            {
                var targetMode = CreatePropertyField(proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.Targeting)), "Targeting", "Targeting");
                content.Add(targetMode);
            }

            // Use Implicit Targeting
            content.Add(CreateHintLabel("Automatically set Source as Target?"));
            var useImplicit = CreateToggle("UseImplicitTargeting", "Use Implicit Targeting");
            useImplicit.BindProperty(proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.UseImplicitTargeting)));
            content.Add(useImplicit);
            
            var implicitTargetingTagProp = proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.ImplicitTag));
            var implicitTargetingTag = new PropertyField(implicitTargetingTagProp, "Implicit Tag");
            implicitTargetingTag.BindProperty(proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.ImplicitTag)));
            content.Add(implicitTargetingTag);

            useImplicit.RegisterValueChangedCallback(evt =>
            {
                implicitTargetingTag.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (IsMultiple) return;

            content.Add(CreateDivider());

            content.Add(CreateHintLabel("Load assets and other data into the runtime data packet."));
            var assetLoadersProp = proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.AssetLoaders));
            var assetLoadersField = new PropertyField(assetLoadersProp, "Asset Loaders");
            assetLoadersField.style.marginBottom = 4;
            assetLoadersField.style.marginTop = 2;
            assetLoadersField.Bind(serializedObject);
            content.Add(assetLoadersField);

            content.Add(CreateDivider());
            
            // Stages
            content.Add(CreateHintLabel("Configure Ability runtime with tasks."));
            content.Add(CreatePropertyField(proxyProp.FindPropertyRelative(nameof(AbilityBehaviour.Stages)), "Stages", "Stages"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Loaders List
        // ═══════════════════════════════════════════════════════════════════════════
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Leveling,
                Title = "Leveling",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/leveling",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.StartingLevel), nameof(Ability.MaxLevel), nameof(Ability.IgnoreWhenLevelZero)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    RebuildLevelingContent();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    RebuildLevelingContent();
                    MarkDirty(ability);
                    Repaint();
                },
                GetDefaultValue = (string path) =>
                {
                    return path switch
                    {
                        nameof(Ability.StartingLevel) => 0,
                        nameof(Ability.MaxLevel) => 4,
                        nameof(Ability.IgnoreWhenLevelZero) => true,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            levelingContent = section.Content;
            RebuildLevelingContent();
        }
        
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            
            levelingContent.Clear();

            // A multiple selection edits the level range when every ability keeps its own; a linked ability's range
            // comes from its provider, which differs by ability (ED-12)
            if (IsMultiple)
            {
                if (targets.All(target => ((Ability)target).LinkMode == EAbilityLinkMode.Standalone)) BuildStandaloneLevelingFields();
                else levelingContent.Add(CreateHintLabel("A linked ability takes its levels from its provider. Select standalone abilities to edit their range together."));
                levelingContent.Bind(serializedObject);
                return;
            }

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
            // BuildCostCooldownSubsection();
            
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
            var typeName = GetProviderTypeName(provider.GetType());
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
            ignoreZero.BindProperty(serializedObject.FindProperty(nameof(Ability.IgnoreWhenLevelZero)));
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
            
            var hintLabel = CreateHintLabel("Select an asset in the Level Source section above to link this ability's levels.");
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
            ignoreZero.BindProperty(serializedObject.FindProperty(nameof(Ability.IgnoreWhenLevelZero)));
            levelingContent.Add(ignoreZero);
        }
        
        private void BuildStandaloneLevelingFields()
        {
            // Level row with editable fields
            var levelRow = CreateRow(4);
            levelingContent.Add(levelRow);
            
            var startingLevel = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.StartingLevel)), "Level", "Starting Level");
            startingLevel.style.flexGrow = 1;
            startingLevel.style.marginRight = 8;
            levelRow.Add(startingLevel);
            
            var maxLevel = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.MaxLevel)), "MaxLevel", "Max Level");
            maxLevel.style.flexGrow = 1;
            levelRow.Add(maxLevel);
            
            // Ignore When Level Zero
            var ignoreZero = CreateToggle("IgnoreWhenLevelZero", "Ignore When Level Zero");
            ignoreZero.BindProperty(serializedObject.FindProperty(nameof(Ability.IgnoreWhenLevelZero)));
            levelingContent.Add(ignoreZero);
        }
        
        private void BuildCostCooldownSubsection(VisualElement parent)
        {
            var costSubsection = CreateSubsection("CostCooldownSubsection", "", Colors.AccentOrange);
            parent.Add(costSubsection);
            
            costSubsection.Add(CreateHintLabel("Ability Cost"));
            var costField = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Cost)), "Cost", "");
            costSubsection.Add(costField);
            
            costSubsection.Add(CreateHintLabel("Ability Cooldown"));
            var cooldownField = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Cooldown)), "Cooldown", "");
            costSubsection.Add(cooldownField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Source Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Leveling,
                Title = "Level Source",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/level-source",
                
                HideClearButton = true,
                HideImportButton = true
            });
            parent.Add(section.Section);
            
            levelSourceContent = section.Content;
            RebuildLevelSourceContent();
        }
        
        // Level Source and Leveling are drawn from the link mode, so they redraw when the mode changes, an Undo among
        // them (ED-12)
        protected override void OnAssetChanged()
        {
            if (levelSourceContent == null || ability.LinkMode == shownLinkMode) return;
            RebuildLevelSourceContent();
            RebuildLevelingContent();
        }

        protected override void RebuildLevelSourceContent()
        {
            levelSourceContent.Clear();
            shownLinkMode = ability.LinkMode;

            var infoLabel = CreateHintLabel(
                "Link this ability to another asset to inherit its level range.");
            infoLabel.style.marginBottom = 8;
            levelSourceContent.Add(infoLabel);
            
            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it. It wrote the mode
            // straight into the asset, with no Undo, and redrew only then (ED-12).
            var linkModeField = new EnumField("Link Mode", ability.LinkMode) { bindingPath = nameof(Ability.LinkMode) };
            linkModeField.style.marginBottom = 6;
            levelSourceContent.Add(linkModeField);

            // A provider and a child's link belong to one ability, so a multiple selection sets the mode alone (ED-12)
            if (IsMultiple)
            {
                levelSourceContent.Add(CreateHintLabel("Select one ability to choose its provider or link its cost and cooldown."));
                levelSourceContent.Bind(serializedObject);
                return;
            }

            if (ability.LinkMode == EAbilityLinkMode.LinkedToProvider)
            {
                BuildProviderSelector(levelSourceContent, ability);
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
            
            BuildChildLinkingContent(levelSourceContent, "Link local cost and cooldown effects to this Ability for level scaling.", "Cost & Cooldown");
        }
        
        private void BuildChildLinkingContent(VisualElement parent, string hint, string linkFocus)
        {
            // Divider
            var divider = new VisualElement { name = "linking-divider" };
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = 8;
            divider.style.marginBottom = 8;
            parent.Add(divider);
            
            // Children linking section
            var childrenHeader = new Label("Child Assets") { name = "linking-children-header" };
            childrenHeader.style.fontSize = 11;
            childrenHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            childrenHeader.style.color = Colors.SectionTitle;
            childrenHeader.style.marginBottom = 4;
            parent.Add(childrenHeader);
            
            var childrenHint = CreateHintLabel(hint);
            childrenHint.name = "linking-children-hint";
            parent.Add(childrenHint);
            
            // Link All / Unlink All buttons
            var bulkButtonsRow = CreateRow(4);
            bulkButtonsRow.name = "linking-bulk-buttons";
            parent.Add(bulkButtonsRow);
            
            var linkAllBtn = new Button(() =>
            {
                if (ConfirmLinkChange(
                        $"Confirm Link {linkFocus}",
                        $"Are you sure you want to link usage effects?" +
                        $"\n\nThis will affect {(ability.Cost ? 1 : 0) + (ability.Cooldown ? 1 : 0)} effects.",
                        "Yes"))
                {
                    // Recorded first, so one Undo takes back everything it changes (ED-12)
                    RecordLinkChange($"Link {linkFocus}", ability, ability.Cost, ability.Cooldown);
                    ability.LinkAllChildren();
                
                    if (ability.Cost) MarkDirty(ability.Cost);
                    if (ability.Cooldown) MarkDirty(ability.Cooldown);
                
                    MarkDirty(ability);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    Repaint();
                }

            });
            linkAllBtn.text = $"Link {linkFocus}";
            linkAllBtn.tooltip = $"Link {linkFocus} to this ability";
            linkAllBtn.style.flexGrow = 1;
            linkAllBtn.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAllBtn);
            bulkButtonsRow.Add(linkAllBtn);
            
            var unlinkAllBtn = new Button(() =>
            {
                // Counts the children the unlink would change; it asked IsLinkedTo(this), the editor, so it always said 0 (ED-31)
                if (ConfirmLinkChange(
                        $"Confirm Unlink {linkFocus}",
                        $"Are you sure you want to unlink usage effects?" +
                        $"\n\nThis will affect {(ability.Cost && ability.Cost.IsLinkedTo(ability) ? 1 : 0) + (ability.Cooldown && ability.Cooldown.IsLinkedTo(ability) ? 1 : 0)} effects." +
                        $"This change only applies to assets linked to this ability",
                        "Yes"))
                {
                    // Recorded first, so one Undo takes back everything it changes (ED-12)
                    RecordLinkChange($"Unlink {linkFocus}", ability, ability.Cost, ability.Cooldown);
                    ability.UnlinkAllChildren();
                
                    if (ability.Cost) MarkDirty(ability.Cost);
                    if (ability.Cooldown) MarkDirty(ability.Cooldown);
                
                    MarkDirty(ability);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    Repaint();
                }
            });
            unlinkAllBtn.text = $"Unlink {linkFocus}";
            unlinkAllBtn.tooltip = $"Unlink {linkFocus} from this ability";
            unlinkAllBtn.style.flexGrow = 1;
            unlinkAllBtn.style.backgroundColor = new Color(0.4f, 0.3f, 0.3f);
            ApplyButtonHoverStyle(unlinkAllBtn);
            bulkButtonsRow.Add(unlinkAllBtn);
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

                var typeName = GetProviderTypeName(provider.GetType());
                var typeColor = Colors.GetAssetColor(provider.GetType());
                
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
                    Selection.activeObject = provider;
                    EditorGUIUtility.PingObject(provider);
                });
                gotoBtn.text = "Go to Provider";
                gotoBtn.style.fontSize = 10;
                gotoBtn.style.height = 20;
                ApplyButtonHoverStyle(gotoBtn);
                actionsRow.Add(gotoBtn);
                
                var unlinkBtn = new Button(() =>
                {
                    if (ConfirmLinkChange(
                        "Unlink Ability",
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe ability will become standalone.",
                        "Unlink"))
                    {
                        // Recorded first, so one Undo takes back everything it changes (ED-12)
                        RecordLinkChange("Unlink Ability", ability);
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
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Validation,
                Title = "Validation Rules",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/validation",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.SourceActivationRules), nameof(Ability.TargetActivationRules)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(Ability.SourceActivationRules) => new List<IAbilityValidationRule>()
                        {
                            new CooldownValidation(),
                            new CostValidation(),
                            new IsAliveValidation()
                        },
                        nameof(Ability.TargetActivationRules) => new List<IAbilityValidationRule>()
                        {
                            new IsAliveValidation()
                        },
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;

            // Each ability keeps its own lists of rules, which a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                content.Add(CreateHintLabel("Select one ability to edit its activation rules."));
                return;
            }

            var sourceRules = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.SourceActivationRules)), "SourceActivationRules", "Source Rules");
            sourceRules.tooltip = "Rules that validate the ability source (caster)";
            content.Add(sourceRules);
            
            var targetRules = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.TargetActivationRules)), "TargetActivationRules", "Target Rules");
            targetRules.tooltip = "Rules that validate the ability target";
            content.Add(targetRules);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildWorkersSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = Section_Workers,
                Title = "Workers",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/workers",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Ability.WorkerGroup)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                }
            });
            parent.Add(section.Section);
    
            var content = section.Content;

            var hint = CreateHintLabel("Workers are subscribed to Ability owner when learned.");
            content.Add(hint);
    
            // Use StandardWorkerGroup instead of individual lists
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty(nameof(Ability.WorkerGroup)), 
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
            var section = CreateSection(new SectionConfig
            {
                Name = Section_LocalData,
                Title = "Local Data",
                AccentColor = GetAssetTypeColor(),
                HelpTopic = "abilities/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Ability.LocalData) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    SyncAssetTag();
                    UpdateHeader();
                    MarkDirty(ability);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private void UpdateAssetTagDisplay() =>
            ShowAssetTag(assetTagValueLabel, ability.Definition.Name, "Ability", ability.Tags.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(ability, ability.Definition.Name, "Ability", ability.Tags.AssetTag, tag =>
            {
                var tags = ability.Tags;
                tags.AssetTag = tag;
                ability.Tags = tags;
            });
            UpdateAssetTagDisplay();
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