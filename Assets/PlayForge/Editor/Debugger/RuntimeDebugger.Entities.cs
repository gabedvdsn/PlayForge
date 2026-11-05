using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Entities view, as the design canvas's Entities board draws it: the hubs ProcessControl holds, beside the one
    // selected, with its attributes, effects, abilities and tags in a grid of four sections, each of which can change it
    // (Q27, ED-40)
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

        // What changed the shown entity's attributes in the last second: each impact it took, by what made it
        private readonly Queue<(float At, IAttribute Attribute, string By, float Delta)> recentImpacts = new();

        // The rows the shown entity's tables select, which their actions act on
        private AbstractEffectContainer selectedEffect;
        private AbilitySpecContainer selectedAbility;
        private Tag? selectedTag;

        private VisualElement BuildEntitiesView()
        {
            var view = new VisualElement { name = "EntitiesView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(0, 440, TwoPaneSplitViewOrientation.Horizontal) { name = "EntitySplit" };
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
                name = "Name", title = "Name", width = 180, stretchable = true,
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
            entityList.columns.Add(TextColumn("Hub", "Hub", 72, index => HubOf(shownHubs[index])));
            entityList.columns.Add(TextColumn("Level", "Lvl", 40, index => shownHubs[index].GetLevel().CurrentValue.ToString(), number: true));
            entityList.columns.Add(TextColumn("Effects", "Effects", 56, index => EffectsOn(shownHubs[index]).Count().ToString(), number: true));
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

        // The hubs the hub filter and the search let through, keeping the selection
        private void RefillEntities()
        {
            if (entityList == null) return;
            shownHubs.Clear();
            shownHubs.AddRange(hubs.Where(gas => ShowsHub(gas) && Matches(NameOf(gas))));
            entityList.itemsSource = shownHubs;
            entityList.Rebuild();
            int at = shownEntity != null ? shownHubs.IndexOf(shownEntity) : -1;
            entityList.SetSelectionWithoutNotify(at >= 0 ? new[] { at } : Array.Empty<int>());
            // A hub that left ProcessControl leaves the detail too
            if (shownEntity != null && !hubs.Contains(shownEntity)) ShowEntity(null);
        }

        private bool ShowsHub(IGameplayAbilitySystem gas) => hubFilter switch
        {
            HubKind.Mono => gas.ToGASObject() != null,
            HubKind.Runtime => gas.ToGASObject() == null,
            _ => true,
        };

        private static string HubOf(IGameplayAbilitySystem gas) => gas.ToGASObject() != null ? "Mono" : "Runtime";

        // An entity's effects, but one a removal has finished, which its shelf clears at its next tick
        private static IEnumerable<AbstractEffectContainer> EffectsOn(IGameplayAbilitySystem gas) =>
            gas.GetEffectSystem()?.Shelf ?? (IEnumerable<AbstractEffectContainer>)Array.Empty<AbstractEffectContainer>();

        // Shows a hub in the list and its detail, as Follow Selection and a process's Show do
        private void SelectEntity(IGameplayAbilitySystem gas)
        {
            if (view != View.Entities)
            {
                view = View.Entities;
                ShowView();
            }
            if (!shownHubs.Contains(gas))
            {
                search = "";
                searchField.SetValueWithoutNotify("");
                hubFilter = HubKind.Both;
                RefillEntities();
            }
            entityList.SetSelection(shownHubs.IndexOf(gas));
        }

        private static void SelectObjectOf(IGameplayAbilitySystem gas)
        {
            var body = gas?.ToGASObject();
            if (body == null) return;
            Selection.activeGameObject = body.gameObject;
            EditorGUIUtility.PingObject(body.gameObject);
        }

        // The asset an entity was made from, when it was made from one
        private static EntityIdentity AssetOf(IGameplayAbilitySystem gas) => gas.ToGASObject() is { } body ? body.EntityData : null;

        // ═══════════════════════════════════════════════════════════════════════════
        // The detail
        // ═══════════════════════════════════════════════════════════════════════════

        // The entity the detail shows: its header, then its attributes, effects, abilities and tags. They're built once,
        // and a sample changes their values in place; the GAS Debugger cleared and rebuilt them every 0.25 s, replacing a
        // field while it was typed in (ED-4)
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
            var grid = new VisualElement { name = "EntitySections" };
            grid.AddToClassList("pf-debugger__grid");
            // The left column's sections draw the line between the two
            var attributes = AttributesSection(gas);
            attributes.AddToClassList("pf-debugger__section--left");
            grid.Add(attributes);
            grid.Add(EffectsSection(gas));
            var abilities = AbilitiesSection(gas);
            abilities.AddToClassList("pf-debugger__section--left");
            grid.Add(abilities);
            grid.Add(TagsSection(gas));
            entityDetail.Add(grid);
            SampleEntity();
        }

        private void ForgetShownEntity()
        {
            if (shownCallbacks != null) shownCallbacks.OnFrameComplete -= OnShownFrame;
            shownCallbacks = null;
            shownEntity = null;
            recentImpacts.Clear();
            selectedEffect = null;
            selectedAbility = null;
            selectedTag = null;
        }

        // The shown entity's frame: each impact it took, by what made it. The snapshot is the entity's own and lasts only
        // through the call
        private void OnShownFrame(FrameSummarySnapshot frame)
        {
            float now = Time.unscaledTime;
            foreach (var impact in frame.ImpactsReceived)
                if (impact.Attribute != null && !Mathf.Approximately(impact.RealImpact.CurrentValue, 0f))
                    recentImpacts.Enqueue((now, impact.Attribute, ImpactSourceOf(impact), impact.RealImpact.CurrentValue));
            while (recentImpacts.Count > 0 && now - recentImpacts.Peek().At > 1f) recentImpacts.Dequeue();
        }

        // What made an impact: its effect, or what else carried it, such as a worker's change
        private static string ImpactSourceOf(ImpactData impact)
        {
            var carrier = impact.SourcedModifier.Carrier;
            if (carrier is GameplayEffectSpec spec && spec.Base != null) return spec.Base.GetName();
            var named = carrier?.GetOrigin()?.GetReadableDefinition()?.GetName();
            return string.IsNullOrWhiteSpace(named) ? "Direct" : named;
        }

        private void SampleEntity()
        {
            if (shownEntity == null) return;
            if (!Alive(shownEntity))
            {
                ShowEntity(null);
                return;
            }
            float now = Time.unscaledTime;
            while (recentImpacts.Count > 0 && now - recentImpacts.Peek().At > 1f) recentImpacts.Dequeue();
            foreach (var update in entityUpdates) update();
        }

        // The entity's type icon, name and what it is, as "Entity · Goblin Raider · Mono hub · Level 3", then Select in
        // Scene and Open Asset
        private VisualElement EntityHeader(IGameplayAbilitySystem gas)
        {
            var header = Header("EntityHeader", new ForgeTypeIcon(typeof(EntityIdentity)), out var title, out var detail);
            var asset = AssetOf(gas);
            var select = HeaderButton(header, "SelectInScene", "Select in Scene", "Select its GameObject", () => SelectObjectOf(gas));
            select.SetEnabled(gas.ToGASObject() != null);
            var open = HeaderButton(header, "OpenAsset", "Open Asset", "Open the entity's asset in a window of its own",
                () => EditorUtility.OpenPropertyEditor(asset));
            open.SetEnabled(asset != null);
            entityUpdates.Add(() =>
            {
                title.text = NameOf(gas);
                var made = asset != null ? asset.GetName() : gas.GetName();
                detail.text = $"Entity · {made} · {HubOf(gas)} hub · Level {gas.GetLevel().CurrentValue}";
            });
            return header;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes
        // ═══════════════════════════════════════════════════════════════════════════

        // Each attribute, by its base and current values, which a double-click sets (Q27), and what changed it: the
        // impacts it took in the last second, by what made them, and the effects that hold a modifier on it. The current
        // value's tooltip breaks it down by retention group, from GetBreakdown; the GAS Debugger read RetainedGroups, which
        // made a dictionary on each read (ED-4)
        private VisualElement AttributesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Attributes", "Attributes");
            section.Content.Add(TableHead(("Attribute", "pf-debugger__col-grow"), ("Base", "pf-debugger__col-56"),
                ("Current", "pf-debugger__col-64"), ("Changed by", "pf-debugger__col-grow12")));
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
                for (int i = 0; i < shown.Count; i++) rows.Add(AttributeRow(gas, shown[i], i, updates, CacheOf));
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = shown.Count.ToString();
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

        private VisualElement AttributeRow(IGameplayAbilitySystem gas, IAttribute attribute, int index, List<Action> updates,
            Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf)
        {
            var row = TableRow($"Attribute {attribute.GetName()}", index, ("Name", "pf-debugger__col-grow"), ("Base", "pf-debugger__col-56"),
                ("Current", "pf-debugger__col-64"), ("ChangedBy", "pf-debugger__col-grow12"));
            row.Q<Label>("Name").text = attribute.GetName();
            row.Q<Label>("Name").AddToClassList("pf-debugger__cell--strong");
            var @base = row.Q<Label>("Base");
            var current = row.Q<Label>("Current");
            var changedBy = row.Q<Label>("ChangedBy");
            @base.AddToClassList("pf-debugger__cell--number");
            current.AddToClassList("pf-debugger__cell--number");
            current.AddToClassList("pf-debugger__cell--strong");
            changedBy.AddToClassList("pf-debugger__cell--secondary");

            AttributeValue Now() => cacheOf()?.TryGetValue(attribute, out var cached) == true ? cached.ActiveValue : default;
            Editable(@base, () => Now().BaseValue, value => SetAttribute(gas, attribute, cacheOf, null, value));
            Editable(current, () => Now().CurrentValue, value => SetAttribute(gas, attribute, cacheOf, value, null));
            updates.Add(() =>
            {
                var cache = cacheOf();
                if (cache == null || !cache.TryGetValue(attribute, out var cached)) return;
                @base.text = Number(cached.ActiveValue.BaseValue);
                current.text = Number(cached.ActiveValue.CurrentValue);
                current.tooltip = BreakdownOf(cached);
                changedBy.text = ChangedBy(gas, attribute);
            });
            return row;
        }

        // What changed an attribute: each source of the impacts it took in the last second, with what it took a second,
        // then each effect on the entity that holds a modifier on it, as "Burning −8/s, Chilled"
        private string ChangedBy(IGameplayAbilitySystem gas, IAttribute attribute)
        {
            var parts = new List<string>();
            foreach (var group in recentImpacts.Where(impact => impact.Attribute.Equals(attribute)).GroupBy(impact => impact.By))
                parts.Add($"{group.Key} {Signed(group.Sum(impact => impact.Delta))}/s");
            foreach (var container in EffectsOn(gas))
            {
                var spec = container.Spec;
                if (spec?.Base == null) continue;
                var name = spec.Base.GetName();
                if (parts.Any(part => part.StartsWith(name + " ", StringComparison.Ordinal)) || parts.Contains(name)) continue;
                bool holds = false;
                foreach (var each in spec.GetImpactedAttributes(EImpactDelivery.Continuous))
                    if (each.Equals(attribute)) holds = true;
                if (holds) parts.Add(name);
            }
            return parts.Count == 0 ? "—" : string.Join(", ", parts);
        }

        // Each retention group's part of the current value, one a line, when there's more than one
        private static string BreakdownOf(CachedAttributeValue cached)
        {
            var parts = cached.GetBreakdown();
            if (parts == null || parts.Count <= 1) return "Double-click to set";
            return string.Join("\n", parts.Select(part => $"{TheForge.TagText(part.Group)} {Signed(part.Value.CurrentValue)}"));
        }

        // A value a double-click sets in place: a number field takes the cell's place until Enter or a click elsewhere
        // sets it, or Escape leaves it
        private void Editable(Label cell, Func<float> read, Action<float> write)
        {
            cell.AddToClassList("pf-debugger__cell--editable");
            cell.RegisterCallback<MouseDownEvent>(down =>
            {
                if (down.button != 0 || down.clickCount != 2) return;
                BeginEdit(cell, read, write);
                down.StopPropagation();
            });
        }

        private void BeginEdit(Label cell, Func<float> read, Action<float> write)
        {
            var parent = cell.parent;
            if (parent == null || parent.Q<FloatField>(cell.name + "Field") != null) return;
            var field = new FloatField { name = cell.name + "Field", isDelayed = true };
            field.SetValueWithoutNotify(read());
            field.AddToClassList("pf-debugger__edit");
            foreach (var each in cell.GetClasses().Where(each => each.StartsWith("pf-debugger__col", StringComparison.Ordinal)).ToList())
                field.AddToClassList(each);
            parent.Insert(parent.IndexOf(cell) + 1, field);
            cell.AddToClassList(HiddenClass);

            bool done = false;
            void End(bool set, float value)
            {
                if (done) return;
                done = true;
                field.RemoveFromHierarchy();
                cell.RemoveFromClassList(HiddenClass);
                if (set) write(value);
                else SampleNow();
            }
            field.RegisterValueChangedCallback(change => End(true, change.newValue));
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                bool parsed = float.TryParse(field.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed);
                End(parsed, typed);
            });
            field.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode != KeyCode.Escape) return;
                End(false, 0f);
                key.StopPropagation();
            }, TrickleDown.TrickleDown);
            field.schedule.Execute(() => field.Q<TextElement>()?.Focus());
        }

        // The entity takes a value set in an attribute's cell as it is: the change goes in directly, as a delta no worker
        // sees, and the next sample shows what the entity holds (Q27)
        private void SetAttribute(IGameplayAbilitySystem gas, IAttribute attribute, Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf,
            float? current, float? @base)
        {
            var cache = cacheOf();
            if (!Alive(gas) || cache == null || !cache.TryGetValue(attribute, out var cached)) return;
            var now = cached.ActiveValue;
            float toCurrent = current.HasValue ? current.Value - now.CurrentValue : 0f;
            float toBase = @base.HasValue ? @base.Value - now.BaseValue : 0f;
            if (toCurrent != 0f || toBase != 0f)
                gas.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateUnworkable(gas, attribute, toCurrent, toBase));
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects
        // ═══════════════════════════════════════════════════════════════════════════

        // The effects on the entity: each one's time left, its stacks and where it came from, as "Player · Fireball". An
        // effect picked under the table lands from the entity itself, and Remove takes the selected one off (Q27). The GAS
        // Debugger printed an impact's values as "current/base", with no label (ED-4)
        private VisualElement EffectsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Effects", "Effects");
            section.Content.Add(TableHead(("Effect", "pf-debugger__col-grow"), ("Left", "pf-debugger__col-64"),
                ("Stacks", "pf-debugger__col-48"), ("From", "pf-debugger__col-grow11")));
            var rows = new VisualElement { name = "EffectRows" };
            section.Content.Add(rows);

            var actions = Actions("EffectActions");
            var newEffect = new ObjectField { name = "NewEffect", objectType = typeof(GameplayEffect), allowSceneObjects = false, tooltip = "An effect to apply" };
            newEffect.AddToClassList("pf-debugger__pick");
            actions.Add(newEffect);
            var applied = new Label { name = "ApplyResult" };
            applied.AddToClassList("pf-debugger__result");
            ActionButton(actions, "ApplyEffect", "Apply", "Apply it to the entity, from the entity",
                () => ApplyEffect(gas, newEffect.value as GameplayEffect, applied));
            var remove = ActionButton(actions, "RemoveEffect", "Remove", "Take the selected effect off, where its source applied it",
                () => RemoveEffect(gas, selectedEffect));
            actions.Add(applied);
            section.Content.Add(actions);

            var shown = new List<AbstractEffectContainer>();
            var updates = new List<Action>();
            IReadOnlyList<AbstractEffectContainer> ShelfOf() => gas.GetEffectSystem()?.Shelf;
            void Build(IReadOnlyList<AbstractEffectContainer> shelf)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                if (shelf != null) shown.AddRange(shelf);
                for (int i = 0; i < shown.Count; i++)
                {
                    var container = shown[i];
                    var row = EffectRow(gas, container, i, updates);
                    Selectable(row, () =>
                    {
                        selectedEffect = container;
                        ShowSelected(rows, row);
                        ShowRemove();
                    });
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                if (selectedEffect != null && !shown.Contains(selectedEffect)) selectedEffect = null;
                ShowSelected(rows, selectedEffect != null ? rows.Children().ElementAtOrDefault(shown.IndexOf(selectedEffect)) : null);
                section.Summary.text = shown.Count.ToString();
                ShowRemove();
            }
            void ShowRemove()
            {
                var name = selectedEffect?.Spec?.Base != null ? selectedEffect.Spec.Base.GetName() : null;
                remove.text = name != null ? $"Remove {name}" : "Remove";
                remove.SetEnabled(name != null);
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

        private VisualElement EffectRow(IGameplayAbilitySystem gas, AbstractEffectContainer container, int index, List<Action> updates)
        {
            var effect = container.Spec?.Base;
            var name = effect != null ? effect.GetName() : "Effect";
            var row = TableRow($"Effect {name}", index, ("Left", "pf-debugger__col-64"), ("Stacks", "pf-debugger__col-48"),
                ("From", "pf-debugger__col-grow11"));
            Lead(row, NameCell(typeof(GameplayEffect), name));
            var left = row.Q<Label>("Left");
            var stacks = row.Q<Label>("Stacks");
            var from = row.Q<Label>("From");
            left.AddToClassList("pf-debugger__cell--number");
            stacks.AddToClassList("pf-debugger__cell--number");
            from.AddToClassList("pf-debugger__cell--secondary");
            var duration = effect != null ? effect.DurationSpecification : null;
            updates.Add(() =>
            {
                left.text = duration?.DurationPolicy == EEffectDurationPolicy.Infinite ? "Infinite" : Seconds(container.DurationRemaining);
                left.tooltip = duration?.DurationPolicy == EEffectDurationPolicy.Infinite
                    ? ""
                    : $"{Seconds(container.DurationRemaining)} of {Seconds(container.TotalDuration)}" +
                      (duration?.EnablePeriodicTicks == true ? $", next tick in {Seconds(container.TimeUntilPeriodTick)}" : "");
                stacks.text = container is AbstractStackingEffectContainer stacking ? stacking.Stacks.ToString() : "1";
                from.text = OriginOf(gas, container.Spec);
            });
            return row;
        }

        // A name cell with its type's icon, as the boards draw an effect's or an ability's
        private static VisualElement NameCell(Type type, string name)
        {
            var cell = new VisualElement { name = "NameCell" };
            cell.AddToClassList("pf-debugger__cell");
            cell.AddToClassList("pf-debugger__col-grow");
            cell.AddToClassList("pf-debugger__name-cell");
            cell.Add(new ForgeTypeIcon(type));
            var label = new Label(name) { name = "Name" };
            label.AddToClassList("pf-debugger__name");
            cell.Add(label);
            return cell;
        }

        // Where an effect came from: its source, then what applied it, as "Player · Fireball", or Self
        private static string OriginOf(IGameplayAbilitySystem gas, GameplayEffectSpec spec)
        {
            var origin = spec?.GetOrigin();
            var source = origin?.GetSource();
            if (source == null) return "—";
            var by = origin.GetReadableDefinition();
            var what = by != null && !ReferenceEquals(by, source) ? by.GetName() : null;
            if (ReferenceEquals(source, gas)) return string.IsNullOrWhiteSpace(what) ? "Self" : $"Self · {what}";
            var who = source is IGameplayAbilitySystem other ? NameOf(other) : source.GetName();
            return string.IsNullOrWhiteSpace(what) ? who : $"{who} · {what}";
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
        private void RemoveEffect(IGameplayAbilitySystem gas, AbstractEffectContainer container)
        {
            var effect = container?.Spec?.Base;
            if (effect == null || !Alive(gas)) return;
            var origin = container.Spec.GetOrigin();
            if (origin != null) gas.RemoveGameplayEffect(effect, origin);
            else gas.RemoveGameplayEffect(effect);
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities
        // ═══════════════════════════════════════════════════════════════════════════

        // The entity's abilities: each one's level, its state, as "Stage 2/3", and the time its cooldown has left. The
        // selected one can be activated, interrupted or taken away, and one picked under the table is granted at the level
        // given (Q27). The GAS Debugger showed the cooldown effect's raw magnitude, before its scaler (ED-4)
        private VisualElement AbilitiesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Abilities", "Abilities");
            section.Content.Add(TableHead(("Ability", "pf-debugger__col-grow"), ("Lvl", "pf-debugger__col-40"),
                ("State", "pf-debugger__col-grow13"), ("Cooldown", "pf-debugger__col-72")));
            var rows = new VisualElement { name = "AbilityRows" };
            section.Content.Add(rows);

            var actions = Actions("AbilityActions");
            var result = new Label { name = "Result" };
            result.AddToClassList("pf-debugger__result");
            var activate = ActionButton(actions, "ActivateAbility", "Activate", "Set the selected ability off, as the entity would",
                () => ActivateAbility(gas, selectedAbility, result));
            var interrupt = ActionButton(actions, "InterruptAbility", "Interrupt", "Stop the selected ability's run",
                () => InterruptAbility(gas, selectedAbility, result));
            var remove = ActionButton(actions, "RemoveAbility", "Remove", "Take the selected ability away from the entity",
                () => RemoveAbility(gas, selectedAbility));
            actions.Add(result);
            section.Content.Add(actions);

            var grant = Actions("AbilityGrant");
            var newAbility = new ObjectField { name = "NewAbility", objectType = typeof(Ability), allowSceneObjects = false, tooltip = "An ability to grant" };
            newAbility.AddToClassList("pf-debugger__pick");
            grant.Add(newAbility);
            var level = new IntegerField { name = "NewAbilityLevel", value = 1, tooltip = "The level to grant it at" };
            level.AddToClassList("pf-debugger__level");
            grant.Add(level);
            var granted = new Label { name = "GrantResult" };
            granted.AddToClassList("pf-debugger__result");
            ActionButton(grant, "GrantAbility", "Grant", "Grant it to the entity at this level",
                () => GrantAbility(gas, newAbility.value as Ability, level.value, granted));
            grant.Add(granted);
            section.Content.Add(grant);

            var shown = new List<AbilitySpecContainer>();
            var updates = new List<Action>();
            List<AbilitySpecContainer> ContainersOf() => gas.GetAbilitySystem()?.GetAbilityContainers() ?? new List<AbilitySpecContainer>();
            void Build(List<AbilitySpecContainer> containers)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                shown.AddRange(containers.OrderBy(container => container.Spec?.Base != null ? container.Spec.Base.GetName() : "", NaturalOrder));
                for (int i = 0; i < shown.Count; i++)
                {
                    var container = shown[i];
                    var row = AbilityRow(gas, container, i, updates);
                    Selectable(row, () =>
                    {
                        selectedAbility = container;
                        ShowSelected(rows, row);
                        ShowActions();
                    });
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                if (selectedAbility != null && !shown.Contains(selectedAbility)) selectedAbility = null;
                ShowSelected(rows, selectedAbility != null ? rows.Children().ElementAtOrDefault(shown.IndexOf(selectedAbility)) : null);
                section.Summary.text = shown.Count.ToString();
                ShowActions();
            }
            void ShowActions()
            {
                var name = selectedAbility?.Spec?.Base != null ? selectedAbility.Spec.Base.GetName() : null;
                activate.text = name != null ? $"Activate {name}" : "Activate";
                interrupt.text = name != null ? $"Interrupt {name}" : "Interrupt";
                remove.text = name != null ? $"Remove {name}" : "Remove";
                activate.SetEnabled(name != null);
                interrupt.SetEnabled(name != null && selectedAbility.IsActive);
                remove.SetEnabled(name != null);
            }
            entityUpdates.Add(() =>
            {
                var containers = ContainersOf();
                if (containers.Count != shown.Count || containers.Any(container => !shown.Contains(container))) Build(containers);
                foreach (var update in updates) update();
                ShowActions();
            });
            Build(ContainersOf());
            return section;
        }

        private VisualElement AbilityRow(IGameplayAbilitySystem gas, AbilitySpecContainer container, int index, List<Action> updates)
        {
            var ability = container.Spec?.Base;
            var name = ability != null ? ability.GetName() : "Ability";
            var row = TableRow($"Ability {name}", index, ("Level", "pf-debugger__col-40"), ("State", "pf-debugger__col-grow13"),
                ("Cooldown", "pf-debugger__col-72"));
            Lead(row, NameCell(typeof(Ability), name));
            var level = row.Q<Label>("Level");
            var state = row.Q<Label>("State");
            var cooldown = row.Q<Label>("Cooldown");
            level.AddToClassList("pf-debugger__cell--number");
            cooldown.AddToClassList("pf-debugger__cell--number");
            updates.Add(() =>
            {
                level.text = container.Spec.GetLevel().CurrentValue.ToString();
                state.text = StateOf(container);
                var left = CooldownOf(gas, container);
                cooldown.text = left > 0f ? Seconds(left) : "—";
            });
            return row;
        }

        // An ability's state: the stage its run is in, as "Stage 2/3", or Active, Cooldown or Ready
        private static string StateOf(AbilitySpecContainer container)
        {
            if (container.IsActive)
            {
                int stage = container.ActiveStageIndex;
                int stages = container.Spec?.Base?.Behaviour?.Stages?.Count ?? 0;
                return stage >= 0 && stages > 0 ? $"Stage {stage + 1}/{stages}" : "Active";
            }
            return container.IsCooldown ? "Cooldown" : "Ready";
        }

        // The time an ability's cooldown has left, its cooldown effect's, after its scaler, or 0
        private static float CooldownOf(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            var cooldown = container.Spec?.Base != null ? container.Spec.Base.Cooldown : null;
            if (cooldown == null) return 0f;
            var left = gas.GetLongestDurationFor(cooldown.Tags.AssetTag);
            return left.FoundDuration ? left.DurationRemaining : 0f;
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
            if (container == null || abilities == null || !Alive(gas)) return;
            result.text = abilities.TryActivateAbility(abilities.CreateActivationRequest(container.Index)) ? "Activated" : "Refused";
            SampleNow();
        }

        private void InterruptAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container, Label result)
        {
            var abilities = gas?.GetAbilitySystem();
            if (container == null || abilities == null || !Alive(gas) || !container.IsActive) return;
            abilities.Inject(container.Index, InterruptSequenceInjection.Instance);
            result.text = "Interrupted";
            SampleNow();
        }

        // The copy in this row, where the entity holds an ability more than once
        private void RemoveAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            var abilities = gas?.GetAbilitySystem();
            if (container == null || abilities == null || !Alive(gas)) return;
            abilities.RemoveAbilityAt(container.Index, container.Spec?.Base);
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags
        // ═══════════════════════════════════════════════════════════════════════════

        // The tags the entity has, each with its count, its weight, and what grants it. A tag's name, typed or picked from
        // the project's, grants it as a loose tag (FEAT-16), which Remove takes back from the selected row (Q27). A tag
        // added straight to the entity's cache went at its next compile. The Tag Debugger listed tags in a window of its own
        private VisualElement TagsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Tags", "Tags");
            section.Content.Add(TableHead(("Tag", "pf-debugger__col-grow"), ("Count", "pf-debugger__col-56"), ("From", "pf-debugger__col-grow")));
            var rows = new VisualElement { name = "TagRows" };
            section.Content.Add(rows);

            var actions = Actions("TagActions");
            var newTag = new TextField { name = "NewTag", tooltip = "A tag's name, such as Status.Stunned" };
            newTag.AddToClassList("pf-debugger__new-tag");
            // Enter grants it, as Add Tag does
            newTag.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;
                GrantTag(gas, newTag);
                key.StopPropagation();
            }, TrickleDown.TrickleDown);
            actions.Add(newTag);
            ActionButton(actions, "AddTag", "Add Tag", "Grant the entity this tag until it's removed", () => GrantTag(gas, newTag));
            Button pick = null;
            pick = ActionButton(actions, "PickTag", "Project Tags", "Grant one of the project's tags", () => PickTag(gas, pick));
            var remove = ActionButton(actions, "RemoveTag", "Remove", "Take back one of the selected tag's loose grants",
                () => TakeTag(gas, selectedTag));
            section.Content.Add(actions);

            var shown = new List<Tag>();
            var looseShown = new List<Tag>();
            var updates = new List<Action>();
            void Build(List<Tag> tags, IReadOnlyList<Tag> loose)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                looseShown.Clear();
                shown.AddRange(tags.Distinct().OrderBy(tag => tag.Name ?? "", NaturalOrder));
                looseShown.AddRange(loose.Distinct());
                for (int i = 0; i < shown.Count; i++)
                {
                    var tag = shown[i];
                    var row = TableRow($"Tag {tag.Name}", i, ("Count", "pf-debugger__col-56"), ("From", "pf-debugger__col-grow"));
                    Lead(row, TagNameCell(tag));
                    var count = row.Q<Label>("Count");
                    var from = row.Q<Label>("From");
                    count.AddToClassList("pf-debugger__cell--number");
                    from.AddToClassList("pf-debugger__cell--secondary");
                    updates.Add(() =>
                    {
                        count.text = gas.GetTagWeight(tag).ToString();
                        from.text = string.Join(", ", SourcesOf(gas, tag).Select(source => source.By).Distinct());
                    });
                    Selectable(row, () =>
                    {
                        selectedTag = tag;
                        ShowSelected(rows, row);
                        ShowRemove();
                    });
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                if (selectedTag is { } chosen && !shown.Contains(chosen)) selectedTag = null;
                ShowSelected(rows, selectedTag is { } kept ? rows.Children().ElementAtOrDefault(shown.IndexOf(kept)) : null);
                section.Summary.text = shown.Count.ToString();
                ShowRemove();
            }
            void ShowRemove()
            {
                bool loose = selectedTag is { } tag && looseShown.Contains(tag);
                remove.text = selectedTag is { } named ? $"Remove {TheForge.TagText(named)}" : "Remove";
                remove.SetEnabled(loose);
            }
            entityUpdates.Add(() =>
            {
                var tags = gas.GetAppliedTags() ?? new List<Tag>();
                var loose = gas.GetLooseTags() ?? Array.Empty<Tag>();
                bool sameTags = tags.Distinct().Count() == shown.Count && tags.All(shown.Contains);
                bool sameLoose = loose.Distinct().Count() == looseShown.Count && loose.All(looseShown.Contains);
                if (!sameTags || !sameLoose) Build(tags, loose);
                foreach (var update in updates) update();
            });
            Build(gas.GetAppliedTags() ?? new List<Tag>(), gas.GetLooseTags() ?? Array.Empty<Tag>());
            return section;
        }

        // A tag's path: its parents in secondary text and its leaf in strong text
        private static VisualElement TagNameCell(Tag tag)
        {
            var cell = new VisualElement { name = "NameCell" };
            cell.AddToClassList("pf-debugger__cell");
            cell.AddToClassList("pf-debugger__col-grow");
            cell.AddToClassList("pf-debugger__tag-name");
            var text = tag.Name ?? "";
            int dot = text.LastIndexOf('.');
            var parents = new Label(dot >= 0 ? text.Substring(0, dot + 1) : "") { name = "Parents" };
            parents.AddToClassList("pf-debugger__tag-parents");
            cell.Add(parents);
            var leaf = new Label(dot >= 0 ? text.Substring(dot + 1) : text) { name = "Name" };
            leaf.AddToClassList("pf-debugger__tag-leaf");
            cell.Add(leaf);
            return cell;
        }

        private void GrantTag(IGameplayAbilitySystem gas, TextField field)
        {
            var name = field.value?.Trim();
            if (string.IsNullOrEmpty(name) || !Alive(gas)) return;
            gas.AddLooseTag(Tag.Generate(name));
            field.SetValueWithoutNotify("");
            SampleNow();
        }

        private void TakeTag(IGameplayAbilitySystem gas, Tag? tag)
        {
            if (tag is not { } taken || !Alive(gas)) return;
            gas.RemoveLooseTag(taken);
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
