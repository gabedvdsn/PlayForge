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


}
