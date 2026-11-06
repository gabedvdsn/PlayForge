using System;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A tag as a field shows it (ED-55, Docs/EDITOR_STYLE.md): Unity's field, its label in the label column, and the tag in
    /// a field's box, its parents in secondary text and its leaf in strong text, or its whole path in strong text while the
    /// full-path setting is on, then a popup's arrow. Pressing the box, or Enter on the field, opens its tag picker. Its
    /// value is the tag's name, which a multiple selection whose tags differ shows as mixed.
    /// </summary>
    internal sealed class ForgeTagField : BaseField<string>
    {
        public const string FieldClass = "pf-tag-field";
        public const string NoneClass = "pf-tag-field__leaf--none";

        private readonly Label parents;
        private readonly Label leaf;

        /// <summary>Sent as the field asks for its picker.</summary>
        public event Action<ForgeTagField> Opened;

        /// <summary>The box the tag shows in, which the picker opens under.</summary>
        public VisualElement Box { get; }

        public ForgeTagField(string label) : base(label, new VisualElement())
        {
            AddToClassList(FieldClass);
            Box = this.Q(className: inputUssClassName);
            Box.AddToClassList("pf-tag-field__box");
            parents = new Label { name = "Parents" };
            parents.AddToClassList("pf-tag-field__parents");
            Box.Add(parents);
            leaf = new Label { name = "Leaf" };
            leaf.AddToClassList("pf-tag-field__leaf");
            Box.Add(leaf);
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

        /// <summary>Shows a tag by its display name, or None.</summary>
        public void Show(Tag tag, bool fullPath)
        {
            SetValueWithoutNotify(tag.Name ?? "");
            var (before, last) = Parts(tag, fullPath);
            parents.text = before;
            parents.EnableInClassList("pf-tag-field__parents--empty", before.Length == 0);
            leaf.text = last;
            leaf.EnableInClassList(NoneClass, !tag.IsValid);
            Box.tooltip = tag.IsValid ? TagDisplaySettings.PathOf(tag) : "No tag";
        }

        /// <summary>
        /// A tag as a field reads it: its parents, as "Status.Debuff.", and its leaf, as "Burn"; or, with the full-path
        /// setting on, no parents and the whole path; or None.
        /// </summary>
        internal static (string Parents, string Leaf) Parts(Tag tag, bool fullPath)
        {
            var path = TagDisplaySettings.PathOf(tag);
            if (string.IsNullOrEmpty(path)) return ("", "None");
            int dot = path.LastIndexOf('.');
            return fullPath || dot < 0 ? ("", path) : (path.Substring(0, dot + 1), path.Substring(dot + 1));
        }

        protected override void UpdateMixedValueContent()
        {
            if (!showMixedValue) return;
            parents.text = "";
            parents.AddToClassList("pf-tag-field__parents--empty");
            leaf.text = "—";
            leaf.RemoveFromClassList(NoneClass);
        }
    }
}
