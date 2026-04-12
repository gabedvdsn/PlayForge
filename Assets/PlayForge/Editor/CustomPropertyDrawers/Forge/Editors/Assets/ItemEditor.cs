using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Item))]
    public class ItemEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_ItemIcon;
        
        private Item item;
        private Label assetTagValueLabel;
        private VisualElement levelSourceContent;
        private VisualElement levelingContent;
        private VisualElement effectLinkStatusContainer;
        private VisualElement abilityLinkStatusContainer;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return item;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(item?.Definition.Name) ? item.Definition.Name : "Unnamed Item";
        }
        
        protected override string GetDisplayDescription()
        {
            if (item == null) return "";
            var desc = item.Definition.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (item?.Definition.Textures != null && item.Definition.Textures.Count > 0)
            {
                var tex = item.Definition.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_ItemIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ITEM";
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(Item));
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/items";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            item = serializedObject.targetObject as Item;
            return item is not null;
        }

        protected override void BuildInspectorContent(VisualElement parent)
        {
            BuildDefinitionSection(parent);
            BuildLevelSourceSection(parent);
            BuildTagsSection(parent);
            BuildLevelingSection(parent);
            BuildEffectsSection(parent);
            BuildAbilitiesSection(parent);
            BuildWorkersSection(parent);
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
                HelpUrl = "https://docs.playforge.dev/items/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var definition = serializedObject.FindProperty(nameof(Item.Definition));
            
            var nameField = CreateTextField("Name", "Name");
            nameField.value = item.Definition.Name;
            nameField.RegisterValueChangedCallback(_ => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                item.Definition.Name = nameField.value;
                UpdateAssetTagDisplay();
                UpdateHeader();
                MarkDirty(item);
                Repaint();
            });
            content.Add(nameField);
            
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 18);
            descField.value = item.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                item.Definition.Description = descField.value;
                MarkDirty(item);
                UpdateHeader();
                Repaint();
            });
            content.Add(descField);
            
            var visibilityField = CreatePropertyField(definition.FindPropertyRelative(nameof(ItemDefinition.Visibility)), "Visibility", "Visibility");
            content.Add(visibilityField);
            
            var texturesField = CreatePropertyField(definition.FindPropertyRelative(nameof(ItemDefinition.Textures)), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
            
            // Allow duplicates toggle
            var duplicatesRow = CreateRow(4);
            var duplicatesToggle = new Toggle("Allow Duplicates");
            duplicatesToggle.value = item.Definition.AllowDuplicates;
            duplicatesToggle.RegisterValueChangedCallback(evt =>
            {
                item.Definition.AllowDuplicates = evt.newValue;
                MarkDirty(item);
            });
            duplicatesToggle.tooltip = "If enabled, multiple instances of this item can exist in the same inventory.";
            duplicatesRow.Add(duplicatesToggle);
            content.Add(duplicatesRow);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagsSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/items/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tags = serializedObject.FindProperty(nameof(Item.Tags));
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(ItemTags.ContextTags)), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(ItemTags.PassiveGrantedTags)), "PassiveGrantedTags", "Granted Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Linking Section (NEW - Level Provider Integration)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSourceSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
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
                "Items typically use their own level. Optionally, link to another asset to inherit its level range.");
            infoLabel.style.marginBottom = 8;
            levelSourceContent.Add(infoLabel);
            
            var linkModeField = new EnumField("Link Mode", item.LinkMode);
            linkModeField.style.marginBottom = 6;
            linkModeField.RegisterValueChangedCallback(evt =>
            {
                item.LinkMode = (EItemLinkMode)evt.newValue;
                serializedObject.Update();
                MarkDirty(item);
                RebuildLevelSourceContent();
                RebuildLevelingContent();
                Repaint();
            });
            levelSourceContent.Add(linkModeField);
            
            if (item.LinkMode == EItemLinkMode.LinkedToProvider)
            {
                BuildProviderSelector(levelSourceContent, item);
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
                        $"Are you sure you want to link active ability and effects?\n\nThis will affect {1 + item.GrantedEffects.Count} assets.",
                        "Yes", "Cancel"))
                {
                    item.LinkAllChildren();
                
                    // Mark all linked children dirty
                    foreach (var effect in item.GrantedEffects)
                    {
                        if (effect != null) MarkDirty(effect);
                    }
                    if (item.ActiveAbility != null) MarkDirty(item.ActiveAbility);
                
                    MarkDirty(item);
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
                        $"Are you sure you want to unlink active ability and effects?" +
                        $"\n\nThis will affect {(item.ActiveAbility && item.ActiveAbility.IsLinkedTo(this) ? 1 : 0) + item.GrantedEffects?.Count(i => i.IsLinkedTo(this)) ?? 0} assets." +
                        $"\n\nThis change only applies to assets linked to this item.",
                        "Yes", "Cancel"))
                {
                    item.UnlinkAllChildren();
                
                    // Mark all unlinked children dirty
                    foreach (var effect in item.GrantedEffects)
                    {
                        if (effect != null) MarkDirty(effect);
                    }
                    if (item.ActiveAbility != null) MarkDirty(item.ActiveAbility);
                
                    MarkDirty(item);
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
            
            if (item.IsLinked)
            {
                var provider = item.LinkedProvider;
                var providerAsset = item.LinkedProvider;
                
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
                        "Unlink Ability",
                        $"Remove link to '{provider.GetProviderName()}'?\n\nThe ability will become standalone.",
                        "Unlink", "Cancel"))
                    {
                        item.Unlink();
                        serializedObject.Update();
                        MarkDirty(item);
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
        
        private string GetProviderTypeName(ILevelProvider provider)
        {
            return provider switch
            {
                EntityIdentity => "Entity",
                Ability => "Ability",
                Item => "Item",
                _ => "Provider"
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/items/leveling"
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
            
            if (item.LinkMode == EItemLinkMode.LinkedToProvider)
            {
                if (item.IsLinked)
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
            var provider = item.LinkedProvider;
            
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
            
            var hintLabel = CreateHintLabel("Select an asset in the Level Source section above to link this item's levels.");
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
            startingLevelValue.value = item.StartingLevel;
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
            maxLevelValue.value = item.MaxLevel;
            maxLevelValue.SetEnabled(false);
            maxLevelValue.style.flexGrow = 1;
            maxLevelValue.style.opacity = 0.6f;
            maxLevelRow.Add(maxLevelValue);
        }
        
        private void BuildStandaloneLevelingFields()
        {
            // Level row with editable fields
            var levelRow = CreateRow(4);
            levelingContent.Add(levelRow);
            
            var startingLevel = CreatePropertyField(serializedObject.FindProperty(nameof(Item.StartingLevel)), "Level", "Level Range");
            startingLevel.style.flexGrow = 1;
            startingLevel.style.marginRight = 8;
            levelRow.Add(startingLevel);
            
            var maxLevel = CreatePropertyField(serializedObject.FindProperty(nameof(Item.MaxLevel)), "MaxLevel");
            maxLevel.style.flexGrow = 1;
            levelRow.Add(maxLevel);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildEffectsSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Effects",
                Title = "Granted Effects",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/items/effects"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var infoLabel = CreateHintLabel(
                "Effects applied while this item is equipped. Link effects to this item for level scaling.");
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);
            
            var effectsField = CreatePropertyField(serializedObject.FindProperty(nameof(Item.GrantedEffects)), "GrantedEffects", "");
            effectsField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
            });
            content.Add(effectsField);
            
            effectLinkStatusContainer = new VisualElement { name = "EffectLinkStatus" };
            content.Add(effectLinkStatusContainer);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAbilitiesSection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Abilities",
                Title = "Abilities",
                AccentColor = GetAssetTypeColor(),
                HelpUrl = "https://docs.playforge.dev/items/abilities"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var infoLabel = CreateHintLabel(
                "Active ability granted by this item. Link to this item for level scaling.");
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);
            
            var activeSubsection = CreateSubsection("ActiveAbilitySubsection", "Active Ability", Colors.AccentOrange);
            content.Add(activeSubsection);
            
            var abilityField = CreatePropertyField(serializedObject.FindProperty(nameof(Item.ActiveAbility)), "Active Ability");
            abilityField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
            });
            activeSubsection.Add(abilityField);
            
            abilityLinkStatusContainer = new VisualElement { name = "AbilityLinkStatus" };
            activeSubsection.Add(abilityLinkStatusContainer);
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
                HelpUrl = "https://docs.playforge.dev/items/workers"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var hint = CreateHintLabel("Workers are subscribed when item is equipped.");
            content.Add(hint);
            
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty(nameof(Item.WorkerGroup)), 
                "WorkerGroup", 
                ""
            );
            content.Add(workerGroupField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            var result = GenerateAssetTag(item.Definition?.Name, "Item");
            assetTagValueLabel.text = result.result;
            
            if (!result.isUnknown)
            {
                var tags = item.Tags;
                var assetTag = tags.AssetTag;
                assetTag.Name = result.result;
                tags.AssetTag = assetTag;
                item.Tags = tags;
            }
        }

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