using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Custom property drawer for AttributeConstraints.
    /// Compact display with conditional SnapInterval visibility.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeConstraints))]
    public class AttributeConstraintsDrawer : PropertyDrawer
    {
        private static readonly Color ConstraintAccent = Colors.AccentYellow;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AttributeConstraintsRoot" };
            
            var autoClampProp = property.FindPropertyRelative("AutoClamp");
            var autoScaleProp = property.FindPropertyRelative("AutoScaleWithBase");
            var roundingProp = property.FindPropertyRelative("RoundingMode");
            var snapIntervalProp = property.FindPropertyRelative("SnapInterval");
            
            // Main container
            var container = new VisualElement { name = "ConstraintsContainer" };
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = ConstraintAccent;
            container.style.paddingLeft = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.paddingRight = 4;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            container.style.backgroundColor = Colors.SubsectionBackground;
            container.style.borderTopLeftRadius = 2;
            container.style.borderTopRightRadius = 2;
            container.style.borderBottomLeftRadius = 2;
            container.style.borderBottomRightRadius = 2;
            root.Add(container);
            
            // Header
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            
            var headerLabel = new Label("Constraints")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Colors.LabelText
                }
            };
            header.Add(headerLabel);
            
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // Summary badges
            var summaryContainer = new VisualElement
            {
                name = "summary",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };
            header.Add(summaryContainer);
            
            container.Add(header);
            
            // Toggle row
            var toggleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            
            // AutoClamp toggle
            var clampToggle = new Toggle { value = autoClampProp.boolValue };
            clampToggle.style.marginRight = 0;
            clampToggle.RegisterValueChangedCallback(evt =>
            {
                autoClampProp.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                UpdateSummary(summaryContainer, autoClampProp, autoScaleProp, roundingProp);
            });
            toggleRow.Add(clampToggle);
            
            var clampLabel = new Label("Clamp")
            {
                tooltip = "Auto clamp attribute values that fall outside of bounds",
                style =
                {
                    fontSize = 10,
                    color = Colors.LabelText,
                    marginRight = 12,
                    marginLeft = 2
                }
            };
            toggleRow.Add(clampLabel);
            
            // AutoScaleWithBase toggle
            var scaleToggle = new Toggle { value = autoScaleProp.boolValue };
            scaleToggle.style.marginRight = 0;
            scaleToggle.RegisterValueChangedCallback(evt =>
            {
                autoScaleProp.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                UpdateSummary(summaryContainer, autoClampProp, autoScaleProp, roundingProp);
            });
            toggleRow.Add(scaleToggle);
            
            var scaleLabel = new Label("Scale Current")
            {
                tooltip = "Auto scale current value proportionally when base value changes",
                style =
                {
                    fontSize = 10,
                    color = Colors.LabelText,
                    marginLeft = 2
                }
            };
            toggleRow.Add(scaleLabel);
            
            container.Add(toggleRow);
            
            // Rounding row
            var roundingRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };
            
            var roundingLabel = new Label("Rounding")
            {
                tooltip = "Round attribute values after changes. May artificially increase/decrease impact.",
                style =
                {
                    fontSize = 10,
                    color = Colors.HintText,
                    width = 55
                }
            };
            roundingRow.Add(roundingLabel);
            
            var roundingField = new EnumField((EAttributeRoundingPolicy)roundingProp.enumValueIndex);
            roundingField.style.flexGrow = 1;
            roundingField.style.minWidth = 80;
            
            roundingRow.Add(roundingField);
            
            container.Add(roundingRow);
            
            // Snap interval row (conditional)
            var snapRow = new VisualElement
            {
                name = "snapRow",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 4
                }
            };
            
            roundingField.RegisterValueChangedCallback(evt =>
            {
                roundingProp.enumValueIndex = (int)(EAttributeRoundingPolicy)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                UpdateSnapVisibility(snapRow, roundingProp);
                UpdateSummary(summaryContainer, autoClampProp, autoScaleProp, roundingProp);
            });
            
            var snapLabel = new Label("Interval")
            {
                tooltip = "Snap values to this interval (e.g., 0.5 for half-integer values)",
                style =
                {
                    fontSize = 10,
                    color = Colors.HintText,
                    width = 55
                }
            };
            snapRow.Add(snapLabel);
            
            var snapField = new FloatField { value = snapIntervalProp.floatValue };
            snapField.style.flexGrow = 1;
            snapField.RegisterValueChangedCallback(evt =>
            {
                snapIntervalProp.floatValue = Mathf.Max(0.001f, evt.newValue); // Prevent zero/negative
                property.serializedObject.ApplyModifiedProperties();
            });
            snapRow.Add(snapField);
            
            container.Add(snapRow);
            
            // Initial state
            UpdateSnapVisibility(snapRow, roundingProp);
            UpdateSummary(summaryContainer, autoClampProp, autoScaleProp, roundingProp);
            
            return root;
        }
        
        private void UpdateSnapVisibility(VisualElement snapRow, SerializedProperty roundingProp)
        {
            bool showSnap = (EAttributeRoundingPolicy)roundingProp.enumValueIndex == EAttributeRoundingPolicy.SnapTo;
            snapRow.style.display = showSnap ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void UpdateSummary(
            VisualElement container, 
            SerializedProperty clampProp, 
            SerializedProperty scaleProp,
            SerializedProperty roundingProp)
        {
            container.Clear();
            
            int activeCount = 0;
            if (clampProp.boolValue) activeCount++;
            if (scaleProp.boolValue) activeCount++;
            if ((EAttributeRoundingPolicy)roundingProp.enumValueIndex != EAttributeRoundingPolicy.None) activeCount++;
            
            if (activeCount == 0)
            {
                container.Add(new Label("None")
                {
                    style =
                    {
                        fontSize = 9,
                        color = Colors.HintText,
                        unityFontStyleAndWeight = FontStyle.Italic
                    }
                });
            }
            else
            {
                if (clampProp.boolValue)
                {
                    container.Add(CreateMicroBadge("C", Colors.AccentGreen, "Clamping enabled"));
                }
                if (scaleProp.boolValue)
                {
                    var badge = CreateMicroBadge("S", Colors.AccentBlue, "Scale with base enabled");
                    badge.style.marginLeft = 2;
                    container.Add(badge);
                }
                
                var rounding = (EAttributeRoundingPolicy)roundingProp.enumValueIndex;
                if (rounding != EAttributeRoundingPolicy.None)
                {
                    string roundChar = rounding switch
                    {
                        EAttributeRoundingPolicy.ToFloor => "F",
                        EAttributeRoundingPolicy.ToCeil => "C",
                        EAttributeRoundingPolicy.Round => "≈",
                        EAttributeRoundingPolicy.SnapTo => "|",
                        _ => "R"
                    };
                    var badge = CreateMicroBadge(roundChar, Colors.AccentOrange, $"Rounding: {rounding}");
                    badge.style.marginLeft = 2;
                    container.Add(badge);
                }
            }
        }
        
        private VisualElement CreateMicroBadge(string text, Color color, string tooltip)
        {
            var badge = new Label(text)
            {
                tooltip = tooltip,
                style =
                {
                    fontSize = 9,
                    color = color,
                    backgroundColor = new Color(color.r, color.g, color.b, 0.15f),
                    paddingLeft = 3,
                    paddingRight = 3,
                    paddingTop = 1,
                    paddingBottom = 1,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    minWidth = 14
                }
            };
            return badge;
        }
    }
}
