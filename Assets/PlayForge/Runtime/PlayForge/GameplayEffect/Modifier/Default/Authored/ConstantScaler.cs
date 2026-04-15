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
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            return Value;
        }
    }
}
