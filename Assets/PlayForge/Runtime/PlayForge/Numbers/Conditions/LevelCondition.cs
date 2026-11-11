using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Whether the level compares with a number (FEAT-29): the level itself, or how far up its range it is, from 0 at
    /// its least to 1 at its most.
    /// </summary>
    [Serializable]
    [GameplayCondition("Level", Reads = NumberReads.Level)]
    public class LevelCondition : GameplayCondition
    {
        [Tooltip("Off: the level itself. On: how far up its range it is, from 0 at its least to 1 at its most")]
        public bool Relative;

        [Tooltip("How the level compares with the value")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;

        [Tooltip("What the level is compared with")]
        public ScalableFloat Value = 1f;

        protected override bool Evaluate(in NumberContext context)
        {
            float level = Relative ? context.Level.Ratio : context.Level.CurrentValue;
            return NumberComparison.Holds(level, Comparison, Value?.Evaluate(in context, this) ?? 0f);
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => Value?.Initialize(carrier);

        public override string Describe() =>
            $"{(Relative ? "relative level" : "level")} {NumberComparison.Symbol(Comparison)} {NumberText.Of(Value)}";
    }
}
