using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Attribute impact carriers are sources of attribute impact (impact carriers)
    /// </summary>
    public interface IAttributeImpactCarrier : ITagSource
    {
        public IHasReadableDefinition GetReadableDefinition();
        public bool DerivationAlive();
        public IAttribute GetAttribute();
        public IEffectOrigin GetOrigin();
        public ITarget GetTarget();
        public List<Tag> GetImpactTypes();
        public Tag GetRetentionGroup();
        public void TrackImpact(ImpactData impactData);
        public TrackedImpact GetTrackedImpact();
        public ImpactDerivationContext GetContextTags();
        // Worker-phase hooks (RunWorkerApplication/Tick/Removal/Impact) moved to
        // ICarrierApplication — synthetic carriers like SourceAttributeImpact have no
        // worker lifecycle and shouldn't be forced to implement four no-ops.
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes();
        public bool RetainImpact();

        /// <summary>
        /// The magnitude-evaluation pipeline this carrier contributes to the cached
        /// attribute aggregator. Default empty so existing implementations remain valid
        /// during the additive phase of the Origin/Carrier/Impacter refactor; concrete
        /// carriers override to expose their <see cref="ScalerImpacter"/>s,
        /// <see cref="ConstantImpacter"/>s, etc.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<IAttributeImpacter> GetImpacters()
            => System.Array.Empty<IAttributeImpacter>();

        public static LevelerImpactDerivation GenerateLevelerDerivation(ISource source, IntValuePairClamped level, IAttribute attribute = null)
        {
            return new LevelerImpactDerivation(source, level, attribute);
        }

        // A raw delta's carrier, with no impact type, as GenerateSimple's is (GAS-25)
        public static SourceAttributeImpact GenerateSourceDerivation(ISource source, IAttribute attribute)
        {
            return new SourceAttributeImpact(source, attribute,
                new List<Tag>(), Tags.IgnoreRetention,
                null, false);
        }
        
        public static SourceAttributeImpact GenerateSourceDerivation(ISource source, IAttribute attribute, Tag retentionGroup, List<Tag> impactType = null, IAttributeImpactCarrier rootCarrier = null, bool retainOverride = false)
        {
            return new SourceAttributeImpact(
                source, attribute, 
                impactType ?? new List<Tag>() { Tags.DisallowImpact }, retentionGroup,
                rootCarrier, retainOverride);
        }

        public static SourceAttributeImpact GenerateSourceDerivation(SourcedModifiedAttributeValue sourceModifier, Tag retentionGroup, List<Tag> impactType, IAttributeImpactCarrier rootCarrier = null, bool retainOverride = false)
        {
            return GenerateSourceDerivation(
                sourceModifier.Carrier.GetOrigin().GetSource(), sourceModifier.Carrier.GetAttribute(), 
                retentionGroup, impactType,
                rootCarrier, retainOverride);
        }

        // GenerateNullifiedDerivation removed — the recompute model has no zombie state.
        // Carriers are present in retainedGroups iff they actively contribute; lifecycle
        // owners call RemoveCarrier when contribution should stop. There is no scenario
        // that needs a "marker carrier that exists but contributes nothing".
    }

    public class SourceAttributeImpact : IAttributeImpactCarrier
    {
        public readonly ISource Source;
        public readonly IAttribute Attribute;
        private readonly List<Tag> ImpactType;
        private readonly Tag RetentionGroup;

        private IAttributeImpactCarrier RootCarrier;
        private bool RetainFallback = false;

        private TrackedImpact TrackedImpact;

        // Optional impacter list — populated when this carrier represents a synthetic
        // (precomputed) delta. Identity-only carriers (the common case for source
        // derivations attached to instant attribute writes) leave it null and the
        // base interface's empty default applies.
        private IReadOnlyList<IAttributeImpacter> Impacters;

        public SourceAttributeImpact(ISource source, IAttribute attribute, List<Tag> impactType, Tag retentionGroup, IAttributeImpactCarrier rootCarrier, bool retainFallback)
        {
            Source = source;
            Attribute = attribute;
            ImpactType = impactType;
            RetentionGroup = retentionGroup;
            RootCarrier = rootCarrier;
            RetainFallback = retainFallback;
            TrackedImpact = new TrackedImpact();
        }

        /// <summary>
        /// Construct with a pre-built impacter list. Used by synthetic-delta callsites
        /// (auto-scale propagation, tracked-impact reversal, root initialisation) that
        /// want their contribution visible to the recompute aggregator instead of being
        /// blindly committed past it.
        /// </summary>
        public SourceAttributeImpact(
            ISource source,
            IAttribute attribute,
            List<Tag> impactType,
            Tag retentionGroup,
            IAttributeImpactCarrier rootCarrier,
            bool retainFallback,
            IReadOnlyList<IAttributeImpacter> impacters)
            : this(source, attribute, impactType, retentionGroup, rootCarrier, retainFallback)
        {
            Impacters = impacters;
        }

        /// <summary>
        /// Build a SourceAttributeImpact whose impacters represent a (currentDelta, baseDelta)
        /// Add contribution. Zero-magnitude slots are omitted so the recompute aggregator
        /// doesn't iterate over no-op impacters.
        /// </summary>
        public static SourceAttributeImpact FromAdditiveDelta(
            ISource source,
            IAttribute attribute,
            float currentDelta,
            float baseDelta,
            Tag retentionGroup = default,
            List<Tag> impactType = null,
            IAttributeImpactCarrier rootCarrier = null,
            bool retainFallback = false)
        {
            var group = retentionGroup.IsValid ? retentionGroup : Tags.IgnoreRetention;
            var built = new System.Collections.Generic.List<IAttributeImpacter>(2);
            var currentStep = currentDelta != 0f ? new ConstantImpacter(currentDelta, ECalculationOperation.Add, EAttributeTarget.Current, group: group) : null;
            var baseStep    = baseDelta    != 0f ? new ConstantImpacter(baseDelta,    ECalculationOperation.Add, EAttributeTarget.Base,    group: group) : null;

            // A commit clamps after every step, and Current is bounded by Base. So a growing Base
            // goes first (else the Current gain is clamped against the old Base and lost), and a
            // shrinking one goes last (it then pulls Current down to the new bound). PF-005.
            if (baseDelta > 0f && baseStep != null) built.Add(baseStep);
            if (currentStep != null) built.Add(currentStep);
            if (baseDelta < 0f && baseStep != null) built.Add(baseStep);
            return new SourceAttributeImpact(
                source, attribute,
                impactType ?? new List<Tag>() { Tags.DisallowImpact },
                retentionGroup.IsValid ? retentionGroup : Tags.IgnoreRetention,
                rootCarrier, retainFallback,
                built);
        }

        public virtual IHasReadableDefinition GetReadableDefinition()
        {
            return RootCarrier?.GetReadableDefinition() ?? Source;
        }
        public virtual bool DerivationAlive()
        {
            return Source?.IsActive() ?? false;
        }
        public IAttribute GetAttribute() => Attribute;
        public virtual IEffectOrigin GetOrigin() => RootCarrier?.GetOrigin() ?? IEffectOrigin.GenerateSourceDerivation(Source);
        public ITarget GetTarget() => Source;
        public List<Tag> GetImpactTypes() => ImpactType;
        public Tag GetRetentionGroup() => RetentionGroup;
        public void TrackImpact(ImpactData impactData) => TrackedImpact.Add(impactData.RealImpact);
        public TrackedImpact GetTrackedImpact() => TrackedImpact;
        public ImpactDerivationContext GetContextTags() => RootCarrier?.GetContextTags() ?? new ImpactDerivationContext(Source.GetContextTags(), null);
        
        // RunWorker* removed from IAttributeImpactCarrier (step 6) — SourceAttributeImpact
        // never had real worker lifecycle to run; the four no-ops were dead weight.
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return new();
        }
        public bool RetainImpact()
        {
            return RootCarrier?.RetainImpact() ?? RetainFallback;
        }
        public IEnumerable<Tag> GetGrantedTags()
        {
            return RootCarrier?.GetGrantedTags() ?? Array.Empty<Tag>();
        }

        /// <summary>
        /// Returns the impacter list this carrier was constructed with, or an empty list
        /// for identity-only carriers. Identity-only carriers route through the legacy
        /// delta-and-forget path during the additive phase of the refactor — once the
        /// recompute aggregator is the only modify path (refactor step 5), every
        /// caller will be expected to supply impacters.
        /// </summary>
        public IReadOnlyList<IAttributeImpacter> GetImpacters()
            => Impacters ?? Array.Empty<IAttributeImpacter>();
    }

    // NullifiedImpactDerivation removed — see comment on the (also removed)
    // GenerateNullifiedDerivation factory above. The recompute model has no zombie state.

    public class LevelerImpactDerivation : SourceAttributeImpact
    {
        private LevelerEffectOrigin levelerOrigin;
        
        public LevelerImpactDerivation(ISource source, IntValuePairClamped level, IAttribute attribute = null) : base(source, attribute, new List<Tag>(){ Tags.DisallowImpact}, Tags.IgnoreRetention, null, false)
        {
            levelerOrigin = IEffectOrigin.GenerateLevelerDerivation(source, level);
        }
        
        public override bool DerivationAlive()
        {
            return false;
        }
        public override IEffectOrigin GetOrigin()
        {
            return levelerOrigin;
        }
    }

    public struct ImpactDerivationContext
    {
        public List<Tag> OriginTags;
        public List<Tag> DerivationTags;

        public ImpactDerivationContext(List<Tag> originTags, List<Tag> derivationTags)
        {
            OriginTags = originTags;
            DerivationTags = derivationTags;
        }

        /// <summary>
        /// Enumerates origin and derivation context tags (in that order).
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tag> All()
        {
            if (OriginTags is not null)
            {
                foreach (var t in OriginTags) yield return t;
            }

            if (DerivationTags is null) yield break;
            
            foreach (var t in DerivationTags) yield return t;
        }
    }
}
