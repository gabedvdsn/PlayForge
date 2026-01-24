using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom property drawer for AbilityTaskBehaviourStage with polished visual styling.
    /// Each stage gets a distinct visual treatment with collapsible tasks.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityTaskBehaviourStage))]
    public class AbilityTaskBehaviourStageDrawer : PropertyDrawer
    {
        // Stage accent colors (cycle through for visual distinction)
        private static readonly Color[] StageColors = new Color[]
        {
            Colors.AccentBlue,
            Colors.AccentGreen,
            Colors.AccentOrange,
            Colors.AccentPurple,
            Colors.AccentCyan,
            Colors.AccentYellow
        };
        
        // Collapse state persistence
        private static Dictionary<string, bool> _collapsedStates = new Dictionary<string, bool>();
        
        private static bool IsCollapsed(string propertyPath)
        {
            return _collapsedStates.TryGetValue(propertyPath, out bool collapsed) && collapsed;
        }
        
        private static void SetCollapsed(string propertyPath, bool collapsed)
        {
            _collapsedStates[propertyPath] = collapsed;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "StageRoot" };
            root.style.marginBottom = 6;
            
            BuildStageUI(root, property);
            
            return root;
        }
        
        private void BuildStageUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            
            int stageIndex = GetStageIndex(property.propertyPath);
            Color stageColor = StageColors[stageIndex % StageColors.Length];
            bool isCollapsed = IsCollapsed(property.propertyPath);
            
            // Main container
            var container = new VisualElement { name = "StageContainer" };
            container.style.borderLeftWidth = 4;
            container.style.borderLeftColor = stageColor;
            container.style.borderTopLeftRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f, 0.8f);
            container.style.paddingTop = 8;
            container.style.paddingBottom = isCollapsed ? 8 : 10;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 8;
            container.style.marginTop = 2;
            
            // Header
            container.Add(CreateStageHeader(property, root, stageIndex, stageColor, isCollapsed));
            
            if (!isCollapsed)
            {
                // Stage content
                container.Add(CreateStageContent(property, root, stageColor));
            }
            else
            {
                // Collapsed summary
                container.Add(CreateCollapsedSummary(property));
            }
            
            root.Add(container);
            
            // Bind after building to ensure all PropertyFields work
            root.Bind(property.serializedObject);
        }
        
        private VisualElement CreateStageHeader(SerializedProperty property, VisualElement root, 
            int stageIndex, Color stageColor, bool isCollapsed)
        {
            var header = new VisualElement { name = "StageHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 8;
            
            // Collapse button
            var collapseBtn = new Button { text = isCollapsed ? "▶" : "▼", tooltip = isCollapsed ? "Expand" : "Collapse" };
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.marginRight = 6;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonStyle(collapseBtn);
            collapseBtn.clicked += () =>
            {
                SetCollapsed(property.propertyPath, !isCollapsed);
                ScheduleRebuild(root, property);
            };
            header.Add(collapseBtn);
            
            // Stage number badge (smaller - 20px)
            var stageBadge = new VisualElement();
            stageBadge.style.width = 20;
            stageBadge.style.height = 20;
            stageBadge.style.borderTopLeftRadius = 10;
            stageBadge.style.borderTopRightRadius = 10;
            stageBadge.style.borderBottomLeftRadius = 10;
            stageBadge.style.borderBottomRightRadius = 10;
            stageBadge.style.backgroundColor = stageColor;
            stageBadge.style.alignItems = Align.Center;
            stageBadge.style.justifyContent = Justify.Center;
            stageBadge.style.marginRight = 6;
            
            var stageNumber = new Label((stageIndex + 1).ToString());
            stageNumber.style.fontSize = 10;
            stageNumber.style.unityFontStyleAndWeight = FontStyle.Bold;
            stageNumber.style.color = Color.white;
            stageNumber.style.unityTextAlign = TextAnchor.MiddleCenter;
            stageBadge.Add(stageNumber);
            header.Add(stageBadge);
            
            // Stage title
            var titleLabel = new Label($"Stage {stageIndex + 1}");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            
            // Policy badge
            var stagePolicyProp = property.FindPropertyRelative("StagePolicy");
            if (stagePolicyProp != null && stagePolicyProp.managedReferenceValue != null)
            {
                var policyName = GetPolicyDisplayName(stagePolicyProp);
                var policyBadge = CreatePolicyBadge(policyName);
                header.Add(policyBadge);
            }
            
            // Apply usage effects indicator
            var applyUsageProp = property.FindPropertyRelative("ApplyUsageEffects");
            if (applyUsageProp != null && applyUsageProp.boolValue)
            {
                var usageBadge = CreateBadge("Usage", Colors.SectionYellow);
                usageBadge.tooltip = "Applies cost and cooldown at the beginning of this stage";
                usageBadge.style.marginLeft = 4;
                header.Add(usageBadge);
            }
            
            // Task count badge
            var tasksProp = property.FindPropertyRelative("Tasks");
            if (tasksProp != null)
            {
                int taskCount = tasksProp.arraySize;
                var taskBadge = CreateBadge($"{taskCount} task{(taskCount != 1 ? "s" : "")}");
                taskBadge.style.marginLeft = 4;
                header.Add(taskBadge);
            }
            
            // Delete button
            var deleteBtn = new Button { text = "×", tooltip = "Delete this stage" };
            deleteBtn.style.width = 20;
            deleteBtn.style.height = 20;
            deleteBtn.style.marginLeft = 8;
            deleteBtn.style.fontSize = 12;
            deleteBtn.style.color = Colors.AccentRed;
            ApplyButtonStyle(deleteBtn);
            deleteBtn.clicked += () => DeleteStage(property, root);
            header.Add(deleteBtn);
            
            return header;
        }
        
        private VisualElement CreateStageContent(SerializedProperty property, VisualElement root, Color stageColor)
        {
            var content = new VisualElement { name = "StageContent" };

            var settings = CreateRow(wrap: true);
            //settings.style.backgroundColor = Colors.SubsectionBackground;
            settings.style.flexDirection = FlexDirection.Column;

            var policyRow = CreateRow();
            policyRow.style.flexGrow = 1;
            policyRow.style.marginRight = 12;
            policyRow.style.marginLeft = 8;
            
            policyRow.Add(CreateFieldLabel("Completion Policy"));
            var stagePolicyProp = property.FindPropertyRelative("StagePolicy");
            if (stagePolicyProp != null)
            {
                var policyField = new PropertyField(stagePolicyProp, "Completion Policy");
                policyField = CreatePropertyField(stagePolicyProp, "StagePolicy", "Completion Policy");
                policyField.style.minHeight = 20;
                policyField.style.flexGrow = 1;
                policyField.style.marginLeft = 24;
                policyRow.Add(policyField);
            }
            settings.Add(policyRow);
            
            // Apply Usage Effects - use PropertyField with label
            var applyUsageContainer = CreateRow();
            applyUsageContainer.style.flexGrow = 1;
            applyUsageContainer.style.marginRight = 18;
            applyUsageContainer.style.marginLeft = 8;
            
            applyUsageContainer.Add(CreateFieldLabel("Apply Usage Effects"));
            var applyUsageProp = property.FindPropertyRelative("ApplyUsageEffects");
            if (applyUsageProp != null)
            {
                var usageField = new PropertyField(applyUsageProp, "");
                usageField.tooltip = "After this stage completes, apply the ability's cost and cooldown";
                usageField.style.minHeight = 20;
                usageField.style.marginLeft = 10;
                applyUsageContainer.Add(usageField);
            }
            settings.Add(applyUsageContainer);
            
            content.Add(settings);
            
            // Tasks section
            var tasksSection = CreateTasksSection(property, root, stageColor);
            content.Add(tasksSection);
            
            return content;
        }
        
        private VisualElement CreateTasksSection(SerializedProperty property, VisualElement root, Color stageColor)
        {
            var section = new VisualElement { name = "TasksSection" };
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = new Color(stageColor.r, stageColor.g, stageColor.b, 0.4f);
            section.style.paddingLeft = 8;
            section.style.marginLeft = 4;
            section.style.marginTop = 4;
            
            // Tasks header
            var tasksHeader = new VisualElement();
            tasksHeader.style.flexDirection = FlexDirection.Row;
            tasksHeader.style.alignItems = Align.Center;
            tasksHeader.style.marginBottom = 6;
            
            var tasksLabel = new Label("TASKS");
            tasksLabel.style.fontSize = 9;
            tasksLabel.style.letterSpacing = 1;
            tasksLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tasksLabel.style.color = Colors.SectionTitle;
            tasksHeader.Add(tasksLabel);
            
            section.Add(tasksHeader);
            
            // Tasks list
            var tasksProp = property.FindPropertyRelative("Tasks");
            if (tasksProp != null)
            {
                var tasksList = new VisualElement { name = "TasksList" };
                
                if (tasksProp.arraySize == 0)
                {
                    var emptyLabel = new Label("No tasks defined. Add a task to define stage behavior.");
                    emptyLabel.style.fontSize = 10;
                    emptyLabel.style.color = Colors.HintText;
                    emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    emptyLabel.style.marginBottom = 6;
                    emptyLabel.style.marginLeft = 4;
                    tasksList.Add(emptyLabel);
                }
                else
                {
                    for (int i = 0; i < tasksProp.arraySize; i++)
                    {
                        var taskProp = tasksProp.GetArrayElementAtIndex(i);
                        var taskElement = CreateTaskElement(taskProp, tasksProp, i, root, property, stageColor);
                        tasksList.Add(taskElement);
                    }
                }
                
                section.Add(tasksList);
                
                // Add task button
                var addButtonRow = new VisualElement();
                addButtonRow.style.flexDirection = FlexDirection.Row;
                addButtonRow.style.marginTop = 6;
                
                var addBtn = new Button { text = "+ Add Task" };
                addBtn.style.paddingLeft = 12;
                addBtn.style.paddingRight = 12;
                addBtn.style.paddingTop = 3;
                addBtn.style.paddingBottom = 3;
                addBtn.style.fontSize = 10;
                ApplyButtonStyle(addBtn);
                addBtn.clicked += () =>
                {
                    tasksProp.arraySize++;
                    tasksProp.serializedObject.ApplyModifiedProperties();
                    ScheduleRebuild(root, property);
                };
                addButtonRow.Add(addBtn);
                
                section.Add(addButtonRow);
            }
            
            return section;
        }
        
        private VisualElement CreateTaskElement(SerializedProperty taskProp, SerializedProperty tasksProp, 
            int index, VisualElement root, SerializedProperty stageProp, Color stageColor)
        {
            var taskContainer = new VisualElement { name = $"Task_{index}" };
            taskContainer.style.flexDirection = FlexDirection.Row;
            taskContainer.style.alignItems = Align.FlexStart;
            taskContainer.style.marginBottom = 4;
            taskContainer.style.paddingTop = 6;
            taskContainer.style.paddingBottom = 6;
            taskContainer.style.paddingLeft = 6;
            taskContainer.style.paddingRight = 4;
            taskContainer.style.backgroundColor = Colors.ItemBackground;
            taskContainer.style.borderTopLeftRadius = 4;
            taskContainer.style.borderTopRightRadius = 4;
            taskContainer.style.borderBottomLeftRadius = 4;
            taskContainer.style.borderBottomRightRadius = 4;
            
            // Task index indicator
            var indexLabel = new Label($"{index + 1}.");
            indexLabel.style.width = 18;
            indexLabel.style.fontSize = 10;
            indexLabel.style.color = Colors.HintText;
            indexLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            indexLabel.style.marginRight = 4;
            indexLabel.style.marginTop = 2;
            taskContainer.Add(indexLabel);
            
            // Task content area - this will contain the AbilityTaskDrawer output
            var taskContent = new VisualElement();
            taskContent.style.flexGrow = 1;
            taskContent.style.minHeight = 22;
            
            // Task property field (uses AbilityTaskDrawer via [CustomPropertyDrawer])
            var taskField = new PropertyField(taskProp);
            taskField.style.marginLeft = 0;
            taskField.style.marginRight = 0;
            taskContent.Add(taskField);
            
            taskContainer.Add(taskContent);
            
            // Button column
            var buttonColumn = new VisualElement();
            buttonColumn.style.flexDirection = FlexDirection.Column;
            buttonColumn.style.marginLeft = 4;
            buttonColumn.style.alignItems = Align.Center;
            
            // Move up
            if (index > 0)
            {
                var upBtn = CreateSmallButton("▲", "Move up", () =>
                {
                    tasksProp.MoveArrayElement(index, index - 1);
                    tasksProp.serializedObject.ApplyModifiedProperties();
                    ScheduleRebuild(root, stageProp);
                });
                buttonColumn.Add(upBtn);
            }
            
            // Move down
            if (index < tasksProp.arraySize - 1)
            {
                var downBtn = CreateSmallButton("▼", "Move down", () =>
                {
                    tasksProp.MoveArrayElement(index, index + 1);
                    tasksProp.serializedObject.ApplyModifiedProperties();
                    ScheduleRebuild(root, stageProp);
                });
                buttonColumn.Add(downBtn);
            }
            
            // Delete
            var deleteBtn = CreateSmallButton("×", "Remove task", () =>
            {
                tasksProp.DeleteArrayElementAtIndex(index);
                tasksProp.serializedObject.ApplyModifiedProperties();
                ScheduleRebuild(root, stageProp);
            });
            deleteBtn.style.color = Colors.AccentRed;
            buttonColumn.Add(deleteBtn);
            
            taskContainer.Add(buttonColumn);
            
            return taskContainer;
        }
        
        private Button CreateSmallButton(string text, string tooltip, Action onClick)
        {
            var btn = new Button { text = text, tooltip = tooltip };
            btn.style.width = 18;
            btn.style.height = 16;
            btn.style.fontSize = 9;
            btn.style.marginBottom = 2;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            ApplyButtonStyle(btn);
            btn.clicked += onClick;
            return btn;
        }
        
        private VisualElement CreateCollapsedSummary(SerializedProperty property)
        {
            var summary = new VisualElement();
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.Center;
            summary.style.marginLeft = 44; // Align with content after smaller badge
            summary.style.marginTop = 4;
            
            var tasksProp = property.FindPropertyRelative("Tasks");
            var policyProp = property.FindPropertyRelative("StagePolicy");
            
            var parts = new List<string>();
            
            // Policy
            if (policyProp != null && policyProp.managedReferenceValue != null)
            {
                parts.Add(GetPolicyDisplayName(policyProp));
            }
            
            // Tasks summary
            if (tasksProp != null)
            {
                int count = tasksProp.arraySize;
                if (count > 0)
                {
                    var taskNames = new List<string>();
                    for (int i = 0; i < Math.Min(count, 3); i++)
                    {
                        var taskProp = tasksProp.GetArrayElementAtIndex(i);
                        if (taskProp.managedReferenceValue != null)
                        {
                            var name = taskProp.managedReferenceValue.GetType().Name;
                            name = name.Replace("AbilityTask", "").Replace("Task", "");
                            taskNames.Add(name);
                        }
                    }
                    
                    if (taskNames.Count > 0)
                    {
                        var taskSummary = string.Join(", ", taskNames);
                        if (count > 3) taskSummary += $" +{count - 3} more";
                        parts.Add($"[{taskSummary}]");
                    }
                }
            }
            
            var summaryLabel = new Label(string.Join(" → ", parts));
            summaryLabel.style.fontSize = 10;
            summaryLabel.style.color = Colors.HintText;
            summary.Add(summaryLabel);
            
            return summary;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreatePolicyBadge(string policyName)
        {
            var badge = new VisualElement();
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 8;
            badge.style.borderTopRightRadius = 8;
            badge.style.borderBottomLeftRadius = 8;
            badge.style.borderBottomRightRadius = 8;
            badge.style.backgroundColor = Colors.PolicyPurple;
            badge.tooltip = "Stage completion policy";
            
            var label = new Label(policyName);
            label.style.fontSize = 9;
            label.style.color = new Color(0.9f, 0.9f, 0.9f);
            badge.Add(label);
            
            return badge;
        }
        
        private string GetPolicyDisplayName(SerializedProperty policyProp)
        {
            if (policyProp.managedReferenceValue == null) return "None";
            
            var typeName = policyProp.managedReferenceValue.GetType().Name;
            typeName = typeName.Replace("ProxyStagePolicy", "").Replace("Policy", "");
            
            // Add spaces before capitals
            var sb = new System.Text.StringBuilder();
            foreach (char c in typeName)
            {
                if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            
            return sb.ToString();
        }
        
        private int GetStageIndex(string propertyPath)
        {
            var match = System.Text.RegularExpressions.Regex.Match(propertyPath, @"data\[(\d+)\]");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
            {
                return index;
            }
            return 0;
        }
        
        private void DeleteStage(SerializedProperty property, VisualElement root)
        {
            // Find the parent array
            var path = property.propertyPath;
            var lastArrayIndex = path.LastIndexOf(".Array.data[");
            if (lastArrayIndex < 0) return;
            
            var arrayPath = path.Substring(0, lastArrayIndex);
            var so = property.serializedObject;
            var arrayProp = so.FindProperty(arrayPath);
            
            if (arrayProp != null && arrayProp.isArray)
            {
                int index = GetStageIndex(path);
                arrayProp.DeleteArrayElementAtIndex(index);
                so.ApplyModifiedProperties();
                
                // Clear collapsed state for deleted and shifted items
                var keysToRemove = _collapsedStates.Keys
                    .Where(k => k.StartsWith(arrayPath))
                    .ToList();
                foreach (var key in keysToRemove)
                    _collapsedStates.Remove(key);
            }
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
        
        private void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            
            root.schedule.Execute(() =>
            {
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildStageUI(root, freshProp);
            });
        }
    }
}