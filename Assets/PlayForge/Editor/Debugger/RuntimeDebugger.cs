using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// One runtime debugger for the GAS, Process and Tag views (Q4). It lists the hubs ProcessControl registers, of either
    /// kind (Q28), each with its attributes, tags, effects and abilities, and every process, with what it's doing and how
    /// long its steps take (ED-5). The lists change as processes join and leave ProcessControl, and the values change in
    /// place, at the rate the toolbar sets, while the game plays (Q30). In edit mode it offers Play Mode. The GAS Debugger
    /// scanned the scene for MonoBehaviour hubs and rebuilt its details every 0.25 s (ED-4), and Process Control and the
    /// Tag Debugger were IMGUI windows of their own.
    /// </summary>
    public sealed partial class RuntimeDebugger : EditorWindow
    {
        private const string WindowTitle = "Runtime Debugger";
        private const string HiddenClass = "pf-debugger__hidden";
        private static readonly int[] Rates = { 2, 4, 10, 30 };

        /// <summary>
        /// Whether the game is live, as it is in play mode. The tests drive the window in edit mode, over a ProcessControl
        /// bootstrapped there.
        /// </summary>
        internal static Func<bool> Live = () => EditorApplication.isPlaying;

        // How many windows time the processes' steps, so the last one to stop turns the timing off
        private static int timing;

        // The view, the search, the filter and the rate, which a domain reload keeps
        [SerializeField] private bool processesShown;
        [SerializeField] private string search = "";
        [SerializeField] private string processFilter = "All";
        [SerializeField] private int samplesPerSecond = 4;

        // The ProcessControl the window follows, and whether a process has joined or left it since the lists were read
        private ProcessControl followed;
        private bool listsStale = true;
        private bool sampling;
        private double nextSample;

        private VisualElement prompt;
        private VisualElement views;
        private VisualElement entitiesView;
        private VisualElement processesView;
        private ToolbarToggle entitiesToggle;
        private ToolbarToggle processesToggle;
        private ToolbarMenu filterMenu;
        private ToolbarMenu rateMenu;
        private ToolbarSearchField searchField;
        private Label status;

        // ═══════════════════════════════════════════════════════════════════════════
        // Opening
        // ═══════════════════════════════════════════════════════════════════════════

        // Under Window, where the Asset Store expects a package's menu (rule 2.5.1.a, ED-19)
        [MenuItem("Window/PlayForge/" + WindowTitle, false, 10)]
        public static void ShowWindow() => Open();

        internal static RuntimeDebugger Open()
        {
            var window = GetWindow<RuntimeDebugger>();
            window.titleContent = new GUIContent(WindowTitle, EditorGUIUtility.IconContent("d_UnityEditor.DebugInspectorWindow").image);
            window.minSize = new Vector2(600, 320);
            window.Show();
            return window;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Following the game
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopSampling();
            Unfollow();
            ForgetShownEntity();
        }

        private void OnPlayModeChanged(PlayModeStateChange change) => ShowState();

        // Live, the views follow the game; in edit mode, the prompt offers Play Mode (Q28)
        private void ShowState()
        {
            if (views == null) return;
            bool live = Live();
            prompt.EnableInClassList(HiddenClass, live);
            views.EnableInClassList(HiddenClass, !live);
            if (live)
            {
                StartSampling();
                SampleNow();
            }
            else
            {
                StopSampling();
                Unfollow();
                ReadLists();
            }
            ShowView();
            ShowStatus();
        }

        // While the game plays, the window reads it at the rate the toolbar sets, and ProcessControl times its steps
        private void StartSampling()
        {
            if (sampling) return;
            sampling = true;
            EditorApplication.update += OnEditorUpdate;
            if (timing++ == 0) ProcessControlBlock.MeasureSteps = true;
        }

        private void StopSampling()
        {
            if (!sampling) return;
            sampling = false;
            EditorApplication.update -= OnEditorUpdate;
            if (--timing == 0) ProcessControlBlock.MeasureSteps = false;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup >= nextSample) SampleNow();
        }

        /// <summary>
        /// Reads the game: the lists again, when a process has joined or left ProcessControl, then the values the window
        /// shows, which change in place.
        /// </summary>
        internal void SampleNow()
        {
            nextSample = EditorApplication.timeSinceStartup + 1.0 / Math.Max(1, samplesPerSecond);
            if (views == null || !Live()) return;
            Follow(ProcessControl.Instance);
            SampleSteps();
            if (listsStale) ReadLists();
            else
            {
                entityList.RefreshItems();
                processList.RefreshItems();
            }
            SampleEntity();
            SampleProcess();
            ShowStatus();
        }

        // The ProcessControl the window follows. A process that joins or leaves it has the lists read again at the next
        // sample, so the window never scans the scene
        private void Follow(ProcessControl control)
        {
            if (control == null) control = null;
            if (ReferenceEquals(control, followed)) return;
            Unfollow();
            followed = control;
            listsStale = true;
            if (control == null) return;
            control.Callbacks.OnProcessRegistered += OnProcessesChanged;
            control.Callbacks.OnProcessUnregistered += OnProcessesChanged;
        }

        private void Unfollow()
        {
            // A ProcessControl destroyed with its scene still has its callbacks to leave
            if (!ReferenceEquals(followed, null))
            {
                followed.Callbacks.OnProcessRegistered -= OnProcessesChanged;
                followed.Callbacks.OnProcessUnregistered -= OnProcessesChanged;
            }
            followed = null;
            listsStale = true;
        }

        private void OnProcessesChanged(ProcessRelay relay, ProcessDataPacket data) => listsStale = true;

        // What ProcessControl holds: every process, and the hubs among them, of either kind (Q28)
        private void ReadLists()
        {
            listsStale = false;
            hubs.Clear();
            processes.Clear();
            if (followed != null)
            {
                foreach (var pcb in followed.FetchActiveProcesses().Values)
                {
                    processes.Add(pcb);
                    if (pcb.Wrapper != null && pcb.Wrapper.TryGetProcess<IGameplayAbilitySystem>(out var gas) && Alive(gas) && !hubs.Contains(gas))
                        hubs.Add(gas);
                }
            }
            hubs.Sort((a, b) => EditorUtility.NaturalCompare(NameOf(a), NameOf(b)));
            processes.Sort((a, b) => a.CacheIndex.CompareTo(b.CacheIndex));
            ForgetLeftProcesses();
            RefillEntities();
            RefillProcesses();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-debugger");
            root.Add(BuildToolbar());

            prompt = BuildPrompt();
            root.Add(prompt);

            views = new VisualElement { name = "Views" };
            views.AddToClassList("pf-debugger__views");
            entitiesView = BuildEntitiesView();
            processesView = BuildProcessesView();
            views.Add(entitiesView);
            views.Add(processesView);
            root.Add(views);

            status = new Label { name = "DebuggerStatus" };
            status.AddToClassList("pf-debugger__status");
            root.Add(status);
            ShowState();
        }

        // The two views, the processes' filter, the search, the rate and ProcessControl's state
        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar { name = "DebuggerToolbar" };
            entitiesToggle = new ToolbarToggle { name = "ShowEntities", text = "Entities", tooltip = "Each entity, with its attributes, tags, effects and abilities" };
            entitiesToggle.RegisterValueChangedCallback(change =>
            {
                processesShown = !change.newValue;
                ShowView();
            });
            toolbar.Add(entitiesToggle);
            processesToggle = new ToolbarToggle { name = "ShowProcesses", text = "Processes", tooltip = "Each process ProcessControl runs" };
            processesToggle.RegisterValueChangedCallback(change =>
            {
                processesShown = change.newValue;
                ShowView();
            });
            toolbar.Add(processesToggle);

            filterMenu = new ToolbarMenu { name = "Filter", tooltip = "Show only the processes in this state" };
            foreach (var each in Filters)
                filterMenu.menu.AppendAction(each, _ =>
                {
                    processFilter = each;
                    ShowView();
                    RefillProcesses();
                }, _ => processFilter == each ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(filterMenu);

            toolbar.Add(new ToolbarSpacer { flex = true });
            searchField = new ToolbarSearchField { name = "Search", tooltip = "Show what has this text in its name" };
            searchField.AddToClassList("pf-debugger__search");
            searchField.SetValueWithoutNotify(search);
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                RefillEntities();
                RefillProcesses();
            });
            toolbar.Add(searchField);

            rateMenu = new ToolbarMenu { name = "Rate", tooltip = "How often the window reads the game while it plays" };
            foreach (var rate in Rates)
                rateMenu.menu.AppendAction($"{rate} per second", _ =>
                {
                    samplesPerSecond = rate;
                    ShowView();
                }, _ => samplesPerSecond == rate ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(rateMenu);

            var control = new ToolbarMenu { name = "Control", text = "ProcessControl", tooltip = "Whether ProcessControl takes new processes and runs them" };
            foreach (var (state, label) in ControlStates)
                control.menu.AppendAction(label, _ => Act(each => each.SetControlState(state)),
                    _ => followed == null ? DropdownMenuAction.Status.Disabled
                        : followed.State == state ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            control.menu.AppendSeparator();
            control.menu.AppendAction("Terminate Every Process", _ => Act(each => each.SetControlState(EProcessControlState.Terminated)),
                _ => followed == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            control.menu.AppendAction("Terminate Every Process Now", _ => Act(each => each.SetControlState(EProcessControlState.TerminatedImmediately)),
                _ => followed == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            toolbar.Add(control);
            return toolbar;
        }

        // A listed process is running or waiting: one that terminates leaves ProcessControl as it does, so Process
        // Control's Terminated filter never matched (ED-5)
        private static readonly string[] Filters = { "All", "Running", "Waiting" };

        private static readonly (EProcessControlState State, string Label)[] ControlStates =
        {
            (EProcessControlState.Ready, "Ready"),
            (EProcessControlState.Closed, "Closed"),
            (EProcessControlState.Waiting, "Waiting"),
            (EProcessControlState.ClosedWaiting, "Closed and Waiting"),
        };

        // In edit mode, Play Mode, where the game runs (Q28)
        private static VisualElement BuildPrompt()
        {
            var box = new VisualElement { name = "Prompt" };
            box.AddToClassList("pf-debugger__prompt");
            box.Add(Note("Entities and processes show in Play Mode."));
            var enter = new Button(EditorApplication.EnterPlaymode) { name = "EnterPlayMode", text = "Enter Play Mode" };
            enter.AddToClassList("pf-debugger__enter");
            box.Add(enter);
            return box;
        }

        // The Entities view or the Processes view, as the toolbar's toggles choose
        private void ShowView()
        {
            if (views == null) return;
            entitiesToggle.SetValueWithoutNotify(!processesShown);
            processesToggle.SetValueWithoutNotify(processesShown);
            entitiesView.EnableInClassList(HiddenClass, processesShown);
            processesView.EnableInClassList(HiddenClass, !processesShown);
            filterMenu.EnableInClassList(HiddenClass, !processesShown);
            filterMenu.text = processFilter;
            rateMenu.text = $"{samplesPerSecond} per second";
        }

        private void ShowStatus()
        {
            if (status == null) return;
            if (!Live()) status.text = "Edit mode";
            else if (followed == null) status.text = "No ProcessControl";
            else
                status.text = $"{CountOf(hubs.Count, "entity", "entities")}, {CountOf(processes.Count, "process", "processes")}, " +
                              $"ProcessControl {ObjectNames.NicifyVariableName(followed.State.ToString())}";
        }

        // An action on ProcessControl, which the window then reads again
        private void Act(Action<ProcessControl> action)
        {
            if (followed == null) return;
            action(followed);
            listsStale = true;
            SampleNow();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Text
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An entity's name: its own, or for a MonoBehaviour hub with none, its GameObject's (GAS-S1).</summary>
        internal static string NameOf(IGameplayAbilitySystem gas)
        {
            if (gas == null) return "";
            var name = gas.GetName();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            var body = gas.ToGASObject();
            return body != null ? body.name : "Unnamed";
        }

        // A hub of either kind that's still there: a MonoBehaviour hub's object goes with its scene
        private static bool Alive(IGameplayAbilitySystem gas) => gas is Object unity ? unity != null : gas != null;

        private bool Matches(string text) =>
            string.IsNullOrWhiteSpace(search) || (text ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private static string CountOf(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

        private static string Seconds(float seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        private static string Signed(float value) => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);

        private static readonly IComparer<string> NaturalOrder = Comparer<string>.Create(EditorUtility.NaturalCompare);

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("pf-note");
            return note;
        }

        // A list column of text, which clips its own (Docs/EDITOR_STYLE.md)
        private static Column TextColumn(string name, string title, float width, Func<int, string> text, bool stretch = false) => new()
        {
            name = name, title = title, width = width, stretchable = stretch,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-debugger__cell");
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(index),
        };

        // A row of a detail: its name on the left, which gives way first, then its values, which never shrink
        private static VisualElement Row(string name, string label)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("pf-debugger__row");
            var title = new Label(label) { name = "Name" };
            title.AddToClassList("pf-debugger__row-name");
            row.Add(title);
            return row;
        }

        private static Label Value(string name)
        {
            var value = new Label { name = name };
            value.AddToClassList("pf-debugger__row-value");
            return value;
        }

        // The titles over a detail's columns
        private static VisualElement ColumnTitles(params string[] titles)
        {
            var row = new VisualElement();
            row.AddToClassList("pf-debugger__row");
            row.AddToClassList("pf-debugger__titles");
            for (int i = 0; i < titles.Length; i++)
            {
                var title = new Label(titles[i]);
                title.AddToClassList(i == 0 ? "pf-debugger__row-name" : "pf-debugger__row-value");
                row.Add(title);
            }
            return row;
        }

        // A detail's section, Unity's foldout header with a one-line summary, open or closed as it was this session
        private static ForgeSection Section(string name, string title)
        {
            var key = $"PlayForge.RuntimeDebugger.{name}";
            var section = new ForgeSection(name, title, SessionState.GetBool(key, true));
            section.Toggled += open => SessionState.SetBool(key, open);
            return section;
        }
    }
}
