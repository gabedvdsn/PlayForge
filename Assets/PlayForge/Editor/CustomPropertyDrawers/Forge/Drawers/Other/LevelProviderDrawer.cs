using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A [LinkedSource] field, the asset whose levels this one takes (ED-66): Unity's object field of level providers,
    /// whose picker lists them, and under it a line of the levels it gives. It was any asset's field that warned at a
    /// wrong pick, with a tinted box, a colored type badge, a × and a "..." that opened a browser window of its own,
    /// which Unity's picker does.
    /// </summary>
    [CustomPropertyDrawer(typeof(LinkedSourceAttribute))]
    public class LevelProviderDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "LevelSourceRoot" };
            // The style sheet lays out the line, wherever the field is drawn (ED-20)
            ForgeTheme.Apply(root);

            var type = fieldInfo?.FieldType;
            var source = new ObjectField(preferredLabel ?? property.displayName)
            {
                name = "LevelSource",
                objectType = type != null && typeof(UnityEngine.Object).IsAssignableFrom(type) ? type : typeof(BaseForgeLevelProvider),
                allowSceneObjects = false,
                tooltip = "The asset whose levels this one takes: an ability or an entity. With none, it keeps its own",
            };
            source.AddToClassList(ObjectField.alignedFieldUssClassName);
            source.BindProperty(property);
            root.Add(source);

            var line = new VisualElement { name = "LevelSourceLink" };
            line.AddToClassList("pf-link-line");
            line.Add(new ForgeGlyph(ForgeGlyphShape.Link));
            var text = new Label { name = "LevelSourceLine" };
            text.AddToClassList("pf-link-line__text");
            line.Add(text);
            root.Add(line);

            var serialized = property.serializedObject;
            var path = property.propertyPath;
            void Show()
            {
                SerializedProperty fresh;
                try
                {
                    fresh = serialized.targetObject != null ? serialized.FindProperty(path) : null;
                }
                catch (Exception)
                {
                    return;
                }
                var provider = fresh is { hasMultipleDifferentValues: false } ? fresh.objectReferenceValue as ILevelProvider : null;
                line.EnableInClassList("pf-hidden", provider == null);
                if (provider != null) text.text = $"{LevelsOf(provider)} from {provider.GetProviderName()}";
            }

            root.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
            return root;
        }

        // The levels a provider gives, as "Levels 1–5", or "Level 3" when it has one
        private static string LevelsOf(ILevelProvider provider)
        {
            int starting = provider.GetStartingLevel(), max = provider.GetMaxLevel();
            return starting == max ? $"Level {starting}" : $"Levels {starting}–{max}";
        }
    }
}
