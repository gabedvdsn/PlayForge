using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Clamps a value between a floor and a ceiling: an inner scaler's, as damage worked out from a target's missing health
    /// is capped, or else its own values per level, as a Simple scaler reads them. It clamped its own table alone, so
    /// nothing another scaler worked out could be capped (FEAT-27).
    /// </summary>
    public class ClampedScaler : AbstractScaler
    {
        [Tooltip("Minimum value (floor)")]
        public float MinValue = 0f;
        
        [Tooltip("Maximum value (ceiling)")]
        public float MaxValue = 100f;

        /// <summary>The scaler whose value it clamps. With none, it clamps its own values per level (FEAT-27).</summary>
        [Tooltip("The scaler whose value it clamps. With none, it clamps its own values per level")]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference]
        public AbstractScaler Inner;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            Inner?.Initialize(deriv);
        }

        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            float value = Inner != null ? Inner.Evaluate(deriv) : EvaluateFromSpec(deriv);
            return Mathf.Clamp(value, MinValue, MaxValue);
        }

        // With an inner scaler, it reads no values of its own (FEAT-27)
        public override EScalerParts Parts => Inner == null ? base.Parts : EScalerParts.None;
    }
}
