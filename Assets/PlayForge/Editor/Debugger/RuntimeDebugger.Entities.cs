using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Entities view, as the design canvas's Entities board draws it (version 22): the hubs ProcessControl holds,
    // beside the one selected, with its attributes, effects, abilities and tags in a grid of four sections, each of
    // whose foot rows changes it (Q27, ED-40)
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

        // The rows the shown entity's tables select, which their foot rows act on
        private IAttribute selectedAttribute;
        private AbstractEffectContainer selectedEffect;
        private AbilitySpecContainer selectedAbility;
        private Tag? selectedTag;

        private VisualElement BuildEntitiesView()
        {
            var view = new VisualElement { name = "EntitiesView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(0, 440, TwoPaneSplitViewOrientation.Horizontal) { name = "EntitySplit" };
            entitySplit = split;
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
            ForgeColumnWidths.Remember(entityList.columns, "Debugger.Entities");
            entityList.selectionChanged +=chosen => ShowEntity(chosen.OfType<IGameplayAbilitySystem>().FirstOrDefault());
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
            selectedAttribute = null;
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
        // The sections' foot rows, as the board draws them (canvas version 22)
        // ═══════════════════════════════════════════════════════════════════════════

        // A section's foot row, under a thin rule: the actions on the row its table selects, then what adds to it. An
        // action that doesn't apply stays in place, disabled, so nothing shifts
        private static VisualElement Foot(string name)
        {
            var foot = new VisualElement { name = name };
            foot.AddToClassList("pf-debugger__foot");
            return foot;
        }

        // A foot row of two lines, as the Abilities section's
        private static VisualElement FootLine(VisualElement foot, string name)
        {
            foot.AddToClassList("pf-debugger__foot--lines");
            var line = new VisualElement { name = name };
            line.AddToClassList("pf-debugger__foot-line");
            foot.Add(line);
            return line;
        }

        private static Label FootLabel(VisualElement foot, string text)
        {
            var label = new Label(text);
            label.AddToClassList("pf-debugger__foot-label");
            foot.Add(label);
            return label;
        }

        // Whether a field has the keyboard, so a sample leaves what's typed in it alone
        private static bool HasFocus(VisualElement field)
        {
            var focused = field.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == field || field.Contains(focused));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes
        // ═══════════════════════════════════════════════════════════════════════════

        // Each attribute, by its base and current values and what changed it: the impacts it took in the last second, by
        // what made them, and the effects that hold a modifier on it. The current value's tooltip breaks it down by
        // retention group, from GetBreakdown; the GAS Debugger read RetainedGroups, which made a dictionary on each read
        // (ED-4). The foot row sets the selected attribute's values (Q27); a double-click on a value goes to its field
        private VisualElement AttributesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Attributes", "Attributes");
            section.Content.Add(TableHead(("Attribute", "pf-debugger__col-grow"), ("Base", "pf-debugger__col-56"),
                ("Current", "pf-debugger__col-64"), ("Changed by", "pf-debugger__col-grow12")));
            var rows = new VisualElement { name = "AttributeRows" };
            section.Content.Add(rows);

            IReadOnlyDictionary<IAttribute, CachedAttributeValue> CacheOf() => gas.GetAttributeSystem()?.GetAttributeCache();
            AttributeValue? Now(IAttribute attribute) =>
                attribute != null && CacheOf()?.TryGetValue(attribute, out var cached) == true ? cached.ActiveValue : null;

            var foot = Foot("AttributeFoot");
            var selectedName = new Label { name = "SelectedAttribute" };
            selectedName.AddToClassList("pf-debugger__foot-name");
            foot.Add(selectedName);
            FootLabel(foot, "Base");
            var baseField = new FloatField { name = "SelectedBase", isDelayed = true, tooltip = "The selected attribute's base value; Enter sets it" };
            baseField.AddToClassList("pf-debugger__foot-number");
            foot.Add(baseField);
            FootLabel(foot, "Current");
            var currentField = new FloatField { name = "SelectedCurrent", isDelayed = true, tooltip = "The selected attribute's current value; Enter sets it" };
            currentField.AddToClassList("pf-debugger__foot-number");
            foot.Add(currentField);
            baseField.RegisterValueChangedCallback(change => SetAttribute(gas, selectedAttribute, CacheOf, null, change.newValue));
            currentField.RegisterValueChangedCallback(change => SetAttribute(gas, selectedAttribute, CacheOf, change.newValue, null));
            section.Content.Add(foot);

            void ShowFoot()
            {
                var now = Now(selectedAttribute);
                bool chosen = now.HasValue;
                selectedName.text = chosen ? selectedAttribute.GetName() : "None";
                selectedName.EnableInClassList("pf-debugger__foot-name--none", !chosen);
                baseField.SetEnabled(chosen);
                currentField.SetEnabled(chosen);
                if (!HasFocus(baseField)) baseField.SetValueWithoutNotify(chosen ? now.Value.BaseValue : 0f);
                if (!HasFocus(currentField)) currentField.SetValueWithoutNotify(chosen ? now.Value.CurrentValue : 0f);
            }

            var shown = new List<IAttribute>();
            var updates = new List<Action>();
            void Build(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                if (cache != null) shown.AddRange(cache.Keys.OrderBy(attribute => attribute.GetName(), NaturalOrder));
                for (int i = 0; i < shown.Count; i++)
                {
                    var attribute = shown[i];
                    var row = AttributeRow(gas, attribute, i, updates, CacheOf);
                    void Choose()
                    {
                        selectedAttribute = attribute;
                        ShowSelected(rows, row);
                        ShowFoot();
                    }
                    Selectable(row, Choose);
                    // A double-click on a value goes to its field in the foot row
                    GoesToField(row.Q<Label>("Base"), Choose, baseField);
                    GoesToField(row.Q<Label>("Current"), Choose, currentField);
                    rows.Add(row);
                }
                if (shown.Count == 0) rows.Add(Note("None"));
                if (selectedAttribute != null && !shown.Contains(selectedAttribute)) selectedAttribute = null;
                ShowSelected(rows, selectedAttribute != null ? rows.Children().ElementAtOrDefault(shown.IndexOf(selectedAttribute)) : null);
                section.Summary.text = shown.Count.ToString();
            }
            entityUpdates.Add(() =>
            {
                var cache = CacheOf();
                if ((cache?.Count ?? 0) != shown.Count || shown.Any(attribute => !cache.ContainsKey(attribute))) Build(cache);
                foreach (var update in updates) update();
                ShowFoot();
            });
            Build(CacheOf());
            ShowFoot();
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

        // A value cell a double-click takes to its field: the row is selected, and the field has the keyboard
        private static void GoesToField(Label cell, Action choose, FloatField field)
        {
            cell.AddToClassList("pf-debugger__cell--editable");
            cell.RegisterCallback<MouseDownEvent>(down =>
            {
                if (down.button != 0 || down.clickCount != 2) return;
                choose();
                field.Focus();
                down.StopPropagation();
            });
        }

        // What changed an attribute: each source of the impacts it took in the last second, with what it took a second,
        // then each effect on the entity that holds a modifier on it, as "Burning -8/s, Chilled"
        private string ChangedBy(IGameplayAbilitySystem gas, IAttribute attribute)
        {
            if (gas == null) return "—";
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
            if (parts == null || parts.Count <= 1) return "Double-click to set it";
            return string.Join("\n", parts.Select(part => $"{TheForge.TagText(part.Group)} {Signed(part.Value.CurrentValue)}"));
        }

        // The entity takes a value set in the foot row as it is: the change goes in directly, as a delta no worker sees,
        // and the next sample shows what the entity holds (Q27)
        private void SetAttribute(IGameplayAbilitySystem gas, IAttribute attribute, Func<IReadOnlyDictionary<IAttribute, CachedAttributeValue>> cacheOf,
            float? current, float? @base)
        {
            var cache = cacheOf();
            if (attribute == null || !Alive(gas) || cache == null || !cache.TryGetValue(attribute, out var cached)) return;
            var now = cached.ActiveValue;
            float toCurrent = current.HasValue ? current.Value - now.CurrentValue : 0f;
            float toBase = @base.HasValue ? @base.Value - now.BaseValue : 0f;
            if (toCurrent != 0f || toBase != 0f)
            {
                gas.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateUnworkable(gas, attribute, toCurrent, toBase));
                ShowAction(current.HasValue
                    ? $"Set {attribute.GetName()}'s current value to {Number(current.Value)}"
                    : $"Set {attribute.GetName()}'s base value to {Number(@base.Value)}");
            }
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects
        // ═══════════════════════════════════════════════════════════════════════════

        // The effects on the entity: each one's time left, its stacks and where it came from, as "Player · Fireball". The
        // foot row's Remove takes the selected one off, and an effect picked there lands from the entity itself (Q27).
        // The GAS Debugger printed an impact's values as "current/base", with no label (ED-4)
        private VisualElement EffectsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Effects", "Effects");
            section.Content.Add(TableHead(("Effect", "pf-debugger__col-grow"), ("Left", "pf-debugger__col-64"),
                ("Stacks", "pf-debugger__col-48"), ("From", "pf-debugger__col-grow11")));
            var rows = new VisualElement { name = "EffectRows" };
            section.Content.Add(rows);

            var foot = Foot("EffectFoot");
            var remove = ActionButton(foot, "RemoveEffect", "Remove", "Take the selected effect off, where its source applied it",
                () => RemoveEffect(gas, selectedEffect));
            var newEffect = new ObjectField { name = "NewEffect", objectType = typeof(GameplayEffect), allowSceneObjects = false, tooltip = "An effect to apply" };
            newEffect.AddToClassList("pf-debugger__pick");
            foot.Add(newEffect);
            var apply = ActionButton(foot, "ApplyEffect", "Apply", "Apply it to the entity, from the entity",
                () => ApplyEffect(gas, newEffect.value as GameplayEffect));
            newEffect.RegisterValueChangedCallback(_ => apply.SetEnabled(newEffect.value != null));
            apply.SetEnabled(false);
            section.Content.Add(foot);

            var shown = new List<AbstractEffectContainer>();
            var updates = new List<Action>();
            IReadOnlyList<AbstractEffectContainer> ShelfOf() => gas.GetEffectSystem()?.Shelf;
            void ShowRemove()
            {
                var name = selectedEffect?.Spec?.Base != null ? selectedEffect.Spec.Base.GetName() : null;
                remove.SetEnabled(name != null);
                remove.tooltip = name != null ? $"Take {name} off, where its source applied it" : "Select an effect to take it off";
            }
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
        private void ApplyEffect(IGameplayAbilitySystem gas, GameplayEffect effect)
        {
            if (effect == null || !Alive(gas)) return;
            bool landed = gas.ApplyGameplayEffect(gas.GenerateEffectSpec(IEffectOrigin.GenerateSourceDerivation(gas), effect));
            ShowAction(landed ? $"Applied {effect.GetName()}" : $"{effect.GetName()} was refused");
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
            ShowAction($"Removed {effect.GetName()}");
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities
        // ═══════════════════════════════════════════════════════════════════════════

        // The entity's abilities: each one's level, its state, as "Stage 2/3", and the time its cooldown has left. The
        // foot row activates or interrupts the selected one, and grants one picked there at the level given (Q27). The
        // GAS Debugger showed the cooldown effect's raw magnitude, before its scaler (ED-4)
        private VisualElement AbilitiesSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Abilities", "Abilities");
            section.Content.Add(TableHead(("Ability", "pf-debugger__col-grow"), ("Lvl", "pf-debugger__col-40"),
                ("State", "pf-debugger__col-grow13"), ("Cooldown", "pf-debugger__col-72")));
            var rows = new VisualElement { name = "AbilityRows" };
            section.Content.Add(rows);

            var foot = Foot("AbilityFoot");
            var selected = FootLine(foot, "AbilityActions");
            var activate = ActionButton(selected, "ActivateAbility", "Activate", "Set the selected ability off, as the entity would",
                () => ActivateAbility(gas, selectedAbility));
            var interrupt = ActionButton(selected, "InterruptAbility", "Interrupt", "Stop the selected ability's run",
                () => InterruptAbility(gas, selectedAbility));
            var grant = FootLine(foot, "AbilityGrant");
            var newAbility = new ObjectField { name = "NewAbility", objectType = typeof(Ability), allowSceneObjects = false, tooltip = "An ability to grant" };
            newAbility.AddToClassList("pf-debugger__pick");
            grant.Add(newAbility);
            FootLabel(grant, "Level");
            var level = new IntegerField { name = "NewAbilityLevel", value = 1, tooltip = "The level to grant it at" };
            level.AddToClassList("pf-debugger__level");
            grant.Add(level);
            var grantButton = ActionButton(grant, "GrantAbility", "Grant", "Grant it to the entity at this level",
                () => GrantAbility(gas, newAbility.value as Ability, level.value));
            newAbility.RegisterValueChangedCallback(_ => grantButton.SetEnabled(newAbility.value != null));
            grantButton.SetEnabled(false);
            section.Content.Add(foot);

            var shown = new List<AbilitySpecContainer>();
            var updates = new List<Action>();
            List<AbilitySpecContainer> ContainersOf() => gas.GetAbilitySystem()?.GetAbilityContainers() ?? new List<AbilitySpecContainer>();
            void ShowActions()
            {
                var name = selectedAbility?.Spec?.Base != null ? selectedAbility.Spec.Base.GetName() : null;
                activate.SetEnabled(name != null);
                interrupt.SetEnabled(name != null && selectedAbility.IsActive);
                activate.tooltip = name != null ? $"Set {name} off, as the entity would" : "Select an ability to set it off";
                interrupt.tooltip = name != null ? $"Stop {name}'s run" : "Select a running ability to stop it";
            }
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

        private void GrantAbility(IGameplayAbilitySystem gas, Ability ability, int level)
        {
            var abilities = gas?.GetAbilitySystem();
            if (ability == null || abilities == null || !Alive(gas)) return;
            bool granted = abilities.GiveAbility(ability, Math.Max(1, level), out _);
            ShowAction(granted ? $"Granted {ability.GetName()}" : $"{ability.GetName()} wasn't granted");
            SampleNow();
        }

        private void ActivateAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            var abilities = gas?.GetAbilitySystem();
            if (container == null || abilities == null || !Alive(gas)) return;
            var name = container.Spec?.Base != null ? container.Spec.Base.GetName() : "The ability";
            bool activated = abilities.TryActivateAbility(abilities.CreateActivationRequest(container.Index));
            ShowAction(activated ? $"Activated {name}" : $"{name} didn't activate");
            SampleNow();
        }

        private void InterruptAbility(IGameplayAbilitySystem gas, AbilitySpecContainer container)
        {
            var abilities = gas?.GetAbilitySystem();
            if (container == null || abilities == null || !Alive(gas) || !container.IsActive) return;
            abilities.Inject(container.Index, InterruptSequenceInjection.Instance);
            ShowAction($"Interrupted {(container.Spec?.Base != null ? container.Spec.Base.GetName() : "the ability")}");
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags
        // ═══════════════════════════════════════════════════════════════════════════

        // The tags the entity has, each with its count, its weight, and what grants it. The foot row's Remove takes back
        // the selected tag where it's loose, and a tag typed or picked from the project's there is granted as a loose tag
        // (FEAT-16, Q27). A tag added straight to the entity's cache went at its next compile. The Tag Debugger listed
        // tags in a window of its own
        private VisualElement TagsSection(IGameplayAbilitySystem gas)
        {
            var section = Section("Tags", "Tags");
            section.Content.Add(TableHead(("Tag", "pf-debugger__col-grow"), ("Count", "pf-debugger__col-56"), ("From", "pf-debugger__col-grow")));
            var rows = new VisualElement { name = "TagRows" };
            section.Content.Add(rows);

            var foot = Foot("TagFoot");
            var remove = ActionButton(foot, "RemoveTag", "Remove", "Take back the selected loose tag", () => TakeTag(gas, selectedTag));
            // A tag field: its name, typed, or one of the project's from its menu
            var field = new VisualElement { name = "TagField" };
            field.AddToClassList("pf-debugger__tag-field");
            var newTag = new TextField { name = "NewTag", tooltip = "A tag's name, such as Status.Stunned" };
            newTag.AddToClassList("pf-debugger__tag-input");
            newTag.textEdition.placeholder = "None";
            field.Add(newTag);
            var pick = new Button { name = "PickTag", tooltip = "Pick one of the project's tags" };
            pick.AddToClassList("pf-debugger__tag-pick");
            pick.Add(new ForgeGlyph(ForgeGlyphShape.ArrowDown));
            pick.clicked += () => PickTag(newTag, field);
            field.Add(pick);
            foot.Add(field);
            var add = ActionButton(foot, "AddTag", "Add Tag", "Grant the entity this tag until it's removed", () => GrantTag(gas, newTag));
            newTag.RegisterValueChangedCallback(change => add.SetEnabled(!string.IsNullOrWhiteSpace(change.newValue)));
            add.SetEnabled(false);
            // Enter grants it, as Add Tag does
            newTag.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;
                GrantTag(gas, newTag);
                key.StopPropagation();
            }, TrickleDown.TrickleDown);
            section.Content.Add(foot);

            var shown = new List<Tag>();
            var looseShown = new List<Tag>();
            var updates = new List<Action>();
            void ShowRemove()
            {
                bool loose = selectedTag is { } tag && looseShown.Contains(tag);
                remove.SetEnabled(loose);
                remove.tooltip = loose
                    ? $"Take back {TheForge.TagText(selectedTag.Value)}, a loose tag"
                    : "Select a loose tag to take it back; what else grants a tag takes it back itself";
            }
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
            field.value = "";
            ShowAction($"Added {name}");
            SampleNow();
        }

        private void TakeTag(IGameplayAbilitySystem gas, Tag? tag)
        {
            if (tag is not { } taken || !Alive(gas)) return;
            if (gas.RemoveLooseTag(taken)) ShowAction($"Removed {taken.Name}");
            SampleNow();
        }

        // The project's tags, each under its root: the one picked goes into the field, for Add Tag to grant
        private static void PickTag(TextField field, VisualElement under)
        {
            var menu = new GenericMenu();
            foreach (var record in ForgeTagRegistry.GetAllTagRecords().OrderBy(record => record.Tag.Name, NaturalOrder))
            {
                var tag = record.Tag;
                var root = tag.Name.Split('.')[0];
                menu.AddItem(new GUIContent($"{root}/{tag.Name}"), field.value == tag.Name, () => field.value = tag.Name);
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
