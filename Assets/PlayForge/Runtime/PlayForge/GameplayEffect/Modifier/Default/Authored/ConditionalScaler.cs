using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One number where a condition holds, and another where it doesn't (FEAT-29): "−0.1 × Target's Health (base) if
    /// Target's Health &lt; 0.3 × Target's Health (base), else 0". The condition is a class read against the number's
    /// context, so a project adds its own (<see cref="GameplayCondition"/>, Decision 43), and each branch is a number of
    /// any kind, read with the scaler as its owner. With no condition, it reads its False branch.
    /// </summary>
    [Serializable]
    [NumberKind("Conditional", Category = "Scaler")]
    public class ConditionalScaler : NumberKind
    {
        [Tooltip("The condition that chooses the branch")]
        [SerializeReference]
        public GameplayCondition When = new TagCondition();

        [Tooltip("What it reads where the condition holds")]
        public ScalableFloat TrueValue = 1f;

        [Tooltip("What it reads where the condition doesn't hold")]
        public ScalableFloat FalseValue = 0f;

        protected override float Evaluate(in NumberContext context)
        {
            var branch = When != null && When.Holds(in context) ? TrueValue : FalseValue;
            return branch?.Evaluate(in context, this) ?? 0f;
        }

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            When?.Initialize(deriv);
            TrueValue?.Initialize(deriv);
            FalseValue?.Initialize(deriv);
        }

        public override string Describe() =>
            $"{NumberText.Of(TrueValue)} if {NumberText.Of(When)}, else {NumberText.Of(FalseValue)}";

        // Its condition, and the branch it took (FEAT-32)
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            bool holds = When != null && trace.Add(When, context, "When").Holds;
            trace.Add(holds ? TrueValue : FalseValue, context, this, holds ? "True" : "False");
        }
    }
}
