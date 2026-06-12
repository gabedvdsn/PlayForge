using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractScalerBehaviour
    {
        public abstract void Initialize(IAttributeImpactCarrier spec);
        public virtual void Initialize(AbstractStackingEffectContainer container) => Initialize(container.Spec);

        public abstract float Evaluate(float magnitude, IAttributeImpactCarrier spec);
        public virtual float Evaluate(float magnitude, AbstractStackingEffectContainer container) => Evaluate(magnitude, container.Spec);
    }

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
