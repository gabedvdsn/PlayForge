using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeValue Drawer
    // ═══════════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeOverflowData Drawer - Vertical layout for Floor/Ceil
    // ═══════════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeSetElement Drawer - Show/Hide approach (no rebuild)
    // ═══════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AttributeSetElement))]
    public class AttributeSetElementDrawer : PropertyDrawer
    {
        // Collapse state persisted by property path
        private static Dictionary<string, bool> _collapsedStates = new Dictionary<string, bool>();
        
        private static bool IsCollapsed(string path)
        {
            return _collapsedStates.TryGetValue(path, out bool c) && c;
        }
        
        private static void SetCollapsed(string path, bool collapsed)
        {
            _collapsedStates[path] = collapsed;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AttributeSetElementRoot" };
            
            bool startCollapsed = IsCollapsed(property.propertyPath);
            
            // Get all properties upfront
            var attributeProp = property.FindPropertyRelative("Attribute");
            var magnitudeProp = property.FindPropertyRelative("Magnitude");
            var modifierProp = property.FindPropertyRelative("Scaling");
            var targetProp = property.FindPropertyRelative("Target");
            var overflowProp = property.FindPropertyRelative("Overflow");
            var collisionProp = property.FindPropertyRelative("CollisionPolicy");
            var constraintsProp = property.FindPropertyRelative("Constraints");
            var retentionProp = property.FindPropertyRelative("RetentionGroup");
            
            // Main container
            var container = new VisualElement { name = "Container" };
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = Colors.AccentGray;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            root.Add(container);
            
            // ═══════════════════════════════════════════════════════════════════
            // Header Row (always visible)
            // ═══════════════════════════════════════════════════════════════════
            
            var headerRow = new VisualElement { name = "HeaderRow" };
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            container.Add(headerRow);
            
            // Collapse button
            var collapseBtn = new Button { name = "CollapseBtn" };
            collapseBtn.text = startCollapsed ? Icons.ChevronRight : Icons.ChevronDown;
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.marginRight = 4;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            collapseBtn.style.backgroundColor = Colors.ButtonBackground;
            collapseBtn.style.borderTopLeftRadius = 3;
            collapseBtn.style.borderTopRightRadius = 3;
            collapseBtn.style.borderBottomLeftRadius = 3;
            collapseBtn.style.borderBottomRightRadius = 3;
            headerRow.Add(collapseBtn);
            
            // Attribute field
            var attributeField = new PropertyField(attributeProp, "");
            attributeField.style.flexGrow = 1;
            attributeField.style.minWidth = 100;
            attributeField.BindProperty(attributeProp);
            headerRow.Add(attributeField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Summary badges (visible when collapsed)
            // ═══════════════════════════════════════════════════════════════════
            
            var summaryContainer = new VisualElement { name = "SummaryContainer" };
            summaryContainer.style.flexDirection = FlexDirection.Row;
            summaryContainer.style.alignItems = Align.Center;
            summaryContainer.style.marginLeft = 8;
            summaryContainer.style.display = startCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
            headerRow.Add(summaryContainer);
            
            // Magnitude badge
            var magBadge = CreateBadge("0", Colors.AccentBlue);
            magBadge.name = "MagBadge";
            summaryContainer.Add(magBadge);
            
            // Target badge
            var targetBadge = CreateBadge("C+B", Colors.HintText);
            targetBadge.name = "TargetBadge";
            targetBadge.style.marginLeft = 4;
            summaryContainer.Add(targetBadge);
            
            // Collision badge
            var retentionBadge = CreateBadge("Def", Colors.HintText);
            retentionBadge.name = "RetentionBadge";
            retentionBadge.style.marginLeft = 4;
            summaryContainer.Add(retentionBadge);
            
            // Target badge
            var clampScaleBadge = CreateBadge("C+B", Colors.HintText);
            clampScaleBadge.name = "ClampScaleBadge";
            clampScaleBadge.style.marginLeft = 4;
            summaryContainer.Add(clampScaleBadge);
            
            // Target badge
            var roundingBadge = CreateBadge("C+B", Colors.HintText);
            roundingBadge.name = "RoundingBadge";
            roundingBadge.style.marginLeft = 4;
            summaryContainer.Add(roundingBadge);
            
            // ═══════════════════════════════════════════════════════════════════
            // Expanded Content (visible when expanded)
            // ═══════════════════════════════════════════════════════════════════
            
            var expandedContent = new VisualElement { name = "ExpandedContent" };
            expandedContent.style.marginTop = 6;
            expandedContent.style.display = startCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            container.Add(expandedContent);
            
            // Retention group row
            var retentionRow = new VisualElement();
            retentionRow.style.flexDirection = FlexDirection.Row;
            retentionRow.style.alignItems = Align.FlexStart;
            retentionRow.style.marginBottom = 4;
            expandedContent.Add(retentionRow);
            
            var retentionLabel = new Label("Retention Group");
            retentionLabel.style.width = 100;
            retentionLabel.style.alignSelf = Align.Center;
            retentionLabel.style.fontSize = 10;
            retentionLabel.style.color = Colors.HintText;
            retentionLabel.style.marginTop = 2;
            retentionRow.Add(retentionLabel);

            var retentionField = new PropertyField(retentionProp, "");
            retentionField.style.flexGrow = 1;
            retentionField.BindProperty(retentionProp);
            retentionRow.Add(retentionField);        
            
            // Magnitude + Target row
            var valuesRow = new VisualElement();
            valuesRow.style.flexDirection = FlexDirection.Row;
            valuesRow.style.alignItems = Align.Center;
            valuesRow.style.marginBottom = 4;
            expandedContent.Add(valuesRow);
            
            var magLabel = new Label("Magnitude");
            magLabel.style.width = 65;
            magLabel.style.fontSize = 10;
            magLabel.style.color = Colors.HintText;
            valuesRow.Add(magLabel);
            
            var magField = new FloatField();
            magField.style.width = 60;
            magField.bindingPath = magnitudeProp.propertyPath;
            valuesRow.Add(magField);
            
            var targetLabel = new Label("Target");
            targetLabel.style.width = 40;
            targetLabel.style.fontSize = 10;
            targetLabel.style.color = Colors.HintText;
            targetLabel.style.marginLeft = 12;
            valuesRow.Add(targetLabel);
            
            var targetField = new PropertyField(targetProp, "");
            targetField.style.flexGrow = 1;
            targetField.BindProperty(targetProp);
            valuesRow.Add(targetField);
            
            // Modifier row
            var modifierField = new PropertyField(modifierProp, "Scaling");
            modifierField.style.marginBottom = 4;
            modifierField.BindProperty(modifierProp);
            expandedContent.Add(modifierField);
            
            // Overflow row
            var overflowRow = new VisualElement();
            overflowRow.style.flexDirection = FlexDirection.Row;
            overflowRow.style.alignItems = Align.FlexStart;
            overflowRow.style.marginBottom = 4;
            expandedContent.Add(overflowRow);
            
            var ovLabel = new Label("Bounds");
            ovLabel.style.width = 65;
            ovLabel.style.fontSize = 10;
            ovLabel.style.color = Colors.HintText;
            ovLabel.style.marginTop = 2;
            overflowRow.Add(ovLabel);
            
            var ovField = new PropertyField(overflowProp, "");
            ovField.style.flexGrow = 1;
            ovField.BindProperty(overflowProp);
            overflowRow.Add(ovField);
            
            // Collision row
            var collisionRow = new VisualElement();
            collisionRow.style.flexDirection = FlexDirection.Row;
            collisionRow.style.alignItems = Align.Center;
            expandedContent.Add(collisionRow);
            
            var collLabel = new Label("Collision");
            collLabel.style.width = 65;
            collLabel.style.fontSize = 10;
            collLabel.style.color = Colors.HintText;
            collisionRow.Add(collLabel);
            
            var collField = new PropertyField(collisionProp, "");
            collField.style.flexGrow = 1;
            collField.BindProperty(collisionProp);
            collisionRow.Add(collField);
            
            // Collision row
            var constraintsRow = new VisualElement();
            constraintsRow.style.flexDirection = FlexDirection.Row;
            constraintsRow.style.alignItems = Align.Center;
            expandedContent.Add(constraintsRow);
            
            var constraintLabel = new Label("Constraints");
            constraintLabel.style.width = 65;
            constraintLabel.style.fontSize = 10;
            constraintLabel.style.color = Colors.HintText;
            constraintsRow.Add(constraintLabel);
            
            var constraintField = new PropertyField(constraintsProp, "");
            constraintField.style.flexGrow = 1;
            constraintField.BindProperty(constraintsProp);
            constraintsRow.Add(constraintField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Update Functions
            // ═══════════════════════════════════════════════════════════════════
            
            void UpdateSummary()
            {
                // Magnitude
                float mag = magnitudeProp.floatValue;
                magBadge.text = mag.ToString("F2");
                magBadge.tooltip = $"Magnitude: {mag:F2}";
                
                // Target
                var target = (ELimitedEffectImpactTarget)targetProp.enumValueIndex;
                targetBadge.text = target == ELimitedEffectImpactTarget.CurrentAndBase ? "C+B" : "B";
                targetBadge.tooltip = $"Targets: {target.ToString()}";
                
                string retentionGroup = retentionProp.FindPropertyRelative("Name").stringValue;
                retentionBadge.text = retentionGroup;
                retentionBadge.style.color = Colors.AccentGreen;
                retentionBadge.style.backgroundColor = new Color(Colors.AccentBlue.r, Colors.AccentBlue.g, Colors.AccentBlue.b, 0.15f);
                retentionBadge.tooltip = $"Retention Group: {retentionGroup}";

                string clampScale = "";
                if (constraintsProp.FindPropertyRelative("AutoClamp").boolValue)
                {
                    clampScale += "C";
                    if (constraintsProp.FindPropertyRelative("AutoScaleWithBase").boolValue) clampScale += "/S";
                }
                else if (constraintsProp.FindPropertyRelative("AutoScaleWithBase").boolValue) clampScale += "S";
                clampScaleBadge.text = clampScale;
                clampScaleBadge.style.color = Colors.AccentOrange;
                clampScaleBadge.style.backgroundColor = new Color(Colors.AccentOrange.r, Colors.AccentOrange.g, Colors.AccentOrange.b, 0.15f);
                clampScaleBadge.tooltip = $"Clamp/Scale: {(clampScale.Contains("C") ? "True" : "False")}/{(clampScale.Contains("S") ? "True" : "False")}";
                
                var roundingTarget = (EAttributeRoundingPolicy)constraintsProp.FindPropertyRelative("RoundingMode").enumValueIndex;
                if (roundingTarget == EAttributeRoundingPolicy.None) roundingBadge.style.display = DisplayStyle.None;
                else
                {
                    roundingBadge.style.display = DisplayStyle.Flex;
                    string roundingText = $"{roundingTarget.ToString()}" +
                                          (roundingTarget == EAttributeRoundingPolicy.SnapTo ? $"/{constraintsProp.FindPropertyRelative("SnapInterval").floatValue:g2}" : string.Empty);
                    roundingBadge.text = roundingText;
                    roundingBadge.style.color = Colors.AccentPurple;
                    roundingBadge.style.backgroundColor = new Color(Colors.AccentPurple.r, Colors.AccentPurple.g, Colors.AccentPurple.b, 0.15f);
                    roundingBadge.tooltip = $"Rounding: {roundingText}";
                }
                
                // Border color
                container.style.borderLeftColor = (EAttributeElementCollisionPolicy)collisionProp.enumValueIndex switch
                {
                    EAttributeElementCollisionPolicy.UseThis => Colors.AccentGreen,
                    EAttributeElementCollisionPolicy.UseExisting => Colors.AccentOrange,
                    EAttributeElementCollisionPolicy.Combine => Colors.AccentBlue,
                    _ => Colors.AccentGray
                };
            }
            
            void ToggleCollapse()
            {
                bool isCollapsed = IsCollapsed(property.propertyPath);
                bool newState = !isCollapsed;
                SetCollapsed(property.propertyPath, newState);
                
                collapseBtn.text = newState ? Icons.ChevronRight : Icons.ChevronDown;
                summaryContainer.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
                expandedContent.style.display = newState ? DisplayStyle.None : DisplayStyle.Flex;
                
                if (newState)
                {
                    UpdateSummary();
                }
            }
            
            // Wire up collapse button
            collapseBtn.clicked += ToggleCollapse;
            
            // Initial summary update
            root.schedule.Execute(UpdateSummary).StartingIn(100);
            
            // Update summary when values change (for when it becomes visible)
            magField.RegisterValueChangedCallback(_ => UpdateSummary());
            targetField.RegisterValueChangeCallback(_ => UpdateSummary());
            collField.RegisterValueChangeCallback(_ => UpdateSummary());
            
            return root;
        }
        
        private Label CreateBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            return badge;
        }
    }
}