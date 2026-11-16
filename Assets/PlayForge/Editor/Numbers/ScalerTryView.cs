using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Scaler asset's Try section: its range of levels; a block for the caster and one for the
    /// target, each an Entity field that fills it and a row for each attribute the scaler reads there, whether the side has
    /// it, its current value and its base; a row for each key a caller sets, whether it's set and its value; then a row of
    /// the scaler's value at each level, as an effect's Levels shows, with the parts behind the selected level under it.
    /// What's typed reads at once, and is kept for the session, never in the asset. The sides and the caller are a
    /// <see cref="TryNumbersView"/>, as a Value Pipeline's try draws them too.
    /// </summary>
    internal sealed class ScalerTryView : VisualElement
    {
        private readonly ScalerAsset asset;
        private readonly ScalerTry tried;
        private readonly IntegerField from;
        private readonly IntegerField to;
        private readonly TryNumbersView sides;
        private readonly VisualElement table;
        private readonly Label parts;
        private readonly List<Label> cells = new();
        private ScalerTry.Reading reading;
        private int selectedLevel;

        public ScalerTryView(ScalerAsset asset)
        {
            this.asset = asset;
            tried = ScalerTry.For(asset);
            name = "ScalerTry";
            AddToClassList("pf-levels");
            AddToClassList("pf-try");

            Add(LevelsRow(tried.From, tried.To, "it reads", ScalerTry.MostLevels, Refresh, out from, out to));

            sides = new TryNumbersView(tried, Refresh, "the scaler");
            Add(sides);
            table = new VisualElement { name = "TryTable" };
            table.AddToClassList("pf-levels__table");
            Add(table);
            parts = new Label { name = "TryParts" };
            parts.AddToClassList("pf-levels__parts");
            Add(parts);
            Refresh();
        }

        /// <summary>
        /// A try's range of levels, as "Levels 1 to 5": its first level after the row's label, and its last, each in a number
        /// field 48 px wide.
        /// </summary>
        internal static VisualElement LevelsRow(int first, int last, string what, int most, System.Action changed,
            out IntegerField from, out IntegerField to)
        {
            var range = new VisualElement { name = "Range" };
            range.AddToClassList("pf-levels__range");
            from = new IntegerField("Levels") { name = "From", value = first, tooltip = $"The first level {what}" };
            from.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            to = new IntegerField { name = "To", value = last, tooltip = $"The last level {what}, up to {most} after the first" };
            from.RegisterValueChangedCallback(_ => changed());
            to.RegisterValueChangedCallback(_ => changed());
            range.Add(from);
            range.Add(new Label("to") { name = "RangeTo" });
            range.Add(to);
            return range;
        }

        /// <summary>The section's line: the levels it reads, as "Levels 1–5".</summary>
        public string Summary => reading == null ? "" : AttributeStartValues.LevelsText(reading.From, reading.To);

        /// <summary>Reads the scaler again, as after an edit to it or to a value here.</summary>
        public void Refresh()
        {
            tried.From = from.value;
            tried.To = to.value;
            reading = tried.Read(asset != null ? asset.Scaler : null);
            // A read may ask for an attribute or a key it hadn't, so the rows follow it
            sides.Follow();
            DrawTable();
            ShowParts();
            tried.Keep();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The value at each level, and its parts
        // ═══════════════════════════════════════════════════════════════════════════

        private void DrawTable()
        {
            table.Clear();
            cells.Clear();
            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-levels__row");
            header.AddToClassList("pf-levels__header");
            header.Add(Name("Level", null));
            for (int level = reading.From; level <= reading.To; level++) header.Add(Cell(level.ToString(), null));
            table.Add(header);

            var row = new VisualElement { name = "TryValue" };
            row.AddToClassList("pf-levels__row");
            row.Add(Name("Value", "The scaler's value at each level, against the caster, the target and the caller above"));
            for (int i = 0; i < reading.Values.Length; i++)
            {
                int index = i;
                float value = reading.Values[i];
                var cell = Cell(float.IsNaN(value) ? "—" : NumberText.Number(value), $"Level {reading.From + i}: the parts below");
                cell.AddToClassList("pf-levels__cell--pickable");
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedLevel = index;
                    ShowParts();
                });
                cells.Add(cell);
                row.Add(cell);
            }
            table.Add(row);
            selectedLevel = Mathf.Clamp(selectedLevel, 0, reading.Values.Length - 1);
        }

        internal static Label Name(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip ?? "" };
            label.AddToClassList("pf-levels__name");
            return label;
        }

        internal static Label Cell(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip ?? text };
            label.AddToClassList("pf-levels__cell");
            return label;
        }

        // The selected level's parts, under the table
        private void ShowParts()
        {
            foreach (var cell in cells) cell.RemoveFromClassList("pf-levels__cell--selected");
            if (asset == null || asset.Scaler == null)
            {
                parts.text = "Give it a kind under Values to try it.";
                return;
            }
            if (selectedLevel < cells.Count) cells[selectedLevel].AddToClassList("pf-levels__cell--selected");
            var trace = reading.Parts != null && selectedLevel < reading.Parts.Length ? reading.Parts[selectedLevel] : null;
            string error = reading.Error == null ? "" : $"\n* Its read threw at {reading.Error}";
            if (trace == null)
            {
                parts.text = $"Level {reading.From + selectedLevel}: no read.{error}";
                return;
            }
            string needs = trace.Needs == null ? ""
                : $"\n* It needs {trace.Needs}, which a try hasn't, so the part that reads it gives what it falls back to.";
            parts.text = $"Level {reading.From + selectedLevel}:\n{trace}{needs}{error}";
        }
    }
}
