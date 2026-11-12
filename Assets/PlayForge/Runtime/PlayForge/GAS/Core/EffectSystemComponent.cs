using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The effect shelf — apply / tick / re-apply / remove for gameplay effects, shared by
    /// BOTH <see cref="RuntimeGameplayAbilitySystem"/> and the MonoBehaviour
    /// <see cref="GameplayAbilitySystem"/> (the twin-drift class of bugs ends here; the
    /// GAS classes keep only thin delegating wrappers).
    ///
    /// <para>Lifecycle under the two-kind impact model (IMPACT_MODEL_REFACTOR.md §3.2):</para>
    /// <code>
    /// Apply (durational):  shelve container
    ///                      → RegisterModifier per Continuous entry
    ///                      → app workers
    ///                      → commit OnApply entries; TickOnApplication? then Periodic ones once
    /// Tick:                commit Periodic entries per execute tick
    /// Remove:              container.OnRemove() → UnregisterModifier per Continuous entry.
    ///                      Commits are permanent — there is no rollback path.
    /// Instant:             commit OnApply entries — no shelf, no removal phase.
    /// </code>
    /// </summary>
    public class EffectSystemComponent
    {
        private readonly IGameplayAbilitySystem Self;

        private readonly List<AbstractEffectContainer> EffectShelf = new();
        private readonly List<AbstractEffectContainer> FinishedEffects = new();
        private bool needsCleaning;

        // What FinishedEffects holds, to look up without walking it. Marking a container finished, and the tick's and
        // the requirement pass's skip of a finished one, each searched the list, so ending n effects cost n² (GAS-P5)
        private readonly HashSet<AbstractEffectContainer> finishedSet = new();

        // A cleaning pass's containers, which leave the shelf and the index together (GAS-P5)
        private readonly HashSet<AbstractEffectContainer> leaving = new();
        private readonly Predicate<AbstractEffectContainer> isLeaving;

        // The shelf's containers by asset tag, in shelf order, for re-application and removal to find their own. They
        // walked the whole shelf, comparing every container's tag (GAS-P3)
        private readonly Dictionary<Tag, List<AbstractEffectContainer>> byTag = new();

        // An application leaves the shelf's removal and ongoing requirements to be checked once, at the next tick. Each
        // application checked every container and rebuilt all tags, twice over for an instant effect, so it cost as
        // much as the entity carried (GAS-P3)
        private bool requirementsDirty;

        // Effects part way through applying here, and those already reported for asking to apply again (GAS-28)
        private readonly HashSet<GameplayEffect> applying = new();
        private readonly HashSet<GameplayEffect> reportedCycles = new();

        /// <summary>Live containers — read-only view (tag compilation, debuggers).</summary>
        public IReadOnlyList<AbstractEffectContainer> Shelf => EffectShelf;

        public EffectSystemComponent(IGameplayAbilitySystem self)
        {
            Self = self;
            isLeaving = leaving.Contains;
        }

        private AttributeSystemComponent AttributeSystem => Self.GetAttributeSystem();

        // ═══════════════════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Per-frame entry point — called from the owning GAS's update.</summary>
        public void Tick()
        {
            // Before the shelf ticks, so a container a requirement ends is skipped, as it was when applying checked. A
            // tag that came or went asks too, whatever granted it: a loose tag's, an ability's or an item's ended no
            // effect until another effect came or went (GAS-56). With nothing on the shelf, there's nothing to ask
            var tags = Self.GetTagCache();
            bool tagsChanged = tags != null && tags.TagsChangedSinceChecked;
            if (tagsChanged && EffectShelf.Count == 0)
            {
                tags.MarkTagsChecked();
                tagsChanged = false;
            }
            if (requirementsDirty || tagsChanged) HandleGameplayEffects();

            TickEffectShelf();

            if (needsCleaning) ClearFinishedEffects();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // APPLY
        // ═══════════════════════════════════════════════════════════════════════════

        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            if (spec is null) return false;

            // Under its own marker in the Profiler, a contained effect's within its container's (FEAT-18)
            using var sample = ForgeProfiler.ApplyEffect.Auto();

            if (!ForgeHelper.ValidateEffectApplicationRequirements(spec, Self.GetAffiliation()))
            {
                return false;
            }

            // An immunity on the target refuses an effect it names, and a miss on its chance to apply refuses it too (FEAT-8)
            if (IsImmuneTo(spec.Base)) return false;
            if (!RollsToApply(spec)) return false;

            // Asked for again before its own application is over, an effect contains itself, through its contained
            // effects or what applying it sets off. It recursed until the stack overflowed, which nothing catches (GAS-28)
            if (!applying.Add(spec.Base))
            {
                if (reportedCycles.Add(spec.Base))
                {
                    ForgeLog.Warning($"{spec.Base.GetName()} was applied again inside its own application, so that " +
                                     "application is refused. An effect that contains itself, directly or through " +
                                     "other effects, would recurse until the stack overflowed.", spec.Base);
                }
                return false;
            }

            // Numbered as it lands, by the entity it lands on (FEAT-13)
            spec.ApplicationId = ++applications;

            try
            {
                switch (spec.Base.DurationSpecification.DurationPolicy)
                {
                    case EEffectDurationPolicy.Instant:
                        ApplyInstantGameplayEffect(spec);
                        break;
                    case EEffectDurationPolicy.Infinite:
                    case EEffectDurationPolicy.Durational:
                        ApplyDurationalGameplayEffect(spec);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                requirementsDirty = true;

                // A cleanse takes off the effects it names as it lands, before its own contained effects do (FEAT-8)
                RemoveEffectsNamedBy(spec);

                // Recorded where the callback fires, for analysis workers. Nothing recorded it (GAS-28)
                Self.GetFrameSummary()?.RecordEffectApplied(spec);
                Self.GetCallbacks()?.EffectApplied(spec);
                ForgeCounters.EffectsApplied.Value++;

                foreach (var containedEffect in spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnApply))
                {
                    ApplyMadeFrom(spec, containedEffect);
                }
                ApplyAddedPackets(spec, EApplyTickRemove.OnApply);
            }
            finally
            {
                // A throw part way mustn't leave the effect refused here for good
                applying.Remove(spec.Base);
            }

            return true;
        }

        // The number of the entity's last effect application (FEAT-13)
        private int applications;

        // Whether the effect lands by its chance to apply. A chance of 1 or more, every effect's unless it's set, lands
        // and draws nothing, and one of 0 or less never lands and draws nothing either, so an effect without a chance
        // leaves the numbers others draw as they were. An effect always landed (FEAT-8)
        private static bool RollsToApply(GameplayEffectSpec spec)
        {
            var chance = spec.Base.ImpactSpecification?.ChanceToApply;
            if (chance == null && spec.Changes == null) return true;
            float odds = chance?.Evaluate(spec) ?? 1f;
            // As its modifiers change it (TW-7)
            if (spec.Changes != null) odds = spec.Changes.Chance(odds);
            // Pseudo-random and lucky as its roll says, its streak the caster's for this effect (TW-8)
            return ForgeChance.Roll(odds, NumberContext.Of(spec), spec.Base.ImpactSpecification?.ChanceRoll, spec.Base);
        }

        // The shelf's immunities, the containers whose effects block others, so an application checks them alone and costs
        // the same however much the entity carries (GAS-P3)
        private readonly List<AbstractEffectContainer> immunities = new();

        // Whether an effect on the shelf, while it holds, blocks the effect: an immunity. Nothing made an entity immune
        // to a kind of effect but a requirement on each effect of the kind (FEAT-8)
        private bool IsImmuneTo(GameplayEffect effect)
        {
            if (immunities.Count == 0) return false;
            foreach (var container in immunities)
            {
                if (!container.Ongoing || finishedSet.Contains(container)) continue;
                if (GameplayEffectTags.Names(container.Spec.Base.Tags.BlocksEffectsTagged, effect)) return true;
            }
            return false;
        }

        // Takes off each other effect on the shelf that the landing effect names: a cleanse. Each leaves at the next tick,
        // as a removed effect does. A cleanse took a removal requirement on each effect it meant to clear (FEAT-8)
        private void RemoveEffectsNamedBy(GameplayEffectSpec spec)
        {
            var named = spec.Base.Tags.RemovesEffectsTagged;
            if (named == null || named.Count == 0) return;
            foreach (var container in EffectShelf)
            {
                // Never the cleanse itself, where it's tagged as a kind it removes
                if (container.Spec.Base == spec.Base || finishedSet.Contains(container)) continue;
                if (GameplayEffectTags.Names(named, container.Spec.Base)) MarkFinished(container);
            }
        }

        // An effect another lands, from its origin and with the magnitudes its caller set: a contained effect, or an
        // overflow's (FEAT-2)
        private bool ApplyMadeFrom(GameplayEffectSpec from, GameplayEffect effect)
        {
            // The rules whose added effect the one it's made from is change it no more (TW-7)
            var spec = EffectModifiers.Make(Self, from.GetOrigin(), effect, from, null);
            spec?.TakeSetByCaller(from.SetByCallerMagnitudes);
            return ApplyGameplayEffect(spec);
        }

        private void ApplyInstantGameplayEffect(GameplayEffectSpec spec)
        {
            var tagCache = Self.GetTagCache();

            tagCache.AddTags(spec.Base.Tags.GrantedTags);
            if (spec.Changes != null) tagCache.AddTags(spec.Changes.GrantedTags);
            spec.RunWorkerApplication(new EffectWorkerContext(Self, spec, Self.GetFrameSummary(), Self.GetActionQueue()));
            
            // Commit every OnApply entry, per attribute. Undefined/null attribute targets
            // early-out inside the attribute system — an instant effect can be pure
            // worker/tag utility.
            //
            // Every commit goes to the removal workers. They saw only the last, and an empty one, on no attribute,
            // when nothing committed (GAS-28)
            List<ImpactData> impacts = null;
            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.OnApply))
            {
                var impact = AttributeSystem.CommitImpact(
                    attribute,
                    new SourcedModifiedAttributeValue(spec, 0f, 0f),
                    spec.GetImpactersFor(attribute, EImpactDelivery.OnApply));

                // An attribute the target doesn't have commits nothing, and comes back empty
                if (impact.Attribute is not null) (impacts ??= new List<ImpactData>(1)).Add(impact);
            }

            spec.RunWorkerRemoval(new EffectWorkerContext(Self, spec, Self.GetFrameSummary(), Self.GetActionQueue(), 1,
                impacts?[impacts.Count - 1], impacts));
            tagCache.RemoveTags(spec.Base.Tags.GrantedTags);
            if (spec.Changes != null) tagCache.RemoveTags(spec.Changes.GrantedTags);

            // An instant effect lands and is over, so its cue is a burst (FEAT-1)
            Cue(spec, ECueMoment.Applied);
        }

        private void ApplyDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            bool ongoing = ForgeHelper.ValidateEffectOngoingRequirements(spec);
            if (TryHandleExistingDurationalGameplayEffect(spec, ongoing))
            {
                // Taken in by the effect on already, whose lasting cues go on (FEAT-1)
                Cue(spec, ECueMoment.Applied);
                return;
            }

            var container = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
            Shelve(container);
            LendAbilities(container);

            // Continuous entries register as modifiers — transient, derived, reversed by
            // unregistration on removal. Registered BEFORE application workers so workers
            // observe the post-apply attribute state.
            RegisterContinuousEntries(spec);

            container.Spec.RunWorkerApplication(new EffectWorkerContext(Self, container.Spec, Self.GetFrameSummary(), Self.GetActionQueue()));

            // Its burst, and its lasting cues begin (FEAT-1)
            Cue(container.Spec, ECueMoment.Applied);
            Cue(container.Spec, ECueMoment.Begin);

            if (!ongoing) return;
            // An OnApply entry is one commit, made as the effect lands. Tick On Application commits them with its tick, and
            // was the only durational path that did, so an impact authored to land as a ticking effect does was dropped,
            // and only its ticks dealt (GAS-71)
            if (spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(container);
            else CommitOnApply(container.Spec);
        }

        // Commits each OnApply entry, per attribute, as the effect is applied
        private void CommitOnApply(GameplayEffectSpec spec)
        {
            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.OnApply))
            {
                AttributeSystem.CommitImpact(
                    attribute,
                    new SourcedModifiedAttributeValue(spec, 0f, 0f),
                    spec.GetImpactersFor(attribute, EImpactDelivery.OnApply));
            }
        }

        /// <summary>
        /// Puts a container on the shelf and in the index, and counts its tags now, as a rebuild would. Counted as it is
        /// shelved, nothing counts them twice: any rebuild after this counts the shelf afresh (GAS-P3). A restore counts
        /// them once, in its own compile as it ends (FEAT-9).
        /// </summary>
        private void Shelve(AbstractEffectContainer container, bool countTags = true)
        {
            EffectShelf.Add(container);

            var tag = container.Spec.Base.Tags.AssetTag;
            if (!byTag.TryGetValue(tag, out var same)) byTag[tag] = same = new List<AbstractEffectContainer>();
            same.Add(container);

            var blocks = container.Spec.Base.Tags.BlocksEffectsTagged;
            if (blocks != null && blocks.Count > 0) immunities.Add(container);

            if (countTags) GrantedTagCompiler.AddEffect(Self.GetTagCache(), container.Spec);
        }

        /// <summary>
        /// Takes a cleaning pass's containers off the shelf and out of the index, in one walk of each. Taken off one at a
        /// time, each a linear remove, ending n containers cost n² (GAS-P5).
        /// </summary>
        private void Unshelve(AbstractEffectContainer[] finished)
        {
            for (int i = 0; i < finished.Length; i++) leaving.Add(finished[i]);

            EffectShelf.RemoveAll(isLeaving);

            // Every list, not only the one under each container's tag, which may have changed since it was shelved
            foreach (var list in byTag.Values) list.RemoveAll(isLeaving);
            if (immunities.Count > 0) immunities.RemoveAll(isLeaving);

            leaving.Clear();
        }

        /// <summary>
        /// Reads every effect on the shelf that <paramref name="origin"/> applied again, re-registering its modifiers, as
        /// something a scaler on it reads has changed: an item's stack count (FEAT-14).
        /// </summary>
        public void RefreshEffectsFrom(IEffectOrigin origin)
        {
            for (int i = 0; i < EffectShelf.Count; i++)
            {
                var container = EffectShelf[i];
                if (container.Spec.Origin == origin && !finishedSet.Contains(container)) RegisterContinuousEntries(container.Spec);
            }
        }

        /// <summary>
        /// Reads every effect on the shelf whose origin's level is kept under <paramref name="levelKey"/> again, as that
        /// level has changed: an item's, an ability's or the entity's own. A lasting effect's magnitude was read as its
        /// modifier registered, so an item that levelled up kept its first level's bonus (GAS-58).
        /// </summary>
        public void RefreshEffectsLevelledBy(Tag levelKey)
        {
            for (int i = 0; i < EffectShelf.Count; i++)
            {
                var container = EffectShelf[i];
                if (finishedSet.Contains(container) || container.Spec.Origin?.GetAssetTag() != levelKey) continue;
                RegisterContinuousEntries(container.Spec);
            }
        }

        // The abilities each container lent its target, with the slot each took, to take back as it comes off. Only a
        // container whose effect grants abilities is here (FEAT-8)
        private readonly Dictionary<AbstractEffectContainer, List<(Ability Ability, int Slot)>> lent = new();

        // The On Hits each container gave its target, to take back as it comes off. Kept apart, as a replacement by another
        // effect under its tag gives the container the other's spec first, and a load clears the shelf quietly (TW-6)
        private readonly Dictionary<AbstractEffectContainer, List<OnHitEntry>> onHitsLent = new();

        // The modifiers each container gave its target, kept apart as its On Hits are (TW-7)
        private readonly Dictionary<AbstractEffectContainer, List<EffectModifierEntry>> modifiersLent = new();

        // Gives the target each ability the container's effect grants, at the level of what applied it, and notes the slot
        // each took. One the target can't take, as one it knows already, isn't lent, so it isn't taken back either, as an
        // item's isn't (GAS-48). An effect couldn't lend an ability (FEAT-8). A restore lends them quietly, and compiles the
        // tags once as it ends (FEAT-9)
        private void LendAbilities(AbstractEffectContainer container, bool quietly = false)
        {
            // Its On Hits, while it lasts, as what it lends (TW-6)
            var onHit = container.Spec.Base.OnHit;
            if (onHit is { Count: > 0 })
            {
                var given = new List<OnHitEntry>(onHit);
                OnHits.Provide(given, Self);
                onHitsLent[container] = given;
            }

            // Its modifiers, while it lasts (TW-7)
            var modifiers = container.Spec.Base.Modifiers;
            if (modifiers is { Count: > 0 })
            {
                var given = new List<EffectModifierEntry>(modifiers);
                EffectModifiers.Provide(given, Self);
                modifiersLent[container] = given;
            }

            var abilities = container.Spec.Base.GrantedAbilities;
            if (abilities == null || abilities.Count == 0) return;
            var abilitySystem = Self.GetAbilitySystem();
            if (abilitySystem == null) return;

            int level = container.Spec.GetOrigin().GetLevel().CurrentValue;
            List<(Ability, int)> slots = null;
            foreach (var ability in abilities)
            {
                if (ability == null) continue;
                int slot;
                bool given = quietly ? abilitySystem.GiveQuietly(ability, level, out slot) : abilitySystem.GiveAbility(ability, level, out slot);
                if (!given) continue;
                (slots ??= new List<(Ability, int)>(abilities.Count)).Add((ability, slot));
            }
            if (slots != null) lent[container] = slots;
        }

        // Takes back what the container lent, interrupting an ability that's running, as an item's is (FEAT-8)
        private void TakeBackAbilities(AbstractEffectContainer container)
        {
            if (onHitsLent.Count > 0 && onHitsLent.Remove(container, out var onHit)) OnHits.Remove(onHit, Self);
            if (modifiersLent.Count > 0 && modifiersLent.Remove(container, out var modifiers)) EffectModifiers.Remove(modifiers, Self);

            if (lent.Count == 0 || !lent.Remove(container, out var slots)) return;
            var abilitySystem = Self.GetAbilitySystem();
            if (abilitySystem == null) return;
            foreach (var (ability, slot) in slots) abilitySystem.RemoveAbilityAt(slot, ability);
        }

        /// <summary>
        /// Register (or refresh) the spec's Continuous entries on every attribute they
        /// target. Re-registration replaces the spec's previous impacter set — this is the
        /// stack-refresh path (stack-aware magnitudes re-evaluate against the container).
        /// </summary>
        private void RegisterContinuousEntries(GameplayEffectSpec spec)
        {
            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.Continuous))
            {
                AttributeSystem.RegisterModifier(
                    attribute,
                    new SourcedModifiedAttributeValue(spec, 0f, 0f),
                    spec.GetImpactersFor(attribute, EImpactDelivery.Continuous));
            }
        }

        private static int StackCount(AbstractEffectContainer container)
            => (container as AbstractStackingEffectContainer)?.Stacks ?? 0;

        private void ApplyTickOnApplication(AbstractEffectContainer container)
        {
            // An OnApply entry is one commit, made as the effect is applied. A durational effect whose only tick is
            // Tick On Application derives OnApply for Add and Flat Bonus, and only the Instant path committed it, so
            // its change never landed (GAS-50)
            CommitOnApply(container.Spec);

            // Evaluate the tick count ONCE (a scaler-driven count must not be re-rolled
            // per loop iteration), run tick workers with that count, then execute.
            int executeTicks = container.Spec.Base.DurationSpecification.GetExecuteTicks(container.Spec, container.InstantExecuteTicks);
            if (executeTicks <= 0) return;

            container.Spec.RunWorkerTick(new EffectWorkerContext(Self, container.Spec, Self.GetFrameSummary(), Self.GetActionQueue(), executeTicks));

            for (int _ = 0; _ < executeTicks; _++)
            {
                ApplyDurationGameplayEffectAsInstant(container);
            }
        }

        /// <summary>One execute tick: commit Periodic entries + apply contained OnTick effects.</summary>
        private void ApplyDurationGameplayEffectAsInstant(AbstractEffectContainer container)
        {
            var spec = container.Spec;

            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.Periodic))
            {
                AttributeSystem.CommitImpact(
                    attribute,
                    new SourcedModifiedAttributeValue(spec, 0f, 0f),
                    spec.GetImpactersFor(attribute, EImpactDelivery.Periodic));
            }

            foreach (var containedEffect in spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnTick))
            {
                ApplyMadeFrom(spec, containedEffect);
            }
            ApplyAddedPackets(spec, EApplyTickRemove.OnTick);
        }

        // The contained effects its modifiers add for a moment, each made with the rule that added it kept from changing
        // it again; those as it ends come through its container's removal, as its own do (TW-7)
        private void ApplyAddedPackets(GameplayEffectSpec spec, EApplyTickRemove when)
        {
            if (spec.Changes == null || spec.Changes.Packets.Count == 0) return;
            foreach (var packet in spec.Changes.Packets)
            {
                if (packet.When != when || packet.Effect == null) continue;
                var made = EffectModifiers.Make(Self, spec.GetOrigin(), packet.Effect, spec, packet.AddedBy);
                made?.TakeSetByCaller(spec.SetByCallerMagnitudes);
                ApplyGameplayEffect(made);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RE-APPLICATION
        // ═══════════════════════════════════════════════════════════════════════════

        private bool TryHandleExistingDurationalGameplayEffect(GameplayEffectSpec spec, bool ongoing)
        {
            if (!byTag.TryGetValue(spec.Base.Tags.AssetTag, out var existing) || existing.Count == 0) return false;

            // By index over the count found here: a tick on application can apply effects, which shelve containers
            int count = existing.Count;

            bool flag = true;
            AbstractStackingEffectContainer overflowing = null;
            StackBoundsConfig overflowBound = null;
            switch (spec.Base.DurationSpecification.ReApplicationPolicy)
            {
                case EEffectReApplicationPolicy.DoNothing:
                    break;
                case EEffectReApplicationPolicy.ReplaceOldContainer:
                {
                    var newContainer = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
                    for (int i = 0; i < count; i++)
                    {
                        var oldContainer = existing[i];
                        bool otherEffect = oldContainer.Spec.Base != spec.Base;

                        // Replacement ends the old spec's modifier contribution, the
                        // container adopts the new spec + duration values, and the new
                        // spec's Continuous entries register fresh. (Old commits are
                        // permanent and stay — commits never reverse.)
                        oldContainer.ReverseImpact();
                        oldContainer.ReplaceValuesWith(newContainer);
                        RegisterContinuousEntries(spec);

                        // Another effect under the same tag grants its own tags, which only a rebuild counts, and lends
                        // its own abilities in place of the old one's (FEAT-8)
                        if (otherEffect)
                        {
                            Self.CompileGrantedTags();
                            TakeBackAbilities(oldContainer);
                            LendAbilities(oldContainer);
                        }
                    }
                    break;
                }
                case EEffectReApplicationPolicy.AppendNewContainer:
                    flag = false;
                    break;
                case EEffectReApplicationPolicy.StackExistingContainers:
                {
                    var containers = existing.Select(c => c as AbstractStackingEffectContainer).ToArray();
                    var container = spec.Base.DurationSpecification.FindStackingContainer(spec, ongoing, containers);
                    if (container is null)
                    {
                        flag = false;
                        break;
                    }

                    int stacks = spec.Base.DurationSpecification.GetStackAmount(container, out overflowBound);
                    if (overflowBound != null) overflowing = container;
                    container.Stack(stacks);

                    // Stacks feed magnitude through the container-aware impacter path —
                    // refresh the registration so the attribute recomputes with the new
                    // stack count. (Periodic entries pick it up on their next tick.)
                    RegisterContinuousEntries(container.Spec);

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Interaction only applies when the re-application was absorbed by existing
            // containers. On the append path (flag == false) the caller proceeds with a
            // fresh application — running Refresh/Extend + ApplyTickOnApplication here as
            // well would double-tick the effect.
            // An application that overflows with Deny Refresh leaves the effect's duration as it was (FEAT-8)
            if (flag && !(overflowBound != null && overflowBound.DenyRefreshOnOverflow))
            {
                switch (spec.Base.DurationSpecification.ReApplicationInteraction)
                {
                    case EEffectInteractionPolicy.DoNothing:
                        break;
                    case EEffectInteractionPolicy.RefreshContainerDuration:
                        for (int i = 0; i < count; i++)
                        {
                            var c = existing[i];
                            c.Refresh();
                            if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                        }
                        break;
                    case EEffectInteractionPolicy.ExtendContainerDuration:
                        for (int i = 0; i < count; i++)
                        {
                            var c = existing[i];
                            int stacksBefore = StackCount(c);
                            c.Extend(spec.Base.DurationSpecification.GetTotalDuration(spec));

                            // Independent durations extend by adding a stack, which refreshes as any other does (GAS-34)
                            if (StackCount(c) != stacksBefore) RegisterContinuousEntries(c.Spec);
                            if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // No tags to rebuild: the containers that absorbed it grant what they did. A rebuild here walked every
                // source of every tag (GAS-P3)
            }

            if (overflowing != null) Overflow(overflowing, overflowBound, spec);

            return flag;
        }

        // A stack its bound held back at its max: the bound's overflow effects land from the application's origin, and
        // the effect's stacks come off, leaving at the next tick as a removed effect does, when the bound says so. A
        // stack past the max did nothing, so a bleed couldn't hemorrhage at its fourth stack (FEAT-8, Decision 30)
        private void Overflow(AbstractStackingEffectContainer container, StackBoundsConfig bound, GameplayEffectSpec spec)
        {
            bool clear = bound.ClearStacksOnOverflow || (spec.Changes?.ClearStacksOnOverflow ?? false);
            if (clear && !finishedSet.Contains(container)) MarkFinished(container);
            if (bound.OverflowEffects != null)
                for (int i = 0; i < bound.OverflowEffects.Count; i++)
                    if (bound.OverflowEffects[i] != null) ApplyMadeFrom(spec, bound.OverflowEffects[i]);

            // And those its modifiers add, each kept from the rule that added it (TW-7)
            if (spec.Changes == null) return;
            foreach (var overflow in spec.Changes.Overflows)
            {
                var made = EffectModifiers.Make(Self, spec.GetOrigin(), overflow.Effect, spec, overflow.AddedBy);
                made?.TakeSetByCaller(spec.SetByCallerMagnitudes);
                ApplyGameplayEffect(made);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // REMOVE
        // ═══════════════════════════════════════════════════════════════════════════

        public bool RemoveGameplayEffect(GameplayEffect effect)
        {
            return RemoveGameplayEffect(effect.Tags.AssetTag);
        }

        /// <summary>
        /// Ends each effect on the shelf that one of <paramref name="tags"/> names, by its asset tag, a context tag or a tag it
        /// grants, or one under them, as a cleanse ends them; how many it ended (TW-4).
        /// </summary>
        public int RemoveEffectsTagged(List<Tag> tags)
        {
            if (tags == null || tags.Count == 0) return 0;
            int ended = 0;
            foreach (var container in Shelf)
            {
                if (finishedSet.Contains(container) || !GameplayEffectTags.Names(tags, container.Spec.Base)) continue;
                MarkFinished(container);
                ended++;
            }
            return ended;
        }

        public bool RemoveGameplayEffect(Tag identifier)
        {
            if (!byTag.TryGetValue(identifier, out var same) || same.Count == 0) return false;

            // Marking finished leaves the shelf, and so the index, as it is until the next clean
            foreach (var container in same) MarkFinished(container);
            return true;
        }

        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin)
        {
            if (!byTag.TryGetValue(effect.Tags.AssetTag, out var same)) return false;

            bool any = false;
            foreach (var container in same)
            {
                if (container.Spec.Origin != origin) continue;

                MarkFinished(container);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Queue a container for removal exactly once. Every path that finishes a container
        /// (expiry, removal requirements, explicit removal) funnels through here — a duplicate
        /// entry in <see cref="FinishedEffects"/> would run OnRemove twice.
        /// </summary>
        private void MarkFinished(AbstractEffectContainer container)
        {
            if (!finishedSet.Add(container)) return;

            FinishedEffects.Add(container);
            needsCleaning = true;
        }

        private void HandleGameplayEffects()
        {
            requirementsDirty = false;

            // Requirements read the applied tags, so compile first: a container that just landed on
            // the shelf must have its granted tags count toward everyone else's ongoing state.
            Self.CompileGrantedTags();

            // The pass reads the tags as they are now (GAS-56)
            Self.GetTagCache()?.MarkTagsChecked();

            foreach (var container in EffectShelf)
            {
                if (finishedSet.Contains(container)) continue;

                if (ForgeHelper.ValidateEffectRemovalRequirements(container.Spec))
                {
                    MarkFinished(container);
                }
                else
                {
                    container.Ongoing = ForgeHelper.ValidateEffectOngoingRequirements(container.Spec);
                }
            }

            // Once: checking requirements changes no tag, and a second compile here rebuilt the same ones (GAS-P3)
        }

        private void TickEffectShelf()
        {
            // Index-based over the frame-start count: executing a tick can apply contained
            // effects, which land NEW containers on the shelf mid-loop (a foreach would
            // throw). Containers are never removed from the shelf during this pass
            // (removal is deferred through FinishedEffects), so indices stay stable;
            // late additions start ticking next frame.
            int shelfCount = EffectShelf.Count;
            for (int i = 0; i < shelfCount; i++)
            {
                var container = EffectShelf[i];
                if (finishedSet.Contains(container)) continue;

                var durationPolicy = container.Spec.Base.DurationSpecification.DurationPolicy;
                if (durationPolicy == EEffectDurationPolicy.Instant) continue;

                float deltaTime = container.Spec.Base.DurationSpecification.GetDeltaTime(container.Spec);

                if (container.Spec.Base.DurationSpecification.EnablePeriodicTicks)
                {
                    container.TickPeriodic(deltaTime, out int executeTicks);

                    if (executeTicks > 0)
                    {
                        executeTicks = container.Spec.Base.DurationSpecification.GetExecuteTicks(
                            container.Spec, executeTicks
                        );

                        // Re-check at the moment a tick would execute. Tags can change without any
                        // effect being applied or removed (abilities, items, other systems), and only
                        // apply/remove re-evaluates the stored Ongoing state.
                        container.Ongoing = ForgeHelper.ValidateEffectOngoingRequirements(container.Spec);
                    }

                    var effectContext = new EffectWorkerContext(
                        Self, container.Spec,
                        Self.GetFrameSummary(), Self.GetActionQueue(),
                        executeTicks);
                    container.Spec.RunWorkerTick(effectContext);

                    if (executeTicks > 0 && container.Ongoing)
                    {
                        Self.GetCallbacks()?.EffectTick(container.Spec, executeTicks);
                        Cue(container.Spec, ECueMoment.Ticked, executeTicks);

                        for (int _ = 0; _ < executeTicks; _++) ApplyDurationGameplayEffectAsInstant(container);
                    }
                }

                if (durationPolicy == EEffectDurationPolicy.Infinite) continue;

                int stacksBefore = StackCount(container);
                container.UpdateTimeRemaining(deltaTime);

                if (container.DurationRemaining > 0)
                {
                    // A stack ran out and others are left. A per-stack magnitude reads the count only when its
                    // attribute recomputes, so this refreshes as gaining a stack does. The old count held until
                    // something else recomputed the attribute, and that change took the credit (GAS-34)
                    if (StackCount(container) != stacksBefore) RegisterContinuousEntries(container.Spec);
                    continue;
                }

                MarkFinished(container);
            }
        }

        private void ClearFinishedEffects()
        {
            // Snapshot-and-clear: OnRemove / removal workers / callbacks can re-enter
            // HandleGameplayEffects and mark MORE containers finished mid-loop. Those land
            // in the (now empty) live list, re-flag needsCleaning, and are processed next
            // update — mutating the list we're iterating would throw.
            var finished = FinishedEffects.ToArray();
            FinishedEffects.Clear();
            finishedSet.Clear();
            needsCleaning = false;

            // Off the shelf and out of the index FIRST, the whole pass together, so re-entrant
            // HandleGameplayEffects passes and removals can't re-mark a container that is mid-removal (GAS-P5)
            Unshelve(finished);

            foreach (var container in finished)
            {
                container.OnRemove();
                TakeBackAbilities(container);

                var effectContext = new EffectWorkerContext(
                    Self, container.Spec,
                    Self.GetFrameSummary(), Self.GetActionQueue());
                container.Spec.RunWorkerRemoval(effectContext);

                // Recorded where the callback fires, as an application is (GAS-28)
                Self.GetFrameSummary()?.RecordEffectRemoved(container.Spec.Base);
                Self.GetCallbacks()?.EffectRemoved(container.Spec.Base);
                ForgeCounters.EffectsRemoved.Value++;

                // Its burst, and its lasting cues end (FEAT-1)
                Cue(container.Spec, ECueMoment.Removed);
                Cue(container.Spec, ECueMoment.End);
            }

            HandleGameplayEffects();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CUES (FEAT-1)
        // ═══════════════════════════════════════════════════════════════════════════

        private enum ECueMoment { Applied, Ticked, Removed, Begin, End }

        // Plays the effect's cues for the moment. A cue that throws is reported, and the effect goes on: what it shows
        // mustn't stop what it does
        private void Cue(GameplayEffectSpec spec, ECueMoment moment, int ticks = 0)
        {
            var cues = spec?.Base?.Cues;
            if (cues == null || cues.Count == 0) return;
            var context = new CueContext(Self, spec, ticks);
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue == null) continue;
                try
                {
                    switch (moment)
                    {
                        case ECueMoment.Applied: cue.OnApplied(context); break;
                        case ECueMoment.Ticked: cue.OnTicked(context); break;
                        case ECueMoment.Removed: cue.OnRemoved(context); break;
                        case ECueMoment.Begin: cue.Begin(context); break;
                        case ECueMoment.End: cue.End(context); break;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RESTORE — a load's quiet paths (FEAT-9)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Takes every effect off the shelf at once with nothing told and nothing run: no removal effects, workers or
        /// callbacks. Nor does it take back the abilities effects lent, which the ability system lets go of with the rest; the
        /// On Hits they gave it does, which the ability system keeps (TW-6). A restore puts back what the save had.
        /// </summary>
        internal void ClearForRestore()
        {
            foreach (var container in EffectShelf)
            {
                container.ContainerIsActive = false;
                foreach (var attribute in container.Spec.GetImpactedAttributes(EImpactDelivery.Continuous))
                    AttributeSystem.UnregisterQuietly(attribute, container.Spec);

                // Its lasting cues end, as its effect leaves; no burst plays (FEAT-1)
                Cue(container.Spec, ECueMoment.End);
            }

            EffectShelf.Clear();
            FinishedEffects.Clear();
            finishedSet.Clear();
            needsCleaning = false;
            byTag.Clear();
            immunities.Clear();
            lent.Clear();

            // The On Hits they gave are the ability system's, which keeps them through its own clear (TW-6)
            foreach (var onHit in onHitsLent.Values) OnHits.Remove(onHit, Self);
            onHitsLent.Clear();
            foreach (var modifiers in modifiersLent.Values) EffectModifiers.Remove(modifiers, Self);
            modifiersLent.Clear();
            requirementsDirty = true;
        }

        /// <summary>
        /// Puts an effect back on the shelf as a save left it: its stacks and clock, its modifiers, and the abilities it
        /// lends. Nothing of its application runs again, its workers, its commits on application or its contained
        /// effects, and nothing is told. The next tick asks its requirements, once the restore has compiled the tags.
        /// </summary>
        internal void Restore(GameplayEffectSpec spec, EffectSave save)
        {
            if (spec == null || save == null) return;
            if (spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Instant) return;

            spec.ApplicationId = ++applications;
            var container = spec.Base.DurationSpecification.GenerateContainer(spec, save.Ongoing);
            container.RestoreClock(save.TotalDuration, save.PeriodDuration, save.Stacks,
                save.Remaining ?? (IReadOnlyList<double>)Array.Empty<double>(), save.UntilTick ?? (IReadOnlyList<double>)Array.Empty<double>());

            Shelve(container, countTags: false);
            LendAbilities(container, quietly: true);
            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.Continuous))
                AttributeSystem.RegisterQuietly(attribute, spec, spec.GetImpactersFor(attribute, EImpactDelivery.Continuous));

            // Its lasting cues begin again, with no burst (FEAT-1)
            Cue(spec, ECueMoment.Begin);

            requirementsDirty = true;
        }

        // The containers a move takes, gathered first, as moving one changes the shelf
        private readonly List<AbstractEffectContainer> moving = new();

        /// <summary>
        /// Moves each of an effect's containers on this shelf onto another entity's, with its stacks and its time left,
        /// as a possession hands a body's effects over (FEAT-23). A move is neither a removal nor an application: nothing
        /// of either runs, its workers, its commits or its contained effects, and neither is told. Its lasting modifiers
        /// leave this entity and hold on the other, its lasting cues end here and begin there, the abilities it lends go
        /// with it, and both entities' tags are compiled again. Each keeps what applied it. Taking an effect off and
        /// applying it to the other started its time again and ran its application twice. Returns how many moved.
        /// </summary>
        public int MoveTo(GameplayEffect effect, IGameplayAbilitySystem onto)
        {
            if (effect == null || onto == null || ReferenceEquals(onto, Self)) return 0;
            var target = onto.GetEffectSystem();
            if (target == null || ReferenceEquals(target, this)) return 0;

            moving.Clear();
            foreach (var container in EffectShelf)
                if (container.Spec.Base == effect && !IsLeaving(container)) moving.Add(container);
            if (moving.Count == 0) return 0;

            foreach (var container in moving)
            {
                // Its clock, as a save takes it
                var save = new EffectSave
                {
                    Ongoing = container.Ongoing,
                    Stacks = (container as AbstractStackingEffectContainer)?.Stacks ?? 0,
                    TotalDuration = container.TotalDuration,
                    PeriodDuration = container.PeriodDuration,
                };
                container.CaptureClock(save.Remaining, save.UntilTick);

                // Off this entity, with nothing told
                container.ContainerIsActive = false;
                foreach (var attribute in container.Spec.GetImpactedAttributes(EImpactDelivery.Continuous))
                    AttributeSystem.UnregisterQuietly(attribute, container.Spec);
                Cue(container.Spec, ECueMoment.End);
                TakeBackAbilities(container);

                // Onto the other, as a restore puts one back, by what applied it
                var spec = onto.GenerateEffectSpec(container.Spec.Origin, effect);
                if (spec == null) continue;
                spec.TakeSetByCaller(container.Spec.SetByCallerMagnitudes);
                target.Restore(spec, save);
            }

            int moved = moving.Count;
            Unshelve(moving.ToArray());
            moving.Clear();
            requirementsDirty = true;
            Self.CompileGrantedTags();
            onto.CompileGrantedTags();
            return moved;
        }

        /// <summary>Whether an effect on the shelf lent the ability in the slot, which comes back with the effect (FEAT-9).</summary>
        internal bool Lends(int slot)
        {
            foreach (var slots in lent.Values)
            {
                foreach (var (_, taken) in slots)
                    if (taken == slot) return true;
            }
            return false;
        }

        /// <summary>Whether the container leaves the shelf at the next tick, as a save asks (FEAT-9).</summary>
        internal bool IsLeaving(AbstractEffectContainer container) => finishedSet.Contains(container);

        // ═══════════════════════════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════════════════════════

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
        {
            container = byTag.TryGetValue(effect.Tags.AssetTag, out var same) && same.Count > 0 ? same[0] : null;
            return container != null;
        }

        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
        {
            containers = byTag.TryGetValue(effect.Tags.AssetTag, out var same) ? same.ToArray() : Array.Empty<AbstractEffectContainer>();
            return containers.Length > 0;
        }

        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                if (container.Spec.Base.GetAssetTag() != lookForTag
                    && container.GetGrantedTags().All(specTag => specTag != lookForTag)) continue;
                if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                    return new EffectDurationRemaining(float.MaxValue, float.MaxValue, true);

                if (!(container.TotalDuration > longestDuration)) continue;
                longestDuration = container.TotalDuration;
                longestRemaining = container.DurationRemaining;
            }

            return new EffectDurationRemaining(longestDuration, longestRemaining, longestDuration >= 0f);
        }

        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                if (!lookForTags.Contains(container.Spec.Base.GetAssetTag())
                    && !container.GetGrantedTags().Any(lookForTags.Contains)) continue;
                if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                {
                    return new EffectDurationRemaining(float.MaxValue, float.MaxValue, true);
                }

                if (!(container.TotalDuration > longestDuration)) continue;
                longestDuration = container.TotalDuration;
                longestRemaining = container.DurationRemaining;
            }

            return new EffectDurationRemaining(longestDuration, longestRemaining, longestDuration >= 0f);
        }
    }
}
