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
                MarkDirty();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 24);
            descField.value = ability.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ability.Definition.Description = descField.value;
                MarkDirty();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(descField);
            
            // Activation Policy
            var activationPolicy = CreateEnumField("ActivationPolicy", "Activation Policy", ability.Definition.ActivationPolicy);
            activationPolicy.RegisterValueChangedCallback(evt =>
            {
                ability.Definition.ActivationPolicy = (EAbilityActivationPolicyExtended)evt.newValue;
                MarkDirty();
            });
            content.Add(activationPolicy);
            
            // Activate Immediately
            var activateImmediately = CreateToggle("ActivateImmediately", "Activate Immediately");
            activateImmediately.value = ability.Definition.ActivateImmediately;
            activateImmediately.RegisterValueChangedCallback(evt =>
            {
                ability.Definition.ActivateImmediately = evt.newValue;
                MarkDirty();
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
            
            // Tag Requirements Subsection
            var reqSubsection = CreateSubsection("TagRequirementsSubsection", "Tag Requirements", Colors.AccentCyan);
            content.Add(reqSubsection);
            
            // Source Requirements
            reqSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("SourceRequirements"), "SourceRequirements", "Source Requirements"));
            reqSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("TargetRequirements"), "TargetRequirements", "Target Requirements"));
            
            // Block/Cancel Tags
            var blockSubsection = CreateSubsection("BlockCancelSubsection", "Block & Cancel", Colors.AccentRed);
            content.Add(blockSubsection);
            blockSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("BlockAbilitiesWithTags"), "BlockAbilitiesWithTags", "Block Abilities With Tags"));
            blockSubsection.Add(CreatePropertyField(tagsProp.FindPropertyRelative("CancelAbilitiesWithTags"), "CancelAbilitiesWithTags", "Cancel Abilities With Tags"));
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
            var proxyProp = serializedObject.FindProperty("Proxy");
            
            // Target Mode
            var targetMode = CreatePropertyField(proxyProp.FindPropertyRelative("TargetMode"), "TargetMode", "Target Mode");
            content.Add(targetMode);
            
            // Use Implicit Targeting
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
            
            var content = section.Content;
            
            // Level row
            var levelRow = CreateRow(4);
            content.Add(levelRow);
            
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
            content.Add(ignoreZero);
            
            // Cost & Cooldown Subsection
            var costSubsection = CreateSubsection("CostCooldownSubsection", "", Colors.AccentOrange);
            content.Add(costSubsection);
            
            costSubsection.Add(CreateHintLabel("Ability Cost"));
            costSubsection.Add(CreatePropertyField(serializedObject.FindProperty("Cost"), "", ""));
            costSubsection.Add(CreateHintLabel("Ability Cooldown"));
            costSubsection.Add(CreatePropertyField(serializedObject.FindProperty("Cooldown"), "", ""));
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

            var h = CreateRow();
            var hint = CreateHintLabel("Workers are subscribed to Ability owner when learned.");
            content.Add(hint);
            
            // Direct PropertyField binding - no foldout wrappers
            content.Add(CreatePropertyField(serializedObject.FindProperty("AttributeWorkers"), "AttributeWorkers", "Attribute Workers"));
            content.Add(CreatePropertyField(serializedObject.FindProperty("TagWorkers"), "TagWorkers", "Tag Workers"));
            content.Add(CreatePropertyField(serializedObject.FindProperty("ImpactWorkers"), "ImpactWorkers", "Impact Workers"));
            content.Add(CreatePropertyField(serializedObject.FindProperty("AnalysisWorkers"), "AnalysisWorkers", "Analysis Workers"));
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
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(ability);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            Repaint();
        }
    }
}