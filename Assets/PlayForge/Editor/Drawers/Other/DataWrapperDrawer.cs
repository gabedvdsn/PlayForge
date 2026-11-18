using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Local Data row: its key, its value's kind and its value, each a column of its own, laid out by the style sheet.
    /// A value's kind is a class, Float to ScriptableObject or one of your own: a kind that holds one Value draws it in the
    /// row, and one of your own with more fields draws each under its name. Each row holds a value of its own, so a row
    /// added as a copy of the last edits its own.
    /// </summary>
    [CustomPropertyDrawer(typeof(DataWrapper))]
    public class DataWrapperDrawer : PropertyDrawer
    {
        // PlayForge's kinds, in the order an author reaches for them; a project's follow, by name
        private static readonly string[] Order =
        {
            "Float", "Int", "String", "Bool", "Tag", "Attribute", "Effect", "Ability", "Entity", "Object", "MonoBehaviour", "ScriptableObject",
        };

        private const string None = "None";

        /// <summary>Every kind a value can be, PlayForge's first, in their order, then a project's, by name.</summary>
        internal static List<Type> Kinds() => TypeCache.GetTypesDerivedFrom<LocalDataValue>()
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => Array.IndexOf(Order, KindName(type)) is var at && at >= 0 ? at : int.MaxValue)
            .ThenBy(KindName)
            .ToList();

        /// <summary>A kind's name in the row: its menu name, or its class's, without "Local".</summary>
        internal static string KindName(Type type)
        {
            var name = ForgeMenuAttribute.NameOf(type);
            if (!string.IsNullOrEmpty(name)) return name;
            name = type.Name;
            return name.StartsWith("Local") && name.Length > "Local".Length ? name.Substring("Local".Length) : name;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "DataWrapper" };
            // The style sheet lays out the row, wherever it's drawn
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-data-wrapper");

            var keyProp = property.FindPropertyRelative(nameof(DataWrapper.Key));
            var valueProp = property.FindPropertyRelative(nameof(DataWrapper.Value));

            // Unity's + copies the last row, and the new row held the last one's value, so an edit to one changed both
            if (ScalerDrawer.FindSharedSibling(valueProp) >= 0)
            {
                ForgeLists.CopyOwn(valueProp);
                property.serializedObject.ApplyModifiedProperties();
            }

            // The row's first column, with no label: under one, as wide as an inspector's label column, the tag's box had
            // no width left to press
            var keyField = new PropertyField(keyProp, "") { name = "Key" };
            keyField.AddToClassList("pf-data-wrapper__key");
            keyField.BindProperty(keyProp);
            root.Add(keyField);

            // Its value's kind, a compact choice
            var kinds = Kinds();
            var names = new List<string> { None };
            names.AddRange(kinds.Select(KindName));
            var typeField = new PopupField<string>(names, 0) { name = "Type" };
            typeField.AddToClassList("pf-data-wrapper__type");
            root.Add(typeField);

            // Its value, which its kind holds
            var valueContainer = new VisualElement { name = "Value" };
            valueContainer.AddToClassList("pf-data-wrapper__value");
            root.Add(valueContainer);

            // The kind and the value as drawn, so a new one, as an Undo puts back, draws them again: fields bound to a value
            // that's gone read a property that has disappeared
            Type shownType = null;
            long shownId = 0;
            bool shownMixed = false;

            void Draw()
            {
                if (!ScalerFields.Alive(valueProp)) return;
                valueProp.serializedObject.Update();
                shownType = KindOf(valueProp);
                shownId = valueProp.managedReferenceId;
                shownMixed = EachAsset.Differ(valueProp, KindOf);
                typeField.SetValueWithoutNotify(names[Math.Max(0, kinds.IndexOf(shownType) + 1)]);
                typeField.showMixedValue = shownMixed;
                DrawValue(valueContainer, valueProp);
            }

            typeField.RegisterValueChangedCallback(evt =>
            {
                int index = names.IndexOf(evt.newValue) - 1;
                var chosen = index >= 0 ? kinds[index] : null;
                // Each selected asset its own value of the kind, which a write through one property would share
                EachAsset.Write(valueProp, own =>
                {
                    if (KindOf(own) != chosen) own.managedReferenceValue = chosen == null ? null : Activator.CreateInstance(chosen);
                });
                Draw();
            });

            root.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                if (!ScalerFields.Alive(valueProp)) return;
                if (KindOf(valueProp) != shownType || valueProp.managedReferenceId != shownId
                    || EachAsset.Differ(valueProp, KindOf) != shownMixed) Draw();
            });
            Draw();
            return root;
        }

        // The kind a row's value is, or none
        private static Type KindOf(SerializedProperty valueProp) => valueProp?.managedReferenceValue?.GetType();

        // The value's own field, in the row where its kind holds one Value; else each of its fields under its name
        private static void DrawValue(VisualElement container, SerializedProperty valueProp)
        {
            container.Clear();
            valueProp.serializedObject.Update();
            if (valueProp.managedReferenceValue == null)
            {
                var pick = new Label("Pick a kind");
                pick.AddToClassList("pf-data-wrapper__field");
                container.Add(pick);
                return;
            }

            var fields = new List<SerializedProperty>();
            var end = valueProp.GetEndProperty();
            var child = valueProp.Copy();
            if (child.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    fields.Add(child.Copy());
                } while (child.NextVisible(false));
            }

            bool single = fields.Count == 1 && fields[0].name == nameof(LocalDataValue<int>.Value);
            foreach (var field in fields)
            {
                VisualElement drawn;
                if (single) drawn = OneValue(field, valueProp);
                else
                {
                    var labelled = new PropertyField(field);
                    labelled.BindProperty(field);
                    drawn = labelled;
                }
                drawn.AddToClassList("pf-data-wrapper__field");
                container.Add(drawn);
            }
        }

        // A kind's one value, in the row with no label, as the row's kind says what it is: a string, a bool or an asset in a
        // field of its type, which binds as it's made; a number that can scale, a tag or a kind of your own by its own drawer
        private static VisualElement OneValue(SerializedProperty field, SerializedProperty valueProp)
        {
            switch (field.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    var text = new TextField();
                    text.BindProperty(field);
                    return text;
                }
                case SerializedPropertyType.Boolean:
                {
                    var toggle = new Toggle();
                    toggle.BindProperty(field);
                    return toggle;
                }
                case SerializedPropertyType.ObjectReference:
                {
                    // An asset's own field, of the type its kind holds; an asset holds no scene's object
                    var asset = new ObjectField { objectType = HeldType(valueProp) ?? typeof(Object), allowSceneObjects = false };
                    asset.BindProperty(field);
                    return asset;
                }
                default:
                {
                    var drawn = new PropertyField(field, "");
                    drawn.BindProperty(field);
                    return drawn;
                }
            }
        }

        // The type a kind's one value holds: its LocalDataValue's T
        private static Type HeldType(SerializedProperty valueProp)
        {
            for (var type = valueProp.managedReferenceValue?.GetType(); type != null; type = type.BaseType)
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LocalDataValue<>)) return type.GetGenericArguments()[0];
            return null;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
