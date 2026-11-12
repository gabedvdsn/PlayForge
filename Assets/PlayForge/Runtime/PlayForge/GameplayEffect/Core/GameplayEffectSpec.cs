using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class GameplayEffectSpec : ICarrierApplication
    {
        public GameplayEffect Base;

        public IEffectOrigin Origin;

        /// <summary>
        /// The application's number on its target, counted from 1 as the target's effects land, or 0 until it lands. An
        /// application had no ID, so nothing could tell two of one effect apart, as a looping cue or a replay needs
        /// (FEAT-13).
        /// </summary>
        public int ApplicationId { get; internal set; }
        public ISource Source;
        public ITarget Target;

        public TrackedImpact TrackedImpact;

        /// <summary>
        /// The last hit's run through a pipeline: each stage's before and after, and a note, as a critical hit (TW-2). Null
        /// until one runs; one is kept for the spec and filled again by each run.
        /// </summary>
        public ValueBreakdown Breakdown { get; private set; }

        internal ValueBreakdown TakeBreakdown() => Breakdown ??= new ValueBreakdown();

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

        // Each worker's state for this application, made the first time the worker asks (FEAT-29)
        private Dictionary<AbstractEffectWorker, object> workerStates;

        /// <summary>
        /// A worker's state for this application of the effect, made the first time the worker asks for it and kept
        /// while the application lasts, as a task's state is kept on its activation's data. A worker is one object on
        /// its effect's asset, shared by every application, so what it counts for one of them is kept here: the
        /// target's health as the effect began, how many times it has ticked. A worker that answers numbers reads it
        /// back as it's asked (FEAT-29).
        /// </summary>
        public T GetWorkerState<T>(AbstractEffectWorker worker) where T : class, new()
        {
            workerStates ??= new Dictionary<AbstractEffectWorker, object>();
            if (workerStates.TryGetValue(worker, out var state)) return (T)state;

            var created = new T();
            workerStates[worker] = created;
            return created;
        }

        // Magnitudes the caller set under a tag, which a Set By Caller scaler reads (FEAT-2)
        private Dictionary<Tag, float> setByCaller;
        private static readonly Dictionary<Tag, float> NoMagnitudes = new();

        /// <summary>The magnitudes the caller set, by tag, which a Set By Caller scaler reads (FEAT-2).</summary>
        public IReadOnlyDictionary<Tag, float> SetByCallerMagnitudes => setByCaller ?? NoMagnitudes;

        /// <summary>
        /// Sets a magnitude under a tag, as Unreal's SetByCaller does, for a Set By Caller scaler to read: code that knows
        /// the amount as it applies the effect passes it in. Set it before the spec is applied (FEAT-2).
        /// </summary>
        public void SetByCaller(Tag key, float magnitude) => (setByCaller ??= new Dictionary<Tag, float>())[key] = magnitude;

        /// <summary>The magnitude the caller set under the tag (FEAT-2).</summary>
        public bool TryGetSetByCaller(Tag key, out float magnitude)
        {
            magnitude = 0f;
            return setByCaller != null && setByCaller.TryGetValue(key, out magnitude);
        }

        /// <summary>
        /// Takes each magnitude another set, as an effect is made from it: its container's, or an activation's data. One
        /// set on this spec already gives way (FEAT-2).
        /// </summary>
        public void TakeSetByCaller(IReadOnlyDictionary<Tag, float> magnitudes)
        {
            if (magnitudes == null || magnitudes.Count == 0) return;
            foreach (var pair in magnitudes) SetByCaller(pair.Key, pair.Value);
        }

        // Cached impacter list — built lazily on first GetImpacters() call so we don't
        // re-allocate every time the cached attribute aggregator recomputes. Rebuilds
        // automatically would require change-tracking on Base.ImpactSpecification; for
        // now the spec is considered immutable for the lifetime of the effect.
        private IReadOnlyList<IAttributeImpacter> cachedImpacters;

        /// <summary>
        /// What modifiers changed of this application (TW-7): its contained effects, its numbers, its overflow, its time,
        /// its stacks, its chance and its tags. Null where none did.
        /// </summary>
        public EffectChanges Changes { get; internal set; }

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
            if (Changes == null || Changes.GrantedTags.Count == 0) return Base.Tags.GrantedTags;
            return GrantedWithChanges();
        }

        // Its own granted tags, then those its modifiers add (TW-7)
        private IEnumerable<Tag> GrantedWithChanges()
        {
            if (Base.Tags.GrantedTags != null)
                foreach (var tag in Base.Tags.GrantedTags) yield return tag;
            foreach (var tag in Changes.GrantedTags) yield return tag;
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
                    entry.ResolveGroup(defaultGroup),
                    entry.AddBehaviour,
                    entry.Pipeline,
                    // The modifiers' stages for its attribute: a lasting entry's on its magnitude, others' as it commits (TW-7)
                    Changes?.StagesFor(entry.AttributeTarget),
                    entry.Delivery == EImpactDelivery.Continuous);
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

                // With this one's magnitudes, as a contained effect takes them (FEAT-2); the rules whose added effect
                // this is change it no more (TW-7)
                var made = EffectModifiers.Make(Target, Origin, contained, this, null);
                made?.TakeSetByCaller(setByCaller);
                yield return made;
            }

            // Those its modifiers add, each made with the rule that added it kept from changing it again (TW-7)
            if (Changes == null) yield break;
            foreach (var packet in Changes.Packets)
            {
                if (packet.When != phase || packet.Effect == null) continue;
                var made = EffectModifiers.Make(Target, Origin, packet.Effect, this, packet.AddedBy);
                made?.TakeSetByCaller(setByCaller);
                yield return made;
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
