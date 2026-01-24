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
        
        private int _pickerControlId;
        private bool _waitingForPicker;
        private Object _lastPickedObject;
        private System.Type _pickerType;
        private System.Action<Object> _pickerCallback;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════
        
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
        
        protected override Color GetAssetTypeColor() => new Color(1f, 0.8f, 0.3f);
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/items";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        protected override void OnVisualize()
        {
            Debug.Log($"Visualize item: {item.name}");
        }
        
        protected override void OnImport()
        {
            EditorGUIUtility.ShowObjectPicker<Item>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
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
        
        private void OnEditorUpdate()
        {
            if (!_waitingForPicker) return;
            
            var currentControlId = EditorGUIUtility.GetObjectPickerControlID();
            
            if (currentControlId == 0)
            {
                _waitingForPicker = false;
                
                if (_lastPickedObject != null && _pickerCallback != null)
                {
                    _pickerCallback(_lastPickedObject);
                    serializedObject.Update();
                    MarkDirty(item);
                    Repaint();
                }
                _lastPickedObject = null;
                _pickerCallback = null;
            }
            else if (currentControlId == _pickerControlId)
            {
                _lastPickedObject = EditorGUIUtility.GetObjectPickerObject();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            item = serializedObject.targetObject as Item;
            if (item == null) return null;

            root = CreateRoot();
            
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            BuildDefinitionSection(scrollView);
            BuildTagsSection(scrollView);
            BuildLevelingSection(scrollView);
            BuildEffectsSection(scrollView);
            BuildAbilitiesSection(scrollView);
            BuildWorkersSection(scrollView);
            
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
                HelpUrl = "https://docs.playforge.dev/items/definition"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var definition = serializedObject.FindProperty("Definition");
            
            var nameField = CreateTextField("Name", "Name");
            nameField.value = item.Definition.Name;
            nameField.RegisterValueChangedCallback(_ => UpdateAssetTagDisplay());
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                item.Definition.Name = nameField.value;
                UpdateAssetTagDisplay();
                MarkDirty(item);
                Repaint();
            });
            content.Add(nameField);
            
            var descField = CreateTextField("Description", "Description", multiline: true, minHeight: 40);
            descField.value = item.Definition.Description;
            descField.RegisterCallback<FocusOutEvent>(_ =>
            {
                item.Definition.Description = descField.value;
                MarkDirty(item);
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
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildTagsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                AccentColor = Colors.AccentGreen,
                HelpUrl = "https://docs.playforge.dev/items/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            var tags = serializedObject.FindProperty("Tags");
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            content.Add(CreatePropertyField(tags.FindPropertyRelative("ContextTags"), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(tags.FindPropertyRelative("PassiveGrantedTags"), "PassiveGrantedTags", "Granted Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelingSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                AccentColor = Colors.AccentPurple,
                HelpUrl = "https://docs.playforge.dev/items/leveling"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var row = CreateRow(4);
            content.Add(row);
            
            var startingLevel = CreatePropertyField(serializedObject.FindProperty("StartingLevel"), "StartingLevel", "Starting Level");
            startingLevel.style.flexGrow = 1;
            startingLevel.style.marginRight = 8;
            row.Add(startingLevel);
            
            var maxLevel = CreatePropertyField(serializedObject.FindProperty("MaxLevel"), "MaxLevel", "Max Level");
            maxLevel.style.flexGrow = 1;
            row.Add(maxLevel);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildEffectsSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Effects",
                Title = "Granted Effects",
                AccentColor = Colors.AccentOrange,
                HelpUrl = "https://docs.playforge.dev/items/effects"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var infoLabel = CreateHintLabel(
                "Effects applied while this item is equipped. Effects can be linked to this item for level scaling.");
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);
            
            var effectsField = CreatePropertyField(serializedObject.FindProperty("GrantedEffects"), "GrantedEffects", "");
            content.Add(effectsField);
            
            BuildEffectLinkStatus(content);
        }
        
        private void BuildEffectLinkStatus(VisualElement parent)
        {
            if (item.GrantedEffects == null || item.GrantedEffects.Count == 0) return;
            
            var statusContainer = new VisualElement();
            statusContainer.style.marginTop = 8;
            statusContainer.style.paddingLeft = 4;
            parent.Add(statusContainer);
            
            var headerLabel = new Label("Link Status:");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.HintText;
            headerLabel.style.marginBottom = 4;
            statusContainer.Add(headerLabel);
            
            foreach (var effect in item.GrantedEffects)
            {
                if (effect == null) continue;
                
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 2;
                
                var isLinked = effect.IsLinkedTo(item);
                var icon = new Label(isLinked ? "✓" : "○");
                icon.style.width = 16;
                icon.style.color = isLinked ? Colors.AccentGreen : Colors.HintText;
                row.Add(icon);
                
                var nameLabel = new Label(effect.GetName());
                nameLabel.style.fontSize = 10;
                nameLabel.style.color = Colors.LabelText;
                nameLabel.style.flexGrow = 1;
                row.Add(nameLabel);
                
                if (!isLinked)
                {
                    var linkBtn = new Button(() =>
                    {
                        effect.LinkToProvider(item);
                        MarkDirty(effect);
                        MarkDirty(item);
                        Repaint();
                    });
                    linkBtn.text = "Link";
                    linkBtn.style.fontSize = 9;
                    linkBtn.style.height = 16;
                    linkBtn.style.paddingLeft = 4;
                    linkBtn.style.paddingRight = 4;
                    ApplyButtonHoverStyle(linkBtn);
                    row.Add(linkBtn);
                }
                
                statusContainer.Add(row);
            }
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
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/items/abilities"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var infoLabel = CreateHintLabel(
                "Abilities granted by this item. Abilities can be linked to this item for level scaling.");
            infoLabel.style.marginBottom = 8;
            content.Add(infoLabel);
            
            var activeSubsection = CreateSubsection("ActiveAbilitySubsection", "Active Ability", Colors.AccentOrange);
            content.Add(activeSubsection);
            
            activeSubsection.Add(CreatePropertyField(serializedObject.FindProperty("ActiveAbility"), "ActiveAbility", ""));
            
            if (item.ActiveAbility != null)
            {
                BuildAbilityLinkStatus(activeSubsection, item.ActiveAbility);
            }
        }
        
        private void BuildAbilityLinkStatus(VisualElement parent, Ability ability)
        {
            if (ability == null) return;
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            row.style.paddingLeft = 4;
            
            var isLinked = ability.IsLinkedTo(item);
            var icon = new Label(isLinked ? "✓" : "○");
            icon.style.width = 16;
            icon.style.color = isLinked ? Colors.AccentGreen : Colors.HintText;
            row.Add(icon);
            
            var statusLabel = new Label(isLinked ? "Linked to this item" : "Not linked");
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = isLinked ? Colors.AccentGreen : Colors.HintText;
            statusLabel.style.flexGrow = 1;
            row.Add(statusLabel);
            
            if (!isLinked)
            {
                var linkBtn = new Button(() =>
                {
                    ability.LinkToProvider(item);
                    MarkDirty(ability);
                    MarkDirty(item);
                    Repaint();
                });
                linkBtn.text = "Link";
                linkBtn.style.fontSize = 9;
                linkBtn.style.height = 16;
                linkBtn.style.paddingLeft = 4;
                linkBtn.style.paddingRight = 4;
                ApplyButtonHoverStyle(linkBtn);
                row.Add(linkBtn);
            }
            else
            {
                var unlinkBtn = new Button(() =>
                {
                    ability.Unlink();
                    MarkDirty(ability);
                    MarkDirty(item);
                    Repaint();
                });
                unlinkBtn.text = "Unlink";
                unlinkBtn.style.fontSize = 9;
                unlinkBtn.style.height = 16;
                unlinkBtn.style.paddingLeft = 4;
                unlinkBtn.style.paddingRight = 4;
                unlinkBtn.style.backgroundColor = new Color(0.5f, 0.3f, 0.3f);
                ApplyButtonHoverStyle(unlinkBtn);
                row.Add(unlinkBtn);
            }
            
            parent.Add(row);
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
                AccentColor = Colors.AccentCyan,
                HelpUrl = "https://docs.playforge.dev/items/workers"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var hint = CreateHintLabel("Workers are subscribed when item is equipped.");
            content.Add(hint);
            
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty("WorkerGroup"), 
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
            Repaint();
        }
    }
}