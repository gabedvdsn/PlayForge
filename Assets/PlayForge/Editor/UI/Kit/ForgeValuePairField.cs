using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute value as a field shows it (ED-49): Unity's field, its label in the label column, then its Current and
    /// Base side by side, each a number field with its name before it, as Unity's own two-part fields show X and Y. The
    /// drawer binds each part to its value.
    /// </summary>
    internal sealed class ForgeValuePairField : BaseField<AttributeValue>
    {
        public const string FieldClass = "pf-value-pair";

        public FloatField Current { get; }
        public FloatField Base { get; }

        public ForgeValuePairField(string label) : base(label, new VisualElement())
        {
            AddToClassList(FieldClass);
            AddToClassList(alignedFieldUssClassName);
            var input = this.Q(className: inputUssClassName);
            input.AddToClassList("pf-value-pair__input");
            Current = new FloatField("Current") { name = "CurrentValue" };
            Current.AddToClassList("pf-value-pair__part");
            input.Add(Current);
            Base = new FloatField("Base") { name = "BaseValue" };
            Base.AddToClassList("pf-value-pair__part");
            Base.AddToClassList("pf-value-pair__part--second");
            input.Add(Base);
        }
    }
}
