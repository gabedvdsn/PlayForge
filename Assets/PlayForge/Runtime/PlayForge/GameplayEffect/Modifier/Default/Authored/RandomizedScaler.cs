using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Adds random variance to level-based scaling.
    /// Useful for damage ranges, proc chances, etc.
    /// Its variance and its multipliers are numbers of any kind, read with the scaler as their owner, so a variance can
    /// widen by level and a Max multiplier can be 3 × its Min (FEAT-29).
    /// </summary>
    [Serializable]
    [NumberKind("Randomized", Category = "Scaler")]
    public class RandomizedScaler : AbstractScaler, ISerializationCallbackReceiver
    {
        [Tooltip("How variance is applied")]
        public EVarianceMode VarianceMode = EVarianceMode.Percentage;

        [Tooltip("Amount of variance: a share of the base value as a percentage, or an amount as a flat variance")]
        [ForgeShowWhen(nameof(VarianceMode), EVarianceMode.Percentage, EVarianceMode.Flat)]
        [NumberMin(0f)]
        public ScalableFloat Variance = 0.1f;

        [Tooltip("Minimum multiplier when using random range")]
        [ForgeShowWhen(nameof(VarianceMode), EVarianceMode.Multiplier)]
        public ScalableFloat MinMultiplier = 0.9f;

        [Tooltip("Maximum multiplier when using random range")]
        [ForgeShowWhen(nameof(VarianceMode), EVarianceMode.Multiplier)]
        public ScalableFloat MaxMultiplier = 1.1f;

        [Tooltip("Roll once per effect spec and keep it, so a buff holds its roll and every tick of one application " +
                 "deals the same. Off, it rolls on every evaluation, and a buff re-rolls whenever its attribute recomputes")]
        public bool RollOncePerSpec = true;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            Variance?.Initialize(deriv);
            MinMultiplier?.Initialize(deriv);
            MaxMultiplier?.Initialize(deriv);

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

        // Its own numbers, read where it is, with itself as their owner (FEAT-29)
        private float Read(ScalableFloat number, IAttributeImpactCarrier deriv, float fallback) =>
            number?.Evaluate(NumberContext.Of(deriv), this) ?? fallback;

        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            float baseValue = EvaluateFromSpec(deriv);
            float roll = Roll(deriv);

            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    // ±Variance% of base value
                    float range = baseValue * Read(Variance, deriv, 0f);
                    return baseValue + Mathf.Lerp(-range, range, roll);

                case EVarianceMode.Flat:
                    // ±Variance flat amount
                    float variance = Read(Variance, deriv, 0f);
                    return baseValue + Mathf.Lerp(-variance, variance, roll);

                case EVarianceMode.Multiplier:
                    // Multiply by random value in range
                    return baseValue * Mathf.Lerp(Read(MinMultiplier, deriv, 1f), Read(MaxMultiplier, deriv, 1f), roll);

                default:
                    return baseValue;
            }
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        // Its base value is its values per level, through its behaviours, so it reads them all (ED-51)
        public override EScalerParts Parts => EScalerParts.All;

        /// <summary>
        /// Gets the minimum possible value at current level (for UI display).
        /// </summary>
        public float GetMinValue(IAttributeImpactCarrier spec)
        {
            float baseValue = EvaluateFromSpec(spec);

            switch (VarianceMode)
            {
                case EVarianceMode.Percentage:
                    return baseValue * (1f - Read(Variance, spec, 0f));
                case EVarianceMode.Flat:
                    return baseValue - Read(Variance, spec, 0f);
                case EVarianceMode.Multiplier:
                    return baseValue * Read(MinMultiplier, spec, 1f);
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
                    return baseValue * (1f + Read(Variance, spec, 0f));
                case EVarianceMode.Flat:
                    return baseValue + Read(Variance, spec, 0f);
                case EVarianceMode.Multiplier:
                    return baseValue * Read(MaxMultiplier, spec, 1f);
                default:
                    return baseValue;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Saved before its variance was a number (FEAT-29)
        // ═══════════════════════════════════════════════════════════════════════════

        [FormerlySerializedAs("Variance"), SerializeField, HideInInspector] private float legacyVariance = LegacyNumber.None;
        [FormerlySerializedAs("MinMultiplier"), SerializeField, HideInInspector] private float legacyMinMultiplier = LegacyNumber.None;
        [FormerlySerializedAs("MaxMultiplier"), SerializeField, HideInInspector] private float legacyMaxMultiplier = LegacyNumber.None;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            LegacyNumber.Move(ref legacyVariance, ref Variance);
            LegacyNumber.Move(ref legacyMinMultiplier, ref MinMultiplier);
            LegacyNumber.Move(ref legacyMaxMultiplier, ref MaxMultiplier);
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
