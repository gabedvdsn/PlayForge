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
            if (type == typeof(SimpleScaler) || type == typeof(CachedSimpleScaler))
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
            
            if (type == typeof(CachedAttributeBackedScaler))
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
        // RandomizedScaler - Mode-dependent UI
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddRandomizedScalerProperties(VisualElement content, SerializedProperty property, VisualElement root)
        {
            var modeProp = property.FindPropertyRelative("VarianceMode");
            var currentMode = modeProp != null ? (EVarianceMode)modeProp.enumValueIndex : EVarianceMode.Percentage;
            
            // Variance Mode dropdown - use EnumField for reliable change detection
            var modeRow = CreateRow(4);
            modeRow.Add(new Label("Mode") { style = { width = 80, color = Colors.LabelText }, tooltip = "How variance is applied" });
            
            var modeField = new EnumField(currentMode);
            modeField.style.flexGrow = 1;
            
            // Capture for callback
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            
            modeField.RegisterValueChangedCallback(evt =>
            {
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                var freshModeProp = freshProp?.FindPropertyRelative("VarianceMode");
                if (freshModeProp != null)
                {
                    freshModeProp.enumValueIndex = Convert.ToInt32(evt.newValue);
                    so.ApplyModifiedProperties();
                    ScheduleRebuild(root, freshProp);
                }
            });
            modeRow.Add(modeField);
            content.Add(modeRow);
            
            // Mode-specific fields
            switch (currentMode)
            {
                case EVarianceMode.Percentage:
                    AddVariancePercentageFields(content, property);
                    break;
                    
                case EVarianceMode.Flat:
                    AddVarianceFlatFields(content, property);
                    break;
                    
                case EVarianceMode.Multiplier:
                    AddVarianceMultiplierFields(content, property);
                    break;
            }
        }
        
        private void AddVariancePercentageFields(VisualElement content, SerializedProperty property)
        {
            var varianceProp = property.FindPropertyRelative("Variance");
            if (varianceProp == null) return;
            
            var row = CreateRow(4);
            row.Add(new Label("Variance") { style = { width = 80, color = Colors.LabelText }, tooltip = "Random ± percentage of base value" });
            
            var field = new FloatField { value = varianceProp.floatValue };
            field.style.width = 60;
            field.tooltip = "0.1 = ±10%";
            field.RegisterValueChangedCallback(evt =>
            {
                varianceProp.floatValue = Mathf.Clamp01(evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);
            
            var slider = new Slider(0f, 1f) { value = varianceProp.floatValue };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                varianceProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(slider);
            
            content.Add(row);
            
            // Preview hint
            float pct = varianceProp.floatValue * 100f;
            var hintLabel = new Label($"Result: base value ± {pct:F0}%");
            hintLabel.style.fontSize = 9;
            hintLabel.style.color = Colors.HintText;
            hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            hintLabel.style.marginTop = 2;
            content.Add(hintLabel);
        }
        
        private void AddVarianceFlatFields(VisualElement content, SerializedProperty property)
        {
            var varianceProp = property.FindPropertyRelative("Variance");
            if (varianceProp == null) return;
            
            var row = CreateRow(4);
            row.Add(new Label("Flat ±") { style = { width = 80, color = Colors.LabelText }, tooltip = "Flat amount added/subtracted" });
            
            var field = new FloatField { value = varianceProp.floatValue };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                varianceProp.floatValue = Mathf.Max(0f, evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);
            
            content.Add(row);
            
            // Preview hint
            var hintLabel = new Label($"Result: base value ± {varianceProp.floatValue:F2}");
            hintLabel.style.fontSize = 9;
            hintLabel.style.color = Colors.HintText;
            hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            hintLabel.style.marginTop = 2;
            content.Add(hintLabel);
        }
        
        private void AddVarianceMultiplierFields(VisualElement content, SerializedProperty property)
        {
            var minProp = property.FindPropertyRelative("MinMultiplier");
            var maxProp = property.FindPropertyRelative("MaxMultiplier");
            
            if (minProp == null || maxProp == null) return;
            
            var row = CreateRow(4);
            row.Add(new Label("Multiplier") { style = { width = 80, color = Colors.LabelText }, tooltip = "Random multiplier range" });
            
            var minField = new FloatField { value = minProp.floatValue };
            minField.style.width = 50;
            minField.tooltip = "Minimum multiplier";
            minField.RegisterValueChangedCallback(evt =>
            {
                minProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(minField);
            
            row.Add(new Label("→") { style = { marginLeft = 4, marginRight = 4, color = Colors.HintText } });
            
            var maxField = new FloatField { value = maxProp.floatValue };
            maxField.style.width = 50;
            maxField.tooltip = "Maximum multiplier";
            maxField.RegisterValueChangedCallback(evt =>
            {
                maxProp.floatValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(maxField);
            
            content.Add(row);
            
            // Preview hint
            var hintLabel = new Label($"Result: base × [{minProp.floatValue:F2}, {maxProp.floatValue:F2}]");
            hintLabel.style.fontSize = 9;
            hintLabel.style.color = Colors.HintText;
            hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            hintLabel.style.marginTop = 2;
            content.Add(hintLabel);
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
                var sourceField = new PropertyField(sourceProp, "Capture From");
                sourceField.BindProperty(sourceProp);
                sourceField.style.marginBottom = 2;
                content.Add(sourceField);
            }
            
            // Which value
            var whichProp = property.FindPropertyRelative("CaptureWhen");
            if (whichProp != null)
            {
                var whichField = new PropertyField(whichProp, "Capture When");
                whichField.BindProperty(whichProp);
                whichField.style.marginBottom = 2;
                content.Add(whichField);
            }
            
            // Operation
            var opProp = property.FindPropertyRelative("CaptureWhat");
            if (opProp != null)
            {
                var opField = new PropertyField(opProp, "Capture Target");
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
        // ConditionalScaler - Condition-dependent UI with proper click handling
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void AddConditionalScalerProperties(VisualElement content, SerializedProperty property, VisualElement root)
        {
            var condProp = property.FindPropertyRelative("Condition");
            var currentCondition = condProp != null ? (EScalerCondition)condProp.enumValueIndex : EScalerCondition.Always;
            
            // Condition dropdown - use EnumField directly for reliable interaction
            var condRow = CreateRow(4);
            condRow.Add(new Label("Condition") { style = { width = 80, color = Colors.LabelText }, tooltip = "The condition to evaluate" });
            
            var condField = new EnumField(currentCondition);
            condField.style.flexGrow = 1;
            
            // Capture for callback
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            
            condField.RegisterValueChangedCallback(evt =>
            {
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                var freshCondProp = freshProp?.FindPropertyRelative("Condition");
                if (freshCondProp != null)
                {
                    freshCondProp.enumValueIndex = Convert.ToInt32(evt.newValue);
                    so.ApplyModifiedProperties();
                    ScheduleRebuild(root, freshProp);
                }
            });
            condRow.Add(condField);
            content.Add(condRow);
            
            // Condition-specific fields
            AddConditionSpecificFields(content, property, currentCondition);
            
            // True/False scalers section
            AddConditionalScalerBranches(content, property);
        }
        
        private void AddConditionSpecificFields(VisualElement content, SerializedProperty property, EScalerCondition condition)
        {
            // Tag-based conditions
            if (condition == EScalerCondition.SourceTagCondition || condition == EScalerCondition.TargetTagCondition)
            {
                var tagQueryProp = property.FindPropertyRelative("TagCondition");
                if (tagQueryProp != null)
                {
                    var targetLabel = condition == EScalerCondition.SourceTagCondition ? "Source" : "Target";
                    
                    var tagSection = new VisualElement();
                    tagSection.style.marginTop = 4;
                    tagSection.style.paddingLeft = 4;
                    tagSection.style.borderLeftWidth = 2;
                    tagSection.style.borderLeftColor = Colors.AccentCyan;
                    
                    var tagLabel = new Label($"{targetLabel} Tag Condition");
                    tagLabel.style.fontSize = 10;
                    tagLabel.style.color = Colors.AccentCyan;
                    tagLabel.style.marginBottom = 2;
                    tagSection.Add(tagLabel);
                    
                    var tagField = new PropertyField(tagQueryProp, "");
                    tagField.BindProperty(tagQueryProp);
                    tagSection.Add(tagField);
                    
                    content.Add(tagSection);
                }
            }
            
            // Attribute threshold conditions
            if (condition == EScalerCondition.SourceAttributeThreshold || condition == EScalerCondition.TargetAttributeThreshold)
            {
                var targetLabel = condition == EScalerCondition.SourceAttributeThreshold ? "Source" : "Target";
                
                var attrSection = new VisualElement();
                attrSection.style.marginTop = 4;
                attrSection.style.paddingLeft = 4;
                attrSection.style.borderLeftWidth = 2;
                attrSection.style.borderLeftColor = Colors.AccentPurple;
                
                var sectionLabel = new Label($"{targetLabel} Attribute Threshold");
                sectionLabel.style.fontSize = 10;
                sectionLabel.style.color = Colors.AccentPurple;
                sectionLabel.style.marginBottom = 2;
                attrSection.Add(sectionLabel);
                
                var attrProp = property.FindPropertyRelative("CheckAttribute");
                if (attrProp != null)
                {
                    var attrField = new PropertyField(attrProp, "Attribute");
                    attrField.BindProperty(attrProp);
                    attrField.style.marginBottom = 2;
                    attrSection.Add(attrField);
                }
                
                // Comparison and threshold on same row
                var compRow = CreateRow(2);
                
                var compProp = property.FindPropertyRelative("Comparison");
                if (compProp != null)
                {
                    var compField = new EnumField((EComparisonOperator)compProp.enumValueIndex);
                    compField.style.width = 100;
                    compField.RegisterValueChangedCallback(evt =>
                    {
                        compProp.enumValueIndex = Convert.ToInt32(evt.newValue);
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    compRow.Add(compField);
                }
                
                var threshProp = property.FindPropertyRelative("ThresholdValue");
                if (threshProp != null)
                {
                    var threshField = new FloatField { value = threshProp.floatValue };
                    threshField.style.flexGrow = 1;
                    threshField.style.marginLeft = 4;
                    threshField.RegisterValueChangedCallback(evt =>
                    {
                        threshProp.floatValue = evt.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    compRow.Add(threshField);
                }
                
                attrSection.Add(compRow);
                content.Add(attrSection);
            }
            
            // Level threshold conditions
            if (condition == EScalerCondition.LevelAbove || condition == EScalerCondition.LevelBelow || condition == EScalerCondition.RelativeLevelAbove)
            {
                var threshProp = property.FindPropertyRelative("ThresholdValue");
                if (threshProp != null)
                {
                    var threshRow = CreateRow(4);
                    
                    string label = condition == EScalerCondition.RelativeLevelAbove ? "Relative Level >" : "Level Threshold";
                    threshRow.Add(new Label(label) { style = { width = 100, color = Colors.LabelText } });
                    
                    var threshField = new FloatField { value = threshProp.floatValue };
                    threshField.style.width = 60;
                    threshField.RegisterValueChangedCallback(evt =>
                    {
                        threshProp.floatValue = evt.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    threshRow.Add(threshField);
                    
                    if (condition == EScalerCondition.RelativeLevelAbove)
                    {
                        var hint = new Label("(0-1)");
                        hint.style.marginLeft = 4;
                        hint.style.fontSize = 9;
                        hint.style.color = Colors.HintText;
                        threshRow.Add(hint);
                    }
                    
                    content.Add(threshRow);
                }
            }
            
            // Always/Never - just show hint
            if (condition == EScalerCondition.Always)
            {
                var hintLabel = new Label("Always uses TRUE scaler");
                hintLabel.style.fontSize = 9;
                hintLabel.style.color = Colors.AccentGreen;
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 2;
                content.Add(hintLabel);
            }
            
            if (condition == EScalerCondition.Never)
            {
                var hintLabel = new Label("Always uses FALSE scaler");
                hintLabel.style.fontSize = 9;
                hintLabel.style.color = new Color(1f, 0.6f, 0.2f);
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 2;
                content.Add(hintLabel);
            }
        }
        
        private void AddConditionalScalerBranches(VisualElement content, SerializedProperty property)
        {
            // Separator
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            separator.style.marginTop = 8;
            separator.style.marginBottom = 6;
            content.Add(separator);
            
            // TRUE branch
            var trueSection = new VisualElement();
            trueSection.style.paddingLeft = 4;
            trueSection.style.borderLeftWidth = 3;
            trueSection.style.borderLeftColor = Colors.AccentGreen;
            trueSection.style.marginBottom = 6;
            
            var trueLabel = new Label("▶ If TRUE:");
            trueLabel.style.fontSize = 10;
            trueLabel.style.color = Colors.AccentGreen;
            trueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            trueLabel.style.marginBottom = 2;
            trueSection.Add(trueLabel);
            
            var trueProp = property.FindPropertyRelative("TrueScaler");
            if (trueProp != null)
            {
                var trueField = new PropertyField(trueProp, "");
                trueField.BindProperty(trueProp);
                trueSection.Add(trueField);
            }
            
            content.Add(trueSection);
            
            // FALSE branch
            var falseSection = new VisualElement();
            falseSection.style.paddingLeft = 4;
            falseSection.style.borderLeftWidth = 3;
            falseSection.style.borderLeftColor = new Color(1f, 0.6f, 0.2f);
            
            var falseLabel = new Label("▶ If FALSE:");
            falseLabel.style.fontSize = 10;
            falseLabel.style.color = new Color(1f, 0.6f, 0.2f);
            falseLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            falseLabel.style.marginBottom = 2;
            falseSection.Add(falseLabel);
            
            var falseProp = property.FindPropertyRelative("FalseScaler");
            if (falseProp != null)
            {
                var falseField = new PropertyField(falseProp, "");
                falseField.BindProperty(falseProp);
                falseSection.Add(falseField);
            }
            
            content.Add(falseSection);
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
            
            if (isCached)
            {
                var hintLabel = new Label("(Group caches member results)");
                hintLabel.style.fontSize = 9;
                hintLabel.style.color = Colors.AccentGreen;
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 4;
                content.Add(hintLabel);
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