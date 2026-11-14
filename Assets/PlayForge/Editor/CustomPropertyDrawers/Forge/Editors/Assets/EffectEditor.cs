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
    /// The Effect inspector, in Docs/EDITOR_STYLE.md's terms and the Effect mockup's (ED-37): Identity, Leveling, Tags,
    /// Duration, Impact, Requirements, Workers and Local Data, then Used By. A collapsed section shows one line, and the
    /// inspector takes its look from the style sheet. Requirements shows the target's table, then the source's, on a
    /// two-button switch.
    /// </summary>
    [CustomEditor(typeof(GameplayEffect)), CanEditMultipleObjects]
    public class GameplayEffectEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_EffectIcon;

        private GameplayEffect effect;

        // Its fields write through the SerializedObject, and its scalers through each asset's own, so it edits a multiple
        // selection; what acts on one effect steps aside: the name, the asset tag, linking, the lists each effect keeps
        // of its own, and Used By (ED-12)
        protected override bool SupportsMultipleObjects => true;

        private IEnumerable<GameplayEffect> Effects => targets.Cast<GameplayEffect>();

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;

        private SectionResult levelingSection;
        private SectionResult tagsSection;
        private SectionResult durationSection;
        private SectionResult impactSection;
        private SectionResult levelsSection;
        private EffectLevelsView levelsView;
        private SectionResult requirementsSection;
        private SectionResult componentsSection;
        private SectionResult workersSection;
        private SectionResult cuesSection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return effect;
        }
        protected override string GetDisplayName()
        {
            return !string.IsNullOrEmpty(effect.GetName()) ? effect.GetName() : "Unnamed Effect";
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

        protected override string GetDocumentationTopic() => "effects";

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
            // The style doc's order, as the Effect mockup has it: Identity, Leveling, Tags, the effect's own Duration and
            // Impact, then Requirements, Workers and Local Data, then what uses the effect. Level Source sat apart from
            // Leveling, Impact came before Duration and Workers before Requirements, and the asset tag sat in Tags (ED-37)
            BuildIdentitySection(parent);
            BuildLevelingSection(parent);
            BuildTagsSection(parent);
            BuildDurationSection(parent);
            BuildImpactSection(parent);
            BuildLevelsSection(parent);
            BuildCuesSection(parent);
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
            MarkDirty(effect);
            Repaint();
        }

        private static string DefinitionPath(string field) => $"{nameof(GameplayEffect.Definition)}.{field}";
        private static string TagsPath(string field) => $"{nameof(GameplayEffect.Tags)}.{field}";

        // ═══════════════════════════════════════════════════════════════════════════
        // Identity Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildIdentitySection(VisualElement parent)
        {
            var section = CreateSection(new SectionConfig
            {
                Name = "Identity",
                Title = "Identity",
                HelpTopic = "effects/definition",

                // Import and Clear leave the name, which identifies the effect and gives its tag (ED-35)
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    DefinitionPath(nameof(GameplayEffectDefinition.Description)), DefinitionPath(nameof(GameplayEffectDefinition.Visibility)),
                    DefinitionPath(nameof(GameplayEffectDefinition.Textures)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(section.Section);
            var content = section.Content;

            nameField = CreateTextField("Name", "Name");
            nameField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            nameField.value = effect.Definition.Name;
            // Typing previews the tag; leaving the field stores the name and the tag in one undoable step (ED-12)
            nameField.RegisterValueChangedCallback(evt => UpdateAssetTagDisplay(evt.newValue));
            nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (IsMultiple || nameField.value == (effect.Definition.Name ?? "")) return;
                Undo.RecordObject(effect, "Rename Effect");
                effect.Definition.Name = nameField.value;
                SyncAssetTag();
                UpdateHeader();
                MarkDirty(effect);
                // Its file follows its name (ED-70)
                RenameFile(effect, nameField.value);
                Repaint();
            });
            // Each effect's tag comes from its own name, so a multiple selection shows the names and can't rename (ED-12)
            if (IsMultiple)
            {
                nameField.SetEnabled(false);
                nameField.showMixedValue = Effects.Select(each => each.Definition.Name).Distinct().Count() > 1;
                nameField.tooltip = "Rename one effect at a time: each effect's tag comes from its own name.";
            }
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset, with no Undo (ED-12)
            content.Add(CreateBoundTextField(DefinitionPath(nameof(GameplayEffectDefinition.Description)),
                "Description", multiline: true, minHeight: 18, name: "Description"));

            // The tag the name gives, under the name, as the mockup places it. It sat at the top of Tags (ED-37). Each effect
            // has its own, so a multiple selection doesn't show one (ED-12)
            if (!IsMultiple)
            {
                assetTagField = new ForgeAssetTagField();
                content.Add(assetTagField);
                UpdateAssetTagDisplay();
            }

            content.Add(CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(GameplayEffectDefinition.Visibility))), "Visibility", "Visibility"));
            var texturesField = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(GameplayEffectDefinition.Textures))), "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                UpdateHeader();
                Repaint();
            });
            content.Add(texturesField);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Section: where the effect's levels come from
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildLevelingSection(VisualElement parent)
        {
            levelingSection = CreateSection(new SectionConfig
            {
                Name = "Leveling",
                Title = "Leveling",
                HelpTopic = "effects/leveling",

                // The link mode. A link is made in the section, which refuses one that would make a loop
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.LinkMode) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(levelingSection.Section);

            levelingContent = levelingSection.Content;
            RebuildLevelingContent();
        }

        // Leveling holds what Level Source did (ED-37), so the base editor's two rebuilds are one here
        protected override void RebuildLevelSourceContent() => RebuildLevelingContent();

        // Link Mode, then the provider and the range it gives while linked, or what an effect's level is, then Link and
        // Unlink Contained Effects. Level Source drew the link state in tinted boxes with colored borders, under sentences
        // about linking, with a red-tinted Unlink (ED-37)
        protected override void RebuildLevelingContent()
        {
            if (levelingContent == null) return;
            levelingContent.Clear();
            shownLeveling = LevelingState();

            // Link Mode binds to its property, so a change undoes, and OnAssetChanged redraws from it (ED-12)
            var linkModeField = new EnumField("Link Mode", effect.LinkMode) { name = "LinkMode", bindingPath = nameof(GameplayEffect.LinkMode) };
            linkModeField.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            levelingContent.Add(linkModeField);

            // A provider and a child's link belong to one effect, so a multiple selection sets the mode alone (ED-12)
            if (IsMultiple)
            {
                levelingContent.Add(CreateNote(Effects.All(each => each.LinkMode != EEffectLinkMode.LinkedToProvider)
                    ? "The effects take the level of what applies them, such as the ability that casts them."
                    : "Each linked effect takes its levels from its own provider."));
                levelingContent.Bind(serializedObject);
                return;
            }

            if (IsLinkMode)
                AddLinkedLevels(levelingContent, effect, "effect", effect.GetLinkedStartingLevel(), effect.GetLinkedMaxLevel(),
                    "No level provider is set, so the effect takes the level of what applies it.");
            else
                levelingContent.Add(CreateNote("The effect takes the level of what applies it, such as the ability that casts it."));

            // Contained effects take the provider's levels, so the buttons wait for one: with none, Link would unlink them
            if (effect.IsLinked)
                levelingContent.Add(CreateChildLinks(effect, "effect's provider", "Contained Effects", "contained effects",
                    Children, effect.LinkAllChildren, effect.UnlinkAllChildren, effect.LinkedProvider));
            levelingContent.Bind(serializedObject);
        }

        private bool IsLinkMode => effect.LinkMode == EEffectLinkMode.LinkedToProvider;

        // What Leveling is drawn from. A multiple selection's is whether every effect is standalone
        private string LevelingState() => IsMultiple
            ? Effects.All(each => each.LinkMode != EEffectLinkMode.LinkedToProvider) ? "own" : "linked"
            : LevelingState(effect, IsLinkMode, effect.GetLinkedStartingLevel(), effect.GetLinkedMaxLevel());

        // The effects it contains, which can take its provider's levels
        private IEnumerable<BaseForgeLevelProvider> Children() =>
            (effect.ImpactSpecification?.Packets ?? new ContainedEffectPacket[0])
            .Select(packet => packet.ContainedEffect).Where(contained => contained != null).Distinct();

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildTagsSection(VisualElement parent)
        {
            tagsSection = CreateSection(new SectionConfig
            {
                Name = "Tags",
                Title = "Tags",
                HelpTopic = "effects/tags",

                // Import and Clear leave the asset tag, which the name gives
                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    TagsPath(nameof(GameplayEffectTags.GrantedTags)), nameof(GameplayEffect.GrantedAbilities), nameof(GameplayEffect.SideChange),
                    TagsPath(nameof(GameplayEffectTags.ContextTags)),
                    TagsPath(nameof(GameplayEffectTags.RemovesEffectsTagged)), TagsPath(nameof(GameplayEffectTags.BlocksEffectsTagged)),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(tagsSection.Section);

            // Granted Tags first, as the mockup has them, then the abilities it lends while it's on. Then the effects this
            // one takes off as it lands, a cleanse, and those it refuses while it's on, an immunity (FEAT-8)
            var content = tagsSection.Content;
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.GrantedTags))), "GrantedTags", "Granted Tags"));
            var lends = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.GrantedAbilities)), "GrantedAbilities", "Granted Abilities");
            lends.tooltip = "Abilities the target has while this is on, at the level of what applied it";
            content.Add(lends);
            // The sides it gives the one it's on while it lasts, a mind control's or a pacify's (TW-18)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.SideChange)), "SideChange", "Side Change"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.ContextTags))), "ContextTags", "Context Tags"));
            var removes = CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.RemovesEffectsTagged))),
                "RemovesEffectsTagged", "Removes Effects Tagged");
            removes.tooltip = "Effects on the target tagged with one of these, or a child of one, come off as this lands";
            content.Add(removes);
            var blocks = CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.BlocksEffectsTagged))),
                "BlocksEffectsTagged", "Blocks Effects Tagged");
            blocks.tooltip = "While this is on the target, an effect tagged with one of these, or a child of one, can't land";
            content.Add(blocks);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Duration and Impact Sections: how long the effect lasts, and what it changes
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildDurationSection(VisualElement parent)
        {
            durationSection = CreateSection(new SectionConfig
            {
                Name = "Duration",
                Title = "Duration",
                HelpTopic = "effects/duration",

                // Duration's Import only logged a warning (ED-32)
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.DurationSpecification) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(durationSection.Section);

            durationSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.DurationSpecification)), "DurationSpecification", ""));
        }

        private void BuildImpactSection(VisualElement parent)
        {
            impactSection = CreateSection(new SectionConfig
            {
                Name = "Impact",
                Title = "Impact",
                HelpTopic = "effects/impact",

                // Impact's Import only logged a warning (ED-32). Its own fields, but the chance to apply, which
                // Requirements shows and imports and clears (FEAT-8)
                SerializedObject = serializedObject,
                PropertyPaths = ImpactPaths(),
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(impactSection.Section);

            var content = impactSection.Content;
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.ImpactSpecification)), "ImpactSpecification", ""));
            // The group the effect's changes count toward, which each impact takes unless it names its own. It sat in
            // Definition, apart from the impacts it's about
            var retention = CreatePropertyField(serializedObject.FindProperty(DefinitionPath(nameof(GameplayEffectDefinition.RetentionGroup))), "RetentionGroup", "Retention Group");
            retention.tooltip = "The group the effect's changes count toward, such as bonus health";
            content.Add(retention);

            // What its hits carry for on-hit effects, its own or else its ability's (TW-8)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.OwnProcCoefficient)), "OwnProcCoefficient", "Own Proc Coefficient"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.ProcCoefficient)), "ProcCoefficient", "Proc Coefficient"));

            // Its area: as it lands, on each the area takes near its target too (TW-10)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Area)), "Area", "Area"));

            // Its own lifesteal: its caster heals a share of each loss it deals (TW-15)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Lifesteal)), "Lifesteal", "Lifesteal"));

            // Its transfers, each from one attribute to another, its caster's or its target's (TW-20)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Transfers)), "Transfers", "Transfers"));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Levels Section: what the effect does, level by level (FEAT-32)
        // ═══════════════════════════════════════════════════════════════════════════

        // Its duration, ticks, chance and each impact at each level, and the parts behind a cell. Read alone, it changes
        // nothing, so it has no Import or Clear
        private void BuildLevelsSection(VisualElement parent)
        {
            levelsSection = CreateSection(new SectionConfig
            {
                Name = "Levels",
                Title = "Levels",
                HelpTopic = "effects/levels",
                StartExpanded = true,
            });
            parent.Add(levelsSection.Section);

            if (IsMultiple)
            {
                var note = new Label("Select one effect to see it level by level.") { name = "LevelsNote" };
                note.AddToClassList("pf-note");
                levelsSection.Content.Add(note);
                return;
            }
            levelsView = new EffectLevelsView(effect);
            levelsSection.Content.Add(levelsView);
            // An edit to the effect reads again
            levelsView.TrackSerializedObjectValue(serializedObject, _ =>
            {
                levelsView.Refresh();
                if (levelsSection != null) levelsSection.Summary.text = LevelsSummary();
            });
        }

        // The first impact at each level, as "-10/-20/-30 Health"
        // Its one line, as its header and The Forge's list read it (TW-21)
        private string LevelsSummary()
        {
            string line = ForgeAssetLines.Of(effect);
            return string.IsNullOrEmpty(line) ? "No impact" : line;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements Section: the target's and the source's tags
        // ═══════════════════════════════════════════════════════════════════════════

        // The target's table, then the source's, on a two-button switch, as the mockup's On the Target and On the Source.
        // Both sides sat one after the other, each in a tinted box (ED-37)
        // The impact's chance to apply, which the Requirements section shows (FEAT-8)
        private static readonly string ChancePath = $"{nameof(GameplayEffect.ImpactSpecification)}.{nameof(GameplayEffectImpact.ChanceToApply)}";

        // And how it rolls, pseudo-random and its luck, which the Requirements section shows with it (TW-8)
        private static readonly string ChanceRollPath = $"{nameof(GameplayEffect.ImpactSpecification)}.{nameof(GameplayEffectImpact.ChanceRoll)}";

        // The impact's own fields, as the section's Import and Clear take them, and its retention group
        private string[] ImpactPaths()
        {
            var paths = new List<string>();
            var impact = serializedObject.FindProperty(nameof(GameplayEffect.ImpactSpecification));
            if (impact != null)
            {
                var child = impact.Copy();
                var end = impact.GetEndProperty();
                if (child.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        if (child.propertyPath != ChancePath && child.propertyPath != ChanceRollPath) paths.Add(child.propertyPath);
                    } while (child.NextVisible(false));
                }
            }
            paths.Add(DefinitionPath(nameof(GameplayEffectDefinition.RetentionGroup)));
            paths.Add(nameof(GameplayEffect.OwnProcCoefficient));
            paths.Add(nameof(GameplayEffect.ProcCoefficient));
            paths.Add(nameof(GameplayEffect.Area));
            paths.Add(nameof(GameplayEffect.Lifesteal));
            paths.Add(nameof(GameplayEffect.Transfers));
            return paths.ToArray();
        }

        private void BuildRequirementsSection(VisualElement parent)
        {
            requirementsSection = CreateSection(new SectionConfig
            {
                Name = "Requirements",
                Title = "Requirements",
                HelpTopic = "effects/requirements",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { ChancePath, ChanceRollPath, nameof(GameplayEffect.TargetRequirements), nameof(GameplayEffect.SourceRequirements), nameof(GameplayEffect.Conditions) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(requirementsSection.Section);
            var content = requirementsSection.Content;

            // The chance it lands, above the requirements, since it gates the whole application as they do: its
            // duration, tags and every impact. Under Impact it read as one impact's chance (FEAT-8)
            var chance = CreatePropertyField(serializedObject.FindProperty(ChancePath), "ChanceToApply", "Chance to Apply");
            chance.tooltip = "How likely the effect is to land, from 0 to 1, drawn each time it's applied. 1 always lands";
            content.Add(chance);
            // How it rolls: pseudo-random, and its luck (TW-8)
            content.Add(CreatePropertyField(serializedObject.FindProperty(ChanceRollPath), "ChanceRoll", "Chance Roll"));

            var sides = new VisualElement { name = "Sides" };
            sides.AddToClassList("pf-segmented");
            var onTarget = new Button { name = "OnTarget", text = "On the target", focusable = false, tooltip = "What the target must have" };
            var onSource = new Button { name = "OnSource", text = "On the source", focusable = false, tooltip = "What the entity that applies it must have" };
            sides.Add(onTarget);
            sides.Add(onSource);
            content.Add(sides);

            var target = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.TargetRequirements)), "TargetRequirements", "");
            var source = CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.SourceRequirements)), "SourceRequirements", "");
            content.Add(target);
            content.Add(source);

            // Its conditions beside the tags of each phase, either side's numbers in reach of each (TW-19)
            string conditions = nameof(GameplayEffect.Conditions);
            content.Add(CreatePropertyField(serializedObject.FindProperty($"{conditions}.{nameof(EffectConditions.Application)}"), "ApplyConditions", "Apply Conditions"));
            content.Add(CreatePropertyField(serializedObject.FindProperty($"{conditions}.{nameof(EffectConditions.Ongoing)}"), "OngoingConditions", "Ongoing Conditions"));
            content.Add(CreatePropertyField(serializedObject.FindProperty($"{conditions}.{nameof(EffectConditions.Removal)}"), "RemovalConditions", "Removal Conditions"));

            void ShowSide(bool onTheTarget)
            {
                target.style.display = onTheTarget ? DisplayStyle.Flex : DisplayStyle.None;
                source.style.display = onTheTarget ? DisplayStyle.None : DisplayStyle.Flex;
                onTarget.EnableInClassList("pf-segmented__selected", onTheTarget);
                onSource.EnableInClassList("pf-segmented__selected", !onTheTarget);
            }
            onTarget.clicked += () => ShowSide(true);
            onSource.clicked += () => ShowSide(false);
            ShowSide(true);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Workers, Local Data and Used By Sections
        // ═══════════════════════════════════════════════════════════════════════════

        // What the effect shows and sounds, an effect's own section after Impact (FEAT-1)
        private void BuildCuesSection(VisualElement parent)
        {
            cuesSection = CreateSection(new SectionConfig
            {
                Name = "Cues",
                Title = "Cues",
                HelpTopic = "effects/cues",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.Cues) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(cuesSection.Section);

            // Each effect keeps its own cue handlers, objects a multiple selection can't edit safely, as its workers (ED-12)
            if (IsMultiple)
            {
                cuesSection.Content.Add(CreateNote("Select one effect to edit its cues."));
                return;
            }
            cuesSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Cues)), "Cues", ""));
        }

        // Its components: features it carries, each a class with hooks into its life, added from the list's + (FN-14)
        private void BuildComponentsSection(VisualElement parent)
        {
            componentsSection = CreateSection(new SectionConfig
            {
                Name = "Components",
                Title = "Components",
                HelpTopic = "effects/components",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.Components) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(componentsSection.Section);

            // Each effect keeps its own components, objects a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                componentsSection.Content.Add(CreateNote("Select one effect to edit its components."));
                return;
            }
            componentsSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Components)), "Components", ""));
        }

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "effects/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[]
                {
                    nameof(GameplayEffect.OnHit), nameof(GameplayEffect.Modifiers), nameof(GameplayEffect.Auras), nameof(GameplayEffect.Workers),
                    nameof(GameplayEffect.WorkerGroup),
                },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            // Each effect keeps its own workers, objects a multiple selection can't edit safely (ED-12)
            if (IsMultiple)
            {
                workersSection.Content.Add(CreateNote("Select one effect to edit its workers."));
                return;
            }
            // The effects the hits of the one it's on apply, while it lasts (TW-6)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.OnHit)), "OnHit", "On Hit"));
            // How it changes other effects, of the one it's on or landing on it, while it lasts (TW-7)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Modifiers)), "Modifiers", "Modifiers"));
            // The effects it holds on those near the one it's on, while it lasts (TW-10)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Auras)), "Auras", "Auras"));
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Workers)), "Workers", ""));
            // The workers it gives the one it's on while it lasts (TW-13)
            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.WorkerGroup)), "WorkerGroup", "Worker Group"));
        }

        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "effects/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.LocalData) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.LocalData)), "LocalData", ""));
        }

        // The assets that use the effect, such as an ability whose cost it is or an item that grants it (ED-37)
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
                usedBySection.Content.Add(CreateNote("Select one effect to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(effect);
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
            if (!IsMultiple && nameField != null && !IsFocused(nameField) && nameField.value != (effect.Definition.Name ?? ""))
                nameField.SetValueWithoutNotify(effect.Definition.Name ?? "");
            UpdateAssetTagDisplay();
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        // The context tags, then what the effect grants while it's on, its tags by name and its abilities counted, as
        // "Grants Status.Burning, 1 ability", then what it removes and blocks, as "Removes Status.Debuff" (FEAT-8)
        private string TagsSectionSummary()
        {
            var parts = new List<string>();
            var named = TagsSummary(effect.Tags.ContextTags, "context tags", 0);
            if (named != "None") parts.Add(named);
            var grants = new List<string>();
            var granted = effect.Tags.GrantedTags?.Where(tag => tag.IsValid).Select(TagDisplaySettings.PathOf).ToList() ?? new List<string>();
            if (granted.Count is 1 or 2) grants.Add(string.Join(", ", granted));
            else if (granted.Count > 2) grants.Add($"{granted.Count} tags");
            int lent = effect.GrantedAbilities?.Count(ability => ability != null) ?? 0;
            if (lent > 0) grants.Add(CountOf(lent, "ability", "abilities"));
            if (grants.Count > 0) parts.Add($"Grants {string.Join(", ", grants)}");
            AddNamed("Removes", effect.Tags.RemovesEffectsTagged);
            AddNamed("Blocks", effect.Tags.BlocksEffectsTagged);
            return parts.Count == 0 ? "None" : string.Join(" · ", parts);

            void AddNamed(string verb, List<Tag> tags)
            {
                var valid = tags?.Where(tag => tag.IsValid).ToList() ?? new List<Tag>();
                if (valid.Count == 1) parts.Add($"{verb} {TagDisplaySettings.PathOf(valid[0])}");
                else if (valid.Count > 1) parts.Add($"{verb} {valid.Count} tags");
            }
        }

        private void UpdateSummaries()
        {
            // A multiple selection's effects differ, so one line would describe only the first
            if (IsMultiple)
            {
                var sections = new[] { levelingSection, tagsSection, durationSection, impactSection, levelsSection, cuesSection, requirementsSection, componentsSection, workersSection, localDataSection, usedBySection };
                foreach (var section in sections.Where(section => section != null)) section.Summary.text = "";
                return;
            }

            if (levelingSection != null) levelingSection.Summary.text = LevelingSummaryText();
            if (tagsSection != null) tagsSection.Summary.text = TagsSectionSummary();
            if (durationSection != null) durationSection.Summary.text = DurationSummary();
            if (impactSection != null) impactSection.Summary.text = ImpactSummary();
            if (levelsSection != null) levelsSection.Summary.text = LevelsSummary();
            if (requirementsSection != null) requirementsSection.Summary.text = RequirementsSummary();
            if (componentsSection != null) componentsSection.Summary.text = CountOf(effect.Components?.Count(each => each != null) ?? 0, "component", "components");
            if (workersSection != null) workersSection.Summary.text = WorkersLine(effect.OnHit, effect.Modifiers, effect.Auras, (effect.Workers?.Count(worker => worker != null) ?? 0) + WorkerCount(effect.WorkerGroup));
            if (cuesSection != null) cuesSection.Summary.text = CountOf(effect.Cues?.Count(cue => cue != null) ?? 0, "cue", "cues");
            if (localDataSection != null) localDataSection.Summary.text = CountOf(effect.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // A standalone effect takes its origin's level; a linked one's range is its provider's
        private string LevelingSummaryText()
        {
            if (!IsLinkMode) return "From what applies it";
            if (!effect.IsLinked) return "No provider";
            return LevelingSummary(effect, true, effect.GetLinkedStartingLevel(), effect.GetLinkedMaxLevel());
        }

        // The policy, then how long and how often, at the effect's first level, as in "Durational · 6 s · 6 ticks"
        private string DurationSummary()
        {
            var duration = effect.DurationSpecification;
            if (duration == null || duration.DurationPolicy == EEffectDurationPolicy.Instant) return "Instant";
            int level = Mathf.Max(1, effect.GetStartingLevel());
            var parts = new List<string> { EnumName(duration.DurationPolicy) };
            if (duration.DurationPolicy == EEffectDurationPolicy.Durational)
            {
                var series = AbilityInspector.BuildEffectSeries(effect, level, level);
                parts.Add($"{ImpactFields.Number(series.Duration[0])} s");
                if (duration.EnablePeriodicTicks) parts.Add(CountOf(series.Ticks[0], "tick", "ticks"));
            }
            else if (duration.EnablePeriodicTicks)
            {
                parts.Add($"every {ImpactFields.Number(AbilityInspector.EvaluateAtLevel(duration.TickIntervalOperation, level, level, level, out _))} s");
            }
            return string.Join(" · ", parts);
        }

        // The first impact, as in "Health −8 · Periodic", then how many more; None when it changes nothing
        private string ImpactSummary()
        {
            var impact = effect.ImpactSpecification;
            return impact == null ? "None" : ImpactsSummary(impact);
        }

        // How many tags the two sides require or forbid, then a chance to apply short of 1, as in "2 requirements ·
        // 30% chance" (FEAT-8)
        private string RequirementsSummary()
        {
            var count = CountOf(RequirementCount(effect.TargetRequirements) + RequirementCount(effect.SourceRequirements),
                "requirement", "requirements");
            var chance = ChanceSummary(effect.ImpactSpecification?.ChanceToApply);
            return chance == null ? count : count == "None" ? chance : $"{count} · {chance}";
        }

        private string ImpactsSummary(GameplayEffectImpact impact)
        {
            int more = impact.AdditionalImpacts?.Count(each => each != null) ?? 0;
            bool authored = impact.AttributeTarget != null ||
                impact.MagnitudeOperation != null && (impact.MagnitudeOperation.Scaler != null || impact.MagnitudeOperation.Magnitude != 0f);
            if (!authored) return more == 0 ? "None" : CountOf(more, "impact", "impacts");

            var magnitude = serializedObject.FindProperty($"{nameof(GameplayEffect.ImpactSpecification)}.{nameof(GameplayEffectImpact.MagnitudeOperation)}");
            var first = $"{ImpactFields.AttributeName(impact.AttributeTarget)} {ImpactFields.MagnitudeText(impact.ImpactOperation, magnitude)} · " +
                        ImpactFields.DeliveryName(impact.ResolveDelivery(effect.DurationSpecification, out _));
            return more == 0 ? first : $"{first} · {more} more";
        }

        // A chance to apply as the summary says it, or null when the effect always lands. One a scaler gives says so, as
        // it changes by level (FEAT-8)
        private static string ChanceSummary(ScalableFloat chance)
        {
            if (chance == null) return null;
            if (chance.Scaler != null && chance.RealMagnitude != EMagnitudeOperation.UseMagnitude) return "scaled chance";
            if (chance.Magnitude >= 1f) return null;
            return $"{ImpactFields.Number(Mathf.Max(0f, chance.Magnitude) * 100f)}% chance";
        }

        // How many tags a side's three groups require or forbid
        private static int RequirementCount(EffectTagRequirements requirements) => requirements == null
            ? 0
            : new[] { requirements.ApplicationRequirements, requirements.OngoingRequirements, requirements.RemovalRequirements }
                .Sum(group => group?.TotalCount ?? 0);

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        // Shows the tag a name gives: the stored name's, or the one being typed (ED-12)
        private void UpdateAssetTagDisplay(string name = null) =>
            ShowAssetTag(assetTagField, name ?? effect.Definition?.Name, "Effect", effect.Tags.AssetTag, SyncAssetTag);

        // Stores the tag the name gives, then shows it (ED-12)
        private void SyncAssetTag()
        {
            StoreAssetTag(effect, effect.Definition?.Name, "Effect", effect.Tags.AssetTag, tag =>
            {
                var tags = effect.Tags;
                tags.AssetTag = tag;
                effect.Tags = tags;
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
