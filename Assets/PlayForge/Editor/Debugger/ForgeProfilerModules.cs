using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The PlayForge Processes module in Unity's Profiler (FEAT-18, Decision 32): ProcessControl's and TaskSequence's
    /// counters, charted, and in its details the selected frame's costliest processes, which PlayForge puts into the
    /// capture as the frame's metadata while the Profiler records.
    /// </summary>
    [Serializable]
    [ProfilerModuleMetadata("PlayForge Processes")]
    public class PlayForgeProcessesProfilerModule : ProfilerModule
    {
        /// <summary>The counters it charts, ten at most.</summary>
        public static readonly ProfilerCounterDescriptor[] Counters =
        {
            new("Processes Running", ForgeCounters.CategoryName),
            new("Processes Waiting", ForgeCounters.CategoryName),
            new("Processes Created", ForgeCounters.CategoryName),
            new("Registrations", ForgeCounters.CategoryName),
            new("Terminations", ForgeCounters.CategoryName),
            new("Steps", ForgeCounters.CategoryName),
            new("State Changes", ForgeCounters.CategoryName),
            new("Step Time", ForgeCounters.CategoryName),
            new("Sequences Running", ForgeCounters.CategoryName),
            new("Stages Started", ForgeCounters.CategoryName),
        };

        public PlayForgeProcessesProfilerModule() : base(Counters) { }

        public override ProfilerModuleViewController CreateDetailsViewController() => new CostliestProcessesView(ProfilerWindow);
    }

    /// <summary>
    /// The PlayForge GAS module in Unity's Profiler (FEAT-18, Decision 32): the GAS's counters, charted: its entities by
    /// hub, its effects, impacts, activations and refusals, and its tag compiles and recomputes.
    /// </summary>
    [Serializable]
    [ProfilerModuleMetadata("PlayForge GAS")]
    public class PlayForgeGASProfilerModule : ProfilerModule
    {
        /// <summary>The counters it charts, ten at most.</summary>
        public static readonly ProfilerCounterDescriptor[] Counters =
        {
            new("Mono Entities", ForgeCounters.CategoryName),
            new("Runtime Entities", ForgeCounters.CategoryName),
            new("Effects Active", ForgeCounters.CategoryName),
            new("Effects Applied", ForgeCounters.CategoryName),
            new("Effects Removed", ForgeCounters.CategoryName),
            new("Impacts", ForgeCounters.CategoryName),
            new("Activations", ForgeCounters.CategoryName),
            new("Refused Activations", ForgeCounters.CategoryName),
            new("Tag Compiles", ForgeCounters.CategoryName),
            new("Recomputes", ForgeCounters.CategoryName),
        };

        public PlayForgeGASProfilerModule() : base(Counters) { }
    }

    /// <summary>
    /// The PlayForge Processes module's details: the selected frame's costliest processes, the costliest first, each by its
    /// ID, its type and its steps' time, read from the frame's metadata, so a saved capture shows them too. A table, as the
    /// Debugger's are, whose Process column gives way first in a narrow window.
    /// </summary>
    internal sealed class CostliestProcessesView : ProfilerModuleViewController
    {
        private readonly struct Row
        {
            public readonly int Process;
            public readonly string Type;
            public readonly double Milliseconds;

            public Row(int process, string type, double milliseconds)
            {
                Process = process;
                Type = type;
                Milliseconds = milliseconds;
            }
        }

        private readonly List<Row> rows = new();
        private MultiColumnListView table;
        private Label status;

        public CostliestProcessesView(ProfilerWindow profilerWindow) : base(profilerWindow) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement { name = "CostliestProcesses" };
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-debugger");

            table = new MultiColumnListView
            {
                name = "Costs",
                fixedItemHeight = 20,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = rows,
            };
            table.AddToClassList("pf-debugger__list");
            table.columns.Add(TextColumn("ID", 56, index => rows[index].Process.ToString(), number: true));
            table.columns.Add(TextColumn("Process", 160, index => rows[index].Type, stretch: true));
            table.columns.Add(TextColumn("Step", 80, index => $"{rows[index].Milliseconds:0.000} ms", number: true));
            root.Add(table);

            var bar = new VisualElement { name = "CostsStatus" };
            bar.AddToClassList("pf-debugger__status");
            status = new Label { name = "CostsFrame" };
            status.AddToClassList("pf-debugger__status-part");
            bar.Add(status);
            root.Add(bar);

            ProfilerWindow.SelectedFrameIndexChanged += Show;
            Show(ProfilerWindow.selectedFrameIndex);
            return root;
        }

        private static Column TextColumn(string title, float width, Func<int, string> text, bool stretch = false, bool number = false) => new()
        {
            name = title, title = title, width = width, stretchable = stretch,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-debugger__cell");
                label.EnableInClassList("pf-debugger__cell--number", number);
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(index),
        };

        // The frame's costs, and under the tag after each, its type's name
        private void Show(long frame)
        {
            rows.Clear();
            if (frame >= 0)
            {
                using var data = ProfilerDriver.GetRawFrameDataView((int)frame, 0);
                if (data != null && data.valid)
                {
                    var costs = data.GetFrameMetaData<ForgeProcessCost>(ForgeFrameCosts.MetaDataId, ForgeFrameCosts.Tag);
                    for (int i = 0; i < costs.Length; i++)
                    {
                        var name = data.GetFrameMetaData<byte>(ForgeFrameCosts.MetaDataId, ForgeFrameCosts.Tag + 1 + i);
                        string type = name.Length > 0 ? Encoding.UTF8.GetString(name.ToArray()) : "Unknown";
                        rows.Add(new Row(costs[i].Process, type, costs[i].Nanoseconds / 1e6));
                    }
                }
            }

            table.RefreshItems();
            status.text = rows.Count > 0
                ? $"Frame {frame}: its {rows.Count} costliest processes, by their steps' time"
                : "No PlayForge process stepped in this frame while the Profiler recorded";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ProfilerWindow != null) ProfilerWindow.SelectedFrameIndexChanged -= Show;
            base.Dispose(disposing);
        }
    }
}
