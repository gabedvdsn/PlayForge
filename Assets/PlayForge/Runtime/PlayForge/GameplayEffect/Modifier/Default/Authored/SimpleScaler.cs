using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Simple level-based scaler that evaluates based on relative level.
    /// Uses the level values array for easy configuration.
    /// </summary>
    [NumberKind("By Level", Reads = NumberReads.Level)]
    [ForgeMenu("", Description = "A number for each level.")]
    public class SimpleScaler : AbstractScaler
    {
        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            return EvaluateFromSpec(deriv);
        }

    }
}
