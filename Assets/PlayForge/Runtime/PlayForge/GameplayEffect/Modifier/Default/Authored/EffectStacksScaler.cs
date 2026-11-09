using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The stack count of the effect's own container: 1 for an effect that doesn't stack. With Multiply With Scaler, a
    /// stacking effect's change grows by the stack, as a sunder of −5 armor a stack does, and a Continuous change reads
    /// the count again as it changes. Only a scaler written for it read an effect's stack count (FEAT-19).
    /// </summary>
    [Serializable]
    [NumberKind("Effect Stacks", Category = "Effect")]
    public class EffectStacksScaler : AbstractScaler
    {
        // It reads the effect's stacks alone (ED-51)
        public override EScalerParts Parts => EScalerParts.None;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
        }

        public override float Evaluate(IAttributeImpactCarrier deriv) =>
            deriv is GameplayEffectSpec { RuntimeContainer: AbstractStackingEffectContainer container } ? container.Stacks : 1f;

        public override float Evaluate(AbstractStackingEffectContainer container) => container?.Stacks ?? 1f;
    }
}
