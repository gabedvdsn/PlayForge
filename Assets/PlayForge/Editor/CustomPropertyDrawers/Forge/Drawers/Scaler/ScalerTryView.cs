using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Scaler asset's Try section (LVL-5, Decision 82): its range of levels; a block for the caster and one for the
    /// target, each an Entity field that fills it and a row for each attribute the scaler reads there, whether the side has
    /// it, its current value and its base; a row for each key a caller sets, whether it's set and its value; then a row of
    /// the scaler's value at each level, as an effect's Levels shows, with the parts behind the selected level under it.
    /// What's typed reads at once, and is kept for the session, never in the asset.
    /// </summary>
    internal sealed class ScalerTryView : VisualElement
    {
        private readonly ScalerAsset asset;
        private readonly ScalerTry tried;
        private readonly IntegerField from;
        private readonly IntegerField to;
        private readonly VisualElement sides;
        private readonly VisualElement table;
        private readonly Label parts;
        private readonly List<Label> cells = new();
        private ScalerTry.Reading reading;
        private int selectedLevel;
        private string drawnRows;

        public ScalerTryView(ScalerAsset asset)
        {
            this.asset = asset;
            tried = ScalerTry.For(asset);
            name = "ScalerTry";
            AddToClassList("pf-levels");
            AddToClassList("pf-try");

            var range = new VisualElement { name = "Range" };
            range.AddToClassList("pf-levels__range");
            from = new IntegerField("Levels") { name = "From", value = tried.From, tooltip = "The first level it reads" };
            from.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            to = new IntegerField { name = "To", value = tried.To, tooltip = $"The last level it reads, up to {ScalerTry.MostLevels} after the first" };
            from.RegisterValueChangedCallback(_ => Refresh());
            to.RegisterValueChangedCallback(_ => Refresh());
            range.Add(from);
            range.Add(new Label("to") { name = "RangeTo" });
            range.Add(to);
            Add(range);

            sides = new VisualElement { name = "TrySides" };
            Add(sides);
            table = new VisualElement { name = "TryTable" };
            table.AddToClassList("pf-levels__table");
            Add(table);
            parts = new Label { name = "TryParts" };
            parts.AddToClassList("pf-levels__parts");
            Add(parts);
            Refresh();
        }

        /// <summary>The section's line: the levels it reads, as "Levels 1–5".</summary>
        public string Summary => reading == null ? "" : AttributeStartValues.LevelsText(reading.From, reading.To);

        /// <summary>Reads the scaler again, as after an edit to it or to a value here.</summary>
        public void Refresh()
        {
            tried.From = from.value;
            tried.To = to.value;
            reading = tried.Read(asset != null ? asset.Scaler : null);
            // A read may ask for an attribute or a key it hadn't, so the rows are drawn after it; and again only then, so a
            // field being typed in stays
            var rows = RowsKey();
            if (rows != drawnRows)
            {
                drawnRows = rows;
                DrawSides();
            }
            DrawTable();
            ShowParts();
            tried.Keep();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The sides and the caller
        // ═══════════════════════════════════════════════════════════════════════════

        private string RowsKey()
        {
            var key = new System.Text.StringBuilder();
            foreach (var side in new[] { tried.Caster, tried.Target })
            {
                key.Append(side.Which).Append(side.Entity != null ? side.Entity.GetInstanceID() : 0).Append(':');
                foreach (var row in side.Attributes) key.Append(row.Attribute != null ? row.Attribute.GetInstanceID() : 0).Append(',');
            }
            key.Append("|");
            foreach (var row in tried.Caller) key.Append(row.Key.Name).Append(',');
            return key.ToString();
        }

        private void DrawSides()
        {
            sides.Clear();
            sides.Add(SideBlock(tried.Caster, "Caster", "the caster"));
            sides.Add(SideBlock(tried.Target, "Target", "the target"));
            sides.Add(CallerBlock());
        }

        private VisualElement SideBlock(ScalerTry.Side side, string title, string what)
        {
            var block = new VisualElement { name = $"Try{title}" };
            block.AddToClassList("pf-try__side");
            var fill = new ObjectField(title)
            {
                name = $"{title} Entity",
                objectType = typeof(EntityIdentity),
                allowSceneObjects = false,
                value = side.Entity,
                tooltip = $"An entity to fill {what}'s attributes from: what its attribute set starts each at, at its starting level",
            };
            fill.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            fill.RegisterValueChangedCallback(change =>
            {
                side.FillFrom(change.newValue as EntityIdentity);
                drawnRows = null;
                schedule.Execute(Refresh);
            });
            block.Add(fill);

            if (side.Attributes.Count == 0)
            {
                var none = new Label($"It reads nothing of {what}.");
                none.AddToClassList("pf-note");
                block.Add(none);
                return block;
            }
            foreach (var row in side.Attributes) block.Add(AttributeRow(row, title, what));
            return block;
        }

        private VisualElement AttributeRow(ScalerTry.AttributeRow row, string title, string what)
        {
            string attribute = row.Attribute.GetName();
            var line = new VisualElement { name = $"{title} {attribute}" };
            line.AddToClassList("pf-try__row");
            var label = new Label(attribute) { tooltip = $"{attribute}, as {what} holds it for the read" };
            label.AddToClassList("pf-try__name");
            line.Add(label);

            var has = new Toggle { name = $"{title} {attribute} Has", value = row.Has, tooltip = $"Whether {what} has {attribute}: without it, it reads as an entity without the attribute" };
            has.AddToClassList("pf-try__has");
            var current = new FloatField { name = $"{title} {attribute} Current", value = row.Current, isDelayed = true, tooltip = $"{attribute}'s current value" };
            current.AddToClassList("pf-try__value");
            var baseValue = new FloatField { name = $"{title} {attribute} Base", value = row.Base, isDelayed = true, tooltip = $"{attribute}'s base value" };
            baseValue.AddToClassList("pf-try__value");
            current.SetEnabled(row.Has);
            baseValue.SetEnabled(row.Has);

            has.RegisterValueChangedCallback(change =>
            {
                row.Has = change.newValue;
                current.SetEnabled(row.Has);
                baseValue.SetEnabled(row.Has);
                Refresh();
            });
            current.RegisterValueChangedCallback(change =>
            {
                // A base typed as the current was keeps in step with it, until it's typed apart
                if (Mathf.Approximately(row.Base, row.Current)) row.Base = change.newValue;
                row.Current = change.newValue;
                baseValue.SetValueWithoutNotify(row.Base);
                Refresh();
            });
            baseValue.RegisterValueChangedCallback(change =>
            {
                row.Base = change.newValue;
                Refresh();
            });
            line.Add(has);
            line.Add(current);
            line.Add(baseValue);
            return line;
        }

        private VisualElement CallerBlock()
        {
            var block = new VisualElement { name = "TryCaller" };
            block.AddToClassList("pf-try__side");
            var title = new Label("Caller") { tooltip = "The values a caller sets, by key, as Set By Caller reads them" };
            title.AddToClassList("pf-try__title");
            block.Add(title);
            if (tried.Caller.Count == 0)
            {
                var none = new Label("It reads no caller's value.");
                none.AddToClassList("pf-note");
                block.Add(none);
                return block;
            }
            foreach (var row in tried.Caller)
            {
                var each = row;
                string key = each.Key.ToString();
                var line = new VisualElement { name = $"Caller {key}" };
                line.AddToClassList("pf-try__row");
                var label = new Label(key) { tooltip = $"{key}: unset, the scaler takes its default" };
                label.AddToClassList("pf-try__name");
                line.Add(label);
                var set = new Toggle { name = $"Caller {key} Set", value = each.Set, tooltip = $"Whether the caller sets {key}" };
                set.AddToClassList("pf-try__has");
                var value = new FloatField { name = $"Caller {key} Value", value = each.Value, isDelayed = true, tooltip = $"The value the caller sets {key} to" };
                value.AddToClassList("pf-try__value");
                value.SetEnabled(each.Set);
                set.RegisterValueChangedCallback(change =>
                {
                    each.Set = change.newValue;
                    value.SetEnabled(each.Set);
                    Refresh();
                });
                value.RegisterValueChangedCallback(change =>
                {
                    each.Value = change.newValue;
                    Refresh();
                });
                line.Add(set);
                line.Add(value);
                block.Add(line);
            }
            return block;
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
