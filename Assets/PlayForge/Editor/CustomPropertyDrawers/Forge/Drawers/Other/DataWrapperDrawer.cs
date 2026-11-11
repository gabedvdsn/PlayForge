using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Local Data row: its key, its type and its value, each a column of its own, laid out by the style sheet. It set its
    /// columns' widths in C#, and drew a separator in a dark gray of its own, in either theme (ED-73).
    /// </summary>
    [CustomPropertyDrawer(typeof(DataWrapper))]
    public class DataWrapperDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "DataWrapper" };
            // The style sheet lays out the row, wherever it's drawn (ED-20, ED-73)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-data-wrapper");

            // Get sub-properties
            var keyProp = property.FindPropertyRelative("Key");
            var typeProp = property.FindPropertyRelative("Type");
            
            var stringProp = property.FindPropertyRelative("stringValue");
            var boolProp = property.FindPropertyRelative("boolValue");
            var intProp = property.FindPropertyRelative("intValue");
            var floatProp = property.FindPropertyRelative("floatValue");
            var objectProp = property.FindPropertyRelative("objectValue");
            var monoProp = property.FindPropertyRelative("monoBehaviourValue");
            var soProp = property.FindPropertyRelative("scriptableObjectValue");
            
            var tagProp = property.FindPropertyRelative("tagValue");
            var attributeProp = property.FindPropertyRelative("attributeValue");
            var abilityProp = property.FindPropertyRelative("abilityValue");
            var effectProp = property.FindPropertyRelative("effectValue");
            var entityProp = property.FindPropertyRelative("entityValue");

            // The row's first column, with no label: under one, as wide as an inspector's label column, the tag's box had
            // no width left to press (ED-68)
            var keyField = new PropertyField(keyProp, "") { name = "Key" };
            keyField.AddToClassList("pf-data-wrapper__key");
            keyField.BindProperty(keyProp);
            root.Add(keyField);

            // Its type, a compact choice
            var typeField = new EnumField((EDataWrapperType)typeProp.enumValueIndex) { name = "Type" };
            typeField.AddToClassList("pf-data-wrapper__type");
            typeField.BindProperty(typeProp);
            root.Add(typeField);

            // Its value, which the type chooses the field of
            var valueContainer = new VisualElement { name = "Value" };
            valueContainer.AddToClassList("pf-data-wrapper__value");
            root.Add(valueContainer);

            // Initial draw
            UpdateValueField(valueContainer, (EDataWrapperType)typeProp.enumValueIndex,
                stringProp, boolProp, intProp, floatProp, objectProp, monoProp, soProp, tagProp, attributeProp, effectProp, abilityProp, entityProp);

            // Update value field when type changes
            typeField.RegisterValueChangedCallback(evt =>
            {
                UpdateValueField(valueContainer, (EDataWrapperType)evt.newValue,
                    stringProp, boolProp, intProp, floatProp, objectProp, monoProp, soProp, tagProp, attributeProp, effectProp, abilityProp, entityProp);
            });

            return root;
        }

        private void UpdateValueField(
            VisualElement container,
            EDataWrapperType type,
            SerializedProperty stringProp,
            SerializedProperty boolProp,
            SerializedProperty intProp,
            SerializedProperty floatProp,
            SerializedProperty objectProp,
            SerializedProperty monoProp,
            SerializedProperty soProp,
            SerializedProperty tagProp,
            SerializedProperty attributeProp,
            SerializedProperty effectProp,
            SerializedProperty abilityProp,
            SerializedProperty entityProp
            )
        {
            container.Clear();

            var valueField = type switch
            {
                EDataWrapperType.String => CreateStringField(stringProp),
                EDataWrapperType.Bool => CreateBoolField(boolProp),
                EDataWrapperType.Int => CreateIntField(intProp),
                EDataWrapperType.Float => CreateFloatField(floatProp),
                EDataWrapperType.Tag => CreatePropertyField(tagProp),
                EDataWrapperType.Attribute => CreatePropertyField(attributeProp),
                EDataWrapperType.Effect => CreatePropertyField(effectProp),
                EDataWrapperType.Ability => CreatePropertyField(abilityProp),
                EDataWrapperType.Entity => CreatePropertyField(entityProp),
                EDataWrapperType.Object => CreateObjectField(objectProp, typeof(Object)),
                // Each edits its own value; they had each other's (ED-30)
                EDataWrapperType.ScriptableObject => CreateObjectField(soProp, typeof(ScriptableObject)),
                EDataWrapperType.MonoBehaviour => CreateObjectField(monoProp, typeof(MonoBehaviour)),
                _ => new Label("Select a type")
            };

            valueField.AddToClassList("pf-data-wrapper__field");
            container.Add(valueField);
        }

        private VisualElement CreateStringField(SerializedProperty prop)
        {
            var field = new TextField { bindingPath = prop.propertyPath };
            field.BindProperty(prop);
            return field;
        }
        
        private VisualElement CreateBoolField(SerializedProperty prop)
        {
            var field = new Toggle { bindingPath = prop.propertyPath };
            field.BindProperty(prop);
            return field;
        }

        private VisualElement CreateIntField(SerializedProperty prop)
        {
            var field = new IntegerField { bindingPath = prop.propertyPath };
            field.BindProperty(prop);
            return field;
        }

        private VisualElement CreateFloatField(SerializedProperty prop)
        {
            var field = new FloatField { bindingPath = prop.propertyPath };
            field.BindProperty(prop);
            return field;
        }

        private VisualElement CreateObjectField(SerializedProperty prop, Type objectType)
        {
            var field = new ObjectField
            {
                objectType = objectType,
                allowSceneObjects = false // ScriptableObjects typically shouldn't reference scene objects
            };
            field.BindProperty(prop);
            return field;
        }

        // A value drawn by its own drawer, with no label: the row's type says what it is (ED-68)
        private VisualElement CreatePropertyField(SerializedProperty prop)
        {
            var field = new PropertyField(prop, "");
            field.BindProperty(prop);

            return field;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
