using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SimpleMagnitudeModifier : AbstractMagnitudeModifier
    {
        public AnimationCurve Scaling;

        public override void Initialize(IAttributeImpactDerivation spec)
        {
            
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            return Scaling.Evaluate(spec.GetEffectDerivation().GetRelativeLevel());
        }
    }
}
