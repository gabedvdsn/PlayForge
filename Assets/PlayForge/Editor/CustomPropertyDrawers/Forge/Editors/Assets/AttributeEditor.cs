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

        protected override BaseForgeLinkProvider GetAsset()
        {
            return null;
        }
        protected override string GetDisplayName()
        {
            string _name = !string.IsNullOrEmpty(attribute.GetName()) ? attribute.Name : "Unnamed Attribute";
            if (!string.IsNullOrEmpty(attribute.Abbreviation)) _name += $" ({attribute.Abbreviation})";
            return _name;
        }
        
        protected override string GetDisplayDescription()
        {
            if (attribute == null) return "";
            var desc = attribute.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (attribute.Textures != null && attribute.Textures.Count > 0)
            {
                var tex = attribute.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_AttributeIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ATTRIBUTE";
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(Attribute));
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/attributes";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        protected override void RebuildLevelSourceContent()
        {
            
        }
        protected override void RebuildLevelingContent()
        {
            
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            attribute = serializedObject.targetObject as Attribute;
            return attribute is not null;
        }
        
        protected override void BuildInspectorContent(VisualElement parent)
        {
            // Sections inside ScrollView
            BuildDefinitionSection(parent);
            BuildLocalDataSection(parent);
            BuildUsageSection(parent);

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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributes/definition",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Attribute.Name), nameof(Attribute.Abbreviation), nameof(Attribute.Description), nameof(Attribute.Textures)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(Attribute.Name) => $"Unnamed Attribute",
                        nameof(Attribute.Abbreviation) => string.Empty,
                        nameof(Attribute.Description) => string.Empty,
                        nameof(Attribute.Textures) => null,
                        _ => null
                    };
                }
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
                UpdateHeader();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(nameField);
            
            // Name field
            var shortNameField = CreateTextField("Abbreviation", "Abbreviation");
            shortNameField.value = attribute.Abbreviation;
            shortNameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attribute.Abbreviation = shortNameField.value;
                MarkDirty(attribute);
                UpdateHeader();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(shortNameField);
            
            // Description field
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 18);
            descField.value = attribute.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attribute.Description = descField.value;
                MarkDirty(attribute);
                UpdateHeader();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(descField);

            var textures = serializedObject.FindProperty(nameof(Attribute.Textures));
            var texturesField = CreatePropertyField(textures, "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                MarkDirty(attribute);
                UpdateHeader();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(texturesField);
        }
        
        private void BuildLocalDataSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Attribute.LocalData) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.LocalData)), "LocalData", ""));
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
                AccentColor = GetAssetTypeColor(),
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