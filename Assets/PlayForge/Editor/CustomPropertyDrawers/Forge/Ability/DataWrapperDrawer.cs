using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(DataWrapper))]
    public class DataWrapperDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            // Get sub-properties
            var keyProp = property.FindPropertyRelative("Key");
            var typeProp = property.FindPropertyRelative("Type");
            
            var stringProp = property.FindPropertyRelative("stringValue");
            var intProp = property.FindPropertyRelative("intValue");
            var floatProp = property.FindPropertyRelative("floatValue");
            var objectProp = property.FindPropertyRelative("objectValue");
            
            var tagProp = property.FindPropertyRelative("tagValue");
            var attributeProp = property.FindPropertyRelative("attributeValue");
            var abilityProp = property.FindPropertyRelative("abilityValue");
            var effectProp = property.FindPropertyRelative("effectValue");
            var entityProp = property.FindPropertyRelative("entityValue");

            var keyField = new PropertyField()
            {
                style =
                {
                    minWidth = 150, maxWidth = 170, marginRight = 6
                }
            };

            var sep = new VisualElement()
            {
                style =
                {
                    maxWidth = 3,
                    borderLeftWidth = 2, borderLeftColor = new Color(.15f, .15f, .15f, 1f),
                    marginRight = 6
                }
            };

            keyField.BindProperty(keyProp);
            root.Add(keyField);
            root.Add(sep);
            
            // Type dropdown (compact)
            var typeField = new EnumField((EDataWrapperType)typeProp.enumValueIndex)
            {
                style =
                {
                    minWidth = 70,
                    maxWidth = 80,
                    marginRight = 8,
                    flexGrow = 0, alignSelf = Align.FlexStart
                }
            };
            
            typeField.BindProperty(typeProp);
            root.Add(typeField);

            // Container for the value field
            var valueContainer = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Row }
            };
            root.Add(valueContainer);

            // Initial draw
            UpdateValueField(valueContainer, (EDataWrapperType)typeProp.enumValueIndex,
                stringProp, intProp, floatProp, objectProp, tagProp, attributeProp, effectProp, abilityProp, entityProp);

            // Update value field when type changes
            typeField.RegisterValueChangedCallback(evt =>
            {
                UpdateValueField(valueContainer, (EDataWrapperType)evt.newValue,
                    stringProp, intProp, floatProp, objectProp, tagProp, attributeProp, effectProp, abilityProp, entityProp);
            });

            return root;
        }

        private void UpdateValueField(
            VisualElement container,
            EDataWrapperType type,
            SerializedProperty stringProp,
            SerializedProperty intProp,
            SerializedProperty floatProp,
            SerializedProperty objectProp,
            SerializedProperty tagProp,
            SerializedProperty attributeProp,
            SerializedProperty effectProp,
            SerializedProperty abilityProp,
            SerializedProperty entityProp
            )
        {
            container.Clear();

            VisualElement valueField = type switch
            {
                EDataWrapperType.String => CreateStringField(stringProp),
                EDataWrapperType.Int => CreateIntField(intProp),
                EDataWrapperType.Float => CreateFloatField(floatProp),
                EDataWrapperType.Tag => CreatePropertyField(tagProp),
                EDataWrapperType.Attribute => CreatePropertyField(attributeProp),
                EDataWrapperType.Effect => CreatePropertyField(effectProp),
                EDataWrapperType.Ability => CreatePropertyField(abilityProp),
                EDataWrapperType.Entity => CreatePropertyField(entityProp),
                EDataWrapperType.Object => CreateObjectField(objectProp, typeof(Object)),
                _ => new Label("Select a type")
            };

            valueField.style.flexGrow = 1;
            container.Add(valueField);
        }

        private VisualElement CreateStringField(SerializedProperty prop)
        {
            var field = new TextField { bindingPath = prop.propertyPath };
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

        private VisualElement CreatePropertyField(SerializedProperty prop)
        {
            var field = new PropertyField();
            field.BindProperty(prop);

            return field;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
