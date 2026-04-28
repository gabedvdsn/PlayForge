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
        
        public override void Initialize(IAttributeImpactDerivation deriv)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation deriv)
        {
            return Value;
        }
    }
}
