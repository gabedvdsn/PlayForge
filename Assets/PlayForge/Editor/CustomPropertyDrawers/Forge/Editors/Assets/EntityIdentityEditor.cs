using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The Entity inspector, in Docs/EDITOR_STYLE.md's terms and the Entity mockup's (ED-37): Identity, Leveling, Tags, the
    /// entity's Attributes, Abilities and Items, Workers and Local Data, then Used By. A collapsed section shows one line,
    /// and the inspector takes its look from the style sheet.
    /// </summary>
    [CustomEditor(typeof(EntityIdentity)), CanEditMultipleObjects]
    public class EntityIdentityEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_EntityIcon;

        private EntityIdentity entity;

        // Its fields write through the SerializedObject, and its Max fields' scalers through each asset's own, so it edits
        // a multiple selection; what acts on one entity steps aside: the name, the asset tag, linking, the attribute table,
        // and Used By (ED-12)
        protected override bool SupportsMultipleObjects => true;

        private IEnumerable<EntityIdentity> Entities => targets.Cast<EntityIdentity>();

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;
        private Toggle equippedTooField;
        private Label equippedTooNote;
        private ForgeAttributeTable attributeTable;

        private SectionResult identitySection;
        private SectionResult levelingSection;
        private SectionResult tagsSection;
        private SectionResult attributesSection;
        private SectionResult abilitiesSection;
        private SectionResult itemsSection;
        private SectionResult workersSection;
        private SectionResult deathSection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration
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

        protected override string GetDocumentationTopic() => "entities";

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
            // The style doc's order: Identity, Leveling, Tags, the entity's own sections, Workers and Local Data, then what
            // uses the entity. Level Source sat apart from Leveling, which was called Level and came after Tags, and the
            // Attribute Set sat in Definition (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildTagsSection(parent);
            BuildAttributesSection(parent);
            BuildAbilitiesSection(parent);
            BuildItemsSection(parent);
            // The entity's own, before Workers and Local Data, as the style doc orders them
            BuildDeathSection(parent);
            BuildWorkersSection(parent);
            BuildLocalDataSection(parent);
            BuildUsedBySection(parent);
            UpdateSummaries();
        }

        // After an Import or a Clear replaces a section's values
        private void OnSectionReplaced()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            UpdateHeader();
            MarkDirty(entity);
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Identity Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildIdentitySection(VisualElement parent)
        {
            identitySection = CreateSection(new SectionConfig
            {
                Name = "Identity",
                Title = "Identity",
                HelpTopic = "entities/definition",

                // Import and Clear leave the name, which identifies the entity and gives its tag (ED-35)
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.Description), nameof(EntityIdentity.Textures) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(identitySection.Section);

            var content = identitySection.Content;

            nameField = CreateTextField("Name", "Name");
            nameField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            nameField.value = entity.Name;
            // Typing previews the tag; leaving the field stores the name and the tag in one undoable step (ED-12)
            nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay(evt.newValue));
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (IsMultiple || nameField.value == (entity.Name ?? "")) return;
                Undo.RecordObject(entity, "Rename Entity");
                entity.Name = nameField.value;
                MarkDirty(entity);
                SyncAssetTag();
                UpdateHeader();
                // Its file follows its name (ED-70)
                RenameFile(entity, nameField.value);
                Repaint();
            });
            // Each entity's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = Entities.Select(each => each.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one entity at a time: each entity's tag comes from its own name.";
            }
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset when it lost focus,
            // with no Undo (ED-12).
            content.Add(CreateBoundTextField(nameof(EntityIdentity.Description), "Description", multiline: true, minHeight: 18));

            // The tag the name gives, under the name, as the mockup places it. It sat at the top of Tags, in a tinted box
            // (ED-37). Each entity has its own, so a multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                assetTagField = new ForgeAssetTagField();
                content.Add(assetTagField);
                UpdateAssetTagDisplay();
            }

            var texturesField = CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.Textures)), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section: the entity's levels, and where they come from
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelingSection(VisualElement parent)
        {
            levelingSection = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                HelpTopic = "entities/leveling",

                // The entity's own range. A link is made in the section, which refuses one that would make a loop
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.StartingLevel), nameof(EntityIdentity.MaxLevel) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                GetDefaultValue = path => path switch
                {
                    nameof(EntityIdentity.StartingLevel) => 1,
                    nameof(EntityIdentity.MaxLevel) => 99,
                    _ => null,
                },
            });
            parent.Add(levelingSection.Section);

            levelingContent = levelingSection.Content;
            RebuildLevelingContent();
        }

        // Leveling holds what Level Source did (ED-37), so the base editor's two rebuilds are one here
        protected override void RebuildLevelSourceContent() => RebuildLevelingContent();

        // Link Mode, then the entity's own range, or its provider and the range the provider gives, then its children's
        // links. Level Source drew the link state in tinted boxes with colored borders, under sentences about linking
        // (ED-37)
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;

            levelingContent.Clear();
            shownLeveling = LevelingState();

            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it. It wrote the mode
            // straight into the asset, with no Undo, and redrew only then (ED-12).
            var linkModeField = new EnumField("Link Mode", entity.LinkMode) { name = "LinkMode", bindingPath = nameof(EntityIdentity.LinkMode) };
            linkModeField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            levelingContent.Add(linkModeField);

            // A provider and a child's link belong to one entity, so a multiple selection sets the mode, and edits the
            // range when every entity keeps its own; a linked entity's range comes from its provider (ED-12)
            if (IsMultiple)
            {
                if (Entities.All(each => each.LinkMode != EItemLinkMode.LinkedToProvider)) BuildOwnLevels();
                else levelingContent.Add(CreateNote("Each linked entity takes its levels from its own provider."));
                levelingContent.Bind(serializedObject);
                return;
            }

            // The range in use while linked: the provider's, or the entity's own while it has none
            if (IsLinkMode) AddLinkedLevels(levelingContent, entity, "entity", entity.GetLinkedStartingLevel(), entity.GetLinkedMaxLevel());
            else BuildOwnLevels();

            // Links the entity's starting items and abilities to it, so they take its levels, or removes those links
            levelingContent.Add(CreateChildLinks(entity, "entity", "Items & Abilities", "starting items and abilities",
                Children, entity.LinkAllChildren, entity.UnlinkAllChildren));
            levelingContent.Bind(serializedObject);
        }

        private bool IsLinkMode => entity.LinkMode == EItemLinkMode.LinkedToProvider;

        // The entity's own range, which it keeps while standalone. Starting and Max Level bind to their properties, so an
        // edit undoes; they wrote straight into the asset, with no Undo (ED-12). Max Level is on a line of its own: the
        // Cap At Max toggle that shared its row set a value nothing read (GAS-45)
        private void BuildOwnLevels()
        {
            levelingContent.Add(BoundInteger(nameof(EntityIdentity.StartingLevel), entity.StartingLevel, "Starting Level"));
            levelingContent.Add(BoundInteger(nameof(EntityIdentity.MaxLevel), entity.MaxLevel, "Max Level"));
        }

        private static IntegerField BoundInteger(string path, int value, string label)
        {
            var field = new IntegerField(label) { name = path, bindingPath = path };
            field.SetValueWithoutNotify(value);
            field.AddToClassList(IntegerField.alignedFieldUssClassName);
            return field;
        }

        // What Leveling is drawn from. A multiple selection's is whether every entity keeps its own range
        private string LevelingState() => IsMultiple
            ? Entities.All(each => each.LinkMode != EItemLinkMode.LinkedToProvider) ? "own" : "linked"
            : LevelingState(entity, IsLinkMode, entity.GetLinkedStartingLevel(), entity.GetLinkedMaxLevel());

        // The entity's starting items and abilities, which can take its levels
        private IEnumerable<BaseForgeLevelProvider> Children() =>
            (entity.StartingItems ?? new List<StartingItemContainer>()).Where(start => start?.Item != null)
            .Select(start => (BaseForgeLevelProvider)start.Item)
            .Concat((entity.StartingAbilities ?? new List<Ability>()).Where(ability => ability != null));

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildTagsSection(VisualElement parent)
        {
            tagsSection = CreateSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                HelpTopic = "entities/tags",

                // Import and Clear leave the asset tag, which the name gives
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.Affiliation), nameof(EntityIdentity.ContextTags), nameof(EntityIdentity.GrantedTags) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(tagsSection.Section);

            // Affiliation first, as the mockup draws it: an entity's team decides what its effects can reach
            var content = tagsSection.Content;
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.Affiliation)), "Affiliation", "Affiliation"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.ContextTags)), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.GrantedTags)), "GrantedTags", "Granted Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes, Abilities and Items Sections: what the entity starts with
        // ═══════════════════════════════════════════════════════════════════════════

        // The set the entity's attributes come from, then the table of what it gives the entity, as the mockup draws them.
        // The set sat in Definition, and the Attributes section was left out (ED-37)
        private void BuildAttributesSection(VisualElement parent)
        {
            attributesSection = CreateSection(new SectionConfig
            {
                Name = "Attributes",
                Title = "Attributes",
                HelpTopic = "entities/attributes",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.AttributeSet), nameof(EntityIdentity.TimeScaleAttribute) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(attributesSection.Section);

            attributesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.AttributeSet)), "AttributeSet", "Attribute Set"));

            // The attribute its time runs at, which a slow or a haste changes (FEAT-22)
            attributesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.TimeScaleAttribute)), "TimeScaleAttribute", "Time Scale"));

            // The table shows one entity's set (ED-12)
            if (IsMultiple)
            {
                attributesSection.Content.Add(CreateNote("Select one entity to see what its set gives."));
                return;
            }
            attributeTable = new ForgeAttributeTable();
            attributeTable.Changed += UpdateSummaries;
            attributesSection.Content.Add(attributeTable);
            // At the entity's levels, as it starts with them (ED-49)
            attributeTable.Show(entity.AttributeSet, entity);
        }

        private void BuildAbilitiesSection(VisualElement parent)
        {
            abilitiesSection = CreateSection(new SectionConfig
            {
                Name = "Abilities",
                Title = "Abilities",
                HelpTopic = "entities/abilities",

                // Import and Clear leave Max Abilities, whose scaler the Import window doesn't copy yet (ED-32, ED-33)
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    nameof(EntityIdentity.ActivationPolicy), nameof(EntityIdentity.StartingAbilities), nameof(EntityIdentity.AllowDuplicateAbilities),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                GetDefaultValue = path => path switch
                {
                    nameof(EntityIdentity.ActivationPolicy) => (int)EAbilityActivationPolicy.QueueActivationIfBusy,
                    _ => null,
                },
            });
            parent.Add(abilitiesSection.Section);

            var content = abilitiesSection.Content;

            // Activation Policy and Allow Duplicates bind to their properties, so an edit undoes. They wrote straight into
            // the asset, with no Undo (ED-12).
            // When Busy, as the mockup names it, with its choices by their own names, such as Queue (ED-37)
            var policyField = new EnumField("When Busy", entity.ActivationPolicy)
            {
                name = nameof(EntityIdentity.ActivationPolicy),
                bindingPath = nameof(EntityIdentity.ActivationPolicy),
                tooltip = "What happens when an ability set to the entity's policy is activated while another runs",
            };
            policyField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            content.Add(policyField);

            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.StartingAbilities)), "StartingAbilities", "Starting Abilities"));
            content.Add(new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxAbilitiesOperation)), ""));
            content.Add(BoundToggle("AllowDuplicates", nameof(EntityIdentity.AllowDuplicateAbilities), entity.AllowDuplicateAbilities,
                "Allow Duplicates", "Let the entity hold the same ability more than once"));
        }

        private void BuildItemsSection(VisualElement parent)
        {
            itemsSection = CreateSection(new SectionConfig
            {
                Name = "Items",
                Title = "Items",
                HelpTopic = "entities/items",

                // Import and Clear leave Max Items and Max Equipped Items, whose scalers the Import window doesn't copy yet
                // (ED-32, ED-33)
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    nameof(EntityIdentity.StartingItems), nameof(EntityIdentity.AllowDuplicateItems), nameof(EntityIdentity.AllowDuplicateEquippedItems),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(itemsSection.Section);

            var content = itemsSection.Content;

            // Each starting item takes one row, with its Equip checkbox (StartingItemDrawer)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.StartingItems)), "StartingItems", "Starting Items"));
            content.Add(new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxItemsOperation)), ""));
            content.Add(new PropertyField(serializedObject.FindProperty(nameof(EntityIdentity.MaxEquippedItemsOperation)), ""));

            // The Allow Duplicate toggles bind to their properties, so an edit undoes (ED-12)
            content.Add(BoundToggle("AllowDuplicateItems", nameof(EntityIdentity.AllowDuplicateItems), entity.AllowDuplicateItems,
                "Allow Duplicates", "Let the inventory hold the same item more than once"));

            // An inventory that holds one of each item never has a second copy to equip, so Equipped Too waits for Allow
            // Duplicates, indented under it, with a note beside it, outside the disabled toggle so it stays legible (ED-37).
            // It opened showing Allow Duplicate Items' value (ED-29)
            equippedTooField = BoundToggle("AllowDuplicateEquippedItems", nameof(EntityIdentity.AllowDuplicateEquippedItems),
                entity.AllowDuplicateEquippedItems, "Equipped Too", "Let more than one copy of the same item be equipped at once");
            equippedTooField.AddToClassList("pf-dependent");
            equippedTooNote = new Label("Needs Allow Duplicates");
            equippedTooNote.AddToClassList("pf-field-note");
            var equippedTooRow = new VisualElement { name = "EquippedTooRow" };
            equippedTooRow.AddToClassList("pf-field-row");
            equippedTooRow.Add(equippedTooField);
            equippedTooRow.Add(equippedTooNote);
            content.Add(equippedTooRow);
            UpdateEquippedToo();
        }

        private static Toggle BoundToggle(string name, string path, bool value, string label, string tooltip)
        {
            var toggle = new Toggle(label) { name = name, bindingPath = path, tooltip = tooltip };
            toggle.SetValueWithoutNotify(value);
            toggle.AddToClassList(Toggle.alignedFieldUssClassName);
            return toggle;
        }

        // Equipped Too waits for Allow Duplicates: in a multiple selection, on every entity (ED-12)
        private void UpdateEquippedToo()
        {
            if (equippedTooField == null) return;
            bool allowed = Entities.All(each => each.AllowDuplicateItems);
            equippedTooField.SetEnabled(allowed);
            equippedTooNote.style.display = allowed ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "entities/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.WorkerGroup) },
                ImportAcrossTypes = true,
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.WorkerGroup)), "WorkerGroup", ""));
        }

        // What its death does: End, or Stay with the effects it ends and takes, and its body (TW-4)
        private void BuildDeathSection(VisualElement parent)
        {
            deathSection = CreateSection(new SectionConfig
            {
                Name = "Death",
                Title = "Death",
                HelpTopic = "entities/death",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.Death) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(deathSection.Section);

            deathSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.Death)), "Death", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Local Data Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "entities/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(EntityIdentity.LocalData) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(EntityIdentity.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Used By Section
        // ═══════════════════════════════════════════════════════════════════════════

        // The assets that use the entity, such as the abilities and items linked to it, each a row that selects it (ED-37)
        private void BuildUsedBySection(VisualElement parent)
        {
            usedBySection = CreateSection(new SectionConfig
            {
                Name = "UsedBy",
                Title = "Used By",
                IncludeButtons = new[] { false, false, false },
            });
            parent.Add(usedBySection.Section);

            if (IsMultiple)
            {
                usedBySection.Content.Add(CreateNote("Select one entity to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(entity);
            usedBy.Changed += UpdateSummaries;
            usedBySection.Content.Add(usedBy);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Changes, and the line each collapsed section shows
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void OnAssetChanged()
        {
            // Leveling is drawn from the link mode and the provider, so it redraws when either changes, an Undo among
            // them (ED-12)
            if (levelingContent != null && LevelingState() != shownLeveling) RebuildLevelingContent();
            // The name field isn't bound, so it follows an Undo here, unless it's being typed in
            if (!IsMultiple && nameField != null && !IsFocused(nameField) && nameField.value != (entity.Name ?? ""))
                nameField.SetValueWithoutNotify(entity.Name ?? "");
            UpdateAssetTagDisplay();
            UpdateEquippedToo();
            // The table follows its set's own changes; here it follows the entity to another set, or to other levels, which
            // its values are at (ED-49)
            if (attributeTable != null && (attributeTable.Set != entity.AttributeSet || attributeTable.Levels != AttributeStartValues.LevelsOf(entity)))
                attributeTable.Show(entity.AttributeSet, entity);
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        private void UpdateSummaries()
        {
            // A multiple selection's entities differ, so one line would describe only the first
            if (IsMultiple)
            {
                var sections = new[] { identitySection, levelingSection, tagsSection, attributesSection, abilitiesSection, itemsSection, workersSection, localDataSection, usedBySection };
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (levelingSection != null)
                levelingSection.Summary.text = LevelingSummary(entity, IsLinkMode, entity.GetLinkedStartingLevel(), entity.GetLinkedMaxLevel());
            if (tagsSection != null) tagsSection.Summary.text = TagsSummaryOf();
            if (attributesSection != null) attributesSection.Summary.text = AttributesSummary();
            if (abilitiesSection != null)
                abilitiesSection.Summary.text = CountOf(entity.StartingAbilities?.Count(ability => ability != null) ?? 0, "ability", "abilities");
            if (itemsSection != null) itemsSection.Summary.text = ItemsSummary();
            if (workersSection != null) workersSection.Summary.text = CountOf(WorkerCount(entity.WorkerGroup), "worker", "workers");
            if (localDataSection != null) localDataSection.Summary.text = CountOf(entity.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // The affiliation, or else the context tags, and how many tags the entity grants, as in "Team.Goblins · 1 granted"
        private string TagsSummaryOf()
        {
            var granted = entity.GrantedTags?.Count(tag => tag.IsValid) ?? 0;
            return entity.Affiliation?.Any(tag => tag.IsValid) == true
                ? TagsSummary(entity.Affiliation, "affiliations", granted)
                : TagsSummary(entity.ContextTags, "context tags", granted);
        }

        // The set and how many attributes it gives, its sub-sets' among them, as in "Goblin Stats · 4 attributes"
        private string AttributesSummary()
        {
            var set = entity.AttributeSet;
            if (set == null) return "None";
            var count = attributeTable?.Count ?? set.GetUnique().Count(attribute => attribute is Object asset ? asset != null : attribute != null);
            return $"{NameOf(set)} · {CountOf(count, "attribute", "attributes")}";
        }

        // How many starting items, and how many of them it equips as it's given, as in "3 items · 1 equipped"
        private string ItemsSummary()
        {
            var starting = entity.StartingItems?.Where(start => start?.Item != null).ToList() ?? new List<StartingItemContainer>();
            var equipped = starting.Count(start => start.EquipOnInit);
            var items = CountOf(starting.Count, "item", "items");
            return equipped == 0 ? items : $"{items} · {equipped} equipped";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        // Shows the tag a name gives: the stored name's, or the one being typed (ED-12)
        private void UpdateAssetTagDisplay(string name = null) =>
            ShowAssetTag(assetTagField, name ?? entity.Name, "Entity", entity.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(entity, entity.Name, "Entity", entity.AssetTag, tag => entity.AssetTag = tag);
            UpdateAssetTagDisplay();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            RebuildLevelingContent();
            UpdateEquippedToo();
            UpdateSummaries();
            Repaint();
        }
    }
}
