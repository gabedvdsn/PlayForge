using System.ComponentModel;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(GameplayEffect))]
    public class GameplayEffectEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_EffectIcon;
        
        private GameplayEffect effect;
        private HeaderResult headerResult;
        private Label assetTagValueLabel;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            effect = serializedObject.targetObject as GameplayEffect;
            if (effect == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // Main Header
            BuildHeader();
            
            // Sections
            BuildDefinitionSection(root);
            BuildTagsSection(root);
            BuildImpactSection(root);
            BuildDurationSection(root);
            BuildWorkersSection(root);
            BuildRequirementsSection(root);
            
            // Bottom padding
            root.Add(CreateBottomPadding());
            
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
                Icon = m_EffectIcon,
                DefaultTitle = GetEffectName(),
                DefaultDescription = GetEffectDescription(),
                ShowRefresh = true,
                ShowLookup = true,
                ShowVisualize = true,
                OnRefresh = Refresh,
                OnLookup = Lookup,
                OnVisualize = Visualize
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
                HelpUrl = "https://docs.playforge.dev/effects/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var definition = serializedObject.FindProperty("Definition");
            
            // Name field
            var nameField = CreateTextField("Name", "Name");
            nameField.value = effect.Definition.Name;
            nameField.RegisterValueChangedCallback(_ => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Name = nameField.value;
                UpdateHeaderLabels();
                UpdateAssetTagDisplay();
                MarkDirty(effect);
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 40);
            descField.value = effect.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Description = descField.value;
                UpdateHeaderLabels();
                MarkDirty(effect);
            });
            content.Add(descField);
            
            // Visibility + Icon row
            var row = CreateRow(4);
            content.Add(row);
            
            var iconField = CreatePropertyField(definition.FindPropertyRelative("Icon"), "Icon", "Icon");
            iconField.style.flexGrow = 1;
            row.Add(iconField);
            
            var visibilityField = CreatePropertyField(definition.FindPropertyRelative("Visibility"), "Visibility", "Visibility");
            visibilityField.style.flexGrow = 1;
            visibilityField.style.marginRight = 8;
            row.Add(visibilityField);
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
                HelpUrl = "https://docs.playforge.dev/effects/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tags = serializedObject.FindProperty("Tags");
            
            // Asset Tag Display (read-only)
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            // Context Tags
            content.Add(CreatePropertyField(tags.FindPropertyRelative("ContextTags"), "ContextTags", "Context Tags"));
            
            // Granted Tags
            content.Add(CreatePropertyField(tags.FindPropertyRelative("GrantedTags"), "GrantedTags", "Granted Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Impact Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildImpactSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Impact",
                Title = "Impact Specification",
                AccentColor = Colors.AccentRed,
                HelpUrl = "https://docs.playforge.dev/effects/impact"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var impactField = CreatePropertyField(serializedObject.FindProperty("ImpactSpecification"), "ImpactSpecification", "Impact Specification");
            impactField.style.marginBottom = 8;
            content.Add(impactField);
            
            //content.Add(CreatePropertyField(serializedObject.FindProperty("DurationSpecification"), "DurationSpecification", "Duration Specification"));
        }

        private void BuildDurationSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Duration",
                Title = "Duration Specification",
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/effects/duration"
            });
            parent.Add(section.Section);

            var content = section.Content;
            
            var durationField = CreatePropertyField(serializedObject.FindProperty("DurationSpecification"), "DurationSpecification", "Duration Specification");
            //durationField.style.marginBottom = 8;
            content.Add(durationField);
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
                HelpUrl = "https://docs.playforge.dev/effects/workers"
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty("Workers"), "Workers", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildRequirementsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Requirements",
                Title = "Requirements",
                AccentColor = Colors.AccentCyan,
                HelpUrl = "https://docs.playforge.dev/effects/requirements"
            });
            parent.Add(section.Section);
            
            var content = section.Content;

            var sourceHeader = CreateHeader("", "Source (Caster)", Colors.AccentBlue);
            content.Add(sourceHeader);
            
            // Direct PropertyField binding - no foldout wrappers to avoid duplication
            var sourceField = CreatePropertyField(serializedObject.FindProperty("SourceRequirements"), "SourceRequirements", "Source Requirements");
            sourceField.style.marginBottom = 8;
            content.Add(sourceField);
            
            content.Add(CreateDivider());
            
            var targetHeader = CreateHeader("Target", "Target", Colors.AccentPurple);
            content.Add(targetHeader);
            
            content.Add(CreatePropertyField(serializedObject.FindProperty("TargetRequirements"), "TargetRequirements", "Target Requirements"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string GetEffectName()
        {
            return !string.IsNullOrEmpty(effect.GetName()) ? effect.GetName() : "Unnamed Effect";
        }
        
        private string GetEffectDescription()
        {
            return !string.IsNullOrEmpty(effect.GetDescription()) ? effect.GetDescription() : "No description provided.";
        }
        
        private void UpdateHeaderLabels()
        {
            if (headerResult?.NameLabel != null)
                headerResult.NameLabel.text = GetEffectName();
            if (headerResult?.DescriptionLabel != null)
                headerResult.DescriptionLabel.text = GetEffectDescription();
        }
        
        private string GenerateAssetTag(string effectName)
        {
            if (string.IsNullOrEmpty(effectName))
                return "Effect";
            
            string cleaned = Regex.Replace(effectName, @"[^a-zA-Z0-9\s]", "");
            string[] words = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + 
                              (words[i].Length > 1 ? words[i].Substring(1) : "");
                }
            }
            
            string result = string.Join("", words);
            
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "Effect" + result;
            }
            
            return string.IsNullOrEmpty(result) ? "Effect" : result;
        }

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            assetTagValueLabel.text = GenerateAssetTag(effect.Definition?.Name);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void SetupCollapsibleSections() { }

        protected override void Lookup()
        {
            PingAsset(effect);
        }
        
        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateHeaderLabels();
            UpdateAssetTagDisplay();
        }
    }
}