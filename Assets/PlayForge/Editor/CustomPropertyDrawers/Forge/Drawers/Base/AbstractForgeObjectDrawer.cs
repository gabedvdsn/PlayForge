using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A reference to a PlayForge asset, or to a Value Pipeline, Crowd Control Rules or Effect Modifier (FN-11), as
    /// Docs/EDITOR_STYLE.md draws one: Unity's object field, with its type's icon,
    /// and beside it a plain button, Open while it names an asset, which shows the asset in a window of its own, or New
    /// while it's empty, which makes one where The Forge makes its type and uses it. A drawer adds its own actions to the
    /// field's right-click menu. It was a button that opened a dropdown list of its own, with hover colors and Clear and
    /// Open icon buttons set in C# (ED-38).
    /// </summary>
    public abstract class AbstractForgeObjectDrawer<T> : PropertyDrawer where T : ScriptableObject
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
            // Its picker offers the active Forge Project's assets alone, where Unity's offers every asset of the type, while
            // the project has a Forge Project (FEAT-34)
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

        /// <summary>
        /// Shows the picker of the active Forge Project's assets of the field's type, None first, as Unity's searchable
        /// dropdown, under the field (FEAT-34).
        /// </summary>
        internal static void ShowProjectPicker(ObjectField field)
        {
            var assets = new List<(Object Asset, string Name)> { (null, "None") };
            assets.AddRange(ProjectAssets().Select(asset => (asset, NameOf(asset))));
            new ForgeAssetPicker(assets, ForgeAssetTypes.NameOf(typeof(T)), "None", picked => field.value = picked).Show(field.worldBound);
        }

        /// <summary>The active Forge Project's assets of the field's type, by name, as its picker offers them (FEAT-34).</summary>
        internal static List<Object> ProjectAssets() => ForgeProjects.AssetsOf(typeof(T))
            .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>The label the field shows: none in a list, whose rows need none, else the property's.</summary>
        protected string LabelOf(SerializedProperty property) => IsInList(property) ? "" : preferredLabel ?? property.displayName;

        /// <summary>Shows an asset in a window of its own, so the asset that holds the field stays in the Inspector.</summary>
        protected virtual void Open(T asset) => EditorUtility.OpenPropertyEditor(asset);

        /// <summary>
        /// Makes the asset New uses, named for the asset that holds the field and the field, as in "Goblin Attribute
        /// Set". One Undo takes back the field's new value; the asset stays.
        /// </summary>
        protected virtual T New(Object host, string name) =>
            typeof(BaseForgeAsset).IsAssignableFrom(typeof(T))
                ? (T)(ScriptableObject)ForgeAssetCreation.Create(typeof(T), name)
                // An asset with no name of its own, as a Value Pipeline, is named by its file (FN-11)
                : (T)ForgeAssetCreation.CreateNew(typeof(T), name);

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
        // attribute set's element, keeps its label: it dropped it, as the tag field did (ED-49, ED-55)
        private static bool IsInList(SerializedProperty property) => property.propertyPath.EndsWith("]");
    }
}
