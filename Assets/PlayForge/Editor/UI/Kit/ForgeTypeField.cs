using System;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The kind a [SerializeReference] field holds, as a field shows it (ED-56, Docs/EDITOR_STYLE.md): Unity's field, its
    /// label in the label column, and the kind's name in a field's box, in strong text, or None in secondary text, then a
    /// popup's arrow. It takes the tag field's box, so the two read alike. Pressing the box, or Enter on the field, opens
    /// its picker. Its value is the kind's name, which a multiple selection whose kinds differ shows as mixed.
    /// </summary>
    internal sealed class ForgeTypeField : BaseField<string>
    {
        public const string FieldClass = "pf-type-field";

        private readonly Label kind;

        /// <summary>Sent as the field asks for its picker.</summary>
        public event Action<ForgeTypeField> Opened;

        /// <summary>The box the kind shows in, which the picker opens under.</summary>
        public VisualElement Box { get; }

        public ForgeTypeField(string label) : base(label, new VisualElement())
        {
            name = "TypeField";
            AddToClassList(FieldClass);
            Box = this.Q(className: inputUssClassName);
            Box.AddToClassList("pf-tag-field__box");
            kind = new Label { name = "TypeName" };
            kind.AddToClassList("pf-tag-field__leaf");
            Box.Add(kind);
            var space = new VisualElement();
            space.AddToClassList("pf-tag-field__space");
            Box.Add(space);
            // A popup's arrow: Unity's own draws only inside its popup field
            var arrow = new ForgeGlyph(ForgeGlyphShape.ArrowDown) { name = "Arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("pf-tag-field__arrow");
            Box.Add(arrow);
            Box.AddManipulator(new Clickable(Open));
            RegisterCallback<NavigationSubmitEvent>(_ => Open());
        }

        private void Open()
        {
            if (enabledInHierarchy) Opened?.Invoke(this);
        }

        /// <summary>Shows a kind by its name, or None.</summary>
        public void Show(string name, bool none, string tip)
        {
            SetValueWithoutNotify(name ?? "");
            kind.text = none ? "None" : name;
            kind.EnableInClassList("pf-tag-field__leaf--none", none);
            Box.tooltip = tip ?? "";
        }

        protected override void UpdateMixedValueContent()
        {
            if (!showMixedValue) return;
            kind.text = "—";
            kind.RemoveFromClassList("pf-tag-field__leaf--none");
        }
    }
}
