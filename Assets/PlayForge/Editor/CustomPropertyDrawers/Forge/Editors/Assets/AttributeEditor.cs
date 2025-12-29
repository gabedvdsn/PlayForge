using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Attribute))]
    public class AttributeEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_AttributeIcon;
        
        private Attribute attribute;
        private HeaderResult headerResult;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            attribute = serializedObject.targetObject as Attribute;
            if (attribute == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // Main Header
            BuildHeader();
            
            // Sections (no scroll view needed for simple inspector)
            BuildDefinitionSection(root);
            BuildUsageSection(root);
            
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
                Icon = m_AttributeIcon,
                DefaultTitle = GetAttributeName(),
                DefaultDescription = GetAttributeDescription(),
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
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/attributes/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Name field
            var nameField = CreateTextField("Name", "Name");
            nameField.value = attribute.Name;
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attribute.Name = nameField.value;
                UpdateHeaderLabels();
                MarkDirty(attribute);
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 60);
            descField.value = attribute.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attribute.Description = descField.value;
                UpdateHeaderLabels();
                MarkDirty(attribute);
            });
            content.Add(descField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Usage Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildUsageSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Usage",
                Title = "Usage",
                AccentColor = Colors.AccentGray,
                HelpUrl = "https://docs.playforge.dev/attributes/usage",
                IncludeButtons = new[] { false, false, false }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            // Usage hints
            content.Add(CreateHintLabel("This attribute can be used in:"));
            
            var usageList = new VisualElement();
            usageList.style.paddingLeft = 8;
            usageList.style.marginBottom = 8;
            content.Add(usageList);
            
            var usageItems = new[]
            {
                "• Attribute Sets - Define starting values",
                "• Gameplay Effects - Modify attribute values",
                "• Validation Rules - Check attribute thresholds",
                "• Ability Costs - Resource consumption"
            };
            
            foreach (var item in usageItems)
            {
                var label = new Label(item);
                label.style.fontSize = 11;
                label.style.color = Colors.HintText;
                usageList.Add(label);
            }
            
            // Find References button
            var findRefsBtn = new Button(FindReferences) { text = "Find All References" };
            findRefsBtn.style.alignSelf = Align.FlexStart;
            findRefsBtn.style.marginTop = 8;
            ApplyButtonHoverStyle(findRefsBtn);
            content.Add(findRefsBtn);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private string GetAttributeName()
        {
            return !string.IsNullOrEmpty(attribute.Name) ? attribute.Name : "Unnamed Attribute";
        }
        
        private string GetAttributeDescription()
        {
            return !string.IsNullOrEmpty(attribute.Description) ? attribute.Description : "No description provided.";
        }
        
        private void UpdateHeaderLabels()
        {
            if (headerResult?.NameLabel != null)
                headerResult.NameLabel.text = GetAttributeName();
            if (headerResult?.DescriptionLabel != null)
                headerResult.DescriptionLabel.text = GetAttributeDescription();
        }
        
        private void FindReferences()
        {
            var guid = GetAssetGuid(attribute);
            PingAsset(attribute);
            
            EditorUtility.DisplayDialog("Find References", 
                $"To find all references to this attribute:\n\n" +
                $"1. Open Edit > Project Settings > Search\n" +
                $"2. Use the Search window (Ctrl+K)\n" +
                $"3. Search for: ref:{guid}\n\n" +
                $"Or use 'Find References In Scene' from the context menu.", 
                "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void SetupCollapsibleSections() { }

        protected override void Lookup()
        {
            FindReferences();
        }
        
        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateHeaderLabels();
        }
    }
}