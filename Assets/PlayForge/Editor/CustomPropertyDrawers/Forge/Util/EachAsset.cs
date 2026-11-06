using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A property across a multiple selection, one selected asset at a time, each through a SerializedObject of its own.
    /// A property of a multiple selection reads the first asset, and a write through it gives every asset the same
    /// value, so what is worked out from an asset's own values, such as a scaler's first value or its curve, is read and
    /// written per asset (ED-12). With one asset selected, each call uses the property found again by its path, since one
    /// a field kept since it was built can point into a managed reference an Undo has since put back (ED-49).
    /// </summary>
    internal static class EachAsset
    {
        /// <summary>Each selected asset's own copy of the property; the property found again when one asset is selected.</summary>
        internal static IEnumerable<SerializedProperty> Of(SerializedProperty property)
        {
            if (property == null) yield break;
            var serialized = property.serializedObject;
            if (!serialized.isEditingMultipleObjects)
            {
                // Found again by its path, as Write finds it (ED-49)
                var fresh = serialized.FindProperty(property.propertyPath);
                if (fresh != null) yield return fresh;
                yield break;
            }
            foreach (var target in serialized.targetObjects)
            {
                if (target == null) continue;
                var own = new SerializedObject(target).FindProperty(property.propertyPath);
                if (own != null) yield return own;
            }
        }

        /// <summary>Whether the selected assets disagree on what a read of the property gives.</summary>
        internal static bool Differ<T>(SerializedProperty property, Func<SerializedProperty, T> read) =>
            Of(property).Select(read).Distinct().Skip(1).Any();

        /// <summary>
        /// Writes each selected asset's own copy of the property, then applies it, in the current Undo step. The
        /// property's SerializedObject then reads what was written.
        /// </summary>
        internal static void Write(SerializedProperty property, Action<SerializedProperty> write)
        {
            if (property == null) return;
            var serialized = property.serializedObject;
            if (!serialized.isEditingMultipleObjects)
            {
                // Found again by its path: a property a field kept since it was built can point into a managed reference
                // an Undo has since put back, and resizing an array through it crashed the editor (ED-49)
                var fresh = serialized.FindProperty(property.propertyPath);
                if (fresh == null) return;
                write(fresh);
                serialized.ApplyModifiedProperties();
                return;
            }
            foreach (var own in Of(property).ToList())
            {
                write(own);
                own.serializedObject.ApplyModifiedProperties();
            }
            serialized.Update();
        }
    }
}
