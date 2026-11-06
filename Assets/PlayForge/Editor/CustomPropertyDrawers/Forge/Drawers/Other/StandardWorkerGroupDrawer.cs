using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A worker group, in Docs/EDITOR_STYLE.md's terms (ED-57): its name, then its four lists of workers, each Unity's
    /// reorderable list, where a new worker starts empty for its kind to be picked. The Workers section it's drawn in
    /// folds, counts and imports it.
    /// <para>
    /// It was a box of its own, with a colored border, colored count badges, a type tag and an Import of its own, from 90
    /// styles set in C#, inside a section whose header already counted and imported it.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(StandardWorkerGroup))]
    public class StandardWorkerGroupDrawer : PropertyDrawer
    {
        // Each list, with what its workers do and which system runs them
        private static readonly (string Path, string Label, string Tip)[] Lists =
        {
            (nameof(StandardWorkerGroup.AttributeWorkers), "Attribute Workers",
                "Workers the attribute system runs before and after it changes an attribute"),
            (nameof(StandardWorkerGroup.ImpactWorkers), "Impact Workers",
                "Workers the ability system runs on each impact the entity deals"),
            (nameof(StandardWorkerGroup.TagWorkers), "Tag Workers",
                "Workers the tag cache runs as the entity's tags change"),
            (nameof(StandardWorkerGroup.AnalysisWorkers), "Analysis Workers",
                "Workers that look over the frame's gathered data at its end"),
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "WorkerGroup" };
            // The style sheet lays out the group, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);

            var name = new TextField("Name")
            {
                name = "WorkerGroupName",
                tooltip = "The group's name, as the debugger shows it",
            };
            name.AddToClassList(TextField.alignedFieldUssClassName);
            name.BindProperty(property.FindPropertyRelative(nameof(StandardWorkerGroup.Name)));
            root.Add(name);

            var serialized = property.serializedObject;
            foreach (var (path, label, tip) in Lists)
            {
                var list = property.FindPropertyRelative(path);
                if (list == null) continue;
                var field = new PropertyField(list, label) { name = path, tooltip = tip };
                ForgeLists.AddsEmpty(field, serialized, list.propertyPath);
                root.Add(field);
            }
            return root;
        }
    }
}
