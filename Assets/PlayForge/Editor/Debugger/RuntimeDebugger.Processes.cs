using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Processes view, as the design canvas's Processes board draws it: every process ProcessControl holds, with what
    // it's doing and how long its steps take, beside the one selected, which can be paused, resumed and terminated (ED-5)
    public sealed partial class RuntimeDebugger
    {
        private readonly List<ProcessControlBlock> processes = new();
        private readonly List<ProcessControlBlock> shownProcesses = new();
        private MultiColumnListView processList;
        private VisualElement processDetail;
        private int shownProcess = -1;

        // What a sample changes in the process's detail, in place
        private readonly List<Action> processUpdates = new();

        // Each process's steps and their time at each sample of the last second, which its step time averages. Process
        // Control's usage was the wall-clock time between a process's steps, much the same for every process whatever its
        // work cost (ED-5)
        private readonly struct StepSample
        {
            public readonly double At;
            public readonly int Steps;
            public readonly double Seconds;

            public StepSample(double at, int steps, double seconds)
            {
                At = at;
                Steps = steps;
                Seconds = seconds;
            }
        }

        private readonly Dictionary<int, List<StepSample>> stepSamples = new();

        private VisualElement BuildProcessesView()
        {
            var view = new VisualElement { name = "ProcessesView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(1, 380, TwoPaneSplitViewOrientation.Horizontal) { name = "ProcessSplit" };
            processSplit = split;
            split.AddToClassList("pf-debugger__split");

            processList = new MultiColumnListView
            {
                name = "Processes",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                sortingMode = ColumnSortingMode.Custom,
                itemsSource = shownProcesses,
            };
            processList.AddToClassList("pf-debugger__list");
            processList.columns.Add(TextColumn("ID", "ID", 44, index => shownProcesses[index].CacheIndex.ToString(), number: true));
            processList.columns.Add(TextColumn("Process", "Process", 160, index => NameOf(shownProcesses[index]), true));
            processList.columns.Add(TextColumn("Handler", "Handler", 120, index => HandlerOf(shownProcesses[index]), true));
            processList.columns.Add(TextColumn("Lifecycle", "Lifecycle", 112, index => shownProcesses[index].Wrapper.Lifecycle.ToString()));
            processList.columns.Add(TextColumn("Timing", "Timing", 96, index => TimingOf(shownProcesses[index].Wrapper.StepTiming)));
            processList.columns.Add(TextColumn("State", "State", 128, index => StateOf(shownProcesses[index])));
            processList.columns.Add(TextColumn("Lifetime", "Lifetime", 64, index => Seconds(shownProcesses[index].UnscaledLifetime), number: true));
            processList.columns.Add(TextColumn("Step", "Step", 64, index => StepTimeOf(shownProcesses[index], false), number: true));
            foreach (var column in processList.columns) column.sortable = true;
            processList.sortColumnDescriptions.Add(new SortColumnDescription("ID", SortDirection.Ascending));
            processList.columnSortingChanged += RefillProcesses;
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

        // The processes the filter and the search let through, in the order the column titles set, keeping the selection
        private void RefillProcesses()
        {
            if (processList == null) return;
            shownProcesses.Clear();
            shownProcesses.AddRange(processes.Where(Shows));
            SortProcesses();
            processList.itemsSource = shownProcesses;
            processList.Rebuild();
            int at = shownProcesses.FindIndex(pcb => pcb.CacheIndex == shownProcess);
            processList.SetSelectionWithoutNotify(at >= 0 ? new[] { at } : Array.Empty<int>());
            if (shownProcess >= 0 && processes.All(pcb => pcb.CacheIndex != shownProcess)) ShowProcess(null);
        }

        // By the column whose title was clicked, then by ID
        private void SortProcesses()
        {
            var by = processList.sortedColumns?.FirstOrDefault();
            if (by == null) return;
            int direction = by.direction == SortDirection.Descending ? -1 : 1;
            Comparison<ProcessControlBlock> compare = by.columnName switch
            {
                "Process" => (a, b) => EditorUtility.NaturalCompare(NameOf(a), NameOf(b)),
                "Handler" => (a, b) => EditorUtility.NaturalCompare(HandlerOf(a), HandlerOf(b)),
                "Lifecycle" => (a, b) => a.Wrapper.Lifecycle.CompareTo(b.Wrapper.Lifecycle),
                "Timing" => (a, b) => a.Wrapper.StepTiming.CompareTo(b.Wrapper.StepTiming),
                "State" => (a, b) => a.State.CompareTo(b.State),
                "Lifetime" => (a, b) => a.UnscaledLifetime.CompareTo(b.UnscaledLifetime),
                "Step" => (a, b) => (StepSecondsOf(a) ?? -1.0).CompareTo(StepSecondsOf(b) ?? -1.0),
                _ => (_, _) => 0,
            };
            shownProcesses.Sort((a, b) =>
            {
                int order = compare(a, b) * direction;
                return order != 0 ? order : a.CacheIndex.CompareTo(b.CacheIndex) * (by.columnName == "ID" ? direction : 1);
            });
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

        // Each process's steps and their time, at this sample. A sample a second old or more is kept, so the average
        // spans the whole last second
        private void SampleSteps()
        {
            double now = EditorApplication.timeSinceStartup;
            foreach (var pcb in processes)
            {
                if (!stepSamples.TryGetValue(pcb.CacheIndex, out var samples))
                    stepSamples[pcb.CacheIndex] = samples = new List<StepSample>();
                samples.Add(new StepSample(now, pcb.Steps, pcb.StepSeconds));
                while (samples.Count > 2 && now - samples[1].At >= 1.0) samples.RemoveAt(0);
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

        // What handles a process: a hub by its entity's name, as the Entities view lists it
        private static string HandlerOf(ProcessControlBlock pcb) => pcb.Handler switch
        {
            null => "None",
            IGameplayAbilitySystem gas => NameOf(gas),
            var handler => handler.GetName() ?? "None",
        };

        // The steps a process takes, as the list shows them
        private static string TimingOf(EProcessStepTiming timing) => timing switch
        {
            EProcessStepTiming.Update => "Update",
            EProcessStepTiming.LateUpdate => "Late",
            EProcessStepTiming.FixedUpdate => "Fixed",
            EProcessStepTiming.UpdateAndLate => "Update + Late",
            EProcessStepTiming.UpdateAndFixed => "Update + Fixed",
            EProcessStepTiming.LateAndFixed => "Late + Fixed",
            EProcessStepTiming.UpdateFixedAndLate => "Update + Fixed + Late",
            _ => "None",
        };

        // A process's state, and the one it moves to next where something has asked it to, as "Running → Terminated". A
        // running process's own next state, the one its lifecycle takes when its run ends, isn't a request
        private static string StateOf(ProcessControlBlock pcb)
        {
            if (pcb.TerminateRequested && pcb.State != EProcessState.Terminated) return $"{pcb.State} → Terminated";
            if (pcb.State == EProcessState.Created && pcb.QueuedState != EProcessState.Created) return $"Created → {pcb.QueuedState}";
            return pcb.State.ToString();
        }

        // A process's time a step over the last second, in seconds, or none when it took no step in it
        private double? StepSecondsOf(ProcessControlBlock pcb)
        {
            if (!stepSamples.TryGetValue(pcb.CacheIndex, out var samples) || samples.Count < 2) return null;
            var first = samples[0];
            var last = samples[samples.Count - 1];
            int steps = last.Steps - first.Steps;
            return steps > 0 ? (last.Seconds - first.Seconds) / steps : null;
        }

        // As "0.05 ms" in the list, and "0.05 milliseconds" in the detail; a dash, or None, for a process that took no
        // step in the last second
        private string StepTimeOf(ProcessControlBlock pcb, bool full)
        {
            var seconds = StepSecondsOf(pcb);
            if (seconds == null) return full ? "None" : "—";
            return Milliseconds(seconds.Value, full);
        }

        private static string Milliseconds(double seconds, bool full) =>
            (seconds * 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + (full ? " milliseconds" : " ms");

        // ═══════════════════════════════════════════════════════════════════════════
        // The detail
        // ═══════════════════════════════════════════════════════════════════════════

        // The process the detail shows: its header, Pause, Resume and Terminate, then what it is, its state and its time.
        // Pause and Resume take the processes it handles with it alike; Process Control's Pause did and its Resume didn't,
        // and it greyed out Pause for a running process that waits or needs control, as it read the state the process
        // would take next (ED-5)
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
            var header = Header("ProcessHeader", new ForgeGlyph(ForgeGlyphShape.Process), out var title, out var detail);
            var select = HeaderButton(header, "SelectInScene", "Select in Scene", "Select its GameObject", () => SelectObjectOf(pcb));
            select.SetEnabled(pcb.Wrapper != null && pcb.Wrapper.TryGetProcess<Component>(out _));
            processDetail.Add(header);

            var actions = Actions("ProcessActions");
            var pause = ActionButton(actions, "Pause", "Pause", "Pause it, and the processes it handles", () => Act(control => control.Pause(id)));
            var resume = ActionButton(actions, "Resume", "Resume", "Resume it, and the processes it handles", () => Act(control => control.Unpause(id)));
            var terminate = ActionButton(actions, "Terminate", "Terminate", "End it, and the processes it handles, as its lifecycle allows",
                () => Act(control => control.Terminate(id)));
            var now = ActionButton(actions, "TerminateNow", "", "End it, and the processes it handles, at once",
                () => Act(control => control.TerminateImmediate(id)));
            now.Add(new ForgeGlyph(ForgeGlyphShape.Terminate));
            now.Add(new Label("Terminate Now"));
            processDetail.Add(actions);

            var about = Section("Process", "Process");
            about.AddToClassList("pf-debugger__section--facts");
            processDetail.Add(about);
            var lifecycle = Fact(about.Content, "Process Lifecycle", "Lifecycle");
            var timing = Fact(about.Content, "Process Timing", "Step timing");
            var priority = Fact(about.Content, "Process Priority", "Step priority");
            var handler = Fact(about.Content, "Process Handler", "Handler");
            var show = new Button(() => ShowHandlerOf(id)) { name = "ShowHandler", text = "Show", tooltip = "Show the process that handles it" };
            show.AddToClassList("pf-debugger__fact-button");
            handler.parent.Add(show);

            var stateSection = Section("ProcessState", "State");
            stateSection.AddToClassList("pf-debugger__section--facts");
            processDetail.Add(stateSection);
            var state = Fact(stateSection.Content, "Process State", "State");
            var queued = Fact(stateSection.Content, "Process Queued", "Queued");
            var run = Fact(stateSection.Content, "Process Run", "Run");
            var cancelled = new HelpBox("Its run was cancelled, so Resume starts a new one.", HelpBoxMessageType.Info) { name = "RunCancelled" };
            stateSection.Content.Add(cancelled);

            var time = Section("ProcessTime", "Time");
            time.AddToClassList("pf-debugger__section--facts");
            processDetail.Add(time);
            var lifetime = Fact(time.Content, "Process Lifetime", "Lifetime");
            var running = Fact(time.Content, "Process Running", "Time running");
            var stepTime = Fact(time.Content, "Process Step Time", "Step time");
            var peak = Fact(time.Content, "Process Peak Step", "Peak step");

            processUpdates.Add(() =>
            {
                if (followed == null || !followed.TryGetPCB(id, out var live)) return;
                title.text = NameOf(live);
                detail.text = $"Process {id} · {(live.isMono ? "Mono" : "Runtime")} process";
                lifecycle.text = live.Wrapper.Lifecycle.ToString();
                timing.text = live.Wrapper.StepTiming.ToString();
                priority.text = live.Wrapper.PriorityMethod == EProcessStepPriorityMethod.Manual
                    ? $"Manual, {live.Wrapper.StepPriority}"
                    : live.Wrapper.PriorityMethod.ToString();
                handler.text = HandlerOf(live);
                show.SetEnabled(HandlerProcessOf(live) != null);

                state.text = live.State.ToString();
                queued.text = QueuedOf(live);
                queued.tooltip = live.State == EProcessState.Running && !live.TerminateRequested
                    ? "The state its lifecycle takes when its run ends"
                    : "";
                run.text = RunOf(live);
                cancelled.EnableInClassList(HiddenClass, run.text != "Cancelled");

                lifetime.text = SecondsInFull(live.UnscaledLifetime);
                running.text = SecondsInFull(live.TotalUpdateTime);
                stepTime.text = StepTimeOf(live, true);
                peak.text = live.Steps > 0 ? Milliseconds(live.PeakStepSeconds, true) : "None";
                foreach (var value in new[] { handler, queued, run, stepTime, peak }) ShowNone(value);

                pause.SetEnabled(live.State == EProcessState.Running);
                resume.SetEnabled(live.State == EProcessState.Waiting);
                terminate.SetEnabled(!live.TerminateRequested);
            });
            SampleProcess();
        }

        // The state a process moves to next, or None
        private static string QueuedOf(ProcessControlBlock pcb)
        {
            if (pcb.TerminateRequested && pcb.State != EProcessState.Terminated) return "Terminated";
            return pcb.QueuedState != pcb.State ? pcb.QueuedState.ToString() : "None";
        }

        // Whether a process's run is going or held by a pause, which a resume picks up, or was cancelled, so a resume
        // starts a new one (Decision 5)
        private static string RunOf(ProcessControlBlock pcb)
        {
            if (pcb.Wrapper.Lifecycle == EProcessLifecycle.Synchronous) return "None";
            if (pcb.RunIsHeld) return pcb.State == EProcessState.Waiting ? "Held" : "Going";
            if (!pcb.HasRun) return "Not started";
            return pcb.State == EProcessState.Waiting ? "Cancelled" : "Ended";
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

        // The process whose own process is the handler, such as a hub's
        private ProcessControlBlock HandlerProcessOf(ProcessControlBlock pcb)
        {
            var handler = pcb.Handler;
            if (handler == null) return null;
            foreach (var each in processes)
                if (each.Wrapper != null && each.Wrapper.TryGetProcess<IGameplayProcessHandler>(out var own) && ReferenceEquals(own, handler))
                    return each;
            return null;
        }

        // Show: the handler's process, selected in the list, where the filter and the search let it through
        private void ShowHandlerOf(int id)
        {
            if (followed == null || !followed.TryGetPCB(id, out var pcb)) return;
            var handler = HandlerProcessOf(pcb);
            if (handler == null) return;
            if (!shownProcesses.Contains(handler))
            {
                search = "";
                searchField.SetValueWithoutNotify("");
                processFilter = AllStates;
                ShowView();
                RefillProcesses();
            }
            int at = shownProcesses.IndexOf(handler);
            processList.SetSelection(at);
            processList.ScrollToItem(at);
        }

        private static void SelectObjectOf(ProcessControlBlock pcb)
        {
            if (pcb.Wrapper == null || !pcb.Wrapper.TryGetProcess<Component>(out var component) || component == null) return;
            Selection.activeGameObject = component.gameObject;
            EditorGUIUtility.PingObject(component.gameObject);
        }
    }
}
