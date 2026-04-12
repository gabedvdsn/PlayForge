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
        private Label assetTagValueLabel;
        
        private Label attributeCountLabel;
        private Label subsetCountLabel;
        private Label uniqueAttributesLabel;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return null;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(attributeSet?.Name) ? attributeSet.Name : "Unnamed Attribute Set";
        }
        
        protected override string GetDisplayDescription()
        {
            if (attributeSet == null) return "";
            
            if (!string.IsNullOrEmpty(attributeSet.Description))
                return Truncate(attributeSet.Description, 80);
            
            int attrCount = attributeSet.Attributes?.Count ?? 0;
            int subsetCount = attributeSet.SubSets?.Count ?? 0;
            var uniqueCount = attributeSet.GetUnique().Count;
            return $"{attrCount} attributes, {subsetCount} subsets ({uniqueCount} unique)";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (attributeSet?.Textures != null && attributeSet.Textures.Count > 0)
            {
                var tex = attributeSet.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_AttributeSetIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ATTRIBUTE SET";
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(AttributeSet));
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/attributesets";
        
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
            attributeSet = serializedObject.targetObject as AttributeSet;
            return attributeSet is not null;
        }
        
        protected override void BuildInspectorContent(VisualElement parent)
        {
            BuildDefinitionSection(parent);
            BuildAttributesSection(parent);
            BuildSubsetsSection(parent);
            BuildSettingsSection(parent);
            BuildWorkersSection(parent);
            BuildLocalDataSection(parent);
            BuildSummarySection(parent);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Definition Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildDefinitionSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Definition",
                Title = "Definition",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/definition",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.Name), nameof(AttributeSet.Description), nameof(AttributeSet.Textures)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    UpdateAssetTagDisplay();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(AttributeSet.Name) => $"Unnamed Attribute Set",
                        nameof(AttributeSet.Description) => string.Empty,
                        nameof(AttributeSet.Textures) => null,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;

            var nameField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.Name)), label: "Name");
            nameField.RegisterValueChangeCallback(evt =>
            {
                //attributeSet.Name = nameField.value;
                //MarkDirty(attributeSet);
                UpdateAssetTagDisplay();
                UpdateHeader();
                Repaint();
            });
            
            /*var nameField = CreateTextField("Name", "Name");
            nameField.value = attributeSet.Name;
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attributeSet.Name = nameField.value;
                MarkDirty(attributeSet);
                UpdateAssetTagDisplay();
                UpdateHeader();
                Repaint();
            });*/
            content.Add(nameField);
            
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 18);
            descField.value = attributeSet.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                attributeSet.Description = descField.value;
                MarkDirty(attributeSet);
                UpdateHeader();
                Repaint();
            });
            content.Add(descField);
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            var texturesField = CreatePropertyField(
                serializedObject.FindProperty(nameof(AttributeSet.Textures)),
                "Textures",
                "Textures"
            );
            texturesField.style.marginTop = 8;
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAttributesSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Attributes",
                Title = "Attributes",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/attributes",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.Attributes)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    UpdateAssetTagDisplay();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(AttributeSet.Attributes) => null,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            attributeCountLabel = CreateBadge((attributeSet.Attributes?.Count ?? 0).ToString());
            attributeCountLabel.name = "AttributeCount";
            section.Header.Insert(2, attributeCountLabel);
            
            var content = section.Content;
            
            var attrProp = serializedObject.FindProperty(nameof(AttributeSet.Attributes));
            var attrField = CreatePropertyField(attrProp, "Attributes", "");
            attrField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshCounts());
            content.Add(attrField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Subsets Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSubsetsSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Subsets",
                Title = "Sub-Sets",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/subsets",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.SubSets)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    UpdateAssetTagDisplay();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(AttributeSet.SubSets) => null,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            subsetCountLabel = CreateBadge((attributeSet.SubSets?.Count ?? 0).ToString());
            subsetCountLabel.name = "SubsetCount";
            section.Header.Insert(2, subsetCountLabel);
            
            var content = section.Content;
            
            content.Add(CreateHintLabel("Include other Attribute Sets to inherit their attributes."));
            
            var subsetProp = serializedObject.FindProperty(nameof(AttributeSet.SubSets));
            var subsetField = CreatePropertyField(subsetProp, "SubSets", "");
            subsetField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshCounts());
            content.Add(subsetField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Settings Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSettingsSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Settings",
                Title = "Collision Settings",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/settings",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.CollisionResolutionPolicy)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    UpdateAssetTagDisplay();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(AttributeSet.CollisionResolutionPolicy) => EValueCollisionPolicy.UseMaximum,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            content.Add(CreateHintLabel("When the same attribute appears in multiple sets:"));

            var policyField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.CollisionResolutionPolicy)), label: "Resolution Policy");
            policyField.RegisterValueChangeCallback(evt => MarkDirty(attributeSet));
            //var policyField = CreateEnumField("CollisionPolicy", "Resolution Policy", attributeSet.CollisionResolutionPolicy);
            /*policyField.RegisterValueChangedCallback(evt =>
            {
                attributeSet.CollisionResolutionPolicy = (EValueCollisionPolicy)evt.newValue;
                MarkDirty(attributeSet);
            });*/
            content.Add(policyField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildWorkersSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/workers",
                
                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.WorkerGroup)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    UpdateAssetTagDisplay();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(AttributeSet.WorkerGroup) => null,
                        _ => null
                    };
                }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var hint = CreateHintLabel("Workers are subscribed when attribute set is initialized.");
            content.Add(hint);
            
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty(nameof(AttributeSet.WorkerGroup)), 
                "WorkerGroup", 
                ""
            );
            content.Add(workerGroupField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Summary Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildSummarySection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Summary",
                Title = "Summary",
                AccentColor = GetAssetTypeColor(),
                IncludeButtons = new[] { false, false, false }
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            uniqueAttributesLabel = new Label($"Total unique attributes: {attributeSet.GetUnique().Count}");
            uniqueAttributesLabel.name = "UniqueAttributesLabel";
            uniqueAttributesLabel.style.fontSize = 11;
            uniqueAttributesLabel.style.color = Colors.HintText;
            content.Add(uniqueAttributesLabel);
            
            var listBtn = new Button(ListAllUniqueAttributes) { text = "List All Unique Attributes" };
            listBtn.style.alignSelf = Align.FlexStart;
            listBtn.style.marginTop = 8;
            ApplyButtonHoverStyle(listBtn);
            content.Add(listBtn);
        }
        
        private void BuildLocalDataSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/abilities/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(AttributeSet.LocalData) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(attributeSet);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            var result = GenerateAssetTag(attributeSet.Name, "AttributeSet");
            assetTagValueLabel.text = result.result;
            
            if (!result.isUnknown)
            {
                var assetTag = attributeSet.AssetTag;
                assetTag.Name = result.result;
                attributeSet.AssetTag = assetTag;
            }
        }
        
        private void RefreshCounts()
        {
            if (attributeCountLabel != null)
                attributeCountLabel.text = (attributeSet.Attributes?.Count ?? 0).ToString();
            
            if (subsetCountLabel != null)
                subsetCountLabel.text = (attributeSet.SubSets?.Count ?? 0).ToString();
            
            if (uniqueAttributesLabel != null)
                uniqueAttributesLabel.text = $"Total unique attributes: {attributeSet.GetUnique().Count}";
            
            Repaint();
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
            
            foreach (var attr in unique.OrderBy(a => a?.GetName() ?? ""))
            {
                if (attr != null)
                {
                    sb.AppendLine($"• {attr.GetName()}");
                }
            }
            
            EditorUtility.DisplayDialog("Unique Attributes", sb.ToString(), "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            RefreshCounts();
            UpdateAssetTagDisplay();
            Repaint();
        }
    }
}