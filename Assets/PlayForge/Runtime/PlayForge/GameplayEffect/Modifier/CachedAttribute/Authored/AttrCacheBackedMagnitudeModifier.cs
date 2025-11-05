using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttrCacheBackedMagnitudeModifier : AbstractCachedMagnitudeModifier
    {
        public Attribute CaptureAttribute;
        public AnimationCurve Scaling;
        public EEffectImpactTargetLimited ScalingPolicy;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            
        }
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (!spec.GetSource().FindAttributeSystem(out var attr) || !attr.TryGetAttributeValue(CaptureAttribute, out AttributeValue attributeValue)) return 0f;
            return ScalingPolicy switch
            {

                EEffectImpactTargetLimited.Current => Scaling.Evaluate(attributeValue.CurrentValue),
                EEffectImpactTargetLimited.Base => Scaling.Evaluate(attributeValue.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            rules.RegisterRelation(CaptureAttribute, attribute);
        }
    }
}
