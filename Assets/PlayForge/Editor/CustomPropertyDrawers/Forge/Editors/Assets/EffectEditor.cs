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
        private Label assetTagValueLabel;
        private VisualElement levelSourceContent;
        
        // For object picker handling - using polling approach for reliability
        private int _pickerControlId;
        private bool _waitingForPicker;
        private Object _lastPickedObject;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override string GetDisplayName()
        {
            return effect.GetName();
        }
        
        protected override string GetDisplayDescription()
        {
            return effect.GetDescription();
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (effect.Definition.Textures != null && effect.Definition.Textures.Count > 0)
            {
                var tex = effect.Definition.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_EffectIcon;
        }
        
        protected override string GetAssetTypeLabel() => "EFFECT";
        
        protected override Color GetAssetTypeColor() => new Color(1f, 0.5f, 0.5f);
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/effects";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        protected override void OnVisualize()
        {
            Debug.Log($"Visualize effect: {effect.name}");
        }
        
        protected override void OnImport()
        {
            EditorGUIUtility.ShowObjectPicker<GameplayEffect>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _waitingForPicker = false;
        }
        
        /// <summary>
        /// Polls for object picker completion since UIElements doesn't reliably receive IMGUI events.
        /// </summary>
        private void OnEditorUpdate()
        {
            if (!_waitingForPicker) return;
            
            // Check if our picker is still active
            var currentControlId = EditorGUIUtility.GetObjectPickerControlID();
            
            if (currentControlId == 0)
            {
                // Picker closed - apply the last tracked selection
                _waitingForPicker = false;
                
                if (_lastPickedObject is BaseForgeLinkProvider provider)
                {
                    effect.LinkedProvider = provider;
                    serializedObject.Update();
                    MarkDirty(effect);
                    RebuildLevelSourceContent();
                    Repaint();
                }
                _lastPickedObject = null;
            }
            else if (currentControlId == _pickerControlId)
            {
                // Picker still open - track current selection for when it closes
                _lastPickedObject = EditorGUIUtility.GetObjectPickerObject();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            effect = serializedObject.targetObject as GameplayEffect;
            if (effect == null) return null;

            root = CreateRoot();
            
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            BuildDefinitionSection(scrollView);
            BuildLevelSourceSection(scrollView);
            BuildTagsSection(scrollView);
            BuildImpactSection(scrollView);
            BuildDurationSection(scrollView);
            BuildWorkersSection(scrollView);
            BuildRequirementsSection(scrollView);
            
            scrollView.Add(CreateBottomPadding());
            
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
                HelpUrl = "https://docs.playforge.dev/effects/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var definition = serializedObject.FindProperty("Definition");
            
            var nameField = CreateTextField("Name", "Name");
            nameField.value = effect.Definition.Name;
            nameField.RegisterValueChangedCallback(_ => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Name = nameField.value;
                UpdateAssetTagDisplay();
                MarkDirty(effect);
                Repaint();
            });
            content.Add(nameField);
            
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 40);
            descField.value = effect.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Description = descField.value;
                MarkDirty(effect);
                Repaint();
            });
            content.Add(descField);
            
            var visibilityField = CreatePropertyField(definition.FindPropertyRelative("Visibility"), "Visibility", "Visibility");
            content.Add(visibilityField);
            
            var texturesField = CreatePropertyField(definition.FindPropertyRelative("Textures"), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ => Repaint());
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Source Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "LevelSource",
                Title = "Level Source",
                AccentColor = Colors.AccentPurple,
                HelpUrl = "https://docs.playforge.dev/effects/level-source"
            });
            parent.Add(section.Section);
            
            levelSourceContent = section.Content;
            RebuildLevelSourceContent();
        }
        
        private void RebuildLevelSourceContent()
        {
            levelSourceContent.Clear();
            
            var infoLabel = CreateHintLabel(
                "Link this effect to an Ability or Entity to derive max level for modifier scaling.");
            infoLabel.style.marginBottom = 8;
            levelSourceContent.Add(infoLabel);
            
            var linkModeField = new EnumField("Link Mode", effect.LinkMode);
            linkModeField.style.marginBottom = 6;
            linkModeField.RegisterValueChangedCallback(evt =>
            {
                effect.LinkMode = (EEffectLinkMode)evt.newValue;
                serializedObject.Update();
                MarkDirty(effect);
                RebuildLevelSourceContent();
            });
            levelSourceContent.Add(linkModeField);
            
            if (effect.LinkMode == EEffectLinkMode.LinkedToProvider)
            {
                BuildProviderSelector();
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
                
                var text = new Label("Effect operates independently with its own level tracking.");
                text.style.fontSize = 10;
                text.style.color = Colors.HintText;
                text.style.unityFontStyleAndWeight = FontStyle.Italic;
                standaloneInfo.Add(text);
                
                levelSourceContent.Add(standaloneInfo);
            }
        }
        
        private void BuildProviderSelector()
        {
            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;
            selectorRow.style.marginBottom = 6;
            
            var label = new Label("Level Provider");
            label.style.width = 100;
            label.style.color = Colors.LabelText;
            selectorRow.Add(label);
            
            var objectField = new ObjectField
            {
                objectType = typeof(BaseForgeLinkProvider),
                allowSceneObjects = false,
                value = effect.LinkedProvider
            };
            objectField.style.flexGrow = 1;
            
            objectField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue as BaseForgeLinkProvider;
                effect.LinkedProvider = newValue;
                serializedObject.Update();
                MarkDirty(effect);
                RebuildLevelSourceContent();
            });
            
            selectorRow.Add(objectField);
            
            var clearBtn = new Button(() =>
            {
                effect.LinkedProvider = null;
                serializedObject.Update();
                MarkDirty(effect);
                RebuildLevelSourceContent();
            });
            clearBtn.text = "×";
            clearBtn.tooltip = "Clear linked provider";
            clearBtn.style.width = 22;
            clearBtn.style.height = 18;
            clearBtn.style.marginLeft = 4;
            clearBtn.style.paddingLeft = 0;
            clearBtn.style.paddingRight = 0;
            clearBtn.style.fontSize = 14;
            ApplyButtonHoverStyle(clearBtn);
            selectorRow.Add(clearBtn);
            
            levelSourceContent.Add(selectorRow);
            
            // Quick select buttons
            var quickSelectRow = new VisualElement();
            quickSelectRow.style.flexDirection = FlexDirection.Row;
            quickSelectRow.style.marginTop = 4;
            quickSelectRow.style.marginLeft = 100;
            
            var selectAbilityBtn = new Button(() => ShowProviderPicker<Ability>());
            selectAbilityBtn.text = "Select Ability...";
            selectAbilityBtn.style.fontSize = 10;
            selectAbilityBtn.style.height = 18;
            ApplyButtonHoverStyle(selectAbilityBtn);
            quickSelectRow.Add(selectAbilityBtn);
            
            var selectEntityBtn = new Button(() => ShowProviderPicker<EntityIdentity>());
            selectEntityBtn.text = "Select Entity...";
            selectEntityBtn.style.fontSize = 10;
            selectEntityBtn.style.height = 18;
            selectEntityBtn.style.marginLeft = 4;
            ApplyButtonHoverStyle(selectEntityBtn);
            quickSelectRow.Add(selectEntityBtn);
            
            levelSourceContent.Add(quickSelectRow);
        }
        
        private void ShowProviderPicker<T>() where T : BaseForgeLinkProvider
        {
            // Generate a unique control ID
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive) + 10000 + Random.Range(1, 1000);
            _waitingForPicker = true;
            _lastPickedObject = effect.LinkedProvider; // Start with current value
            EditorGUIUtility.ShowObjectPicker<T>(effect.LinkedProvider as T, false, "", _pickerControlId);
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
            
            if (effect.IsLinked)
            {
                var provider = effect.LinkedProvider;
                var providerAsset = effect.LinkedProvider;
                
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
                
                var typeName = providerAsset is Ability ? "Ability" : 
                               providerAsset is EntityIdentity ? "Entity" : "Provider";
                var typeColor = providerAsset is Ability ? Colors.AccentBlue : 
                                providerAsset is EntityIdentity ? Colors.AccentOrange : Colors.AccentPurple;
                
                infoGrid.Add(CreateInfoRow("Type:", typeName, typeColor));
                infoGrid.Add(CreateInfoRow("Name:", provider.GetProviderName(), Colors.LabelText));
                infoGrid.Add(CreateInfoRow("Max Level:", provider.GetMaxLevel().ToString(), Colors.AccentGreen));
                infoGrid.Add(CreateInfoRow("Start Level:", provider.GetStartingLevel().ToString(), Colors.LabelText));
                
                statusBox.Add(infoGrid);
                
                var actionsRow = new VisualElement();
                actionsRow.style.flexDirection = FlexDirection.Row;
                actionsRow.style.marginTop = 8;
                actionsRow.style.justifyContent = Justify.FlexEnd;
                
                var gotoBtn = new Button(() =>
                {
                    Selection.activeObject = providerAsset;
                    EditorGUIUtility.PingObject(providerAsset);
                });
                gotoBtn.text = "Go to Provider";
                gotoBtn.style.fontSize = 10;
                gotoBtn.style.height = 20;
                ApplyButtonHoverStyle(gotoBtn);
                actionsRow.Add(gotoBtn);
                
                var unlinkBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog(
                        "Unlink Effect",
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe effect will become standalone.",
                        "Unlink", "Cancel"))
                    {
                        effect.Unlink();
                        serializedObject.Update();
                        MarkDirty(effect);
                        RebuildLevelSourceContent();
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
                
                var warningText = new Label("No provider selected. Select an Ability or Entity above.");
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
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            content.Add(CreatePropertyField(tags.FindPropertyRelative("ContextTags"), "ContextTags", "Context Tags"));
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
            
            var impactField = CreatePropertyField(serializedObject.FindProperty("ImpactSpecification"), "ImpactSpecification", "Impact Specification");
            impactField.style.marginBottom = 8;
            section.Content.Add(impactField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Duration Section
        // ═══════════════════════════════════════════════════════════════════════════
        
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

            var durationField = CreatePropertyField(serializedObject.FindProperty("DurationSpecification"), "DurationSpecification", "Duration Specification");
            section.Content.Add(durationField);
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

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            var result = GenerateAssetTag(effect.Definition?.Name, "Effect");
            assetTagValueLabel.text = result.result;
            
            if (!result.isUnknown)
            {
                var tags = effect.Tags;
                var assetTag = tags.AssetTag;
                assetTag.Name = result.result;
                tags.AssetTag = assetTag;
                effect.Tags = tags;
            }
        }

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            RebuildLevelSourceContent();
            Repaint();
        }
    }
}