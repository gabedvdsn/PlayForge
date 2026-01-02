using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// UIElements property drawer for LevelThreshold - compact inline format.
    /// </summary>
    [CustomPropertyDrawer(typeof(LevelThreshold))]
    public class LevelThresholdDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "LevelThreshold" };
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginBottom = 2;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;
            root.style.backgroundColor = Colors.ItemBackground;
            root.style.borderTopLeftRadius = 3;
            root.style.borderTopRightRadius = 3;
            root.style.borderBottomLeftRadius = 3;
            root.style.borderBottomRightRadius = 3;
            
            // Level label
            var lvLabel = new Label("Lv");
            lvLabel.style.color = Colors.AccentBlue;
            lvLabel.style.fontSize = 10;
            lvLabel.style.marginRight = 2;
            root.Add(lvLabel);
            
            // Level field
            var levelProp = property.FindPropertyRelative("Level");
            var levelField = new IntegerField { value = levelProp.intValue };
            levelField.style.width = 40;
            levelField.style.marginRight = 6;
            levelField.RegisterValueChangedCallback(evt =>
            {
                levelProp.intValue = evt.newValue;
                levelProp.serializedObject.ApplyModifiedProperties();
            });
            root.Add(levelField);
            
            // Arrow
            var arrow = new Label("→");
            arrow.style.color = Colors.HintText;
            arrow.style.marginRight = 6;
            root.Add(arrow);
            
            // Value field
            var valueProp = property.FindPropertyRelative("Value");
            var valueField = new FloatField { value = valueProp.floatValue };
            valueField.style.width = 60;
            valueField.style.marginRight = 8;
            valueField.RegisterValueChangedCallback(evt =>
            {
                valueProp.floatValue = evt.newValue;
                valueProp.serializedObject.ApplyModifiedProperties();
            });
            root.Add(valueField);
            
            // Tier name
            var tierNameProp = property.FindPropertyRelative("TierName");
            var tierField = new TextField { value = tierNameProp.stringValue };
            tierField.style.flexGrow = 1;
            tierField.style.minWidth = 60;
            if (string.IsNullOrEmpty(tierNameProp.stringValue))
            {
                tierField.style.color = Colors.HintText;
            }
            tierField.RegisterValueChangedCallback(evt =>
            {
                tierNameProp.stringValue = evt.newValue;
                tierNameProp.serializedObject.ApplyModifiedProperties();
                tierField.style.color = string.IsNullOrEmpty(evt.newValue) ? Colors.HintText : Colors.LabelText;
            });
            root.Add(tierField);
            
            return root;
        }
    }
    
    /// <summary>
    /// UIElements property drawer for MagnitudeModifierGroupMember.
    /// Shows operation indicator, type label, and nested scaler.
    /// </summary>
    [CustomPropertyDrawer(typeof(MagnitudeModifierGroupMember))]
    public class MagnitudeModifierGroupMemberDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "GroupMember" };
            root.style.marginBottom = 4;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.backgroundColor = Colors.SubsectionBackground;
            root.style.borderTopLeftRadius = 4;
            root.style.borderTopRightRadius = 4;
            root.style.borderBottomLeftRadius = 4;
            root.style.borderBottomRightRadius = 4;
            
            var opProp = property.FindPropertyRelative("RelativeOperation");
            var calcProp = property.FindPropertyRelative("Calculation");
            
            // Header row
            var header = CreateRow(4);
            
            // Operation color bar
            var operation = (ECalculationOperation)opProp.enumValueIndex;
            Color opColor = operation switch
            {
                ECalculationOperation.Add => Colors.AccentGreen,
                ECalculationOperation.Multiply => Colors.AccentBlue,
                ECalculationOperation.Override => Colors.AccentOrange,
                _ => Colors.AccentGray
            };
            
            var colorBar = new VisualElement();
            colorBar.style.width = 4;
            colorBar.style.height = 20;
            colorBar.style.backgroundColor = opColor;
            colorBar.style.borderTopLeftRadius = 2;
            colorBar.style.borderBottomLeftRadius = 2;
            colorBar.style.marginRight = 6;
            header.Add(colorBar);
            
            // Operation symbol
            string opSymbol = operation switch
            {
                ECalculationOperation.Add => "+",
                ECalculationOperation.Multiply => "×",
                ECalculationOperation.Override => "=",
                _ => "?"
            };
            
            var opLabel = new Label(opSymbol);
            opLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            opLabel.style.fontSize = 14;
            opLabel.style.color = opColor;
            opLabel.style.marginRight = 4;
            opLabel.style.width = 16;
            opLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(opLabel);
            
            // Operation dropdown
            var opField = new EnumField(operation);
            opField.style.width = 80;
            opField.RegisterValueChangedCallback(evt =>
            {
                opProp.enumValueIndex = (int)(ECalculationOperation)evt.newValue;
                opProp.serializedObject.ApplyModifiedProperties();
                
                // Update visual
                var newOp = (ECalculationOperation)evt.newValue;
                colorBar.style.backgroundColor = newOp switch
                {
                    ECalculationOperation.Add => Colors.AccentGreen,
                    ECalculationOperation.Multiply => Colors.AccentBlue,
                    ECalculationOperation.Override => Colors.AccentOrange,
                    _ => Colors.AccentGray
                };
                opLabel.text = newOp switch
                {
                    ECalculationOperation.Add => "+",
                    ECalculationOperation.Multiply => "×",
                    ECalculationOperation.Override => "=",
                    _ => "?"
                };
                opLabel.style.color = colorBar.style.backgroundColor;
            });
            header.Add(opField);
            
            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            
            // Type indicator
            if (calcProp.managedReferenceValue != null)
            {
                string typeName = calcProp.managedReferenceValue.GetType().Name;
                if (typeName.EndsWith("Scaler"))
                    typeName = typeName.Substring(0, typeName.Length - 6);
                
                var typeLabel = new Label(typeName);
                typeLabel.style.fontSize = 10;
                typeLabel.style.color = Colors.HintText;
                typeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                header.Add(typeLabel);
            }
            
            root.Add(header);
            
            // Scaler content
            var scalerField = new PropertyField(calcProp, "");
            scalerField.style.marginTop = 4;
            scalerField.BindProperty(calcProp);
            root.Add(scalerField);
            
            return root;
        }
    }
    
    /// <summary>
    /// UIElements property drawer for CachedMagnitudeModifierGroupMember.
    /// </summary>
    [CustomPropertyDrawer(typeof(CachedMagnitudeModifierGroupMember))]
    public class CachedMagnitudeModifierGroupMemberDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "CachedGroupMember" };
            root.style.marginBottom = 4;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 4;
            root.style.backgroundColor = Colors.SubsectionBackground;
            root.style.borderTopLeftRadius = 4;
            root.style.borderTopRightRadius = 4;
            root.style.borderBottomLeftRadius = 4;
            root.style.borderBottomRightRadius = 4;
            root.style.borderLeftWidth = 2;
            root.style.borderLeftColor = Colors.AccentCyan;
            
            var opProp = property.FindPropertyRelative("RelativeOperation");
            var calcProp = property.FindPropertyRelative("Calculation");
            
            // Header row
            var header = CreateRow(4);
            
            var operation = (ECalculationOperation)opProp.enumValueIndex;
            string opSymbol = operation switch
            {
                ECalculationOperation.Add => "+",
                ECalculationOperation.Multiply => "×",
                ECalculationOperation.Override => "=",
                _ => "?"
            };
            
            var opLabel = new Label(opSymbol);
            opLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            opLabel.style.fontSize = 12;
            opLabel.style.marginRight = 4;
            header.Add(opLabel);
            
            var opField = new EnumField(operation);
            opField.style.width = 80;
            opField.RegisterValueChangedCallback(evt =>
            {
                opProp.enumValueIndex = (int)(ECalculationOperation)evt.newValue;
                opProp.serializedObject.ApplyModifiedProperties();
            });
            header.Add(opField);
            
            var spacer = CreateFlexSpacer();
            header.Add(spacer);
            
            var cachedLabel = new Label("CACHED");
            cachedLabel.style.fontSize = 8;
            cachedLabel.style.color = Colors.AccentCyan;
            cachedLabel.style.paddingLeft = 4;
            cachedLabel.style.paddingRight = 4;
            cachedLabel.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f, 0.3f);
            cachedLabel.style.borderTopLeftRadius = 2;
            cachedLabel.style.borderTopRightRadius = 2;
            cachedLabel.style.borderBottomLeftRadius = 2;
            cachedLabel.style.borderBottomRightRadius = 2;
            header.Add(cachedLabel);
            
            root.Add(header);
            
            var scalerField = new PropertyField(calcProp, "");
            scalerField.style.marginTop = 4;
            scalerField.BindProperty(calcProp);
            root.Add(scalerField);
            
            return root;
        }
    }
}