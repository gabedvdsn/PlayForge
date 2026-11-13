using System;
using System.Collections.Generic;
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
        private const string AddsOwnCopiesHook = "pf-list--adds-own-copies";
        private const string KeepsStatesHook = "pf-list--keeps-states";

        /// <summary>
        /// A list of objects its field holds by reference: its + adds an empty element, for its kind to be picked. Grown
        /// as an array, the list gave the new element the last one's object, so the two were one.
        /// </summary>
        public static void AddsEmpty(PropertyField field, SerializedObject serialized, string path) =>
            Hook(field, AddsEmptyHook, list => list.overridingAddButtonBehavior = (_, _) => AddEmpty(serialized, path));

        /// <summary>
        /// A list whose elements hold objects by reference, as a scaler Group's members hold their scalers: its + adds a copy
        /// of the last element, as Unity's does, each object it holds a copy of its own. Grown as an array, the new element
        /// held the last one's objects, so an edit to one changed both (FN-2). An element that is itself an object held by
        /// reference starts empty, as <see cref="AddsEmpty"/> has it.
        /// </summary>
        public static void AddsOwnCopies(PropertyField field, SerializedObject serialized, string path) =>
            Hook(field, AddsOwnCopiesHook, list => list.overridingAddButtonBehavior = (_, _) => AddOwnCopies(serialized, path));

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

        /// <summary>
        /// Adds a copy of the last element to the list at <paramref name="path"/>, each object it holds a copy of its own, as
        /// one Undo step; in a multiple selection, each asset's list copies its own last element (ED-12).
        /// </summary>
        internal static void AddOwnCopies(SerializedObject serialized, string path)
        {
            var list = Find(serialized, path);
            if (list == null) return;
            EachAsset.Write(list, own =>
            {
                int at = own.arraySize;
                own.arraySize = at + 1;
                var added = own.GetArrayElementAtIndex(at);
                if (added.propertyType == SerializedPropertyType.ManagedReference)
                {
                    added.managedReferenceValue = null;
                    return;
                }
                foreach (var held in HeldByReference(added)) CopyOwn(own.serializedObject.FindProperty(held));
            });
        }

        /// <summary>Gives a field a copy of its own of the object it holds, with the same values.</summary>
        internal static void CopyOwn(SerializedProperty field)
        {
            var current = field?.managedReferenceValue;
            if (current == null) return;
            var copy = Activator.CreateInstance(current.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(current), copy);
            field.managedReferenceValue = copy;
        }

        // The paths of the objects an element holds by reference, each the outermost: an object's own go with its copy
        private static List<string> HeldByReference(SerializedProperty element)
        {
            var paths = new List<string>();
            var each = element.Copy();
            var end = element.GetEndProperty();
            bool enter = true;
            while (each.Next(enter) && !SerializedProperty.EqualContents(each, end))
            {
                enter = each.propertyType != SerializedPropertyType.ManagedReference;
                if (!enter && each.managedReferenceValue != null) paths.Add(each.propertyPath);
            }
            return paths;
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
