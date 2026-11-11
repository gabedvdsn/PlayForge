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
    /// The limits a number field's constant keeps in the inspector (NUM-15), from its <see cref="NumberMinAttribute"/>,
    /// <see cref="NumberMaxAttribute"/> and <see cref="NumberRangeAttribute"/>, in what the field stores.
    /// </summary>
    public readonly struct NumberLimits
    {
        /// <summary>The least the constant takes, or none.</summary>
        public readonly float? Min;

        /// <summary>The most the constant takes, or none.</summary>
        public readonly float? Max;

        /// <summary>Whether the constant is set on a slider between the two, as a <see cref="NumberRangeAttribute"/> asks.</summary>
        public readonly bool Slider;

        public NumberLimits(float? min, float? max, bool slider)
        {
            Min = min;
            Max = max;
            Slider = slider && min.HasValue && max.HasValue;
        }

        /// <summary>Whether the field has a limit.</summary>
        public bool Any => Min.HasValue || Max.HasValue;

        /// <summary>A number, kept inside the limits.</summary>
        public float Clamp(float value)
        {
            if (Min.HasValue && value < Min.Value) value = Min.Value;
            if (Max.HasValue && value > Max.Value) value = Max.Value;
            return value;
        }

        /// <summary>The limits as a tooltip says them, as "From 0 to 100", times a scale a percent field shows them by.</summary>
        public string Text(float scale = 1f)
        {
            if (Min.HasValue && Max.HasValue) return $"From {ScalerFields.Number(Min.Value * scale)} to {ScalerFields.Number(Max.Value * scale)}";
            if (Min.HasValue) return $"At least {ScalerFields.Number(Min.Value * scale)}";
            return Max.HasValue ? $"At most {ScalerFields.Number(Max.Value * scale)}" : "";
        }
    }

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

        /// <summary>
        /// What a field's context provides where it doesn't say: everything but an owner, which only a field whose code
        /// gives it one has, and says so (NUM-9).
        /// </summary>
        public const NumberReads DefaultProvides = NumberReads.All & ~NumberReads.Owner;

        /// <summary>
        /// What a field's context provides: its <see cref="NumberContextAttribute"/>'s, or <see cref="DefaultProvides"/>
        /// without one.
        /// </summary>
        public static NumberReads ProvidesOf(FieldInfo field) =>
            field?.GetCustomAttribute<NumberContextAttribute>()?.Provides ?? DefaultProvides;

        /// <summary>
        /// What a number property's context provides, found through the managed references on its path: its own field's
        /// <see cref="NumberContextAttribute"/>'s where it has one; else, for a number inside another's scaler, what the
        /// outermost number's field provides, as it's read in that number's context, with an owner where a scaler or a
        /// condition reads it, as each reads its own numbers with itself as their owner (FEAT-29).
        /// </summary>
        public static NumberReads ProvidesOf(SerializedProperty property)
        {
            var field = FieldOf(property);
            if (field?.GetCustomAttribute<NumberContextAttribute>() is { } declared) return declared.Provides;
            var outermost = OutermostNumber(property);
            if (outermost == null || outermost.propertyPath == property.propertyPath) return ProvidesOf(field);
            var provides = ProvidesOf(FieldOf(outermost));
            FieldOf(OwnerNumber(property), out var holder);
            return IsScalerPart(holder) ? provides | NumberReads.Owner : provides;
        }

        // Whether an object of this type is a scaler or a condition, which reads its own numbers with itself as their owner
        private static bool IsScalerPart(Type type) =>
            type != null && (typeof(AbstractScaler).IsAssignableFrom(type) || typeof(GameplayCondition).IsAssignableFrom(type));

        /// <summary>A field's unit: its <see cref="NumberContextAttribute"/>'s, else its <see cref="ScalerOperationKeyword"/>'s.</summary>
        public static string UnitOf(FieldInfo field) =>
            field?.GetCustomAttribute<NumberContextAttribute>()?.Unit ?? field?.GetCustomAttribute<ScalerOperationKeyword>()?.Unit;

        /// <summary>
        /// The limits a number field's constant keeps (NUM-15): its <see cref="NumberRangeAttribute"/>'s, which sets it on
        /// a slider, narrowed by a <see cref="NumberMinAttribute"/> or a <see cref="NumberMaxAttribute"/> beside it. A
        /// whole-number field keeps the whole numbers inside them.
        /// </summary>
        public static NumberLimits LimitsOf(FieldInfo field, bool whole)
        {
            if (field == null) return default;
            var range = field.GetCustomAttribute<NumberRangeAttribute>();
            float? min = range?.Min, max = range?.Max;
            if (field.GetCustomAttribute<NumberMinAttribute>() is { } least) min = min.HasValue ? Mathf.Max(min.Value, least.Min) : least.Min;
            if (field.GetCustomAttribute<NumberMaxAttribute>() is { } most) max = max.HasValue ? Mathf.Min(max.Value, most.Max) : most.Max;
            if (whole)
            {
                if (min.HasValue) min = Mathf.Ceil(min.Value);
                if (max.HasValue) max = Mathf.Floor(max.Value);
            }
            if (min.HasValue && max.HasValue && max.Value < min.Value) max = min;
            return new NumberLimits(min, max, range != null);
        }

        /// <summary>
        /// What's wrong with a number field that has Unity's <c>[Min]</c> or <c>[Range]</c>, naming the field and what to
        /// use in its place (NUM-15); null for a field that isn't a number, or has neither. Unity gives such a field to
        /// that attribute's drawer, which draws "Use Range with float or int" where the number was.
        /// </summary>
        public static string UnityLimitOn(FieldInfo field)
        {
            if (field == null) return null;
            var type = ElementOf(field.FieldType);
            if (!typeof(ScalableFloat).IsAssignableFrom(type) && !typeof(ScalableInt).IsAssignableFrom(type)) return null;
            bool min = field.GetCustomAttribute<MinAttribute>() != null;
            bool range = field.GetCustomAttribute<RangeAttribute>() != null;
            if (!min && !range) return null;
            var has = min && range ? "[Min] and [Range]" : min ? "[Min]" : "[Range]";
            var use = min && range ? "[NumberMin] or [NumberRange]" : min ? "[NumberMin]" : "[NumberRange]";
            return $"{field.DeclaringType?.Name}.{field.Name} is a number field with Unity's {has}, which the inspector draws as " +
                   $"an error in place of the number. Use {use}.";
        }

        // As scripts load, the console names each saved number field that has Unity's [Min] or [Range] (NUM-15)
        [InitializeOnLoadMethod]
        private static void WarnOfUnityLimits()
        {
            var fields = new HashSet<FieldInfo>(TypeCache.GetFieldsWithAttribute<MinAttribute>());
            fields.UnionWith(TypeCache.GetFieldsWithAttribute<RangeAttribute>());
            foreach (var field in fields)
            {
                // A class Unity doesn't save is never drawn
                var holder = field.DeclaringType;
                if (holder == null || (!typeof(UnityEngine.Object).IsAssignableFrom(holder) && !holder.IsSerializable)) continue;
                var message = UnityLimitOn(field);
                if (message != null) ForgeLog.Warning(message);
            }
        }

        /// <summary>
        /// The field a property is, read along its path through the values of the managed references on it, so a number
        /// inside a scaler is found as its own class declares it; null where the path can't be followed.
        /// </summary>
        public static FieldInfo FieldOf(SerializedProperty property) => FieldOf(property, out _);

        /// <summary>The field a property is, with the type of the object that holds it, as its value is now.</summary>
        public static FieldInfo FieldOf(SerializedProperty property, out Type holder)
        {
            holder = null;
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
                holder = type;
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

        /// <summary>
        /// The number a property is or sits in, the outermost on its path: a kind's own field, in a scaler in a number,
        /// gives that number. Null where the path holds none.
        /// </summary>
        public static SerializedProperty OutermostNumber(SerializedProperty property)
        {
            if (property?.serializedObject == null) return null;
            var parts = property.propertyPath.Split('.');
            var walked = "";
            for (int i = 0; i < parts.Length; i++)
            {
                walked = i == 0 ? parts[i] : $"{walked}.{parts[i]}";
                if (parts[i] == "Array") continue;
                var at = property.serializedObject.FindProperty(walked);
                if (IsNumber(at)) return at;
            }
            return null;
        }

        /// <summary>
        /// The number whose owner a property's relative read reads (FEAT-29): the innermost number on its path, itself
        /// among them, that a scaler or a condition holds as its own field, as each reads those with itself as their
        /// owner; else the outermost number, as a Group's members and a Threshold's tiers are read in its context. Null
        /// where the path holds no number.
        /// </summary>
        public static SerializedProperty OwnerNumber(SerializedProperty property)
        {
            if (property?.serializedObject == null) return null;
            var numbers = new List<SerializedProperty>();
            var parts = property.propertyPath.Split('.');
            var walked = "";
            for (int i = 0; i < parts.Length; i++)
            {
                walked = i == 0 ? parts[i] : $"{walked}.{parts[i]}";
                if (parts[i] == "Array") continue;
                var at = property.serializedObject.FindProperty(walked);
                if (IsNumber(at)) numbers.Add(at);
            }
            if (numbers.Count == 0) return null;
            for (int i = numbers.Count - 1; i > 0; i--)
            {
                FieldOf(numbers[i], out var holder);
                if (IsScalerPart(holder)) return numbers[i];
            }
            return numbers[0];
        }

        // The object at a path, read off the asset itself: the value a field holds, or a list's element
        private static object ValueAt(object root, string path)
        {
            object value = root;
            if (string.IsNullOrEmpty(path)) return value;
            foreach (var part in path.Split('.'))
            {
                if (value == null) return null;
                if (part == "Array") continue;
                if (part.StartsWith("data["))
                {
                    if (!int.TryParse(part.Substring(5, part.Length - 6), out int index)) return null;
                    value = value is IList list && index >= 0 && index < list.Count ? list[index] : null;
                    continue;
                }
                value = Find(value.GetType(), part)?.GetValue(value);
            }
            return value;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Relative to a Field: the fields a number can read on the object that holds it (NUM-9)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The fields a relative number can read: the other number fields of the object that holds its number, plain or a
        /// number, that the inspector shows, in the order they're declared. The number's own field stays out, and so does
        /// any field that reads it back, straight or through another, so a loop can't be picked.
        /// </summary>
        /// <param name="property">The number, or a property inside it, as the kind's own Field.</param>
        public static List<string> SiblingChoices(SerializedProperty property)
        {
            var choices = new List<string>();
            var number = OwnerNumber(property);
            var own = FieldOf(number, out var holder);
            if (own == null || holder == null) return choices;
            int cut = number.propertyPath.LastIndexOf('.');
            var owner = ValueAt(number.serializedObject.targetObject, cut < 0 ? "" : number.propertyPath.Substring(0, cut));

            // A scaler's own fields, as its inspector draws them, and not those every scaler has, as its Max Level
            var chain = new List<Type>();
            for (var each = holder; each != null && each != typeof(object) && each != typeof(AbstractScaler) && each != typeof(NumberKind); each = each.BaseType)
                chain.Add(each);
            chain.Reverse();
            foreach (var level in chain)
            {
                foreach (var field in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.Name == own.Name || !RelativeToFieldKind.IsNumberField(field)) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (field.IsNotSerialized || field.GetCustomAttribute<HideInInspector>() != null) continue;
                    if (owner != null && ReadsBack(owner, holder, field, own.Name, 0)) continue;
                    if (!choices.Contains(field.Name)) choices.Add(field.Name);
                }
            }
            return choices;
        }

        /// <summary>
        /// The type of the object a property's relative read reads, which holds its <see cref="OwnerNumber"/>: a
        /// scaler or a condition for its own numbers, else the object that holds the outermost number.
        /// </summary>
        public static Type OwnerTypeOf(SerializedProperty property)
        {
            FieldOf(OwnerNumber(property), out var holder);
            return holder;
        }

        // Whether a field's number reads the named one, straight or through the fields it reads, on the same owner
        private static bool ReadsBack(object owner, Type holder, FieldInfo field, string name, int depth)
        {
            if (depth > MostDepth) return true;
            foreach (var relative in ScalersIn(field.GetValue(owner), Walk.SameOwner).OfType<RelativeToFieldKind>())
            {
                if (string.IsNullOrEmpty(relative.Field)) continue;
                if (relative.Field == name) return true;
                var next = Find(holder, relative.Field);
                if (next != null && next != field && ReadsBack(owner, holder, next, name, depth + 1)) return true;
            }
            return false;
        }

        /// <summary>How far <see cref="ScalersIn"/> walks into the numbers a value holds.</summary>
        public enum Walk
        {
            /// <summary>Into every number inside it, a scaler's and a condition's own among them.</summary>
            Everything,

            /// <summary>
            /// Into the numbers read with the same owner: not those a scaler or a condition holds as its own fields, which
            /// each reads with itself as their owner; a Threshold's tiers are read in its context, so they are walked.
            /// </summary>
            SameOwner,

            /// <summary>Into no number inside it: the scalers its own number reads, a Group's members among them.</summary>
            SameNumber,
        }

        /// <summary>Every scaler a number, a scaler or a condition holds, itself among them, and those inside them.</summary>
        public static List<AbstractScaler> ScalersIn(object value, Walk walk = Walk.Everything)
        {
            var found = new List<AbstractScaler>();
            Collect(value, 0, found, walk);
            return found;
        }

        private static void Collect(object value, int depth, List<AbstractScaler> found, Walk walk)
        {
            if (value == null || depth > MostDepth) return;
            switch (value)
            {
                case ScalableFloat number:
                    Collect(number.Scaler, depth + 1, found, walk);
                    return;
                case ScalableInt whole:
                    Collect(whole.Scaler, depth + 1, found, walk);
                    return;
                case AbstractScaler scaler:
                    if (found.Contains(scaler)) return;
                    found.Add(scaler);
                    break;
            }
            bool own = IsScalerPart(value.GetType());
            foreach (var field in PartsOf(value.GetType()))
            {
                var held = field.GetValue(value);
                if (held is IEnumerable many and not string)
                {
                    foreach (var each in many)
                        if (Walks(each, own, walk)) Collect(each, depth + 1, found, walk);
                }
                else if (Walks(held, own, walk)) Collect(held, depth + 1, found, walk);
            }
        }

        // Whether a walk goes into a value a field holds: a number only as the walk says
        private static bool Walks(object held, bool heldByScalerPart, Walk walk)
        {
            if (held is not ScalableFloat and not ScalableInt) return true;
            return walk switch
            {
                Walk.SameNumber => false,
                Walk.SameOwner => !heldByScalerPart,
                _ => true,
            };
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
        /// caster, a Conditional's condition, and what the scalers, conditions and numbers inside it read. A number a
        /// scaler or a condition holds as its own field reads its owner there, so what it reads of an owner isn't asked
        /// of the field (FEAT-29).
        /// </summary>
        public static NumberReads ReadsOf(AbstractScaler scaler) => ReadsOf(scaler, 0);

        /// <summary>What a condition reads of its context, as it's set now, with what the numbers and conditions inside it read.</summary>
        public static NumberReads ReadsOf(GameplayCondition condition) => ReadsOf(condition, 0);

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
                        _ => NeedsOf(scaler.GetType()),
                    };
                    return reads | PartsRead(scaler, depth);
                case GameplayCondition condition:
                    return condition.Reads | PartsRead(condition, depth);
                default:
                    return PartsRead(value, depth);
            }
        }

        // What the scalers, conditions and numbers a value holds read: its fields that hold one, a list of them, or a
        // struct holding one. A scaler's or a condition's own number reads itself as its owner
        private static NumberReads PartsRead(object value, int depth)
        {
            var reads = NumberReads.None;
            bool own = IsScalerPart(value.GetType());
            foreach (var field in PartsOf(value.GetType()))
            {
                var held = field.GetValue(value);
                if (held is IEnumerable many and not string)
                {
                    foreach (var each in many) reads |= ReadOfPart(each, own, depth);
                }
                else reads |= ReadOfPart(held, own, depth);
            }
            return reads;
        }

        private static NumberReads ReadOfPart(object held, bool heldByScalerPart, int depth)
        {
            var reads = ReadsOf(held, depth + 1);
            return heldByScalerPart && held is ScalableFloat or ScalableInt ? reads & ~NumberReads.Owner : reads;
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

        // Whether a field of this type can hold a scaler, a condition or a number, itself or in a struct or class of its own
        private static bool CanHold(Type type, int depth)
        {
            if (type == null || depth > 3 || type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
            if (typeof(AbstractScaler).IsAssignableFrom(type) || typeof(GameplayCondition).IsAssignableFrom(type)
                || type == typeof(ScalableFloat) || type == typeof(ScalableInt)
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
