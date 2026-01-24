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
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        
        // Cached data
        private List<ProcessControlBlock> _filteredProcesses = new();
        private double _lastRefreshTime;
        private const float RefreshInterval = 0.1f;
        
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
                RefreshProcessList();
            }
            
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
                
                _filteredProcesses = all
                    .Where(pcb => pcb?.Relay?.Wrapper != null)
                    .Where(pcb => MatchesStateFilter(pcb))
                    .Where(pcb => MatchesSearchFilter(pcb))
                    .OrderBy(pcb => pcb.Relay.Wrapper.ProcessName)
                    .ToList();
            }
            catch (InvalidOperationException)
            {
                // Collection was modified during iteration
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
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel, GUILayout.MinWidth(120));
            
            GUILayout.FlexibleSpace();
            
            // Compact info
            var stateText = pcb.QueuedState != pcb.State 
                ? $"{pcb.State} → {pcb.QueuedState}" 
                : pcb.State.ToString();
            EditorGUILayout.LabelField(stateText, GUILayout.Width(150));
            EditorGUILayout.LabelField($"{relay.UpdateTime:F1}s", GUILayout.Width(50));
            EditorGUILayout.LabelField($"{relay.Lifetime:F1}s", GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawProcessSimple(ProcessControlBlock pcb)
        {
            var relay = pcb.Relay;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header row
            EditorGUILayout.BeginHorizontal();
            DrawStateIndicator(pcb.State, 14);
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField(relay.Wrapper.ProcessName, EditorStyles.boldLabel);
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
}
#endif