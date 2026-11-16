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
    /// A reference to a PlayForge asset, or to a Value Pipeline, Crowd Control Rules or Effect Modifier:
    /// Unity's object field, with its type's icon,
    /// and beside it a plain button, Open while it names an asset, which shows the asset in a window of its own, or New
    /// while it's empty, which asks for a new one's name in The Forge's New dialog, makes it where The Forge makes its type
    /// and uses it. A drawer adds its own actions to the
    /// field's right-click menu. It was a button that opened a dropdown list of its own, with hover colors and Clear and
    /// Open icon buttons set in C#.
    /// </summary>
    public abstract class AbstractForgeObjectDrawer<T> : PropertyDrawer where T : ScriptableObject
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { name = "ReferenceRow" };
            // The style sheet lays out the row, wherever the drawer is drawn: in Unity's own inspector too
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
            // Its picker offers the active Forge Project's assets alone, where Unity's offers every asset of the type, while
            // the project has a Forge Project
            void PickInProject(EventBase down, int button)
            {
                if (button != 0 || ForgeProjects.All().Count == 0) return;
                var selector = field.Q(className: ObjectField.selectorUssClassName);
                if (selector == null || down.target is not VisualElement target || (target != selector && !selector.Contains(target))) return;
                down.StopImmediatePropagation();
                ShowProjectPicker(field);
            }
            field.RegisterCallback<PointerDownEvent>(down => PickInProject(down, down.button), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseDownEvent>(down => PickInProject(down, down.button), TrickleDown.TrickleDown);

            var action = new Button { name = "ReferenceAction" };
            action.AddToClassList("pf-reference__action");
            row.Add(action);

            // What the field acts for, kept by name: a list's drawers are drawn again as the list changes
            var host = property.serializedObject.targetObject;
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            var label = IsInList(property) ? ForgeAssetTypes.NameOf(typeof(T)) : property.displayName;
            void Show(Object value)
            {
                var asset = value as T;
                action.text = asset != null ? "Open" : "New";
                action.tooltip = asset != null
                    ? $"Show {NameOf(asset)} in a window of its own"
                    : $"Name a new {ForgeAssetTypes.NameOf(typeof(T))}, make it where The Forge makes them, and use it";
                action.clickable = new Clickable(() =>
                {
                    if (field.value is T shown) Open(shown);
                    // The field takes it once it's made, by its property found again: the dialog waits on its name
                    else New(host, $"{NameOf(host)} {label}", made =>
                    {
                        if (serialized.targetObject == null) return;
                        serialized.Update();
                        var now = serialized.FindProperty(path);
                        if (now == null) return;
                        now.objectReferenceValue = made;
                        serialized.ApplyModifiedProperties();
                    });
                });
            }
            Show(property.objectReferenceValue);
            field.RegisterValueChangedCallback(change => Show(change.newValue));
            field.AddManipulator(new ContextualMenuManipulator(menu => AddMenuItems(menu.menu, field, host)));
            return row;
        }

        /// <summary>
        /// Shows the picker of the active Forge Project's assets of the field's type, None first and the one it holds checked,
        /// in PlayForge's dropdown, under the field.
        /// </summary>
        internal static void ShowProjectPicker(ObjectField field)
        {
            var assets = new List<(Object Asset, string Name)> { (null, "None") };
            assets.AddRange(ProjectAssets().Select(asset => (asset, NameOf(asset))));
            new ForgeAssetPicker(assets, ForgeAssetTypes.NameOf(typeof(T)), "None", picked => field.value = picked, field.value).Show(field.worldBound);
        }

        /// <summary>The active Forge Project's assets of the field's type, by name, as its picker offers them.</summary>
        internal static List<Object> ProjectAssets() => ForgeProjects.AssetsOf(typeof(T))
            .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>The label the field shows: none in a list, whose rows need none, else the property's.</summary>
        protected string LabelOf(SerializedProperty property) => IsInList(property) ? "" : preferredLabel ?? property.displayName;

        /// <summary>Shows an asset in a window of its own, so the asset that holds the field stays in the Inspector.</summary>
        protected virtual void Open(T asset) => EditorUtility.OpenPropertyEditor(asset);

        /// <summary>
        /// Asks for the name of the asset New uses, starting as one for the asset that holds the field and the field, as in
        /// "Goblin Attribute Set", in The Forge's New dialog, where its folder can change too; then makes it, as The Forge
        /// does, and hands it to <paramref name="made"/>. It made the asset at once, by that name. One Undo takes
        /// back the field's new value; the asset stays.
        /// </summary>
        protected virtual void New(Object host, string name, Action<T> made) =>
            ForgeNewAssetWindow.Show(typeof(T), created =>
            {
                if (created is not T asset) return;
                Made(asset, host);
                made?.Invoke(asset);
            }, name: name);

        /// <summary>Called as New's asset is made, before the field takes it, as an effect is linked. Nothing by default.</summary>
        protected virtual void Made(T asset, Object host) { }

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

        // Whether the field is a list's row itself. A field inside a list's element, as a scaler's attribute inside an
        // attribute set's element, keeps its label: it dropped it, as the tag field did
        private static bool IsInList(SerializedProperty property) => property.propertyPath.EndsWith("]");
    }
}
