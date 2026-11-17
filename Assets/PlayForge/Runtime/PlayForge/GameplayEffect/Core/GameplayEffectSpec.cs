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
        /// application had no ID, so nothing could tell two of one effect apart, as a looping cue or a replay needs.
        /// </summary>
        public int ApplicationId { get; internal set; }
        public ISource Source;
        public ITarget Target;

        public TrackedImpact TrackedImpact;

        /// <summary>
        /// The last hit's run through a pipeline: each stage's before and after, and a note, as a critical hit. Null
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

        // Each worker's state for this application, made the first time the worker asks
        private Dictionary<AbstractEffectWorker, object> workerStates;
        private ComponentStates componentStates;

        /// <summary>What the effect's components keep for this application of it.</summary>
        public ComponentStates ComponentStates => componentStates ??= new ComponentStates();

        /// <summary>
        /// A worker's state for this application of the effect, made the first time the worker asks for it and kept
        /// while the application lasts, as a task's state is kept on its activation's data. A worker is one object on
        /// its effect's asset, shared by every application, so what it counts for one of them is kept here: the
        /// target's health as the effect began, how many times it has ticked. A worker that answers numbers reads it
        /// back as it's asked.
        /// </summary>
        public T GetWorkerState<T>(AbstractEffectWorker worker) where T : class, new()
        {
            workerStates ??= new Dictionary<AbstractEffectWorker, object>();
            if (workerStates.TryGetValue(worker, out var state)) return (T)state;

            var created = new T();
            workerStates[worker] = created;
            return created;
        }

        // Magnitudes the caller set under a tag, which a Set By Caller scaler reads
        private Dictionary<Tag, float> setByCaller;
        private static readonly Dictionary<Tag, float> NoMagnitudes = new();

        /// <summary>The magnitudes the caller set, by tag, which a Set By Caller scaler reads.</summary>
        public IReadOnlyDictionary<Tag, float> SetByCallerMagnitudes => setByCaller ?? NoMagnitudes;

        /// <summary>
        /// Sets a magnitude under a tag, as Unreal's SetByCaller does, for a Set By Caller scaler to read: code that knows
        /// the amount as it applies the effect passes it in. Set it before the spec is applied.
        /// </summary>
        public void SetByCaller(Tag key, float magnitude) => (setByCaller ??= new Dictionary<Tag, float>())[key] = magnitude;

        /// <summary>The magnitude the caller set under the tag.</summary>
        public bool TryGetSetByCaller(Tag key, out float magnitude)
        {
            magnitude = 0f;
            return setByCaller != null && setByCaller.TryGetValue(key, out magnitude);
        }

        /// <summary>
        /// Takes each magnitude another set, as an effect is made from it: its container's, or an activation's data. One
        /// set on this spec already gives way.
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
        /// What modifiers changed of this application: its contained effects, its numbers, its overflow, its time,
        /// its stacks, its chance and its tags. Null where none did.
        /// </summary>
        public EffectChanges Changes { get; internal set; }

        /// <summary>Whether it landed by another's area, so it spreads no further.</summary>
        public bool FromArea { get; internal set; }

        /// <summary>
        /// How many spreads it is from where its effect first landed: 0 for a landing of its own, 1 for one a Spread
        /// component made from it, and on.
        /// </summary>
        public int SpreadHop { get; internal set; }

        /// <summary>
        /// The categories what applied it claims it as, for a purge, beside those its origin gives it: an aura claims the
        /// effects it holds on those near it Lent, as it takes them back as its holder goes or they leave.
        /// </summary>
        public EEffectCategory Claimed { get; set; }

        /// <summary>Whether a lasting impact of its fades with it, a barrier's, so its shelf refreshes it as it ticks.</summary>
        internal bool Fades { get; private set; }

        /// <summary>
        /// Whether it lands as a container of its own, whatever its effect does when it's applied again, as each spent
        /// charge's cooldown runs alone.
        /// </summary>
        internal bool OwnContainer { get; set; }

        /// <summary>What its lasting transfers hold on each side, to give back as it ends. Null where none holds any.</summary>
        internal List<LentTransfer> TransfersLent { get; set; }

        // Each Chance condition's roll for this hit, kept so a hit read twice reads the same
        private Dictionary<object, bool> rolls;

        internal bool TryGetRoll(object key, out bool rolled)
        {
            rolled = false;
            return rolls != null && rolls.TryGetValue(key, out rolled);
        }

        internal void KeepRoll(object key, bool rolled) => (rolls ??= new Dictionary<object, bool>())[key] = rolled;

        /// <summary>
        /// What its hits carry for their on-hit effects: its effect's own where it sets one, or else the ability's
        /// that made it, or 1. On Hit's effects scale their chance and duration by it, and 0 sets nothing off.
        /// </summary>
        public float ProcCoefficient =>
            Base.OwnProcCoefficient ? Base.ProcCoefficient.Evaluate(NumberContext.Of(this))
            : Origin?.GetReadableDefinition() is Ability ability ? ability.Definition.ProcCoefficient.Evaluate(NumberContext.Of(this)) : 1f;

        /// <summary>
        /// Its icon: its effect's own, or, under its effect's Inherit From Origin, its origin's texture under the effect's
        /// Origin Texture, the ability, item or entity that applied it, its own where the origin has none of it.
        /// </summary>
        public Texture2D GetIcon()
        {
            var definition = Base?.Definition;
            if (definition is { InheritFromOrigin: true } && Origin?.GetReadableDefinition() is IHasTextures origin
                && !ReferenceEquals(origin, Base)
                && ForgeHelper.TextureUnder(origin.GetTextureItems(), definition.OriginTexture) is { } inherited)
                return inherited;
            return Base?.GetDefaultIcon();
        }

        public GameplayEffectSpec(GameplayEffect GameplayEffect, IEffectOrigin origin, ITarget target)
        {
            Base = GameplayEffect;
            Origin = origin;
            
            Source = Origin.GetSource();
            Target = target;

            TrackedImpact = new TrackedImpact();
            SourceCapturedAttributes = new Dictionary<IScaler, AttributeValue?>();
        }
        
        // SourcedImpact / AttributeImpact removed. The spec's
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
        /// <summary>Where the application hit, where its applier said, as a projectile's: its cues play there.</summary>
        public Vector3? HitPoint { get; private set; }

        /// <summary>Which way the hit faces, as off the surface it struck, where its applier said.</summary>
        public Vector3? HitNormal { get; private set; }

        /// <summary>How hard it hit, where its applier said, for its cues to scale with; 0 where it said nothing.</summary>
        public float HitMagnitude { get; private set; }

        /// <summary>
        /// Gives the application where it hit, before it's applied: its cues play at the point, facing the normal, and
        /// scale with the magnitude, as a sword's spark where the blade struck.
        /// </summary>
        public GameplayEffectSpec WithHit(Vector3 point, Vector3? normal = null, float magnitude = 0f)
        {
            HitPoint = point;
            HitNormal = normal;
            HitMagnitude = magnitude;
            return this;
        }

        public void RunWorkerApplication(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) Run(worker, worker.OnEffectApplication(ctx), in ctx, EEffectWorkerHook.Application);
            // Its components hear it, at the moment its workers do
            if (Base.Components != null)
                foreach (var component in Base.Components) component?.Land(new EffectComponentContext(ctx));
        }
        public void RunWorkerTick(EffectWorkerContext ctx) => RunWorkerTick(ctx, true);

        /// <summary>Its workers hear the frame's tick, and its components one that executes.</summary>
        internal void RunWorkerTick(EffectWorkerContext ctx, bool executes)
        {
            // A periodic effect's workers are called every frame, with the ticks that frame executes, most frames none
            foreach (var worker in Base.Workers) Run(worker, worker.OnEffectTick(ctx), in ctx, EEffectWorkerHook.Tick);
            // Its components hear a tick as one executes, at the moment its workers do: each call was a tick to
            // them, so a Spread each tick spread every frame. A tick that doesn't execute, as the effect's ongoing
            // requirements fail, they don't hear: its impacts don't land, and its tick cues played anyway
            if (Base.Components != null && ctx.ExecuteTicks > 0 && executes)
                foreach (var component in Base.Components) component?.Tick(new EffectComponentContext(ctx));
        }
        public void RunWorkerRemoval(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) Run(worker, worker.OnEffectRemoval(ctx), in ctx, EEffectWorkerHook.Removal);
            // Its components hear it, at the moment its workers do
            if (Base.Components != null)
                foreach (var component in Base.Components) component?.End(new EffectComponentContext(ctx));
        }
        // A worker's actions, queued; and where the entity it ran on is heard, its run told with what it queued. A
        // tick, which its workers hear every frame and most frames have none of, is told only as one executes or a worker
        // queues something. Its hits run on their dealer's side, the rest on the side it's on
        private void Run(AbstractEffectWorker worker, IEnumerable<IRootAction> actions, in EffectWorkerContext ctx, EEffectWorkerHook hook)
        {
            var callbacks = ctx.System?.GetCallbacks();
            if (callbacks == null || !callbacks.HasEffectWorkerListeners)
            {
                ctx.ActionQueue.EnqueueRange(actions);
                return;
            }
            var queued = actions != null ? new List<IRootAction>(actions) : new List<IRootAction>();
            for (int i = 0; i < queued.Count; i++) ctx.ActionQueue.Enqueue(queued[i]);
            if (hook == EEffectWorkerHook.Tick && ctx.ExecuteTicks <= 0 && queued.Count == 0) return;
            var side = hook == EEffectWorkerHook.Impact ? EWorkerSide.Dealer : EWorkerSide.Target;
            callbacks.EffectWorkerRan(new EffectWorkerRun(this, worker, hook, side, ctx.System, queued));
        }

        /// <summary>Its components hear the one it's on die, while it's on them.</summary>
        internal void RunComponentDeath(EffectWorkerContext ctx)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components) component?.Death(new EffectComponentContext(ctx));
        }

        /// <summary>Its components hear it start to last on the one it's on, lending what they give while it lasts.</summary>
        internal void HearLend(EffectWorkerContext ctx)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components) component?.Lend(new EffectComponentContext(ctx));
        }

        /// <summary>Its components hear it stop lasting on the one it's on, taking back what they lent.</summary>
        internal void HearTakeBack(EffectWorkerContext ctx)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components) component?.TakeBack(new EffectComponentContext(ctx));
        }

        /// <summary>Its components hear this application taken in by the effect on already.</summary>
        internal void HearReapply(EffectWorkerContext ctx)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components) component?.Reapply(new EffectComponentContext(ctx));
        }

        /// <summary>Its components hear this application refused, with why.</summary>
        internal void HearRefused(EffectWorkerContext ctx, EEffectRefusal refusal)
        {
            if (Base?.Components == null) return;
            foreach (var component in Base.Components) component?.Refused(new EffectComponentContext(ctx), refusal);
        }

        /// <summary>Its components hear its stacks change while it lasts.</summary>
        internal void HearStacksChanged(EffectWorkerContext ctx, int before, int after)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components) component?.StacksChanged(new EffectComponentContext(ctx), before, after);
        }

        public void RunWorkerImpact(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) Run(worker, worker.OnEffectImpact(ctx), in ctx, EEffectWorkerHook.Impact);
            // Its components hear it, at the moment its workers do
            if (Base.Components != null)
                foreach (var component in Base.Components) component?.Hit(new EffectComponentContext(ctx));
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

        // Its own granted tags, then those its modifiers add
        private IEnumerable<Tag> GrantedWithChanges()
        {
            if (Base.Tags.GrantedTags != null)
                foreach (var tag in Base.Tags.GrantedTags) yield return tag;
            foreach (var tag in Changes.GrantedTags) yield return tag;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Impacter pipeline — entry-driven
        //
        // One ScalerImpacter per authored AttributeImpactEntry, built lazily and cached
        // for the spec's lifetime. Entries carry their own attribute target, delivery
        // and retention group, so the attribute system can ask for exactly the
        // impacters that apply to (attribute, delivery) without re-deriving intent.
        // ════════════════════════════════════════════════════════════════════════

        private List<(AttributeImpactEntry entry, IAttributeImpacter impacter)> entryImpacters;

        // Per delivery, the attributes impacted and each one's impacters, in entry order, built with the entries: the
        // spec is immutable for its life. Each lookup built a new list or set, on every tick
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
            bool durational = Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Durational;
            Fades = false;
            foreach (var entry in impact.Entries)
            {
                if (entry == null) continue;

                // A lasting barrier of a durational effect fades with it, where its attribute says
                bool fades = durational && entry.Delivery == EImpactDelivery.Continuous
                             && entry.AttributeTarget is Attribute { Role: EAttributeRole.Barrier, FadesWithEffect: true };
                Fades |= fades;
                var impacter = new ScalerImpacter(
                    entry.Magnitude,
                    entry.Operation,
                    entry.TargetSlot,
                    entry.SortOrder,
                    entry.MultiplyBehaviour,
                    entry.ResolveGroup(defaultGroup),
                    entry.AddBehaviour,
                    entry.Pipeline,
                    // The modifiers' stages for its attribute: a lasting entry's on its magnitude, others' as it commits
                    Changes?.StagesFor(entry.AttributeTarget),
                    entry.Delivery == EImpactDelivery.Continuous,
                    fades);
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
            var packets = Base.ImpactSpecification?.Packets;
            if (packets != null)
            {
                for (int i = 0; i < packets.Length; i++)
                {
                    var contained = packets[i].ContainedEffect;
                    if (packets[i].Policy != phase || contained == null) continue;

                    // On this one's target, or on its caster where it says
                    var onto = packets[i].LandsOn == EEffectRecipient.Caster ? Origin?.GetSource() as ITarget : Target;
                    if (onto == null) continue;

                    // With this one's magnitudes, as a contained effect takes them; the rules whose added effect
                    // this is change it no more
                    var made = EffectModifiers.Make(onto, Origin, contained, this, null);
                    made?.TakeSetByCaller(setByCaller);
                    yield return made;
                }
            }

            // Those its modifiers add, each made with the rule that added it kept from changing it again
            if (Changes == null) yield break;
            foreach (var packet in Changes.Packets)
            {
                if (packet.When != phase || packet.Effect == null) continue;
                var onto = packet.LandsOn == EEffectRecipient.Caster ? Origin?.GetSource() as ITarget : Target;
                if (onto == null) continue;
                var made = EffectModifiers.Make(onto, Origin, packet.Effect, this, packet.AddedBy);
                made?.TakeSetByCaller(setByCaller);
                yield return made;
            }
        }

        // IsSustainedModifier removed — the Mode A/B inference is gone. Each impact entry
        // declares its own delivery (Continuous | OnApply | Periodic); delivery implies
        // the contribution kind. See AttributeImpactEntry.

        public bool ValidateApplication(ITarget target)
        {
            // Mirrors the gating that RuntimeGameplayAbilitySystem.ApplyGameplayEffect
            // does today; centralising it here lets the new ITarget.ApplyCarrier path
            // use one validation contract regardless of the carrier's concrete kind.
            return ForgeHelper.ValidateEffectApplicationRequirements(this, target.GetAffiliation());
        }
    }
}
