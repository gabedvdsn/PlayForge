using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class ScalerDrawer
    {
        // Base class field names to exclude from reflection-based display
        private static readonly HashSet<string> BaseScalerFields = new HashSet<string>
        {
            "Configuration", "MaxLevel", "LevelValues", "Interpolation", "Scaling", "Behaviours"
        };
        
        /// <summary>
        /// Adds type-specific property fields for each scaler type.
        /// Falls back to reflection for unknown/custom scaler types.
        /// </summary>
        private void AddTypeSpecificProperties(VisualElement content, SerializedProperty property, Type type, VisualElement root)
        {
            var scaler = property.managedReferenceValue as AbstractScaler;
            if (scaler == null) return;
            
            // ═══════════════════════════════════════════════════════════════════════════
            // Known Types - Custom UI
            // ═══════════════════════════════════════════════════════════════════════════
            
            // SimpleScaler / AttrCacheSimpleScaler - no extra fields
            if (type == typeof(SimpleScaler) || type == typeof(AttrCacheSimpleScaler))
                return;
            
            if (type == typeof(ConstantScaler))
            {
                AddConstantScalerProperties(content, property);
                return;
            }
            
            if (type == typeof(ClampedScaler))
            {
                AddClampedScalerProperties(content, property);
                return;
            }
            
            if (type == typeof(RandomizedScaler))
            {
                AddRandomizedScalerProperties(content, property, root);
                return;
            }
            
            if (type == typeof(AttributeBackedScaler))
            {
                AddAttributeBackedScalerProperties(content, property, false);
                return;
            }
            
            if (type == typeof(AttrCacheBackedScaler))
            {
                AddAttributeBackedScalerProperties(content, property, true);
                return;
            }
            
            if (type == typeof(ConditionalScaler))
            {
                AddConditionalScalerProperties(content, property, root);
                return;
            }
            
            if (type == typeof(ScalerGroup))
            {
                AddScalerGroupProperties(content, property, false);
                return;
            }
            
            if (type == typeof(CachedScalerGroup))
            {
                AddScalerGroupProperties(content, property, true);
                return;
            }
            
            if (type == typeof(ThresholdScaler))
            {
                AddThresholdScalerProperties(content, property);
                return;
            }
            
            // ═══════════════════════════════════════════════════════════════════════════
            // Fallback: Reflection-based for user-created scalers
            // ═══════════════════════════════════════════════════════════════════════════
            AddReflectionBasedProperties(content, property, type);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // Reflection Fallback for Custom Scalers
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddReflectionBasedProperties(VisualElement content, SerializedProperty property, Type type)
        {
            var headerLabel = new Label($"[{FormatTypeName(type.Name)}]");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.AccentPurple;
            headerLabel.style.marginBottom = 4;
            content.Add(headerLabel);
            
            // Iterate through serialized properties
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                    
                    // Skip base class fields
                    if (BaseScalerFields.Contains(iterator.name))
                        continue;
                    
                    // Create PropertyField for each custom field
                    var field = new PropertyField(iterator.Copy());
                    field.BindProperty(iterator.Copy());
                    field.style.marginBottom = 2;
                    content.Add(field);
                    
                } while (iterator.NextVisible(false));
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ConstantScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddConstantScalerProperties(VisualElement content, SerializedProperty property)
        {
            var row = CreateRow(4);
            row.Add(new Label("Value") { style = { width = 80, color = Colors.LabelText }, tooltip = "The constant value to return" });
            
            var valueProp = property.FindPropertyRelative("Value");
            var valueField = new FloatField { value = valueProp?.floatValue ?? 1f };
            valueField.style.flexGrow = 1;
            valueField.RegisterValueChangedCallback(evt =>
            {
                if (valueProp != null) valueProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(valueField);
            
            content.Add(row);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ClampedScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddClampedScalerProperties(VisualElement content, SerializedProperty property)
        {
            var row = CreateRow(4);
            var minProp = property.FindPropertyRelative("MinValue");
            var maxProp = property.FindPropertyRelative("MaxValue");
            
            row.Add(new Label("Clamp") { style = { width = 50, color = Colors.LabelText }, tooltip = "Min/Max clamp bounds" });
            
            var minField = new FloatField { value = minProp?.floatValue ?? 0f };
            minField.style.width = 60;
            minField.tooltip = "Minimum value (floor)";
            minField.RegisterValueChangedCallback(evt =>
            {
                if (minProp != null) minProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(minField);
            
            row.Add(new Label("→") { style = { marginLeft = 4, marginRight = 4, color = Colors.HintText } });
            
            var maxField = new FloatField { value = maxProp?.floatValue ?? 100f };
            maxField.style.width = 60;
            maxField.tooltip = "Maximum value (ceiling)";
            maxField.RegisterValueChangedCallback(evt =>
            {
                if (maxProp != null) maxProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(maxField);
            
            content.Add(row);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // RandomizedScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddRandomizedScalerProperties(VisualElement content, SerializedProperty property, VisualElement root)
        {
            var varianceProp = property.FindPropertyRelative("Variance");
            
            var row = CreateRow(4);
            row.Add(new Label("Variance %") { style = { width = 80, color = Colors.LabelText }, tooltip = "Random ± percentage applied to final value" });
            
            var field = new FloatField { value = varianceProp?.floatValue ?? 0.1f };
            field.style.width = 60;
            field.tooltip = "0.1 = ±10%";
            field.RegisterValueChangedCallback(evt =>
            {
                if (varianceProp != null) varianceProp.floatValue = Mathf.Clamp01(evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);
            
            var slider = new Slider(0f, 1f) { value = varianceProp?.floatValue ?? 0.1f };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                if (varianceProp != null) varianceProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(slider);
            
            content.Add(row);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // AttributeBackedScaler / AttrCacheBackedScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddAttributeBackedScalerProperties(VisualElement content, SerializedProperty property, bool isCached)
        {
            // Target attribute
            var attrProp = property.FindPropertyRelative("CaptureAttribute");
            if (attrProp != null)
            {
                var attrField = new PropertyField(attrProp, "Attribute");
                attrField.BindProperty(attrProp);
                attrField.style.marginBottom = 2;
                content.Add(attrField);
            }
            
            // Get from source/target
            var sourceProp = property.FindPropertyRelative("CaptureFrom");
            if (sourceProp != null)
            {
                var sourceField = new PropertyField(attrProp, "\tCapture From");
                sourceField.BindProperty(sourceProp);
                sourceField.style.marginBottom = 2;
                content.Add(sourceField);
            }
            
            // Which value
            var whichProp = property.FindPropertyRelative("CaptureWhen");
            if (whichProp != null)
            {
                var whichField = new PropertyField(whichProp, "\tCapture When");
                whichField.BindProperty(whichProp);
                whichField.style.marginBottom = 2;
                content.Add(whichField);
            }
            
            // Operation
            var opProp = property.FindPropertyRelative("CaptureWhat");
            if (opProp != null)
            {
                var opField = new PropertyField(opProp, "\tCapture Target");
                opField.BindProperty(opProp);
                opField.style.marginBottom = 2;
                content.Add(opField);
            }
            
            if (isCached)
            {
                var hintLabel = new Label("(Scaler uses cached attribute values)");
                hintLabel.style.fontSize = 9;
                hintLabel.style.color = Colors.AccentGreen;
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                content.Add(hintLabel);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ConditionalScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddConditionalScalerProperties(VisualElement content, SerializedProperty property, VisualElement root)
        {
            // Condition type
            var condProp = property.FindPropertyRelative("Condition");
            if (condProp != null)
            {
                var condField = new PropertyField(condProp);
                condField.BindProperty(condProp);
                condField.style.marginBottom = 2;
                condField.RegisterValueChangeCallback(_ => ScheduleRebuild(root, property));
                content.Add(condField);
            }
            
            var condition = condProp != null ? (EScalerCondition)condProp.enumValueIndex : EScalerCondition.Always;
            
            // Tag-based conditions
            if (condition == EScalerCondition.SourceHasTag || condition == EScalerCondition.TargetHasTag ||
                condition == EScalerCondition.SourceMissingTag || condition == EScalerCondition.TargetMissingTag)
            {
                var tagProp = property.FindPropertyRelative("RequiredTag");
                if (tagProp != null)
                {
                    var tagField = new PropertyField(tagProp, "Tag");
                    tagField.BindProperty(tagProp);
                    tagField.style.marginBottom = 2;
                    content.Add(tagField);
                }
                
                var weightProp = property.FindPropertyRelative("RequiredWeight");
                if (weightProp != null)
                {
                    var weightRow = CreateRow(4);
                    weightRow.Add(new Label("Min Weight") { style = { width = 80, color = Colors.LabelText } });
                    var weightField = new IntegerField { value = weightProp.intValue };
                    weightField.style.width = 60;
                    weightField.RegisterValueChangedCallback(e =>
                    {
                        weightProp.intValue = e.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    weightRow.Add(weightField);
                    content.Add(weightRow);
                }
            }
            
            // Attribute threshold conditions
            if (condition == EScalerCondition.SourceAttributeThreshold || condition == EScalerCondition.TargetAttributeThreshold)
            {
                var attrProp = property.FindPropertyRelative("CheckAttribute");
                if (attrProp != null)
                {
                    var attrField = new PropertyField(attrProp, "Attribute");
                    attrField.BindProperty(attrProp);
                    attrField.style.marginBottom = 2;
                    content.Add(attrField);
                }
                
                var compProp = property.FindPropertyRelative("Comparison");
                if (compProp != null)
                {
                    var compField = new PropertyField(compProp);
                    compField.BindProperty(compProp);
                    compField.style.marginBottom = 2;
                    content.Add(compField);
                }
            }
            
            // Level/threshold conditions
            if (condition == EScalerCondition.LevelAbove || condition == EScalerCondition.LevelBelow ||
                condition == EScalerCondition.RelativeLevelAbove || 
                condition == EScalerCondition.SourceAttributeThreshold || condition == EScalerCondition.TargetAttributeThreshold)
            {
                var threshProp = property.FindPropertyRelative("ThresholdValue");
                if (threshProp != null)
                {
                    var threshRow = CreateRow(4);
                    threshRow.Add(new Label("Threshold") { style = { width = 80, color = Colors.LabelText } });
                    var threshField = new FloatField { value = threshProp.floatValue };
                    threshField.style.width = 60;
                    threshField.RegisterValueChangedCallback(e =>
                    {
                        threshProp.floatValue = e.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    threshRow.Add(threshField);
                    content.Add(threshRow);
                }
            }
            
            // True/False scalers
            var trueLabel = new Label("If TRUE:") { style = { fontSize = 10, color = Colors.AccentGreen, marginTop = 6 } };
            content.Add(trueLabel);
            
            var trueProp = property.FindPropertyRelative("TrueScaler");
            if (trueProp != null)
            {
                var trueField = new PropertyField(trueProp, "");
                trueField.BindProperty(trueProp);
                trueField.style.marginBottom = 2;
                content.Add(trueField);
            }
            
            var falseLabel = new Label("If FALSE:") { style = { fontSize = 10, color = new Color(1f, 0.6f, 0.2f), marginTop = 4 } };
            content.Add(falseLabel);
            
            var falseProp = property.FindPropertyRelative("FalseScaler");
            if (falseProp != null)
            {
                var falseField = new PropertyField(falseProp, "");
                falseField.BindProperty(falseProp);
                content.Add(falseField);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ScalerGroup / CachedScalerGroup
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddScalerGroupProperties(VisualElement content, SerializedProperty property, bool isCached)
        {
            // Override collision policy
            var policyProp = property.FindPropertyRelative("OverrideMemberCollisionPolicy");
            if (policyProp != null)
            {
                var policyField = new PropertyField(policyProp, "Override Policy");
                policyField.BindProperty(policyProp);
                policyField.style.marginBottom = 4;
                content.Add(policyField);
            }
            
            // Calculations array
            var calcProp = property.FindPropertyRelative("Calculations");
            if (calcProp != null)
            {
                var calcLabel = new Label($"Calculations ({calcProp.arraySize})");
                calcLabel.style.fontSize = 10;
                calcLabel.style.color = Colors.HintText;
                calcLabel.style.marginTop = 4;
                content.Add(calcLabel);
                
                var calcField = new PropertyField(calcProp, "");
                calcField.BindProperty(calcProp);
                content.Add(calcField);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ThresholdScaler
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddThresholdScalerProperties(VisualElement content, SerializedProperty property)
        {
            // Default value
            var defaultProp = property.FindPropertyRelative("DefaultValue");
            if (defaultProp != null)
            {
                var defaultRow = CreateRow(4);
                defaultRow.Add(new Label("Default") { style = { width = 60, color = Colors.LabelText }, tooltip = "Value if below all thresholds" });
                
                var defaultField = new FloatField { value = defaultProp.floatValue };
                defaultField.style.width = 60;
                defaultField.RegisterValueChangedCallback(e =>
                {
                    defaultProp.floatValue = e.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });
                defaultRow.Add(defaultField);
                content.Add(defaultRow);
            }
            
            // Thresholds array
            var threshProp = property.FindPropertyRelative("Thresholds");
            if (threshProp != null)
            {
                var threshLabel = new Label($"Thresholds ({threshProp.arraySize})");
                threshLabel.style.fontSize = 10;
                threshLabel.style.color = Colors.HintText;
                threshLabel.style.marginTop = 4;
                content.Add(threshLabel);
                
                var threshField = new PropertyField(threshProp, "");
                threshField.BindProperty(threshProp);
                content.Add(threshField);
            }
        }
    }
}