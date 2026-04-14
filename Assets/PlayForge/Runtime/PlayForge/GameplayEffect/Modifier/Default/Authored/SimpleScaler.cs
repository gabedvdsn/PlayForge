using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Simple level-based scaler that evaluates based on relative level.
    /// Uses the level values array for easy configuration.
    /// </summary>
    public class SimpleScaler : AbstractScaler
    {
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            return EvaluateFromSpec(spec);
        }

    }
}
