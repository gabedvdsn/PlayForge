using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Whether an attribute of the caster or the target compares with a number (FEAT-29): "Target's Health &lt; 0.3 ×
    /// Target's Health (base)". The number is any kind, read with the condition as its owner. An entity without the
    /// attribute doesn't hold.
    /// </summary>
    [Serializable]
    [GameplayCondition("Attribute")]
    public class AttributeCondition : GameplayCondition
    {
        [Tooltip("Whose attribute it reads")]
        public NumberSide On = NumberSide.Caster;

        [Tooltip("The attribute it compares")]
        public Attribute Attribute;

        [Tooltip("Its current value, or its base")]
        public EAttributeTargetBinary What = EAttributeTargetBinary.Current;

        [Tooltip("How the attribute compares with the value")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;

        [Tooltip("What the attribute is compared with: any number, as 0.3 × the target's base Health")]
        public ScalableFloat Value = 50f;

        public override NumberReads Reads => On == NumberSide.Caster ? NumberReads.Caster : NumberReads.Target;

        protected override bool Evaluate(in NumberContext context)
        {
            if (!context.TryGetAttribute(On, Attribute, out var value)) return false;
            float attribute = What == EAttributeTargetBinary.Base ? value.BaseValue : value.CurrentValue;
            return NumberComparison.Holds(attribute, Comparison, Value?.Evaluate(in context, this) ?? 0f);
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => Value?.Initialize(carrier);

        public override string Describe()
        {
            string side = On == NumberSide.Caster ? "Caster's" : "Target's";
            string name = Attribute != null ? Attribute.GetName() : "attribute";
            string what = What == EAttributeTargetBinary.Base ? " (base)" : "";
            return $"{side} {name}{what} {NumberComparison.Symbol(Comparison)} {NumberText.Of(Value)}";
        }
    }
}
