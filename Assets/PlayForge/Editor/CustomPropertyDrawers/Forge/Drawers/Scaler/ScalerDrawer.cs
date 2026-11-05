using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A scaler field drawn on its own, such as an effect's Delta Time Scaler or an attribute set element's Scaling, in
    /// the canvas scaler board's terms (ED-33): its name and kind, the range it gives, then its content: its values per
    /// level with their curve, Stretch Over Levels, the kind's own fields and its behaviours, or a Shared scaler's
    /// asset. It drew a tinted box with a colored border, a collapse arrow, Quick Fill tabs and an import button, and
    /// rebuilt itself on a copy of the first selected asset, so an edit after a rebuild reached that asset alone (ED-12).
    /// It writes nothing while it draws (ED-34).
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractScaler), true)]
    public class ScalerDrawer : PropertyDrawer
    {
        private const string NoKind = "None";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "ScalerRoot" };
            // The style sheet lays out the scaler, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-scaler-field");
            var label = preferredLabel ?? property.displayName;
            var kinds = ScalerFields.KindsFor(property);

            Type shownType = null;
            bool built = false;
            Label range = null;

            void Draw()
            {
                if (!ScalerFields.Alive(property)) return;
                root.Clear();
                shownType = property.managedReferenceValue?.GetType();
                built = true;

                var choices = new List<string> { NoKind };
                choices.AddRange(kinds.Select(ScalerFields.KindName));
                var shownName = shownType == null ? NoKind : ScalerFields.KindName(shownType);
                if (!choices.Contains(shownName)) choices.Add(shownName);

                var row = new VisualElement { name = "Row" };
                row.AddToClassList("pf-magnitude__row");
                var kind = new PopupField<string>(label, choices, shownName) { name = "Kind", tooltip = "The scaler's kind, or none" };
                kind.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                kind.AddToClassList("pf-magnitude__kind-labelled");
                row.Add(kind);
                range = new Label { name = "Range" };
                range.AddToClassList("pf-magnitude__range");
                row.Add(range);
                root.Add(row);

                // A scaler another list element shares, after Unity's array "+" copied its reference id, says so, with a
                // button that gives this element its own copy. Building the drawer cloned it, so drawing changed it (ED-34)
                int sharedWith = FindSharedSibling(property);
                if (sharedWith >= 0)
                {
                    var note = new HelpBox($"Element {sharedWith} holds the same scaler, so an edit here changes it too.",
                        HelpBoxMessageType.Warning) { name = "SharedSibling" };
                    note.Add(new Button(() => GiveOwnCopy(property)) { name = "MakeSeparateCopy", text = "Make a Separate Copy" });
                    root.Add(note);
                }

                if (shownType != null)
                {
                    var content = ScalerFields.Content(property);
                    content.AddToClassList("pf-scaler-field__content");
                    root.Add(content);
                }

                kind.RegisterValueChangedCallback(change =>
                {
                    if (change.newValue == change.previousValue) return;
                    var type = change.newValue == NoKind ? null : kinds.FirstOrDefault(each => ScalerFields.KindName(each) == change.newValue);
                    var values = ScalerFields.ValuesOf(property);
                    ScalerFields.SetKind(property, type, values.Length > 0 ? values[0] : 1f);
                    Draw();
                });
                Show();
            }

            void Show()
            {
                if (range != null) range.text = ScalerFields.RangeText(ScalerFields.ValuesOf(property), null);
            }

            root.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                if (!ScalerFields.Alive(property)) return;
                if (!built || property.managedReferenceValue?.GetType() != shownType) Draw();
                else Show();
            });
            Draw();
            return root;
        }

        /// <summary>
        /// Finds another element of the same list whose scaler has this property's managed-reference id. Unity's array
        /// "+" copies the previous element's id, so two elements can hold one scaler, and an edit to one changes both.
        /// Returns that element's index, or -1. It only reads (ED-34).
        /// </summary>
        private static int FindSharedSibling(SerializedProperty property)
        {
            if (property == null) return -1;
            if (property.propertyType != SerializedPropertyType.ManagedReference) return -1;
            if (property.managedReferenceValue == null) return -1;

            long id = property.managedReferenceId;
            string path = property.propertyPath;
            const string arrayData = ".Array.data[";
            int marker = path.LastIndexOf(arrayData, StringComparison.Ordinal);
            if (marker < 0) return -1;

            string arrayPath = path.Substring(0, marker);
            int bracketStart = path.IndexOf('[', marker);
            int bracketEnd = path.IndexOf(']', bracketStart);
            if (bracketStart < 0 || bracketEnd < 0) return -1;
            if (!int.TryParse(path.Substring(bracketStart + 1, bracketEnd - bracketStart - 1), out int myIndex)) return -1;

            // What follows the element in the path, as ".Current.Scaling", or nothing when the property is the element
            string relative = path.Substring(bracketEnd + 1).TrimStart('.');
            var array = property.serializedObject.FindProperty(arrayPath);
            if (array == null || !array.isArray) return -1;

            for (int i = 0; i < array.arraySize; i++)
            {
                if (i == myIndex) continue;
                var sibling = array.GetArrayElementAtIndex(i);
                var siblingRef = string.IsNullOrEmpty(relative) ? sibling : sibling.FindPropertyRelative(relative);
                if (siblingRef == null || siblingRef.propertyType != SerializedPropertyType.ManagedReference) continue;
                if (siblingRef.managedReferenceId == id) return i;
            }
            return -1;
        }

        // Gives this element a scaler of its own, a copy of the one it shares, as one undoable step
        private static void GiveOwnCopy(SerializedProperty property)
        {
            var current = property.managedReferenceValue;
            if (current == null) return;
            var clone = Activator.CreateInstance(current.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(current), clone);
            property.managedReferenceValue = clone;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
