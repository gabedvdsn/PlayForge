using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
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

        // Each attribute, by its current value and its base, which take a value typed in (Q27). The current value's
        // tooltip breaks it down by retention group, from GetBreakdown; the GAS Debugger read RetainedGroups, which made
        // a dictionary on each read (ED-4)
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
                foreach (var attribute in shown) rows.Add(AttributeRow(gas, attribute, updates, CacheOf));
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

        private VisualElement AttributeRow(IGameplayAbilitySystem gas, IAttribute attribute, List<Action> updates,
            Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf)
        {
            var row = Row($"Attribute {attribute.GetName()}", attribute.GetName());
            var current = ValueField("Current");
            var @base = ValueField("Base");
            @base.tooltip = "Its base value";
            current.RegisterValueChangedCallback(change => SetAttribute(gas, attribute, cacheOf, change.newValue, null));
            @base.RegisterValueChangedCallback(change => SetAttribute(gas, attribute, cacheOf, null, change.newValue));
            row.Add(current);
            row.Add(@base);
            updates.Add(() =>
            {
                var cache = cacheOf();
                if (cache == null || !cache.TryGetValue(attribute, out var cached)) return;
                // A field being typed in keeps what's typed
                if (!Focused(current)) current.SetValueWithoutNotify(cached.ActiveValue.CurrentValue);
                if (!Focused(@base)) @base.SetValueWithoutNotify(cached.ActiveValue.BaseValue);
                current.tooltip = BreakdownOf(cached);
            });
            return row;
        }

        private static FloatField ValueField(string name)
        {
            var field = new FloatField { name = name, isDelayed = true };
            field.AddToClassList("pf-debugger__value");
            return field;
        }

        // Whether a field has the keyboard, as while it's typed in
        private static bool Focused(VisualElement field) =>
            field.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        // The entity takes a value typed into an attribute's field as it is: the change goes in directly, as a delta no
        // worker sees, and the next sample shows what the entity holds (Q27)
        private void SetAttribute(IGameplayAbilitySystem gas, IAttribute attribute, Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf,
            float? current, float? @base)
        {
            var cache = cacheOf();
            if (!Alive(gas) || cache == null || !cache.TryGetValue(attribute, out var cached)) return;
            var now = cached.ActiveValue;
            float toCurrent = current.HasValue ? current.Value - now.CurrentValue : 0f;
            float toBase = @base.HasValue ? @base.Value - now.BaseValue : 0f;
            if (toCurrent == 0f && toBase == 0f) return;
            gas.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateUnworkable(gas, attribute, toCurrent, toBase));
            SampleNow();
        }

        // Each retention group's part of the current value, one a line, when there's more than one
        private static string BreakdownOf(CachedAttributeValue cached)
        {
            var parts = cached.GetBreakdown();
            if (parts == null || parts.Count <= 1) return "Its current value";
            return string.Join("\n", parts.Select(part => $"{TheForge.TagText(part.Group)} {Signed(part.Value.CurrentValue)}"));
        }

        // The tags the entity has, each with its weight: how many of its grants hold it. A tag's name, typed or picked
        // from the project's, grants it as a loose tag (FEAT-16), which its row's Remove takes back (Q27). A tag added
        // straight to the entity's cache went at its next compile. The Tag Debugger listed tags in a window of its own
        private VisualElement TagsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Tags", "Tags");
            var add = new VisualElement { name = "TagAdd" };
            add.AddToClassList("pf-debugger__row");
            var newTag = new TextField { name = "NewTag", tooltip = "A tag's name, such as Status.Stunned" };
            newTag.AddToClassList("pf-debugger__new-tag");
            // Enter grants it, as Add Tag does
            newTag.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;
                GrantTag(gas, newTag);
                key.StopPropagation();
            }, TrickleDown.TrickleDown);
            add.Add(newTag);
            var addButton = new Button(() => GrantTag(gas, newTag)) { name = "AddTag", text = "Add Tag", tooltip = "Grant the entity this tag until it's removed" };
            addButton.AddToClassList("pf-debugger__row-button");
            add.Add(addButton);
            Button pick = null;
            pick = new Button(() => PickTag(gas, pick)) { name = "PickTag", text = "Project Tags", tooltip = "Grant one of the project's tags" };
            pick.AddToClassList("pf-debugger__row-button");
            add.Add(pick);
            section.Content.Add(add);

            section.Content.Add(ColumnTitles("Tag", "Weight"));
            var rows = new VisualElement { name = "TagRows" };
            section.Content.Add(rows);

            var shown = new List<Tag>();
            var looseShown = new List<Tag>();
            var weights = new List<Label>();
            void Build(List<Tag> tags, IReadOnlyList<Tag> loose)
            {
                rows.Clear();
                weights.Clear();
                shown.Clear();
                looseShown.Clear();
                shown.AddRange(tags.Distinct().OrderBy(TheForge.TagText, NaturalOrder));
                looseShown.AddRange(loose.Distinct());
                foreach (var tag in shown)
                {
                    var row = Row($"Tag {tag.Name}", TheForge.TagText(tag));
                    var weight = Value("Weight");
                    row.Add(weight);
                    weights.Add(weight);
                    if (looseShown.Contains(tag))
                    {
                        var remove = new Button(() => TakeTag(gas, tag)) { name = "RemoveTag", text = "Remove", tooltip = "Take back one of its loose grants" };
                        remove.AddToClassList("pf-debugger__row-button");
                        row.Add(remove);
                    }
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = CountOf(shown.Count, "tag", "tags");
            }
            entityUpdates.Add(() =>
            {
                var tags = gas.GetAppliedTags() ?? new List<Tag>();
                var loose = gas.GetLooseTags() ?? Array.Empty<Tag>();
                bool sameTags = tags.Distinct().Count() == shown.Count && tags.All(shown.Contains);
                bool sameLoose = loose.Distinct().Count() == looseShown.Count && loose.All(looseShown.Contains);
                if (!sameTags || !sameLoose) Build(tags, loose);
                for (int i = 0; i < shown.Count; i++) weights[i].text = gas.GetTagWeight(shown[i]).ToString();
            });
            Build(gas.GetAppliedTags() ?? new List<Tag>(), gas.GetLooseTags() ?? Array.Empty<Tag>());
            return section;
        }

        private void GrantTag(IGameplayAbilitySystem gas, TextField field)
        {
            var name = field.value?.Trim();
            if (string.IsNullOrEmpty(name) || !Alive(gas)) return;
            gas.AddLooseTag(Tag.Generate(name));
            field.SetValueWithoutNotify("");
            SampleNow();
        }

        private void TakeTag(IGameplayAbilitySystem gas, Tag tag)
        {
            if (!Alive(gas)) return;
            gas.RemoveLooseTag(tag);
            SampleNow();
        }

        // The project's tags, each under its root, which grant themselves
        private void PickTag(IGameplayAbilitySystem gas, VisualElement under)
        {
            var menu = new GenericMenu();
            foreach (var record in ForgeTagRegistry.GetAllTagRecords().OrderBy(record => record.Tag.Name, NaturalOrder))
            {
                var tag = record.Tag;
                var root = tag.Name.Split('.')[0];
                menu.AddItem(new GUIContent($"{root}/{tag.Name}"), false, () =>
                {
                    if (!Alive(gas)) return;
                    gas.AddLooseTag(tag);
                    SampleNow();
                });
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("The project has no tags"));
            menu.DropDown(under.worldBound);
        }

        // The effects on the entity: each one's time left of its duration, its next tick and its stacks, and Remove. An
        // effect picked here lands on the entity, from the entity itself (Q27). The GAS Debugger printed an impact's values
        // as "current/base", with no label (ED-4)
        private VisualElement EffectsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Effects", "Effects");
            var apply = new VisualElement { name = "EffectApply" };
            apply.AddToClassList("pf-debugger__row");
            var newEffect = new ObjectField { name = "NewEffect", objectType = typeof(GameplayEffect), allowSceneObjects = false, tooltip = "An effect to apply" };
            newEffect.AddToClassList("pf-debugger__pick");
            apply.Add(newEffect);
            var applied = Value("ApplyResult");
            var applyButton = new Button(() => ApplyEffect(gas, newEffect.value as GameplayEffect, applied))
            {
                name = "ApplyEffect", text = "Apply", tooltip = "Apply it to the entity, from the entity",
            };
            applyButton.AddToClassList("pf-debugger__row-button");
            apply.Add(applyButton);
            apply.Add(applied);
            section.Content.Add(apply);
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
                foreach (var container in shown) rows.Add(EffectRow(gas, container, updates));
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

        private VisualElement EffectRow(IGameplayAbilitySystem gas, AbstractEffectContainer container, List<Action> updates)
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
            if (effect != null)
            {
                var remove = new Button(() => RemoveEffect(gas, effect, container.Spec.Origin))
                {
                    name = "RemoveEffect", text = "Remove", tooltip = "Take it off the entity, where its source applied it",
                };
                remove.AddToClassList("pf-debugger__row-button");
                row.Add(remove);
            }
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
        // has left, then Activate and Remove. An ability picked here is granted at the level given (Q27). The GAS
        // Debugger showed the cooldown effect's raw magnitude, before its scaler (ED-4)
        private VisualElement AbilitiesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Abilities", "Abilities");
            var grant = new VisualElement { name = "AbilityGrant" };
            grant.AddToClassList("pf-debugger__row");
            var newAbility = new ObjectField { name = "NewAbility", objectType = typeof(Ability), allowSceneObjects = false, tooltip = "An ability to grant" };
            newAbility.AddToClassList("pf-debugger__pick");
            grant.Add(newAbility);
            var level = new IntegerField { name = "NewAbilityLevel", value = 1, tooltip = "The level to grant it at" };
            level.AddToClassList("pf-debugger__level");
            grant.Add(level);
            var granted = Value("GrantResult");
            var grantButton = new Button(() => GrantAbility(gas, newAbility.value as Ability, level.value, granted))
            {
                name = "GrantAbility", text = "Grant", tooltip = "Grant it to the entity at this level",
            };
            grantButton.AddToClassList("pf-debugger__row-button");
            grant.Add(grantButton);
            grant.Add(granted);
            section.Content.Add(grant);
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

        private VisualElement AbilityRow(IGameplayAbilitySystem gas, AbilitySpecContainer container, List<Action> updates)
        {
            var ability = container.Spec?.Base;
            var name = ability != null ? ability.GetName() : "Ability";
            var row = Row($"Ability {name}", name);
            var level = Value("Level");
            var state = Value("State");
            var result = Value("Result");
            row.Add(level);
            row.Add(state);
            row.Add(result);
            var activate = new Button(() => ActivateAbility(gas, container, result))
            {
                name = "ActivateAbility", text = "Activate", tooltip = "Set it off, as the entity would",
            };
            activate.AddToClassList("pf-debugger__row-button");
            row.Add(activate);
            var remove = new Button(() => RemoveAbility(gas, container)) { name = "RemoveAbility", text = "Remove", tooltip = "Take it away from the entity" };
            remove.AddToClassList("pf-debugger__row-button");
            row.Add(remove);
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

        // An effect applied in the debugger lands as one the entity applied to itself would
        private void ApplyEffect(IGameplayAbilitySystem gas, GameplayEffect effect, Label result)
        {
            if (effect == null || !Alive(gas)) return;
            bool landed = gas.ApplyGameplayEffect(gas.GenerateEffectSpec(IEffectOrigin.GenerateSourceDerivation(gas), effect));
            result.text = landed ? "Applied" : "Refused";
            SampleNow();
        }

        // The entity clears a removed effect off its shelf at its next tick
        private void RemoveEffect(IGameplayAbilitySystem gas, GameplayEffect effect, IEffectOrigin origin)
        {
            if (!Alive(gas)) return;
            if (origin != null) gas.RemoveGameplayEffect(effect, origin);
            else gas.RemoveGameplayEffect(effect);
            SampleNow();
        }

        private void GrantAbility(IGameplayAbilitySystem gas, Ability ability, int level, Label result)
        {
            var abilities = gas?.GetAbilitySystem();
            if (ability == null || abilities == null || !Alive(gas)) return;
            result.text = abilities.GiveAbility(ability, Math.Max(1, level), out _) ? "Granted" : "Refused";
            SampleNow();
        }

        private void ActivateAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container, Label result)
        {
            var abilities = gas?.GetAbilitySystem();
            if (abilities == null || !Alive(gas)) return;
            result.text = abilities.TryActivateAbility(abilities.CreateActivationRequest(container.Index)) ? "Activated" : "Refused";
            SampleNow();
        }

        // The copy in this row, where the entity holds an ability more than once
        private void RemoveAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            var abilities = gas?.GetAbilitySystem();
            if (abilities == null || !Alive(gas)) return;
            abilities.RemoveAbilityAt(container.Index, container.Spec?.Base);
            SampleNow();
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
