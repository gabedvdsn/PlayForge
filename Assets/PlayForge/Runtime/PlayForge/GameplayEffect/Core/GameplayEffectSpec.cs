using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class GameplayEffectSpec : ICarrierApplication
    {
        public GameplayEffect Base;

        public IEffectOrigin Origin;
        public ISource Source;
        public ITarget Target;

        public TrackedImpact TrackedImpact;

        /// <summary>
        /// The shelf container currently holding this spec (null for instant effects and
        /// for specs not yet shelved). Set by <see cref="AbstractEffectContainer"/> on
        /// construction and on <see cref="AbstractEffectContainer.ReplaceValuesWith"/>.
        /// Lets the impacter pipeline evaluate container-dependent state (stack count)
        /// without the attribute system knowing about containers.
        /// </summary>
        public AbstractEffectContainer RuntimeContainer { get; private set; }

        public void SetRuntimeContainer(AbstractEffectContainer container)
        {
            RuntimeContainer = container;
        }

        private Dictionary<IScaler, AttributeValue?> SourceCapturedAttributes;

        // Cached impacter list — built lazily on first GetImpacters() call so we don't
        // re-allocate every time the cached attribute aggregator recomputes. Rebuilds
        // automatically would require change-tracking on Base.ImpactSpecification; for
        // now the spec is considered immutable for the lifetime of the effect.
        private IReadOnlyList<IAttributeImpacter> cachedImpacters;

        public GameplayEffectSpec(GameplayEffect GameplayEffect, IEffectOrigin origin, ITarget target)
        {
            Base = GameplayEffect;
            Origin = origin;
            
            Source = Origin.GetSource();
            Target = target;

            TrackedImpact = new TrackedImpact();
            SourceCapturedAttributes = new Dictionary<IScaler, AttributeValue?>();
        }
        
        // SourcedImpact / AttributeImpact removed (refactor step 9). The spec's
        // GetImpacters() pipeline (ScalerImpacter wrapping ImpactSpecification) replaces
        // both — the cached attribute aggregator handles op + target-slot dispatch in
        // one place (CachedAttributeValue.Recompute / ApplyOneShotImpacters), eliminating
        // the parallel switch-on-op-and-target machinery that used to live here.

        public IHasReadableDefinition GetReadableDefinition()
        {
            return Base;
        }
        /// <summary>
        /// True iff this exact spec instance is currently held by a container on the
        /// source's effect shelf. Diagnostic only — the recompute aggregator no longer
        /// filters by this. Walks <see cref="IGameplayAbilitySystem.TryGetEffectContainers"/>
        /// (plural) so AppendNewContainer scenarios with multiple co-existing specs of
        /// the same effect each report alive correctly.
        /// </summary>
        public bool DerivationAlive()
        {
            if (Source?.ToGAS() is not { } gas) return false;
            if (!gas.TryGetEffectContainers(Base, out var containers) || containers == null) return false;
            foreach (var c in containers)
                if (c?.Spec == this) return true;
            return false;
        }
        public IAttribute GetAttribute()
        {
            return Base.ImpactSpecification.AttributeTarget;
        }
        public IEffectOrigin GetOrigin()
        {
            return Origin;
        }
        public ITarget GetTarget()
        {
            return Target;
        }
        public List<Tag> GetImpactTypes()
        {
            return Base.ImpactSpecification.ImpactTypes;
        }
        public Tag GetRetentionGroup()
        {
            return Base.Definition.RetentionGroup;
        }
        public void TrackImpact(ImpactData impactData)
        {
            TrackedImpact.Add(impactData.RealImpact);
        }
        public TrackedImpact GetTrackedImpact()
        {
            return TrackedImpact;
        }
        public ImpactDerivationContext GetContextTags()
        {
            return new ImpactDerivationContext(Origin.GetContextTags(), Base.Tags.ContextTags);
        }
        public void RunWorkerApplication(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectApplication(ctx));
        }
        public void RunWorkerTick(EffectWorkerContext ctx)
        {
            foreach (var worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectTick(ctx));
        }
        public void RunWorkerRemoval(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectRemoval(ctx));
        }
        public void RunWorkerImpact(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectImpact(ctx));
        }
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return SourceCapturedAttributes;
        }
        public bool RetainImpact()
        {
            return Base.ImpactSpecification.TargetImpact != EAttributeTarget.Current || Base.DurationSpecification.DurationPolicy != EEffectDurationPolicy.Instant;
        }
        public IEnumerable<Tag> GetGrantedTags()
        {
            return Base.Tags.GrantedTags;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Impacter pipeline (additive — see refactor plan §2.3 / step 2)
        //
        // The current asset shape exposes one (magnitude, scaler, op, target) quad on
        // GameplayEffectImpact, so one ScalerImpacter per spec is the right cardinality
        // for now. Multi-impacter authoring per asset is deferred (plan §5A → option B).
        // ════════════════════════════════════════════════════════════════════════

        public IReadOnlyList<IAttributeImpacter> GetImpacters()
        {
            if (cachedImpacters != null) return cachedImpacters;
            var impact = Base.ImpactSpecification;
            cachedImpacters = new IAttributeImpacter[]
            {
                new ScalerImpacter(
                    impact.MagnitudeOperation,
                    impact.ImpactOperation,
                    impact.TargetImpact,
                    sortOrder: 0,
                    multiplyBehaviour: impact.MultiplyBehaviour)
            };
            return cachedImpacters;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ICarrierApplication — application-side surface
        //
        // All the underlying logic already exists on Base / DurationSpecification /
        // ImpactSpecification; this layer just exposes it through the carrier-typed
        // contract so ITarget can apply any ICarrierApplication (not just spec).
        // ════════════════════════════════════════════════════════════════════════

        public EEffectDurationPolicy GetDurationPolicy()
            => Base.DurationSpecification.DurationPolicy;

        public AbstractEffectContainer GenerateContainer(bool ongoing)
            => Base.DurationSpecification.GenerateContainer(this, ongoing);

        public IEnumerable<ICarrierApplication> GetContainedCarriers(EApplyTickRemove phase)
        {
            // Each contained GameplayEffect generates a fresh spec (which is itself an
            // ICarrierApplication) using THIS spec's origin and target — preserving the
            // attribution chain that Source.ApplyGameplayEffect / Source.GenerateEffectSpec
            // would otherwise stamp in.
            foreach (var contained in Base.ImpactSpecification.GetContainedEffects(phase))
            {
                if (contained == null) continue;
                yield return Target.GenerateEffectSpec(Origin, contained);
            }
        }

        /// <summary>
        /// Mode A iff durational/infinite AND not tick-delivered. A durational effect with
        /// no tick (no TickOnApplication, no periodic ticks) has no way to commit impact
        /// other than being retained as a sustained modifier — so that's exactly what it
        /// is. Instant effects and anything tick-delivered are Mode B (discrete impact).
        /// </summary>
        public bool IsSustainedModifier()
        {
            var dur = Base?.DurationSpecification;
            if (dur == null) return false;
            if (dur.DurationPolicy == EEffectDurationPolicy.Instant) return false;

            bool tickDelivered = dur.TickOnApplication || dur.EnablePeriodicTicks;
            return !tickDelivered;
        }

        public bool ValidateApplication(ITarget target)
        {
            // Mirrors the gating that RuntimeGameplayAbilitySystem.ApplyGameplayEffect
            // does today; centralising it here lets the new ITarget.ApplyCarrier path
            // use one validation contract regardless of the carrier's concrete kind.
            return ForgeHelper.ValidateEffectApplicationRequirements(this, target.GetAffiliation());
        }
    }
}
