using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Adds random variance to level-based scaling.
    /// Useful for damage ranges, proc chances, etc.
    /// Its variance is a class: by a share of its value, by an amount, between two multipliers, or a way of your own, as
    /// dice. A variance's numbers are of any kind, read with the variance as their owner, so a share can widen by level
    /// and a Max multiplier can be 3 × its Min.
    /// </summary>
    [Serializable]
    [NumberKind("Randomized")]
    [ForgeMenu("Scaler", Description = "A number for each level, with random variance.")]
    public class RandomizedScaler : AbstractScaler
    {
        [Tooltip("How its roll varies its value: by a share of it, by an amount, between two multipliers, or a way of your " +
                 "own; none, not at all")]
        [SerializeReference] public AbstractVariance Variance = new PercentageVariance();

        [Tooltip("Roll once per effect spec and keep it, so a buff holds its roll and every tick of one application " +
                 "deals the same. Off, it rolls on every evaluation, and a buff re-rolls whenever its attribute recomputes")]
        public bool RollOncePerSpec = true;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            Variance?.Initialize(deriv);

            // One roll per spec, kept with what the spec captures. Rolled on every evaluation, a modifier re-rolled
            // whenever its attribute recomputed. Initialize can run twice for one spec
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
            return Variance?.Vary(baseValue, roll, deriv) ?? baseValue;
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        // Its base value is its values per level, through its behaviours, so it reads them all
        public override EScalerParts Parts => EScalerParts.All;

        /// <summary>
        /// Gets the minimum possible value at current level (for UI display).
        /// </summary>
        public float GetMinValue(IAttributeImpactCarrier spec)
        {
            float baseValue = EvaluateFromSpec(spec);
            return Variance?.Lowest(baseValue, spec) ?? baseValue;
        }

        /// <summary>
        /// Gets the maximum possible value at current level (for UI display).
        /// </summary>
        public float GetMaxValue(IAttributeImpactCarrier spec)
        {
            float baseValue = EvaluateFromSpec(spec);
            return Variance?.Highest(baseValue, spec) ?? baseValue;
        }
    }

    /// <summary>
    /// How a Randomized scaler varies its value with its roll, from 0 to 1: by a share of it, by an amount, between two
    /// multipliers, or a way of your own, as dice or a bell curve, a [Serializable] class made from this one, which is
    /// offered beside them. Its numbers are read for the carrier with the variance as their owner.
    /// </summary>
    [Serializable]
    public abstract class AbstractVariance
    {
        /// <summary>The value varied by the roll, from 0 to 1, for the carrier it's read for. The value as it is here.</summary>
        public virtual float Vary(float value, float roll, IAttributeImpactCarrier carrier) => value;

        /// <summary>The lowest the value can vary to for the carrier, as a tooltip shows a range. The value here.</summary>
        public virtual float Lowest(float value, IAttributeImpactCarrier carrier) => value;

        /// <summary>The highest the value can vary to for the carrier. The value here.</summary>
        public virtual float Highest(float value, IAttributeImpactCarrier carrier) => value;

        /// <summary>Readies its numbers for a carrier, as its scaler readies its own. Nothing here.</summary>
        public virtual void Initialize(IAttributeImpactCarrier carrier) { }

        /// <summary>Its numbers, each with its role, as a number's explanation lists them. None here.</summary>
        public virtual IEnumerable<(string Role, ScalableFloat Number)> Numbers() { yield break; }

        /// <summary>A number of its own, read for the carrier with this variance as its owner; the fallback where it's none.</summary>
        protected float Read(ScalableFloat number, IAttributeImpactCarrier carrier, float fallback) =>
            number?.Evaluate(NumberContext.Of(carrier), this) ?? fallback;
    }

    /// <summary>Up or down by a share of the value, as 0.1 for 10% either way.</summary>
    [Serializable]
    [ForgeMenu(Name = "Percentage", Description = "Up or down by a share of the value, as 0.1 for 10% either way.")]
    public class PercentageVariance : AbstractVariance
    {
        [Tooltip("The share of the value it varies by either way, 0.1 for 10%")]
        [NumberMin(0f)] public ScalableFloat Share = 0.1f;

        public override float Vary(float value, float roll, IAttributeImpactCarrier carrier)
        {
            float range = value * Read(Share, carrier, 0f);
            return value + Mathf.Lerp(-range, range, roll);
        }

        public override float Lowest(float value, IAttributeImpactCarrier carrier) => value * (1f - Read(Share, carrier, 0f));

        public override float Highest(float value, IAttributeImpactCarrier carrier) => value * (1f + Read(Share, carrier, 0f));

        public override void Initialize(IAttributeImpactCarrier carrier) => Share?.Initialize(carrier);

        public override IEnumerable<(string Role, ScalableFloat Number)> Numbers() { yield return ("Share", Share); }
    }

    /// <summary>Up or down by an amount, as 5 for a hit of 20 landing from 15 to 25.</summary>
    [Serializable]
    [ForgeMenu(Name = "Flat", Description = "Up or down by an amount, as 5 for a hit of 20 landing from 15 to 25.")]
    public class FlatVariance : AbstractVariance
    {
        [Tooltip("The amount it varies by either way")]
        [NumberMin(0f)] public ScalableFloat Amount = 1f;

        public override float Vary(float value, float roll, IAttributeImpactCarrier carrier)
        {
            float amount = Read(Amount, carrier, 0f);
            return value + Mathf.Lerp(-amount, amount, roll);
        }

        public override float Lowest(float value, IAttributeImpactCarrier carrier) => value - Read(Amount, carrier, 0f);

        public override float Highest(float value, IAttributeImpactCarrier carrier) => value + Read(Amount, carrier, 0f);

        public override void Initialize(IAttributeImpactCarrier carrier) => Amount?.Initialize(carrier);

        public override IEnumerable<(string Role, ScalableFloat Number)> Numbers() { yield return ("Amount", Amount); }
    }

    /// <summary>The value times a multiplier between its Min and its Max, as 0.9 to 1.1.</summary>
    [Serializable]
    [ForgeMenu(Name = "Multiplier", Description = "The value times a multiplier between its Min and its Max, as 0.9 to 1.1.")]
    public class MultiplierVariance : AbstractVariance
    {
        [Tooltip("The lowest multiplier")]
        public ScalableFloat Min = 0.9f;

        [Tooltip("The highest multiplier")]
        public ScalableFloat Max = 1.1f;

        public override float Vary(float value, float roll, IAttributeImpactCarrier carrier) =>
            value * Mathf.Lerp(Read(Min, carrier, 1f), Read(Max, carrier, 1f), roll);

        public override float Lowest(float value, IAttributeImpactCarrier carrier) => value * Read(Min, carrier, 1f);

        public override float Highest(float value, IAttributeImpactCarrier carrier) => value * Read(Max, carrier, 1f);

        public override void Initialize(IAttributeImpactCarrier carrier)
        {
            Min?.Initialize(carrier);
            Max?.Initialize(carrier);
        }

        public override IEnumerable<(string Role, ScalableFloat Number)> Numbers()
        {
            yield return ("Min", Min);
            yield return ("Max", Max);
        }
    }
}
