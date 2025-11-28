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
        public Attribute GetAttribute();
        public IEffectOrigin GetEffectDerivation();
        public ISource GetSource();
        public ITarget GetTarget();
        public List<Tag> GetImpactTypes();
        public Tag AttributeRetention();
        public void TrackImpact(AbilityImpactData impactData);
        public bool TryGetTrackedImpact(out AttributeValue impactValue);
        public bool TryGetLastTrackedImpact(out AttributeValue impactValue);
        public List<Tag> GetContextTags();
        public void RunEffectApplicationWorkers();
        public void RunEffectTickWorkers();
        public void RunEffectRemovalWorkers();
        public void RunEffectImpactWorkers(AbilityImpactData impactData);
        public Dictionary<IMagnitudeModifier, AttributeValue?> GetSourcedCapturedAttributes();
        
        public static SourceAttributeDerivation GenerateSourceDerivation(ISource source, Attribute attribute, Tag retainImpact, Tag impactType)
        {
            return new SourceAttributeDerivation(source, attribute, impactType, retainImpact);
        }

        public static SourceAttributeDerivation GenerateSourceDerivation(SourcedModifiedAttributeValue sourceModifier, Tag retainImpact, Tag impactType)
        {
            return GenerateSourceDerivation(sourceModifier.Derivation.GetSource(), sourceModifier.Derivation.GetAttribute(), retainImpact, impactType);
        }
    }

    public class SourceAttributeDerivation : IAttributeImpactDerivation
    {
        private ISource Source;
        public Attribute Attribute;
        private Tag ImpactType;
        private Tag RetainImpact;

        public SourceAttributeDerivation(ISource source, Attribute attribute, Tag impactType, Tag retainImpact)
        {
            Source = source;
            Attribute = attribute;
            ImpactType = impactType;
            RetainImpact = retainImpact;
        }

        public Attribute GetAttribute()
        {
            return Attribute;
        }
        public IEffectOrigin GetEffectDerivation()
        {
            return IEffectOrigin.GenerateSourceDerivation(Source);
        }
        public ISource GetSource()
        {
            return Source;
        }
        public ITarget GetTarget()
        {
            return Source;
        }
        public List<Tag> GetImpactTypes()
        {
            return new List<Tag>(){ ImpactType };
        }

        public Tag AttributeRetention()
        {
            return RetainImpact;
        }
        
        public void TrackImpact(AbilityImpactData impactData)
        {
            // Source derivations do not track their impact
        }
        
        public bool TryGetTrackedImpact(out AttributeValue impactValue)
        {
            impactValue = default;
            return false;
        }
        public bool TryGetLastTrackedImpact(out AttributeValue impactValue)
        {
            impactValue = default;
            return false;
        }
        public List<Tag> GetContextTags()
        {
            return Source.GetContextTags();
        }
        public void RunEffectApplicationWorkers()
        {
            // Nothing to do here!
        }
        public void RunEffectTickWorkers()
        {
            // Nothing to do here!
        }
        public void RunEffectRemovalWorkers()
        {
            // Nothing to do here!
        }
        public void RunEffectImpactWorkers(AbilityImpactData impactData)
        {
            // Nothing to do here!
        }
        public Dictionary<IMagnitudeModifier, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return new();
        }
    }
}
