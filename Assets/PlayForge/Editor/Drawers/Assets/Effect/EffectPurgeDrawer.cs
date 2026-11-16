using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// An effect's Purges, a foldout of its fields, its arrow in line with the lists' around it in the Tags section.
    /// Unity hangs an inspector foldout's arrow 12 px into the margin, to the left of a list's arrow; this one
    /// keeps the lists' column, as <c>pf-foldout--aligned</c> sets in PlayForge.uss.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectPurge))]
    public class EffectPurgeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new Foldout { text = preferredLabel ?? property.displayName, value = property.isExpanded, name = "PurgeFoldout" };
            ForgeTheme.Apply(foldout);
            foldout.AddToClassList("pf-foldout--aligned");
            var path = property.propertyPath;
            var serialized = property.serializedObject;
            foldout.RegisterValueChangedCallback(change =>
            {
                if (change.target != foldout) return;
                var now = serialized.targetObject != null ? serialized.FindProperty(path) : null;
                if (now != null) now.isExpanded = change.newValue;
            });

            // Each of its fields, Unity's, as a class's foldout holds them
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (child.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    foldout.Add(new PropertyField(child.Copy()) { name = child.name });
                } while (child.NextVisible(false));
            }
            return foldout;
        }
    }
}
