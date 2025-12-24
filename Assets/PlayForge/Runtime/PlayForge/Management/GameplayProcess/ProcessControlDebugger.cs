#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProcessControlDebugger : EditorWindow
    {
        private EProcessDebuggerPolicy Policy = EProcessDebuggerPolicy.Simple;
        private Vector2 scrollPos;
        
        [MenuItem("Tools/PlayForge/Runtime Tools/Process Debugger", priority = 35)]
        public static void ShowWindow()
        {
            GetWindow<ProcessControlDebugger>("Process Debugger");
        }

        private void OnGUI()
        {
            if (ProcessControl.Instance is null)
            {
                EditorGUILayout.LabelField("ProcessControl not found.");
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"State: {ProcessControl.Instance.State}  |  {ProcessControl.Instance.Active}/{ProcessControl.Instance.Created}");

            Policy = (EProcessDebuggerPolicy)EditorGUILayout.EnumPopup("Weapon", Policy);
            
            EditorGUILayout.BeginHorizontal("box");
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.Ready);
            if (GUILayout.Button("Ready"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.Ready);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.Closed);
            if (GUILayout.Button("Closed"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.Closed);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal("box");
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.Waiting);
            if (GUILayout.Button("Waiting"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.Waiting);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.ClosedWaiting);
            if (GUILayout.Button("ClosedWaiting"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.ClosedWaiting);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal("box");
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.Terminated);
            if (GUILayout.Button("Terminated"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.Terminated);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State == EProcessControlState.TerminatedImmediately);
            if (GUILayout.Button("TerminatedImmediately"))
            {
                ProcessControl.Instance.SetControlState(EProcessControlState.TerminatedImmediately);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();

            switch (Policy)
            {

                case EProcessDebuggerPolicy.Minimum:
                    ShowMinimum();
                    break;
                case EProcessDebuggerPolicy.Simple:
                    ShowSimple();
                    break;
                case EProcessDebuggerPolicy.Full:
                    ShowFull();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ShowMinimum()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            try
            {
                foreach (var kvp in ProcessControl.Instance.FetchActiveProcesses())
                {
                    var relay = kvp.Value.Relay;
                    if (relay.Wrapper is null) continue;

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"{relay.Wrapper.ProcessName}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"ID: {relay.CacheIndex} | {relay.State} -> {relay.QueuedState} | {relay.UpdateTime:F2} / {relay.Lifetime:F2} ({relay.Wrapper.StepTiming})");
                    
                    EditorGUILayout.EndVertical();
                }
                
            }
            catch (InvalidOperationException) { }
            EditorGUILayout.EndScrollView();
        }

        private void ShowSimple()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            try
            {
                foreach (var kvp in ProcessControl.Instance.FetchActiveProcesses())
                {
                    var relay = kvp.Value.Relay;
                    if (relay.Wrapper is null) continue;

                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.LabelField($"{relay.Wrapper.ProcessName}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"ID: {relay.CacheIndex} | {relay.Wrapper.Lifecycle} | {relay.Wrapper.StepTiming}");
                    
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"State: {relay.State}");
                    EditorGUILayout.LabelField($"Queued: {relay.QueuedState}");
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"UpdateTime: {relay.UpdateTime:F2} seconds");
                    EditorGUILayout.LabelField($"Lifetime: {relay.Lifetime:F2} seconds");
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                }
            }
            catch (InvalidOperationException) { }
            EditorGUILayout.EndScrollView();
        }

        private void ShowFull()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            try
            {
                foreach (var kvp in ProcessControl.Instance.FetchActiveProcesses())
                {
                    var relay = kvp.Value.Relay;
                    if (relay.Wrapper is null) continue;

                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.LabelField($"{relay.Wrapper.ProcessName}\t", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"ID: {relay.CacheIndex} | {relay.Wrapper.Lifecycle} | {relay.Wrapper.StepTiming}");
                    
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"State: {relay.State}");
                    EditorGUILayout.LabelField($"Queued: {relay.QueuedState}");
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"UpdateTime: {relay.UpdateTime:F2} seconds");
                    EditorGUILayout.LabelField($"Lifetime: {relay.UnscaledLifetime:F2} seconds");
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUI.BeginDisabledGroup(relay.State == EProcessState.Waiting || relay.QueuedState == EProcessState.Waiting);
                    if (GUILayout.Button("Wait"))
                    {
                        ProcessControl.Instance.Wait(relay.CacheIndex);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(ProcessControl.Instance.State != EProcessControlState.Ready && ProcessControl.Instance.State != EProcessControlState.Closed || relay.State is EProcessState.Running or EProcessState.Terminated || relay.Wrapper.Lifecycle == EProcessLifecycle.SelfTerminating);
                    if (GUILayout.Button("Run"))
                    {
                        ProcessControl.Instance.Run(relay.CacheIndex);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(relay.State == EProcessState.Terminated || relay.QueuedState == EProcessState.Terminated);
                    if (GUILayout.Button("Terminate"))
                    {
                        ProcessControl.Instance.Terminate(relay.CacheIndex);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    
                    if (GUILayout.Button("Force Terminate"))
                    {
                        ProcessControl.Instance.TerminateImmediate(relay.CacheIndex);
                    }
                    
                    EditorGUILayout.EndVertical();
                }
            }
            catch (InvalidOperationException) { }
            EditorGUILayout.EndScrollView();
        }
        
        private void OnInspectorUpdate()
        {
            Repaint();
        }

    }

    public enum EProcessDebuggerPolicy
    {
        Minimum,
        Simple,
        Full
    }
}
#endif
