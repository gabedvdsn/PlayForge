using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Processes view: every process ProcessControl holds, with what it's doing and how long its steps take, beside
    // the one selected, which can be paused, resumed and terminated (ED-5)
    public sealed partial class RuntimeDebugger
    {
        private readonly List<ProcessControlBlock> processes = new();
        private readonly List<ProcessControlBlock> shownProcesses = new();
        private MultiColumnListView processList;
        private VisualElement processDetail;
        private int shownProcess = -1;

        // What a sample changes in the process's detail, in place
        private readonly List<Action> processUpdates = new();

        // Each process's steps and their time at the last sample, and its time a step since. Process Control's usage was
        // the wall-clock time between a process's steps, much the same for every process whatever its work cost (ED-5)
        private struct StepSample
        {
            public int Steps;
            public double Seconds;
            public double PerStep;
        }

        private readonly Dictionary<int, StepSample> stepSamples = new();

        private VisualElement BuildProcessesView()
        {
            var view = new VisualElement { name = "ProcessesView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(0, 420, TwoPaneSplitViewOrientation.Horizontal) { name = "ProcessSplit" };
            split.AddToClassList("pf-debugger__split");

            processList = new MultiColumnListView
            {
                name = "Processes",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = shownProcesses,
            };
            processList.AddToClassList("pf-debugger__list");
            processList.columns.Add(TextColumn("Name", "Name", 140, index => NameOf(shownProcesses[index]), true));
            processList.columns.Add(TextColumn("ID", "ID", 40, index => shownProcesses[index].CacheIndex.ToString()));
            processList.columns.Add(TextColumn("Handler", "Handler", 100, index => HandlerOf(shownProcesses[index])));
            processList.columns.Add(TextColumn("State", "State", 70, index => StateOf(shownProcesses[index])));
            processList.columns.Add(TextColumn("Step", "Step", 70, index => StepTimeOf(shownProcesses[index])));
            processList.selectionChanged += chosen => ShowProcess(chosen.OfType<ProcessControlBlock>().FirstOrDefault());
            split.Add(processList);

            var scroll = new ScrollView { name = "ProcessScroll" };
            scroll.AddToClassList("pf-debugger__detail-scroll");
            processDetail = new VisualElement { name = "ProcessDetail" };
            processDetail.AddToClassList("pf-debugger__detail");
            scroll.Add(processDetail);
            split.Add(scroll);
            view.Add(split);
            ShowProcess(null);
            return view;
        }

        // The processes the filter and the search let through, keeping the selection
        private void RefillProcesses()
        {
            if (processList == null) return;
            shownProcesses.Clear();
            shownProcesses.AddRange(processes.Where(Shows));
            processList.itemsSource = shownProcesses;
            processList.Rebuild();
            int at = shownProcesses.FindIndex(pcb => pcb.CacheIndex == shownProcess);
            processList.SetSelectionWithoutNotify(at >= 0 ? new[] { at } : Array.Empty<int>());
            if (shownProcess >= 0 && processes.All(pcb => pcb.CacheIndex != shownProcess)) ShowProcess(null);
        }

        private bool Shows(ProcessControlBlock pcb)
        {
            bool state = processFilter switch
            {
                "Running" => pcb.State == EProcessState.Running,
                "Waiting" => pcb.State == EProcessState.Waiting,
                _ => true,
            };
            return state && (Matches(NameOf(pcb)) || Matches(HandlerOf(pcb)));
        }

        // The time each process's steps took since the last sample, a step, as ProcessControl times them while the window
        // reads the game. A process that took no step since keeps the time it had
        private void SampleSteps()
        {
            foreach (var pcb in processes)
            {
                stepSamples.TryGetValue(pcb.CacheIndex, out var last);
                int steps = pcb.Steps;
                double seconds = pcb.StepSeconds;
                double perStep = steps > last.Steps ? (seconds - last.Seconds) / (steps - last.Steps) : last.PerStep;
                stepSamples[pcb.CacheIndex] = new StepSample { Steps = steps, Seconds = seconds, PerStep = perStep };
            }
        }

        // The samples of processes that left ProcessControl go with them
        private void ForgetLeftProcesses()
        {
            if (stepSamples.Count == 0) return;
            var here = new HashSet<int>(processes.Select(pcb => pcb.CacheIndex));
            foreach (var gone in stepSamples.Keys.Where(index => !here.Contains(index)).ToList()) stepSamples.Remove(gone);
        }

        private static string NameOf(ProcessControlBlock pcb) => pcb.Wrapper?.ProcessName ?? "Process";

        private static string HandlerOf(ProcessControlBlock pcb) => pcb.Handler?.GetName() ?? "None";

        private static string StateOf(ProcessControlBlock pcb) => ObjectNames.NicifyVariableName(pcb.State.ToString());

        // A process's time a step, as "0.12 ms"; a dash before it has stepped
        private string StepTimeOf(ProcessControlBlock pcb) =>
            stepSamples.TryGetValue(pcb.CacheIndex, out var sample) && sample.Steps > 0
                ? (sample.PerStep * 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " ms"
                : "—";

        // ═══════════════════════════════════════════════════════════════════════════
        // The detail
        // ═══════════════════════════════════════════════════════════════════════════

        // The process the detail shows: what it is, what it's doing and its step time, then Pause, Resume and Terminate.
        // Pause and Resume take the processes it handles with it alike; Process Control's Pause did and its Resume
        // didn't, and it greyed out Pause for a running process that waits or needs control, as it read the state the
        // process would take next (ED-5)
        private void ShowProcess(ProcessControlBlock pcb)
        {
            if (processDetail == null) return;
            processDetail.Clear();
            processUpdates.Clear();
            shownProcess = pcb?.CacheIndex ?? -1;
            if (pcb == null)
            {
                processDetail.Add(Note("Select a process to see it here."));
                return;
            }

            int id = pcb.CacheIndex;
            var header = new VisualElement { name = "ProcessHeader" };
            header.AddToClassList("pf-debugger__header");
            var name = new Label(NameOf(pcb)) { name = "HeaderName" };
            name.AddToClassList("pf-debugger__header-name");
            header.Add(name);
            var idLabel = new Label($"ID {id}") { name = "HeaderID" };
            idLabel.AddToClassList("pf-debugger__header-detail");
            header.Add(idLabel);
            processDetail.Add(header);

            var section = Section("Process", "Process");
            processDetail.Add(section);
            Label Fact(string rowName, string label, string valueName)
            {
                var row = Row(rowName, label);
                var value = Value(valueName);
                row.Add(value);
                section.Content.Add(row);
                return value;
            }
            var handler = Fact("Process Handler", "Handler", "Handler");
            var lifecycle = Fact("Process Lifecycle", "Lifecycle", "Lifecycle");
            var timing = Fact("Process Timing", "Steps in", "Timing");
            var state = Fact("Process State", "State", "State");
            var next = Fact("Process Next", "Next state", "NextState");
            var lifetime = Fact("Process Lifetime", "Lifetime", "Lifetime");
            var stepTime = Fact("Process Step Time", "Step time", "StepTime");
            var children = Fact("Process Children", "Handles", "Children");

            var actions = new VisualElement { name = "ProcessActions" };
            actions.AddToClassList("pf-button-row");
            actions.AddToClassList("pf-debugger__actions");
            Button ActionButton(string buttonName, string text, string tooltip, Action<ProcessControl> act)
            {
                var button = new Button(() => Act(act)) { name = buttonName, text = text, tooltip = tooltip };
                actions.Add(button);
                return button;
            }
            var pause = ActionButton("Pause", "Pause", "Pause it, and the processes it handles", control => control.Pause(id));
            var resume = ActionButton("Resume", "Resume", "Resume it, and the processes it handles", control => control.Unpause(id));
            var terminate = ActionButton("Terminate", "Terminate", "End it, and the processes it handles, as its lifecycle allows", control => control.Terminate(id));
            var now = ActionButton("TerminateNow", "Terminate Now", "End it, and the processes it handles, at once", control => control.TerminateImmediate(id));
            processDetail.Add(actions);

            processUpdates.Add(() =>
            {
                if (followed == null || !followed.TryGetPCB(id, out var live)) return;
                handler.text = HandlerOf(live);
                lifecycle.text = ObjectNames.NicifyVariableName(live.Wrapper.Lifecycle.ToString());
                timing.text = ObjectNames.NicifyVariableName(live.Wrapper.StepTiming.ToString());
                state.text = live.TerminateRequested ? $"{StateOf(live)}, ending" : StateOf(live);
                next.text = ObjectNames.NicifyVariableName(live.QueuedState.ToString());
                lifetime.text = Seconds(live.UnscaledLifetime);
                var step = StepTimeOf(live);
                stepTime.text = step == "—" ? "No steps yet" : $"{step} per step";
                int handled = followed.GetChildProcessIds(id)?.Count ?? 0;
                children.text = CountOf(handled, "process", "processes");
                pause.SetEnabled(live.State == EProcessState.Running);
                resume.SetEnabled(live.State == EProcessState.Waiting);
                terminate.SetEnabled(!live.TerminateRequested);
                now.SetEnabled(true);
            });
            SampleProcess();
        }

        private void SampleProcess()
        {
            if (shownProcess < 0) return;
            if (followed == null || !followed.TryGetPCB(shownProcess, out _))
            {
                ShowProcess(null);
                return;
            }
            foreach (var update in processUpdates) update();
        }
    }
}
