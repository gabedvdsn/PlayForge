using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(AttributeSet))]
    public class AttributeSetEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_AttributeSetIcon;
        
        private AttributeSet attributeSet;
        private HeaderResult headerResult;
        
        // Count labels for dynamic updates
        private Label attributeCountLabel;
        private Label subsetCountLabel;
        private Label uniqueAttributesLabel;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            attributeSet = serializedObject.targetObject as AttributeSet;
            if (attributeSet == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // Main Header
            BuildHeader();
            
            // Sections
            BuildAttributesSection(root);
            BuildSubsetsSection(root);
            BuildSettingsSection(root);
            BuildSummarySection(root);
            
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
                Icon = m_AttributeSetIcon,
                DefaultTitle = attributeSet.name,
                DefaultDescription = GetHeaderDescription(),
                ShowRefresh = true,
                ShowLookup = true,
                ShowVisualize = true,
                OnRefresh = Refresh,
                OnLookup = Lookup,
                OnVisualize = Visualize
            });
            
            root.Add(headerResult.Header);
        }
        
        private string GetHeaderDescription()
        {
            int attrCount = attributeSet.Attributes?.Count ?? 0;
            int subsetCount = attributeSet.SubSets?.Count ?? 0;
            var uniqueCount = attributeSet.GetUnique().Count;
            return $"Contains {attrCount} attributes, {subsetCount} subsets ({uniqueCount} unique total)";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAttributesSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Attributes",
                Title = "Attributes",
                AccentColor = Colors.AccentGreen,
                HelpUrl = "https://docs.playforge.dev/attributesets/attributes"
            });
            parent.Add(section.Section);
            
            // Add count badge to header
            attributeCountLabel = CreateBadge((attributeSet.Attributes?.Count ?? 0).ToString());
            attributeCountLabel.name = "AttributeCount";
            // Insert before divider (index 2 = after arrow and title)
            section.Header.Insert(2, attributeCountLabel);
            
            var content = section.Content;
            
            // Attributes list
            var attrProp = serializedObject.FindProperty("Attributes");
            var attrField = CreatePropertyField(attrProp, "Attributes", "");
            attrField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshCounts());
            content.Add(attrField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Subsets Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSubsetsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Subsets",
                Title = "Sub-Sets",
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/attributesets/subsets"
            });
            parent.Add(section.Section);
            
            // Add count badge to header
            subsetCountLabel = CreateBadge((attributeSet.SubSets?.Count ?? 0).ToString());
            subsetCountLabel.name = "SubsetCount";
            section.Header.Insert(2, subsetCountLabel);
            
            var content = section.Content;
            
            // Hint
            content.Add(CreateHintLabel("Include other Attribute Sets to inherit their attributes."));
            
            // Subsets list
            var subsetProp = serializedObject.FindProperty("SubSets");
            var subsetField = CreatePropertyField(subsetProp, "SubSets", "");
            subsetField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshCounts());
            content.Add(subsetField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Settings Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSettingsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Settings",
                Title = "Collision Settings",
                AccentColor = Colors.AccentOrange,
                HelpUrl = "https://docs.playforge.dev/attributesets/settings"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Hint
            content.Add(CreateHintLabel("When the same attribute appears in multiple sets:"));
            
            // Collision Policy enum
            var policyField = CreateEnumField("CollisionPolicy", "Resolution Policy", attributeSet.CollisionResolutionPolicy);
            policyField.RegisterValueChangedCallback(evt =>
            {
                attributeSet.CollisionResolutionPolicy = (EValueCollisionPolicy)evt.newValue;
                MarkDirty(attributeSet);
            });
            content.Add(policyField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Summary Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSummarySection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Summary",
                Title = "Summary",
                AccentColor = Colors.AccentGray,
                IncludeButtons = new[] { false, false, false }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Unique count label
            uniqueAttributesLabel = new Label($"Total unique attributes: {attributeSet.GetUnique().Count}");
            uniqueAttributesLabel.name = "UniqueAttributesLabel";
            uniqueAttributesLabel.style.fontSize = 11;
            uniqueAttributesLabel.style.color = Colors.HintText;
            content.Add(uniqueAttributesLabel);
            
            // List button
            var listBtn = new Button(ListAllUniqueAttributes) { text = "List All Unique Attributes" };
            listBtn.style.alignSelf = Align.FlexStart;
            listBtn.style.marginTop = 8;
            ApplyButtonHoverStyle(listBtn);
            content.Add(listBtn);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void RefreshCounts()
        {
            if (attributeCountLabel != null)
                attributeCountLabel.text = (attributeSet.Attributes?.Count ?? 0).ToString();
            
            if (subsetCountLabel != null)
                subsetCountLabel.text = (attributeSet.SubSets?.Count ?? 0).ToString();
            
            if (uniqueAttributesLabel != null)
                uniqueAttributesLabel.text = $"Total unique attributes: {attributeSet.GetUnique().Count}";
            
            if (headerResult?.DescriptionLabel != null)
                headerResult.DescriptionLabel.text = GetHeaderDescription();
        }
        
        private void ListAllUniqueAttributes()
        {
            var unique = attributeSet.GetUnique();
            
            if (unique.Count == 0)
            {
                EditorUtility.DisplayDialog("Unique Attributes", "No attributes defined in this set.", "OK");
                return;
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"Unique Attributes ({unique.Count}):");
            sb.AppendLine();
            
            foreach (var attr in unique.OrderBy(a => a?.Name ?? ""))
            {
                if (attr != null)
                {
                    sb.AppendLine($"• {attr.Name}");
                }
            }
            
            EditorUtility.DisplayDialog("Unique Attributes", sb.ToString(), "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void SetupCollapsibleSections() { }

        protected override void Lookup()
        {
            PingAsset(attributeSet);
        }
        
        protected override void Refresh()
        {
            serializedObject.Update();
            RefreshCounts();
        }
    }
}