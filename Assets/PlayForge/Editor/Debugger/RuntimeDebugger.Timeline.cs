using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public sealed partial class RuntimeDebugger
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // The Timeline view: what the entities did over time (FEAT-12, Decision 30)
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly float[] TimelineSpans = { 5f, 10f, 30f, 60f };

        // How far back the strips reach, which a domain reload keeps
        [SerializeField] private float timelineSpan = 10f;

        private TimelineRecorder recorder;
        private VisualElement timelineView;
        private MultiColumnListView trackList;
        private VisualElement timelineDetail;
        private TwoPaneSplitView timelineSplit;
        private ToolbarToggle recordToggle;
        private ToolbarButton clearButton;
        private ToolbarMenu spanMenu;
        private readonly List<TimelineTrack> shownTracks = new();
        private readonly List<Action> timelineUpdates = new();
        private TimelineTrack shownTrack;
        private int shownRun = -1;
        private int drawnVersion = -1;

        // The game's time, which the recorder measures by
        private static float TimelineNow => ForgeTime.Time;

        private VisualElement BuildTimelineView()
        {
            recorder ??= new TimelineRecorder();
            var view = new VisualElement { name = "TimelineView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(1, 380, TwoPaneSplitViewOrientation.Horizontal) { name = "TimelineSplit" };
            timelineSplit = split;
            split.AddToClassList("pf-debugger__split");

            trackList = new MultiColumnListView
            {
                name = "Tracks",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = shownTracks,
            };
            trackList.AddToClassList("pf-debugger__list");
            trackList.columns.Add(new Column
            {
                name = "Track", title = "Track", width = 200, stretchable = true,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("pf-debugger__name-cell");
                    var ability = new ForgeTypeIcon(typeof(Ability)) { name = "AbilityIcon" };
                    var effect = new ForgeTypeIcon(typeof(GameplayEffect)) { name = "EffectIcon" };
                    cell.Add(ability);
                    cell.Add(effect);
                    var name = new Label { name = "Name" };
                    name.AddToClassList("pf-debugger__name");
                    cell.Add(name);
                    return cell;
                },
                bindCell = (cell, index) =>
                {
                    var track = shownTracks[index];
                    cell.Q("AbilityIcon").EnableInClassList(HiddenClass, track.Kind != TimelineKind.Ability);
                    cell.Q("EffectIcon").EnableInClassList(HiddenClass, track.Kind != TimelineKind.Effect);
                    cell.Q<Label>("Name").text = $"{track.EntityName} · {track.Name}";
                },
            });
            trackList.columns.Add(TextColumn("Spans", "Runs", 48, index => CountOf(shownTracks[index]).ToString(), number: true));
            trackList.columns.Add(new Column
            {
                name = "Time", title = SpanTitle(), width = 240, stretchable = true,
                makeCell = () => new TimelineStrip(),
                bindCell = (cell, index) =>
                {
                    var track = shownTracks[index];
                    ((TimelineStrip)cell).Show(track, TimelineNow, timelineSpan, track == shownTrack ? shownRun : -1);
                },
            });
            trackList.selectionChanged += chosen => ShowTrack(chosen.OfType<TimelineTrack>().FirstOrDefault());
            split.Add(trackList);

            var scroll = new ScrollView { name = "TimelineScroll" };
            scroll.AddToClassList("pf-debugger__detail-scroll");
            timelineDetail = new VisualElement { name = "TimelineDetail" };
            timelineDetail.AddToClassList("pf-debugger__detail");
            scroll.Add(timelineDetail);
            split.Add(scroll);
            view.Add(split);
            timelineView = view;
            ShowTrack(null);
            return view;
        }

        // The toolbar's part for the Timeline: Record, Clear, and how far back the strips reach
        private void BuildTimelineTools(Toolbar toolbar)
        {
            recordToggle = new ToolbarToggle { name = "Record", text = "Record", tooltip = "Record what the entities' abilities and effects do as the game plays" };
            recordToggle.SetValueWithoutNotify(true);
            recordToggle.RegisterValueChangedCallback(change =>
            {
                if (recorder != null) recorder.Recording = change.newValue;
            });
            toolbar.Add(recordToggle);

            clearButton = new ToolbarButton(() =>
            {
                recorder?.Clear();
                ShowTrack(null);
                RefillTracks();
            }) { name = "ClearTimeline", text = "Clear", tooltip = "Forget what's been recorded" };
            toolbar.Add(clearButton);

            spanMenu = new ToolbarMenu { name = "TimelineSpan", tooltip = "How far back the strips reach" };
            foreach (var each in TimelineSpans)
                spanMenu.menu.AppendAction(SpanText(each), _ =>
                {
                    timelineSpan = each;
                    ShowView();
                    RefillTracks();
                }, _ => Mathf.Approximately(timelineSpan, each) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(spanMenu);
        }

        private static string SpanText(float seconds) => $"Last {seconds:0} s";

        private string SpanTitle() => SpanText(timelineSpan);

        // The toolbar's Timeline tools show with its view
        private void ShowTimelineTools(bool live)
        {
            bool shown = live && view == View.Timeline;
            recordToggle.EnableInClassList(HiddenClass, !shown);
            clearButton.EnableInClassList(HiddenClass, !shown);
            spanMenu.EnableInClassList(HiddenClass, !shown);
            spanMenu.text = SpanTitle();
            var time = trackList?.columns["Time"];
            if (time != null) time.title = SpanTitle();
        }

        // The recorder hears the entities the window lists, whatever the view, so the Timeline has their past
        private void SampleTimeline()
        {
            if (recorder == null) return;
            recorder.Follow(hubs);
            if (view != View.Timeline) return;
            if (recorder.Version != drawnVersion) RefillTracks();
            else trackList.RefreshItems();
            foreach (var update in timelineUpdates) update();
        }

        // Leaving Play mode, the recorder stops hearing; entering it again, it starts afresh
        private void StopTimeline() => recorder?.Dispose();

        private void StartTimeline()
        {
            if (recorder == null) return;
            recorder.Clear();
            drawnVersion = -1;
        }

        private static int CountOf(TimelineTrack track) => track.Spans.Count(span => !span.Refused);

        // The tracks the search lets through, in the order their first span came, keeping the selection
        private void RefillTracks()
        {
            if (trackList == null || recorder == null) return;
            drawnVersion = recorder.Version;
            shownTracks.Clear();
            shownTracks.AddRange(recorder.Tracks.Where(track => Matches($"{track.EntityName} · {track.Name}")));
            trackList.itemsSource = shownTracks;
            trackList.Rebuild();
            int at = shownTracks.IndexOf(shownTrack);
            trackList.SetSelectionWithoutNotify(at >= 0 ? new[] { at } : Array.Empty<int>());
            if (shownTrack != null && !recorder.Tracks.Contains(shownTrack)) ShowTrack(null);
            else if (shownTrack != null) ShowTrack(shownTrack, keepRun: true);
        }

        // A track's detail: an ability's graph, its stages and their tasks, beside its runs, or an effect's applications
        private void ShowTrack(TimelineTrack track, bool keepRun = false)
        {
            if (timelineDetail == null) return;
            timelineDetail.Clear();
            timelineUpdates.Clear();
            if (!keepRun || track != shownTrack) shownRun = track != null ? LatestRun(track) : -1;
            shownTrack = track;
            if (track == null)
            {
                timelineDetail.Add(Note(recorder is { Tracks: { Count: > 0 } }
                    ? "Select a track to see it here."
                    : "Nothing recorded yet. An ability that runs, or an effect that lands, shows here as the game plays."));
                return;
            }

            bool ability = track.Kind == TimelineKind.Ability;
            var icon = new ForgeTypeIcon(ability ? typeof(Ability) : typeof(GameplayEffect));
            var header = Header("TimelineHeader", icon, out var title, out var detail);
            title.text = track.Name;
            timelineDetail.Add(header);
            timelineUpdates.Add(() => detail.text = HeaderOf(track));
            detail.text = HeaderOf(track);

            if (ability) ShowAbilityTrack(track);
            else ShowEffectTrack(track);
        }

        private static string HeaderOf(TimelineTrack track)
        {
            if (track.Kind == TimelineKind.Effect)
            {
                int landed = track.Spans.Count;
                return $"Effect · {track.EntityName} · {CountOf(landed, "application", "applications")}";
            }
            int runs = CountOf(track);
            int refused = track.Spans.Count - runs;
            var line = $"Ability · {track.EntityName} · {CountOf(runs, "run", "runs")}";
            return refused > 0 ? $"{line} · {refused} refused" : line;
        }

        // The last run that wasn't refused, which the detail shows the stages of
        private static int LatestRun(TimelineTrack track)
        {
            for (int i = track.Spans.Count - 1; i >= 0; i--)
                if (!track.Spans[i].Refused) return i;
            return -1;
        }

        private void ShowAbilityTrack(TimelineTrack track)
        {
            var asset = track.Asset as Ability;
            var stages = asset != null && asset.Behaviour?.Stages != null ? asset.Behaviour.Stages : new List<AbilityTaskBehaviourStage>();

            // Its graph: each stage in order, with its policy and its tasks, and when the selected run reached it
            var graphSection = Section("Stages", "Stages");
            graphSection.Summary.text = stages.Count.ToString();
            var graph = new VisualElement { name = "Graph" };
            graph.AddToClassList("pf-debugger__graph");
            graphSection.Content.Add(graph);
            for (int i = 0; i < stages.Count; i++) graph.Add(StageBlock(track, stages[i], i));
            if (stages.Count == 0) graph.Add(Note("None"));
            timelineDetail.Add(graphSection);

            // Its runs, the latest last
            var runsSection = Section("Runs", "Runs");
            runsSection.Content.Add(TableHead(("Run", "pf-debugger__col-40"), ("Start", "pf-debugger__col-60"), ("Length", "pf-debugger__col-60"),
                ("Stages", "pf-debugger__col-48"), ("Outcome", "pf-debugger__col-grow")));
            var rows = new VisualElement { name = "RunRows" };
            runsSection.Content.Add(rows);
            for (int i = 0; i < track.Spans.Count; i++) rows.Add(RunRow(track, i, stages.Count, rows));
            if (track.Spans.Count == 0) rows.Add(Note("None"));
            runsSection.Summary.text = track.Spans.Count.ToString();
            timelineDetail.Add(runsSection);
        }

        // A stage of the graph: its number and policy, then its tasks, and the selected run's time in it
        private VisualElement StageBlock(TimelineTrack track, AbilityTaskBehaviourStage stage, int index)
        {
            var block = new VisualElement { name = $"Stage {index + 1}" };
            block.AddToClassList("pf-debugger__stage");
            var head = new VisualElement();
            head.AddToClassList("pf-debugger__stage-head");
            var name = new Label($"Stage {index + 1}") { name = "StageName" };
            name.AddToClassList("pf-debugger__stage-name");
            head.Add(name);
            var policy = new Label(PolicyOf(stage)) { name = "Policy" };
            policy.AddToClassList("pf-debugger__cell--secondary");
            policy.AddToClassList("pf-debugger__stage-policy");
            head.Add(policy);
            var timing = new Label { name = "Timing" };
            timing.AddToClassList("pf-debugger__stage-timing");
            head.Add(timing);
            block.Add(head);

            var tasks = stage?.Tasks ?? new List<AbstractAbilityTask>();
            foreach (var task in tasks)
            {
                var label = new Label(TaskName(task)) { name = "Task" };
                label.AddToClassList("pf-debugger__stage-task");
                block.Add(label);
            }
            if (tasks.Count == 0) block.Add(Note("No tasks"));

            void Update()
            {
                var run = shownRun >= 0 && shownRun < track.Spans.Count ? track.Spans[shownRun] : null;
                var reached = run?.Stages.LastOrDefault(each => each.Index == index);
                timing.text = reached == null
                    ? (run == null ? "" : "Not reached")
                    : $"+{Seconds(reached.Start - run.Start)} · {Seconds((reached.Open ? TimelineNow : reached.End) - reached.Start)}";
            }
            Update();
            timelineUpdates.Add(Update);
            return block;
        }

        private static string PolicyOf(AbilityTaskBehaviourStage stage)
        {
            var policy = stage?.StagePolicy;
            if (policy == null) return "When All";
            var name = policy.GetType().Name;
            foreach (var suffix in new[] { "StagePolicy", "Policy" })
                if (name.EndsWith(suffix) && name.Length > suffix.Length) name = name.Substring(0, name.Length - suffix.Length);
            return ObjectNames.NicifyVariableName(name);
        }

        // A task's kind, as Apply Effects for an ApplyEffectsAbilityTask
        private static string TaskName(AbstractAbilityTask task)
        {
            if (task == null) return "None";
            var name = task.GetType().Name;
            foreach (var suffix in new[] { "AbilityTask", "Task" })
                if (name.EndsWith(suffix) && name.Length > suffix.Length)
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            return ObjectNames.NicifyVariableName(name);
        }

        private VisualElement RunRow(TimelineTrack track, int index, int stageCount, VisualElement rows)
        {
            var span = track.Spans[index];
            var row = TableRow($"Run {index + 1}", index, ("Index", "pf-debugger__col-40"), ("Start", "pf-debugger__col-60"),
                ("Length", "pf-debugger__col-60"), ("Stages", "pf-debugger__col-48"), ("Outcome", "pf-debugger__col-grow"));
            row.Q<Label>("Index").text = (index + 1).ToString();
            row.Q<Label>("Start").text = Seconds(span.Start);
            var length = row.Q<Label>("Length");
            var stages = row.Q<Label>("Stages");
            var outcome = row.Q<Label>("Outcome");
            foreach (var number in new[] { row.Q<Label>("Index"), row.Q<Label>("Start"), length, stages })
                number.AddToClassList("pf-debugger__cell--number");

            void Update()
            {
                length.text = span.Refused ? "—" : Seconds(span.Length(TimelineNow));
                stages.text = span.Refused ? "—" : $"{span.Stages.Select(each => each.Index).Distinct().Count()}/{stageCount}";
                outcome.text = span.Refused ? $"Refused: {span.Note}" : span.Open ? "Running" : span.Stages.Any(each => !each.Succeeded) ? "Stopped" : "Ended";
            }
            Update();
            timelineUpdates.Add(Update);

            if (!span.Refused)
            {
                Selectable(row, () =>
                {
                    shownRun = index;
                    ShowSelected(rows, row);
                    foreach (var update in timelineUpdates) update();
                    trackList.RefreshItems();
                });
            }
            if (index == shownRun) row.AddToClassList("pf-debugger__table-row--selected");
            return row;
        }

        private void ShowEffectTrack(TimelineTrack track)
        {
            var section = Section("Applications", "Applications");
            section.Content.Add(TableHead(("Application", "pf-debugger__col-grow"), ("Start", "pf-debugger__col-60"), ("End", "pf-debugger__col-60"),
                ("Length", "pf-debugger__col-60")));
            var rows = new VisualElement { name = "ApplicationRows" };
            section.Content.Add(rows);
            for (int i = 0; i < track.Spans.Count; i++)
            {
                var span = track.Spans[i];
                var row = TableRow($"Application {i + 1}", i, ("Index", "pf-debugger__col-grow"), ("Start", "pf-debugger__col-60"),
                    ("End", "pf-debugger__col-60"), ("Length", "pf-debugger__col-60"));
                row.Q<Label>("Index").text = span.Instant ? $"{i + 1}, instant" : (i + 1).ToString();
                row.Q<Label>("Start").text = Seconds(span.Start);
                var end = row.Q<Label>("End");
                var length = row.Q<Label>("Length");
                foreach (var number in new[] { row.Q<Label>("Start"), end, length }) number.AddToClassList("pf-debugger__cell--number");
                void Update()
                {
                    end.text = span.Open ? "—" : Seconds(span.End);
                    length.text = Seconds(span.Length(TimelineNow));
                }
                Update();
                timelineUpdates.Add(Update);
                rows.Add(row);
            }
            if (track.Spans.Count == 0) rows.Add(Note("None"));
            section.Summary.text = track.Spans.Count.ToString();
            timelineDetail.Add(section);
        }
    }

    /// <summary>
    /// A track's spans over the last seconds, drawn in its row: a run, or an effect's time on the shelf, as a bar, the one
    /// selected in the selection's color, and an instant, an instant effect or a refused activation, as a tick (FEAT-12).
    /// </summary>
    internal sealed class TimelineStrip : VisualElement
    {
        public TimelineStrip()
        {
            AddToClassList("pf-debugger__strip");
            pickingMode = PickingMode.Ignore;
        }

        public void Show(TimelineTrack track, float now, float span, int selected)
        {
            Clear();
            if (track == null || span <= 0f) return;
            float from = now - span;
            for (int i = 0; i < track.Spans.Count; i++)
            {
                var each = track.Spans[i];
                float end = each.Open ? now : each.End;
                if (end < from || each.Start > now) continue;
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("pf-debugger__span");
                float left = Mathf.Clamp01((each.Start - from) / span);
                float right = Mathf.Clamp01((end - from) / span);
                bar.style.left = Length.Percent(left * 100f);
                if (each.Instant || each.Refused)
                {
                    bar.AddToClassList("pf-debugger__span--instant");
                    bar.EnableInClassList("pf-debugger__span--refused", each.Refused);
                }
                else bar.style.width = Length.Percent(Mathf.Max(right - left, 0.004f) * 100f);
                bar.EnableInClassList("pf-debugger__span--open", each.Open);
                bar.EnableInClassList("pf-debugger__span--selected", i == selected);
                Add(bar);
            }
        }
    }
}
