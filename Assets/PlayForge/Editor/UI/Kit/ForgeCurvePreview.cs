using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A read-only line of a scaler's values, one point per level, as every view of a scaler shows beside it (Decision
    /// 28). The style sheet gives its line and guides their colors, which it reads as it draws: the line takes the
    /// element's color, and the frame its border color. A second series, such as an import's source, draws in the
    /// secondary color.
    /// </summary>
    internal sealed class ForgeCurvePreview : VisualElement
    {
        public const string PreviewClass = "pf-curve";
        public const string SecondClass = "pf-curve__second";

        private float[] values = Array.Empty<float>();
        private float[] second;
        private readonly VisualElement secondColor;

        public ForgeCurvePreview()
        {
            AddToClassList(PreviewClass);
            // An element of its own, so the style sheet can give the second series a color the line reads
            secondColor = new VisualElement { name = "SecondColor", pickingMode = PickingMode.Ignore };
            secondColor.AddToClassList(SecondClass);
            Add(secondColor);
            generateVisualContent += Draw;
        }

        /// <summary>Shows these values, one per level, and a second series beside them, or none.</summary>
        public void Show(float[] shown, float[] beside = null)
        {
            values = shown ?? Array.Empty<float>();
            second = beside;
            tooltip = Describe(values);
            MarkDirtyRepaint();
        }

        /// <summary>A series as a tooltip reads it, as in "6 at level 1, rising to 8 at level 5".</summary>
        internal static string Describe(float[] series)
        {
            if (series == null || series.Length == 0) return "No values";
            string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);
            if (series.Length == 1 || series.All(value => Mathf.Approximately(value, series[0])))
                return $"{Number(series[0])} at every level";
            var last = series[series.Length - 1];
            var way = last > series[0] ? "rising" : last < series[0] ? "falling" : "ending";
            return $"{Number(series[0])} at level 1, {way} to {Number(last)} at level {series.Length}";
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 4 || rect.height < 4) return;
            var painter = context.painter2D;

            // The frame's guides: the bottom and left edges
            painter.strokeColor = resolvedStyle.borderBottomColor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, rect.height - 0.5f));
            painter.LineTo(new Vector2(rect.width, rect.height - 0.5f));
            painter.Stroke();

            var all = second != null ? values.Concat(second).ToArray() : values;
            if (all.Length == 0) return;
            float min = all.Min(), max = all.Max();
            if (Mathf.Approximately(min, max))
            {
                min -= 1f;
                max += 1f;
            }

            if (second != null && second.Length > 0) Series(painter, second, min, max, rect, secondColor.resolvedStyle.color);
            Series(painter, values, min, max, rect, resolvedStyle.color);
        }

        // One series: a line through its points, spread across the width, then a dot at each point
        private static void Series(Painter2D painter, float[] series, float min, float max, Rect rect, Color color)
        {
            if (series.Length == 0) return;
            const float pad = 3f;
            Vector2 At(int i)
            {
                float x = series.Length == 1 ? rect.width / 2f : pad + (rect.width - 2 * pad) * i / (series.Length - 1);
                float y = pad + (rect.height - 2 * pad) * (1f - Mathf.InverseLerp(min, max, series[i]));
                return new Vector2(x, y);
            }

            painter.strokeColor = color;
            painter.fillColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(At(0));
            for (int i = 1; i < series.Length; i++) painter.LineTo(At(i));
            painter.Stroke();
            for (int i = 0; i < series.Length; i++)
            {
                painter.BeginPath();
                painter.Arc(At(i), 2f, 0f, 360f);
                painter.Fill();
            }
        }
    }
}
