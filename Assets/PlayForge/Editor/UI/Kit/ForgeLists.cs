using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Unity's reorderable list, as a PropertyField draws it, made to keep two of PlayForge's rules (ED-64, ED-57). The
    /// field builds its list as it binds, and builds it again as an Undo changes the list, so each list is hooked as the
    /// field is laid out, or as it's pressed if that came first, before the list acts. Nothing polls (ED-11).
    /// </summary>
    internal static class ForgeLists
    {
        private const string AddsEmptyHook = "pf-list--adds-empty";
        private const string KeepsStatesHook = "pf-list--keeps-states";

        /// <summary>
        /// A list of objects its field holds by reference: its + adds an empty element, for its kind to be picked. Grown
        /// as an array, the list gave the new element the last one's object, so the two were one.
        /// </summary>
        public static void AddsEmpty(PropertyField field, SerializedObject serialized, string path) =>
            Hook(field, AddsEmptyHook, list => list.overridingAddButtonBehavior = (_, _) => AddEmpty(serialized, path));

        /// <summary>
        /// A list whose elements keep their collapse states by place (<see cref="DrawerCollapseState"/>): as its − removes
        /// an element, or one is dragged, the states move with the elements (ED-18). The − removes the selected elements,
        /// or the last, as Unity's does.
        /// </summary>
        public static void KeepsStates(PropertyField field, SerializedObject serialized, string path) =>
            Hook(field, KeepsStatesHook, list =>
            {
                list.onRemove = view => Remove(view, serialized, path);
                list.itemIndexChanged += (from, to) => Move(serialized, path, from, to);
            });

        // Hooks the field's own list once, and each list it builds after
        private static void Hook(PropertyField field, string hook, System.Action<BaseListView> apply)
        {
            void Apply()
            {
                var list = field.Q<ListView>();
                if (list == null || list.ClassListContains(hook) || list.GetFirstAncestorOfType<PropertyField>() != field) return;
                list.AddToClassList(hook);
                apply(list);
            }

            field.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            field.RegisterCallback<PointerDownEvent>(_ => Apply(), TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(_ => Apply(), TrickleDown.TrickleDown);
        }

        /// <summary>Adds an empty element to the end of the list at <paramref name="path"/>, as one Undo step.</summary>
        internal static void AddEmpty(SerializedObject serialized, string path)
        {
            var list = Find(serialized, path);
            if (list == null) return;
            int at = list.arraySize;
            list.arraySize = at + 1;
            var added = list.GetArrayElementAtIndex(at);
            if (added.propertyType == SerializedPropertyType.ManagedReference) added.managedReferenceValue = null;
            serialized.ApplyModifiedProperties();
        }

        // Removes the selected elements, or the last, moving each later element's state up a place
        private static void Remove(BaseListView view, SerializedObject serialized, string path)
        {
            var list = Find(serialized, path);
            if (list == null || list.arraySize == 0) return;
            var removed = view.selectedIndices.Where(index => index >= 0 && index < list.arraySize)
                .Distinct().OrderByDescending(index => index).ToList();
            if (removed.Count == 0) removed.Add(list.arraySize - 1);
            foreach (var index in removed)
            {
                int before = list.arraySize;
                DrawerCollapseState.RemoveElement(list, index, before);
                list.DeleteArrayElementAtIndex(index);
                // An element holding an object is cleared by the first delete, and removed by the second
                if (list.arraySize == before) list.DeleteArrayElementAtIndex(index);
            }
            serialized.ApplyModifiedProperties();
            view.ClearSelection();
        }

        private static void Move(SerializedObject serialized, string path, int from, int to)
        {
            if (from == to) return;
            var list = Find(serialized, path);
            if (list != null) DrawerCollapseState.MoveElement(list, from, to);
        }

        // The list, found again by its path, as an Undo puts objects back (ED-49)
        private static SerializedProperty Find(SerializedObject serialized, string path)
        {
            if (serialized == null || serialized.targetObject == null) return null;
            serialized.Update();
            var list = serialized.FindProperty(path);
            return list is { isArray: true } && list.propertyType != SerializedPropertyType.String ? list : null;
        }
    }
}
