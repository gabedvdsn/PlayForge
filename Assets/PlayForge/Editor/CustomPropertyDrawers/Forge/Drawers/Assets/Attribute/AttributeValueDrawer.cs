using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
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
            currentLabel.style.color = ForgeDrawerStyles.Colors.HintText;
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
            sep.style.color = ForgeDrawerStyles.Colors.HintText;
            container.Add(sep);
            
            // Base
            var baseLabel = new Label("Base");
            baseLabel.style.width = 28;
            baseLabel.style.color = ForgeDrawerStyles.Colors.HintText;
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
}
