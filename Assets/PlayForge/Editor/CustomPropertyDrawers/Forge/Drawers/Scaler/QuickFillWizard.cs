using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Quick Fill, from a scaler field's menu (ED-63, Docs/EDITOR_STYLE.md): what it fills, the scaler's levels or its
    /// points; a pattern and its values; rounding; and a preview of the values as the scaler's table shows them, with their
    /// curve. Apply writes them, and the curve a spread scaler reads, as one Undo step. It drew a look of its own, a purple
    /// bold title, a green Apply, 10 px hints and tinted boxes, from styles set in C#, with its fields outside the label
    /// column, and wrote through a property it kept from when it opened.
    /// </summary>
    public class QuickFillWizard : EditorWindow
    {
        /// <summary>The shape the values take from the first to the last.</summary>
        public enum EFillPattern
        {
            [InspectorName("Same Value")] Constant,
            [InspectorName("Straight Line")] Linear,
            [InspectorName("Slow Then Fast")] Exponential,
            [InspectorName("Fast Then Slow")] Logarithmic,
            Steps,
            [InspectorName("Add Each Level")] Additive,
            [InspectorName("Multiply Each Level")] Multiplicative,
            [InspectorName("Ease In and Out")] Smooth,
            [InspectorName("Custom Curve")] Curve,
        }

        /// <summary>How the values are rounded once made.</summary>
        public enum ERounding
        {
            None,
            [InspectorName("Whole Numbers")] WholeNumbers,
            [InspectorName("Nearest Step")] ToStep,
        }

        private const string Hidden = "pf-hidden";

        // The scaler, found again by its path whenever it's read or written
        private Object[] targets;
        private string path;
        private Action onApply;

        private EFillPattern pattern = EFillPattern.Linear;
        private float from = 1f;
        private float to = 10f;
        private float strength = 2f;
        private int steps = 4;
        private float add = 1f;
        private float multiply = 1.1f;
        private AnimationCurve shape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private ERounding rounding = ERounding.None;
        private float step = 1f;

        // How many values it fills: the levels or points the scaler's table shows
        private int _levelCount;
        private bool spread;
        private bool ownCount;
        private string fills = "";
        private EScalerInterpolation between = EScalerInterpolation.Linear;

        private VisualElement previewTable;
        private ForgeCurvePreview curve;

        public static void Show(SerializedProperty prop, Action onApply)
        {
            var window = GetWindow<QuickFillWizard>(true, "Quick Fill");
            window.targets = prop.serializedObject.targetObjects;
            window.path = prop.propertyPath;
            window.onApply = onApply;
            window.minSize = new Vector2(400f, 440f);
            window.Read(prop);
            window.Rebuild();
            window.Show();
        }

        // What it fills, and values to start from: the scaler's first and last
        private void Read(SerializedProperty scaler)
        {
            var values = ScalerFields.ValuesOf(scaler);
            if (values.Length > 0)
            {
                from = values[0];
                to = values[values.Length - 1];
            }
            spread = scaler.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels))?.boolValue ?? false;
            var interpolation = scaler.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
            if (interpolation != null) between = (EScalerInterpolation)interpolation.intValue;
            var config = scaler.FindPropertyRelative(nameof(AbstractScaler.Configuration));
            var range = ScalerFields.LevelsOf(scaler.serializedObject);
            bool follows = !spread && range.Known && config != null && (ELevelConfig)config.intValue == ELevelConfig.LockToLevelProvider;

            // It fills the levels the scaler's table shows: its level provider's, while it follows one, rather than the
            // values it has (ED-53); a spread scaler's points; or its own levels
            int shown = ScalerFields.LevelsShown(scaler);
            _levelCount = Mathf.Clamp(shown > 0 ? shown : values.Length > 0 ? values.Length : 4, 1, 100);
            ownCount = !follows;
            fills = spread ? Count(_levelCount, "point", "points")
                : follows ? $"{range.From}'s levels ({range.Text})"
                : $"Levels 1–{_levelCount}";
        }

        private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

        private void CreateGUI() => Rebuild();

        private void Rebuild()
        {
            var root = rootVisualElement;
            if (root == null) return;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            var fillsRow = ImpactFields.ValueRow("Fills", "Fills", spread
                ? "A spread scaler's points, evenly along its levels"
                : "The levels its table shows");
            fillsRow.SetValueWithoutNotify(fills);
            root.Add(fillsRow);

            // A count of its own: a spread scaler's points, or the levels of a scaler that doesn't follow its provider
            var count = new IntegerField(spread ? "Points" : "Levels") { name = "Count", value = _levelCount, isDelayed = true };
            count.AddToClassList(IntegerField.alignedFieldUssClassName);
            count.EnableInClassList(Hidden, !ownCount);
            count.RegisterValueChangedCallback(change =>
            {
                _levelCount = Mathf.Clamp(change.newValue, 1, 100);
                count.SetValueWithoutNotify(_levelCount);
                fills = spread ? Count(_levelCount, "point", "points") : $"Levels 1–{_levelCount}";
                fillsRow.SetValueWithoutNotify(fills);
                ShowPreview();
            });
            root.Add(count);

            var patternField = new EnumField("Pattern", pattern) { name = "Pattern", tooltip = "The shape the values take, from the first to the last" };
            patternField.AddToClassList(EnumField.alignedFieldUssClassName);
            root.Add(patternField);

            // The pattern's values, each under it, as a field that depends on another is
            var parts = new VisualElement { name = "Parts" };
            root.Add(parts);
            var fromField = Number("From", "From", from, "The first value", value => from = value);
            var toField = Number("To", "To", to, "The last value", value => to = value);
            var strengthField = Number("Strength", "Strength", strength, "How strongly it bends: 1 is a straight line", value => strength = Mathf.Clamp(value, 0.1f, 10f));
            var stepsField = new IntegerField("Steps") { name = "Steps", value = steps, tooltip = "How many equal steps it climbs in" };
            stepsField.AddToClassList(IntegerField.alignedFieldUssClassName);
            stepsField.AddToClassList("pf-dependent");
            stepsField.RegisterValueChangedCallback(change =>
            {
                steps = Mathf.Clamp(change.newValue, 1, 100);
                ShowPreview();
            });
            var addField = Number("Add", "Add", add, "What each level adds to the one before", value => add = value);
            var multiplyField = Number("Multiply", "Multiply", multiply, "What each level multiplies the one before by", value => multiply = value);
            var shapeField = new CurveField("Shape") { name = "Shape", value = shape, tooltip = "The shape from the first value to the last, across 0 to 1" };
            shapeField.AddToClassList(CurveField.alignedFieldUssClassName);
            shapeField.AddToClassList("pf-dependent");
            shapeField.RegisterValueChangedCallback(change =>
            {
                shape = change.newValue ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                ShowPreview();
            });
            foreach (var part in new VisualElement[] { fromField, toField, strengthField, stepsField, addField, multiplyField, shapeField })
                parts.Add(part);

            var roundingField = new EnumField("Rounding", rounding) { name = "Rounding", tooltip = "How each value is rounded once made" };
            roundingField.AddToClassList(EnumField.alignedFieldUssClassName);
            root.Add(roundingField);
            var stepField = Number("Step", "Step", step, "The step each value rounds to, as 5", value => step = value);
            root.Add(stepField);

            // The values as the scaler's table shows them, with their curve
            var preview = new VisualElement { name = "Preview" };
            preview.AddToClassList("pf-scaler-table");
            previewTable = new VisualElement { name = "Grid" };
            preview.Add(previewTable);
            curve = new ForgeCurvePreview { name = "Curve" };
            preview.Add(curve);
            root.Add(preview);

            var footer = new VisualElement();
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(Apply) { name = "Apply", text = "Apply" });
            root.Add(footer);

            // Each pattern shows the values it reads
            void ShowParts()
            {
                bool ranged = pattern is not (EFillPattern.Constant or EFillPattern.Additive or EFillPattern.Multiplicative);
                fromField.label = pattern == EFillPattern.Constant ? "Value" : "From";
                toField.EnableInClassList(Hidden, !ranged);
                strengthField.EnableInClassList(Hidden, pattern is not (EFillPattern.Exponential or EFillPattern.Logarithmic));
                stepsField.EnableInClassList(Hidden, pattern != EFillPattern.Steps);
                addField.EnableInClassList(Hidden, pattern != EFillPattern.Additive);
                multiplyField.EnableInClassList(Hidden, pattern != EFillPattern.Multiplicative);
                shapeField.EnableInClassList(Hidden, pattern != EFillPattern.Curve);
                stepField.EnableInClassList(Hidden, rounding != ERounding.ToStep);
            }
            patternField.RegisterValueChangedCallback(change =>
            {
                if (change.newValue is EFillPattern chosen) pattern = chosen;
                ShowParts();
                ShowPreview();
            });
            roundingField.RegisterValueChangedCallback(change =>
            {
                if (change.newValue is ERounding chosen) rounding = chosen;
                ShowParts();
                ShowPreview();
            });
            ShowParts();
            ShowPreview();
        }

        // A number the pattern reads, under the pattern
        private FloatField Number(string label, string name, float value, string tooltip, Action<float> set)
        {
            var field = new FloatField(label) { name = name, value = value, tooltip = tooltip };
            field.AddToClassList(FloatField.alignedFieldUssClassName);
            field.AddToClassList("pf-dependent");
            field.RegisterValueChangedCallback(change =>
            {
                set(change.newValue);
                ShowPreview();
            });
            return field;
        }

        private float[] Values() => Fill(pattern, _levelCount, from, to, strength, steps, add, multiply, shape, rounding, step);

        // The preview: a row of levels, or points, over the values, ten to a row, and the curve beside them
        private void ShowPreview()
        {
            if (previewTable == null) return;
            previewTable.Clear();
            var values = Values();
            for (int start = 0; start < values.Length; start += 10)
            {
                var heads = new VisualElement();
                heads.AddToClassList("pf-scaler-table__levels");
                var row = new VisualElement();
                row.AddToClassList("pf-scaler-table__values");
                var head = new Label(spread ? "Point" : "Level");
                head.AddToClassList("pf-scaler-table__head");
                heads.Add(head);
                var valueHead = new Label("Value");
                valueHead.AddToClassList("pf-scaler-table__head");
                row.Add(valueHead);
                for (int i = start; i < Mathf.Min(start + 10, values.Length); i++)
                {
                    var at = new Label((i + 1).ToString(CultureInfo.InvariantCulture)) { name = $"PreviewAt{i + 1}" };
                    at.AddToClassList("pf-scaler-table__cell");
                    heads.Add(at);
                    var value = new Label(ScalerFields.Number(values[i])) { name = $"Preview{i + 1}" };
                    value.AddToClassList("pf-scaler-table__cell");
                    value.AddToClassList("pf-scaler-table__read");
                    row.Add(value);
                }
                previewTable.Add(heads);
                previewTable.Add(row);
            }
            // A spread scaler's curve passes between its points as it reads them; a table per level steps
            curve?.Show(values, between: spread ? between : (EScalerInterpolation?)null);
        }

        /// <summary>
        /// The values a pattern makes, as many as asked, then rounded: the same value; a straight line, a curve that starts
        /// slow or fast, or eases in and out, from one value to another; even steps between them; a value that adds or
        /// multiplies at each level; or a drawn shape from one to the other.
        /// </summary>
        internal static float[] Fill(EFillPattern pattern, int count, float from, float to, float strength, int steps, float add,
            float multiply, AnimationCurve shape, ERounding rounding, float step)
        {
            var values = new float[Mathf.Max(1, count)];
            for (int i = 0; i < values.Length; i++)
            {
                float t = values.Length > 1 ? (float)i / (values.Length - 1) : 0f;
                float value = pattern switch
                {
                    EFillPattern.Constant => from,
                    EFillPattern.Linear => Mathf.Lerp(from, to, t),
                    EFillPattern.Exponential => Mathf.Lerp(from, to, Mathf.Pow(t, strength)),
                    EFillPattern.Logarithmic => Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, strength)),
                    EFillPattern.Steps => Mathf.Lerp(from, to, Mathf.Min(1f, Mathf.Floor(t * Mathf.Max(1, steps) + 0.0001f) / Mathf.Max(1, steps))),
                    EFillPattern.Additive => from + add * i,
                    EFillPattern.Multiplicative => from * Mathf.Pow(multiply, i),
                    EFillPattern.Smooth => Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)),
                    EFillPattern.Curve => Mathf.LerpUnclamped(from, to, shape != null ? shape.Evaluate(t) : t),
                    _ => from,
                };
                values[i] = rounding switch
                {
                    ERounding.WholeNumbers => Mathf.Round(value),
                    ERounding.ToStep when step > 0f => Mathf.Round(value / step) * step,
                    _ => value,
                };
            }
            return values;
        }

        // Writes the values into each selected scaler, and the curve a spread one reads, as one Undo step, through the
        // scaler found again by its path
        internal void Apply()
        {
            if (targets == null || targets.Length == 0 || targets.Any(target => target == null) || string.IsNullOrEmpty(path))
            {
                Close();
                return;
            }
            var serialized = new SerializedObject(targets);
            var scaler = serialized.FindProperty(path);
            var valuesProp = scaler?.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            if (valuesProp == null)
            {
                Close();
                return;
            }
            var values = Values();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Quick Fill");
            EachAsset.Write(valuesProp, own =>
            {
                own.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) own.GetArrayElementAtIndex(i).floatValue = values[i];
            });
            ScalerFields.KeepCurve(scaler);
            Undo.CollapseUndoOperations(group);
            onApply?.Invoke();
            Close();
        }
    }
}
