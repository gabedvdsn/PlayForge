using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Editor window that displays all tags and weights for GameplayAbilitySystem objects in the scene.
    /// Only functional during Play Mode.
    /// </summary>
    public class RuntimeTagViewer : EditorWindow
    {
        private Vector2 _scrollPosition;
        private Dictionary<GameplayAbilitySystem, bool> _foldoutStates = new();
        private float _refreshInterval = 0.2f;
        private double _lastRefreshTime;
        private List<GameplayAbilitySystem> _cachedSystems = new();
        
        [MenuItem("Tools/PlayForge/Runtime Tools/Tag Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<RuntimeTagViewer>("Runtime Tags");
            window.minSize = new Vector2(300, 200);
        }
        
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
        
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            _cachedSystems.Clear();
            _foldoutStates.Clear();
            Repaint();
        }
        
        private void OnInspectorUpdate()
        {
            // Refresh periodically during play mode
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                FindAllSystems();
                Repaint();
            }
        }
        
        private void OnGUI()
        {
            // Not in play mode
            if (!EditorApplication.isPlaying)
            {
                DrawNotPlayingMessage();
                return;
            }
            
            // Toolbar
            DrawToolbar();
            
            // Find systems
            RefreshSystems();
            
            if (_cachedSystems.Count == 0)
            {
                EditorGUILayout.HelpBox("No GameplayAbilitySystem objects found in scene.", MessageType.Info);
                return;
            }
            
            // Draw systems
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            foreach (var system in _cachedSystems)
            {
                if (system == null) continue;
                DrawSystemSection(system);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawNotPlayingMessage()
        {
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            
            EditorGUILayout.LabelField("Enter Play Mode to view runtime tags", style);
            
            EditorGUILayout.Space(10);
            
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
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _cachedSystems.Clear();
                RefreshSystems();
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"{_cachedSystems.Count} systems", EditorStyles.miniLabel, GUILayout.Width(70));
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void RefreshSystems()
        {
            if (_cachedSystems.Count == 0 || _cachedSystems.Any(s => s == null))
            {
                FindAllSystems();
            }
        }

        private void FindAllSystems()
        {
            _cachedSystems = Object.FindObjectsByType<GameplayAbilitySystem>(FindObjectsSortMode.None).ToList();
        }
        
        private void DrawSystemSection(GameplayAbilitySystem system)
        {
            // Ensure foldout state exists
            if (!_foldoutStates.ContainsKey(system))
                _foldoutStates[system] = true;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            
            _foldoutStates[system] = EditorGUILayout.Foldout(_foldoutStates[system], GUIContent.none, true);
            
            // Ping object on click
            if (GUILayout.Button(GetSystemName(system), EditorStyles.boldLabel))
            {
                EditorGUIUtility.PingObject(system);
                Selection.activeGameObject = system.gameObject;
            }
            
            GUILayout.FlexibleSpace();
            
            // Tag count badge
            var tags = system.GetAppliedTags();
            EditorGUILayout.LabelField($"{tags?.Count ?? 0} tags", EditorStyles.miniLabel, GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();
            
            // Content
            if (_foldoutStates[system])
            {
                EditorGUI.indentLevel++;
                DrawTagList(system, tags);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(2);
        }
        
        private string GetSystemName(GameplayAbilitySystem system)
        {
            // Try to get name from Data
            if (system.Data != null)
            {
                var name = system.Data.GetName();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            
            // Fallback to GameObject name
            return system.gameObject.name;
        }
        
        private void DrawTagList(GameplayAbilitySystem system, List<Tag> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                EditorGUILayout.LabelField("No tags", EditorStyles.miniLabel);
                return;
            }
            
            // Sort by name
            var sortedTags = tags.OrderBy(t => t.ToString()).ToList();
            
            // Draw header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tag", EditorStyles.miniBoldLabel, GUILayout.MinWidth(150));
            EditorGUILayout.LabelField("Weight", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // Draw separator
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            
            // Draw tags
            foreach (var tag in sortedTags)
            {
                DrawTagRow(system, tag);
            }
        }
        
        private void DrawTagRow(GameplayAbilitySystem system, Tag tag)
        {
            int weight = system.GetWeight(tag);
            
            EditorGUILayout.BeginHorizontal();
            
            // Tag name
            EditorGUILayout.LabelField(tag.ToString(), GUILayout.MinWidth(150));
            
            // Weight with color indicator
            var weightStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight
            };
            
            // Color based on weight
            if (weight > 1)
            {
                weightStyle.normal.textColor = new Color(0.4f, 0.7f, 0.4f); // Green for stacked
            }
            
            EditorGUILayout.LabelField(weight.ToString(), weightStyle, GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
