using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Adds random variance to level-based scaling.
    /// Useful for damage ranges, proc chances, etc.
    /// </summary>
    public class RandomizedScaler : AbstractScaler
    {
        [Tooltip("How variance is applied")]
        public EVarianceMode VarianceMode = EVarianceMode.Percentage;
        
        [Tooltip("Amount of variance (interpretation depends on mode)")]
        [Range(0f, 1f)]
        public float Variance = 0.1f;
        
        [Tooltip("Minimum multiplier when using random range")]
        public float MinMultiplier = 0.9f;
        
        [Tooltip("Maximum multiplier when using random range")]
        public float MaxMultiplier = 1.1f;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            float baseValue = EvaluateFromSpec(spec);
            
            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    // ±Variance% of base value
                    float range = baseValue * Variance;
                    return baseValue + Random.Range(-range, range);
                    
                case EVarianceMode.Flat:
                    // ±Variance flat amount
                    return baseValue + Random.Range(-Variance, Variance);
                    
                case EVarianceMode.Multiplier:
                    // Multiply by random value in range
                    return baseValue * Random.Range(MinMultiplier, MaxMultiplier);
                    
                default:
                    return baseValue;
            }
        }

        /*public override bool UseScalingOptions()
        {
            return false;
        }*/

        /// <summary>
        /// Gets the minimum possible value at current level (for UI display).
        /// </summary>
        public float GetMinValue(IAttributeImpactDerivation spec)
        {
            float baseValue = EvaluateFromSpec(spec);
            
            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    return baseValue * (1f - Variance);
                case EVarianceMode.Flat:
                    return baseValue - Variance;
                case EVarianceMode.Multiplier:
                    return baseValue * MinMultiplier;
                default:
                    return baseValue;
            }
        }
        
        /// <summary>
        /// Gets the maximum possible value at current level (for UI display).
        /// </summary>
        public float GetMaxValue(IAttributeImpactDerivation spec)
        {
            float baseValue = EvaluateFromSpec(spec);
            
            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    return baseValue * (1f + Variance);
                case EVarianceMode.Flat:
                    return baseValue + Variance;
                case EVarianceMode.Multiplier:
                    return baseValue * MaxMultiplier;
                default:
                    return baseValue;
            }
        }
    }
    
    public enum EVarianceMode
    {
        [Tooltip("Variance is a percentage of base value")]
        Percentage,
        
        [Tooltip("Variance is a flat amount added/subtracted")]
        Flat,
        
        [Tooltip("Base value is multiplied by random value in range")]
        Multiplier
    }
}