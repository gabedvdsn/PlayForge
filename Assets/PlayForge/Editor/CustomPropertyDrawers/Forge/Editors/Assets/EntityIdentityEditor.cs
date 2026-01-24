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

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════
        
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
            if (entity?.Textures != null && entity.Textures.Count > 0)
            {
                var tex = entity.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_EntityIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ENTITY";
        
        protected override Color GetAssetTypeColor() => new Color(0.3f, 0.8f, 0.6f);
        
        protected override string GetDocumentationUrl() => "https://docs.playforge.dev/entities";
        
        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;
        
        protected override void OnVisualize()
        {
            Debug.Log($"Visualize entity: {entity.name}");
        }
        
        protected override void OnImport()
        {
            EditorGUIUtility.ShowObjectPicker<EntityIdentity>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            entity = serializedObject.targetObject as EntityIdentity;
            if (entity == null) return null;

            root = CreateRoot();
            
            var scrollView = CreateScrollView();
            root.Add(scrollView);
            
            BuildDefinitionSection(scrollView);
            BuildTagsSection(scrollView);
            BuildLevelSection(scrollView);
            BuildAbilitiesSection(scrollView);
            BuildItemsSection(scrollView);
            BuildAttributesSection(scrollView);
            BuildWorkersSection(scrollView);
            BuildLocalDataSection(scrollView);
            
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
                AccentColor = Colors.AccentCyan,
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
                Repaint();
            });
            content.Add(nameField);
            
            var descriptionField = CreateTextField("Description", "Description", multiline: true, minHeight: 24);
            descriptionField.value = entity.Description;
            descriptionField.RegisterCallback<FocusOutEvent>(_ =>
            {
                entity.Description = descriptionField.value;
                MarkDirty(entity);
                Repaint();
            });
            content.Add(descriptionField);
            
            var texturesField = CreatePropertyField(
                serializedObject.FindProperty("Textures"), 
                "Textures", 
                "Textures"
            );
            texturesField.style.marginTop = 8;
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
                AccentColor = Colors.AccentYellow,
                HelpUrl = "https://docs.playforge.dev/entities/tags"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            assetTagValueLabel = valueLabel;
            UpdateAssetTagDisplay();
            content.Add(assetTagContainer);
            
            var grantedField = CreatePropertyField(
                serializedObject.FindProperty("GrantedTags"), 
                "GrantedTags", 
                "Granted Tags"
            );
            grantedField.style.marginTop = 6;
            content.Add(grantedField);
            
            var affiliationField = CreatePropertyField(
                serializedObject.FindProperty("Affiliation"), 
                "Affiliation", 
                "Affiliation"
            );
            affiliationField.style.marginTop = 6;
            content.Add(affiliationField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildLevelSection(VisualElement parent)
        {
            var section = CreateCollapsibleSection(new SectionConfig
            {
                Name = "Level",
                Title = "Level",
                AccentColor = Colors.AccentGreen,
                HelpUrl = "https://docs.playforge.dev/entities/level"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var levelField = CreateIntegerField("Level", "Starting Level", entity.Level);
            levelField.RegisterValueChangedCallback(evt =>
            {
                entity.Level = evt.newValue;
                MarkDirty(entity);
            });
            content.Add(levelField);
            
            var row = CreateRow(4);
            content.Add(row);
            
            var maxLevelField = CreateIntegerField("MaxLevel", "Max Level", entity.MaxLevel);
            maxLevelField.style.flexGrow = 1;
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
            
            var relativeLevelLabel = new Label($"Relative Level: {entity.RelativeLevel:P0}");
            relativeLevelLabel.style.fontSize = 10;
            relativeLevelLabel.style.color = Colors.HintText;
            relativeLevelLabel.style.marginTop = 4;
            relativeLevelLabel.style.paddingLeft = 4;
            content.Add(relativeLevelLabel);
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
                AccentColor = Colors.AccentPurple,
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
            
            var row = CreateRow(4);
            content.Add(row);
            
            var maxAbilitiesField = CreateIntegerField("MaxAbilities", "Max Abilities", entity.MaxAbilities);
            maxAbilitiesField.style.flexGrow = 1;
            maxAbilitiesField.style.marginRight = 8;
            maxAbilitiesField.RegisterValueChangedCallback(evt =>
            {
                entity.MaxAbilities = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(maxAbilitiesField);
            
            var allowDupsToggle = CreateToggle("AllowDuplicates", "Allow Duplicates");
            allowDupsToggle.value = entity.AllowDuplicateAbilities;
            allowDupsToggle.style.minWidth = 120;
            allowDupsToggle.RegisterValueChangedCallback(evt =>
            {
                entity.AllowDuplicateAbilities = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(allowDupsToggle);
            
            var startingField = CreatePropertyField(
                serializedObject.FindProperty("StartingAbilities"), 
                "StartingAbilities", 
                "Starting Abilities"
            );
            startingField.style.marginTop = 8;
            content.Add(startingField);
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
                AccentColor = new Color(1f, 0.8f, 0.3f), // Golden yellow
                HelpUrl = "https://docs.playforge.dev/entities/items"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var row = CreateRow(4);
            content.Add(row);
            
            var maxItemsField = CreateIntegerField("MaxItems", "Max Items", entity.MaxItems);
            maxItemsField.style.flexGrow = 1;
            maxItemsField.style.marginRight = 8;
            maxItemsField.RegisterValueChangedCallback(evt =>
            {
                entity.MaxItems = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(maxItemsField);
            
            var allowDupsToggle = CreateToggle("AllowDuplicateItems", "Allow Duplicates");
            allowDupsToggle.value = entity.AllowDuplicateItems;
            allowDupsToggle.style.minWidth = 120;
            allowDupsToggle.RegisterValueChangedCallback(evt =>
            {
                entity.AllowDuplicateItems = evt.newValue;
                MarkDirty(entity);
            });
            row.Add(allowDupsToggle);
            
            var startingField = CreatePropertyField(
                serializedObject.FindProperty("StartingItems"), 
                "StartingItems", 
                "Starting Items"
            );
            startingField.style.marginTop = 8;
            content.Add(startingField);
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
                AccentColor = Colors.AccentBlue,
                HelpUrl = "https://docs.playforge.dev/entities/attributes"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            content.Add(CreatePropertyField(
                serializedObject.FindProperty("AttributeSet"), 
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
                AccentColor = Colors.AccentOrange,
                HelpUrl = "https://docs.playforge.dev/entities/workers"
            });
            parent.Add(section.Section);
            
            var content = section.Content;
            
            var hint = CreateHintLabel("Workers are subscribed when entity is initialized.");
            content.Add(hint);
            
            var workerGroupField = CreatePropertyField(
                serializedObject.FindProperty("WorkerGroup"), 
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
                AccentColor = Colors.AccentGray,
                HelpUrl = "https://docs.playforge.dev/entities/localdata"
            });
            parent.Add(section.Section);
            
            section.Content.Add(CreatePropertyField(
                serializedObject.FindProperty("LocalData"), 
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
            Repaint();
        }
    }
}