using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An effect level by level, as The Forge shows it (FEAT-32): a row for its duration, its ticks and its chance where it
    /// has them, then one for each impact, its own and its contained effects', with the value at each level. A cell
    /// selected shows the parts behind it, what each read and what it gave. It reads with no caster and no target, so a
    /// part that reads one gives what it falls back to, and its row says what it needs.
    /// </summary>
    internal sealed class EffectLevelsView : VisualElement
    {
        // The most levels it shows at once, so its columns stay readable
        internal const int MostLevels = 10;

        private sealed class Row
        {
            public string Name;
            public string Line;
            public string Needs;
            public NumberTrace[] Parts;
            public readonly List<Label> Cells = new();
        }

        private readonly GameplayEffect effect;
        private readonly IntegerField from;
        private readonly IntegerField to;
        private readonly VisualElement table;
        private readonly Label parts;
        private readonly List<Row> rows = new();
        private int selectedRow = -1;
        private int selectedLevel;
        private int min;

        public EffectLevelsView(GameplayEffect effect)
        {
            this.effect = effect;
            name = "EffectLevels";
            AddToClassList("pf-levels");

            var range = new VisualElement { name = "Range" };
            range.AddToClassList("pf-levels__range");
            from = new IntegerField("Levels") { name = "From", value = 1, tooltip = "The first level it shows" };
            from.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            to = new IntegerField { name = "To", value = DefaultTop(effect), tooltip = $"The last level it shows, up to {MostLevels} after the first" };
            from.RegisterValueChangedCallback(_ => Refresh());
            to.RegisterValueChangedCallback(_ => Refresh());
            range.Add(from);
            range.Add(new Label("to") { name = "RangeTo" });
            range.Add(to);
            Add(range);

            table = new VisualElement { name = "Table" };
            table.AddToClassList("pf-levels__table");
            Add(table);
            parts = new Label { name = "Parts" };
            parts.AddToClassList("pf-levels__parts");
            Add(parts);
            Refresh();
        }

        // A linked effect shows its provider's levels; another, five
        private static int DefaultTop(GameplayEffect effect) =>
            Mathf.Clamp(effect != null && effect.IsLinked ? effect.GetMaxLevel() : 5, 1, MostLevels);

        /// <summary>Reads the effect again, as after an edit.</summary>
        public void Refresh()
        {
            table.Clear();
            rows.Clear();
            parts.text = "";
            if (effect == null) return;

            min = Mathf.Max(1, from.value);
            int max = Mathf.Clamp(to.value, min, min + MostLevels - 1);
            var series = AbilityInspector.BuildEffectSeries(effect, min, max, null, null);
            if (series == null) return;

            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-levels__row");
            header.AddToClassList("pf-levels__header");
            header.Add(Name("Level", null));
            for (int level = min; level <= max; level++) header.Add(Cell(level.ToString(), null));
            table.Add(header);

            Add(series, "");
            if (selectedRow < 0 || selectedRow >= rows.Count || rows[selectedRow].Parts == null)
            {
                selectedRow = rows.FindIndex(row => row.Parts != null);
                selectedLevel = 0;
            }
            selectedLevel = Mathf.Clamp(selectedLevel, 0, max - min);
            ShowParts();
        }

        // An effect's rows, and its contained effects', each under its name
        private void Add(EffectLevelSeries series, string prefix)
        {
            bool lasting = series.DurationPolicy == EEffectDurationPolicy.Durational;
            if (lasting) Add($"{prefix}Duration (s)", series.Duration, null, null, null);
            if (series.PeriodicTicks && lasting) Add($"{prefix}Ticks", series.Ticks.Select(ticks => (float)ticks).ToArray(), null, null, null);
            if (series.PeriodicTicks && series.DurationPolicy == EEffectDurationPolicy.Infinite)
                Add($"{prefix}Every (s)", series.Interval, null, null, null);
            if (series.Chance != null && series.Chance.Any(chance => chance < 1f))
                Add($"{prefix}Chance", series.Chance, null, null, null, percent: true);

            foreach (var impact in series.Impacts)
            {
                string attribute = impact.Attribute != null ? impact.Attribute.GetName() : "Nothing";
                string how = impact.Delivery switch
                {
                    EImpactDelivery.Periodic => "each tick",
                    EImpactDelivery.Continuous => "while it lasts",
                    _ => "as it lands",
                };
                Add($"{prefix}{attribute} {how}", impact.Magnitude, impact.Parts, impact.Line, impact.Needs);
            }

            foreach (var contained in series.Contained)
                if (contained.Series != null) Add(contained.Series, $"{prefix}{contained.Series.Effect?.GetName()}: ");
        }

        private void Add(string rowName, float[] values, NumberTrace[] traces, string line, string needs, bool percent = false)
        {
            var row = new Row { Name = rowName, Parts = traces, Line = line, Needs = needs };
            int index = rows.Count;
            rows.Add(row);

            var element = new VisualElement { name = $"Row{index}" };
            element.AddToClassList("pf-levels__row");
            element.Add(Name(needs == null ? rowName : $"{rowName} *", line == null ? null : needs == null ? line : $"{line}\nNeeds {needs}"));
            for (int i = 0; i < values.Length; i++)
            {
                int level = i;
                var cell = Cell(NumberText.Number(values[i], percent), traces == null ? null : $"Level {min + i}: the parts below");
                if (traces != null)
                {
                    cell.AddToClassList("pf-levels__cell--pickable");
                    cell.RegisterCallback<ClickEvent>(_ =>
                    {
                        selectedRow = index;
                        selectedLevel = level;
                        ShowParts();
                    });
                }
                row.Cells.Add(cell);
                element.Add(cell);
            }
            table.Add(element);
        }

        private static Label Name(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip ?? "" };
            label.AddToClassList("pf-levels__name");
            return label;
        }

        private static Label Cell(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip ?? text };
            label.AddToClassList("pf-levels__cell");
            return label;
        }

        // The selected cell's parts, under the table
        private void ShowParts()
        {
            foreach (var row in rows)
                foreach (var cell in row.Cells) cell.RemoveFromClassList("pf-levels__cell--selected");
            if (selectedRow < 0 || selectedRow >= rows.Count || rows[selectedRow].Parts == null)
            {
                parts.text = "";
                return;
            }
            var selected = rows[selectedRow];
            if (selectedLevel < selected.Cells.Count) selected.Cells[selectedLevel].AddToClassList("pf-levels__cell--selected");
            var trace = selected.Parts[Mathf.Clamp(selectedLevel, 0, selected.Parts.Length - 1)];
            string needs = selected.Needs == null ? ""
                : $"\n* It needs {selected.Needs}, which a read in the editor hasn't, so the part that reads it gives what it falls back to.";
            parts.text = $"{selected.Name}, level {min + selectedLevel}:\n{trace}{needs}";
        }
    }
}
