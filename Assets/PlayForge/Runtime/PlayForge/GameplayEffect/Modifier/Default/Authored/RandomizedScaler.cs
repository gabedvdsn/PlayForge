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

        [Tooltip("Roll once per effect spec and keep it, so a buff holds its roll and every tick of one application " +
                 "deals the same. Off, it rolls on every evaluation, and a buff re-rolls whenever its attribute recomputes")]
        public bool RollOncePerSpec = true;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            // One roll per spec, kept with what the spec captures. Rolled on every evaluation, a modifier re-rolled
            // whenever its attribute recomputed (GAS-27, Decision 12). Initialize can run twice for one spec
            var captured = deriv?.GetSourcedCapturedAttributes();
            if (!RollOncePerSpec || captured is null || captured.ContainsKey(this)) return;

            float roll = ForgeRandom.Value;
            captured[this] = new AttributeValue(roll, roll);
        }

        // The spec's roll, or a fresh one when rolling every time or for a carrier that keeps none
        private float Roll(IAttributeImpactCarrier deriv)
        {
            if (RollOncePerSpec && deriv?.GetSourcedCapturedAttributes() is { } captured
                                && captured.TryGetValue(this, out var kept) && kept.HasValue)
            {
                return kept.Value.CurrentValue;
            }

            return ForgeRandom.Value;
        }

        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            float baseValue = EvaluateFromSpec(deriv);
            float roll = Roll(deriv);

            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    // ±Variance% of base value
                    float range = baseValue * Variance;
                    return baseValue + Mathf.Lerp(-range, range, roll);

                case EVarianceMode.Flat:
                    // ±Variance flat amount
                    return baseValue + Mathf.Lerp(-Variance, Variance, roll);

                case EVarianceMode.Multiplier:
                    // Multiply by random value in range
                    return baseValue * Mathf.Lerp(MinMultiplier, MaxMultiplier, roll);
                    
                default:
                    return baseValue;
            }
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        /// <summary>
        /// Gets the minimum possible value at current level (for UI display).
        /// </summary>
        public float GetMinValue(IAttributeImpactCarrier spec)
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
        public float GetMaxValue(IAttributeImpactCarrier spec)
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