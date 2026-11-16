using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A number kept between a floor and a ceiling: damage worked out from a target's missing health,
    /// capped. What it clamps and both bounds are numbers of any kind, read with the scaler as their owner, so its
    /// ceiling can be twice its floor, and its floor can scale by level.
    /// </summary>
    [Serializable]
    [NumberKind("Clamped")]
    [ForgeMenu("Scaler", Description = "A number kept between a floor and a ceiling.")]
    public class ClampedScaler : NumberKind
    {
        [Tooltip("The least it reads")]
        public ScalableFloat MinValue = 0f;

        [Tooltip("The most it reads")]
        public ScalableFloat MaxValue = 100f;

        [Tooltip("What it clamps")]
        public ScalableFloat Value = new(new SimpleScaler(), 1f, EMagnitudeOperation.UseScaler);

        protected override float Evaluate(in NumberContext context)
        {
            float value = Value?.Evaluate(in context, this) ?? 0f;
            float min = MinValue?.Evaluate(in context, this) ?? float.NegativeInfinity;
            float max = MaxValue?.Evaluate(in context, this) ?? float.PositiveInfinity;
            return Mathf.Clamp(value, min, max);
        }

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            Value?.Initialize(deriv);
            MinValue?.Initialize(deriv);
            MaxValue?.Initialize(deriv);
        }

        public override string Describe() =>
            $"{NumberText.Of(Value)}, between {NumberText.Of(MinValue)} and {NumberText.Of(MaxValue)}";

        // What it clamps, and its bounds
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            trace.Add(Value, context, this, "Value");
            trace.Add(MinValue, context, this, "Min");
            trace.Add(MaxValue, context, this, "Max");
        }
    }
}
