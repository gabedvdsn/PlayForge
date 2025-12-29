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
    
    /// <summary>
    /// Custom PropertyDrawer for AttributeValue.
    /// Displays CurrentValue and BaseValue inline.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeValue))]
    public class AttributeValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            // Current Value
            var currentProp = property.FindPropertyRelative("CurrentValue");
            var currentContainer = new VisualElement();
            currentContainer.style.flexDirection = FlexDirection.Row;
            currentContainer.style.alignItems = Align.Center;
            currentContainer.style.flexGrow = 1;
            container.Add(currentContainer);
            
            var currentLabel = new Label("Cur");
            currentLabel.style.width = 28;
            currentLabel.style.color = Colors.HintText;
            currentLabel.style.fontSize = 10;
            currentLabel.tooltip = "Current Value";
            currentContainer.Add(currentLabel);
            
            var currentField = new FloatField();
            currentField.style.flexGrow = 1;
            currentField.style.minWidth = 50;
            currentField.bindingPath = currentProp.propertyPath;
            currentContainer.Add(currentField);
            
            // Separator
            var separator = new Label("/");
            separator.style.marginLeft = 4;
            separator.style.marginRight = 4;
            separator.style.color = Colors.HintText;
            separator.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(separator);
            
            // Base Value
            var baseProp = property.FindPropertyRelative("BaseValue");
            var baseContainer = new VisualElement();
            baseContainer.style.flexDirection = FlexDirection.Row;
            baseContainer.style.alignItems = Align.Center;
            baseContainer.style.flexGrow = 1;
            container.Add(baseContainer);
            
            var baseLabel = new Label("Base");
            baseLabel.style.width = 32;
            baseLabel.style.color = Colors.HintText;
            baseLabel.style.fontSize = 10;
            baseLabel.tooltip = "Base Value";
            baseContainer.Add(baseLabel);
            
            var baseField = new FloatField();
            baseField.style.flexGrow = 1;
            baseField.style.minWidth = 50;
            baseField.bindingPath = baseProp.propertyPath;
            baseContainer.Add(baseField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeOverflowData Drawer
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Custom PropertyDrawer for AttributeOverflowData.
    /// Shows Policy enum and conditionally displays Floor/Ceil based on policy.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeOverflowData))]
    public class AttributeOverflowDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // Get properties
            var policyProp = property.FindPropertyRelative("Policy");
            var floorProp = property.FindPropertyRelative("Floor");
            var ceilProp = property.FindPropertyRelative("Ceil");
            
            // Policy row
            var policyRow = CreateRow(4);
            container.Add(policyRow);
            
            var policyField = new PropertyField(policyProp, "Policy");
            policyField.style.flexGrow = 1;
            policyRow.Add(policyField);
            
            // Floor row (conditional)
            var floorRow = CreateRow(4);
            floorRow.name = "FloorRow";
            container.Add(floorRow);
            
            var floorLabel = new Label("Floor");
            floorLabel.style.width = 50;
            floorLabel.style.color = Colors.HintText;
            floorLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            floorRow.Add(floorLabel);
            
            var floorField = new PropertyField(floorProp, "");
            floorField.style.flexGrow = 1;
            floorRow.Add(floorField);
            
            // Ceil row (conditional)
            var ceilRow = CreateRow(2);
            ceilRow.name = "CeilRow";
            container.Add(ceilRow);
            
            var ceilLabel = new Label("Ceil");
            ceilLabel.style.width = 50;
            ceilLabel.style.color = Colors.HintText;
            ceilLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            ceilRow.Add(ceilLabel);
            
            var ceilField = new PropertyField(ceilProp, "");
            ceilField.style.flexGrow = 1;
            ceilRow.Add(ceilField);
            
            // Update visibility based on policy
            void UpdateVisibility()
            {
                var policy = (EAttributeOverflowPolicy)policyProp.enumValueIndex;
                
                // Floor is shown for: FloorToBase, FloorToCeil
                bool showFloor = policy == EAttributeOverflowPolicy.FloorToBase || 
                                 policy == EAttributeOverflowPolicy.FloorToCeil;
                
                // Ceil is shown for: ZeroToCeil, FloorToCeil
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
    // AttributeSetElement Drawer
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Custom PropertyDrawer for AttributeSetElement.
    /// Displays attribute configuration with themed sections.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeSetElement))]
    public class AttributeSetElementDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = CreateMainContainer(Colors.AccentGreen);
            
            // ═══════════════════════════════════════════════════════════════════
            // Header Row - Attribute reference
            // ═══════════════════════════════════════════════════════════════════
            
            var headerRow = CreateRow(6);
            headerRow.style.alignItems = Align.Center;
            container.Add(headerRow);
            
            // Color indicator based on collision policy
            var policyIndicator = CreateColorIndicator(Colors.AccentGreen, 4, 24);
            policyIndicator.name = "PolicyIndicator";
            policyIndicator.style.alignSelf = Align.Center;
            policyIndicator.style.marginRight = 8;
            headerRow.Add(policyIndicator);
            
            /*// Attribute icon
            var attrIcon = new Label(Icons.Attribute);
            attrIcon.style.fontSize = 14;
            attrIcon.style.marginRight = 4;
            attrIcon.style.color = Colors.AccentGreen;
            headerRow.Add(attrIcon);*/
            
            // Attribute field (main identifier)
            var attributeProp = property.FindPropertyRelative("Attribute");
            var attributeField = new PropertyField(attributeProp, "");
            attributeField.style.flexGrow = 1;
            attributeField.style.minWidth = 140;
            headerRow.Add(attributeField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Values Section
            // ═══════════════════════════════════════════════════════════════════
            
            var valuesSection = CreateSection("VALUES", Colors.AccentBlue);
            container.Add(valuesSection);
            
            // Magnitude + Target row
            var magnitudeRow = CreateRow(4);
            valuesSection.Add(magnitudeRow);
            
            var magnitudeLabel = new Label("Magnitude");
            magnitudeLabel.style.width = 70;
            magnitudeLabel.style.color = Colors.HeaderText;
            magnitudeLabel.style.alignSelf = Align.Center;
            magnitudeRow.Add(magnitudeLabel);
            
            var magnitudeProp = property.FindPropertyRelative("Magnitude");
            var magnitudeField = new FloatField();
            magnitudeField.style.width = 80;
            magnitudeField.bindingPath = magnitudeProp.propertyPath;
            magnitudeField.tooltip = "Base magnitude value for this attribute";
            magnitudeRow.Add(magnitudeField);
            
            /*var targetLabel = new Label();
            targetLabel.style.width = 50;
            targetLabel.style.marginLeft = 16;
            targetLabel.style.color = Colors.HeaderText;
            magnitudeRow.Add(targetLabel);*/
            
            var targetProp = property.FindPropertyRelative("Target");
            var targetField = new PropertyField(targetProp, "");
            targetField.style.flexGrow = 1;
            targetField.style.marginLeft = 8;
            targetField.tooltip = "CurrentAndBase: Sets both current and base\nBase: Sets only base value (current starts at 0)";
            magnitudeRow.Add(targetField);
            
            // Modifier field
            var modifierRow = CreateRow(2);
            valuesSection.Add(modifierRow);
            
            var modifierProp = property.FindPropertyRelative("Modifier");
            var modifierField = new PropertyField(modifierProp, "Modifier");
            modifierField.style.flexGrow = 1;
            modifierField.tooltip = "Optional: Dynamic modifier for magnitude calculation";
            modifierRow.Add(modifierField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Overflow Section
            // ═══════════════════════════════════════════════════════════════════
            
            var overflowSection = CreateSection("OVERFLOW BOUNDS", Colors.AccentOrange);
            container.Add(overflowSection);
            
            var overflowProp = property.FindPropertyRelative("Overflow");
            var overflowField = new PropertyField(overflowProp, "");
            overflowField.style.marginBottom = 2;
            overflowSection.Add(overflowField);
            
            // Policy explanation hint
            var overflowHint = CreateHintLabel("Defines min/max bounds for attribute values");
            overflowSection.Add(overflowHint);
            
            // ═══════════════════════════════════════════════════════════════════
            // Collision Policy Section
            // ═══════════════════════════════════════════════════════════════════
            
            var collisionSection = CreateSection("COLLISION POLICY", Colors.AccentPurple);
            container.Add(collisionSection);
            
            var collisionRow = CreateRow(2);
            collisionSection.Add(collisionRow);
            
            var collisionProp = property.FindPropertyRelative("CollisionPolicy");
            var collisionField = new PropertyField(collisionProp, "Policy");
            collisionField.style.flexGrow = 1;
            collisionRow.Add(collisionField);
            
            // Policy description label (updates dynamically)
            var policyDescLabel = CreateHintLabel("");
            policyDescLabel.name = "PolicyDescription";
            collisionSection.Add(policyDescLabel);
            
            // ═══════════════════════════════════════════════════════════════════
            // Dynamic Updates
            // ═══════════════════════════════════════════════════════════════════
            
            void UpdatePolicyIndicator()
            {
                var policy = (EAttributeElementCollisionPolicy)collisionProp.enumValueIndex;
                
                // Update indicator color
                policyIndicator.style.backgroundColor = policy switch
                {
                    EAttributeElementCollisionPolicy.UseThis => Colors.AccentGreen,
                    EAttributeElementCollisionPolicy.UseExisting => Colors.AccentOrange,
                    EAttributeElementCollisionPolicy.Combine => Colors.AccentBlue,
                    _ => Colors.AccentGray
                };
                
                // Update tooltip
                policyIndicator.tooltip = policy switch
                {
                    EAttributeElementCollisionPolicy.UseCollisionSetting => "",
                    EAttributeElementCollisionPolicy.UseThis => "This value takes priority",
                    EAttributeElementCollisionPolicy.UseExisting => "Existing value takes priority",
                    EAttributeElementCollisionPolicy.Combine => "Values will be combined",
                    _ => ""
                };
                
                // Update description text
                policyDescLabel.text = policy switch
                {
                    EAttributeElementCollisionPolicy.UseCollisionSetting => $"Resolves collisions using the set collision policy",
                    EAttributeElementCollisionPolicy.UseThis => "This definition overrides any existing values",
                    EAttributeElementCollisionPolicy.UseExisting => "Keeps existing value if attribute already defined",
                    EAttributeElementCollisionPolicy.Combine => "Adds this value to existing (if present)",
                    _ => ""
                };
            }
            
            // Schedule initial update and register for changes
            container.schedule.Execute(UpdatePolicyIndicator).StartingIn(50);
            collisionField.RegisterValueChangeCallback(_ => UpdatePolicyIndicator());
            
            return container;
        }
    }
}