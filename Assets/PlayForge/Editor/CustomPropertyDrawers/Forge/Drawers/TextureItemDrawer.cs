using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(TextureItem))]
    public class TextureItemDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    alignItems = Align.Center,
                    minHeight = 20
                }
            };

            // Get sub-properties
            var tagProp = property.FindPropertyRelative("Tag");
            var textureProp = property.FindPropertyRelative("Texture");

            // Tag field (left side)
            var tagField = new PropertyField();
            tagField.label = "";
            tagField.style.minWidth = 150;
            tagField.style.maxWidth = 200;
            tagField.style.flexGrow = 0;
            tagField.style.marginRight = 6;
            tagField.BindProperty(tagProp);
            root.Add(tagField);

            // Separator
            var separator = new VisualElement
            {
                style =
                {
                    width = 2,
                    alignSelf = Align.Stretch,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                    marginRight = 8,
                    marginTop = 2,
                    marginBottom = 2
                }
            };
            root.Add(separator);

            // Texture field (right side)
            var textureField = new ObjectField
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                style =
                {
                    flexGrow = 1,
                    minWidth = 100
                }
            };
            textureField.BindProperty(textureProp);
            root.Add(textureField);

            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}