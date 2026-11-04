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
    [CustomEditor(typeof(GameplayEffect))]
    public class GameplayEffectEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_EffectIcon;

        private GameplayEffect effect;

        private TextField nameField;
        private ForgeAssetTagField assetTagField;
        private VisualElement levelingContent;
        // What Leveling was drawn from, so it redraws when that changes
        private string shownLeveling;

        private SectionResult levelingSection;
        private SectionResult tagsSection;
        private SectionResult durationSection;
        private SectionResult impactSection;
        private SectionResult requirementsSection;
        private SectionResult workersSection;
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

        protected override bool ShowVisualizeButton => true;
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
            BuildRequirementsSection(parent);
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
                if (nameField.value == (effect.Definition.Name ?? "")) return;
                Undo.RecordObject(effect, "Rename Effect");
                effect.Definition.Name = nameField.value;
                SyncAssetTag();
                UpdateHeader();
                MarkDirty(effect);
                Repaint();
            });
            content.Add(nameField);

            // Description binds to its property, so an edit undoes. It wrote straight into the asset, with no Undo (ED-12)
            content.Add(CreateBoundTextField(DefinitionPath(nameof(GameplayEffectDefinition.Description)),
                "Description", multiline: true, minHeight: 18, name: "Description"));

            // The tag the name gives, under the name, as the mockup places it. It sat at the top of Tags (ED-37)
            assetTagField = new ForgeAssetTagField();
            content.Add(assetTagField);
            UpdateAssetTagDisplay();

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

        private string LevelingState() => LevelingState(effect, IsLinkMode, effect.GetLinkedStartingLevel(), effect.GetLinkedMaxLevel());

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
                PropertyPaths = new[] { TagsPath(nameof(GameplayEffectTags.GrantedTags)), TagsPath(nameof(GameplayEffectTags.ContextTags)) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(tagsSection.Section);

            // Granted Tags first, as the mockup has them
            var content = tagsSection.Content;
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.GrantedTags))), "GrantedTags", "Granted Tags"));
            content.Add(CreatePropertyField(serializedObject.FindProperty(TagsPath(nameof(GameplayEffectTags.ContextTags))), "ContextTags", "Context Tags"));
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

                // Impact's Import only logged a warning (ED-32)
                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.ImpactSpecification), DefinitionPath(nameof(GameplayEffectDefinition.RetentionGroup)) },
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
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Requirements Section: the target's and the source's tags
        // ═══════════════════════════════════════════════════════════════════════════

        // The target's table, then the source's, on a two-button switch, as the mockup's On the Target and On the Source.
        // Both sides sat one after the other, each in a tinted box (ED-37)
        private void BuildRequirementsSection(VisualElement parent)
        {
            requirementsSection = CreateSection(new SectionConfig
            {
                Name = "Requirements",
                Title = "Requirements",
                HelpTopic = "effects/requirements",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.TargetRequirements), nameof(GameplayEffect.SourceRequirements) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(requirementsSection.Section);
            var content = requirementsSection.Content;

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

        private void BuildWorkersSection(VisualElement parent)
        {
            workersSection = CreateSection(new SectionConfig
            {
                Name = "Workers",
                Title = "Workers",
                HelpTopic = "effects/workers",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(GameplayEffect.Workers) },
                OnImportComplete = OnSectionReplaced,
                OnClearComplete = OnSectionReplaced,
            });
            parent.Add(workersSection.Section);

            workersSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(GameplayEffect.Workers)), "Workers", ""));
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
            if (nameField != null && !IsFocused(nameField) && nameField.value != (effect.Definition.Name ?? ""))
                nameField.SetValueWithoutNotify(effect.Definition.Name ?? "");
            UpdateAssetTagDisplay();
            UpdateSummaries();
        }

        private static bool IsFocused(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        private void UpdateSummaries()
        {
            if (levelingSection != null) levelingSection.Summary.text = LevelingSummaryText();
            if (tagsSection != null)
                tagsSection.Summary.text = TagsSummary(effect.Tags.ContextTags, "context tags", effect.Tags.GrantedTags?.Count(tag => tag.IsValid) ?? 0);
            if (durationSection != null) durationSection.Summary.text = DurationSummary();
            if (impactSection != null) impactSection.Summary.text = ImpactSummary();
            if (requirementsSection != null)
                requirementsSection.Summary.text = CountOf(RequirementCount(effect.TargetRequirements) + RequirementCount(effect.SourceRequirements),
                    "requirement", "requirements");
            if (workersSection != null) workersSection.Summary.text = CountOf(effect.Workers?.Count(worker => worker != null) ?? 0, "worker", "workers");
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
            if (impact == null) return "None";
            int more = impact.AdditionalImpacts?.Count(each => each != null) ?? 0;
            bool authored = impact.AttributeTarget != null ||
                impact.MagnitudeOperation != null && (impact.MagnitudeOperation.Scaler != null || impact.MagnitudeOperation.Magnitude != 0f);
            if (!authored) return more == 0 ? "None" : CountOf(more, "impact", "impacts");

            var magnitude = serializedObject.FindProperty($"{nameof(GameplayEffect.ImpactSpecification)}.{nameof(GameplayEffectImpact.MagnitudeOperation)}");
            var first = $"{ImpactFields.AttributeName(impact.AttributeTarget)} {ImpactFields.MagnitudeText(impact.ImpactOperation, magnitude)} · " +
                        ImpactFields.DeliveryName(impact.ResolveDelivery(effect.DurationSpecification, out _));
            return more == 0 ? first : $"{first} · {more} more";
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
