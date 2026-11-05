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
    /// The Attribute Set inspector, in Docs/EDITOR_STYLE.md's terms (ED-37): Identity, Leveling, the set's Attributes,
    /// Sub-Sets and Collision Settings, then All Attributes, the table of what the set gives an entity, then Workers,
    /// Local Data and Used By. A collapsed section shows one line, and the inspector takes its look from the style sheet.
    /// </summary>
    [CustomEditor(typeof(AttributeSet)), CanEditMultipleObjects]
    public class AttributeSetEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_AttributeSetIcon;

        private AttributeSet attributeSet;

        // Its fields write through the SerializedObject, so it edits a multiple selection; what acts on one set steps
        // aside: the name, the asset tag, linking, the attributes each set keeps of its own, All Attributes, and Used By
        // (ED-12)
        protected override bool SupportsMultipleObjects => true;

        private IEnumerable<AttributeSet> Sets => targets.Cast<AttributeSet>();

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;
        private ForgeAttributeTable attributeTable;

        private SectionResult levelingSection;
        private SectionResult attributesSection;
        private SectionResult subsetsSection;
        private SectionResult settingsSection;
        private SectionResult allAttributesSection;
        private SectionResult workersSection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration
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

        protected override string GetDocumentationTopic() => "attributesets";

        protected override bool ShowVisualizeButton => true;
        protected override bool ShowImportButton => true;

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
            // The style doc's order: Identity, Leveling, the set's own sections, Workers and Local Data, then what uses
            // the set. All Attributes, the table of what the set gives an entity, follows the sections that decide it.
            // Level Source sat apart from Leveling, and a Summary section opened a dialog that listed the attributes
            // (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildAttributesSection(parent);
            BuildSubsetsSection(parent);
            BuildSettingsSection(parent);
            BuildAllAttributesSection(parent);
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
            MarkDirty(attributeSet);
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Identity Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildIdentitySection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Identity",
                Title = "Identity",
                HelpTopic = "attributesets/definition",

                // Import and Clear leave the name, which identifies the set and gives its tag (ED-35)
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(AttributeSet.Description), nameof(AttributeSet.Textures) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(section.Section);

            var content = section.Content;

            nameField = CreateTextField("Name", "Name");
            nameField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            nameField.value = attributeSet.Name;
            // Typing previews the tag; leaving the field stores the name and the tag in one undoable step (ED-12)
            nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay(evt.newValue));
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (IsMultiple || nameField.value == (attributeSet.Name ?? "")) return;
                Undo.RecordObject(attributeSet, "Rename Attribute Set");
                attributeSet.Name = nameField.value;
                MarkDirty(attributeSet);
                SyncAssetTag();
                UpdateHeader();
                Repaint();
            });
            // Each set's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = Sets.Select(each => each.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one set at a time: each set's tag comes from its own name.";
            }
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset when it lost focus,
            // with no Undo (ED-12).
            content.Add(CreateBoundTextField(nameof(AttributeSet.Description), "Description", multiline: true, minHeight: 18));

            // The tag the name gives, under the name. It sat in a tinted, bordered box (ED-37). Each set has its own, so a
            // multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                assetTagField = new ForgeAssetTagField();
                content.Add(assetTagField);
                UpdateAssetTagDisplay();
            }

            var texturesField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.Textures)), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section: the range the set's cached scalers read, and where it comes from
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelingSection(VisualElement parent)
        {
            levelingSection = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                HelpTopic = "attributesets/leveling",

                // The set's own range. A link is made in the section, which refuses one that would make a loop
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(AttributeSet.StartingLevel), nameof(AttributeSet.MaxLevel) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                GetDefaultValue = path => path switch
                {
                    nameof(AttributeSet.StartingLevel) => 1,
                    nameof(AttributeSet.MaxLevel)      => 4,
                    _ => null
                }
            });
            parent.Add(levelingSection.Section);

            levelingContent = levelingSection.Content;
            RebuildLevelingContent();
        }

        // Leveling holds what Level Source did (ED-37), so the base editor's two rebuilds are one here
        protected override void RebuildLevelSourceContent() => RebuildLevelingContent();

        // Link Mode, then the set's own range, or its provider and the range the provider gives. Level Source drew the
        // link state in tinted boxes with colored borders, under sentences about linking (ED-37)
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            levelingContent.Clear();
            shownLeveling = LevelingState();

            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it. It wrote the mode
            // straight into the asset, with no Undo, and redrew only then (ED-12).
            var linkModeField = new EnumField("Link Mode", attributeSet.LinkMode) { name = "LinkMode", bindingPath = nameof(AttributeSet.LinkMode) };
            linkModeField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            levelingContent.Add(linkModeField);

            // The range in use while linked: the provider's, or the set's own while it has none. A provider belongs to one
            // set, so a multiple selection edits the range when every set keeps its own (ED-12)
            if (IsMultiple && !Sets.All(each => each.LinkMode != EAttributeSetLinkMode.LinkedToProvider))
                levelingContent.Add(CreateNote("Each linked set takes its levels from its own provider."));
            else if (!IsMultiple && IsLinkMode) AddLinkedLevels(levelingContent, attributeSet, "set", attributeSet.GetStartingLevel(), attributeSet.GetMaxLevel());
            else
            {
                levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.StartingLevel)), "StartingLevel", "Starting Level"));
                levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.MaxLevel)), "MaxLevel", "Max Level"));
            }

            levelingContent.Bind(serializedObject);
        }

        private bool IsLinkMode => attributeSet.LinkMode == EAttributeSetLinkMode.LinkedToProvider;

        // What Leveling is drawn from. A multiple selection's is whether every set keeps its own range
        private string LevelingState() => IsMultiple
            ? Sets.All(each => each.LinkMode != EAttributeSetLinkMode.LinkedToProvider) ? "own" : "linked"
            : LevelingState(attributeSet, IsLinkMode, attributeSet.GetStartingLevel(), attributeSet.GetMaxLevel());

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes, Sub-Sets and Collision Settings Sections: what the set gives
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildAttributesSection(VisualElement parent)
        {
            attributesSection = CreateSection(new SectionConfig
            {
                Name = "Attributes",
                Title = "Attributes",
                HelpTopic = "attributesets/attributes",

                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.Attributes)},
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(attributesSection.Section);

            // Each set keeps its own attributes, a list a multiple selection can't edit safely: its elements at one index
            // are different attributes, and a list of another length would be cut (ED-12)
            if (IsMultiple)
            {
                attributesSection.Content.Add(CreateNote("Select one set to edit its attributes."));
                return;
            }
            // The list's header counts its elements; the section's header showed a count of its own as well (ED-37)
            attributesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.Attributes)), "Attributes", ""));
        }

        private void BuildSubsetsSection(VisualElement parent)
        {
            subsetsSection = CreateSection(new SectionConfig
            {
                Name = "Subsets",
                Title = "Sub-Sets",
                HelpTopic = "attributesets/subsets",

                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.SubSets)},
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(subsetsSection.Section);

            var subsetField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.SubSets)), "SubSets", "");
            subsetField.tooltip = "Sets whose attributes this set gives too";
            subsetsSection.Content.Add(subsetField);
        }

        private void BuildSettingsSection(VisualElement parent)
        {
            settingsSection = CreateSection(new SectionConfig
            {
                Name = "Settings",
                Title = "Collision Settings",
                HelpTopic = "attributesets/settings",

                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.CollisionResolutionPolicy)},
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                GetDefaultValue = path => path switch
                {
                    nameof(AttributeSet.CollisionResolutionPolicy) => EValueCollisionPolicy.UseMaximum,
                    _ => null
                }
            });
            parent.Add(settingsSection.Section);

            var policyField = CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.CollisionResolutionPolicy)), "CollisionResolutionPolicy", "Resolution Policy");
            policyField.tooltip = "How the set settles an attribute that it and its sub-sets both give";
            settingsSection.Content.Add(policyField);
        }

        // The attributes the set gives an entity, its sub-sets' with them, settled as the runtime settles them. A Summary
        // section counted them and opened a dialog that listed their names (ED-37)
        private void BuildAllAttributesSection(VisualElement parent)
        {
            allAttributesSection = CreateSection(new SectionConfig
            {
                Name = "AllAttributes",
                Title = "All Attributes",
                IncludeButtons = new[] { false, false, false },
            });
            parent.Add(allAttributesSection.Section);

            // The table shows one set's attributes (ED-12)
            if (IsMultiple)
            {
                allAttributesSection.Content.Add(CreateNote("Select one set to see all its attributes."));
                return;
            }
            attributeTable = new ForgeAttributeTable();
            attributeTable.Changed += UpdateSummaries;
            allAttributesSection.Content.Add(attributeTable);
            attributeTable.Show(attributeSet);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers and Local Data Sections
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "attributesets/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(AttributeSet.WorkerGroup)},
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.WorkerGroup)), "WorkerGroup", ""));
        }

        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "attributesets/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(AttributeSet.LocalData) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(AttributeSet.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Used By Section
        // ═══════════════════════════════════════════════════════════════════════════

        // The assets that use the set, such as an entity built from it or a set that holds it, each a row that selects it
        // (ED-37)
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
                usedBySection.Content.Add(CreateNote("Select one set to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(attributeSet);
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
            if (!IsMultiple && nameField != null && !IsFocused(nameField) && nameField.value != (attributeSet.Name ?? ""))
                nameField.SetValueWithoutNotify(attributeSet.Name ?? "");
            UpdateAssetTagDisplay();
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        private void UpdateSummaries()
        {
            // A multiple selection's sets differ, so one line would describe only the first
            if (IsMultiple)
            {
                var sections = new[] { levelingSection, attributesSection, subsetsSection, settingsSection, allAttributesSection, workersSection, localDataSection, usedBySection };
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (levelingSection != null)
                levelingSection.Summary.text = LevelingSummary(attributeSet, IsLinkMode, attributeSet.GetStartingLevel(), attributeSet.GetMaxLevel());
            if (attributesSection != null)
                attributesSection.Summary.text = CountOf(attributeSet.Attributes?.Count(element => element?.Attribute != null) ?? 0, "attribute", "attributes");
            if (subsetsSection != null)
                subsetsSection.Summary.text = CountOf(attributeSet.SubSets?.Count(set => set != null) ?? 0, "sub-set", "sub-sets");
            if (settingsSection != null)
                settingsSection.Summary.text = ObjectNames.NicifyVariableName(attributeSet.CollisionResolutionPolicy.ToString());
            if (allAttributesSection != null && attributeTable != null)
                allAttributesSection.Summary.text = CountOf(attributeTable.Count, "attribute", "attributes");
            if (workersSection != null) workersSection.Summary.text = CountOf(WorkerCount(attributeSet.WorkerGroup), "worker", "workers");
            if (localDataSection != null) localDataSection.Summary.text = CountOf(attributeSet.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        // Shows the tag a name gives: the stored name's, or the one being typed (ED-12)
        private void UpdateAssetTagDisplay(string name = null) =>
            ShowAssetTag(assetTagField, name ?? attributeSet.Name, "AttributeSet", attributeSet.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(attributeSet, attributeSet.Name, "AttributeSet", attributeSet.AssetTag,
                tag => attributeSet.AssetTag = tag);
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
            attributeTable?.Show(attributeSet);
            UpdateSummaries();
            Repaint();
        }
    }
}
