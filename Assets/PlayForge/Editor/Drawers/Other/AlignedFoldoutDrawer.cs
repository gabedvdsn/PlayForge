using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Each class PlayForge serializes in a field with no drawer of its own, drawn as a foldout of its fields, its arrow
    /// in line with the rows around it, as a list's is: an attribute read's retention groups, as an × Attribute scaler's
    /// Groups, a turn to face's settings, an asset's definition and tags, a list's entries. Unity hangs an inspector
    /// foldout's arrow 12 px into the margin, to the left of a list's arrow and, in a scaler's block, out to the block's
    /// edge; this one keeps the lists' column, as <c>pf-foldout--aligned</c> sets in PlayForge.uss. A class opts in with a
    /// <see cref="CustomPropertyDrawer"/> of its type here. A field marked <see cref="ForgeShowWhenAttribute"/> shows only
    /// while the field it names says so, as a read's Groups shows only for the reads that name groups. An inspector drawn
    /// with IMGUI gets the same foldout.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityDefinition))]
    [CustomPropertyDrawer(typeof(AbilityTags))]
    [CustomPropertyDrawer(typeof(AbilityTrigger))]
    [CustomPropertyDrawer(typeof(AttributeMagnitudeSpec))]
    [CustomPropertyDrawer(typeof(CombatLogSettings))]
    [CustomPropertyDrawer(typeof(CueRoute))]
    [CustomPropertyDrawer(typeof(CuePlacement))]
    [CustomPropertyDrawer(typeof(FloatingTextValue))]
    [CustomPropertyDrawer(typeof(FloatingTextLook))]
    [CustomPropertyDrawer(typeof(FloatingTextMotion))]
    [CustomPropertyDrawer(typeof(FloatingTextStacking))]
    [CustomPropertyDrawer(typeof(EffectConditions))]
    [CustomPropertyDrawer(typeof(EffectModifierEntry))]
    [CustomPropertyDrawer(typeof(EffectModifierRule))]
    [CustomPropertyDrawer(typeof(EntityDeath))]
    [CustomPropertyDrawer(typeof(ForgeAssetCatalog.Entry))]
    [CustomPropertyDrawer(typeof(ForgeAuthoringSettings))]
    [CustomPropertyDrawer(typeof(ForgeProjectSettings))]
    [CustomPropertyDrawer(typeof(ForgeTagLibrary.Entry))]
    [CustomPropertyDrawer(typeof(GameplayEffectDefinition))]
    [CustomPropertyDrawer(typeof(GameplayEffectTags))]
    [CustomPropertyDrawer(typeof(ItemDefinition))]
    [CustomPropertyDrawer(typeof(ItemTags))]
    [CustomPropertyDrawer(typeof(NewAssetSettings))]
    [CustomPropertyDrawer(typeof(OnHitEntry))]
    [CustomPropertyDrawer(typeof(ProcessAttribute))]
    [CustomPropertyDrawer(typeof(PurgeLevels))]
    [CustomPropertyDrawer(typeof(PurgeTier))]
    [CustomPropertyDrawer(typeof(ResolveOrderEntry))]
    [CustomPropertyDrawer(typeof(RetentionGroups))]
    [CustomPropertyDrawer(typeof(TagAttributePair))]
    [CustomPropertyDrawer(typeof(TaggedCues))]
    [CustomPropertyDrawer(typeof(TargetAttributeCondition))]
    [CustomPropertyDrawer(typeof(TemplateListSnapshot))]
    [CustomPropertyDrawer(typeof(TurnToFaceSettings))]
    [CustomPropertyDrawer(typeof(TypeDefaults))]
    public class AlignedFoldoutDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new Foldout { text = preferredLabel ?? property.displayName, value = property.isExpanded, name = "AlignedFoldout" };
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
            var type = DrawnType(fieldInfo);
            foreach (var child in Fields(property))
            {
                var field = new PropertyField(child) { name = child.name };
                foldout.Add(field);
                var when = WhenOf(type, child.name);
                if (when.Length > 0) ForgeShowWhen.Track(field, serialized, now => Shows(now, path, when));
            }
            return foldout;
        }

        // An inspector drawn with IMGUI, as a project's own editor may be, draws the same foldout of its fields, where a
        // drawer for UI Toolkit alone draws nothing there
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            foreach (var child in ShownFields(property)) height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded) return;
            using var indent = new EditorGUI.IndentLevelScope();
            float y = line.yMax;
            foreach (var child in ShownFields(property))
            {
                y += EditorGUIUtility.standardVerticalSpacing;
                float height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                y += height;
            }
        }

        // Its visible fields, each a copy, in order
        private static IEnumerable<SerializedProperty> Fields(SerializedProperty property)
        {
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true)) yield break;
            do
            {
                if (SerializedProperty.EqualContents(child, end)) yield break;
                yield return child.Copy();
            } while (child.NextVisible(false));
        }

        // Its visible fields that show now, for IMGUI, which draws them again each time
        private IEnumerable<SerializedProperty> ShownFields(SerializedProperty property)
        {
            var type = DrawnType(fieldInfo);
            return Fields(property).Where(child => WhenOf(type, child.name) is not { Length: > 0 } when
                                                   || Shows(property.serializedObject, property.propertyPath, when) != false);
        }

        // The conditions a field of the class shows under
        private static ForgeShowWhenAttribute[] WhenOf(Type type, string field) =>
            type?.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetCustomAttributes<ForgeShowWhenAttribute>().ToArray() ?? Array.Empty<ForgeShowWhenAttribute>();

        // The class the drawn field holds, or its elements' where it's a list or an array
        private static Type DrawnType(FieldInfo info)
        {
            var type = info?.FieldType;
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : type;
        }

        // Whether each field a condition names, beside this one, holds one of its values: false where one doesn't, and
        // null, which shows the field, where one can't be told, as a selection's values differ
        private static bool? Shows(SerializedObject serialized, string path, ForgeShowWhenAttribute[] when)
        {
            bool told = true;
            foreach (var each in when)
            {
                var says = Says(serialized.FindProperty($"{path}.{each.Field}"), each.Values);
                if (says == false) return false;
                told &= says.HasValue;
            }
            return told ? true : null;
        }

        // Whether a field holds one of the values given: a bool's, or an enum's or a whole number's
        private static bool? Says(SerializedProperty field, object[] values)
        {
            if (field == null || field.hasMultipleDifferentValues) return null;
            if (field.propertyType == SerializedPropertyType.Boolean)
                return values.Length == 0 ? field.boolValue : values.Any(value => value is bool on && on == field.boolValue);
            return ForgeShowWhen.Says(field, values.OfType<IConvertible>().Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray());
        }
    }
}
