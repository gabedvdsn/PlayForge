using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Mono Process's inspector, in Docs/EDITOR_STYLE.md's terms (ED-61): a Process section of Unity's fields, its
    /// lifecycle, its timing, how it's ordered as it steps, how it's made, and whether it follows its siblings, each
    /// choice saying what it does in its field's tooltip; then the component's own fields. Priority Method shows while
    /// the process steps, and Priority under it while the method is Manual. It drew a tinted box, a gray hint in 10 px
    /// under each field, set 50 ms after it drew, and a gray rule.
    /// <para>
    /// Every field is a PropertyField bound to its property, so a multiple selection edits each process, and a prefab
    /// instance's edits show as overrides. Without [CanEditMultipleObjects] the Inspector never offered one (ED-12).
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AbstractMonoProcess), true), CanEditMultipleObjects]
    public class AbstractMonoProcessEditor : UnityEditor.Editor
    {
        private const string Hidden = "pf-hidden";

        private static readonly Dictionary<int, string> Lifecycles = new()
        {
            [(int)EProcessLifecycle.SelfTerminating] = "Runs once, and ends as its run completes",
            [(int)EProcessLifecycle.RunThenWait] = "Runs once, then waits to be run again",
            [(int)EProcessLifecycle.RequiresControl] = "Waits from the start, to be run and ended by its owner",
            [(int)EProcessLifecycle.Synchronous] = "Ends from its update loop, with no async run",
        };

        private static readonly Dictionary<int, string> Timings = new()
        {
            [(int)EProcessStepTiming.None] = "Steps in no update loop",
            [(int)EProcessStepTiming.Update] = "Steps in Update, each frame",
            [(int)EProcessStepTiming.LateUpdate] = "Steps in LateUpdate, each frame",
            [(int)EProcessStepTiming.FixedUpdate] = "Steps in FixedUpdate, each physics step",
            [(int)EProcessStepTiming.UpdateAndLate] = "Steps in Update and LateUpdate, each frame",
            [(int)EProcessStepTiming.UpdateAndFixed] = "Steps in Update and FixedUpdate",
            [(int)EProcessStepTiming.LateAndFixed] = "Steps in LateUpdate and FixedUpdate",
            [(int)EProcessStepTiming.UpdateFixedAndLate] = "Steps in Update, LateUpdate and FixedUpdate",
        };

        private static readonly Dictionary<int, string> Methods = new()
        {
            [(int)EProcessStepPriorityMethod.Manual] = "By its Priority: a lower one steps earlier",
            [(int)EProcessStepPriorityMethod.First] = "Before the other processes",
            [(int)EProcessStepPriorityMethod.Last] = "After the other processes",
        };

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement { name = "MonoProcess" };
            // The style sheet lays out the section, wherever the inspector is drawn (ED-20)
            ForgeTheme.Apply(root);

            var section = new ForgeSection("Process", "Process", true);
            root.Add(section);
            var content = section.Content;

            var lifecycle = Field(content, nameof(AbstractMonoProcess.ProcessLifecycle), "Lifecycle");
            var timing = Field(content, nameof(AbstractMonoProcess.ProcessTiming), "Timing");
            var method = Field(content, nameof(AbstractMonoProcess.PriorityMethod), "Priority Method");
            var priority = Field(content, nameof(AbstractMonoProcess.ProcessStepPriority), "Priority");
            priority?.AddToClassList("pf-dependent");
            var instantiator = Field(content, nameof(AbstractMonoProcess.Instantiator), "Instantiator");
            if (instantiator != null) instantiator.tooltip = "Makes and destroys the process's object; with none, Object.Instantiate and Object.Destroy do";
            var cascade = Field(content, nameof(AbstractMonoProcess.ParticipateInSiblingCascade), null);
            if (cascade != null) cascade.tooltip = "Pauses and ends as its siblings do; off, a sibling's pause or end leaves it be";

            // Each choice's tooltip, and the priority's fields, follow the process
            void Show()
            {
                if (serializedObject.targetObject == null) return;
                var lifecycleProp = serializedObject.FindProperty(nameof(AbstractMonoProcess.ProcessLifecycle));
                var timingProp = serializedObject.FindProperty(nameof(AbstractMonoProcess.ProcessTiming));
                var methodProp = serializedObject.FindProperty(nameof(AbstractMonoProcess.PriorityMethod));
                if (lifecycle != null) lifecycle.tooltip = TipOf(lifecycleProp, Lifecycles, "How the process runs and ends");
                if (timing != null) timing.tooltip = TipOf(timingProp, Timings, "The update loops the process steps in");
                if (method != null) method.tooltip = TipOf(methodProp, Methods, "Where the process steps among the others");

                bool steps = timingProp == null || timingProp.hasMultipleDifferentValues || timingProp.intValue != (int)EProcessStepTiming.None;
                bool manual = methodProp != null && (methodProp.hasMultipleDifferentValues || methodProp.intValue == (int)EProcessStepPriorityMethod.Manual);
                method?.EnableInClassList(Hidden, !steps);
                priority?.EnableInClassList(Hidden, !steps || !manual);
            }
            // Each field says when its value changes, as the user picks one or an Undo puts one back
            lifecycle?.RegisterValueChangeCallback(_ => Show());
            timing?.RegisterValueChangeCallback(_ => Show());
            method?.RegisterValueChangeCallback(_ => Show());
            Show();

            // The component's own fields, after the process's
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        // A property's field, named by its property, or none where the process has no such property
        private PropertyField Field(VisualElement parent, string path, string label)
        {
            var property = serializedObject.FindProperty(path);
            if (property == null) return null;
            var field = label == null ? new PropertyField(property) : new PropertyField(property, label);
            field.name = path;
            parent.Add(field);
            return field;
        }

        // A choice's tooltip: its name and what it does, as "Run Then Wait: runs once, then waits to be run again"
        private static string TipOf(SerializedProperty choice, Dictionary<int, string> meanings, string field)
        {
            if (choice == null || choice.hasMultipleDifferentValues) return field;
            var names = choice.enumDisplayNames;
            var index = choice.enumValueIndex;
            var name = index >= 0 && index < names.Length ? names[index] : null;
            return meanings.TryGetValue(choice.intValue, out var meaning) && name != null
                ? $"{name}: {char.ToLowerInvariant(meaning[0])}{meaning.Substring(1)}"
                : field;
        }
    }
}
