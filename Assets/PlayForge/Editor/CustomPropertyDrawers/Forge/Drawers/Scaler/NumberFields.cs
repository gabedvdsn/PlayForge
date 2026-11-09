using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What the number field's inspector and its Problems rule know of numbers (FEAT-29): which properties are numbers,
    /// what a field's context provides and its unit, what a kind needs and where the kind menu files it, and what a
    /// number's kind reads, inside other scalers too.
    /// </summary>
    public static class NumberFields
    {
        // The number types' saved names, the old ones among them, as a SerializedProperty's type gives them
        private static readonly HashSet<string> NumberTypes = new()
        {
            nameof(ScalableFloat), nameof(ScalableInt), "ScalerMagnitudeOperation", "ScalerIntegerMagnitudeOperation",
        };

        private static readonly HashSet<string> WholeTypes = new() { nameof(ScalableInt), "ScalerIntegerMagnitudeOperation" };

        /// <summary>Whether a property is a number field: a ScalableFloat or a ScalableInt, by its new name or its old.</summary>
        public static bool IsNumber(SerializedProperty property) =>
            property != null && property.propertyType == SerializedPropertyType.Generic && NumberTypes.Contains(property.type);

        /// <summary>Whether a number field takes whole numbers.</summary>
        public static bool IsWhole(SerializedProperty property) => property != null && WholeTypes.Contains(property.type);

        // ═══════════════════════════════════════════════════════════════════════════
        // A field's declaration
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>What a field's context provides: its <see cref="NumberContextAttribute"/>'s, or everything without one.</summary>
        public static NumberReads ProvidesOf(FieldInfo field) =>
            field?.GetCustomAttribute<NumberContextAttribute>()?.Provides ?? NumberReads.All;

        /// <summary>What a number property's context provides, found through the managed references on its path.</summary>
        public static NumberReads ProvidesOf(SerializedProperty property) => ProvidesOf(FieldOf(property));

        /// <summary>A field's unit: its <see cref="NumberContextAttribute"/>'s, else its <see cref="ScalerOperationKeyword"/>'s.</summary>
        public static string UnitOf(FieldInfo field) =>
            field?.GetCustomAttribute<NumberContextAttribute>()?.Unit ?? field?.GetCustomAttribute<ScalerOperationKeyword>()?.Unit;

        /// <summary>
        /// The field a property is, read along its path through the values of the managed references on it, so a number
        /// inside a scaler is found as its own class declares it; null where the path can't be followed.
        /// </summary>
        public static FieldInfo FieldOf(SerializedProperty property)
        {
            if (property?.serializedObject?.targetObject == null) return null;
            var type = property.serializedObject.targetObject.GetType();
            FieldInfo field = null;
            var parts = property.propertyPath.Split('.');
            var walked = "";
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == "Array")
                {
                    walked += ".Array";
                    continue;
                }
                if (part.StartsWith("data["))
                {
                    walked += $".{part}";
                    type = ManagedTypeAt(property.serializedObject, walked) ?? type;
                    continue;
                }

                field = Find(type, part);
                if (field == null) return null;
                walked = walked.Length == 0 ? part : $"{walked}.{part}";
                type = ElementOf(field.FieldType);
                if (i + 1 < parts.Length && parts[i + 1] != "Array") type = ManagedTypeAt(property.serializedObject, walked) ?? type;
            }
            return field;
        }

        private static Type ManagedTypeAt(SerializedObject serialized, string path)
        {
            var at = serialized.FindProperty(path);
            return at is { propertyType: SerializedPropertyType.ManagedReference } ? at.managedReferenceValue?.GetType() : null;
        }

        private static FieldInfo Find(Type type, string name)
        {
            for (var each = type; each != null && each != typeof(object); each = each.BaseType)
            {
                var field = each.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static Type ElementOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return type;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Kinds: what each needs, and where the menu files it
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The kind menu's group for the scalers of every kind, and for kinds that name none.</summary>
        public const string ScalerCategory = "Scaler";

        /// <summary>What a kind needs of a field's context, as its <see cref="NumberKindAttribute"/> says: none without one.</summary>
        public static NumberReads NeedsOf(Type kind) => NumberKindAttribute.Of(kind)?.Reads ?? NumberReads.None;

        /// <summary>Whether a field whose context provides <paramref name="provides"/> resolves a kind, so its menu offers it.</summary>
        public static bool Resolves(NumberReads provides, Type kind) => (NeedsOf(kind) & ~provides) == NumberReads.None;

        /// <summary>
        /// The kind menu's group for a kind: the category its <see cref="NumberKindAttribute"/> names; none for the kinds
        /// at the menu's top, By Level, Shared, × Attribute and Set By Caller; Scaler for every other.
        /// </summary>
        public static string CategoryOf(Type kind)
        {
            var declared = NumberKindAttribute.Of(kind);
            if (declared != null) return string.IsNullOrEmpty(declared.Category) ? null : declared.Category;
            return kind == typeof(CachedSimpleScaler) || kind == typeof(SharedCachedScaler) ? null : ScalerCategory;
        }

        /// <summary>A kind's place in the kind menu, as "Scaler/Clamped", or its name alone at the top.</summary>
        public static string MenuPathOf(Type kind)
        {
            var category = CategoryOf(kind);
            var name = ScalerFields.KindName(kind);
            return string.IsNullOrEmpty(category) ? name : $"{category}/{name}";
        }

        /// <summary>
        /// Kinds in the menu's order: the top's first, By Level, Shared, × Attribute and Set By Caller, then the rest by
        /// name; then each category's by name, the categories by name.
        /// </summary>
        public static List<Type> InMenuOrder(IEnumerable<Type> kinds) => kinds
            .OrderBy(kind => CategoryOf(kind) == null ? 0 : 1)
            .ThenBy(kind => CategoryOf(kind) ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(TopRank)
            .ThenBy(ScalerFields.KindName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        private static int TopRank(Type kind) =>
            kind == typeof(SimpleScaler) || kind == typeof(CachedSimpleScaler) ? 0
            : ScalerFields.IsShared(kind) ? 1
            : kind == typeof(AttributeBackedScaler) ? 2
            : kind == typeof(SetByCallerScaler) ? 3
            : 4;

        // ═══════════════════════════════════════════════════════════════════════════
        // What a number reads
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// What a scaler reads of its context, as it's set now: a kind's needs, an attribute read from the target or the
        /// caster, a Conditional's condition, and what the scalers and numbers inside it read.
        /// </summary>
        public static NumberReads ReadsOf(AbstractScaler scaler) => ReadsOf(scaler, 0);

        // How deep a scaler's own scalers are walked, past which a loop is assumed
        private const int MostDepth = 12;

        private static NumberReads ReadsOf(object value, int depth)
        {
            if (value == null || depth > MostDepth) return NumberReads.None;
            switch (value)
            {
                case ScalableFloat number: return ReadsOf(number.Scaler, depth + 1);
                case ScalableInt whole: return ReadsOf(whole.Scaler, depth + 1);
                case AbstractScaler scaler:
                    var reads = scaler switch
                    {
                        NumberKind kind => kind.Reads,
                        AttributeBackedScaler attribute => attribute.CaptureFrom == ESourceTarget.Target ? NumberReads.Target : NumberReads.Caster,
                        ConditionalScaler conditional => ConditionReads(conditional.Condition),
                        _ => NeedsOf(scaler.GetType()),
                    };
                    return reads | PartsRead(scaler, depth);
                default:
                    return PartsRead(value, depth);
            }
        }

        private static NumberReads ConditionReads(EScalerCondition condition)
        {
            var name = condition.ToString();
            if (name.StartsWith("Target")) return NumberReads.Target;
            if (name.StartsWith("Source")) return NumberReads.Caster;
            return NumberReads.Level;
        }

        // What the scalers and numbers a value holds read: its fields that hold one, a list of them, or a struct holding one
        private static NumberReads PartsRead(object value, int depth)
        {
            var reads = NumberReads.None;
            foreach (var field in PartsOf(value.GetType()))
            {
                var held = field.GetValue(value);
                if (held is IEnumerable many and not string)
                {
                    foreach (var each in many) reads |= ReadsOf(each, depth + 1);
                }
                else reads |= ReadsOf(held, depth + 1);
            }
            return reads;
        }

        private static readonly Dictionary<Type, List<FieldInfo>> Parts = new();

        // A type's saved fields that can hold a scaler or a number, found once a type
        private static List<FieldInfo> PartsOf(Type type)
        {
            if (Parts.TryGetValue(type, out var found)) return found;
            found = new List<FieldInfo>();
            for (var each = type; each != null && each != typeof(object) && each != typeof(AbstractScaler); each = each.BaseType)
            {
                foreach (var field in each.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null && field.GetCustomAttribute<SerializeReference>() == null) continue;
                    if (field.IsNotSerialized) continue;
                    if (CanHold(ElementOf(field.FieldType), 0)) found.Add(field);
                }
            }
            Parts[type] = found;
            return found;
        }

        // Whether a field of this type can hold a scaler or a number, itself or in a struct or class of its own
        private static bool CanHold(Type type, int depth)
        {
            if (type == null || depth > 3 || type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
            if (typeof(AbstractScaler).IsAssignableFrom(type) || type == typeof(ScalableFloat) || type == typeof(ScalableInt)
                || typeof(ScalableFloat).IsAssignableFrom(type) || typeof(ScalableInt).IsAssignableFrom(type)) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (type.GetCustomAttribute<SerializableAttribute>() == null) return false;
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => CanHold(ElementOf(field.FieldType), depth + 1));
        }

        /// <summary>
        /// What a number reads that its field's context doesn't provide, as a sentence's end: "the target", "the caster
        /// and the target". Empty when it resolves.
        /// </summary>
        public static string Missing(NumberReads reads, NumberReads provides)
        {
            var missing = reads & ~provides;
            if (missing == NumberReads.None) return "";
            var names = new List<string>();
            foreach (NumberReads each in Enum.GetValues(typeof(NumberReads)))
            {
                if (each == NumberReads.None || each == NumberReads.All || (missing & each) == 0) continue;
                names.Add(each switch
                {
                    NumberReads.Level => "a level",
                    NumberReads.Caster => "the caster",
                    NumberReads.Target => "the target",
                    NumberReads.Caller => "the caller's magnitudes",
                    NumberReads.LocalData => "Local Data",
                    NumberReads.Carrier => "an effect",
                    NumberReads.Data => "an activation's data",
                    NumberReads.Owner => "its owner",
                    _ => each.ToString(),
                });
            }
            return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        }
    }
}
