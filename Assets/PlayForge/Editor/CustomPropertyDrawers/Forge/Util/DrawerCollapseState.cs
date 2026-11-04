using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Whether a part of a drawer is collapsed. It is kept for each asset and property for the rest of the editor
    /// session, through domain reloads. A field that holds a [SerializeReference] object keeps it with that object, so
    /// the state follows the object when its list is reordered. The drawers kept it in static dictionaries keyed by
    /// property path alone: collapsing a part on one asset collapsed it on every asset, a domain reload forgot it, and a
    /// moved worker took the state of the worker that had been in its place (ED-18).
    /// </summary>
    internal static class DrawerCollapseState
    {
        private const string Prefix = "PlayForge.Collapsed.";
        private const int Unset = -1;

        /// <summary>
        /// Whether the property's drawer, or the part of it that <paramref name="part"/> names, is collapsed. Until it
        /// is set, the drawer's own default.
        /// </summary>
        public static bool IsCollapsed(SerializedProperty property, bool collapsedByDefault, string part = null)
        {
            var stored = SessionState.GetInt(Key(property, part), Unset);
            return stored == Unset ? collapsedByDefault : stored == 1;
        }

        public static void SetCollapsed(SerializedProperty property, bool collapsed, string part = null) =>
            SessionState.SetInt(Key(property, part), collapsed ? 1 : 0);

        /// <summary>
        /// Moves each later element's state up a place after the element at <paramref name="removed"/> is deleted, so
        /// it stays with its element. <paramref name="countBefore"/> is the list's length before the delete. An element
        /// that holds a [SerializeReference] object needs none of this, since its state follows the object.
        /// </summary>
        public static void RemoveElement(SerializedProperty list, int removed, int countBefore, string part = null)
        {
            string Element(int index) => Key(list.serializedObject, $"{list.propertyPath}.Array.data[{index}]", part);

            for (int index = removed; index < countBefore - 1; index++)
            {
                var next = SessionState.GetInt(Element(index + 1), Unset);
                if (next == Unset) SessionState.EraseInt(Element(index));
                else SessionState.SetInt(Element(index), next);
            }
            SessionState.EraseInt(Element(countBefore - 1));
        }

        private static string Key(SerializedProperty property, string part)
        {
            var heldObject = property.propertyType == SerializedPropertyType.ManagedReference
                ? property.managedReferenceId
                : -1;
            return Key(property.serializedObject, heldObject >= 0 ? $"#{heldObject}" : property.propertyPath, part);
        }

        private static string Key(SerializedObject serializedObject, string identity, string part)
        {
            var target = serializedObject.targetObject;
            var asset = target != null ? target.GetInstanceID() : 0;
            return part == null ? $"{Prefix}{asset}/{identity}" : $"{Prefix}{asset}/{identity}/{part}";
        }
    }
}
