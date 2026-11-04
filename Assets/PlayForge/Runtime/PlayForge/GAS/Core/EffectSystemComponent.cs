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
    ///                      → TickOnApplication? commit OnApply entries, then Periodic ones once
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
        }

        private AttributeSystemComponent AttributeSystem => Self.GetAttributeSystem();

        // ═══════════════════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Per-frame entry point — called from the owning GAS's update.</summary>
        public void Tick()
        {
            // Before the shelf ticks, so a container a requirement ends is skipped, as it was when applying checked
            if (requirementsDirty) HandleGameplayEffects();

            TickEffectShelf();

            if (needsCleaning) ClearFinishedEffects();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // APPLY
        // ═══════════════════════════════════════════════════════════════════════════

        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            if (spec is null) return false;

            if (!ForgeHelper.ValidateEffectApplicationRequirements(spec, Self.GetAffiliation()))
            {
                return false;
            }

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

                // Recorded where the callback fires, for analysis workers. Nothing recorded it (GAS-28)
                Self.GetFrameSummary()?.RecordEffectApplied(spec);
                Self.GetCallbacks()?.EffectApplied(spec);

                foreach (var containedEffect in spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnApply))
                {
                    ApplyGameplayEffect(spec.GetOrigin(), containedEffect);
                }
            }
            finally
            {
                // A throw part way mustn't leave the effect refused here for good
                applying.Remove(spec.Base);
            }

            return true;
        }

        private bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect effect)
        {
            var spec = Self.GenerateEffectSpec(origin, effect);
            return ApplyGameplayEffect(spec);
        }

        private void ApplyInstantGameplayEffect(GameplayEffectSpec spec)
        {
            var tagCache = Self.GetTagCache();

            tagCache.AddTags(spec.Base.Tags.GrantedTags);
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
        }

        private void ApplyDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            bool ongoing = ForgeHelper.ValidateEffectOngoingRequirements(spec);
            if (TryHandleExistingDurationalGameplayEffect(spec, ongoing)) return;

            var container = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
            Shelve(container);

            // Continuous entries register as modifiers — transient, derived, reversed by
            // unregistration on removal. Registered BEFORE application workers so workers
            // observe the post-apply attribute state.
            RegisterContinuousEntries(spec);

            container.Spec.RunWorkerApplication(new EffectWorkerContext(Self, container.Spec, Self.GetFrameSummary(), Self.GetActionQueue()));

            if (ongoing && spec.Base.DurationSpecification.TickOnApplication)
            {
                ApplyTickOnApplication(container);
            }
        }

        /// <summary>
        /// Puts a container on the shelf and in the index, and counts its tags now, as a rebuild would. Counted as it is
        /// shelved, nothing counts them twice: any rebuild after this counts the shelf afresh (GAS-P3).
        /// </summary>
        private void Shelve(AbstractEffectContainer container)
        {
            EffectShelf.Add(container);

            var tag = container.Spec.Base.Tags.AssetTag;
            if (!byTag.TryGetValue(tag, out var same)) byTag[tag] = same = new List<AbstractEffectContainer>();
            same.Add(container);

            GrantedTagCompiler.AddEffect(Self.GetTagCache(), container.Spec);
        }

        private void Unshelve(AbstractEffectContainer container)
        {
            EffectShelf.Remove(container);

            // Under the tag it was shelved with, unless the effect's tag has changed since
            if (byTag.TryGetValue(container.Spec.Base.Tags.AssetTag, out var same) && same.Remove(container)) return;
            foreach (var list in byTag.Values)
            {
                if (list.Remove(container)) return;
            }
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
            foreach (var attribute in container.Spec.GetImpactedAttributes(EImpactDelivery.OnApply))
            {
                AttributeSystem.CommitImpact(
                    attribute,
                    new SourcedModifiedAttributeValue(container.Spec, 0f, 0f),
                    container.Spec.GetImpactersFor(attribute, EImpactDelivery.OnApply));
            }

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
                ApplyGameplayEffect(spec.Origin, containedEffect);
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

                        // Another effect under the same tag grants its own tags, which only a rebuild counts
                        if (otherEffect) Self.CompileGrantedTags();
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

                    int stacks = spec.Base.DurationSpecification.GetStackAmount(container);
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
            if (flag)
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

            return flag;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // REMOVE
        // ═══════════════════════════════════════════════════════════════════════════

        public bool RemoveGameplayEffect(GameplayEffect effect)
        {
            return RemoveGameplayEffect(effect.Tags.AssetTag);
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
            if (FinishedEffects.Contains(container)) return;

            FinishedEffects.Add(container);
            needsCleaning = true;
        }

        private void HandleGameplayEffects()
        {
            requirementsDirty = false;

            // Requirements read the applied tags, so compile first: a container that just landed on
            // the shelf must have its granted tags count toward everyone else's ongoing state.
            Self.CompileGrantedTags();

            foreach (var container in EffectShelf)
            {
                if (FinishedEffects.Contains(container)) continue;

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
                if (FinishedEffects.Contains(container)) continue;

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
            needsCleaning = false;

            foreach (var container in finished)
            {
                // Off the shelf FIRST so re-entrant HandleGameplayEffects passes can't
                // re-mark a container that is mid-removal.
                Unshelve(container);
                container.OnRemove();

                var effectContext = new EffectWorkerContext(
                    Self, container.Spec,
                    Self.GetFrameSummary(), Self.GetActionQueue());
                container.Spec.RunWorkerRemoval(effectContext);

                // Recorded where the callback fires, as an application is (GAS-28)
                Self.GetFrameSummary()?.RecordEffectRemoved(container.Spec.Base);
                Self.GetCallbacks()?.EffectRemoved(container.Spec.Base);
            }

            HandleGameplayEffects();
        }

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
