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
    ///                      → TickOnApplication? commit Periodic entries once
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

            HandleGameplayEffects();

            Self.GetCallbacks()?.EffectApplied(spec);

            foreach (var containedEffect in spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnApply))
            {
                ApplyGameplayEffect(spec.GetOrigin(), containedEffect);
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
            ImpactData impactData = default;
            foreach (var attribute in spec.GetImpactedAttributes(EImpactDelivery.OnApply))
            {
                impactData = AttributeSystem.CommitImpact(
                    attribute,
                    new SourcedModifiedAttributeValue(spec, 0f, 0f),
                    spec.GetImpactersFor(attribute, EImpactDelivery.OnApply));
            }

            spec.RunWorkerRemoval(new EffectWorkerContext(Self, spec, Self.GetFrameSummary(), Self.GetActionQueue(), 1, impactData));
            tagCache.RemoveTags(spec.Base.Tags.GrantedTags);

            HandleGameplayEffects();
        }

        private void ApplyDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            bool ongoing = ForgeHelper.ValidateEffectOngoingRequirements(spec);
            if (TryHandleExistingDurationalGameplayEffect(spec, ongoing)) return;

            var container = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
            EffectShelf.Add(container);

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

        private void ApplyTickOnApplication(AbstractEffectContainer container)
        {
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
            if (!TryGetEffectContainers(spec.Base, out var _containers)) return false;

            bool flag = true;
            switch (spec.Base.DurationSpecification.ReApplicationPolicy)
            {
                case EEffectReApplicationPolicy.DoNothing:
                    break;
                case EEffectReApplicationPolicy.ReplaceOldContainer:
                {
                    var newContainer = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
                    foreach (var oldContainer in _containers)
                    {
                        // Replacement ends the old spec's modifier contribution, the
                        // container adopts the new spec + duration values, and the new
                        // spec's Continuous entries register fresh. (Old commits are
                        // permanent and stay — commits never reverse.)
                        oldContainer.ReverseImpact();
                        oldContainer.ReplaceValuesWith(newContainer);
                        RegisterContinuousEntries(spec);
                    }
                    break;
                }
                case EEffectReApplicationPolicy.AppendNewContainer:
                    flag = false;
                    break;
                case EEffectReApplicationPolicy.StackExistingContainers:
                {
                    var containers = _containers.Select(c => c as AbstractStackingEffectContainer).ToArray();
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
                        foreach (var c in _containers)
                        {
                            c.Refresh();
                            if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                        }
                        break;
                    case EEffectInteractionPolicy.ExtendContainerDuration:
                        foreach (var c in _containers)
                        {
                            c.Extend(spec.Base.DurationSpecification.GetTotalDuration(spec));
                            if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Self.CompileGrantedTags();
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
            var toRemove = EffectShelf.Where(container => container.Spec.Base.Tags.AssetTag == identifier).ToArray();
            foreach (AbstractEffectContainer container in toRemove)
            {
                MarkFinished(container);
            }

            return toRemove.Length > 0;
        }

        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin)
        {
            var toRemove = EffectShelf.Where(container => container.Spec.Base.Tags.AssetTag == effect.Tags.AssetTag
                                                          && container.Spec.Origin == origin).ToArray();
            foreach (AbstractEffectContainer container in toRemove)
            {
                MarkFinished(container);
            }

            return toRemove.Length > 0;
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

            Self.CompileGrantedTags();
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

                container.UpdateTimeRemaining(deltaTime);

                if (container.DurationRemaining > 0) continue;

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
                EffectShelf.Remove(container);
                container.OnRemove();

                var effectContext = new EffectWorkerContext(
                    Self, container.Spec,
                    Self.GetFrameSummary(), Self.GetActionQueue());
                container.Spec.RunWorkerRemoval(effectContext);

                Self.GetCallbacks()?.EffectRemoved(container.Spec.Base);
            }

            HandleGameplayEffects();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════════════════════════

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
        {
            foreach (var _container in EffectShelf.Where(_container => _container.Spec.Base.Tags.AssetTag == effect.Tags.AssetTag))
            {
                container = _container;
                return true;
            }

            container = null;
            return false;
        }

        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
        {
            containers = EffectShelf.Where(c => c.Spec.Base.Tags.AssetTag == effect.Tags.AssetTag).ToArray();
            return containers.Any();
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
