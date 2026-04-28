using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Evaluates like SimpleScaler but clamps the result between min and max.
    /// Useful for ensuring values stay within acceptable bounds.
    /// </summary>
    public class ClampedScaler : AbstractScaler
    {
        [Tooltip("Minimum value (floor)")]
        public float MinValue = 0f;
        
        [Tooltip("Maximum value (ceiling)")]
        public float MaxValue = 100f;
        
        public override void Initialize(IAttributeImpactDerivation deriv)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation deriv)
        {
            float value = EvaluateFromSpec(deriv);
            return Mathf.Clamp(value, MinValue, MaxValue);
        }
    }
}
