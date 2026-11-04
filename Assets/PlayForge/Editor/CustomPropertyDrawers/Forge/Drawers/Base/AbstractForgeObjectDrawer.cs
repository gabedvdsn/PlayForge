using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A reference to a PlayForge asset, as Docs/EDITOR_STYLE.md draws one: Unity's object field, with its type's icon,
    /// and beside it a plain button, Open while it names an asset, which shows the asset in a window of its own, or New
    /// while it's empty, which makes one where The Forge makes its type and uses it. A drawer adds its own actions to the
    /// field's right-click menu. It was a button that opened a dropdown list of its own, with hover colors and Clear and
    /// Open icon buttons set in C# (ED-38).
    /// </summary>
    public abstract class AbstractForgeObjectDrawer<T> : PropertyDrawer where T : BaseForgeAsset
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { name = "ReferenceRow" };
            // The style sheet lays out the row, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(row);
            row.AddToClassList("pf-reference");

            var field = new ObjectField(LabelOf(property))
            {
                name = "Reference",
                objectType = typeof(T),
                allowSceneObjects = false,
                tooltip = property.tooltip,
            };
            field.AddToClassList(ObjectField.alignedFieldUssClassName);
            field.BindProperty(property);
            row.Add(field);

            var action = new Button { name = "ReferenceAction" };
            action.AddToClassList("pf-reference__action");
            row.Add(action);

            // What the field acts for, kept by name: a list's drawers are drawn again as the list changes
            var host = property.serializedObject.targetObject;
            var label = IsInList(property) ? ForgeAssetTypes.NameOf(typeof(T)) : property.displayName;
            void Show(Object value)
            {
                var asset = value as T;
                action.text = asset != null ? "Open" : "New";
                action.tooltip = asset != null
                    ? $"Show {NameOf(asset)} in a window of its own"
                    : $"Make a new {ForgeAssetTypes.NameOf(typeof(T))} where The Forge makes them, and use it";
                action.clickable = new Clickable(() =>
                {
                    if (field.value is T shown) Open(shown);
                    else if (New(host, $"{NameOf(host)} {label}") is T made) field.value = made;
                });
            }
            Show(property.objectReferenceValue);
            field.RegisterValueChangedCallback(change => Show(change.newValue));
            field.AddManipulator(new ContextualMenuManipulator(menu => AddMenuItems(menu.menu, field, host)));
            return row;
        }

        /// <summary>The label the field shows: none in a list, whose rows need none, else the property's.</summary>
        protected string LabelOf(SerializedProperty property) => IsInList(property) ? "" : preferredLabel ?? property.displayName;

        /// <summary>Shows an asset in a window of its own, so the asset that holds the field stays in the Inspector.</summary>
        protected virtual void Open(T asset) => EditorUtility.OpenPropertyEditor(asset);

        /// <summary>
        /// Makes the asset New uses, named for the asset that holds the field and the field, as in "Goblin Attribute
        /// Set". One Undo takes back the field's new value; the asset stays.
        /// </summary>
        protected virtual T New(Object host, string name) => ForgeAssetCreation.Create<T>(name);

        /// <summary>Adds a drawer's own actions to the field's right-click menu. None by default.</summary>
        protected virtual void AddMenuItems(DropdownMenu menu, ObjectField field, Object host) { }

        /// <summary>The attributes on the field the drawer draws, such as an effect field's template.</summary>
        protected IEnumerable<TAttr> GetFieldAttributes<TAttr>() where TAttr : System.Attribute =>
            fieldInfo?.GetCustomAttributes(typeof(TAttr), true).Cast<TAttr>() ?? Enumerable.Empty<TAttr>();

        /// <summary>An asset's PlayForge name, or its file name when it has none.</summary>
        protected static string NameOf(Object asset) => asset switch
        {
            null => "None",
            BaseForgeAsset forge when !string.IsNullOrEmpty(forge.GetName()) => forge.GetName(),
            _ => asset.name,
        };

        private static bool IsInList(SerializedProperty property) => property.propertyPath.Contains(".Array.data[");
    }
}
