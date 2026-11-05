using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Entities view: the hubs ProcessControl holds, beside the one selected, with its attributes, tags, effects,
    // abilities and impacts (ED-4)
    public sealed partial class RuntimeDebugger
    {
        private readonly List<IGameplayAbilitySystem> hubs = new();
        private readonly List<IGameplayAbilitySystem> shownHubs = new();
        private MultiColumnListView entityList;
        private VisualElement entityDetail;
        private IGameplayAbilitySystem shownEntity;

        // What a sample changes in the entity's detail, in place
        private readonly List<Action> entityUpdates = new();

        // The shown entity's callbacks, which the window listens to while it shows the entity: an entity with a listener
        // fills a frame summary every frame. The GAS Debugger listened to every entity and never stopped (ED-26)
        private GameplayAbilitySystemCallbacks shownCallbacks;
        private ImpactData? lastDealt;
        private ImpactData? lastTaken;

        private VisualElement BuildEntitiesView()
        {
            var view = new VisualElement { name = "EntitiesView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal) { name = "EntitySplit" };
            split.AddToClassList("pf-debugger__split");

            entityList = new MultiColumnListView
            {
                name = "Entities",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = shownHubs,
            };
            entityList.AddToClassList("pf-debugger__list");
            entityList.columns.Add(new Column
            {
                name = "Name", title = "Name", width = 140, stretchable = true,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("pf-debugger__name-cell");
                    cell.Add(new ForgeTypeIcon(typeof(EntityIdentity)));
                    var name = new Label { name = "Name" };
                    name.AddToClassList("pf-debugger__name");
                    cell.Add(name);
                    return cell;
                },
                bindCell = (cell, index) => cell.Q<Label>("Name").text = NameOf(shownHubs[index]),
            });
            entityList.columns.Add(TextColumn("Level", "Level", 64, index => LevelOf(shownHubs[index])));
            entityList.columns.Add(TextColumn("Affiliation", "Affiliation", 110, index => AffiliationOf(shownHubs[index])));
            entityList.selectionChanged += chosen => ShowEntity(chosen.OfType<IGameplayAbilitySystem>().FirstOrDefault());
            // A double click selects a MonoBehaviour hub's GameObject
            entityList.itemsChosen += chosen => SelectObjectOf(chosen.OfType<IGameplayAbilitySystem>().FirstOrDefault());
            split.Add(entityList);

            var scroll = new ScrollView { name = "EntityScroll" };
            scroll.AddToClassList("pf-debugger__detail-scroll");
            entityDetail = new VisualElement { name = "EntityDetail" };
            entityDetail.AddToClassList("pf-debugger__detail");
            scroll.Add(entityDetail);
            split.Add(scroll);
            view.Add(split);
            ShowEntity(null);
            return view;
        }

        // The hubs the search lets through, keeping the selection
        private void RefillEntities()
        {
            if (entityList == null) return;
            shownHubs.Clear();
            shownHubs.AddRange(hubs.Where(gas => Matches(NameOf(gas))));
            entityList.itemsSource = shownHubs;
            entityList.Rebuild();
            int at = shownEntity != null ? shownHubs.IndexOf(shownEntity) : -1;
            entityList.SetSelectionWithoutNotify(at >= 0 ? new[] { at } : Array.Empty<int>());
            // A hub that left ProcessControl leaves the detail too
            if (shownEntity != null && !hubs.Contains(shownEntity)) ShowEntity(null);
        }

        // An entity's level of its levels, as "2 of 5"
        private static string LevelOf(IGameplayAbilitySystem gas)
        {
            var level = gas.GetLevel();
            return $"{level.CurrentValue} of {level.MaxValue}";
        }

        private static string AffiliationOf(IGameplayAbilitySystem gas) =>
            string.Join(", ", (gas.GetAffiliation() ?? new List<Tag>()).Select(TheForge.TagText));

        private static void SelectObjectOf(IGameplayAbilitySystem gas)
        {
            var body = gas?.ToGASObject();
            if (body == null) return;
            Selection.activeGameObject = body.gameObject;
            EditorGUIUtility.PingObject(body.gameObject);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The detail
        // ═══════════════════════════════════════════════════════════════════════════

        // The entity the detail shows: its header, then its attributes, tags, effects, abilities and impacts. They're
        // built once, and a sample changes their values in place; the GAS Debugger cleared and rebuilt them every 0.25 s,
        // replacing a field while it was typed in (ED-4)
        private void ShowEntity(IGameplayAbilitySystem gas)
        {
            if (entityDetail == null) return;
            if (gas != null && ReferenceEquals(gas, shownEntity)) return;
            ForgetShownEntity();
            entityDetail.Clear();
            entityUpdates.Clear();
            if (gas == null || !Alive(gas))
            {
                entityDetail.Add(Note("Select an entity to see it here."));
                return;
            }

            shownEntity = gas;
            shownCallbacks = gas.GetCallbacks();
            if (shownCallbacks != null) shownCallbacks.OnFrameComplete += OnShownFrame;
            entityDetail.Add(EntityHeader(gas));
            entityDetail.Add(AttributesSection(gas));
            entityDetail.Add(TagsSection(gas));
            entityDetail.Add(EffectsSection(gas));
            entityDetail.Add(AbilitiesSection(gas));
            entityDetail.Add(ImpactsSection());
            SampleEntity();
        }

        private void ForgetShownEntity()
        {
            if (shownCallbacks != null) shownCallbacks.OnFrameComplete -= OnShownFrame;
            shownCallbacks = null;
            shownEntity = null;
            lastDealt = null;
            lastTaken = null;
        }

        // The shown entity's frame: the last impact it dealt and the last it took. The snapshot is the entity's own and
        // lasts only through the call
        private void OnShownFrame(FrameSummarySnapshot frame)
        {
            if (frame.ImpactsDealt.Count > 0) lastDealt = frame.ImpactsDealt[frame.ImpactsDealt.Count - 1];
            if (frame.ImpactsReceived.Count > 0) lastTaken = frame.ImpactsReceived[frame.ImpactsReceived.Count - 1];
        }

        private void SampleEntity()
        {
            if (shownEntity == null) return;
            if (!Alive(shownEntity))
            {
                ShowEntity(null);
                return;
            }
            foreach (var update in entityUpdates) update();
        }

        // The entity's type icon, name and level, and Select for a MonoBehaviour hub, which selects its GameObject
        private VisualElement EntityHeader(IGameplayAbilitySystem gas)
        {
            var header = new VisualElement { name = "EntityHeader" };
            header.AddToClassList("pf-debugger__header");
            header.Add(new ForgeTypeIcon(typeof(EntityIdentity)));
            var name = new Label(NameOf(gas)) { name = "HeaderName" };
            name.AddToClassList("pf-debugger__header-name");
            header.Add(name);
            var level = new Label { name = "HeaderLevel" };
            level.AddToClassList("pf-debugger__header-detail");
            header.Add(level);
            entityUpdates.Add(() => level.text = $"Level {LevelOf(gas)}");
            if (gas.ToGASObject() != null)
            {
                var select = new Button(() => SelectObjectOf(gas)) { name = "Select", text = "Select", tooltip = "Select its GameObject" };
                select.AddToClassList("pf-debugger__header-button");
                header.Add(select);
            }
            else
            {
                var kind = new Label("Runtime entity") { name = "Kind" };
                kind.AddToClassList("pf-debugger__header-detail");
                header.Add(kind);
            }
            return header;
        }

        // Each attribute, by its current value and its base. The current value's tooltip breaks it down by retention
        // group, from GetBreakdown; the GAS Debugger read RetainedGroups, which made a dictionary on each read (ED-4)
        private VisualElement AttributesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Attributes", "Attributes");
            section.Content.Add(ColumnTitles("Attribute", "Current", "Base"));
            var rows = new VisualElement { name = "AttributeRows" };
            section.Content.Add(rows);

            var shown = new List<IAttribute>();
            var updates = new List<Action>();
            IReadOnlyDictionary<IAttribute, CachedAttributeValue> CacheOf() => gas.GetAttributeSystem()?.GetAttributeCache();
            void Build(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                if (cache != null) shown.AddRange(cache.Keys.OrderBy(attribute => attribute.GetName(), NaturalOrder));
                foreach (var attribute in shown) rows.Add(AttributeRow(attribute, updates, CacheOf));
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = CountOf(shown.Count, "attribute", "attributes");
            }
            entityUpdates.Add(() =>
            {
                var cache = CacheOf();
                if ((cache?.Count ?? 0) != shown.Count || shown.Any(attribute => !cache.ContainsKey(attribute))) Build(cache);
                foreach (var update in updates) update();
            });
            Build(CacheOf());
            return section;
        }

        private static VisualElement AttributeRow(IAttribute attribute, List<Action> updates, Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf)
        {
            var row = Row($"Attribute {attribute.GetName()}", attribute.GetName());
            var current = ValueField("Current", "Its current value");
            var @base = ValueField("Base", "Its base value");
            row.Add(current);
            row.Add(@base);
            updates.Add(() =>
            {
                var cache = cacheOf();
                if (cache == null || !cache.TryGetValue(attribute, out var cached)) return;
                current.SetValueWithoutNotify(cached.ActiveValue.CurrentValue);
                @base.SetValueWithoutNotify(cached.ActiveValue.BaseValue);
                current.tooltip = BreakdownOf(cached);
            });
            return row;
        }

        private static FloatField ValueField(string name, string tooltip)
        {
            var field = new FloatField { name = name, tooltip = tooltip, isReadOnly = true };
            field.AddToClassList("pf-debugger__value");
            return field;
        }

        // Each retention group's part of the current value, one a line, when there's more than one
        private static string BreakdownOf(CachedAttributeValue cached)
        {
            var parts = cached.GetBreakdown();
            if (parts == null || parts.Count <= 1) return "Its current value";
            return string.Join("\n", parts.Select(part => $"{TheForge.TagText(part.Group)} {Signed(part.Value.CurrentValue)}"));
        }

        // The tags the entity has, each with its weight: how many of its grants hold it. The Tag Debugger listed them in a
        // window of its own
        private VisualElement TagsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Tags", "Tags");
            section.Content.Add(ColumnTitles("Tag", "Weight"));
            var rows = new VisualElement { name = "TagRows" };
            section.Content.Add(rows);

            var shown = new List<Tag>();
            var weights = new List<Label>();
            void Build(List<Tag> tags)
            {
                rows.Clear();
                weights.Clear();
                shown.Clear();
                shown.AddRange(tags.Distinct().OrderBy(TheForge.TagText, NaturalOrder));
                foreach (var tag in shown)
                {
                    var row = Row($"Tag {tag.Name}", TheForge.TagText(tag));
                    var weight = Value("Weight");
                    row.Add(weight);
                    weights.Add(weight);
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = CountOf(shown.Count, "tag", "tags");
            }
            entityUpdates.Add(() =>
            {
                var tags = gas.GetAppliedTags() ?? new List<Tag>();
                if (tags.Distinct().Count() != shown.Count || tags.Any(tag => !shown.Contains(tag))) Build(tags);
                for (int i = 0; i < shown.Count; i++) weights[i].text = gas.GetTagWeight(shown[i]).ToString();
            });
            Build(gas.GetAppliedTags() ?? new List<Tag>());
            return section;
        }

        // The effects on the entity: each one's time left of its duration, its next tick and its stacks. The GAS Debugger
        // printed an impact's values as "current/base", with no label (ED-4)
        private VisualElement EffectsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Effects", "Effects");
            var rows = new VisualElement { name = "EffectRows" };
            section.Content.Add(rows);

            var shown = new List<AbstractEffectContainer>();
            var updates = new List<Action>();
            IReadOnlyList<AbstractEffectContainer> ShelfOf() => gas.GetEffectSystem()?.Shelf;
            void Build(IReadOnlyList<AbstractEffectContainer> shelf)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                if (shelf != null) shown.AddRange(shelf);
                foreach (var container in shown) rows.Add(EffectRow(container, updates));
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = CountOf(shown.Count, "effect", "effects");
            }
            entityUpdates.Add(() =>
            {
                var shelf = ShelfOf();
                if (!Same(shelf, shown)) Build(shelf);
                foreach (var update in updates) update();
            });
            Build(ShelfOf());
            return section;
        }

        private static VisualElement EffectRow(AbstractEffectContainer container, List<Action> updates)
        {
            var effect = container.Spec?.Base;
            var name = effect != null ? effect.GetName() : "Effect";
            var row = Row($"Effect {name}", name);
            var stacks = Value("Stacks");
            var tick = Value("Tick");
            var remaining = Value("Remaining");
            row.Add(stacks);
            row.Add(tick);
            row.Add(remaining);
            var duration = effect != null ? effect.DurationSpecification : null;
            updates.Add(() =>
            {
                remaining.text = duration?.DurationPolicy == EEffectDurationPolicy.Infinite
                    ? "Infinite"
                    : $"{Seconds(container.DurationRemaining)} of {Seconds(container.TotalDuration)}";
                tick.text = duration?.EnablePeriodicTicks == true ? $"Tick in {Seconds(container.TimeUntilPeriodTick)}" : "";
                stacks.text = container is AbstractStackingEffectContainer stacking && stacking.Stacks > 1 ? $"{stacking.Stacks} stacks" : "";
            });
            return row;
        }

        // The entity's abilities: each one's level, and whether it's active or cooling down, with the time its cooldown
        // has left. The GAS Debugger showed the cooldown effect's raw magnitude, before its scaler (ED-4)
        private VisualElement AbilitiesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Abilities", "Abilities");
            var rows = new VisualElement { name = "AbilityRows" };
            section.Content.Add(rows);

            var shown = new List<AbilitySpecContainer>();
            var updates = new List<Action>();
            List<AbilitySpecContainer> ContainersOf() => gas.GetAbilitySystem()?.GetAbilityContainers() ?? new List<AbilitySpecContainer>();
            void Build(List<AbilitySpecContainer> containers)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                shown.AddRange(containers.OrderBy(container => container.Spec?.Base != null ? container.Spec.Base.GetName() : "", NaturalOrder));
                foreach (var container in shown) rows.Add(AbilityRow(gas, container, updates));
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = CountOf(shown.Count, "ability", "abilities");
            }
            entityUpdates.Add(() =>
            {
                var containers = ContainersOf();
                if (containers.Count != shown.Count || containers.Any(container => !shown.Contains(container))) Build(containers);
                foreach (var update in updates) update();
            });
            Build(ContainersOf());
            return section;
        }

        private static VisualElement AbilityRow(IGameplayAbilitySystem gas, AbilitySpecContainer container, List<Action> updates)
        {
            var ability = container.Spec?.Base;
            var name = ability != null ? ability.GetName() : "Ability";
            var row = Row($"Ability {name}", name);
            var level = Value("Level");
            var state = Value("State");
            row.Add(level);
            row.Add(state);
            updates.Add(() =>
            {
                level.text = $"Level {container.Spec.GetLevel().CurrentValue}";
                state.text = StateOf(gas, container);
            });
            return row;
        }

        private static string StateOf(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            if (container.IsActive) return "Active";
            var cooldown = container.Spec?.Base != null ? container.Spec.Base.Cooldown : null;
            if (cooldown != null)
            {
                var left = gas.GetLongestDurationFor(cooldown.Tags.AssetTag);
                if (left.FoundDuration) return $"Cooldown {Seconds(left.DurationRemaining)}";
            }
            return "Ready";
        }

        // The last impact the entity dealt and the last it took
        private VisualElement ImpactsSection()
        {
            var section = Section("Impacts", "Impacts");
            var dealtRow = Row("Impact Dealt", "Last dealt");
            var dealt = Value("Value");
            dealtRow.Add(dealt);
            section.Content.Add(dealtRow);
            var takenRow = Row("Impact Taken", "Last taken");
            var taken = Value("Value");
            takenRow.Add(taken);
            section.Content.Add(takenRow);
            entityUpdates.Add(() =>
            {
                dealt.text = lastDealt is { } impact ? $"{ImpactText(impact)} to {NameOfTarget(impact.Target)}" : "None";
                taken.text = lastTaken is { } hit ? $"{ImpactText(hit)} from {NameOfSource(hit)}" : "None";
                section.Summary.text = lastDealt == null && lastTaken == null ? "None yet" : "";
            });
            return section;
        }

        private static string ImpactText(ImpactData impact) =>
            $"{(impact.Attribute != null ? impact.Attribute.GetName() : "Attribute")} {Signed(impact.RealImpact.CurrentValue)}";

        private static string NameOfTarget(ITarget target) =>
            target is IGameplayAbilitySystem gas ? NameOf(gas) : target?.GetName() ?? "None";

        private static string NameOfSource(ImpactData impact)
        {
            var source = impact.SourcedModifier.Carrier?.GetOrigin()?.GetSource();
            return source is IGameplayAbilitySystem gas ? NameOf(gas) : source?.GetName() ?? "None";
        }

        // Whether a list holds the same items, in order, as the window shows
        private static bool Same<T>(IReadOnlyList<T> now, List<T> shown) where T : class
        {
            if ((now?.Count ?? 0) != shown.Count) return false;
            for (int i = 0; i < shown.Count; i++)
                if (!ReferenceEquals(now[i], shown[i])) return false;
            return true;
        }
    }
}
