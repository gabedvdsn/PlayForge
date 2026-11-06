using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Returns a constant value regardless of level.
    /// Useful for fixed bonuses or base values.
    /// </summary>
    public class ConstantScaler : AbstractScaler
    {
        [Tooltip("The constant value to return")]
        public float Value = 1f;

        // It reads its value alone (ED-51)
        public override EScalerParts Parts => EScalerParts.None;
        
        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            return Value;
        }
    }
}
