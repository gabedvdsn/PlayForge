using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A rule in a scaler's Behaviours, which adjusts the value the scaler works out, in the list's order: at each
    /// read, a behaviour is handed the carrier the value is read for (<c>Initialize</c>), then the value so far, and
    /// gives back the value after it (<c>Evaluate</c>). Derive from it and override both.
    /// </summary>
    [Serializable]
    public abstract class AbstractScalerBehaviour
    {
        public abstract void Initialize(IAttributeImpactCarrier spec);
        public virtual void Initialize(AbstractStackingEffectContainer container) => Initialize(container.Spec);

        public abstract float Evaluate(float magnitude, IAttributeImpactCarrier spec);
        public virtual float Evaluate(float magnitude, AbstractStackingEffectContainer container) => Evaluate(magnitude, container.Spec);
    }


}
