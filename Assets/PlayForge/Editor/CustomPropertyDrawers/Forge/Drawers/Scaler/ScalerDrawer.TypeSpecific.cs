using System;
using System.Collections;
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
        /// Draws fields declared on the concrete scaler subclass via reflection.
        /// Fields inherited from AbstractScaler / AbstractCachedScaler (or any base) are
        /// skipped — those are rendered by the standard scaler chrome elsewhere in the drawer.
        /// Labels are suppressed only for fields whose type derives from BaseForgeAsset.
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

            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            foreach (var fieldInfo in fields)
            {
                if (!IsSerializableField(fieldInfo)) continue;

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
