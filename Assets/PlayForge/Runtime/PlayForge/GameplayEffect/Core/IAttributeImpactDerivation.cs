using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Attribute impact derivations are sources of attribute impact (impact carriers)
    /// </summary>
    public interface IAttributeImpactDerivation
    {
        public Tag GetCacheKey();
        public bool DerivationAlive();
        public IAttribute GetAttribute();
        public IEffectOrigin GetEffectDerivation();
        public ISource GetSource();
        public ITarget GetTarget();
        public List<Tag> GetImpactTypes();
        public Tag GetRetentionGroup();
        public void TrackImpact(ImpactData impactData);
        public TrackedImpact GetTrackedImpact();
        public ImpactDerivationContext GetContextTags();
        public void RunWorkerApplication(EffectWorkerContext ctx);
        public void RunWorkerTick(EffectWorkerContext ctx);
        public void RunWorkerRemoval(EffectWorkerContext ctx);
        public void RunWorkerImpact(EffectWorkerContext ctx);
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes();
        public bool RetainImpact();

        public static LevelerImpactDerivation GenerateLevelerDerivation(ISource source, IntValuePairClamped level, IAttribute attribute = null)
        {
            return new LevelerImpactDerivation(source, level, attribute);
        }

        public static SourceAttributeImpact GenerateSourceDerivation(ISource source, IAttribute attribute)
        {
            return new SourceAttributeImpact(source, attribute,
                new List<Tag>() { Tags.DisallowImpact }, Tags.IgnoreRetention,
                null, false);
        }
        
        public static SourceAttributeImpact GenerateSourceDerivation(ISource source, IAttribute attribute, Tag retentionGroup, List<Tag> impactType = null, IAttributeImpactDerivation rootDerivation = null, bool retainOverride = false)
        {
            return new SourceAttributeImpact(
                source, attribute, 
                impactType ?? new List<Tag>() { Tags.DisallowImpact }, retentionGroup,
                rootDerivation, retainOverride);
        }

        public static SourceAttributeImpact GenerateSourceDerivation(SourcedModifiedAttributeValue sourceModifier, Tag retentionGroup, List<Tag> impactType, IAttributeImpactDerivation rootDerivation = null, bool retainOverride = false)
        {
            return GenerateSourceDerivation(
                sourceModifier.Derivation.GetSource(), sourceModifier.Derivation.GetAttribute(), 
                retentionGroup, impactType,
                rootDerivation, retainOverride);
        }

        public static NullifiedImpactDerivation GenerateNullifiedDerivation(IAttributeImpactDerivation derivation)
        {
            return new NullifiedImpactDerivation(
                derivation.GetCacheKey(), 
                derivation.GetSource(), derivation.GetAttribute(), 
                derivation.GetImpactTypes(), 
                derivation.GetRetentionGroup(), derivation);
        }
    }

    public class SourceAttributeImpact : IAttributeImpactDerivation
    {
        public readonly ISource Source;
        public readonly IAttribute Attribute;
        private readonly List<Tag> ImpactType;
        private readonly Tag RetentionGroup;
        
        private IAttributeImpactDerivation RootDerivation;
        private bool RetainFallback = false;

        private TrackedImpact TrackedImpact;

        public SourceAttributeImpact(ISource source, IAttribute attribute, List<Tag> impactType, Tag retentionGroup, IAttributeImpactDerivation rootDerivation, bool retainFallback)
        {
            Source = source;
            Attribute = attribute;
            ImpactType = impactType;
            RetentionGroup = retentionGroup;
            RootDerivation = rootDerivation;
            RetainFallback = retainFallback;
            TrackedImpact = new TrackedImpact();
        }

        public virtual Tag GetCacheKey()
        {
            return RootDerivation?.GetCacheKey() ?? Source.GetAssetTag();
        }
        public virtual bool DerivationAlive()
        {
            return Source?.IsActive() ?? false;
        }
        public IAttribute GetAttribute() => Attribute;
        public virtual IEffectOrigin GetEffectDerivation() => RootDerivation?.GetEffectDerivation() ?? IEffectOrigin.GenerateSourceDerivation(Source);
        public ISource GetSource() => Source;
        public ITarget GetTarget() => Source;
        public List<Tag> GetImpactTypes() => ImpactType;
        public Tag GetRetentionGroup() => RetentionGroup;
        public void TrackImpact(ImpactData impactData) => TrackedImpact.Add(impactData.RealImpact);
        public TrackedImpact GetTrackedImpact() => TrackedImpact;
        public ImpactDerivationContext GetContextTags() => RootDerivation?.GetContextTags() ?? new ImpactDerivationContext(Source.GetContextTags(), null);
        
        public void RunWorkerApplication(EffectWorkerContext ctx) { }
        public void RunWorkerTick(EffectWorkerContext ctx) { }
        public void RunWorkerRemoval(EffectWorkerContext ctx) { }
        public void RunWorkerImpact(EffectWorkerContext ctx) { }
        
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return new();
        }
        public bool RetainImpact()
        {
            return RootDerivation?.RetainImpact() ?? RetainFallback;
        }
    }

    public class NullifiedImpactDerivation : SourceAttributeImpact
    {
        public Tag CacheKey;
        public Tag RootKey;
        
        public NullifiedImpactDerivation(Tag cacheKey, ISource source, IAttribute attribute, List<Tag> impactType, Tag retentionGroup, IAttributeImpactDerivation rootDerivation) : base(source, attribute, impactType, retentionGroup, null, rootDerivation.RetainImpact())
        {
            CacheKey = cacheKey;
            RootKey = rootDerivation.GetEffectDerivation().GetAssetTag();
        }

        public override Tag GetCacheKey()
        {
            return CacheKey;
        }

        public override bool DerivationAlive()
        {
            return false;
        }
    }

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
        public override IEffectOrigin GetEffectDerivation()
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
