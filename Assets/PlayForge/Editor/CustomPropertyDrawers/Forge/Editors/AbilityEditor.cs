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
        private HeaderResult headerResult;
        private Label assetTagValueLabel;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            ability = serializedObject.targetObject as Ability;
            if (ability == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // Main Header
            BuildHeader();
            
            // ScrollView with sections
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            // Build all sections
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
        // Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildHeader()
        {
            headerResult = CreateMainHeader(new HeaderConfig
            {
                Icon = m_AbilityIcon,
                DefaultTitle = GetAbilityName(),
                DefaultDescription = GetAbilityDescription(),
                ShowRefresh = true,
                ShowLookup = true,
                OnRefresh = Refresh,
                OnLookup = Lookup
            });
            
            root.Add(headerResult.Header);
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
            //nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ability.Definition.Name = nameField.value;
                UpdateHeaderLabels();
                UpdateAssetTagDisplay();
                MarkDirty();
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 24);
            descField.value = ability.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ability.Definition.Description = descField.value;
                UpdateHeaderLabels();
                MarkDirty();
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
            var sourceReqContainer = new VisualElement { name = "SourceRequirements" };
            sourceReqContainer.Add(CreateHintLabel("Source Requirements"));
            var sourceReqs = tagsProp.FindPropertyRelative("SourceRequirements");
            sourceReqContainer.Add(CreatePropertyField(sourceReqs.FindPropertyRelative("RequireTags"), "Require", "Require"));
            sourceReqContainer.Add(CreatePropertyField(sourceReqs.FindPropertyRelative("AvoidTags"), "Avoid", "Avoid"));
            reqSubsection.Add(sourceReqContainer);
            
            reqSubsection.Add(CreateDivider(4));
            
            // Target Requirements
            var targetReqContainer = new VisualElement { name = "TargetRequirements" };
            targetReqContainer.Add(CreateHintLabel("Target Requirements"));
            var targetReqs = tagsProp.FindPropertyRelative("TargetRequirements");
            targetReqContainer.Add(CreatePropertyField(targetReqs.FindPropertyRelative("RequireTags"), "Require", "Require"));
            targetReqContainer.Add(CreatePropertyField(targetReqs.FindPropertyRelative("AvoidTags"), "Avoid", "Avoid"));
            reqSubsection.Add(targetReqContainer);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Runtime Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildRuntimeSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Runtime",
                Title = "Runtime",
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/abilities/runtime"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var proxyProp = serializedObject.FindProperty("Proxy");
            
            // Targeting
            content.Add(CreatePropertyField(proxyProp.FindPropertyRelative("Targeting"), "TargetingTask", "Targeting Task"));
            
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
            
            var startingLevel = CreatePropertyField(serializedObject.FindProperty("Level"), "Level", "Starting Level");
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
            var costSubsection = CreateSubsection("CostCooldownSubsection", "Usage Effects", Colors.AccentOrange);
            content.Add(costSubsection);
            
            costSubsection.Add(CreatePropertyField(serializedObject.FindProperty("Cost"), "Cost", "Cost Effect"));
            costSubsection.Add(CreatePropertyField(serializedObject.FindProperty("Cooldown"), "Cooldown", "Cooldown Effect"));
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
        
        private string GetAbilityName()
        {
            return !string.IsNullOrEmpty(ability.Definition.Name) ? ability.Definition.Name : "Unnamed Ability";
        }
        
        private string GetAbilityDescription()
        {
            return !string.IsNullOrEmpty(ability.Definition.Description) ? ability.Definition.Description : "No description provided.";
        }
        
        private void UpdateHeaderLabels()
        {
            if (headerResult?.NameLabel != null)
                headerResult.NameLabel.text = GetAbilityName();
            if (headerResult?.DescriptionLabel != null)
                headerResult.DescriptionLabel.text = GetAbilityDescription();
        }
        
        

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            assetTagValueLabel.text = GenerateAssetTag(ability.Definition.Name, "Ability");
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(ability);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void SetupCollapsibleSections()
        {
            // Not needed - sections are built programmatically with built-in collapse behavior
        }

        protected override void Lookup()
        {
            EditorGUIUtility.PingObject(ability);
        }

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateHeaderLabels();
            UpdateAssetTagDisplay();
        }
    }
}