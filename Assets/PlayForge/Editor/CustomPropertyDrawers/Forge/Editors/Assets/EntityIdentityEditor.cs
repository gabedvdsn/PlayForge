using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(EntityIdentity))]
    public class EntityIdentityEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_EntityIcon;
        
        private EntityIdentity entity;
        private Label assetTagValueLabel;
        private VisualElement levelSourceContent;
        private VisualElement levelingContent;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return entity;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(entity?.Name) ? entity.Name : "Unnamed Entity";
        }
        
        protected override string GetDisplayDescription()
        {
            if (entity == null) return "";
            var desc = entity.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (entity.Textures != null && entity.Textures.Count > 0)
            {
                foreach (var text in entity.Textures)
                {
                    if (text.Tag == Tags.PRIMARY) return text.Texture;
                }
                
                var tex = entity.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_EntityIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ENTITY";

        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(EntityIdentity));
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/entities";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            entity = serializedObject.targetObject as EntityIdentity;
            return entity is not null;
        }
        
        protected override void BuildInspectorContent(VisualElement parent)
        {
            BuildDefinitionSection(parent);
            BuildLevelSourceSection(parent);
            BuildTagsSection(parent);
            BuildLevelingSection(parent);
            BuildAbilitiesSection(parent);
            BuildItemsSection(parent);
            // BuildAttributesSection(parent);
            BuildWorkersSection(parent);
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
                HelpUrl = "https://docs.playforge.dev/entities/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var nameField = CreateTextField("Name", "Name");
            nameField.value = entity.Name;
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                entity.Name = nameField.value;
                MarkDirty(entity);
                UpdateAssetTagDisplay();
                UpdateHeader();
                Repaint();
            });
            content.Add(nameField);
            
            var descriptionField = CreateTextField("Description", "Description", multiline: true, minHeight: 18);
            descriptionField.value = entity.Description;
            descriptionField.RegisterCallback<FocusOutEvent>(_ =>
            {
                entity.Description = descriptionField.value;
                MarkDirty(entity);
                UpdateHeader();
                Repaint();
            });
            content.Add(descriptionField);
            
            var texturesField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.Textures)), 
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
            
            content.Add(CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.AttributeSet)), 
                "AttributeSet", 
                "Attribute Set"
            ));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Source Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Linking",
                Title = "Level Source",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/items/linking"
            });
            parent.Add(section.Section);
            
            levelSourceContent = section.Content;
            
            RebuildLevelSourceContent();
        }
        
        protected override void RebuildLevelSourceContent()
        {
            levelSourceContent.Clear();
            
            var infoLabel = CreateHintLabel(
                "Entities typically use their own level. Optionally, link to another asset to inherit its level range.");
            infoLabel.style.marginBottom = 8;
            levelSourceContent.Add(infoLabel);
            
            var linkModeField = new EnumField("Link Mode", entity.LinkMode);
            linkModeField.style.marginBottom = 6;
            linkModeField.RegisterValueChangedCallback(evt =>
            {
                entity.LinkMode = (EItemLinkMode)evt.newValue;
                serializedObject.Update();
                MarkDirty(entity);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            levelSourceContent.Add(linkModeField);
            
            if (entity.LinkMode == EItemLinkMode.LinkedToProvider)
            {
                BuildProviderSelector(levelSourceContent, entity);
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
                
                var text = new Label("Item operates independently with its own level tracking.");
                text.style.fontSize = 10;
                text.style.color = Colors.HintText;
                text.style.unityFontStyleAndWeight = FontStyle.Italic;
                standaloneInfo.Add(text);
                
                levelSourceContent.Add(standaloneInfo);
            }
            
            // Re-bind after rebuilding
            levelSourceContent.Bind(serializedObject);
            
            BuildChildLinkingContent(levelSourceContent, "Link local effects and abilities to this item for level scaling.", "Ability & Effects");
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
                        $"Are you sure you want to link starting items & abilities?" +
                        $"\n\nThis will affect {(entity.StartingItems?.Count ?? 0) + entity.StartingAbilities?.Count ?? 0} assets.",
                        "Yes", "Cancel"))
                {
                    entity.LinkAllChildren();
                
                    if (entity.StartingItems is not null)
                    {
                        foreach (var item in entity.StartingItems)
                        {
                            if (item is not null) MarkDirty(item.Item);
                        }
                    }
                    
                    if (entity.StartingAbilities is not null)
                    {
                        foreach (var ability in entity.StartingAbilities)
                        {
                            if (ability is not null) MarkDirty(ability);
                        }
                    }
                
                    MarkDirty(entity);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    Repaint();
                }
            });
            linkAllBtn.text = $"Link {linkFocus}";
            linkAllBtn.tooltip = $"Link {linkFocus} to this item";
            linkAllBtn.style.flexGrow = 1;
            linkAllBtn.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAllBtn);
            bulkButtonsRow.Add(linkAllBtn);
            
            var unlinkAllBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                        $"Confirm Unlink {linkFocus}",
                        $"Are you sure you want to unlink starting items & abilities?" +
                        $"\n\nThis will affect {(entity.StartingItems?.Where(e => e.Item.IsLinkedTo(this)).Count() ?? 0) + entity.StartingAbilities?.Where(e => e.IsLinkedTo(this)).Count() ?? 0} assets." +
                        $"\n\nThis change only applies to assets linked to this entity.",
                        "Yes", "Cancel"))
                {
                    entity.UnlinkAllChildren();
                
                    if (entity.StartingItems is not null)
                    {
                        foreach (var item in entity.StartingItems)
                        {
                            if (item is not null) MarkDirty(item.Item);
                        }
                    }
                    
                    if (entity.StartingAbilities is not null)
                    {
                        foreach (var ability in entity.StartingAbilities)
                        {
                            if (ability is not null) MarkDirty(ability);
                        }
                    }
                
                    MarkDirty(entity);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    Repaint();
                }
            });
            
            unlinkAllBtn.text = $"Unlink {linkFocus}";
            unlinkAllBtn.tooltip = $"Unlink {linkFocus} from this item";
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
            
            if (entity.IsLinked)
            {
                var provider = entity.LinkedProvider;
                
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
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe entity will become standalone.",
                        "Unlink", "Cancel"))
                    {
                        entity.Unlink();
                        serializedObject.Update();
                        MarkDirty(entity);
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
                HelpUrl = "https://docs.playforge.dev/entities/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            var contextField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.ContextTags)), 
                "ContextTags", 
                "Context Tags"
            );
            contextField.style.marginTop = 6;
            content.Add(contextField);
            
            var grantedField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.GrantedTags)), 
                "GrantedTags", 
                "Granted Tags"
            );
            grantedField.style.marginTop = 6;
            content.Add(grantedField);
            
            var affiliationField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.Affiliation)), 
                "Affiliation", 
                "Affiliation"
            );
            affiliationField.style.marginTop = 6;
            content.Add(affiliationField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Level",
                Title = "Level",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/entities/level"
            });
            parent.Add(section.Section);

            levelingContent = section.Content;
            
            RebuildLevelingContent();
        }
        
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            
            levelingContent.Clear();
            
            // Determine which state we're in:
            // 1. Standalone mode - show editable level fields
            // 2. LinkedToProvider mode with no provider - show disabled level fields + warning
            // 3. LinkedToProvider mode with provider - show read-only derived values
            
            if (entity.LinkMode == EItemLinkMode.LinkedToProvider)
            {
                if (entity.IsLinked)
                {
                    // Case 3: Fully linked - show read-only derived values
                    BuildLinkedLevelingDisplay();
                }
                else
                {
                    // Case 2: Link mode selected but no provider yet - show disabled fields + warning
                    BuildNoProviderSelectedDisplay();
                }
            }
            else
            {
                // Case 1: Standalone mode - show editable fields
                BuildStandaloneLevelingFields();
            }
            
            // Re-bind the content after rebuilding to ensure PropertyFields work
            levelingContent.Bind(serializedObject);
        }

        private void BuildLinkedLevelingDisplay()
        {
            var provider = entity.LinkedProvider;
            
            // Info box showing levels are derived from provider
            var linkedBox = new VisualElement();
            linkedBox.style.backgroundColor = new Color(0.2f, 0.25f, 0.3f, 0.4f);
            linkedBox.style.borderTopLeftRadius = 4;
            linkedBox.style.borderTopRightRadius = 4;
            linkedBox.style.borderBottomLeftRadius = 4;
            linkedBox.style.borderBottomRightRadius = 4;
            linkedBox.style.borderLeftWidth = 3;
            linkedBox.style.borderLeftColor = Colors.AccentBlue;
            linkedBox.style.paddingLeft = 8;
            linkedBox.style.paddingRight = 8;
            linkedBox.style.paddingTop = 8;
            linkedBox.style.paddingBottom = 8;
            linkedBox.style.marginBottom = 8;
            levelingContent.Add(linkedBox);
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;
            
            var linkIcon = new Label("🔗");
            linkIcon.style.marginRight = 6;
            headerRow.Add(linkIcon);
            
            var headerLabel = new Label("Levels Derived from Provider");
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.color = Colors.AccentBlue;
            headerRow.Add(headerLabel);
            
            linkedBox.Add(headerRow);
            
            // Provider info
            var providerName = provider.GetProviderName();
            var typeName = provider is Item ? "Item" : "Provider";
            var typeColor = provider is Item ? new Color(1f, 0.8f, 0.3f) : Colors.AccentPurple;
            
            var sourceRow = new VisualElement();
            sourceRow.style.flexDirection = FlexDirection.Row;
            sourceRow.style.marginBottom = 4;
            
            var sourceLabel = new Label("Source:");
            sourceLabel.style.width = 100;
            sourceLabel.style.fontSize = 11;
            sourceLabel.style.color = Colors.HintText;
            sourceRow.Add(sourceLabel);
            
            var sourceValue = new Label($"{providerName} ({typeName})");
            sourceValue.style.fontSize = 11;
            sourceValue.style.color = typeColor;
            sourceRow.Add(sourceValue);
            
            linkedBox.Add(sourceRow);
            
            // Level display row
            var levelRow = CreateRow(4);
            levelRow.style.marginTop = 8;
            linkedBox.Add(levelRow);
            
            // Starting Level (read-only)
            var startingLevelContainer = new VisualElement();
            startingLevelContainer.style.flexGrow = 1;
            startingLevelContainer.style.marginRight = 8;
            levelRow.Add(startingLevelContainer);
            
            var startingLevelRow = new VisualElement();
            startingLevelRow.style.flexDirection = FlexDirection.Row;
            startingLevelRow.style.alignItems = Align.Center;
            startingLevelContainer.Add(startingLevelRow);
            
            var startingLevelLabel = new Label("Starting Level");
            startingLevelLabel.style.width = 90;
            startingLevelLabel.style.fontSize = 11;
            startingLevelLabel.style.color = Colors.LabelText;
            startingLevelRow.Add(startingLevelLabel);
            
            var startingLevelValue = new IntegerField();
            startingLevelValue.value = provider.GetStartingLevel();
            startingLevelValue.SetEnabled(false);
            startingLevelValue.style.flexGrow = 1;
            startingLevelValue.style.opacity = 0.8f;
            startingLevelRow.Add(startingLevelValue);
            
            // Max Level (read-only)
            var maxLevelContainer = new VisualElement();
            maxLevelContainer.style.flexGrow = 1;
            levelRow.Add(maxLevelContainer);
            
            var maxLevelRow = new VisualElement();
            maxLevelRow.style.flexDirection = FlexDirection.Row;
            maxLevelRow.style.alignItems = Align.Center;
            maxLevelContainer.Add(maxLevelRow);
            
            var maxLevelLabel = new Label("Max Level");
            maxLevelLabel.style.width = 70;
            maxLevelLabel.style.fontSize = 11;
            maxLevelLabel.style.color = Colors.LabelText;
            maxLevelRow.Add(maxLevelLabel);
            
            var maxLevelValue = new IntegerField();
            maxLevelValue.value = provider.GetMaxLevel();
            maxLevelValue.SetEnabled(false);
            maxLevelValue.style.flexGrow = 1;
            maxLevelValue.style.opacity = 0.8f;
            maxLevelRow.Add(maxLevelValue);
            
            // Hint text
            var hintLabel = CreateHintLabel("These values are controlled by the linked provider. Unlink to edit manually.");
            hintLabel.style.marginTop = 8;
            linkedBox.Add(hintLabel);
        }
        
        private void BuildNoProviderSelectedDisplay()
        {
            // Warning box - linked mode but no provider selected
            var warningBox = new VisualElement();
            warningBox.style.backgroundColor = new Color(0.3f, 0.25f, 0.2f, 0.3f);
            warningBox.style.borderTopLeftRadius = 4;
            warningBox.style.borderTopRightRadius = 4;
            warningBox.style.borderBottomLeftRadius = 4;
            warningBox.style.borderBottomRightRadius = 4;
            warningBox.style.borderLeftWidth = 3;
            warningBox.style.borderLeftColor = Colors.AccentYellow;
            warningBox.style.paddingLeft = 8;
            warningBox.style.paddingRight = 8;
            warningBox.style.paddingTop = 8;
            warningBox.style.paddingBottom = 8;
            warningBox.style.marginBottom = 8;
            levelingContent.Add(warningBox);
            
            // Warning header
            var warningRow = new VisualElement();
            warningRow.style.flexDirection = FlexDirection.Row;
            warningRow.style.alignItems = Align.Center;
            warningRow.style.marginBottom = 8;
            
            var warningIcon = new Label("⚠");
            warningIcon.style.color = Colors.AccentYellow;
            warningIcon.style.marginRight = 6;
            warningRow.Add(warningIcon);
            
            var warningText = new Label("No Level Provider Selected");
            warningText.style.unityFontStyleAndWeight = FontStyle.Bold;
            warningText.style.color = Colors.AccentYellow;
            warningRow.Add(warningText);
            
            warningBox.Add(warningRow);
            
            var hintLabel = CreateHintLabel("Select an asset in the Level Source section above to link this entity's levels.");
            warningBox.Add(hintLabel);
            
            // Level display row (disabled fields showing ability's own values)
            var levelRow = CreateRow(4);
            levelRow.style.marginTop = 8;
            warningBox.Add(levelRow);
            
            // Starting Level (disabled)
            var startingLevelContainer = new VisualElement();
            startingLevelContainer.style.flexGrow = 1;
            startingLevelContainer.style.marginRight = 8;
            levelRow.Add(startingLevelContainer);
            
            var startingLevelRow = new VisualElement();
            startingLevelRow.style.flexDirection = FlexDirection.Row;
            startingLevelRow.style.alignItems = Align.Center;
            startingLevelContainer.Add(startingLevelRow);
            
            var startingLevelLabel = new Label("Starting Level");
            startingLevelLabel.style.width = 90;
            startingLevelLabel.style.fontSize = 11;
            startingLevelLabel.style.color = Colors.HintText;
            startingLevelRow.Add(startingLevelLabel);
            
            var startingLevelValue = new IntegerField();
            startingLevelValue.value = entity.StartingLevel;
            startingLevelValue.SetEnabled(false);
            startingLevelValue.style.flexGrow = 1;
            startingLevelValue.style.opacity = 0.6f;
            startingLevelRow.Add(startingLevelValue);
            
            // Max Level (disabled)
            var maxLevelContainer = new VisualElement();
            maxLevelContainer.style.flexGrow = 1;
            levelRow.Add(maxLevelContainer);
            
            var maxLevelRow = new VisualElement();
            maxLevelRow.style.flexDirection = FlexDirection.Row;
            maxLevelRow.style.alignItems = Align.Center;
            maxLevelContainer.Add(maxLevelRow);
            
            var maxLevelLabel = new Label("Max Level");
            maxLevelLabel.style.width = 70;
            maxLevelLabel.style.fontSize = 11;
            maxLevelLabel.style.color = Colors.HintText;
            maxLevelRow.Add(maxLevelLabel);
            
            var maxLevelValue = new IntegerField();
            maxLevelValue.value = entity.MaxLevel;
            maxLevelValue.SetEnabled(false);
            maxLevelValue.style.flexGrow = 1;
            maxLevelValue.style.opacity = 0.6f;
            maxLevelRow.Add(maxLevelValue);
        }
        
        private void BuildStandaloneLevelingFields()
        {
            var startlevelField = CreateIntegerField("StartingLevel", "Starting Level", entity.StartingLevel);
            startlevelField.style.flexGrow = 1;
            startlevelField.RegisterValueChangedCallback(evt =>
            {
                entity.StartingLevel = evt.newValue;
                MarkDirty(entity);
            });
            levelingContent.Add(startlevelField);
            
            var row = CreateRow(4);
            levelingContent.Add(row);
            
            var maxLevelField = CreateIntegerField("MaxLevel", "Max Level", entity.MaxLevel);
            maxLevelField.style.flexGrow = 1;
            maxLevelField.style.marginRight = 8;
            maxLevelField.RegisterValueChangedCallback(evt =>
            {
                entity.MaxLevel = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(maxLevelField);
            
            var capToggle = CreateToggle("CapAtMaxLevel", "Cap At Max");
            capToggle.value = entity.CapAtMaxLevel;
            capToggle.style.minWidth = 100;
            capToggle.RegisterValueChangedCallback(evt =>
            {
                entity.CapAtMaxLevel = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(capToggle);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAbilitiesSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Abilities",
                Title = "Abilities",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/entities/abilities"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var policyField = CreateEnumField("ActivationPolicy", "Activation Policy", entity.ActivationPolicy);
            policyField.RegisterValueChangedCallback(evt =>
            {
                entity.ActivationPolicy = (EAbilityActivationPolicy)evt.newValue;
                MarkDirty(entity);
            });
            content.Add(policyField);
            
            var startingField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.StartingAbilities)), 
                "StartingAbilities", 
                "Starting Abilities"
            );
            startingField.style.marginTop = 8;
            content.Add(startingField);
            
            content.Add(CreateDivider(3, 6, 4, 4));
            
            var maxAbilitiesField = new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxAbilitiesOperation)), "");
            maxAbilitiesField.style.marginBottom = 2;
            content.Add(maxAbilitiesField);
            
            /*var maxAbilitiesField = CreateIntegerField("MaxAbilities", "Max Abilities", entity.MaxAbilities);
            maxAbilitiesField.style.flexGrow = 1;
            maxAbilitiesField.style.marginRight = 8;
            maxAbilitiesField.RegisterValueChangedCallback(evt =>
            {
                entity.MaxAbilities = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(maxAbilitiesField);*/
            
            var allowDupsToggle = CreateToggle("AllowDuplicates", "Allow Duplicate Abilities");
            allowDupsToggle.value = entity.AllowDuplicateAbilities;
            allowDupsToggle.style.minWidth = 120;
            allowDupsToggle.RegisterValueChangedCallback(evt =>
            {
                entity.AllowDuplicateAbilities = evt.newValue;
                MarkDirty(entity);
            });
            content.Add(allowDupsToggle);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Items Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildItemsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Items",
                Title = "Items",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/entities/items"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var startingField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.StartingItems)), 
                "StartingItems", 
                "Starting Items"
            );
            startingField.style.marginTop = 8;
            content.Add(startingField);
            
            content.Add(CreateDivider(3, 6, 4, 4));
            
            var maxItemsField = new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxItemsOperation)), "");
            maxItemsField.style.marginBottom = 2;
            content.Add(maxItemsField);
            
            var maxEquippedItemsField = new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxEquippedItemsOperation)), "");
            maxEquippedItemsField.style.marginBottom = 2;
            content.Add(maxEquippedItemsField);
            
            /*var maxItemsField = CreateIntegerField("MaxItems", "Max Items", entity.MaxItems);
            maxItemsField.style.flexGrow = 1;
            maxItemsField.style.marginRight = 8;
            maxItemsField.RegisterValueChangedCallback(evt =>
            {
                entity.MaxItems = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(maxItemsField);*/
            
            var allowDupsToggle = CreateToggle("AllowDuplicateItems", "Allow Duplicate Items");
            allowDupsToggle.value = entity.AllowDuplicateItems;
            allowDupsToggle.style.minWidth = 120;
            allowDupsToggle.RegisterValueChangedCallback(evt =>
            {
                entity.AllowDuplicateItems = evt.newValue;
                MarkDirty(entity);
            });
            content.Add(allowDupsToggle);
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/entities/attributes"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            content.Add(CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.AttributeSet)), 
                "AttributeSet", 
                "Attribute Set"
            ));
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
                HelpUrl = "https://docs.playforge.dev/entities/workers"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var hint = CreateHintLabel("Workers are subscribed when entity is initialized.");
            content.Add(hint);
            
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.WorkerGroup)), 
                "WorkerGroup", 
                ""
            );
            content.Add(workerGroupField);
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
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/entities/localdata"
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(
                serializedObject.FindProperty(nameof(EntityIdentity.LocalData)), 
                "LocalData", 
                ""
            ));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            var result = GenerateAssetTag(entity.Name, "Entity");
            assetTagValueLabel.text = result.result;
            
            if (!result.isUnknown)
            {
                var assetTag = entity.AssetTag;
                assetTag.Name = result.result;
                entity.AssetTag = assetTag;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            RebuildLevelSourceContent();
            RebuildLevelingContent();
            Repaint();
        }
    }
}