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

        // Level provider sections
        private VisualElement levelSourceContent;
        private VisualElement levelingContent;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return attributeSet;
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
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Provider Sections
        //
        // Mirrors the Ability/Item editor pattern. The AttributeSet itself is now a
        // BaseForgeLevelProvider so cached scalers authored on its elements can derive
        // their level bounds from the set (or from a chained linked provider).
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "LevelSource",
                Title = "Level Source",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/levelsource",
            });
            parent.Add(section.Section);

            levelSourceContent = section.Content;
            RebuildLevelSourceContent();
        }

        protected override void RebuildLevelSourceContent()
        {
            if (levelSourceContent == null) return;
            levelSourceContent.Clear();

            var info = CreateHintLabel(
                "Determines the level range used by cached scalers authored on this set's elements.");
            info.style.marginBottom = 8;
            levelSourceContent.Add(info);

            var linkModeField = new EnumField("Link Mode", attributeSet.LinkMode);
            linkModeField.style.marginBottom = 6;
            linkModeField.RegisterValueChangedCallback(evt =>
            {
                attributeSet.LinkMode = (EAttributeSetLinkMode)evt.newValue;
                serializedObject.Update();
                MarkDirty(attributeSet);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            levelSourceContent.Add(linkModeField);

            if (attributeSet.LinkMode == EAttributeSetLinkMode.LinkedToProvider)
            {
                BuildProviderSelector(levelSourceContent, attributeSet);
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
                
                var text = new Label("Attribute Set operates independently with its own level tracking for cached scalers.");
                text.style.fontSize = 10;
                text.style.color = Colors.HintText;
                text.style.unityFontStyleAndWeight = FontStyle.Italic;
                standaloneInfo.Add(text);
                
                levelSourceContent.Add(standaloneInfo);
                
                /*var standalone = CreateHintLabel(
                    "Standalone — cached scalers use this set's local StartingLevel/MaxLevel.");
                standalone.style.marginTop = 6;
                levelSourceContent.Add(standalone);*/
            }

            levelSourceContent.Bind(serializedObject);
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
            
            if (attributeSet.IsLinked)
            {
                var provider = attributeSet.LinkedProvider;
                
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
                    if (EditorUtility.DisplayDialog(
                        "Unlink Ability",
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe ability will become standalone.",
                        "Unlink", "Cancel"))
                    {
                        attributeSet.Unlink();
                        serializedObject.Update();
                        MarkDirty(attributeSet);
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
            
            VisualElement CreateInfoRow(string label, string value, Color valueColor)
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
        }

        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/attributesets/leveling",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(AttributeSet.StartingLevel), nameof(AttributeSet.MaxLevel) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    MarkDirty(attributeSet);
                    RebuildLevelingContent();
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    MarkDirty(attributeSet);
                    RebuildLevelingContent();
                    Repaint();
                },
                GetDefaultValue = path => path switch
                {
                    nameof(AttributeSet.StartingLevel) => 1,
                    nameof(AttributeSet.MaxLevel)      => 4,
                    _ => null
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

            // Three states (parallels AbilityEditor / ItemEditor):
            //   1. Standalone           — editable StartingLevel + MaxLevel.
            //   2. Linked, no provider  — warning + disabled local fields.
            //   3. Linked + resolved    — read-only display of derived values.
            if (attributeSet.LinkMode == EAttributeSetLinkMode.LinkedToProvider)
            {
                if (attributeSet.LinkedProvider != null) BuildLinkedDisplay();
                else BuildNoProviderWarning();
            }
            else
            {
                BuildStandaloneFields();
            }

            levelingContent.Bind(serializedObject);
        }

        private void BuildStandaloneFields()
        {
            var row = CreateRow(8);
            levelingContent.Add(row);

            var startField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.StartingLevel)), "Level", "Starting Level");
            startField.style.flexGrow = 1;
            row.Add(startField);

            var maxField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.MaxLevel)), "Level", "Max Level");
            maxField.style.flexGrow = 1;
            row.Add(maxField);

            levelingContent.Add(CreateHintLabel(
                "Cached scalers in this set whose Level Configuration is 'Lock to Level Provider' " +
                "use this MaxLevel to size their level-value arrays."));
        }

        private void BuildNoProviderWarning()
        {
            var warning = CreateHintLabel(
                "Link Mode is set to 'Linked to Provider' but no provider is selected. " +
                "Pick a provider above or switch back to Standalone to edit levels here.");
            warning.style.color = Colors.AccentYellow;
            warning.style.marginBottom = 6;
            levelingContent.Add(warning);

            var row = CreateRow(8);
            levelingContent.Add(row);

            var startField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.StartingLevel)), "Level", "Starting Level");
            startField.style.flexGrow = 1;
            startField.SetEnabled(false);
            row.Add(startField);

            var maxField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.MaxLevel)), "Level", "Max Level");
            maxField.style.flexGrow = 1;
            maxField.SetEnabled(false);
            row.Add(maxField);
        }

        private void BuildLinkedDisplay()
        {
            var provider = attributeSet.LinkedProvider;

            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.2f, 0.25f, 0.3f, 0.4f);
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = Colors.AccentBlue;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.marginBottom = 8;
            levelingContent.Add(box);

            var header = new Label($"🔗  Levels derived from {provider.GetProviderName()} ({GetProviderTypeName(provider.GetType())})");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.AccentBlue;
            header.style.marginBottom = 6;
            box.Add(header);

            var row = CreateRow(8);
            box.Add(row);

            var startField = new IntegerField("Starting Level") { value = provider.GetStartingLevel() };
            startField.SetEnabled(false);
            startField.style.flexGrow = 1;
            startField.style.opacity = 0.85f;
            row.Add(startField);

            var maxField = new IntegerField("Max Level") { value = provider.GetMaxLevel() };
            maxField.SetEnabled(false);
            maxField.style.flexGrow = 1;
            maxField.style.opacity = 0.85f;
            row.Add(maxField);

            box.Add(CreateHintLabel("These values are controlled by the linked provider. Unlink to edit manually."));
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
            BuildLevelSourceSection(parent);
            BuildLevelingSection(parent);
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
            RebuildLevelSourceContent();
            RebuildLevelingContent();
            Repaint();
        }
    }
}