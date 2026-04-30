using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AttributeOverflowData))]
    public class AttributeOverflowDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            var policyProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Policy));
            var floorProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Floor));
            var ceilProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Ceil));

            var floorBase = floorProp.FindPropertyRelative(nameof(AttributeValue.BaseValue));
            var floorCurr = floorProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue));
            
            var ceilBase = ceilProp.FindPropertyRelative(nameof(AttributeValue.BaseValue));
            var ceilCurr = ceilProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue));
            
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
            floorLabel.style.color = ForgeDrawerStyles.Colors.HintText;
            floorRow.Add(floorLabel);
            
            var floorField = new PropertyField(floorProp, "");
            floorField.style.flexGrow = 1;
            floorField.BindProperty(floorProp);
            floorRow.Add(floorField);

            var floorButton = new Button()
            {
                text = "−∞",
                tooltip = "Set both floor values to negative infinity (unlimited lower bound)",
                style =
                {
                    minWidth = 32,
                    height = 20,
                    marginLeft = 8,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ForgeDrawerStyles.Colors.AccentBlue
                },
                focusable = false
            };
            floorButton.clicked += () =>
            {
                UpdateUnlimitedBounds(floorBase, floorCurr, Mathf.NegativeInfinity);
                property.serializedObject.ApplyModifiedProperties();
            };
            floorRow.Add(floorButton);
            
            // Ceil row (shown for ZeroToCeil, FloorToCeil)
            var ceilRow = new VisualElement { name = "CeilRow" };
            ceilRow.style.flexDirection = FlexDirection.Row;
            ceilRow.style.alignItems = Align.Center;
            ceilRow.style.marginTop = 2;
            container.Add(ceilRow);
            
            var ceilLabel = new Label("Ceil");
            ceilLabel.style.width = 36;
            ceilLabel.style.fontSize = 10;
            ceilLabel.style.color = ForgeDrawerStyles.Colors.HintText;
            ceilRow.Add(ceilLabel);
            
            var ceilField = new PropertyField(ceilProp, "");
            ceilField.style.flexGrow = 1;
            ceilField.BindProperty(ceilProp);
            ceilRow.Add(ceilField);

            var ceilButton = new Button()
            {
                text = "+∞",
                tooltip = "Set both ceil values to positive infinity (unlimited upper bound)",
                style =
                {
                    minWidth = 32,
                    height = 20,
                    marginLeft = 8,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = ForgeDrawerStyles.Colors.AccentBlue
                },
                focusable = false
            };
            ceilButton.clicked += () =>
            {
                UpdateUnlimitedBounds(ceilBase, ceilCurr, Mathf.Infinity);
                property.serializedObject.ApplyModifiedProperties();
            };
            ceilRow.Add(ceilButton);
            
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
            
            void UpdateUnlimitedBounds(SerializedProperty baseProp, SerializedProperty currProp, float value)
            {
                baseProp.floatValue = value;
                currProp.floatValue = value;
            }
            
            return container;
        }
    }
}
