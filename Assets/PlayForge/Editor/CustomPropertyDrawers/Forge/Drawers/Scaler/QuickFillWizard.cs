using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Quick Fill Wizard window for configuring scaler level values with live preview.
    /// </summary>
    public class QuickFillWizard : EditorWindow
    {
        private SerializedProperty _targetProp;
        private Action _onApply;
        
        // Fill parameters
        private EFillPattern _pattern = EFillPattern.Linear;
        private float _startValue = 1f;
        private float _endValue = 10f;
        private float _exponent = 2f;
        private int _stepCount = 5;
        private float _baseValue = 1f;
        private float _incrementPerLevel = 1f;
        private float _multiplierPerLevel = 1.1f;
        
        // Preview
        private float[] _previewValues;
        private int _levelCount;
        
        public enum EFillPattern
        {
            Constant,
            Linear,
            Exponential,
            Logarithmic,
            Steps,
            Additive,
            Multiplicative
        }
        
        public static void Show(SerializedProperty prop, Action onApply)
        {
            var window = GetWindow<QuickFillWizard>(true, "Quick Fill Wizard");
            window._targetProp = prop;
            window._onApply = onApply;
            window.minSize = new Vector2(380, 480);
            window.maxSize = new Vector2(450, 600);
            window.LoadCurrentValues();
            window.Show();
        }
        
        private void LoadCurrentValues()
        {
            if (_targetProp == null) return;
            
            var lvp = _targetProp.FindPropertyRelative("LevelValues");
            if (lvp != null && lvp.arraySize > 0)
            {
                _levelCount = lvp.arraySize;
                _startValue = lvp.GetArrayElementAtIndex(0).floatValue;
                _endValue = lvp.GetArrayElementAtIndex(lvp.arraySize - 1).floatValue;
            }
            else
            {
                _levelCount = 10;
            }
            
            UpdatePreview();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            
            // Header
            var header = new Label("🧙 Quick Fill Wizard");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.AccentPurple;
            header.style.marginBottom = 12;
            root.Add(header);
            
            // Pattern selection
            var patternRow = CreateRow("Fill Pattern");
            var patternField = new EnumField(_pattern);
            patternField.style.flexGrow = 1;
            patternField.RegisterValueChangedCallback(evt =>
            {
                _pattern = (EFillPattern)evt.newValue;
                RebuildUI();
                UpdatePreview();
            });
            patternRow.Add(patternField);
            root.Add(patternRow);
            
            // Description
            var descLabel = new Label(GetPatternDescription(_pattern));
            descLabel.style.fontSize = 10;
            descLabel.style.color = Colors.HintText;
            descLabel.style.marginBottom = 8;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.name = "PatternDescription";
            root.Add(descLabel);
            
            // Parameters container
            var paramsContainer = new VisualElement { name = "ParamsContainer" };
            paramsContainer.style.marginBottom = 12;
            root.Add(paramsContainer);
            BuildParameterFields(paramsContainer);
            
            // Preview section
            var previewSection = new VisualElement { name = "PreviewSection" };
            previewSection.style.marginTop = 8;
            root.Add(previewSection);
            BuildPreviewSection(previewSection);
            
            // Buttons
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 12;
            
            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);
            
            var applyBtn = new Button(ApplyFill) { text = "Apply" };
            applyBtn.style.backgroundColor = Colors.AccentGreen;
            applyBtn.style.color = Color.white;
            btnRow.Add(applyBtn);
            
            root.Add(btnRow);
        }
        
        private void RebuildUI()
        {
            var root = rootVisualElement;
            
            // Update description
            var descLabel = root.Q<Label>("PatternDescription");
            if (descLabel != null)
            {
                descLabel.text = GetPatternDescription(_pattern);
            }
            
            // Rebuild parameters
            var paramsContainer = root.Q<VisualElement>("ParamsContainer");
            if (paramsContainer != null)
            {
                paramsContainer.Clear();
                BuildParameterFields(paramsContainer);
            }
            
            // Rebuild preview
            var previewSection = root.Q<VisualElement>("PreviewSection");
            if (previewSection != null)
            {
                previewSection.Clear();
                BuildPreviewSection(previewSection);
            }
        }
        
        private void BuildParameterFields(VisualElement container)
        {
            switch (_pattern)
            {
                case EFillPattern.Constant:
                    container.Add(CreateFloatRow("Value", _startValue, v => { _startValue = v; UpdatePreview(); }));
                    break;
                    
                case EFillPattern.Linear:
                case EFillPattern.Exponential:
                case EFillPattern.Logarithmic:
                    container.Add(CreateFloatRow("Start Value", _startValue, v => { _startValue = v; UpdatePreview(); }));
                    container.Add(CreateFloatRow("End Value", _endValue, v => { _endValue = v; UpdatePreview(); }));
                    if (_pattern == EFillPattern.Exponential)
                    {
                        container.Add(CreateFloatRow("Exponent", _exponent, v => { _exponent = v; UpdatePreview(); }, 0.1f, 5f));
                    }
                    break;
                    
                case EFillPattern.Steps:
                    container.Add(CreateFloatRow("Start Value", _startValue, v => { _startValue = v; UpdatePreview(); }));
                    container.Add(CreateFloatRow("End Value", _endValue, v => { _endValue = v; UpdatePreview(); }));
                    container.Add(CreateIntRow("Step Count", _stepCount, v => { _stepCount = v; UpdatePreview(); }, 1, 20));
                    break;
                    
                case EFillPattern.Additive:
                    container.Add(CreateFloatRow("Base Value", _baseValue, v => { _baseValue = v; UpdatePreview(); }));
                    container.Add(CreateFloatRow("Per Level", _incrementPerLevel, v => { _incrementPerLevel = v; UpdatePreview(); }));
                    break;
                    
                case EFillPattern.Multiplicative:
                    container.Add(CreateFloatRow("Base Value", _baseValue, v => { _baseValue = v; UpdatePreview(); }));
                    container.Add(CreateFloatRow("Multiplier", _multiplierPerLevel, v => { _multiplierPerLevel = v; UpdatePreview(); }, 0.5f, 3f));
                    break;
            }
        }
        
        private void BuildPreviewSection(VisualElement container)
        {
            // Preview header
            var previewHeader = new Label($"Preview ({_levelCount} levels)");
            previewHeader.style.fontSize = 11;
            previewHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewHeader.style.color = Colors.LabelText;
            previewHeader.style.marginBottom = 6;
            container.Add(previewHeader);
            
            if (_previewValues == null || _previewValues.Length == 0)
            {
                container.Add(new Label("No preview available") { style = { color = Colors.HintText } });
                return;
            }
            
            // Visual curve preview
            var curveBox = new VisualElement();
            curveBox.style.height = 80;
            curveBox.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            curveBox.style.borderTopLeftRadius = 4;
            curveBox.style.borderTopRightRadius = 4;
            curveBox.style.borderBottomLeftRadius = 4;
            curveBox.style.borderBottomRightRadius = 4;
            curveBox.style.marginBottom = 8;
            
            // Create curve for preview
            var curve = new AnimationCurve();
            float minVal = float.MaxValue, maxVal = float.MinValue;
            for (int i = 0; i < _previewValues.Length; i++)
            {
                float t = _previewValues.Length > 1 ? (float)i / (_previewValues.Length - 1) : 0f;
                curve.AddKey(t, _previewValues[i]);
                minVal = Mathf.Min(minVal, _previewValues[i]);
                maxVal = Mathf.Max(maxVal, _previewValues[i]);
            }
            
            var curveField = new UnityEditor.UIElements.CurveField { value = curve };
            curveField.style.flexGrow = 1;
            curveField.style.height = 80;
            curveField.SetEnabled(false);
            curveBox.Add(curveField);
            container.Add(curveBox);
            
            // Range info
            var rangeRow = new VisualElement();
            rangeRow.style.flexDirection = FlexDirection.Row;
            rangeRow.style.justifyContent = Justify.SpaceBetween;
            rangeRow.style.marginBottom = 8;
            
            var minLabel = new Label($"Min: {minVal:F2}");
            minLabel.style.fontSize = 10;
            minLabel.style.color = Colors.HintText;
            rangeRow.Add(minLabel);
            
            var maxLabel = new Label($"Max: {maxVal:F2}");
            maxLabel.style.fontSize = 10;
            maxLabel.style.color = Colors.HintText;
            rangeRow.Add(maxLabel);
            
            container.Add(rangeRow);
            
            // Value table (scrollable)
            var tableLabel = new Label("Level Values:");
            tableLabel.style.fontSize = 10;
            tableLabel.style.color = Colors.HintText;
            tableLabel.style.marginBottom = 4;
            container.Add(tableLabel);
            
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.maxHeight = 120;
            scrollView.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            scrollView.style.borderTopLeftRadius = 3;
            scrollView.style.borderTopRightRadius = 3;
            scrollView.style.borderBottomLeftRadius = 3;
            scrollView.style.borderBottomRightRadius = 3;
            scrollView.style.paddingLeft = 4;
            scrollView.style.paddingRight = 4;
            scrollView.style.paddingTop = 4;
            scrollView.style.paddingBottom = 4;
            
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            
            for (int i = 0; i < _previewValues.Length; i++)
            {
                var cell = new VisualElement();
                cell.style.width = new Length(25f, LengthUnit.Percent);
                cell.style.flexDirection = FlexDirection.Row;
                cell.style.paddingRight = 4;
                cell.style.paddingBottom = 2;
                
                var lvLabel = new Label($"{i + 1}:");
                lvLabel.style.width = 22;
                lvLabel.style.fontSize = 9;
                lvLabel.style.color = Colors.HintText;
                lvLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                cell.Add(lvLabel);
                
                var valLabel = new Label($"{_previewValues[i]:F2}");
                valLabel.style.fontSize = 9;
                valLabel.style.color = Colors.AccentBlue;
                valLabel.style.marginLeft = 2;
                cell.Add(valLabel);
                
                grid.Add(cell);
            }
            
            scrollView.Add(grid);
            container.Add(scrollView);
        }
        
        private void UpdatePreview()
        {
            _previewValues = new float[_levelCount];
            
            for (int i = 0; i < _levelCount; i++)
            {
                float t = _levelCount > 1 ? (float)i / (_levelCount - 1) : 0f;
                
                _previewValues[i] = _pattern switch
                {
                    EFillPattern.Constant => _startValue,
                    EFillPattern.Linear => Mathf.Lerp(_startValue, _endValue, t),
                    EFillPattern.Exponential => Mathf.Lerp(_startValue, _endValue, Mathf.Pow(t, _exponent)),
                    EFillPattern.Logarithmic => Mathf.Lerp(_startValue, _endValue, Mathf.Sqrt(t)),
                    EFillPattern.Steps => CalculateStepValue(t),
                    EFillPattern.Additive => _baseValue + (_incrementPerLevel * i),
                    EFillPattern.Multiplicative => _baseValue * Mathf.Pow(_multiplierPerLevel, i),
                    _ => 1f
                };
            }
            
            // Rebuild preview section
            var previewSection = rootVisualElement?.Q<VisualElement>("PreviewSection");
            if (previewSection != null)
            {
                previewSection.Clear();
                BuildPreviewSection(previewSection);
            }
        }
        
        private float CalculateStepValue(float t)
        {
            int step = Mathf.FloorToInt(t * _stepCount);
            float stepT = (float)step / _stepCount;
            return Mathf.Lerp(_startValue, _endValue, stepT);
        }
        
        private void ApplyFill()
        {
            if (_targetProp == null || _previewValues == null) return;
            
            var lvp = _targetProp.FindPropertyRelative("LevelValues");
            if (lvp == null) return;
            
            // Ensure array is correct size
            if (lvp.arraySize != _previewValues.Length)
            {
                lvp.arraySize = _previewValues.Length;
            }
            
            // Apply values
            for (int i = 0; i < _previewValues.Length; i++)
            {
                lvp.GetArrayElementAtIndex(i).floatValue = _previewValues[i];
            }
            
            // Regenerate curve
            RegenerateCurve();
            
            _targetProp.serializedObject.ApplyModifiedProperties();
            _onApply?.Invoke();
            
            Close();
        }
        
        private void RegenerateCurve()
        {
            var lvp = _targetProp.FindPropertyRelative("LevelValues");
            var sp = _targetProp.FindPropertyRelative("Scaling");
            var ip = _targetProp.FindPropertyRelative("Interpolation");
            
            if (lvp == null || lvp.arraySize == 0) return;
            
            var interpolation = ip != null ? (EScalerInterpolation)ip.enumValueIndex : EScalerInterpolation.Linear;
            
            var curve = new AnimationCurve();
            int n = lvp.arraySize;
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                curve.AddKey(new Keyframe(t, lvp.GetArrayElementAtIndex(i).floatValue));
            }
            
            var tm = interpolation switch
            {
                EScalerInterpolation.Constant => UnityEditor.AnimationUtility.TangentMode.Constant,
                EScalerInterpolation.Linear => UnityEditor.AnimationUtility.TangentMode.Linear,
                _ => UnityEditor.AnimationUtility.TangentMode.ClampedAuto
            };
            
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tm);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tm);
            }
            
            if (sp != null)
                sp.animationCurveValue = curve;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateRow(string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var labelElement = new Label(label);
            labelElement.style.width = 90;
            labelElement.style.color = Colors.LabelText;
            row.Add(labelElement);
            
            return row;
        }
        
        private VisualElement CreateFloatRow(string label, float value, Action<float> onChange, float min = float.MinValue, float max = float.MaxValue)
        {
            var row = CreateRow(label);
            
            var field = new FloatField { value = value };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                float clamped = Mathf.Clamp(evt.newValue, min, max);
                if (!Mathf.Approximately(clamped, evt.newValue))
                {
                    field.SetValueWithoutNotify(clamped);
                }
                onChange?.Invoke(clamped);
            });
            row.Add(field);
            
            if (min != float.MinValue && max != float.MaxValue)
            {
                var slider = new Slider(min, max) { value = value };
                slider.style.flexGrow = 1;
                slider.style.marginLeft = 8;
                slider.RegisterValueChangedCallback(evt =>
                {
                    field.SetValueWithoutNotify(evt.newValue);
                    onChange?.Invoke(evt.newValue);
                });
                row.Add(slider);
            }
            
            return row;
        }
        
        private VisualElement CreateIntRow(string label, int value, Action<int> onChange, int min = 1, int max = 100)
        {
            var row = CreateRow(label);
            
            var field = new IntegerField { value = value };
            field.style.width = 50;
            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, min, max);
                if (clamped != evt.newValue)
                {
                    field.SetValueWithoutNotify(clamped);
                }
                onChange?.Invoke(clamped);
            });
            row.Add(field);
            
            var slider = new SliderInt(min, max) { value = value };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                onChange?.Invoke(evt.newValue);
            });
            row.Add(slider);
            
            return row;
        }
        
        private string GetPatternDescription(EFillPattern pattern)
        {
            return pattern switch
            {
                EFillPattern.Constant => "All levels have the same value.",
                EFillPattern.Linear => "Values increase linearly from start to end.",
                EFillPattern.Exponential => "Values follow an exponential curve (slow start, fast end). Higher exponent = more curve.",
                EFillPattern.Logarithmic => "Values follow a logarithmic curve (fast start, slow end). Good for diminishing returns.",
                EFillPattern.Steps => "Values jump in discrete steps rather than smooth progression.",
                EFillPattern.Additive => "Each level adds a fixed amount to the base value.",
                EFillPattern.Multiplicative => "Each level multiplies the previous value. Good for compound growth.",
                _ => ""
            };
        }
    }
}
