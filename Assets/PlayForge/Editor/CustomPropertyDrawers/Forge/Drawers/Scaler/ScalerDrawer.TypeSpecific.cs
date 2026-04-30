using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class ScalerDrawer
    {
        /// <summary>
        /// Draws all fields introduced by *concrete* classes in the scaler's inheritance chain.
        ///
        /// Reflection walk stops at the first abstract base (typically <c>AbstractScaler</c> or
        /// <c>AbstractCachedScaler</c>) so the framework chrome those expose — Configuration,
        /// MaxLevel, LevelValues, Interpolation, Scaling curve, Behaviours, etc. — isn't
        /// duplicated here; the rest of <see cref="ScalerDrawer"/> renders them already.
        ///
        /// Fields from *intermediate* concrete classes ARE included, which fixes the previous
        /// behaviour where e.g. <c>CachedAttributeBackedScaler</c> only showed its own
        /// <c>RelativeOperation</c> while <c>CaptureAttribute</c>/<c>CaptureWhat</c> declared
        /// on its concrete parent <c>CachedAttributeScaler</c> were silently dropped.
        ///
        /// Order: base-most concrete → most-derived, so a child class's additions appear after
        /// its parent's fields — matches authoring intuition. Fields with the same name across
        /// levels (i.e. <c>new</c>-shadowed) are deduplicated by name; the deepest declaration wins.
        ///
        /// Labels are suppressed only for fields whose type derives from <see cref="BaseForgeAsset"/>.
        /// </summary>
        private void AddTypeSpecificProperties(VisualElement content, SerializedProperty property, Type type, VisualElement root)
        {
            var scaler = property.managedReferenceValue as AbstractScaler;
            if (scaler == null) return;

            var headerLabel = new Label($"[{FormatTypeName(type.Name)}]");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.AccentPurple;
            headerLabel.style.marginBottom = 4;
            content.Add(headerLabel);

            foreach (var fieldInfo in CollectConcreteHierarchyFields(type))
            {
                var fieldProp = property.FindPropertyRelative(fieldInfo.Name);
                if (fieldProp == null) continue;

                bool suppressLabel = IsBaseForgeAssetField(fieldInfo.FieldType);
                string label = suppressLabel ? string.Empty : null; // null → PropertyField default label

                var field = new PropertyField(fieldProp, label);
                field.BindProperty(fieldProp);
                field.style.marginBottom = 2;
                content.Add(field);
            }
        }

        /// <summary>
        /// Walks <paramref name="type"/>'s base chain upward, halting at the first abstract type,
        /// and yields serializable fields declared at each level — base-concrete first, derived last.
        /// Fields shadowed by name across levels collapse to a single entry (deepest declaration wins),
        /// preventing duplicate <see cref="PropertyField"/>s for the same serialized property path.
        /// </summary>
        private static IEnumerable<FieldInfo> CollectConcreteHierarchyFields(Type type)
        {
            // Build the concrete chain: [most-derived, ..., base-most concrete]
            var chain = new List<Type>();
            var t = type;
            while (t != null && !t.IsAbstract)
            {
                chain.Add(t);
                t = t.BaseType;
            }

            // Iterate base-most → most-derived so output ordering matches declaration order
            // a user would expect when reading the class top-down with inheritance flattened.
            chain.Reverse();

            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            // Map name → field. Re-assignment naturally lets a derived `new`-shadowed field
            // replace its parent's, mirroring how Unity actually serializes shadowed fields.
            var byName = new Dictionary<string, FieldInfo>();
            var ordered = new List<string>();

            foreach (var level in chain)
            {
                foreach (var f in level.GetFields(flags))
                {
                    if (!IsSerializableField(f)) continue;
                    if (!byName.ContainsKey(f.Name)) ordered.Add(f.Name);
                    byName[f.Name] = f;
                }
            }

            foreach (var name in ordered) yield return byName[name];
        }

        private static bool IsSerializableField(FieldInfo fieldInfo)
        {
            if (fieldInfo.IsNotSerialized) return false;
            if (fieldInfo.IsStatic) return false;

            if (fieldInfo.IsPublic)
            {
                if (fieldInfo.GetCustomAttribute<NonSerializedAttribute>() != null) return false;
                if (fieldInfo.GetCustomAttribute<HideInInspector>() != null) return false;
                return true;
            }

            // Non-public fields require [SerializeField] or [SerializeReference]
            return fieldInfo.GetCustomAttribute<SerializeField>() != null
                || fieldInfo.GetCustomAttribute<SerializeReference>() != null;
        }

        private static bool IsBaseForgeAssetField(Type fieldType)
        {
            if (fieldType == null) return false;
            if (typeof(BaseForgeAsset).IsAssignableFrom(fieldType)) return true;

            // Arrays of BaseForgeAsset
            if (fieldType.IsArray)
            {
                var elem = fieldType.GetElementType();
                if (elem != null && typeof(BaseForgeAsset).IsAssignableFrom(elem)) return true;
            }

            // Generic lists of BaseForgeAsset (List<T>, etc.)
            if (fieldType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(fieldType))
            {
                var args = fieldType.GetGenericArguments();
                if (args.Length == 1 && typeof(BaseForgeAsset).IsAssignableFrom(args[0])) return true;
            }

            return false;
        }
    }
}
