using System;

namespace FarEmerald.PlayForge
{
    public class AttenuateScalerBehaviour : AbstractScalerBehaviour
    {
        public EMathApplicationPolicy Policy;
        
        public override void Initialize(IAttributeImpactCarrier spec)
        {
            
        }
        public override float Evaluate(float magnitude, IAttributeImpactCarrier spec)
        {
            return Policy switch
            {

                EMathApplicationPolicy.AsIs => magnitude,
                EMathApplicationPolicy.OnePlus => 1 + magnitude,
                EMathApplicationPolicy.OneMinus => 1 - magnitude,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
