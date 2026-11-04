using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom drawer for StandardWorkerGroup.
    /// Styled to match Requirements and Scaler drawers.
    /// </summary>
    [CustomPropertyDrawer(typeof(StandardWorkerGroup))]
    public class StandardWorkerGroupDrawer : PropertyDrawer
    {
        private static HashSet<string> _rebuildingProperties = new HashSet<string>();
        
        private static readonly Color WorkerGroupAccent = Colors.AccentGreen;
        
        // Kept per asset for the editor session, starting collapsed (ED-18)
        private static bool IsCollapsed(SerializedProperty property) =>
            DrawerCollapseState.IsCollapsed(property, collapsedByDefault: true);
        private static void SetCollapsed(SerializedProperty property, bool collapsed) =>
            DrawerCollapseState.SetCollapsed(property, collapsed);
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "WorkerGroupRoot" };
            // The style sheet sizes and colors the kit's glyphs, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(root);
            root.style.marginBottom = 4;
            BuildUI(root, property);
            return root;
        }
        
        private void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            var serializedObject = property.serializedObject;
            if (_rebuildingProperties.Contains(propPath)) return;
            _rebuildingProperties.Add(propPath);
            root.schedule.Execute(() =>
            {
                _rebuildingProperties.Remove(propPath);
                if (targetObject == null) return;
                // The drawer's own SerializedObject, so the rebuilt UI edits every selected asset. A copy over the
                // first target edited that asset alone (ED-12)
                if (root.panel == null) return;
                var so = serializedObject;
                so.Update();
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildUI(root, freshProp);
            });
        }
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            if (property?.serializedObject?.targetObject == null) return;
            
            bool isCollapsed = IsCollapsed(property);
            
            var nameProp = property.FindPropertyRelative("Name");
            string customName = nameProp?.stringValue ?? "";
            var counts = GetTotalWorkerCount(property);
            
            // Main container with border styling
            var container = new VisualElement { name = "WorkerGroupContainer" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = WorkerGroupAccent;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
            container.style.borderRightWidth = 1;
            container.style.borderRightColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = Colors.SectionBackground;
            container.style.paddingTop = 4;
            container.style.paddingBottom = isCollapsed ? 4 : 6;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.marginTop = 2;
            root.Add(container);
            
            // Header
            var header = new VisualElement { name = "WorkerGroupHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 4;
            container.Add(header);
            
            // Collapse button
            var collapseBtn = new Button { text = isCollapsed ? Icons.ChevronRight : Icons.ChevronDown };
            collapseBtn.tooltip = isCollapsed ? "Expand" : "Collapse";
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.marginRight = 4;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonStyle(collapseBtn);
            collapseBtn.clicked += () => { SetCollapsed(property, !isCollapsed); ScheduleRebuild(root, property); };
            header.Add(collapseBtn);
            
            // Title - show custom name if set, otherwise "Worker Group"
            if (!string.IsNullOrEmpty(customName) && customName != "Workers")
            {
                header.Add(new Label(customName) { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginRight = 6 } });
                header.Add(CreateBadge("Worker Group", Colors.HintText));
            }
            else
            {
                header.Add(new Label("Worker Group") { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            }
            
            // Spacer
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // Count badge
            if (counts.total > 0) 
            {
                header.Add(CreateBadge(counts.attr.ToString(), GetWorkerTypeColor("Attribute"), "Attribute Workers"));
                header.Add(CreateBadge(counts.impact.ToString(), GetWorkerTypeColor("Impact"), "Impact Workers"));
                header.Add(CreateBadge(counts.tags.ToString(), GetWorkerTypeColor("Tag"), "Tag Workers"));
                header.Add(CreateBadge(counts.analysis.ToString(), GetWorkerTypeColor("Analysis"), "Analysis Workers"));
            }
            else if (isCollapsed)
            {
                header.Add(new Label("Empty") { style = { fontSize = 9, color = Colors.HintText, unityFontStyleAndWeight = FontStyle.Italic } });
            }
            
            // Import button
            var importBtn = new Button { tooltip = "Import workers from another asset" };
            importBtn.Add(new ForgeGlyph(ForgeGlyphShape.Import));
            importBtn.style.width = 22;
            importBtn.style.height = 20;
            importBtn.style.marginLeft = 8;
            importBtn.style.fontSize = 10;
            ApplyButtonStyle(importBtn);
            // The one Import window, which lists every asset with a worker group, as this window's own did (ED-32)
            importBtn.clicked += () => ForgeImportWindow.ShowFor(property.serializedObject, "Workers", new[] { property.propertyPath },
                () => ScheduleRebuild(root, property), acrossTypes: true);
            header.Add(importBtn);
            
            // Type indicator
            header.Add(CreateTypeIndicator("Workers", WorkerGroupAccent));
            
            // Content (when expanded)
            if (!isCollapsed)
            {
                var content = new VisualElement { style = { paddingTop = 4 } };
                container.Add(content);
                
                // Name field
                var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
                nameRow.Add(new Label("Name") { style = { width = 50, fontSize = 10, color = Colors.HintText } });
                var nameField = new TextField { value = customName, style = { flexGrow = 1 } };
                nameField.RegisterValueChangedCallback(evt => 
                { 
                    if (nameProp != null) 
                    { 
                        nameProp.stringValue = evt.newValue; 
                        property.serializedObject.ApplyModifiedProperties(); 
                    } 
                });
                nameRow.Add(nameField);
                content.Add(nameRow);
                
                // Worker type sections
                content.Add(CreateWorkerTypeSection(property, "AttributeWorkers", "Attribute", "Perform work before or after attribute modifications.", GetWorkerTypeColor("Attribute")));
                
                var impactSection = CreateWorkerTypeSection(property, "ImpactWorkers", "Impact", "Inspect any outgoing impact dealt.", GetWorkerTypeColor("Impact"));
                impactSection.style.marginTop = 6;
                content.Add(impactSection);
                
                var tagSection = CreateWorkerTypeSection(property, "TagWorkers", "Tag", "Inspect & react to changes in applied tags.", GetWorkerTypeColor("Tag"));
                tagSection.style.marginTop = 6;
                content.Add(tagSection);
                
                var analysisSection = CreateWorkerTypeSection(property, "AnalysisWorkers", "Analysis", "Analyze all gathered data at the end of each frame.", GetWorkerTypeColor("Analysis"));
                analysisSection.style.marginTop = 6;
                content.Add(analysisSection);
                
                container.Bind(property.serializedObject);
            }
        }

        private Color GetWorkerTypeColor(string workerType)
        {
            return Colors.BorderDark;
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
        
        private (int attr, int impact, int tags, int analysis, int total) GetTotalWorkerCount(SerializedProperty property)
        {
            var value = (GetArrayCount(property, "AttributeWorkers"),
                GetArrayCount(property, "ImpactWorkers"),
                GetArrayCount(property, "TagWorkers"),
                GetArrayCount(property, "AnalysisWorkers"),
                0);
            value.Item5 = value.Item1 + value.Item2 + value.Item3 + value.Item4;
            return value;
        }
        
        private int GetArrayCount(SerializedProperty property, string arrayName)
        {
            var array = property.FindPropertyRelative(arrayName);
            return array?.arraySize ?? 0;
        }
        
        private VisualElement CreateTypeIndicator(string text, Color color)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 8;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.marginLeft = 4;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = color;
            container.style.borderBottomColor = color;
            container.style.borderLeftColor = color;
            container.style.borderRightColor = color;
            container.style.backgroundColor = new Color(0.18f, 0.20f, 0.22f, 0.8f);
            
            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = Colors.LabelText;
            container.Add(label);
            
            return container;
        }
        
        private Label CreateBadge(string text, Color color, string tooltip = null)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.marginLeft = 3;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.tooltip = tooltip;
            return badge;
        }
        
        private VisualElement CreateWorkerTypeSection(
            SerializedProperty property, 
            string propertyName, 
            string displayName,
            string hint,
            Color accentColor)
        {
            var arrayProp = property.FindPropertyRelative(propertyName);
            if (arrayProp == null) return new VisualElement();
            
            var section = new VisualElement();
            section.style.paddingLeft = 4;
            section.style.paddingTop = 2;
            section.style.paddingBottom = 2;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = accentColor;
            section.style.backgroundColor = Colors.SubsectionBackground;
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            // Section header
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            header.Add(new Label(displayName) { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.LabelText } });
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            int count = arrayProp.arraySize;
            if (count > 0)
            {
                //header.Add(new Label($"({count})") { style = { fontSize = 9, color = accentColor } });
            }
            section.Add(header);
            
            if (!string.IsNullOrEmpty(hint))
            {
                section.Add(CreateHintLabel(hint));
            }
            
            // Property field for the list
            var field = new PropertyField(arrayProp, "");
            field.style.marginTop = 2;
            field.BindProperty(arrayProp);
            section.Add(field);
            
            return section;
        }
    }
}
