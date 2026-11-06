using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A choice among a few options shown side by side, as a scaler's Values, Per Level or Spread Over Levels (ED-54):
    /// Unity's field, its label in the label column, and a button for each option, the chosen one pressed. Its value is
    /// the chosen option's place. A mixed value presses none.
    /// </summary>
    internal sealed class ForgeSegmentedField : BaseField<int>
    {
        public const string FieldClass = "pf-segmented-field";

        private readonly List<Button> buttons = new();

        public ForgeSegmentedField(string label, params (string Text, string Tooltip)[] options) : base(label, new VisualElement())
        {
            AddToClassList(FieldClass);
            var input = this.Q(className: inputUssClassName);
            input.AddToClassList("pf-segmented");
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var button = new Button(() => value = index) { name = options[i].Text.Replace(" ", ""), text = options[i].Text, tooltip = options[i].Tooltip };
                buttons.Add(button);
                input.Add(button);
            }
            ShowChosen();
        }

        public override void SetValueWithoutNotify(int newValue)
        {
            base.SetValueWithoutNotify(newValue);
            ShowChosen();
        }

        protected override void UpdateMixedValueContent() => ShowChosen();

        private void ShowChosen()
        {
            for (int i = 0; i < buttons.Count; i++) buttons[i].EnableInClassList("pf-segmented__selected", !showMixedValue && i == value);
        }
    }
}
