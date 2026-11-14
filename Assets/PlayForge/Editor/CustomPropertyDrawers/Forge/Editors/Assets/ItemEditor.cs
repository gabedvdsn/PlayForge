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
    /// The Item inspector, in Docs/EDITOR_STYLE.md's terms (ED-37): Identity, Leveling, Tags, the item's Effects and
    /// Abilities, Workers and Local Data, then Used By. A collapsed section shows one line, and the inspector takes its
    /// look from the style sheet.
    /// </summary>
    [CustomEditor(typeof(Item)), CanEditMultipleObjects]
    public class ItemEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_ItemIcon;

        private Item item;

        // Its fields write through the SerializedObject, so it edits a multiple selection, and what acts on one item steps
        // aside: the name, the asset tag, linking, and a linked item's levels (ED-12)
        protected override bool SupportsMultipleObjects => true;

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;

        private SectionResult identitySection;
        private SectionResult levelingSection;
        private SectionResult tagsSection;
        private SectionResult effectsSection;
        private SectionResult abilitiesSection;
        private SectionResult componentsSection;
        private SectionResult workersSection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        private IEnumerable<Item> Items => targets.Cast<Item>();

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration
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

        protected override string GetDocumentationTopic() => "items";

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
            // The style doc's order: Identity, Leveling, Tags, the item's own sections, Workers and Local Data, then what
            // uses the item. Level Source sat apart from Leveling, after Identity, and Tags came before Leveling (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildTagsSection(parent);
            BuildEffectsSection(parent);
            BuildAbilitiesSection(parent);
            BuildComponentsSection(parent);
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
            MarkDirty(item);
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
                HelpTopic = "items/definition",

                // Import and Clear leave the name, which identifies the item and gives its tag (ED-35)
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    DefinitionPath(nameof(ItemDefinition.Description)), DefinitionPath(nameof(ItemDefinition.Visibility)),
                    DefinitionPath(nameof(ItemDefinition.MaxStackSize)), DefinitionPath(nameof(ItemDefinition.Textures)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(identitySection.Section);

            var content = identitySection.Content;
            var definition = serializedObject.FindProperty(nameof(Item.Definition));

            nameField = CreateTextField("Name", "Name");
            nameField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            nameField.value = item.Definition.Name;
            // Typing previews the tag; leaving the field stores the name and the tag in one undoable step (ED-12)
            nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay(evt.newValue));
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (IsMultiple || nameField.value == (item.Definition.Name ?? "")) return;
                Undo.RecordObject(item, "Rename Item");
                item.Definition.Name = nameField.value;
                SyncAssetTag();
                UpdateHeader();
                MarkDirty(item);
                // Its file follows its name (ED-70)
                RenameFile(item, nameField.value);
                Repaint();
            });
            // Each item's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = Items.Select(each => each.Definition.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one item at a time: each item's tag comes from its own name.";
            }
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset when it lost focus,
            // with no Undo (ED-12).
            content.Add(CreateBoundTextField(DefinitionPath(nameof(ItemDefinition.Description)),
                "Description", multiline: true, minHeight: 18, name: "Description"));

            // The tag the name gives, under the name, as the mockups place it. It sat at the top of Tags, in a tinted box
            // (ED-37). Each item has its own, so a multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                assetTagField = new ForgeAssetTagField();
                content.Add(assetTagField);
                UpdateAssetTagDisplay();
            }

            content.Add(CreatePropertyField(definition.FindPropertyRelative(nameof(ItemDefinition.Visibility)), "Visibility", "Visibility"));

            // How many one slot holds, which nothing read and the inspector didn't draw (FEAT-14, GAS-51)
            var stackSize = CreatePropertyField(definition.FindPropertyRelative(nameof(ItemDefinition.MaxStackSize)), "MaxStackSize", "Max Stack Size");
            stackSize.tooltip = "How many of the item one slot holds. 1 or less, and each is a slot of its own";
            content.Add(stackSize);

            var texturesField = CreatePropertyField(definition.FindPropertyRelative(nameof(ItemDefinition.Textures)), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);

            // No Allow Duplicates toggle: the entity's Allow Duplicate Items decides, and nothing read the item's (GAS-45)
        }

        private static string DefinitionPath(string field) => $"{nameof(Item.Definition)}.{field}";

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section: the item's levels, and where they come from
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelingSection(VisualElement parent)
        {
            levelingSection = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                HelpTopic = "items/leveling",

                // The item's own range. A link is made in the section, which refuses one that would make a loop
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.StartingLevel), nameof(Item.MaxLevel) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                GetDefaultValue = path => path switch
                {
                    nameof(Item.StartingLevel) => 1,
                    nameof(Item.MaxLevel) => 4,
                    _ => null,
                },
            });
            parent.Add(levelingSection.Section);

            levelingContent = levelingSection.Content;
            RebuildLevelingContent();
        }

        // Leveling holds what Level Source did (ED-37), so the base editor's two rebuilds are one here
        protected override void RebuildLevelSourceContent() => RebuildLevelingContent();

        // Link Mode, then the item's own range, or its provider and the range the provider gives, then its children's
        // links. Level Source drew the link state in tinted boxes with colored borders, under sentences about linking
        // (ED-37)
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;

            levelingContent.Clear();
            shownLeveling = LevelingState();

            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it. It wrote the mode
            // straight into the asset, with no Undo, and redrew only then (ED-12).
            var linkModeField = new EnumField("Link Mode", item.LinkMode) { name = "LinkMode", bindingPath = nameof(Item.LinkMode) };
            linkModeField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            levelingContent.Add(linkModeField);

            // A provider and a child's link belong to one item, so a multiple selection sets the mode, and edits the range
            // when every item keeps its own; a linked item's range comes from its provider, which differs by item (ED-12)
            if (IsMultiple)
            {
                if (Items.All(each => each.LinkMode == EItemLinkMode.Standalone)) BuildOwnLevels();
                else levelingContent.Add(CreateNote("Each linked item takes its levels from its own provider."));
                levelingContent.Bind(serializedObject);
                return;
            }

            // The range in use while linked: the provider's, or the item's own while it has none
            if (IsLinkMode) AddLinkedLevels(levelingContent, item, "item", item.GetStartingLevel(), item.GetMaxLevel());
            else BuildOwnLevels();

            // Links the item's active ability and granted effects to it, so they take its levels, or removes those links
            levelingContent.Add(CreateChildLinks(item, "item", "Ability & Effects", "active ability and granted effects",
                Children, item.LinkAllChildren, item.UnlinkAllChildren));
            levelingContent.Bind(serializedObject);
        }

        private bool IsLinkMode => item.LinkMode == EItemLinkMode.LinkedToProvider;

        // The item's own range, which it keeps while standalone
        private void BuildOwnLevels()
        {
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.StartingLevel)), "StartingLevel", "Starting Level"));
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.MaxLevel)), "MaxLevel", "Max Level"));
        }

        // What Leveling is drawn from. A multiple selection's is whether every item keeps its own range
        private string LevelingState() => IsMultiple
            ? Items.All(each => each.LinkMode == EItemLinkMode.Standalone) ? "own" : "linked"
            : LevelingState(item, IsLinkMode, item.GetStartingLevel(), item.GetMaxLevel());

        // The item's active ability and granted effects, which can take its levels
        private IEnumerable<BaseForgeLevelProvider> Children() =>
            (item.GrantedEffects ?? new List<GameplayEffect>()).Where(effect => effect != null).Cast<BaseForgeLevelProvider>()
            .Concat(item.ActiveAbility != null ? new BaseForgeLevelProvider[] { item.ActiveAbility } : new BaseForgeLevelProvider[0]);

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildTagsSection(VisualElement parent)
        {
            tagsSection = CreateSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                HelpTopic = "items/tags",

                // Import and Clear leave the asset tag, which the name gives
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    $"{nameof(Item.Tags)}.{nameof(ItemTags.ContextTags)}", $"{nameof(Item.Tags)}.{nameof(ItemTags.PassiveGrantedTags)}",
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(tagsSection.Section);

            var content = tagsSection.Content;
            var tags = serializedObject.FindProperty(nameof(Item.Tags));
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(ItemTags.ContextTags)), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(tags.FindPropertyRelative(nameof(ItemTags.PassiveGrantedTags)), "PassiveGrantedTags", "Granted Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects and Abilities Sections: what the item grants while it's equipped
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildEffectsSection(VisualElement parent)
        {
            effectsSection = CreateSection(new SectionConfig
            {
                Name = "Effects",
                Title = "Effects",
                HelpTopic = "items/effects",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.GrantedEffects) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(effectsSection.Section);

            effectsSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.GrantedEffects)), "GrantedEffects", "Granted Effects"));
        }

        private void BuildAbilitiesSection(VisualElement parent)
        {
            abilitiesSection = CreateSection(new SectionConfig
            {
                Name = "Abilities",
                Title = "Abilities",
                HelpTopic = "items/abilities",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.ActiveAbility) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(abilitiesSection.Section);

            abilitiesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.ActiveAbility)), "ActiveAbility", "Active Ability"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers Section
        // ═══════════════════════════════════════════════════════════════════════════

        // Its components: features it carries, each a class with hooks into its life, added from the list's + (FN-14)
        private void BuildComponentsSection(VisualElement parent)
        {
            componentsSection = CreateSection(new SectionConfig
            {
                Name = "Components",
                Title = "Components",
                HelpTopic = "items/components",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.Components) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(componentsSection.Section);

            // Each item keeps its own components, objects a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                componentsSection.Content.Add(CreateNote("Select one item to edit its components."));
                return;
            }
            componentsSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.Components)), "Components", ""));
        }

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "items/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.OnHit), nameof(Item.Modifiers), nameof(Item.WorkerGroup) },
                ImportAcrossTypes = true,
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            // The effects its holder's hits apply (TW-6)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.OnHit)), "OnHit", "On Hit"));
            // How it changes other effects, its holder's or those landing on it (TW-7)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.Modifiers)), "Modifiers", "Modifiers"));
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.WorkerGroup)), "WorkerGroup", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Local Data Section
        // ═══════════════════════════════════════════════════════════════════════════

        // Items inherit Local Data, as every asset does, but this inspector drew none, so it couldn't be edited (ED-23)
        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "items/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.LocalData) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Used By Section
        // ═══════════════════════════════════════════════════════════════════════════

        // The assets that use the item, such as an entity that starts with it or an effect linked to it, each a row that
        // selects it, as the Attribute inspector lists its own (ED-15, ED-37)
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
                usedBySection.Content.Add(CreateNote("Select one item to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(item);
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
            if (!IsMultiple && nameField != null && !IsFocused(nameField) && nameField.value != (item.Definition.Name ?? ""))
                nameField.SetValueWithoutNotify(item.Definition.Name ?? "");
            UpdateAssetTagDisplay();
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        private void UpdateSummaries()
        {
            var sections = new[] { identitySection, levelingSection, tagsSection, effectsSection, abilitiesSection, componentsSection, workersSection, localDataSection, usedBySection };
            // A multiple selection's items differ, so one line would describe only the first
            if (IsMultiple)
            {
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (identitySection != null) identitySection.Summary.text = TagDisplaySettings.PathOf(item.Definition.Visibility);
            if (levelingSection != null)
                levelingSection.Summary.text = LevelingSummary(item, IsLinkMode, item.GetStartingLevel(), item.GetMaxLevel());
            if (tagsSection != null)
                tagsSection.Summary.text = TagsSummary(item.Tags.ContextTags, "context tags", item.Tags.PassiveGrantedTags?.Count(tag => tag.IsValid) ?? 0);
            if (effectsSection != null) effectsSection.Summary.text = CountOf(item.GrantedEffects?.Count(effect => effect != null) ?? 0, "effect", "effects");
            if (abilitiesSection != null) abilitiesSection.Summary.text = item.ActiveAbility != null ? NameOf(item.ActiveAbility) : "None";
            if (componentsSection != null) componentsSection.Summary.text = CountOf(item.Components?.Count(each => each != null) ?? 0, "component", "components");
            if (workersSection != null) workersSection.Summary.text = WorkersLine(item.OnHit, item.Modifiers, WorkerCount(item.WorkerGroup));
            if (localDataSection != null) localDataSection.Summary.text = CountOf(item.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        // Shows the tag a name gives: the stored name's, or the one being typed (ED-12)
        private void UpdateAssetTagDisplay(string name = null) =>
            ShowAssetTag(assetTagField, name ?? item.Definition?.Name, "Item", item.Tags.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(item, item.Definition?.Name, "Item", item.Tags.AssetTag, tag =>
            {
                var tags = item.Tags;
                tags.AssetTag = tag;
                item.Tags = tags;
            });
            UpdateAssetTagDisplay();
        }

        protected override void Refresh()
        {
            serializedObject.Update();
            UpdateAssetTagDisplay();
            RebuildLevelingContent();
            UpdateSummaries();
            Repaint();
        }
    }
}
