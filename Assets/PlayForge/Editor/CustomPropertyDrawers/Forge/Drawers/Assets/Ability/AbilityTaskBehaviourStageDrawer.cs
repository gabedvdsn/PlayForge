using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability's stage, in Docs/EDITOR_STYLE.md's terms (ED-64): a foldout row named by its place, as "Stage 1", with
    /// Critical on its right while a task commits the ability to it; folded, one line of what it holds, as "2 tasks · All ·
    /// Applies usage effects"; open, its completion policy, its usage effects, its condition, what cancels it and what it
    /// blocks, then its tasks in Unity's reorderable list, where a new task starts empty. The stages' own list removes a
    /// stage.
    /// <para>
    /// It drew a colored badge for the stage's number, its policy, its usage effects, its task count and a critical task,
    /// ▶ and ▼ as text, a delete button, and a list of its own with ▲, ▼ and × buttons, from 163 styles set in C#; and it
    /// grew that list as an array, which gave a new task the last one's object, so the two were one.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityTaskBehaviourStage))]
    public class AbilityTaskBehaviourStageDrawer : PropertyDrawer
    {
        public const string CollapsedClass = "pf-element--collapsed";
        private const string Hidden = "pf-hidden";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "StageRoot" };
            // The style sheet lays out the stage, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-element");

            // The stage, found again by its path, as the stages' list moves and an Undo puts objects back (ED-49)
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            SerializedProperty Fresh()
            {
                try
                {
                    return serialized.targetObject != null ? serialized.FindProperty(path) : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // ─── The foldout row: the arrow, the stage's place, and Critical ──
            var header = new VisualElement { name = "StageHeader" };
            header.AddToClassList("pf-element__header");
            root.Add(header);
            var arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, "Collapse", null) { name = "CollapseBtn" };
            arrow.AddToClassList("pf-element__arrow");
            header.Add(arrow);
            var title = new Label($"Stage {IndexOf(path) + 1}") { name = "StageTitle" };
            title.AddToClassList("pf-stage__title");
            header.Add(title);
            var critical = new Label("Critical")
            {
                name = "StageCritical",
                tooltip = "A task in the stage is a critical section: nothing cancels the ability while the stage runs",
            };
            critical.AddToClassList("pf-element__delivery");
            header.Add(critical);

            // The one line a folded stage shows
            var summary = new Label { name = "Summary" };
            summary.AddToClassList("pf-element__summary");
            root.Add(summary);

            var content = new VisualElement { name = "StageContent" };
            content.AddToClassList("pf-element__content");
            root.Add(content);

            content.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.StagePolicy)), "Completion")
            {
                name = "StagePolicy",
                tooltip = "When the stage is complete: as all its tasks end, as any does, or as its policy says",
            });

            var usage = new Toggle("Apply Usage Effects")
            {
                name = "ApplyUsageEffects",
                tooltip = "Apply the ability's cost and cooldown in this stage. With no stage set to, they apply as the ability starts, after its targeting (TW-17)",
            };
            usage.AddToClassList(Toggle.alignedFieldUssClassName);
            usage.BindProperty(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.ApplyUsageEffects)));
            content.Add(usage);
            var atStart = new Toggle("At Stage Start")
            {
                name = "ApplyUsageAtStageStart",
                tooltip = "Apply them as the stage starts; off, as it ends",
            };
            atStart.AddToClassList(Toggle.alignedFieldUssClassName);
            atStart.AddToClassList("pf-dependent");
            atStart.BindProperty(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.ApplyUsageAtStageStart)));
            content.Add(atStart);

            content.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Condition)), "Condition")
            {
                name = "Condition",
            });
            content.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.CancelledBy)), "Cancelled By")
            {
                name = "CancelledBy",
                tooltip = "The abilities that cancel this one as they activate while this stage runs; with none, the ability's own",
            });
            content.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Blocks)), "Blocks")
            {
                name = "Blocks",
                tooltip = "The abilities that can't activate while this stage runs; with none, the ability's own",
            });

            // The tasks, in Unity's reorderable list. A new task starts empty, for its kind to be picked
            var tasks = new PropertyField(property.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Tasks)), "Tasks") { name = "Tasks" };
            content.Add(tasks);
            ForgeLists.AddsEmpty(tasks, serialized, $"{path}.{nameof(AbilityTaskBehaviourStage.Tasks)}");

            // ─── Behaviour ─────────────────────────────────────────────────────
            void Show()
            {
                var stage = Fresh();
                if (stage == null) return;
                var tasksProp = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Tasks));
                var usageProp = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.ApplyUsageEffects));
                summary.text = SummaryOf(stage);
                critical.EnableInClassList(Hidden, !HasCritical(tasksProp));
                atStart.EnableInClassList(Hidden, usageProp == null || !usageProp.boolValue || usageProp.hasMultipleDifferentValues);
            }

            void SetFolded(bool folded)
            {
                root.EnableInClassList(CollapsedClass, folded);
                arrow.Glyph.Shape = folded ? ForgeGlyphShape.ArrowRight : ForgeGlyphShape.ArrowDown;
                arrow.tooltip = folded ? "Expand" : "Collapse";
            }

            arrow.clicked += () =>
            {
                var stage = Fresh();
                if (stage == null) return;
                bool folded = !DrawerCollapseState.IsCollapsed(stage, collapsedByDefault: false);
                DrawerCollapseState.SetCollapsed(stage, folded);
                SetFolded(folded);
            };
            root.TrackSerializedObjectValue(serialized, _ => Show());
            SetFolded(DrawerCollapseState.IsCollapsed(property, collapsedByDefault: false));
            Show();
            return root;
        }

        /// <summary>
        /// A folded stage's line: its tasks, its completion policy, when it applies usage effects, and the tag it runs on,
        /// as "2 tasks · All · Applies usage effects".
        /// </summary>
        internal static string SummaryOf(SerializedProperty stage)
        {
            var parts = new List<string>();
            var tasks = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Tasks));
            int count = tasks?.arraySize ?? 0;
            parts.Add(count == 1 ? "1 task" : $"{count} tasks");
            var policy = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.StagePolicy))?.managedReferenceValue;
            if (policy != null) parts.Add(PolicyName(policy.GetType()));
            var usage = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.ApplyUsageEffects));
            if (usage != null && usage.boolValue)
            {
                var atStart = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.ApplyUsageAtStageStart));
                parts.Add(atStart == null || atStart.boolValue ? "Applies usage effects" : "Applies usage effects as it ends");
            }
            var condition = stage.FindPropertyRelative(nameof(AbilityTaskBehaviourStage.Condition));
            var tag = condition?.FindPropertyRelative(nameof(AbilityStageCondition.Tag));
            if (tag != null)
            {
                var name = TagDisplaySettings.PathOf(tag);
                if (!string.IsNullOrEmpty(name))
                {
                    bool missing = condition.FindPropertyRelative(nameof(AbilityStageCondition.WhenMissing))?.boolValue ?? false;
                    parts.Add(missing ? $"Without {name}" : $"With {name}");
                }
            }
            return string.Join(" · ", parts);
        }

        // A policy's kind as the line names it, as "All" for AllProxyStagePolicy
        private static string PolicyName(Type type) =>
            ObjectNames.NicifyVariableName(type.Name.Replace("ProxyStagePolicy", "").Replace("Policy", ""));

        // Whether a task in the list is a critical section
        private static bool HasCritical(SerializedProperty tasks)
        {
            if (tasks == null || !tasks.isArray) return false;
            for (int i = 0; i < tasks.arraySize; i++)
                if (tasks.GetArrayElementAtIndex(i).managedReferenceValue is AbstractAbilityTask { IsCriticalSection: true })
                    return true;
            return false;
        }

        // A stage's place in its list, from its path, as 0 for "...Stages.Array.data[0]"
        private static int IndexOf(string path)
        {
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            return open >= 0 && close > open && int.TryParse(path.Substring(open + 1, close - open - 1), out int index) ? index : 0;
        }
    }
}
