using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttrCacheSimpleMagnitudeModifier : AbstractCachedMagnitudeModifier
    {
        public AnimationCurve Scaling;

        public override void Initialize(IAttributeImpactDerivation spec)
        {
            
        }
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            return Scaling.Evaluate(spec.GetEffectDerivation().GetRelativeLevel());
        }

        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            // Doesn't do anything
        }
    }
}
