using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The Ability inspector, in Docs/EDITOR_STYLE.md's terms and the Ability mockup's (ED-37): Identity, Leveling, Tags,
    /// the ability's Activation and Stages, Requirements, Workers and Local Data, then Used By. A collapsed section shows
    /// one line, and the inspector takes its look from the style sheet.
    /// </summary>
    [CustomEditor(typeof(Ability)), CanEditMultipleObjects]
    public class AbilityEditor : BasePlayForgeEditor
    {
        private Ability ability;

        // Its fields write through the SerializedObject, so it edits a multiple selection, and what acts on one ability
        // steps aside: the name, the asset tag, linking, a linked ability's levels, and the lists each ability keeps of
        // its own (ED-12)
        protected override bool SupportsMultipleObjects => true;

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;
        private PropertyField implicitTagField;

        // Under the cooldown, while no stage applies the usage effects (ED-41)
        private HelpBox chargesNote;

        private SectionResult levelingSection;
        private SectionResult tagsSection;
        private SectionResult activationSection;
        private SectionResult stagesSection;
        private SectionResult requirementsSection;
        private SectionResult componentsSection;
        private SectionResult workersSection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        private IEnumerable<Ability> Abilities => targets.Cast<Ability>();

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return ability;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(ability.GetName()) ? ability.GetName() : "Unnamed Ability";
        }

        protected override string GetDisplayDescription()
        {
            if (ability == null) return "";
            var desc = ability.Definition.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }

        protected override Texture2D GetHeaderIcon()
        {
            if (ability.Definition.Textures != null && ability.Definition.Textures.Count > 0)
            {
                foreach (var text in ability.Definition.Textures)
                {
                    if (text.Tag == Tags.PRIMARY) return text.Texture;
                }

                var tex = ability.Definition.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return null;
        }

        protected override string GetAssetTypeLabel() => "ABILITY";

        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(Ability));

        protected override string GetDocumentationTopic() => "abilities";

        protected override bool ShowImportButton => true;

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            ability = serializedObject.targetObject as Ability;
            return ability is not null;
        }

        protected override void BuildInspectorContent(VisualElement parent)
        {
            // The style doc's order: Identity, Leveling, Tags, the ability's own sections, then Requirements, Workers and
            // Local Data, then what uses the ability. Level Source sat apart from Leveling, under the same section name;
            // Runtime Behaviour came before Tags; and the tag requirements sat in Tags, apart from the checks (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildTagsSection(parent);
            BuildActivationSection(parent);
            BuildStagesSection(parent);
            BuildRequirementsSection(parent);
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
            MarkDirty(ability);
            Repaint();
        }

        private static string DefinitionPath(string field) => $"{nameof(Ability.Definition)}.{field}";
        private static string TagsPath(string field) => $"{nameof(Ability.Tags)}.{field}";
        private static string BehaviourPath(string field) => $"{nameof(Ability.Behaviour)}.{field}";

        // ═══════════════════════════════════════════════════════════════════════════
        // Identity Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildIdentitySection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Identity",
                Title = "Identity",
                HelpTopic = "abilities/definition",

                // Import and Clear leave the name, which identifies the ability and gives its tag (ED-35)
                SerializedObject = serializedObject,
                PropertyPaths = new[] { DefinitionPath(nameof(AbilityDefinition.Description)), DefinitionPath(nameof(AbilityDefinition.Textures)) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(section.Section);

            var content = section.Content;

            nameField = CreateTextField("Name", "Name");
            nameField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            nameField.value = ability.Definition.Name;
            // Typing previews the tag; leaving the field stores the name and the tag in one undoable step (ED-12)
            nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay(evt.newValue));
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (IsMultiple || nameField.value == (ability.Definition.Name ?? "")) return;
                Undo.RecordObject(ability, "Rename Ability");
                ability.Definition.Name = nameField.value;
                SyncAssetTag();
                UpdateHeader();
                MarkDirty(ability);
                // Its file follows its name (ED-70)
                RenameFile(ability, nameField.value);
                Repaint();
            });
            // Each ability's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = Abilities.Select(each => each.Definition.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one ability at a time: each ability's tag comes from its own name.";
            }
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset, with no Undo (ED-12)
            content.Add(CreateBoundTextField(DefinitionPath(nameof(AbilityDefinition.Description)),
                "Description", multiline: true, minHeight: 18, name: "Description"));

            // The tag the name gives, under the name, as the mockup places it. It sat at the top of Tags, in a tinted box
            // (ED-37). Each ability has its own, so a multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                assetTagField = new ForgeAssetTagField();
                content.Add(assetTagField);
                UpdateAssetTagDisplay();
            }

            var texturesField = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.Textures))), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section: the ability's levels, and where they come from
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelingSection(VisualElement parent)
        {
            levelingSection = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                HelpTopic = "abilities/leveling",

                // The ability's own range. A link is made in the section, which refuses one that would make a loop
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Ability.StartingLevel), nameof(Ability.MaxLevel), nameof(Ability.IgnoreWhenLevelZero) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                // Its own defaults: Clear set the Starting Level to 0
                GetDefaultValue = path => path switch
                {
                    nameof(Ability.StartingLevel) => 1,
                    nameof(Ability.MaxLevel) => 4,
                    nameof(Ability.IgnoreWhenLevelZero) => true,
                    _ => null,
                },
            });
            parent.Add(levelingSection.Section);

            levelingContent = levelingSection.Content;
            RebuildLevelingContent();
        }

        // Leveling holds what Level Source did (ED-37), so the base editor's two rebuilds are one here
        protected override void RebuildLevelSourceContent() => RebuildLevelingContent();

        // Link Mode, then the ability's own range, or its provider and the range the provider gives, then Ignore When
        // Level Zero and its cost and cooldown's links. Level Source drew the link state in tinted boxes with colored
        // borders, under sentences about linking (ED-37)
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            levelingContent.Clear();
            shownLeveling = LevelingState();

            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it (ED-12)
            var linkModeField = new EnumField("Link Mode", ability.LinkMode) { name = "LinkMode", bindingPath = nameof(Ability.LinkMode) };
            linkModeField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            levelingContent.Add(linkModeField);

            // A provider and a child's link belong to one ability, so a multiple selection sets the mode, and edits the
            // range when every ability keeps its own; a linked ability's range comes from its provider (ED-12)
            if (IsMultiple)
            {
                if (Abilities.All(each => each.LinkMode == EAbilityLinkMode.Standalone)) BuildOwnLevels();
                else levelingContent.Add(CreateNote("Each linked ability takes its levels from its own provider."));
            }
            else if (IsLinkMode) AddLinkedLevels(levelingContent, ability, "ability", ability.GetLinkedStartingLevel(), ability.GetLinkedMaxLevel());
            else BuildOwnLevels();

            var ignoreZero = new Toggle("Ignore When Level Zero")
            {
                name = "IgnoreWhenLevelZero",
                bindingPath = nameof(Ability.IgnoreWhenLevelZero),
                tooltip = "At level 0, the ability can't activate",
            };
            ignoreZero.AddToClassList(Toggle.alignedFieldUssClassName);
            levelingContent.Add(ignoreZero);

            // Links the ability's cost and cooldown to it, so they take its levels, or removes those links
            if (!IsMultiple)
                levelingContent.Add(CreateChildLinks(ability, "ability", "Cost & Cooldown", "cost and cooldown",
                    Children, ability.LinkAllChildren, ability.UnlinkAllChildren));
            levelingContent.Bind(serializedObject);
        }

        private bool IsLinkMode => ability.LinkMode == EAbilityLinkMode.LinkedToProvider;

        private void BuildOwnLevels()
        {
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.StartingLevel)), "StartingLevel", "Starting Level"));
            levelingContent.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.MaxLevel)), "MaxLevel", "Max Level"));
        }

        // What Leveling is drawn from. A multiple selection's is whether every ability keeps its own range
        private string LevelingState() => IsMultiple
            ? Abilities.All(each => each.LinkMode == EAbilityLinkMode.Standalone) ? "own" : "linked"
            : LevelingState(ability, IsLinkMode, ability.GetLinkedStartingLevel(), ability.GetLinkedMaxLevel());

        // The ability's cost and cooldown, which can take its levels
        private IEnumerable<BaseForgeLevelProvider> Children() =>
            new BaseForgeLevelProvider[] { ability.Cost, ability.Cooldown }.Where(effect => effect != null).Distinct();

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildTagsSection(VisualElement parent)
        {
            tagsSection = CreateSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                HelpTopic = "abilities/tags",

                // Import and Clear leave the asset tag, which the name gives; the requirements sit in Requirements
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    TagsPath(nameof(AbilityTags.ContextTags)), TagsPath(nameof(AbilityTags.PassiveGrantedTags)), TagsPath(nameof(AbilityTags.ActiveGrantedTags)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(tagsSection.Section);

            // The granted tags sat in a tinted box with a small heading (ED-37)
            var content = tagsSection.Content;
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(AbilityTags.ContextTags))), "ContextTags", "Context Tags"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(AbilityTags.PassiveGrantedTags))), "PassiveTags", "Passive Tags"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(AbilityTags.ActiveGrantedTags))), "ActiveTags", "Active Tags"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Activation and Stages Sections: how the ability starts, and what it does
        // ═══════════════════════════════════════════════════════════════════════════

        // When Busy, Activate At Start, Cost, Cooldown and targeting, as the mockup's Activation draws them. Cost and
        // Cooldown sat in a tinted box at the end of Definition, and the targeting in Runtime Behaviour, under sentences
        // that explained them (ED-37)
        private void BuildActivationSection(VisualElement parent)
        {
            activationSection = CreateSection(new SectionConfig
            {
                Name = "Activation",
                Title = "Activation",
                HelpTopic = "abilities/activation",

                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    DefinitionPath(nameof(AbilityDefinition.ActivationPolicy)), DefinitionPath(nameof(AbilityDefinition.BufferWindow)),
                    DefinitionPath(nameof(AbilityDefinition.BufferPriority)), DefinitionPath(nameof(AbilityDefinition.ActivateImmediately)),
                    DefinitionPath(nameof(AbilityDefinition.ChanceToActivate)), DefinitionPath(nameof(AbilityDefinition.ActivationRoll)),
                    DefinitionPath(nameof(AbilityDefinition.ProcCoefficient)), DefinitionPath(nameof(AbilityDefinition.IgnoresCrowdControl)),
                    nameof(Ability.Cost), nameof(Ability.AttributeCosts), nameof(Ability.Cooldown), BehaviourPath(nameof(AbilityBehaviour.Targeting)),
                    BehaviourPath(nameof(AbilityBehaviour.UseImplicitTargeting)), BehaviourPath(nameof(AbilityBehaviour.ImplicitTag)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(activationSection.Section);
            var content = activationSection.Content;

            // When Busy and Activate At Start bind to their properties, so an edit undoes. They wrote straight into the
            // asset, with no Undo (ED-12)
            var policy = new EnumField("When Busy", ability.Definition.ActivationPolicy)
            {
                name = "ActivationPolicy",
                bindingPath = DefinitionPath(nameof(AbilityDefinition.ActivationPolicy)),
                tooltip = "What happens when the ability is activated while another runs: the entity's policy, or the ability's own",
            };
            policy.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            content.Add(policy);

            // The input buffer, which is what When Busy's Queue does: how long a press waits, and which goes first (FEAT-6)
            var window = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.BufferWindow))), "BufferWindow", "Buffer Window");
            window.tooltip = "Under Queue, how long a press that can't start yet waits, in seconds, for the ability to be free";
            content.Add(window);
            var priority = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.BufferPriority))), "BufferPriority", "Buffer Priority");
            priority.tooltip = "Which waiting press fires first: the highest, then the newest of equals";
            content.Add(priority);

            // The mockup's Activate When Granted. The runtime tries it only for an entity's Starting Abilities, as its GAS
            // starts, so the label says so: an ability an item or code grants later doesn't try it
            var immediately = new Toggle("Activate At Start")
            {
                name = "ActivateImmediately",
                bindingPath = DefinitionPath(nameof(AbilityDefinition.ActivateImmediately)),
                tooltip = "As the entity's GAS starts, a Starting Ability set to this tries to activate once",
            };
            immediately.SetValueWithoutNotify(ability.Definition.ActivateImmediately);
            immediately.AddToClassList(Toggle.alignedFieldUssClassName);
            content.Add(immediately);

            // What sets it off on its own: a gameplay event, or a tag gained (FEAT-4)
            var triggers = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.Triggers))), "Triggers", "Triggers");
            triggers.tooltip = "Gameplay events sent to the entity, and tags it gains, that activate the ability on their own";
            content.Add(triggers);

            // Its chance to activate, rolled once for each activation, and how it rolls; and what its effects' hits carry
            // for on-hit effects (TW-8)
            content.Add(CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.ChanceToActivate))), "ChanceToActivate", "Chance to Activate"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.ActivationRoll))), "ActivationRoll", "Activation Roll"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.ProcCoefficient))), "ProcCoefficient", "Proc Coefficient"));
            // The crowd control it ignores, by its tags, as Avatar ignores Silenced and not a stun (TW-16)
            content.Add(CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(AbilityDefinition.IgnoresCrowdControl))), "IgnoresCrowdControl", "Ignores Crowd Control"));

            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Cost)), "Cost", "Cost"));
            // What it spends of attributes beside its Cost effect, each the attribute or the role it names (TW-29). TW-28's
            // Mana Cost spent the project's Mana alone
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.AttributeCosts)), "AttributeCosts", "Attribute Costs"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Cooldown)), "Cooldown", "Cooldown"));

            // The targeting is an object each ability keeps of its own, which a multiple selection can't edit safely (ED-12)
            if (IsMultiple) content.Add(CreateNote("Select one ability to edit its targeting."));
            else content.Add(CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.Targeting))), "Targeting", "Targeting"));

            var useImplicit = new Toggle("Use Implicit Targeting")
            {
                name = "UseImplicitTargeting",
                bindingPath = BehaviourPath(nameof(AbilityBehaviour.UseImplicitTargeting)),
                tooltip = "The ability's source is its target",
            };
            useImplicit.AddToClassList(Toggle.alignedFieldUssClassName);
            content.Add(useImplicit);

            // Shown while Use Implicit Targeting is on, which it follows. It showed or hid only when the toggle changed,
            // so it showed as the inspector opened whatever the toggle said
            implicitTagField = CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.ImplicitTag))), "ImplicitTag", "Implicit Tag");
            content.Add(implicitTagField);
            useImplicit.RegisterValueChangedCallback(change => ShowImplicitTag(change.newValue));
            ShowImplicitTag(ability.Behaviour.UseImplicitTargeting);
        }

        private void ShowImplicitTag(bool shown)
        {
            if (implicitTagField != null) implicitTagField.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The note under the components while its charges never run out, in the Problems view's words (TW-17, FN-14)
        private void ShowCharges()
        {
            if (chargesNote == null) return;
            var message = IsMultiple ? null : ChargesRule.NeverRunOut(ability);
            chargesNote.text = message ?? "";
            chargesNote.style.display = message != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The asset loaders and the stages: what the ability does as it runs. They sat in Runtime Behaviour, between
        // dividers and under sentences that explained them (ED-37)
        private void BuildStagesSection(VisualElement parent)
        {
            stagesSection = CreateSection(new SectionConfig
            {
                Name = "Stages",
                Title = "Stages",
                HelpTopic = "abilities/stages",

                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    BehaviourPath(nameof(AbilityBehaviour.AssetLoaders)), BehaviourPath(nameof(AbilityBehaviour.CancelledBy)),
                    BehaviourPath(nameof(AbilityBehaviour.Blocks)), BehaviourPath(nameof(AbilityBehaviour.Stages)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(stagesSection.Section);

            // The asset loaders and the stages are lists each ability keeps of its own, which a multiple selection can't
            // edit safely: a list of a different length would be cut (ED-12)
            if (IsMultiple)
            {
                stagesSection.Content.Add(CreateNote("Select one ability to edit its asset loaders and stages."));
                return;
            }
            stagesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.AssetLoaders))), "AssetLoaders", "Asset Loaders"));
            // What cancels the ability and what it blocks, in each stage that names none of its own (FEAT-5)
            var cancelledBy = CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.CancelledBy))), "DefaultCancelledBy", "Cancelled By");
            cancelledBy.tooltip = "Abilities, by tag, that cancel this one as they activate, in each stage that names none";
            stagesSection.Content.Add(cancelledBy);
            var blocks = CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.Blocks))), "DefaultBlocks", "Blocks");
            blocks.tooltip = "Abilities, by tag, that can't activate while this one runs, in each stage that names none";
            stagesSection.Content.Add(blocks);
            // The stages' list removes and moves a stage, and each stage keeps its fold as it does (ED-64, ED-18)
            var stages = CreatePropertyField(serializedObject.FindProperty(BehaviourPath(nameof(AbilityBehaviour.Stages))), "Stages", "Stages");
            ForgeLists.KeepsStates(stages, serializedObject, BehaviourPath(nameof(AbilityBehaviour.Stages)));
            stagesSection.Content.Add(stages);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements Section: the tags and the checks the ability needs
        // ═══════════════════════════════════════════════════════════════════════════

        // The tag requirements, then the caster's and the target's checks, as the mockup's Requirements holds them. The tag
        // requirements sat in Tags, and the checks in a Validation Rules section of their own (ED-37)
        private void BuildRequirementsSection(VisualElement parent)
        {
            requirementsSection = CreateSection(new SectionConfig
            {
                Name = "Requirements",
                Title = "Requirements",
                HelpTopic = "abilities/validation",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { TagsPath(nameof(AbilityTags.TagRequirements)), nameof(Ability.SourceActivationRules), nameof(Ability.TargetActivationRules) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
                // Clear leaves the checks: emptying them would let the ability skip its cooldown and cost
                GetDefaultValue = path => path switch
                {
                    nameof(Ability.SourceActivationRules) => new List<IAbilityValidationRule>(),
                    nameof(Ability.TargetActivationRules) => new List<IAbilityValidationRule>(),
                    _ => null,
                },
            });
            parent.Add(requirementsSection.Section);
            var content = requirementsSection.Content;

            var tagRequirements = serializedObject.FindProperty(TagsPath(nameof(AbilityTags.TagRequirements)));
            if (tagRequirements != null) content.Add(CreatePropertyField(tagRequirements, "TagRequirements", ""));

            // Each ability keeps its own lists of checks, which a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                content.Add(CreateNote("Select one ability to edit its checks."));
                return;
            }
            var sourceRules = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.SourceActivationRules)), "SourceActivationRules", "Caster Checks");
            sourceRules.tooltip = "Checks the caster must pass to activate the ability";
            content.Add(sourceRules);
            var targetRules = CreatePropertyField(serializedObject.FindProperty(nameof(Ability.TargetActivationRules)), "TargetActivationRules", "Target Checks");
            targetRules.tooltip = "Checks the target must pass";
            content.Add(targetRules);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers, Local Data and Used By Sections
        // ═══════════════════════════════════════════════════════════════════════════

        // Its components: features it carries, each a class with hooks into its life, added from the list's + (FN-14)
        private void BuildComponentsSection(VisualElement parent)
        {
            componentsSection = CreateSection(new SectionConfig
            {
                Name = "Components",
                Title = "Components",
                HelpTopic = "abilities/components",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Ability.Components) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(componentsSection.Section);

            // Each ability keeps its own components, objects a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                componentsSection.Content.Add(CreateNote("Select one ability to edit its components."));
                return;
            }
            componentsSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Components)), "Components", ""));

            // Its charges, a component, with a note under the list where they never run out (TW-17)
            chargesNote = new HelpBox("", HelpBoxMessageType.Warning) { name = "ChargesNeverRunOut" };
            componentsSection.Content.Add(chargesNote);
            ShowCharges();
        }

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "abilities/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Ability.OnHit), nameof(Ability.Modifiers), nameof(Ability.Lifesteal), nameof(Ability.WorkerGroup) },
                ImportAcrossTypes = true,
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            // The effects its holder's hits apply (TW-6)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.OnHit)), "OnHit", "On Hit"));
            // How it changes other effects, its holder's or those landing on it (TW-7)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Modifiers)), "Modifiers", "Modifiers"));
            // What its holder heals of the damage it deals (TW-15)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.Lifesteal)), "Lifesteal", "Lifesteal"));
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.WorkerGroup)), "WorkerGroup", ""));
        }

        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "abilities/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Ability.LocalData) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Ability.LocalData)), "LocalData", ""));
        }

        // The assets that use the ability, such as an entity that starts with it or an item that grants it (ED-37)
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
                usedBySection.Content.Add(CreateNote("Select one ability to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(ability);
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
            if (!IsMultiple && nameField != null && !IsFocused(nameField) && nameField.value != (ability.Definition.Name ?? ""))
                nameField.SetValueWithoutNotify(ability.Definition.Name ?? "");
            ShowImplicitTag(ability.Behaviour.UseImplicitTargeting);
            ShowCharges();
            UpdateAssetTagDisplay();
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        private void UpdateSummaries()
        {
            var sections = new[] { levelingSection, tagsSection, activationSection, stagesSection, requirementsSection, componentsSection, workersSection, localDataSection, usedBySection };
            // A multiple selection's abilities differ, so one line would describe only the first
            if (IsMultiple)
            {
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (levelingSection != null)
                levelingSection.Summary.text = LevelingSummary(ability, IsLinkMode, ability.GetLinkedStartingLevel(), ability.GetLinkedMaxLevel());
            if (tagsSection != null)
            {
                var granted = (ability.Tags.PassiveGrantedTags?.Count(tag => tag.IsValid) ?? 0) + (ability.Tags.ActiveGrantedTags?.Count(tag => tag.IsValid) ?? 0);
                tagsSection.Summary.text = TagsSummary(ability.Tags.ContextTags, "context tags", granted);
            }
            if (activationSection != null) activationSection.Summary.text = ActivationSummary();
            if (stagesSection != null)
                stagesSection.Summary.text = CountOf(ability.Behaviour.Stages?.Count(stage => stage != null) ?? 0, "stage", "stages");
            if (requirementsSection != null) requirementsSection.Summary.text = RequirementsSummary();
            if (componentsSection != null) componentsSection.Summary.text = CountOf(ability.Components?.Count(each => each != null) ?? 0, "component", "components");
            if (workersSection != null) workersSection.Summary.text = WorkersLine(ability.OnHit, ability.Modifiers, null, ability.Lifesteal, WorkerCount(ability.WorkerGroup));
            if (localDataSection != null) localDataSection.Summary.text = CountOf(ability.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // When Busy's choice, then the cost and the cooldown, and the charges where they're counted, as in "Queue · Fireball
        // Cost · Fireball Cooldown · 3 charges"
        private string ActivationSummary()
        {
            var parts = new List<string> { EnumName(ability.Definition.ActivationPolicy) };
            if (ability.Cost != null) parts.Add(NameOf(ability.Cost));
            // Each attribute cost, as "30 Mana": its attribute's name, or its role's (TW-29)
            foreach (var cost in ability.AttributeCosts ?? new List<AttributeCost>())
            {
                if (cost?.Amount == null || (cost.Amount.Magnitude <= 0f && AbstractScaler.IsNone(cost.Amount.Scaler))) continue;
                parts.Add(NumberText.Of(cost.Amount, cost.Attribute != null ? cost.Attribute.Name : cost.Role.IsValid ? cost.Role.ToString() : "?"));
            }
            if (ability.Cooldown != null) parts.Add(NameOf(ability.Cooldown));
            // Its charges, as "3 charges" (TW-17)
            if (ChargesRule.Counted(ability) && ability.Charges != null)
                parts.Add(ability.Charges.IsConstant ? CountOf(ability.Charges.Constant, "charge", "charges") : NumberText.Of(ability.Charges, "charges"));
            return string.Join(" · ", parts);
        }

        // How many checks, and how many tag requirements, as in "4 checks · 2 tag requirements"
        private string RequirementsSummary()
        {
            var checks = (ability.SourceActivationRules?.Count(rule => rule != null) ?? 0) + (ability.TargetActivationRules?.Count(rule => rule != null) ?? 0);
            var tags = ability.Tags.TagRequirements?.TotalRequirementCount ?? 0;
            var parts = new List<string> { CountOf(checks, "check", "checks") };
            if (tags > 0) parts.Add(CountOf(tags, "tag requirement", "tag requirements"));
            return string.Join(" · ", parts);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        // Shows the tag a name gives: the stored name's, or the one being typed (ED-12)
        private void UpdateAssetTagDisplay(string name = null) =>
            ShowAssetTag(assetTagField, name ?? ability.Definition.Name, "Ability", ability.Tags.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(ability, ability.Definition.Name, "Ability", ability.Tags.AssetTag, tag =>
            {
                var tags = ability.Tags;
                tags.AssetTag = tag;
                ability.Tags = tags;
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
