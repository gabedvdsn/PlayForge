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
            // The style doc's order: Identity, Leveling, Tags, the item's own sections, Workers and Local Data, then what
            // uses the item. Level Source sat apart from Leveling, after Identity, and Tags came before Leveling (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildTagsSection(parent);
            BuildEffectsSection(parent);
            BuildAbilitiesSection(parent);
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
                    DefinitionPath(nameof(ItemDefinition.Textures)),
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

            if (item.LinkMode == EItemLinkMode.LinkedToProvider)
            {
                levelingContent.Add(CreateLevelProviderField(item));
                if (!item.IsLinked)
                    levelingContent.Add(new HelpBox("No level provider is set, so the item uses its own levels.", HelpBoxMessageType.Warning));
                // The range in use: the provider's, or the item's own while it has none
                levelingContent.Add(ShownLevel("StartingLevel", "Starting Level", item.GetStartingLevel()));
                levelingContent.Add(ShownLevel("MaxLevel", "Max Level", item.GetMaxLevel()));
            }
            else
            {
                BuildOwnLevels();
            }

            levelingContent.Add(BuildChildLinks());
            levelingContent.Bind(serializedObject);
        }

        // The item's own range, which it keeps while standalone
        private void BuildOwnLevels()
        {
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.StartingLevel)), "StartingLevel", "Starting Level"));
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Item.MaxLevel)), "MaxLevel", "Max Level"));
        }

        // A level the item uses but doesn't set here, shown in a disabled field
        private static IntegerField ShownLevel(string name, string label, int value)
        {
            var field = new IntegerField(label) { name = name };
            field.SetValueWithoutNotify(value);
            field.AddToClassList(IntegerField.alignedFieldUssClassName);
            field.SetEnabled(false);
            return field;
        }

        // What Leveling is drawn from: the link mode, and while linked, the provider and the range it gives. A
        // multiple selection's is whether every item keeps its own range
        private string LevelingState()
        {
            if (IsMultiple) return Items.All(each => each.LinkMode == EItemLinkMode.Standalone) ? "own" : "linked";
            if (item.LinkMode != EItemLinkMode.LinkedToProvider) return "own";
            var provider = item.LinkedProvider ? item.LinkedProvider.GetInstanceID() : 0;
            return $"linked {provider} {item.GetStartingLevel()} {item.GetMaxLevel()}";
        }

        // The item's active ability and granted effects, which can take its levels
        private IEnumerable<Object> Children() =>
            (item.GrantedEffects ?? new List<GameplayEffect>()).Where(effect => effect != null).Cast<Object>()
            .Concat(item.ActiveAbility != null ? new Object[] { item.ActiveAbility } : new Object[0]);

        // Links the item's active ability and granted effects to it, so they take its levels, or removes those links. They
        // sat under a divider and a small bold heading, with a sentence about them, and Unlink was tinted red (ED-37)
        private VisualElement BuildChildLinks()
        {
            const string linkFocus = "Ability & Effects";
            var row = new VisualElement { name = "ChildLinks" };
            row.AddToClassList("pf-button-row");

            row.Add(new Button(() =>
            {
                var count = Children().Count();
                if (!ConfirmLinkChange($"Confirm Link {linkFocus}",
                        $"Are you sure you want to link active ability and effects?\n\nThis will affect {count} {(count == 1 ? "asset" : "assets")}.",
                        "Yes")) return;

                // Recorded first, so one Undo takes back everything it changes (ED-12)
                RecordLinkChange($"Link {linkFocus}", Children().Append(item));
                item.LinkAllChildren();
                foreach (var child in Children()) MarkDirty(child);
                MarkDirty(item);
                RebuildLevelingContent();
                Repaint();
            })
            {
                text = $"Link {linkFocus}",
                tooltip = "Link the active ability and granted effects to this item, so they take its levels",
            });

            row.Add(new Button(() =>
            {
                // Counts the children the unlink would change; it asked IsLinkedTo(this), the editor, so it always said 0 (ED-31)
                var linked = Children().OfType<BaseForgeLevelProvider>().Count(child => child.IsLinkedTo(item));
                if (!ConfirmLinkChange($"Confirm Unlink {linkFocus}",
                        "Are you sure you want to unlink active ability and effects?" +
                        $"\n\nThis will affect {linked} {(linked == 1 ? "asset" : "assets")}." +
                        "\n\nThis change only applies to assets linked to this item.",
                        "Yes")) return;

                // Recorded first, so one Undo takes back everything it changes (ED-12)
                RecordLinkChange($"Unlink {linkFocus}", Children().Append(item));
                item.UnlinkAllChildren();
                foreach (var child in Children()) MarkDirty(child);
                MarkDirty(item);
                RebuildLevelingContent();
                Repaint();
            })
            {
                text = $"Unlink {linkFocus}",
                tooltip = "Remove the links the active ability and granted effects have to this item",
            });

            return row;
        }

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

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "items/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Item.WorkerGroup) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

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
            var sections = new[] { identitySection, levelingSection, tagsSection, effectsSection, abilitiesSection, workersSection, localDataSection, usedBySection };
            // A multiple selection's items differ, so one line would describe only the first
            if (IsMultiple)
            {
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (identitySection != null) identitySection.Summary.text = item.Definition.Visibility.IsValid ? item.Definition.Visibility.Name : "";
            if (levelingSection != null) levelingSection.Summary.text = LevelingSummary();
            if (tagsSection != null) tagsSection.Summary.text = TagsSummary();
            if (effectsSection != null) effectsSection.Summary.text = CountOf(item.GrantedEffects?.Count(effect => effect != null) ?? 0, "effect", "effects");
            if (abilitiesSection != null) abilitiesSection.Summary.text = item.ActiveAbility != null ? NameOf(item.ActiveAbility) : "None";
            if (workersSection != null) workersSection.Summary.text = CountOf(WorkerCount(item.WorkerGroup), "worker", "workers");
            if (localDataSection != null) localDataSection.Summary.text = CountOf(item.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // The range in use, as in "Levels 1–4", and for a linked item where it comes from
        private string LevelingSummary()
        {
            var range = LevelRange(item.GetStartingLevel(), item.GetMaxLevel());
            if (item.LinkMode != EItemLinkMode.LinkedToProvider) return range;
            return item.IsLinked ? $"{range} · from {NameOf(item.LinkedProvider)}" : $"{range} · no provider";
        }

        // The context tags, or how many, and how many tags the item grants, as in "Weapon.Sword · 1 granted"
        private string TagsSummary()
        {
            var context = item.Tags.ContextTags?.Where(tag => tag.IsValid).Select(tag => tag.Name).ToList() ?? new List<string>();
            var granted = item.Tags.PassiveGrantedTags?.Count(tag => tag.IsValid) ?? 0;
            var parts = new List<string>();
            if (context.Count is 1 or 2) parts.Add(string.Join(", ", context));
            else if (context.Count > 2) parts.Add($"{context.Count} context tags");
            if (granted > 0) parts.Add($"{granted} granted");
            return parts.Count == 0 ? "None" : string.Join(" · ", parts);
        }

        private static int WorkerCount(StandardWorkerGroup group) => group == null
            ? 0
            : (group.AttributeWorkers?.Count(worker => worker != null) ?? 0) + (group.ImpactWorkers?.Count(worker => worker != null) ?? 0) +
              (group.TagWorkers?.Count(worker => worker != null) ?? 0) + (group.AnalysisWorkers?.Count(worker => worker != null) ?? 0);

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
