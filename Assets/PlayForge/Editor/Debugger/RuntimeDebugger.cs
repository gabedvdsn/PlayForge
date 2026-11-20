using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// The Debugger: one window for the game as it plays, in place of the GAS Debugger, Process Control and the Tag
    /// Debugger. Its Entities view lists the hubs
    /// ProcessControl registers, of either kind, beside one hub's attributes, effects, abilities and tags, which it
    /// can change. Its Processes view lists every process, with what it's doing and how long its steps take,
    /// and its Tags view every tag the entities hold, as a tree, beside who holds the one selected. Its Timeline
    /// view shows what the entities' abilities and effects did over time, as a recorder heard it. The lists follow
    /// ProcessControl as processes join and leave, and the values change in place, at the rate the window menu sets,
    /// while the game plays. Outside Play mode it offers Play Mode. The GAS Debugger scanned the scene for
    /// MonoBehaviour hubs and rebuilt its details every 0.25 s; Process Control and the Tag Debugger were IMGUI
    /// windows of their own. The Entities, Tags and Timeline views come with the ability system, in files of their own
    /// that this one's hooks call, so PlayForge Process Sequencer, which has none, has the Processes view alone.
    /// </summary>
    public sealed partial class RuntimeDebugger : EditorWindow, IHasCustomMenu
    {
        private const string WindowTitle = "Debugger";
        private const string HiddenClass = "pf-debugger__hidden";
        private static readonly int[] Rates = { 2, 4, 10, 30 };

        /// <summary>
        /// Whether the game is live, as it is in Play mode. The tests drive the window in edit mode, over a ProcessControl
        /// bootstrapped there.
        /// </summary>
        internal static Func<bool> Live = () => EditorApplication.isPlaying;

        // How many windows time the processes' steps, so the last one to stop turns the timing off
        private static int timing;

        // The views; PlayForge Process Sequencer has the Processes view alone
        private enum View
        {
            Entities,
            Processes,
            Tags,
            Timeline,
        }

        // The view, the search, the filters, the rate and Follow Selection, which a domain reload keeps
        [SerializeField] private View view;
        [SerializeField] private string search = "";
        [SerializeField] private string processFilter = AllStates;
        [SerializeField] private int samplesPerSecond = 4;

        // The ProcessControl the window follows, and whether a process has joined or left it since the lists were read
        private ProcessControl followed;
        private bool listsStale = true;
        private bool sampling;
        private double nextSample;

        private VisualElement prompt;
        private VisualElement views;
        private VisualElement processesView;
        private readonly Dictionary<View, ToolbarToggle> viewToggles = new();
        private ToolbarSearchField searchField;
        private ToolbarMenu filterMenu;
        private Label controlLabel;
        private ToolbarMenu controlMenu;
        private Label frameLabel;
        private VisualElement statusBar;
        private Label controlStatus;
        private Label processStatus;
        private Label actionStatus;
        private Label frameStatus;
        private ToolbarSpacer toolbarSpacer;

        // A narrow window, as a docked column, and the views' splits it lays out
        private const float NarrowWidth = 720f;
        private const float NarrowListHeight = 240f;
        private const string NarrowClass = "pf-debugger--narrow";
        private const string SourceColumnClass = "pf-debugger__col-source";
        private static readonly string[] NarrowHiddenColumns = { "Handler", "Lifecycle", "Timing", "Lifetime" };
        private const string ProcessesTable = "Debugger.Processes";
        private bool narrow;
        private bool fitted;
        private TwoPaneSplitView processSplit;

        // ═══════════════════════════════════════════════════════════════════════════
        // Opening
        // ═══════════════════════════════════════════════════════════════════════════

        // Under Window, where the Asset Store expects a package's menu (rule 2.5.1.a)
        [MenuItem("Window/PlayForge/" + WindowTitle, false, 10)]
        public static void ShowWindow() => Open();

        internal static RuntimeDebugger Open()
        {
            var window = GetWindow<RuntimeDebugger>();
            window.titleContent = new GUIContent(WindowTitle, EditorGUIUtility.IconContent("d_UnityEditor.DebugInspectorWindow").image);
            window.minSize = new Vector2(640, 320);
            window.Show();
            return window;
        }

        // The window menu holds the rate, which stays off the toolbar
        public void AddItemsToMenu(GenericMenu menu)
        {
            foreach (var rate in Rates)
                menu.AddItem(new GUIContent($"Samples a Second/{rate}"), samplesPerSecond == rate, () => samplesPerSecond = rate);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Following the game
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EnableAbilityViews();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopSampling();
            Unfollow();
            DisableAbilityViews();
        }

        private void OnPlayModeChanged(PlayModeStateChange change) => ShowState();

        // Live, the views follow the game; outside Play mode, the prompt offers it
        private void ShowState()
        {
            if (views == null) return;
            bool live = Live();
            ForgeStack.Show(prompt, !live);
            ForgeStack.Show(views, live);
            statusBar.EnableInClassList(HiddenClass, !live);
            searchField.SetEnabled(live);
            ShowAbilityState(live);
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
        }

        // While the game plays, the window reads it at the rate the window menu sets, and ProcessControl times its steps
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
                processList.RefreshItems();
                RefreshAbilityLists();
            }
            SampleProcess();
            SampleAbilityViews();
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

        // What ProcessControl holds: every process, and the hubs among them, of either kind
        private void ReadLists()
        {
            listsStale = false;
            processes.Clear();
            if (followed != null) processes.AddRange(followed.FetchActiveProcesses().Values);
            processes.Sort((a, b) => a.CacheIndex.CompareTo(b.CacheIndex));
            ForgetLeftProcesses();
            RefillProcesses();
            ReadAbilityLists();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-debugger");
            root.RegisterCallback<GeometryChangedEvent>(changed => Fit(changed.newRect.width));
            root.Add(BuildToolbar());

            // The prompt and the views take turns in one place, as the views do among themselves: each stays laid out
            // while another shows, so a view's split and columns keep their sizes
            var body = ForgeStack.Make(new VisualElement { name = "Body" });
            prompt = BuildPrompt();
            body.Add(prompt);

            views = ForgeStack.Make(new VisualElement { name = "Views" });
            views.AddToClassList("pf-debugger__views");
            processesView = BuildProcessesView();
            views.Add(processesView);
            BuildAbilityViews(views);
            body.Add(views);
            root.Add(body);

            statusBar = new VisualElement { name = "DebuggerStatus" };
            statusBar.AddToClassList("pf-debugger__status");
            controlStatus = StatusPart("ControlStatus");
            processStatus = StatusPart("ProcessStatus");
            BuildAbilityStatus();
            actionStatus = StatusPart("ActionStatus");
            actionStatus.AddToClassList("pf-debugger__status-action");
            frameStatus = StatusPart("FrameStatus");
            frameStatus.AddToClassList(HiddenClass);
            root.Add(statusBar);
            ShowState();
        }

        private Label StatusPart(string name)
        {
            var part = new Label { name = name };
            part.AddToClassList("pf-debugger__status-part");
            statusBar.Add(part);
            return part;
        }

        // The three views, the search, the view's filter, Follow Selection or ProcessControl's state, and the frame
        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar { name = "DebuggerToolbar" };
            toolbar.AddToClassList("pf-debugger__toolbar");
            var segments = new VisualElement { name = "ViewToggles" };
            segments.AddToClassList("pf-debugger__views-toggles");
            var shown = new List<(View View, string Text, string Tip)> { (View.Processes, "Processes", "Each process ProcessControl runs") };
            AddAbilityViewToggles(shown);
            foreach (var (each, text, tip) in shown)
            {
                var toggle = new ToolbarToggle { name = $"Show{text}", text = text, tooltip = tip };
                toggle.AddToClassList("pf-debugger__view-toggle");
                toggle.RegisterValueChangedCallback(change =>
                {
                    // A segment stays pressed: a click on the pressed one keeps its view
                    if (change.newValue) view = each;
                    ShowView();
                    SampleNow();
                });
                viewToggles[each] = toggle;
                segments.Add(toggle);
            }
            toolbar.Add(segments);

            searchField = new ToolbarSearchField { name = "Search" };
            searchField.AddToClassList("pf-debugger__search");
            searchField.SetValueWithoutNotify(search);
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                RefillProcesses();
                RefillAbilityViews();
            });
            toolbar.Add(searchField);
            BuildEntityFilter(toolbar);

            filterMenu = new ToolbarMenu { name = "Filter", tooltip = "Show the processes in one state" };
            foreach (var each in Filters)
                filterMenu.menu.AppendAction(each, _ =>
                {
                    processFilter = each;
                    ShowView();
                    RefillProcesses();
                }, _ => processFilter == each ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(filterMenu);
            BuildTimelineToolbar(toolbar);

            toolbarSpacer = new ToolbarSpacer { flex = true };
            toolbar.Add(toolbarSpacer);
            BuildFollowToggle(toolbar);

            controlLabel = new Label("ProcessControl") { name = "ControlLabel" };
            controlLabel.AddToClassList("pf-debugger__toolbar-label");
            toolbar.Add(controlLabel);
            controlMenu = new ToolbarMenu { name = "Control", tooltip = "ProcessControl's state: whether it takes new processes and runs them" };
            foreach (var (state, text) in ControlStates)
                controlMenu.menu.AppendAction(text, _ => Act(each => each.SetControlState(state)),
                    _ => followed == null ? DropdownMenuAction.Status.Disabled
                        : followed.State == state ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(controlMenu);

            frameLabel = new Label { name = "Frame" };
            frameLabel.AddToClassList("pf-debugger__frame");
            toolbar.Add(frameLabel);
            return toolbar;
        }

        private const string AllStates = "All states";

        // A listed process is running or waiting: one that terminates leaves ProcessControl as it does, so Process
        // Control's Terminated filter never matched
        private static readonly string[] Filters = { AllStates, "Running", "Waiting" };

        private static readonly (EProcessControlState State, string Text)[] ControlStates =
        {
            (EProcessControlState.Ready, "Ready"),
            (EProcessControlState.Waiting, "Waiting"),
            (EProcessControlState.Closed, "Closed"),
            (EProcessControlState.ClosedWaiting, "Closed and Waiting"),
            (EProcessControlState.Terminated, "Terminated"),
            (EProcessControlState.TerminatedImmediately, "Terminated Immediately"),
        };

        // Outside Play mode, Play Mode, where the game runs
        private static VisualElement BuildPrompt()
        {
            var box = new VisualElement { name = "Prompt" };
            box.AddToClassList("pf-debugger__prompt");
            var text = new Label("Not in Play mode.");
            text.AddToClassList("pf-debugger__prompt-text");
            box.Add(text);
            var enter = new Button(EditorApplication.EnterPlaymode) { name = "EnterPlayMode", tooltip = "Enter Play mode" };
            enter.AddToClassList("pf-debugger__enter");
            enter.Add(new ForgeGlyph(ForgeGlyphShape.Play));
            enter.Add(new Label("Enter Play Mode"));
            box.Add(enter);
            return box;
        }

        // The view the toolbar's toggles choose, with its search and its part of the toolbar
        private void ShowView()
        {
            if (views == null) return;
            bool live = Live();
            // A view the package doesn't have, as PlayForge Process Sequencer has no Entities view, gives way to the
            // Processes view
            if (!viewToggles.ContainsKey(view)) view = View.Processes;
            foreach (var (each, toggle) in viewToggles) toggle.SetValueWithoutNotify(each == view);
            ForgeStack.Show(processesView, view == View.Processes);
            ShowAbilityViews(live);
            filterMenu.EnableInClassList(HiddenClass, !live || view != View.Processes);
            controlLabel.EnableInClassList(HiddenClass, !live || view != View.Processes || narrow);
            controlMenu.EnableInClassList(HiddenClass, !live || view != View.Processes);
            frameLabel.EnableInClassList(HiddenClass, !live || narrow);
            toolbarSpacer.EnableInClassList(HiddenClass, narrow);
            filterMenu.text = processFilter;
            var placeholder = view switch
            {
                View.Processes => "Search processes",
                View.Tags => "Search tags",
                View.Timeline => "Search tracks",
                _ => "Search entities",
            };
            searchField.tooltip = placeholder;
            var field = searchField.Q<TextField>();
            if (field != null) field.textEdition.placeholder = placeholder;
        }

        // The status bar's counts: ProcessControl's state, the processes by state, the entities by hub
        private void ShowStatus()
        {
            if (statusBar == null) return;
            frameLabel.text = $"Frame {Grouped(Time.frameCount)}";

            // A narrow window's status bar holds the frame count, and counts without their parts
            frameStatus.text = frameLabel.text;
            frameStatus.EnableInClassList(HiddenClass, !narrow);
            controlStatus.EnableInClassList(HiddenClass, narrow);
            controlMenu.text = followed != null ? ControlStates.First(each => each.State == followed.State).Text : "None";
            if (followed == null)
            {
                controlStatus.text = "ProcessControl: None";
                processStatus.text = "";
            }
            else
            {
                controlStatus.text = $"ProcessControl: {ControlStates.First(each => each.State == followed.State).Text}";
                int running = processes.Count(pcb => pcb.State == EProcessState.Running);
                int waiting = processes.Count(pcb => pcb.State == EProcessState.Waiting);
                int created = processes.Count(pcb => pcb.State == EProcessState.Created);
                var parts = new List<string> { $"{running} running", $"{waiting} waiting" };
                if (created > 0) parts.Add($"{created} created");
                processStatus.text = narrow
                    ? CountOf(processes.Count, "process", "processes")
                    : $"{CountOf(processes.Count, "process", "processes")}: {string.Join(", ", parts)}";
            }
            ShowAbilityStatus();
        }

        /// <summary>
        /// Lays the window out for its width, as EDITOR_STYLE's Debugger section has it. Below 720 px, as a docked column,
        /// each view's list sits above its detail, the toolbar's views take a row of their own, the frame count moves to
        /// the status bar, the Processes list keeps ID, Process, State and Step, and Held By drops Source. The window kept
        /// its wide layout at any width, so a docked one squeezed its lists and details side by side.
        /// </summary>
        internal void Fit(float width)
        {
            if (views == null || float.IsNaN(width) || width <= 0f) return;
            bool now = width < NarrowWidth;
            if (fitted && now == narrow) return;
            fitted = true;
            narrow = now;
            rootVisualElement.EnableInClassList(NarrowClass, narrow);
            Lay(processSplit, 1, 380);
            FitAbilityViews();
            // A narrow window hides the four, and a wide one shows those the user didn't hide from the header's menu, so
            // the fit keeps nothing as the user's choice. The list's header shows or hides a column at once, while its rows
            // take their cells only at a rebuild Unity schedules. The window fits itself as it's laid out, so the header
            // would resize a column the rows have no cell for yet, which throws: the rows are built again with the header
            if (processList != null)
            {
                bool changed = false;
                ForgeColumnWidths.FitToWidth(() =>
                {
                    foreach (var name in NarrowHiddenColumns)
                    {
                        var column = processList.columns[name];
                        bool shown = !narrow && !ForgeColumnWidths.HiddenByUser(ProcessesTable, name);
                        if (column.visible == shown) continue;
                        column.visible = shown;
                        changed = true;
                    }
                });
                if (changed) processList.Rebuild();
            }
            rootVisualElement.Query(className: SourceColumnClass).ForEach(cell => cell.EnableInClassList(HiddenClass, narrow));
            ShowView();
            ShowStatus();
        }

        // A view's list beside its detail, the pane given keeping its width, or in a narrow window above it
        private void Lay(TwoPaneSplitView split, int widePane, float wideDimension)
        {
            if (split == null) return;
            split.orientation = narrow ? TwoPaneSplitViewOrientation.Vertical : TwoPaneSplitViewOrientation.Horizontal;
            split.fixedPaneIndex = narrow ? 0 : widePane;
            split.fixedPaneInitialDimension = narrow ? NarrowListHeight : wideDimension;
        }

        // What the last action in the window did, as "Applied Burning" or "Burning was refused", on the status bar's
        // right, so the foot rows never shift to say it
        private void ShowAction(string text)
        {
            if (actionStatus != null) actionStatus.text = text;
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
        // The ability system's views
        // ═══════════════════════════════════════════════════════════════════════════

        // The hooks the Entities, Tags and Timeline views fill, where the package has the ability system. Without it,
        // as in PlayForge Process Sequencer, each is nothing, and the window has the Processes view alone
        partial void EnableAbilityViews();
        partial void DisableAbilityViews();
        partial void AddAbilityViewToggles(List<(View View, string Text, string Tip)> toggles);
        partial void BuildAbilityViews(VisualElement views);
        partial void BuildEntityFilter(Toolbar toolbar);
        partial void BuildTimelineToolbar(Toolbar toolbar);
        partial void BuildFollowToggle(Toolbar toolbar);
        partial void BuildAbilityStatus();
        partial void ShowAbilityState(bool live);
        partial void ShowAbilityViews(bool live);
        partial void ShowAbilityStatus();
        partial void FitAbilityViews();
        partial void ReadAbilityLists();
        partial void RefreshAbilityLists();
        partial void RefillAbilityViews();
        partial void SampleAbilityViews();

        // A handler's name as the views name it: an entity's as the Entities view lists it
        static partial void NameHandler(IGameplayProcessHandler handler, ref string name);

        // ═══════════════════════════════════════════════════════════════════════════
        // Text
        // ═══════════════════════════════════════════════════════════════════════════

        private bool Matches(string text) =>
            string.IsNullOrWhiteSpace(search) || (text ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private static string CountOf(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

        // A number with its thousands apart, as "1 842"
        private static string Grouped(int value) =>
            value.ToString("#,0", new NumberFormatInfo { NumberGroupSeparator = " " });

        // A time in a table, as "3.2 s", and in full in a detail, as "3.4 seconds"
        private static string Seconds(float seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        private static string SecondsInFull(float seconds) =>
            seconds.ToString("0.0", CultureInfo.InvariantCulture) + (Mathf.Approximately(seconds, 1f) ? " second" : " seconds");

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Signed(float value) => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);

        private static readonly IComparer<string> NaturalOrder = Comparer<string>.Create(EditorUtility.NaturalCompare);

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("pf-note");
            return note;
        }

        // A list column of text, which clips its own
        private static Column TextColumn(string name, string title, float width, Func<int, string> text, bool stretch = false,
            bool number = false) => new()
        {
            name = name, title = title, width = width, stretchable = stretch,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-debugger__cell");
                label.EnableInClassList("pf-debugger__cell--number", number);
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(index),
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Details: a header, sections, tables and facts
        // ═══════════════════════════════════════════════════════════════════════════

        // A detail's header: the 32 px icon, the name, a line of what it is, then its buttons
        private static VisualElement Header(string name, VisualElement icon, out Label title, out Label detail)
        {
            var header = new VisualElement { name = name };
            header.AddToClassList("pf-debugger__header");
            icon.AddToClassList("pf-debugger__header-icon");
            header.Add(icon);
            var text = new VisualElement();
            text.AddToClassList("pf-debugger__header-text");
            title = new Label { name = "HeaderName" };
            title.AddToClassList("pf-debugger__header-name");
            text.Add(title);
            detail = new Label { name = "HeaderDetail" };
            detail.AddToClassList("pf-debugger__header-detail");
            text.Add(detail);
            header.Add(text);
            return header;
        }

        private static Button HeaderButton(VisualElement header, string name, string text, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text, tooltip = tooltip };
            button.AddToClassList("pf-debugger__header-button");
            header.Add(button);
            return button;
        }

        // A section: Unity's foldout header with its count on the right, open or closed as it was this session
        private static ForgeSection Section(string name, string title)
        {
            var key = $"PlayForge.Debugger.{name}";
            var section = new ForgeSection(name, title, SessionState.GetBool(key, true));
            section.AddToClassList("pf-debugger__section");
            section.Toggled += open => SessionState.SetBool(key, open);
            return section;
        }

        // A table's column titles. Each column is a class, which sets its width in the style sheet, and the first cell is
        // marked as one, since USS has no :first-child
        private const string FirstCellClass = "pf-debugger__cell--first";

        private static VisualElement TableHead(params (string Title, string Column)[] columns)
        {
            var head = new VisualElement();
            head.AddToClassList("pf-debugger__table-head");
            foreach (var (title, column) in columns)
            {
                var cell = new Label(title);
                cell.AddToClassList("pf-debugger__cell");
                cell.AddToClassList(column);
                cell.EnableInClassList(FirstCellClass, head.childCount == 0);
                head.Add(cell);
            }
            return head;
        }

        // A table's row, alternating with the one before, whose cells are labels named as given
        private static VisualElement TableRow(string name, int index, params (string Name, string Column)[] cells)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("pf-debugger__table-row");
            row.EnableInClassList("pf-debugger__table-row--alternate", index % 2 == 1);
            foreach (var (cellName, column) in cells)
            {
                var cell = new Label { name = cellName };
                cell.AddToClassList("pf-debugger__cell");
                cell.AddToClassList(column);
                cell.EnableInClassList(FirstCellClass, row.childCount == 0);
                row.Add(cell);
            }
            return row;
        }

        // A row's leading cell, such as a name with its icon, before the cells TableRow made
        private static void Lead(VisualElement row, VisualElement cell)
        {
            foreach (var each in row.Children()) each.RemoveFromClassList(FirstCellClass);
            cell.AddToClassList(FirstCellClass);
            row.Insert(0, cell);
        }

        // A fact whose value is None, in secondary text
        private static void ShowNone(Label value) => value.EnableInClassList("pf-debugger__fact-value--none", value.text == "None");

        // A row a click selects, which the table's own actions then act on
        private static void Selectable(VisualElement row, Action select)
        {
            row.RegisterCallback<MouseDownEvent>(down =>
            {
                if (down.button != 0) return;
                select();
            });
        }

        private static void ShowSelected(VisualElement rows, VisualElement selected)
        {
            foreach (var row in rows.Children()) row.EnableInClassList("pf-debugger__table-row--selected", row == selected);
        }

        // A detail's fact: its name in the label column, then its value
        private static Label Fact(VisualElement parent, string name, string label)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("pf-debugger__fact");
            var title = new Label(label);
            title.AddToClassList("pf-debugger__fact-name");
            row.Add(title);
            var value = new Label { name = "Value" };
            value.AddToClassList("pf-debugger__fact-value");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        // A row of a table's actions, under it
        private static VisualElement Actions(string name)
        {
            var actions = new VisualElement { name = name };
            actions.AddToClassList("pf-debugger__actions");
            return actions;
        }

        private static Button ActionButton(VisualElement actions, string name, string text, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text, tooltip = tooltip };
            button.AddToClassList("pf-debugger__action");
            actions.Add(button);
            return button;
        }
    }
}
