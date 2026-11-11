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

        [Tooltip("Which retention groups' parts it reads: all of them, the whole value, unless it says (TW-3)")]
        public RetentionGroups Groups = new();

        [Tooltip("How the attribute compares with the value")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;

        [Tooltip("What the attribute is compared with: any number, as 0.3 × the target's base Health")]
        public ScalableFloat Value = 50f;

        public override NumberReads Reads => On == NumberSide.Caster ? NumberReads.Caster : NumberReads.Target;

        protected override bool Evaluate(in NumberContext context)
        {
            if (!context.TryGetAttribute(On, Attribute, Groups, out var value)) return false;
            float attribute = What == EAttributeTargetBinary.Base ? value.BaseValue : value.CurrentValue;
            return NumberComparison.Holds(attribute, Comparison, Value?.Evaluate(in context, this) ?? 0f);
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => Value?.Initialize(carrier);

        // The attribute's value, and the number it was compared with (FEAT-32)
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            string name = Attribute != null ? Attribute.GetName() : "attribute";
            string label = $"{(On == NumberSide.Caster ? "Caster's" : "Target's")} {name}{RetentionGroups.Describe(Groups)}{(What == EAttributeTargetBinary.Base ? " (base)" : "")}";
            if (context.TryGetAttribute(On, Attribute, Groups, out var value))
                trace.Add(label, What == EAttributeTargetBinary.Base ? value.BaseValue : value.CurrentValue, "Attribute");
            else trace.Add(label, 0f, "Attribute").Needing($"{(On == NumberSide.Caster ? "the caster's" : "the target's")} {name}");
            trace.Add(Value, context, this, "Value");
        }

        public override string Describe()
        {
            string side = On == NumberSide.Caster ? "Caster's" : "Target's";
            string name = Attribute != null ? Attribute.GetName() : "attribute";
            string what = What == EAttributeTargetBinary.Base ? " (base)" : "";
            return $"{side} {name}{RetentionGroups.Describe(Groups)}{what} {NumberComparison.Symbol(Comparison)} {NumberText.Of(Value)}";
        }
    }
}
