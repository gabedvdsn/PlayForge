using System.Collections.Generic;
using System.Text;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Value Pipeline's Try section, as a Scaler asset's is drawn: the value going in, whether chances come up,
    /// and its range of levels; a block for the caster and one for the target, each an Entity field that fills it and a
    /// row for each attribute the stages read there; a row for each key a caller sets; then a row of what comes out at each
    /// level, with the steps behind the selected level under it, each stage's value in and out and its note. What's typed
    /// reads at once, and is kept for the session, never in the asset.
    /// </summary>
    internal sealed class ValuePipelineTryView : VisualElement
    {
        private readonly ValuePipeline pipeline;
        private readonly ValuePipelineTry tried;
        private readonly FloatField value;
        private readonly Toggle chances;
        private readonly IntegerField from;
        private readonly IntegerField to;
        private readonly TryNumbersView sides;
        private readonly VisualElement table;
        private readonly Label steps;
        private readonly List<Label> cells = new();
        private ValuePipelineTry.Reading reading;
        private int selectedLevel;

        public ValuePipelineTryView(ValuePipeline pipeline)
        {
            this.pipeline = pipeline;
            tried = ValuePipelineTry.For(pipeline);
            name = "ValuePipelineTry";
            AddToClassList("pf-levels");
            AddToClassList("pf-try");

            value = new FloatField("Value In")
            {
                name = "TryIn", value = tried.Value, isDelayed = true,
                tooltip = "The value the hit lands with, going in: a loss below 0, as a blow of 100 is −100; a gain above 0",
            };
            value.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            value.RegisterValueChangedCallback(_ => Refresh());
            Add(value);

            chances = new Toggle("Chances Come Up")
            {
                name = "TryChances", value = tried.ChancesComeUp,
                tooltip = "On, each chance a stage rolls comes up where it can, as a critical strike or a block; off, each misses " +
                          "where it can. A chance of 1 always comes up and one of 0 never does",
            };
            chances.AddToClassList(BaseField<bool>.alignedFieldUssClassName);
            chances.RegisterValueChangedCallback(_ => Refresh());
            Add(chances);

            Add(ScalerTryView.LevelsRow(tried.From, tried.To, "it runs at", ValuePipelineTry.MostLevels, Refresh, out from, out to));

            sides = new TryNumbersView(tried.Numbers, Refresh, "the number that reads it");
            Add(sides);
            table = new VisualElement { name = "TryTable" };
            table.AddToClassList("pf-levels__table");
            Add(table);
            steps = new Label { name = "TrySteps" };
            steps.AddToClassList("pf-levels__parts");
            Add(steps);
            Refresh();
        }

        /// <summary>The section's line: the value in and the levels it runs at, as "−100 in, Levels 1–5".</summary>
        public string Summary => reading == null ? "" : $"{NumberText.Number(reading.In)} in, {AttributeStartValues.LevelsText(reading.From, reading.To)}";

        /// <summary>After each run, for the section's line to follow a value typed here.</summary>
        public event System.Action Ran;

        /// <summary>Runs the pipeline again, as after an edit to its stages or to a value here.</summary>
        public void Refresh()
        {
            tried.Value = value.value;
            tried.ChancesComeUp = chances.value;
            tried.From = from.value;
            tried.To = to.value;
            reading = tried.Read(pipeline);
            // A run may ask for an attribute or a key it hadn't, so the rows follow it
            sides.Follow();
            DrawTable();
            ShowSteps();
            tried.Keep();
            Ran?.Invoke();
        }

        private void DrawTable()
        {
            table.Clear();
            cells.Clear();
            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-levels__row");
            header.AddToClassList("pf-levels__header");
            header.Add(ScalerTryView.Name("Level", null));
            for (int level = reading.From; level <= reading.To; level++) header.Add(ScalerTryView.Cell(level.ToString(), null));
            table.Add(header);

            var row = new VisualElement { name = "TryOut" };
            row.AddToClassList("pf-levels__row");
            row.Add(ScalerTryView.Name("Out", "What the hit lands with at each level, from the value in, against the caster, the target and the caller above"));
            for (int i = 0; i < reading.Levels.Length; i++)
            {
                int index = i;
                var run = reading.Levels[i];
                string text = run.StoppedBy != null || float.IsNaN(run.Out) ? "—" : NumberText.Number(run.Out);
                string tooltip = run.StoppedBy != null
                    ? $"Level {run.Level}: stopped, {run.StoppedBy.Name()}, so nothing of the effect lands"
                    : $"Level {run.Level}: the steps below";
                var cell = ScalerTryView.Cell(text, tooltip);
                cell.AddToClassList("pf-levels__cell--pickable");
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedLevel = index;
                    ShowSteps();
                });
                cells.Add(cell);
                row.Add(cell);
            }
            table.Add(row);
            selectedLevel = Mathf.Clamp(selectedLevel, 0, reading.Levels.Length - 1);
        }

        // The selected level's steps, under the table: the value in, each stage's step, and what came out
        private void ShowSteps()
        {
            foreach (var cell in cells) cell.RemoveFromClassList("pf-levels__cell--selected");
            if (pipeline == null || pipeline.Stages == null || pipeline.Stages.Count == 0)
            {
                steps.text = "Give it a stage under Stages to try it.";
                return;
            }
            if (selectedLevel < cells.Count) cells[selectedLevel].AddToClassList("pf-levels__cell--selected");
            var run = selectedLevel < reading.Levels.Length ? reading.Levels[selectedLevel] : null;
            if (run == null)
            {
                steps.text = "";
                return;
            }

            var text = new StringBuilder($"Level {run.Level}: {NumberText.Number(reading.In)} in");
            if (run.StoppedBy != null)
            {
                text.Append($"\nStopped: {run.StoppedBy.Name()}. It rolls as the effect lands, before the pipeline runs, and nothing of the effect lands.");
            }
            else if (run.Error != null)
            {
                text.Append($"\n* Its run threw: {run.Error}");
            }
            else
            {
                foreach (var step in run.Steps)
                {
                    text.Append($"\n{step.Stage}: {NumberText.Number(step.Before)} → {NumberText.Number(step.After)}");
                    if (!string.IsNullOrEmpty(step.Note)) text.Append($", {step.Note}");
                }
                text.Append($"\n= {NumberText.Number(run.Out)}");
            }
            steps.text = text.ToString();
        }
    }
}
