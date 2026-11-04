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
        // Impacter pipeline — entry-driven (IMPACT_MODEL_REFACTOR.md §3.1)
        //
        // One ScalerImpacter per authored AttributeImpactEntry, built lazily and cached
        // for the spec's lifetime. Entries carry their own attribute target, delivery
        // and retention group, so the attribute system can ask for exactly the
        // impacters that apply to (attribute, delivery) without re-deriving intent.
        // ════════════════════════════════════════════════════════════════════════

        private List<(AttributeImpactEntry entry, IAttributeImpacter impacter)> entryImpacters;

        // Per delivery, the attributes impacted and each one's impacters, in entry order, built with the entries: the
        // spec is immutable for its life. Each lookup built a new list or set, on every tick (GAS-P4)
        private static readonly int Deliveries = Enum.GetValues(typeof(EImpactDelivery)).Length;
        private static readonly IReadOnlyList<IAttributeImpacter> NoImpacters = Array.Empty<IAttributeImpacter>();
        private List<IAttribute>[] attributesByDelivery;
        private List<(IAttribute attribute, List<IAttributeImpacter> impacters)>[] impactersByDelivery;

        private void BuildEntryImpacters()
        {
            if (entryImpacters != null) return;
            entryImpacters = new List<(AttributeImpactEntry, IAttributeImpacter)>();
            attributesByDelivery = new List<IAttribute>[Deliveries];
            impactersByDelivery = new List<(IAttribute, List<IAttributeImpacter>)>[Deliveries];
            for (int d = 0; d < Deliveries; d++)
            {
                attributesByDelivery[d] = new List<IAttribute>();
                impactersByDelivery[d] = new List<(IAttribute, List<IAttributeImpacter>)>();
            }

            var impact = Base.ImpactSpecification;
            if (impact?.Entries == null) return;

            var defaultGroup = Base.Definition.RetentionGroup;
            foreach (var entry in impact.Entries)
            {
                if (entry == null) continue;
                var impacter = new ScalerImpacter(
                    entry.Magnitude,
                    entry.Operation,
                    entry.TargetSlot,
                    entry.SortOrder,
                    entry.MultiplyBehaviour,
                    entry.ResolveGroup(defaultGroup));
                entryImpacters.Add((entry, impacter));
                IndexImpacter(entry.Delivery, entry.AttributeTarget, impacter);
            }
        }

        private void IndexImpacter(EImpactDelivery delivery, IAttribute attribute, IAttributeImpacter impacter)
        {
            var buckets = impactersByDelivery[(int)delivery];
            for (int i = 0; i < buckets.Count; i++)
            {
                if (!AttributeMatches(buckets[i].attribute, attribute)) continue;
                buckets[i].impacters.Add(impacter);
                return;
            }

            buckets.Add((attribute, new List<IAttributeImpacter> { impacter }));
            if (attribute != null) attributesByDelivery[(int)delivery].Add(attribute);
        }

        /// <summary>
        /// All impacters whose entry targets <paramref name="attribute"/> with the given delivery. The list is the
        /// spec's own, built once: read it, don't change it.
        /// </summary>
        public IReadOnlyList<IAttributeImpacter> GetImpactersFor(IAttribute attribute, EImpactDelivery delivery)
        {
            BuildEntryImpacters();
            var buckets = impactersByDelivery[(int)delivery];
            for (int i = 0; i < buckets.Count; i++)
            {
                if (AttributeMatches(buckets[i].attribute, attribute)) return buckets[i].impacters;
            }
            return NoImpacters;
        }

        /// <summary>
        /// Distinct attributes impacted at the given delivery, in entry order (null target entries are skipped). A
        /// <c>foreach</c> over the result allocates nothing.
        /// </summary>
        public ReadOnlyListView<IAttribute> GetImpactedAttributes(EImpactDelivery delivery)
        {
            BuildEntryImpacters();
            return new ReadOnlyListView<IAttribute>(attributesByDelivery[(int)delivery]);
        }

        public bool HasImpacters(EImpactDelivery delivery)
        {
            BuildEntryImpacters();
            return impactersByDelivery[(int)delivery].Count > 0;
        }

        private static bool AttributeMatches(IAttribute a, IAttribute b)
            => a == null ? b == null : b != null && ReferenceEquals(a, b);

        /// <summary>
        /// Legacy carrier surface: impacters for the PRIMARY attribute
        /// (<c>ImpactSpecification.AttributeTarget</c>) across all deliveries. Transitional —
        /// the attribute system consumes <see cref="GetImpactersFor"/> in the new model.
        /// </summary>
        public IReadOnlyList<IAttributeImpacter> GetImpacters()
        {
            if (cachedImpacters != null) return cachedImpacters;
            BuildEntryImpacters();

            var primary = Base.ImpactSpecification.AttributeTarget;
            var list = new List<IAttributeImpacter>();
            foreach (var (entry, impacter) in entryImpacters)
            {
                if (AttributeMatches(entry.AttributeTarget, primary)) list.Add(impacter);
            }
            cachedImpacters = list;
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

        // IsSustainedModifier removed — the Mode A/B inference is gone. Each impact entry
        // declares its own delivery (Continuous | OnApply | Periodic); delivery implies
        // the contribution kind. See AttributeImpactEntry / IMPACT_MODEL_REFACTOR.md §1.

        public bool ValidateApplication(ITarget target)
        {
            // Mirrors the gating that RuntimeGameplayAbilitySystem.ApplyGameplayEffect
            // does today; centralising it here lets the new ITarget.ApplyCarrier path
            // use one validation contract regardless of the carrier's concrete kind.
            return ForgeHelper.ValidateEffectApplicationRequirements(this, target.GetAffiliation());
        }
    }
}
