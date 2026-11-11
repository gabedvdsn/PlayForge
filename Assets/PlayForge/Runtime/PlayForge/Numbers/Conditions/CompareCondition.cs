using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Whether one number compares with another (FEAT-29): "Caster's Health &gt; 2 × Target's Health". Each is any kind,
    /// read with the condition as its owner.
    /// </summary>
    [Serializable]
    [GameplayCondition("Compare Numbers")]
    public class CompareCondition : GameplayCondition
    {
        [Tooltip("The number on the left")]
        public ScalableFloat Left = 0f;

        [Tooltip("How the left compares with the right")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;

        [Tooltip("The number on the right")]
        public ScalableFloat Right = 0f;

        protected override bool Evaluate(in NumberContext context) =>
            NumberComparison.Holds(Left?.Evaluate(in context, this) ?? 0f, Comparison, Right?.Evaluate(in context, this) ?? 0f);

        public override void Initialize(IAttributeImpactCarrier carrier)
        {
            Left?.Initialize(carrier);
            Right?.Initialize(carrier);
        }

        // The two numbers it compared (FEAT-32)
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            trace.Add(Left, context, this, "Left");
            trace.Add(Right, context, this, "Right");
        }

        public override string Describe() =>
            $"{NumberText.Of(Left)} {NumberComparison.Symbol(Comparison)} {NumberText.Of(Right)}";
    }
}
