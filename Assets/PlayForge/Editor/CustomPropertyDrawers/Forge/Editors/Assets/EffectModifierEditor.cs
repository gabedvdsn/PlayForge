using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An Effect Modifier's inspector (TW-7): what it's for, its rule, the effects it picks and its changes, each of a
    /// kind with its fields, and a line that says them. Unity's own fields, as <c>Docs/EDITOR_STYLE.md</c> has it.
    /// </summary>
    [CustomEditor(typeof(EffectModifier))]
    public class EffectModifierEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var modifier = (EffectModifier)target;
            var root = new VisualElement { name = "EffectModifierRoot" };
            ForgeTheme.Apply(root);

            var description = new TextField("Description")
            {
                name = "Description", multiline = true, bindingPath = nameof(EffectModifier.Description),
                tooltip = "What the modifier is for",
            };
            description.AddToClassList(TextField.alignedFieldUssClassName);
            root.Add(description);

            var line = new Label(Describe(modifier.Rule)) { name = "ModifierLine", tooltip = "What it changes, of which effects" };
            line.style.whiteSpace = WhiteSpace.Normal;
            root.Add(line);

            var rule = new PropertyField(serializedObject.FindProperty(nameof(EffectModifier.Rule)), "Rule")
            {
                name = "Rule",
                tooltip = "The effects it picks, by tag, which way, while what holds, and what it changes of each",
            };
            rule.RegisterCallback<SerializedPropertyChangeEvent>(_ => line.text = Describe(modifier.Rule));
            root.Add(rule);
            return root;
        }

        /// <summary>A rule in a line: "Outgoing, effects tagged Status.Chill: Adds Mark as it lands, then × 2 on Health".</summary>
        public static string Describe(EffectModifierRule rule)
        {
            if (rule == null) return "No rule";
            string which = rule.EffectTags is { Count: > 0 }
                ? $"effects tagged {string.Join(", ", rule.EffectTags.Where(tag => tag.IsValid).Select(tag => tag.ToString()))}"
                : "every effect";
            var changes = rule.Changes?.Where(change => change != null).Select(change => change.Describe()).ToList();
            string what = changes is { Count: > 0 } ? string.Join(", then ", changes) : "no changes";
            return $"{rule.Direction}, {which}: {what}";
        }
    }

    /// <summary>A modifier's change: its kind, always of some kind, then its fields, as a worker is drawn (TW-7).</summary>
    [CustomPropertyDrawer(typeof(AbstractEffectChange), true)]
    public class EffectChangeDrawer : AbstractGenericDrawer<AbstractEffectChange>
    {
        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
