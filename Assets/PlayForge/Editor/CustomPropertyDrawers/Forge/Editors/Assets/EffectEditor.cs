using System.Linq;
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

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLinkProvider GetAsset()
        {
            return effect;
        }
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
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(GameplayEffect));
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/effects";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            effect = serializedObject.targetObject as GameplayEffect;
            return effect is not null;
        }
        
        protected override void BuildInspectorContent(VisualElement parent)
        {
            BuildDefinitionSection(parent);
            BuildLevelSourceSection(parent);
            BuildTagsSection(parent);
            BuildImpactSection(parent);
            BuildDurationSection(parent);
            BuildWorkersSection(parent);
            BuildRequirementsSection(parent);
            BuildLocalDataSection(parent);
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
                HelpUrl = "https://docs.playforge.dev/effects/definition"
            });
            parent.Add(section.Section);
            
            //if (IsCollapsed(effect, section.Name))
            
            var content = section.Content;
            var definition = serializedObject.FindProperty(nameof(GameplayEffect.Definition));
            
            var nameField = CreateTextField("Name", "Name");
            nameField.value = effect.Definition.Name;
            nameField.RegisterValueChangedCallback(_ => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Name = nameField.value;
                UpdateAssetTagDisplay();
                UpdateHeader();
                MarkDirty(effect);
                Repaint();
            });
            content.Add(nameField);
            
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 18);
            descField.value = effect.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                effect.Definition.Description = descField.value;
                UpdateHeader();
                MarkDirty(effect);
                Repaint();
            });
            content.Add(descField);
            
            var retGroupField = CreatePropertyField(definition.FindPropertyRelative(nameof(GameplayEffectDefinition.RetentionGroup)), "RetentionGroup", "Retention Group");
            //retGroupField.Q<Label>().style.marginRight = 12;
            content.Add(retGroupField);
            
            var visibilityField = CreatePropertyField(definition.FindPropertyRelative(nameof(GameplayEffectDefinition.Visibility)), "Visibility", "Visibility");
            content.Add(visibilityField);
            
            var texturesField = CreatePropertyField(definition.FindPropertyRelative(nameof(GameplayEffectDefinition.Textures)), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                MarkDirty(effect);
                Repaint();
            });
            
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/level-source"
            });
            parent.Add(section.Section);
            
            levelSourceContent = section.Content;
            RebuildLevelSourceContent();
        }
        
        protected override void RebuildLevelSourceContent()
        {
            levelSourceContent.Clear();
            
            var infoLabel = CreateHintLabel(
                "Link this effect to another asset to derive its level range.");
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
                Repaint();
            });
            levelSourceContent.Add(linkModeField);
            
            if (effect.LinkMode == EEffectLinkMode.LinkedToProvider)
            {
                BuildProviderSelector(levelSourceContent, effect);
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
            
            // Re-bind after rebuilding
            levelSourceContent.Bind(serializedObject);
            
            BuildChildLinkingContent(levelSourceContent, "Link contained effects (see Packets) to this effect for level scaling.", "Contained Effects");
        }
        protected override void RebuildLevelingContent()
        {
            
        }

        private void BuildChildLinkingContent(VisualElement parent, string hint, string linkFocus)
        {
            // Divider
            var divider = new VisualElement { name = "linking-divider" };
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = 8;
            divider.style.marginBottom = 8;
            parent.Add(divider);
            
            // Children linking section
            var childrenHeader = new Label("Child Assets") { name = "linking-children-header" };
            childrenHeader.style.fontSize = 11;
            childrenHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            childrenHeader.style.color = Colors.SectionTitle;
            childrenHeader.style.marginBottom = 4;
            parent.Add(childrenHeader);
            
            var childrenHint = CreateHintLabel(hint);
            childrenHint.name = "linking-children-hint";
            parent.Add(childrenHint);
            
            // Link All / Unlink All buttons
            var bulkButtonsRow = CreateRow(4);
            bulkButtonsRow.name = "linking-bulk-buttons";
            parent.Add(bulkButtonsRow);
            
            var linkAllBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                        $"Confirm Link {linkFocus}",
                        $"Are you sure you want to link all contained effects?" +
                        $"\n\nThis will affect {effect.ImpactSpecification.Packets.Length} effects.",
                        "Yes", "Cancel"))
                {
                    effect.LinkAllChildren();

                    foreach (var packet in effect.ImpactSpecification.Packets)
                    {
                        MarkDirty(packet.ContainedEffect);
                    }
                
                    MarkDirty(effect);
                }
            });
            linkAllBtn.text = $"Link {linkFocus}";
            linkAllBtn.tooltip = $"Link {linkFocus} to this ability";
            linkAllBtn.style.flexGrow = 1;
            linkAllBtn.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAllBtn);
            bulkButtonsRow.Add(linkAllBtn);
            
            var unlinkAllBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                        $"Confirm Unlink {linkFocus}",
                        $"Are you sure you want to unlink all contained effects?" +
                        $"\n\nThis will affect {effect.ImpactSpecification.Packets.Count(p => p.ContainedEffect.IsLinkedTo(this))} effects." +
                        $"\n\nThis change only applies to assets linked to this effect.",
                        "Yes", "Cancel"))
                {
                    effect.UnlinkAllChildren();

                    foreach (var packet in effect.ImpactSpecification.Packets)
                    {
                        MarkDirty(packet.ContainedEffect);
                    }
                
                    MarkDirty(effect);
                    RebuildLevelSourceContent();
                    Repaint();
                }
            });
            unlinkAllBtn.text = $"Unlink {linkFocus}";
            unlinkAllBtn.tooltip = $"Unlink {linkFocus} from this ability";
            unlinkAllBtn.style.flexGrow = 1;
            unlinkAllBtn.style.backgroundColor = new Color(0.4f, 0.3f, 0.3f);
            ApplyButtonHoverStyle(unlinkAllBtn);
            bulkButtonsRow.Add(unlinkAllBtn);
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tags = serializedObject.FindProperty(nameof(GameplayEffect.Tags));
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(GameplayEffectTags.ContextTags)), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(GameplayEffectTags.GrantedTags)), "GrantedTags", "Granted Tags"));
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/impact"
            });
            parent.Add(section.Section);
            
            var impactField = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.ImpactSpecification)), "ImpactSpecification", "Impact Specification");
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/duration"
            });
            parent.Add(section.Section);

            var durationField = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.DurationSpecification)), "DurationSpecification", "Duration Specification");
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/workers"
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Workers)), "Workers", ""));
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/effects/requirements"
            });
            parent.Add(section.Section);
            
            var content = section.Content;

            /*var sourceHeader = CreateHeader("", "Source (Caster)", Colors.AccentBlue);
            content.Add(sourceHeader);*/
            
            var sourceField = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.SourceRequirements)), "SourceRequirements", "Source (Caster) Requirements");
            sourceField.style.marginBottom = 8;
            content.Add(sourceField);
            
            //content.Add(CreateDivider());
            
            /*var targetHeader = CreateHeader("Target", "Target", Colors.AccentPurple);
            content.Add(targetHeader);*/
            
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.TargetRequirements)), "TargetRequirements", "Target Requirements"));
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
                PropertyPaths = new[] { nameof(GameplayEffect.LocalData) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(effect);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateAssetTagDisplay();
                    UpdateHeader();
                    MarkDirty(effect);
                    Repaint();
                }
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.LocalData)), "LocalData", ""));
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
            UpdateAssetTagDisplay();
            RebuildLevelSourceContent();
            Repaint();
        }
    }
}