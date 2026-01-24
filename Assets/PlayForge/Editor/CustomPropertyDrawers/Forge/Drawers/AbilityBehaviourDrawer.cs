using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom property drawer for AbilityBehaviour with polished visual styling.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityBehaviour))]
    public class AbilityBehaviourDrawer : PropertyDrawer
    {
        // Collapse state persistence
        private static Dictionary<string, bool> _targetingCollapsed = new Dictionary<string, bool>();
        private static Dictionary<string, bool> _stagesCollapsed = new Dictionary<string, bool>();
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AbilityBehaviourRoot" };
            root.style.marginBottom = 4;
            
            BuildUI(root, property);
            
            return root;
        }
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            
            // ═══════════════════════════════════════════════════════════════════
            // Targeting Section
            // ═══════════════════════════════════════════════════════════════════
            var targetingSection = CreateTargetingSection(property, root);
            root.Add(targetingSection);
            
            // ═══════════════════════════════════════════════════════════════════
            // Stages Section
            // ═══════════════════════════════════════════════════════════════════
            var stagesSection = CreateStagesSection(property, root);
            root.Add(stagesSection);
        }
        
        private VisualElement CreateTargetingSection(SerializedProperty property, VisualElement root)
        {
            var propPath = property.propertyPath + ".Targeting";
            bool isCollapsed = _targetingCollapsed.TryGetValue(propPath, out bool c) && c;
            
            var container = new VisualElement { name = "TargetingSection" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = Colors.AccentCyan;
            container.style.borderTopLeftRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = Colors.SectionBackground;
            container.style.paddingTop = 6;
            container.style.paddingBottom = isCollapsed ? 6 : 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 6;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 6;
            
            // Collapse button
            var collapseBtn = CreateCollapseButton(isCollapsed, () =>
            {
                _targetingCollapsed[propPath] = !isCollapsed;
                ScheduleRebuild(root, property);
            });
            header.Add(collapseBtn);
            
            // Icon and title
            var iconLabel = new Label(Icons.Target);
            iconLabel.style.fontSize = 14;
            iconLabel.style.marginRight = 6;
            iconLabel.style.color = Colors.AccentCyan;
            header.Add(iconLabel);
            
            var titleLabel = new Label("Targeting");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            
            // Implicit targeting badge
            var implicitProp = property.FindPropertyRelative("UseImplicitTargeting");
            if (implicitProp != null && implicitProp.boolValue)
            {
                var badge = CreateBadge("Implicit", Colors.SectionCyan);
                badge.tooltip = "Uses casting system as implicit target";
                header.Add(badge);
            }
            
            container.Add(header);
            
            if (!isCollapsed)
            {
                // Targeting task field
                var targetingProp = property.FindPropertyRelative("Targeting");
                if (targetingProp != null)
                {
                    var targetingField = new PropertyField(targetingProp, "Targeting Task");
                    targetingField.style.marginBottom = 4;
                    container.Add(targetingField);
                }
                
                // Implicit targeting toggle
                if (implicitProp != null)
                {
                    var implicitField = new PropertyField(implicitProp, "Use Implicit Targeting");
                    implicitField.tooltip = "Implicitly provides the casting system as a target";
                    container.Add(implicitField);
                }
            }
            else
            {
                // Collapsed summary
                var targetingProp = property.FindPropertyRelative("Targeting");
                string summary = GetTargetingSummary(targetingProp, implicitProp);
                var summaryLabel = new Label(summary);
                summaryLabel.style.fontSize = 10;
                summaryLabel.style.color = Colors.HintText;
                summaryLabel.style.marginLeft = 22;
                summaryLabel.style.marginTop = 2;
                container.Add(summaryLabel);
            }
            
            return container;
        }
        
        private VisualElement CreateStagesSection(SerializedProperty property, VisualElement root)
        {
            var propPath = property.propertyPath + ".Stages";
            bool isCollapsed = _stagesCollapsed.TryGetValue(propPath, out bool c) && c;
            
            var container = new VisualElement { name = "StagesSection" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = Colors.AccentBlue;
            container.style.borderTopLeftRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = Colors.SectionBackground;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 6;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 6;
            
            // Collapse button
            var collapseBtn = CreateCollapseButton(isCollapsed, () =>
            {
                _stagesCollapsed[propPath] = !isCollapsed;
                ScheduleRebuild(root, property);
            });
            header.Add(collapseBtn);
            
            // Icon and title
            var iconLabel = new Label(Icons.Behavior);
            iconLabel.style.fontSize = 14;
            iconLabel.style.marginRight = 6;
            iconLabel.style.color = Colors.AccentBlue;
            header.Add(iconLabel);
            
            var titleLabel = new Label("Execution Stages");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            
            // Stage count badge
            var stagesProp = property.FindPropertyRelative("Stages");
            if (stagesProp != null)
            {
                int count = stagesProp.arraySize;
                var badge = CreateBadge($"{count} stage{(count != 1 ? "s" : "")}", Colors.SectionBlue);
                header.Add(badge);
            }
            
            container.Add(header);
            
            if (!isCollapsed && stagesProp != null)
            {
                // Stages list with custom rendering
                var stagesList = new VisualElement { name = "StagesList" };
                stagesList.style.marginTop = 4;
                
                for (int i = 0; i < stagesProp.arraySize; i++)
                {
                    var stageProp = stagesProp.GetArrayElementAtIndex(i);
                    var stageElement = new PropertyField(stageProp);
                    stageElement.style.marginBottom = 4;
                    stagesList.Add(stageElement);
                }
                
                // Add stage button
                var addButtonRow = new VisualElement();
                addButtonRow.style.flexDirection = FlexDirection.Row;
                addButtonRow.style.justifyContent = Justify.Center;
                addButtonRow.style.marginTop = 8;
                
                var addBtn = new Button { text = "+ Add Stage" };
                addBtn.style.paddingLeft = 16;
                addBtn.style.paddingRight = 16;
                addBtn.style.paddingTop = 4;
                addBtn.style.paddingBottom = 4;
                ApplyButtonStyle(addBtn);
                addBtn.clicked += () =>
                {
                    stagesProp.arraySize++;
                    stagesProp.serializedObject.ApplyModifiedProperties();
                    ScheduleRebuild(root, property);
                };
                addButtonRow.Add(addBtn);
                
                stagesList.Add(addButtonRow);
                container.Add(stagesList);
            }
            else if (isCollapsed && stagesProp != null)
            {
                // Collapsed summary
                var summary = GetStagesSummary(stagesProp);
                var summaryLabel = new Label(summary);
                summaryLabel.style.fontSize = 10;
                summaryLabel.style.color = Colors.HintText;
                summaryLabel.style.marginLeft = 22;
                summaryLabel.style.marginTop = 2;
                container.Add(summaryLabel);
            }
            
            return container;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateCollapseButton(bool isCollapsed, Action onClick)
        {
            var btn = new Button { text = isCollapsed ? "▶" : "▼", tooltip = isCollapsed ? "Expand" : "Collapse" };
            btn.style.width = 18;
            btn.style.height = 18;
            btn.style.marginRight = 4;
            btn.style.fontSize = 8;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonStyle(btn);
            btn.clicked += onClick;
            return btn;
        }
        
        private static void ApplyButtonStyle(Button btn)
        {
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        private string GetTargetingSummary(SerializedProperty targetingProp, SerializedProperty implicitProp)
        {
            var parts = new List<string>();
            
            if (targetingProp != null && targetingProp.managedReferenceValue != null)
            {
                var typeName = targetingProp.managedReferenceValue.GetType().Name;
                typeName = typeName.Replace("TargetingAbilityTask", "").Replace("Targeting", "");
                parts.Add(typeName);
            }
            else
            {
                parts.Add("No targeting task");
            }
            
            if (implicitProp != null && implicitProp.boolValue)
            {
                parts.Add("+ implicit self");
            }
            
            return string.Join(" ", parts);
        }
        
        private string GetStagesSummary(SerializedProperty stagesProp)
        {
            if (stagesProp.arraySize == 0)
                return "No stages defined";
            
            int totalTasks = 0;
            for (int i = 0; i < stagesProp.arraySize; i++)
            {
                var stageProp = stagesProp.GetArrayElementAtIndex(i);
                var tasksProp = stageProp.FindPropertyRelative("Tasks");
                if (tasksProp != null)
                    totalTasks += tasksProp.arraySize;
            }
            
            return $"{stagesProp.arraySize} stage(s), {totalTasks} total task(s)";
        }
        
        private void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            
            root.schedule.Execute(() =>
            {
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildUI(root, freshProp);
            });
        }
    }
}