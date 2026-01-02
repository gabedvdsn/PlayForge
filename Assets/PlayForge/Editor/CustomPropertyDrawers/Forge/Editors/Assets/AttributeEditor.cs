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

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(attribute?.Name) ? attribute.Name : "Unnamed Attribute";
        }
        
        protected override string GetDisplayDescription()
        {
            if (attribute == null) return "";
            var desc = attribute.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            return m_AttributeIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ATTRIBUTE";
        
        protected override Color GetAssetTypeColor() => new Color(0.4f, 0.6f, 1f); // Blue
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/attributes";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        protected override void OnVisualize()
        {
            // TODO: Open attribute visualizer (show usage across attribute sets)
            Debug.Log($"Visualize attribute: {attribute.name}");
        }
        
        protected override void OnImport()
        {
            // Open asset picker for attributes
            EditorGUIUtility.ShowObjectPicker<Attribute>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            attribute = serializedObject.targetObject as Attribute;
            if (attribute == null) return null;

            // Build UI programmatically
            root = CreateRoot();
            
            // ScrollView for sections (header is in OnHeaderGUI)
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            // Sections inside ScrollView
            BuildDefinitionSection(scrollView);
            BuildUsageSection(scrollView);
            
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
                MarkDirty(attribute);
                Repaint(); // Refresh IMGUI header
            });
            content.Add(nameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 60);
            descField.value = attribute.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attribute.Description = descField.value;
                MarkDirty(attribute);
                Repaint(); // Refresh IMGUI header
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

        protected override void Refresh()
        {
            serializedObject.Update();
            Repaint();
        }
    }
}