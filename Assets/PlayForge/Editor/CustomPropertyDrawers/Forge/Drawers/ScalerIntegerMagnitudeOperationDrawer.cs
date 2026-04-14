using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(ScalerIntegerMagnitudeOperation))]
    public class ScalerIntegerMagnitudeOperationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            
            // Get custom keyword from attribute on this field
            var keyword = GetKeyword();
            var labels = GetLabels(keyword);
            
            // Magnitude field (int)
            var magnitudeProp = property.FindPropertyRelative("Magnitude");
            var magnitudeField = new PropertyField(magnitudeProp, labels.Magnitude);
            magnitudeField.style.marginBottom = 2;
            root.Add(magnitudeField);
            
            // Operation + Rounding row
            var operationRow = new VisualElement();
            operationRow.style.flexDirection = FlexDirection.Row;
            operationRow.style.marginBottom = 2;
            root.Add(operationRow);
            
            // Operation field (takes most of the space)
            var operationProp = property.FindPropertyRelative("RealMagnitude");
            var operationField = new PropertyField(operationProp, labels.Operation);
            operationField.style.flexGrow = 1;
            operationField.style.flexShrink = 1;
            operationRow.Add(operationField);
            
            // Rounding field (compact)
            var roundingProp = property.FindPropertyRelative("Rounding");
            var roundingField = new PropertyField(roundingProp, "");
            roundingField.style.width = 80;
            roundingField.style.marginLeft = 4;
            operationRow.Add(roundingField);
            
            // Scaler field
            // ScalerDrawer uses [DeriveScalerName] + reflection to find this field's [ScalerOperationKeyword]
            var scalerProp = property.FindPropertyRelative("Scaler");
            var scalerField = new PropertyField(scalerProp);
            root.Add(scalerField);
            
            return root;
        }
        
        private string GetKeyword()
        {
            var attr = fieldInfo?.GetCustomAttribute<ScalerOperationKeyword>();
            return attr?.Keyword;
        }
        
        private (string Magnitude, string Operation) GetLabels(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return ("Magnitude", "Real Magnitude");
            
            return ($"{keyword}", $"Real {keyword}");
        }
    }
}