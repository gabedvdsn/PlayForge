using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A row of fields as one field (ED-66): Unity's field, its label in the label column when it has one, then its parts
    /// side by side, each a field of its own with its name, if it has one, before it in secondary text, as Unity's
    /// two-part fields show X and Y. A tag query's count is its comparison and its number; a level threshold is its
    /// level, its value and its tier. The drawer binds each part.
    /// </summary>
    internal sealed class ForgePartsField : BaseField<string>
    {
        public const string FieldClass = "pf-parts-field";
        private const string PartClass = "pf-parts-field__part";
        private const string NextPartClass = "pf-parts-field__part--next";

        private readonly VisualElement input;

        public ForgePartsField(string label) : base(label, new VisualElement())
        {
            AddToClassList(FieldClass);
            AddToClassList(alignedFieldUssClassName);
            input = this.Q(className: inputUssClassName);
            input.AddToClassList("pf-parts-field__input");
        }

        /// <summary>Adds a part after those before it, and returns it.</summary>
        public T Part<T>(T part) where T : VisualElement
        {
            part.AddToClassList(PartClass);
            if (input.childCount > 0) part.AddToClassList(NextPartClass);
            input.Add(part);
            return part;
        }
    }
}
