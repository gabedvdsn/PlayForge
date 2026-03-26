#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProcessControlDebugger : EditorWindow
    {
        // View Settings
        private EProcessDebuggerPolicy _viewPolicy = EProcessDebuggerPolicy.Simple;
        private EProcessStateFilter _stateFilter = EProcessStateFilter.All;
        private EProcessSortMode _sortMode = EProcessSortMode.CacheIndex;
        private EUsageSortMode _usageMode = EUsageSortMode.Frame;
        private bool _showUsage = false;
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        
        // Cached data
        private List<ProcessControlBlock> _filteredProcesses = new();
        private double _lastRefreshTime;
        private const float RefreshInterval = 0.1f;
        
        // Usage tracking
        private Dictionary<int, float> _previousUpdateTimes = new();
        private Dictionary<int, float> _recentDeltas = new();
        private float _totalCumulativeUpdateTime;
        private float _totalRecentDelta;
        
        // Usage bar constants
        private const float UsageBarWidth = 90f;
        private const float UsageBarHeight = 12f;
        private static readonly Color UsageBarBgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        
        // State colors
        private static readonly Color RunningColor = new Color(0.4f, 0.7f, 0.4f);
        private static readonly Color WaitingColor = new Color(0.7f, 0.6f, 0.3f);
        private static readonly Color TerminatedColor = new Color(0.7f, 0.4f, 0.4f);
        private static readonly Color CreatedColor = new Color(0.5f, 0.5f, 0.7f);
        
        [MenuItem("Tools/PlayForge/Runtime Tools/Process Control")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProcessControlDebugger>("Process Debugger");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                DrawNotPlayingMessage();
                return;
            }
            
            if (ProcessControl.Instance == null)
            {
                EditorGUILayout.HelpBox("ProcessControl not found in scene.", MessageType.Warning);
                return;
            }

            DrawControlStateSection();
            EditorGUILayout.Space(4);
            DrawToolbar();
            DrawOptionsBar();
            EditorGUILayout.Space(2);
            DrawProcessList();
        }

        private void DrawNotPlayingMessage()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Enter Play Mode to debug processes", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(8);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enter Play Mode", GUILayout.Width(120)))
            {
                EditorApplication.isPlaying = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Control State Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DrawControlStateSection()
        {
            var pc = ProcessControl.Instance;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header with stats
            EditorGUILayout.BeginHorizontal();
            
            // State indicator with color
            var stateColor = GetControlStateColor(pc.State);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = stateColor;
            EditorGUILayout.LabelField($"State: {pc.State}", EditorStyles.boldLabel, GUILayout.Width(180));
            GUI.backgroundColor = prevBg;
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"Running: {pc.Running}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"Active: {pc.Active}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"Total: {pc.Created}", GUILayout.Width(70));
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(4);
            
            // State buttons - Row 1: Normal states
            EditorGUILayout.BeginHorizontal();
            DrawStateButton("Ready", EProcessControlState.Ready, 
                "Accept new processes and run all", GetControlStateColor(EProcessControlState.Ready));
            DrawStateButton("Closed", EProcessControlState.Closed, 
                "Don't accept new processes, run existing", GetControlStateColor(EProcessControlState.Closed));
            DrawStateButton("Waiting", EProcessControlState.Waiting, 
                "Accept new but pause all processes", GetControlStateColor(EProcessControlState.Waiting));
            DrawStateButton("ClosedWaiting", EProcessControlState.ClosedWaiting, 
                "Don't accept new, pause all existing", GetControlStateColor(EProcessControlState.ClosedWaiting));
            EditorGUILayout.EndHorizontal();
            
            // State buttons - Row 2: Termination states
            EditorGUILayout.BeginHorizontal();
            DrawStateButton("Terminate", EProcessControlState.Terminated, 
                "Gracefully terminate all processes", new Color(1f, 0.5f, 0.5f), true);
            DrawStateButton("Terminate Immediately", EProcessControlState.TerminatedImmediately, 
                "Force terminate all processes NOW", Color.red, true);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStateButton(string label, EProcessControlState state, string tooltip, Color tint, bool dangerous = false)
        {
            var pc = ProcessControl.Instance;
            bool isCurrent = pc.State == state;
            
            EditorGUI.BeginDisabledGroup(isCurrent);
            
            var prevBg = GUI.backgroundColor;
            if (isCurrent)
                GUI.backgroundColor = tint;
            
            var content = new GUIContent(label, tooltip);
            
            if (GUILayout.Button(content))
            {
                if (dangerous)
                {
                    if (EditorUtility.DisplayDialog(
                        "Confirm State Change",
                        $"Are you sure you want to change state to {state}?\n\nThis will affect {pc.Active} active processes.",
                        "Yes", "Cancel"))
                    {
                        pc.SetControlState(state);
                    }
                }
                else
                {
                    pc.SetControlState(state);
                }
            }
            
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();
        }
        
        private Color GetControlStateColor(EProcessControlState state)
        {
            return state switch
            {
                EProcessControlState.Ready => Color.green,
                EProcessControlState.Waiting => Color.yellow,
                EProcessControlState.Closed => Color.gray,
                EProcessControlState.ClosedWaiting => (Color.yellow + Color.gray) / 2f,
                EProcessControlState.Terminated => new Color(1f, 0.5f, 0.5f),
                EProcessControlState.TerminatedImmediately => Color.red,
                _ => Color.white
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Toolbar
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // View policy
            EditorGUILayout.LabelField("View:", GUILayout.Width(35));
            _viewPolicy = (EProcessDebuggerPolicy)EditorGUILayout.EnumPopup(_viewPolicy, EditorStyles.toolbarDropDown, GUILayout.Width(80));
            
            EditorGUILayout.Space(8);
            
            // State filter
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(38));
            _stateFilter = (EProcessStateFilter)EditorGUILayout.EnumPopup(_stateFilter, EditorStyles.toolbarDropDown, GUILayout.Width(80));
            
            // Search
            EditorGUILayout.Space(8);
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(100));
            
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }
            
            GUILayout.FlexibleSpace();
            
            // Refresh button
            if (GUILayout.Button("↻", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                _lastRefreshTime = 0;
                RefreshProcessList();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawOptionsBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Sort mode
            EditorGUILayout.LabelField("Sort:", GUILayout.Width(32));
            _sortMode = (EProcessSortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarDropDown, GUILayout.Width(110));
            
            EditorGUILayout.Space(12);
            
            // Usage toggle
            _showUsage = GUILayout.Toggle(_showUsage, new GUIContent("Usage", "Show update time usage per process"), EditorStyles.toolbarButton, GUILayout.Width(50));
            if (_showUsage)
            {
                _usageMode = (EUsageSortMode)EditorGUILayout.EnumPopup(_usageMode, EditorStyles.toolbarDropDown, GUILayout.Width(90));
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Process List
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DrawProcessList()
        {
            RefreshProcessList();
            
            if (_filteredProcesses.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _stateFilter == EProcessStateFilter.All && string.IsNullOrEmpty(_searchFilter)
                        ? "No active processes."
                        : "No processes match the current filter.",
                    MessageType.Info);
                return;
            }
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            foreach (var pcb in _filteredProcesses)
            {
                if (pcb?.Relay?.Wrapper == null) continue;
                
                switch (_viewPolicy)
                {
                    case EProcessDebuggerPolicy.Minimum:
                        DrawProcessMinimum(pcb);
                        break;
                    case EProcessDebuggerPolicy.Simple:
                        DrawProcessSimple(pcb);
                        break;
                    case EProcessDebuggerPolicy.Full:
                        DrawProcessFull(pcb);
                        break;
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void RefreshProcessList()
        {
            if (ProcessControl.Instance == null) return;
            
            // Throttle refresh
            if (EditorApplication.timeSinceStartup - _lastRefreshTime < RefreshInterval)
                return;
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            
            try
            {
                var all = ProcessControl.Instance.FetchActiveProcesses().Values;
                
                var valid = all
                    .Where(pcb => pcb?.Relay?.Wrapper != null)
                    .Where(pcb => MatchesStateFilter(pcb))
                    .Where(pcb => MatchesSearchFilter(pcb));
                
                // Materialize before sorting (needed for usage computation)
                var validList = valid.ToList();
                
                // Compute usage data (needed for usage sort modes and usage display)
                bool needsUsage = _showUsage 
                    || _sortMode is EProcessSortMode.RelativeUse or EProcessSortMode.RecentUse or EProcessSortMode.OverallUse;
                if (needsUsage)
                {
                    _filteredProcesses = validList;
                    ComputeUsageData();
                }
                
                // Apply sort
                _filteredProcesses = ApplySort(validList).ToList();
            }
            catch (InvalidOperationException)
            {
                // Collection was modified during iteration
            }
        }
        
        private IEnumerable<ProcessControlBlock> ApplySort(IEnumerable<ProcessControlBlock> source)
        {
            return _sortMode switch
            {
                EProcessSortMode.CacheIndex => source.OrderBy(pcb => pcb.Relay.CacheIndex),
                EProcessSortMode.Name => source.OrderBy(pcb => pcb.Relay.Wrapper.ProcessName, StringComparer.OrdinalIgnoreCase),
                EProcessSortMode.Handler => source.OrderBy(pcb => GetHandlerName(pcb), StringComparer.OrdinalIgnoreCase)
                                                   .ThenBy(pcb => pcb.Relay.Wrapper.ProcessName, StringComparer.OrdinalIgnoreCase),
                EProcessSortMode.RelativeUse => source.OrderByDescending(pcb => pcb.Relay.UpdateTime),
                EProcessSortMode.RecentUse => source.OrderByDescending(pcb => 
                    _recentDeltas.TryGetValue(pcb.Relay.CacheIndex, out var d) ? d : 0f),
                EProcessSortMode.OverallUse => source.OrderByDescending(pcb => pcb.Relay.UpdateTime),
                _ => source.OrderBy(pcb => pcb.Relay.CacheIndex)
            };
        }
        
        private static string GetHandlerName(ProcessControlBlock pcb)
        {
            if (pcb.Handler == null) return "(none)";
            
            try
            {
                var name = pcb.Handler.GetName();
                return string.IsNullOrEmpty(name) ? pcb.Handler.GetType().Name : name;
            }
            catch
            {
                return pcb.Handler.GetType().Name;
            }
        }
        
        private void ComputeUsageData()
        {
            var newDeltas = new Dictionary<int, float>();
            float totalCumulative = 0f;
            float totalDelta = 0f;
            
            foreach (var pcb in _filteredProcesses)
            {
                int id = pcb.Relay.CacheIndex;
                float currentUpdateTime = pcb.Relay.UpdateTime;
                
                // Cumulative
                totalCumulative += currentUpdateTime;
                
                // Delta since last refresh
                float previousTime = _previousUpdateTimes.TryGetValue(id, out var prev) ? prev : currentUpdateTime;
                float delta = Mathf.Max(0f, currentUpdateTime - previousTime);
                newDeltas[id] = delta;
                totalDelta += delta;
                
                // Store current for next frame
                _previousUpdateTimes[id] = currentUpdateTime;
            }
            
            _recentDeltas = newDeltas;
            _totalCumulativeUpdateTime = totalCumulative;
            _totalRecentDelta = totalDelta;
            
            // Clean out stale entries from _previousUpdateTimes
            var activeIds = new HashSet<int>(_filteredProcesses.Select(pcb => pcb.Relay.CacheIndex));
            var staleKeys = _previousUpdateTimes.Keys.Where(k => !activeIds.Contains(k)).ToList();
            foreach (var key in staleKeys)
            {
                _previousUpdateTimes.Remove(key);
            }
        }
        
        private bool MatchesStateFilter(ProcessControlBlock pcb)
        {
            return _stateFilter switch
            {
                EProcessStateFilter.All => true,
                EProcessStateFilter.Running => pcb.State == EProcessState.Running,
                EProcessStateFilter.Waiting => pcb.State == EProcessState.Waiting,
                EProcessStateFilter.Terminated => pcb.State == EProcessState.Terminated,
                _ => true
            };
        }
        
        private bool MatchesSearchFilter(ProcessControlBlock pcb)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return pcb.Relay.Wrapper.ProcessName.ToLower().Contains(_searchFilter.ToLower());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Process Views
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DrawProcessMinimum(ProcessControlBlock pcb)
        {
            var relay = pcb.Relay;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // State indicator
            DrawStateIndicator(pcb.State, 12);
            
            // Name
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel, GUILayout.MinWidth(200));
            
            GUILayout.FlexibleSpace();
            
            // Compact info
            var stateText = pcb.QueuedState != pcb.State 
                ? $"{pcb.State} → {pcb.QueuedState}" 
                : pcb.State.ToString();
            EditorGUILayout.LabelField(stateText, GUILayout.Width(150));
            EditorGUILayout.LabelField($"{relay.UpdateTime:F1}s", GUILayout.Width(50));
            EditorGUILayout.LabelField($"{relay.Lifetime:F1}s", GUILayout.Width(50));
            
            if (_showUsage) DrawUsageInline(pcb);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawProcessSimple(ProcessControlBlock pcb)
        {
            var relay = pcb.Relay;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header row
            EditorGUILayout.BeginHorizontal();
            DrawStateIndicator(pcb.State, 14);
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel, GUILayout.MinWidth(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"ID: {relay.CacheIndex}", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // Info row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{relay.Wrapper.Lifecycle}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"{relay.Wrapper.StepTiming}", GUILayout.Width(120));
            
            GUILayout.FlexibleSpace();
            
            // State with queued indicator
            var stateLabel = pcb.QueuedState != pcb.State 
                ? $"{pcb.State} → {pcb.QueuedState}" 
                : pcb.State.ToString();
            EditorGUILayout.LabelField(stateLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            // Time row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Update: {relay.UpdateTime:F2}s", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Lifetime: {relay.Lifetime:F2}s", GUILayout.Width(110));
            
            if (_showUsage)
            {
                GUILayout.FlexibleSpace();
                DrawUsageInline(pcb);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawProcessFull(ProcessControlBlock pcb)
        {
            var relay = pcb.Relay;
            var pc = ProcessControl.Instance;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            DrawStateIndicator(pcb.State, 16);
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel, GUILayout.MinWidth(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"ID: {relay.CacheIndex}", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // Properties
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Lifecycle: {relay.Wrapper.Lifecycle}", GUILayout.Width(160));
            EditorGUILayout.LabelField($"Timing: {relay.Wrapper.StepTiming}", GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
            
            // State info
            EditorGUILayout.BeginHorizontal();
            
            var stateStyle = new GUIStyle(EditorStyles.label);
            stateStyle.normal.textColor = GetProcessStateColor(pcb.State);
            EditorGUILayout.LabelField($"State: {pcb.State}", stateStyle, GUILayout.Width(100));
            
            if (pcb.QueuedState != pcb.State)
            {
                var queuedStyle = new GUIStyle(EditorStyles.label);
                queuedStyle.normal.textColor = GetProcessStateColor(pcb.QueuedState);
                EditorGUILayout.LabelField($"→", queuedStyle, GUILayout.Width(25));
                EditorGUILayout.LabelField($"{pcb.QueuedState}", queuedStyle, GUILayout.Width(100));
            }
            else
            {
                EditorGUILayout.LabelField("(no change queued)", EditorStyles.miniLabel, GUILayout.Width(100));
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Timing
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Update Time: {relay.UpdateTime:F2}s", GUILayout.Width(140));
            EditorGUILayout.LabelField($"Lifetime: {relay.UnscaledLifetime:F2}s", GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            
            // Usage row (full view gets its own dedicated row)
            if (_showUsage)
            {
                DrawUsageRow(pcb);
            }
            
            EditorGUILayout.Space(2);
            
            // Control buttons
            EditorGUILayout.BeginHorizontal();
            
            // Wait button
            bool canWait = pcb.State == EProcessState.Running && pcb.QueuedState != EProcessState.Waiting;
            EditorGUI.BeginDisabledGroup(!canWait);
            if (GUILayout.Button("Pause", GUILayout.Height(20)))
            {
                pc.Wait(relay.CacheIndex);
            }
            EditorGUI.EndDisabledGroup();
            
            // Run button
            bool canRun = pcb.State == EProcessState.Waiting && 
                          pcb.QueuedState != EProcessState.Running &&
                          pc.State is EProcessControlState.Ready or EProcessControlState.Closed;
            EditorGUI.BeginDisabledGroup(!canRun);
            if (GUILayout.Button("Resume", GUILayout.Height(20)))
            {
                pc.Run(relay.CacheIndex);
            }
            EditorGUI.EndDisabledGroup();
            
            // Terminate button
            bool canTerminate = pcb.State != EProcessState.Terminated && pcb.QueuedState != EProcessState.Terminated;
            EditorGUI.BeginDisabledGroup(!canTerminate);
            if (GUILayout.Button("Terminate", GUILayout.Height(20)))
            {
                pc.Terminate(relay.CacheIndex);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            // Force terminate (always available for non-terminated)
            EditorGUI.BeginDisabledGroup(pcb.State == EProcessState.Terminated);
            if (GUILayout.Button("Force Terminate", GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog(
                    "Force Terminate",
                    $"Force terminate '{relay.Wrapper.ProcessName}'?\n\nThis may cause unexpected behavior.",
                    "Force Terminate", "Cancel"))
                {
                    pc.TerminateImmediate(relay.CacheIndex);
                }
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Usage Visualization
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Compact inline usage: "Σ 12% ▸ 8%  [████░░░░░░]" — used in Minimum and Simple views.
        /// </summary>
        private void DrawUsageInline(ProcessControlBlock pcb)
        {
            GetUsagePercents(pcb, out float cumulativePct, out float recentPct, out float overallPct, out float targetPct);
            
            var usageLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            usageLabelStyle.alignment = TextAnchor.MiddleRight;
            
            EditorGUILayout.LabelField($"\u03A3{cumulativePct:F0}%", usageLabelStyle, GUILayout.Width(34));
            EditorGUILayout.LabelField($"\u25B8{recentPct:F0}%", usageLabelStyle, GUILayout.Width(34));
            EditorGUILayout.LabelField($"\u25CB{overallPct:F1}%", usageLabelStyle, GUILayout.Width(44));
            
            // Bar (uses recent % for the fill, since that's the more actionable metric)
            DrawUsageBar(targetPct);
        }
        
        /// <summary>
        /// Full-width usage row with labels — used in Full view.
        /// </summary>
        private void DrawUsageRow(ProcessControlBlock pcb)
        {
            GetUsagePercents(pcb, out float cumulativePct, out float recentPct, out float overallPct, out float targetPct);
            
            EditorGUILayout.BeginHorizontal();
            
            var labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            
            EditorGUILayout.LabelField("Usage:", labelStyle, GUILayout.Width(40));
            EditorGUILayout.LabelField($"Relative: {cumulativePct:F1}%", labelStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField($"Frame: {recentPct:F1}%", labelStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField($"Overall: {overallPct:F2}%", labelStyle, GUILayout.Width(86));
            
            DrawUsageBar(targetPct);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void GetUsagePercents(ProcessControlBlock pcb, out float cumulativePct, out float recentPct, out float overallPct, out float targetPct)
        {
            int id = pcb.CacheIndex;
            float updateTime = pcb.TotalUpdateTime;
            
            // Cumulative: this process's total update time / sum of all processes' total update time
            cumulativePct = _totalCumulativeUpdateTime > 0f
                ? (updateTime / _totalCumulativeUpdateTime) * 100f
                : 0f;
            
            // Recent: this process's delta since last refresh / total delta across all processes
            float delta = _recentDeltas.TryGetValue(id, out var d) ? d : 0f;
            recentPct = _totalRecentDelta > 0f
                ? (delta / _totalRecentDelta) * 100f
                : 0f;
            
            // Overall: this process's total update time / total wall-clock time since start
            float totalElapsed = Time.realtimeSinceStartup;
            overallPct = totalElapsed > 0f
                ? (updateTime / totalElapsed) * 100f
                : 0f;

            targetPct = _usageMode switch
            {
                EUsageSortMode.Relative => cumulativePct,
                EUsageSortMode.Frame => recentPct,
                EUsageSortMode.Overall => overallPct,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /// <summary>
        /// Draws a fixed-width horizontal bar that fills proportionally to the percentage.
        /// Color interpolates from green (low) through yellow (mid) to red (high).
        /// </summary>
        private void DrawUsageBar(float percent)
        {
            var rect = GUILayoutUtility.GetRect(UsageBarWidth, UsageBarHeight, 
                GUILayout.Width(UsageBarWidth), GUILayout.Height(UsageBarHeight));
            
            // Vertically center within the line
            rect.y += (EditorGUIUtility.singleLineHeight - UsageBarHeight) / 2f;
            
            // Background
            EditorGUI.DrawRect(rect, UsageBarBgColor);
            
            // Fill
            float t = Mathf.Clamp01(percent / 100f);
            if (t > 0.001f)
            {
                var fillRect = new Rect(rect.x, rect.y, rect.width * t, rect.height);
                EditorGUI.DrawRect(fillRect, GetUsageColor(t));
            }
            
            // Border
            DrawRectBorder(rect, new Color(0.3f, 0.3f, 0.3f, 0.6f));
        }
        
        /// <summary>
        /// Green at 0%, yellow at ~35%, red at 100%.
        /// </summary>
        private static Color GetUsageColor(float t)
        {
            float min = .35f;
            float max = .65f;

            switch (t)
            {
                // Two-stop gradient: green -> yellow -> red
                case < 0.35f:
                {
                    float localT = t / 0.25f;
                    return Color.Lerp(
                        new Color(0.3f, 0.75f, 0.3f),  // green
                        new Color(0.85f, 0.75f, 0.2f),  // yellow
                        localT);
                }
                case <= .75f:
                {
                    float localT = (t - 0.35f) / 0.75f;
                    return Color.Lerp(
                        new Color(0.85f, 0.75f, 0.2f),  // yellow
                        new Color(1f, 0.4f, 0.1f),  // red
                        localT);
                }
                case > .75f:
                {
                    float localT = (t - .75f) / 1f;
                    return Color.Lerp(
                        new Color(1f, 0.4f, 0.1f),  // yellow
                        new Color(0.85f, 0.25f, 0.2f),  // red
                        localT);
                }
                default:
                {
                    return Color.cyan;
                }
            }
        }
        
        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);                 // top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);          // bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);                // left
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);         // right
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DrawStateIndicator(EProcessState state, float size)
        {
            var color = GetProcessStateColor(state);
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            rect.y += (EditorGUIUtility.singleLineHeight - size) / 2;
            
            EditorGUI.DrawRect(rect, color);
        }
        
        private Color GetProcessStateColor(EProcessState state)
        {
            return state switch
            {
                EProcessState.Running => RunningColor,
                EProcessState.Waiting => WaitingColor,
                EProcessState.Terminated => TerminatedColor,
                EProcessState.Created => CreatedColor,
                _ => Color.gray
            };
        }
        
        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }
    }

    public enum EProcessDebuggerPolicy
    {
        Minimum,
        Simple,
        Full
    }
    
    public enum EProcessStateFilter
    {
        All,
        Running,
        Waiting,
        Terminated
    }
    
    public enum EProcessSortMode
    {
        CacheIndex,
        Name,
        Handler,
        RelativeUse,
        RecentUse,
        OverallUse
    }

    public enum EUsageSortMode
    {
        Relative,
        Frame,
        Overall
    }
}
#endif