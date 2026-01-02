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
    
    [CustomPropertyDrawer(typeof(AttributeValue))]
    public class AttributeValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            var currentProp = property.FindPropertyRelative("CurrentValue");
            var baseProp = property.FindPropertyRelative("BaseValue");
            
            // Current
            var currentLabel = new Label("Cur");
            currentLabel.style.width = 24;
            currentLabel.style.color = Colors.HintText;
            currentLabel.style.fontSize = 9;
            currentLabel.tooltip = "Current Value";
            container.Add(currentLabel);
            
            var currentField = new FloatField();
            currentField.style.flexGrow = 1;
            currentField.style.minWidth = 40;
            currentField.bindingPath = currentProp.propertyPath;
            container.Add(currentField);
            
            // Separator
            var sep = new Label("/");
            sep.style.marginLeft = 4;
            sep.style.marginRight = 4;
            sep.style.color = Colors.HintText;
            container.Add(sep);
            
            // Base
            var baseLabel = new Label("Base");
            baseLabel.style.width = 28;
            baseLabel.style.color = Colors.HintText;
            baseLabel.style.fontSize = 9;
            baseLabel.tooltip = "Base Value";
            container.Add(baseLabel);
            
            var baseField = new FloatField();
            baseField.style.flexGrow = 1;
            baseField.style.minWidth = 40;
            baseField.bindingPath = baseProp.propertyPath;
            container.Add(baseField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeOverflowData Drawer - Vertical layout for Floor/Ceil
    // ═══════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AttributeOverflowData))]
    public class AttributeOverflowDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            var policyProp = property.FindPropertyRelative("Policy");
            var floorProp = property.FindPropertyRelative("Floor");
            var ceilProp = property.FindPropertyRelative("Ceil");
            
            // Policy row
            var policyRow = new VisualElement();
            policyRow.style.flexDirection = FlexDirection.Row;
            policyRow.style.alignItems = Align.Center;
            policyRow.style.marginBottom = 2;
            container.Add(policyRow);
            
            var policyField = new PropertyField(policyProp, "");
            policyField.style.flexGrow = 1;
            policyField.BindProperty(policyProp);
            policyRow.Add(policyField);
            
            // Floor row (shown for FloorToBase, FloorToCeil)
            var floorRow = new VisualElement { name = "FloorRow" };
            floorRow.style.flexDirection = FlexDirection.Row;
            floorRow.style.alignItems = Align.Center;
            floorRow.style.marginTop = 2;
            container.Add(floorRow);
            
            var floorLabel = new Label("Floor");
            floorLabel.style.width = 36;
            floorLabel.style.fontSize = 10;
            floorLabel.style.color = Colors.HintText;
            floorRow.Add(floorLabel);
            
            var floorField = new PropertyField(floorProp, "");
            floorField.style.flexGrow = 1;
            floorField.BindProperty(floorProp);
            floorRow.Add(floorField);
            
            // Ceil row (shown for ZeroToCeil, FloorToCeil)
            var ceilRow = new VisualElement { name = "CeilRow" };
            ceilRow.style.flexDirection = FlexDirection.Row;
            ceilRow.style.alignItems = Align.Center;
            ceilRow.style.marginTop = 2;
            container.Add(ceilRow);
            
            var ceilLabel = new Label("Ceil");
            ceilLabel.style.width = 36;
            ceilLabel.style.fontSize = 10;
            ceilLabel.style.color = Colors.HintText;
            ceilRow.Add(ceilLabel);
            
            var ceilField = new PropertyField(ceilProp, "");
            ceilField.style.flexGrow = 1;
            ceilField.BindProperty(ceilProp);
            ceilRow.Add(ceilField);
            
            void UpdateVisibility()
            {
                var policy = (EAttributeOverflowPolicy)policyProp.enumValueIndex;
                
                // Floor shown for: FloorToBase, FloorToCeil
                bool showFloor = policy == EAttributeOverflowPolicy.FloorToBase || 
                                 policy == EAttributeOverflowPolicy.FloorToCeil;
                
                // Ceil shown for: ZeroToCeil, FloorToCeil
                bool showCeil = policy == EAttributeOverflowPolicy.ZeroToCeil || 
                                policy == EAttributeOverflowPolicy.FloorToCeil;
                
                floorRow.style.display = showFloor ? DisplayStyle.Flex : DisplayStyle.None;
                ceilRow.style.display = showCeil ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            container.schedule.Execute(UpdateVisibility).StartingIn(50);
            policyField.RegisterValueChangeCallback(_ => UpdateVisibility());
            
            return container;
        }
    }
    
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
            var modifierProp = property.FindPropertyRelative("Modifier");
            var targetProp = property.FindPropertyRelative("Target");
            var overflowProp = property.FindPropertyRelative("Overflow");
            var collisionProp = property.FindPropertyRelative("CollisionPolicy");
            
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
            collapseBtn.text = startCollapsed ? "▶" : "▼";
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
            magBadge.tooltip = "Magnitude";
            summaryContainer.Add(magBadge);
            
            // Target badge
            var targetBadge = CreateBadge("C+B", Colors.HintText);
            targetBadge.name = "TargetBadge";
            targetBadge.style.marginLeft = 4;
            summaryContainer.Add(targetBadge);
            
            // Collision badge
            var collisionBadge = CreateBadge("Def", Colors.HintText);
            collisionBadge.name = "CollisionBadge";
            collisionBadge.style.marginLeft = 4;
            summaryContainer.Add(collisionBadge);
            
            // ═══════════════════════════════════════════════════════════════════
            // Expanded Content (visible when expanded)
            // ═══════════════════════════════════════════════════════════════════
            
            var expandedContent = new VisualElement { name = "ExpandedContent" };
            expandedContent.style.marginTop = 6;
            expandedContent.style.display = startCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            container.Add(expandedContent);
            
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
            var modifierField = new PropertyField(modifierProp, "Modifier");
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
            
            // ═══════════════════════════════════════════════════════════════════
            // Update Functions
            // ═══════════════════════════════════════════════════════════════════
            
            void UpdateSummary()
            {
                // Magnitude
                float mag = magnitudeProp.floatValue;
                magBadge.text = mag.ToString("F1");
                
                // Target
                var target = (ELimitedEffectImpactTarget)targetProp.enumValueIndex;
                targetBadge.text = target == ELimitedEffectImpactTarget.CurrentAndBase ? "C+B" : "B";
                targetBadge.tooltip = target.ToString();
                
                // Collision
                var collision = (EAttributeElementCollisionPolicy)collisionProp.enumValueIndex;
                var (collText, collColor) = collision switch
                {
                    EAttributeElementCollisionPolicy.UseThis => ("This", Colors.AccentGreen),
                    EAttributeElementCollisionPolicy.UseExisting => ("Exist", Colors.AccentOrange),
                    EAttributeElementCollisionPolicy.Combine => ("Add", Colors.AccentBlue),
                    _ => ("Def", Colors.HintText)
                };
                collisionBadge.text = collText;
                collisionBadge.style.color = collColor;
                collisionBadge.style.backgroundColor = new Color(collColor.r, collColor.g, collColor.b, 0.15f);
                collisionBadge.tooltip = $"Collision: {collision}";
                
                // Border color
                container.style.borderLeftColor = collision switch
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
                
                collapseBtn.text = newState ? "▶" : "▼";
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