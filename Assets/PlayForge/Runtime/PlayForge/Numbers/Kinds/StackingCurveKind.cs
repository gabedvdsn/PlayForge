using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>How a stacking curve turns a count into its number (TW-9), with its a.</summary>
    public enum EStackingCurve
    {
        /// <summary>a × n: Soldier's Syringe's 15% a stack.</summary>
        Linear,

        /// <summary>1 − 1 / (1 + a × n): Tougher Times' block, 13% with one and 60% with ten, never 1.</summary>
        Hyperbolic,

        /// <summary>1 / (1 + a × n): what Dota's armor leaves of a physical hit, 1 / (1 + 0.06 × armor).</summary>
        Reciprocal,

        /// <summary>a / n, or 0 where n is 0: a wind-up an attack speed shortens, a / speed.</summary>
        Inverse,

        /// <summary>a to the n: a chance that falls by a share each stack, 0.9 to the n.</summary>
        Exponential,
    }

    /// <summary>
    /// A count through a stacking curve, then a number added (TW-9): an item's stacks, an effect's stacks or an attribute,
    /// read as its curve says. Risk of Rain's items stack this way, and Dota's armor, so a number that reads stacks or armor
    /// needs no worker of its own. Plus 1 makes a share a multiplier.
    /// </summary>
    [Serializable]
    [NumberKind("Stacking Curve", Category = "Scaler")]
    public class StackingCurveKind : NumberKind
    {
        [Tooltip("What it counts, n: an item's stacks, an effect's stacks, an attribute")]
        public ScalableFloat Count = 0f;

        [Tooltip("How the count turns into the number: Linear a × n, Hyperbolic 1 − 1 / (1 + a × n), Reciprocal " +
                 "1 / (1 + a × n), Inverse a / n, Exponential a to the n")]
        public EStackingCurve Curve = EStackingCurve.Linear;

        [Tooltip("The curve's a, read for each number")]
        public ScalableFloat Coefficient = 1f;

        [Tooltip("Added after the curve: 1 makes a share a multiplier")]
        public float Plus;

        /// <summary>The curve's value for a count <paramref name="n"/> and an <paramref name="a"/>, before Plus.</summary>
        public static float Of(EStackingCurve curve, float a, float n) => curve switch
        {
            EStackingCurve.Linear => a * n,
            EStackingCurve.Hyperbolic => Mathf.Approximately(1f + a * n, 0f) ? 0f : 1f - 1f / (1f + a * n),
            EStackingCurve.Reciprocal => Mathf.Approximately(1f + a * n, 0f) ? 0f : 1f / (1f + a * n),
            EStackingCurve.Inverse => Mathf.Approximately(n, 0f) ? 0f : a / n,
            EStackingCurve.Exponential => Mathf.Pow(a, n),
            _ => n,
        };

        protected override float Evaluate(in NumberContext context)
        {
            float n = Count?.Evaluate(in context, this) ?? 0f;
            float a = Coefficient?.Evaluate(in context, this) ?? 1f;
            return Of(Curve, a, n) + Plus;
        }

        public override void Initialize(IAttributeImpactCarrier carrier)
        {
            Count?.Initialize(carrier);
            Coefficient?.Initialize(carrier);
        }

        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            trace.Add(Count, context, this, "Count");
            trace.Add(Coefficient, context, this, "Coefficient");
        }

        public override string Describe()
        {
            string n = NumberText.Of(Count);
            string a = NumberText.Of(Coefficient);
            string curve = Curve switch
            {
                EStackingCurve.Linear => $"{a} × {n}",
                EStackingCurve.Hyperbolic => $"1 − 1 / (1 + {a} × {n})",
                EStackingCurve.Reciprocal => $"1 / (1 + {a} × {n})",
                EStackingCurve.Inverse => $"{a} / {n}",
                EStackingCurve.Exponential => $"{a} ^ {n}",
                _ => n,
            };
            return Plus == 0f ? curve : $"{curve} + {NumberText.Number(Plus)}";
        }
    }
}
