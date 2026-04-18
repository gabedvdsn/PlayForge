using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(ScalerMagnitudeOperation))]
    public class ScalerMagnitudeOperationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            
            // Get custom keyword from attribute on this field
            var keyword = GetKeyword();
            var labels = GetLabels(keyword);
            
            // Magnitude field
            var magnitudeProp = property.FindPropertyRelative("Magnitude");
            var magnitudeField = new PropertyField(magnitudeProp, labels.Magnitude);
            magnitudeField.style.marginBottom = 2;
            root.Add(magnitudeField);
            
            // Operation field
            var operationProp = property.FindPropertyRelative("RealMagnitude");
            var operationField = new PropertyField(operationProp, labels.Operation);
            operationField.style.marginBottom = 2;
            root.Add(operationField);
            
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