using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What the scaler drawer and the magnitude fields share, in Decision 28's model and the canvas's scaler board
    /// (ED-33), reworked from the user's list (ED-51 to ED-54, Decision 36): the kinds a field takes, in the Kind menu's
    /// order; a kind's own fields, whatever their types, a scaler of the user's own among them; the parts of every scaler
    /// the kind reads: its values per level, Per Level or Spread Over Levels, following its asset's level provider, as a
    /// table with the curve beside it, and its behaviours; and a shared scaler's asset, with how many fields share it.
    /// Nothing here writes while it draws (ED-34): every write follows an edit, through the field's own SerializedObject,
    /// so it reaches each selected asset and one Undo takes it back (ED-12).
    /// </summary>
    internal static class ScalerFields
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Kinds
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The kinds a field takes, as the Kind menu lists them: By Level and Shared first, then the rest by name. A field's
        /// ScalerRootAssignment narrows them, as an attribute set's take only the cached kinds; a Scaler asset can't hold a
        /// Shared scaler, which would read itself.
        /// </summary>
        internal static List<Type> KindsFor(SerializedProperty scaler)
        {
            var assignment = scaler.GetAttribute<ScalerRootAssignment>();
            bool inAsset = scaler.serializedObject.targetObject is ScalerAsset;
            // A field declared as a narrower type, as a cached Group member's, takes its kinds alone: a kind it can't hold
            // was offered, and failed to set (ED-81)
            var declared = DeclaredTypeOf(scaler);
            return TypeCache.GetTypesDerivedFrom<AbstractScaler>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null)
                .Where(type => declared == null || declared.IsAssignableFrom(type))
                .Where(type => assignment?.RootType == null || assignment.RootType.IsAssignableFrom(type))
                .Where(type => assignment?.AvoidType == null || !assignment.AvoidType.IsAssignableFrom(type))
                .Where(type => !(inAsset && IsShared(type)))
                .OrderBy(Rank).ThenBy(KindName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // The type a managed reference field is declared as, from Unity's "Assembly Namespace.Type"; null where it can't tell
        private static Type DeclaredTypeOf(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference) return null;
            var name = property.managedReferenceFieldTypename;
            int space = string.IsNullOrEmpty(name) ? -1 : name.IndexOf(' ');
            return space < 0 ? null : Type.GetType($"{name.Substring(space + 1)}, {name.Substring(0, space)}");
        }

        private static int Rank(Type type) =>
            type == typeof(SimpleScaler) || type == typeof(CachedSimpleScaler) ? 0 : IsShared(type) ? 1 : 2;

        /// <summary>Whether a kind reads a Scaler asset: Shared, or the cached Shared an attribute set's elements take.</summary>
        internal static bool IsShared(Type type) => type == typeof(SharedScaler) || type == typeof(SharedCachedScaler);

        /// <summary>
        /// A kind's name in the Kind menu: the name its <see cref="NumberKindAttribute"/> gives, as By Level for a simple
        /// scaler and Shared for a Scaler asset's (FEAT-29); else its class's name, without "Cached" and "Scaler".
        /// </summary>
        internal static string KindName(Type type)
        {
            if (type == null) return "None";
            if (type == typeof(CachedSimpleScaler)) return "By Level";
            if (type == typeof(SharedCachedScaler)) return "Shared";
            if (NumberKindAttribute.Of(type) is { Name: { Length: > 0 } declared }) return declared;
            var name = type.Name;
            if (name.StartsWith("Cached")) name = name.Substring("Cached".Length);
            if (name.EndsWith("Scaler")) name = name.Substring(0, name.Length - "Scaler".Length);
            return name.Length > 0 ? ObjectNames.NicifyVariableName(name) : "Scaler";
        }

        /// <summary>
        /// Gives the field a new scaler of a kind, which maps levels exactly and starts with one value: for each selected
        /// asset, the value firstValue reads from that asset's own field. Each asset gets an object of its own, since one
        /// shared between assets would be one scaler in two places, and a field of that kind keeps its scaler (ED-12).
        /// Null takes the scaler away.
        /// </summary>
        internal static void SetKind(SerializedProperty scaler, Type type, Func<SerializedProperty, float> firstValue) =>
            EachAsset.Write(scaler, own =>
            {
                if (type != null && KindOf(own) == type) return;
                // A scaler's name stays, as a Scaler asset's name is its scaler's (ED-74)
                var kept = (own.managedReferenceValue as AbstractScaler)?.Name;
                var made = Make(type, firstValue(own));
                if (made != null && !string.IsNullOrEmpty(kept)) made.Name = kept;
                own.managedReferenceValue = made;
            });

        /// <summary>A field's kind: its scaler's type, or null for a field with none.</summary>
        internal static Type KindOf(SerializedProperty scaler) => scaler?.managedReferenceValue?.GetType();

        /// <summary>A new scaler of a kind, which maps levels exactly and starts with one value.</summary>
        internal static AbstractScaler Make(Type type, float firstValue)
        {
            if (type == null) return null;
            var made = (AbstractScaler)Activator.CreateInstance(type);
            // A scaler made here maps levels exactly (Decision 28)
            made.StretchOverLevels = false;
            made.LevelValues = new[] { firstValue };
            made.Scaling = AbstractScaler.CurveFor(made.LevelValues, made.Interpolation);
            return made;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Values
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A scaler's values per level: its own, or its Scaler asset's when it's shared; none for other kinds.</summary>
        internal static float[] ValuesOf(SerializedProperty scaler)
        {
            if (scaler == null || scaler.propertyType != SerializedPropertyType.ManagedReference) return Array.Empty<float>();
            if (IsShared(scaler.managedReferenceValue?.GetType()))
            {
                var sharedAsset = AssetOf(scaler.managedReferenceValue);
                return sharedAsset != null && sharedAsset.Scaler != null ? sharedAsset.Scaler.LevelValues ?? Array.Empty<float>() : Array.Empty<float>();
            }
            var values = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            if (values == null || !values.isArray) return Array.Empty<float>();
            var read = new float[values.arraySize];
            for (int i = 0; i < read.Length; i++) read[i] = values.GetArrayElementAtIndex(i).floatValue;
            return read;
        }

        /// <summary>Whether a scaler reads values per level: its own, as its kind's parts say, or its Scaler asset's.</summary>
        internal static bool ReadsValues(SerializedProperty scaler) => scaler?.managedReferenceValue is AbstractScaler kind
            && (IsShared(kind.GetType()) || (kind.Parts & EScalerParts.LevelValues) != 0);

        /// <summary>
        /// A series' range as a field reads it, as in "6–8 s", or "6 s" when every level reads the same. A range with a
        /// negative end reads "-25 to -10": a dash between two minus signs read as "-25—10" (ED-51).
        /// </summary>
        internal static string RangeText(IReadOnlyList<float> values, string unit)
        {
            if (values == null || values.Count == 0) return "";
            float min = values.Min(), max = values.Max();
            var suffix = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
            if (Mathf.Approximately(min, max)) return $"{Number(min)}{suffix}";
            return min < 0f || max < 0f ? $"{Number(min)} to {Number(max)}{suffix}" : $"{Number(min)}–{Number(max)}{suffix}";
        }

        internal static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// Writes the curve a stretched scaler reads, from its values, in the edit that changed them, so they undo as one
        /// step. Each selected asset's curve comes from its own values (ED-12). Called after an edit, never while drawing.
        /// </summary>
        internal static void KeepCurve(SerializedProperty scaler)
        {
            if (!Alive(scaler)) return;
            EachAsset.Write(scaler, own =>
            {
                var curve = own.FindPropertyRelative(nameof(AbstractScaler.Scaling));
                var interpolation = own.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
                if (curve == null || interpolation == null) return;
                var wanted = AbstractScaler.CurveFor(ValuesOf(own), (EScalerInterpolation)interpolation.intValue);
                if (!SameCurve(curve.animationCurveValue, wanted)) curve.animationCurveValue = wanted;
            });
        }

        private static bool SameCurve(AnimationCurve a, AnimationCurve b)
        {
            if (a == null || b == null || a.length != b.length) return false;
            for (int i = 0; i < a.length; i++)
            {
                if (!Mathf.Approximately(a[i].time, b[i].time) || !Mathf.Approximately(a[i].value, b[i].value)) return false;
            }
            return true;
        }

        /// <summary>Whether a property still names a field, as it may not once its scaler changes kind or an Undo removes it.</summary>
        internal static bool Alive(SerializedProperty property)
        {
            try
            {
                return property != null && property.serializedObject.targetObject != null &&
                       property.serializedObject.FindProperty(property.propertyPath) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the field has the focus, as it does while a value is typed or chosen in it.</summary>
        internal static bool IsTypedInto(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        // ═══════════════════════════════════════════════════════════════════════════
        // What a scaler shows
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The class a field takes while its kind's settings leave it out, as a Randomized scaler's Variance while it multiplies.</summary>
        internal const string HiddenClass = "pf-scaler__hidden";

        /// <summary>The class of the block a scaler's fields sit on, under the field they belong to (ED-51, Decision 36).</summary>
        internal const string BlockClass = "pf-scaler-block";

        /// <summary>The class of a scaler's content while its kind has nothing to show.</summary>
        internal const string EmptyClass = "pf-scaler--empty";

        /// <summary>
        /// A scaler's content: a Shared scaler's asset; otherwise the kind's own fields, then the parts of every scaler the
        /// kind reads (<see cref="AbstractScaler.Parts"/>): its values per level, with their curve, and its behaviours. It
        /// drew the values for every kind but those that hid them, so a Fixed Value showed values it never read, and a
        /// Randomized scaler hid the values its base comes from (ED-51).
        /// </summary>
        internal static VisualElement Content(SerializedProperty scaler)
        {
            var content = new VisualElement { name = "ScalerContent" };
            content.AddToClassList("pf-scaler");
            var value = scaler.managedReferenceValue as AbstractScaler;
            if (value == null) return content;

            if (IsShared(value.GetType()))
            {
                content.Add(SharedRows(scaler));
                return content;
            }

            int kindFields = 0;
            foreach (var field in KindFields(scaler))
            {
                content.Add(field);
                kindFields++;
            }

            // A Group whose members multiply, with none that adds, multiplies 0, and says so under its members (ED-80)
            HelpBox multipliesNothing = null;
            if (value is ScalerGroup or CachedScalerGroup)
            {
                multipliesNothing = new HelpBox(MultipliesNothingText, HelpBoxMessageType.Warning) { name = "MultipliesNothing" };
                content.Add(multipliesNothing);
            }
            void ShowNote() => multipliesNothing?.EnableInClassList(HiddenClass,
                !EachAsset.Of(scaler).Any(own => MultipliesNothing(own.managedReferenceValue as AbstractScaler)));

            var parts = new VisualElement { name = "Parts" };
            content.Add(parts);
            var shown = (EScalerParts)(-1);
            void ShowParts()
            {
                var now = PartsOf(scaler);
                if (now == shown) return;
                shown = now;
                // A kind with nothing to show, as Effect Stacks, draws no block of its own
                content.EnableInClassList(EmptyClass, kindFields == 0 && now == EScalerParts.None);
                parts.Clear();
                if ((now & EScalerParts.LevelValues) != 0) parts.Add(Values(scaler, (now & EScalerParts.LevelRange) != 0));
                var behaviours = scaler.FindPropertyRelative(nameof(AbstractScaler.Behaviours));
                if ((now & EScalerParts.Behaviours) != 0 && behaviours != null)
                {
                    var list = new PropertyField(behaviours, "Behaviours")
                    {
                        name = "Behaviours",
                        tooltip = "Rules that adjust the value the scaler works out, in order",
                    };
                    list.BindProperty(behaviours);
                    parts.Add(list);
                }
            }
            // A kind's parts can follow its fields, as a scaler of a project's own can read its values per level in one mode alone
            content.TrackSerializedObjectValue(scaler.serializedObject, _ =>
            {
                if (!Alive(scaler)) return;
                ShowParts();
                ShowNote();
            });
            ShowParts();
            ShowNote();
            return content;
        }

        /// <summary>What a Group whose members multiply nothing says under its members, and the Problems view after its field.</summary>
        internal const string MultipliesNothingText = "No member adds, so the members that multiply or divide work on 0.";

        /// <summary>
        /// Whether a Group's members multiply with none that adds, so they multiply the sum of nothing, 0 (ED-80). Members
        /// join as the sum of those that add, times each that multiplies, plus each flat bonus, in any order. Where a member
        /// overrides, and the Group doesn't set overriding members aside, it reads the override, and multiplies nothing.
        /// </summary>
        internal static bool MultipliesNothing(AbstractScaler scaler)
        {
            var members = scaler switch
            {
                ScalerGroup group => (group.Calculations ?? Array.Empty<MagnitudeModifierGroupMember>())
                    .Select(member => (Joins: member.RelativeOperation, Kind: member.Calculation)).ToList(),
                CachedScalerGroup cached => (cached.Calculations ?? Array.Empty<CachedMagnitudeModifierGroupMember>())
                    .Select(member => (Joins: member.RelativeOperation, Kind: (AbstractScaler)member.Calculation)).ToList(),
                _ => null,
            };
            if (members == null) return false;
            members.RemoveAll(member => AbstractScaler.IsNone(member.Kind));
            var policy = scaler is ScalerGroup own ? own.OverrideMemberCollisionPolicy : ((CachedScalerGroup)scaler).OverrideMemberCollisionPolicy;
            if (policy != EValueCollisionPolicy.Ignore && members.Any(member => member.Joins == EGroupOperation.Override)) return false;
            return members.Any(member => member.Joins is EGroupOperation.Multiply or EGroupOperation.Divide)
                && members.All(member => member.Joins != EGroupOperation.Add);
        }

        /// <summary>What the selected scalers read of every scaler's parts: what any of them reads (ED-12).</summary>
        internal static EScalerParts PartsOf(SerializedProperty scaler) => EachAsset.Of(scaler)
            .Select(each => (each.managedReferenceValue as AbstractScaler)?.Parts ?? EScalerParts.None)
            .Aggregate(EScalerParts.None, (all, each) => all | each);

        // The classes every scaler shares, whose fields a scaler's parts draw: a kind's own fields are those declared
        // below them, the user's own abstract bases among them
        private static readonly HashSet<Type> SharedBases = new() { typeof(AbstractScaler), typeof(AbstractCachedScaler), typeof(NumberKind) };

        /// <summary>
        /// The fields a kind adds, each as Unity draws its type, from the classes in its chain below the bases every scaler
        /// shares, a scaler of the user's own and its own abstract bases among them (ED-51). It stopped at the first
        /// abstract class, so a scaler built on a base of the user's own lost that base's fields. A field marked Hide in
        /// Inspector stays out, and one marked <see cref="ForgeShowWhenAttribute"/> shows only while the field it names
        /// holds a value that shows it, as a Randomized scaler's multipliers for its Multiplier mode.
        /// </summary>
        internal static IEnumerable<VisualElement> KindFields(SerializedProperty scaler)
        {
            var type = scaler.managedReferenceValue?.GetType();
            if (type == null) yield break;
            var chain = new List<Type>();
            for (var each = type; each != null && !SharedBases.Contains(each) && each != typeof(object); each = each.BaseType) chain.Add(each);
            chain.Reverse();
            var seen = new HashSet<string>();
            foreach (var level in chain)
            {
                foreach (var info in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (info.IsStatic || !seen.Add(info.Name)) continue;
                    if (!info.IsPublic && info.GetCustomAttribute<SerializeField>() == null && info.GetCustomAttribute<SerializeReference>() == null) continue;
                    if (info.GetCustomAttribute<HideInInspector>() != null) continue;
                    var property = scaler.FindPropertyRelative(info.Name);
                    if (property == null) continue;
                    var field = new PropertyField(property) { name = info.Name };
                    field.BindProperty(property);
                    // Every condition a field carries must hold for it to show (FEAT-29)
                    var when = info.GetCustomAttributes<ForgeShowWhenAttribute>().ToArray();
                    if (when.Length == 0)
                    {
                        yield return field;
                        continue;
                    }
                    // The field in a holder of its own, which hears the scaler's changes, as a bound field rebuilds its children
                    var holder = new VisualElement { name = $"{info.Name}Holder" };
                    holder.Add(field);
                    void Show() => field.EnableInClassList(HiddenClass,
                        !EachAsset.Of(scaler).Any(own => when.All(each => Holds(own.FindPropertyRelative(each.Field), each.Values))));
                    holder.TrackSerializedObjectValue(scaler.serializedObject, _ =>
                    {
                        if (Alive(scaler)) Show();
                    });
                    Show();
                    yield return holder;
                }
            }
        }

        // Whether a field holds one of the values given: an enum's or a whole number's value, or a bool's. A field that
        // isn't there shows what depends on it
        private static bool Holds(SerializedProperty property, object[] values)
        {
            if (property == null) return true;
            foreach (var value in values)
            {
                if (value == null) continue;
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        if (value is bool flag && property.boolValue == flag) return true;
                        break;
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Integer:
                        if (value is IConvertible && property.intValue == Convert.ToInt32(value, CultureInfo.InvariantCulture)) return true;
                        break;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The levels a scaler's asset has
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The levels an asset's scalers follow: from its first to its last, and whose they are, as "Fireball".</summary>
        internal readonly struct LevelRange : IEquatable<LevelRange>
        {
            public readonly int First;
            public readonly int Last;
            public readonly string From;

            public LevelRange(int first, int last, string from)
            {
                First = first;
                Last = last;
                From = from ?? "";
            }

            public bool Known => Last > 0;
            public string Text => First == Last ? $"{First}" : $"{First}–{Last}";
            public bool Equals(LevelRange other) => First == other.First && Last == other.Last && From == other.From;
            public override bool Equals(object obj) => obj is LevelRange other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(First, Last, From);
        }

        /// <summary>
        /// The levels the selected assets' scalers follow, when they're known and agree: an ability's, an item's or an
        /// entity's own, or those of the provider it links to, as a linked effect's are its ability's. An unlinked effect, an
        /// attribute set or a Scaler asset takes its levels from whatever uses it, so has none known (ED-53).
        /// </summary>
        internal static LevelRange LevelsOf(SerializedObject serialized)
        {
            LevelRange? found = null;
            foreach (var target in serialized.targetObjects)
            {
                var range = target is BaseForgeLevelProvider provider ? LevelsOf(provider) : default;
                if (found == null) found = range;
                else if (!found.Value.Equals(range)) return default;
            }
            return found ?? default;
        }

        private static LevelRange LevelsOf(BaseForgeLevelProvider provider)
        {
            // A link's provider's levels, through each link in a chain, as far as a chain can go before it loops
            for (int hop = 0; provider != null && provider.IsLinked && hop < 8; hop++) provider = provider.LinkedProvider;
            if (provider == null || provider is GameplayEffect) return default;
            int last = provider.GetMaxLevel();
            if (last <= 0) return default;
            return new LevelRange(Mathf.Clamp(provider.GetStartingLevel(), 1, last), last, ForgeImportWindow.NameOf(provider));
        }

        /// <summary>
        /// How many levels a scaler's table shows: its level provider's, while its values follow one level by level, or
        /// else the values it has (ED-53). Quick Fill fills as many.
        /// </summary>
        internal static int LevelsShown(SerializedProperty scaler)
        {
            var values = scaler?.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            int count = values != null && values.isArray ? values.arraySize : 0;
            var stretch = scaler?.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels));
            var config = scaler?.FindPropertyRelative(nameof(AbstractScaler.Configuration));
            if (stretch == null || config == null || stretch.boolValue) return count;
            var range = LevelsOf(scaler.serializedObject);
            return range.Known && (ELevelConfig)config.intValue == ELevelConfig.LockToLevelProvider ? range.Last : count;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Values per level
        // ═══════════════════════════════════════════════════════════════════════════

        private const int PerLevel = 0;
        private const int Spread = 1;

        /// <summary>
        /// A scaler's values per level: Values, Per Level or Spread Over Levels side by side, which the user couldn't tell
        /// apart as a Stretch Over Levels box (ED-54); the levels they follow, an asset's level provider's unless the
        /// scaler takes levels of its own (ED-53); the table, with the curve beside it; and, spread, how a level between
        /// two values reads. Each row follows the asset, and only edits write (ED-34).
        /// </summary>
        internal static VisualElement Values(SerializedProperty scaler, bool readsRange)
        {
            var root = new VisualElement { name = "Values" };
            var stretchProp = scaler.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels));
            var valuesProp = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            if (stretchProp == null || valuesProp == null) return root;

            var mode = new ForgeSegmentedField("Values",
                ("Per Level", "Each level reads its own value, and a level past the last reads the last"),
                ("Spread Over Levels", "The values spread over the level range, as points on a curve, and a level between two points reads between them"))
            {
                name = "ValuesMode",
            };
            mode.AddToClassList(ForgeSegmentedField.alignedFieldUssClassName);
            mode.RegisterValueChangedCallback(change =>
            {
                bool spread = change.newValue == Spread;
                EachAsset.Write(stretchProp, own =>
                {
                    if (own.boolValue != spread) own.boolValue = spread;
                });
                KeepCurve(scaler);
            });
            root.Add(mode);

            var rows = new VisualElement { name = "ValueRows" };
            root.Add(rows);
            string shape = null;
            Action update = null;
            void Refresh()
            {
                bool spread = EachAsset.Of(scaler).Any(own => own.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels))?.boolValue == true);
                mode.SetValueWithoutNotify(spread ? Spread : PerLevel);
                mode.showMixedValue = stretchProp.hasMultipleDifferentValues;
                var now = ShapeOf(scaler, readsRange);
                if (now != shape)
                {
                    shape = now;
                    rows.Clear();
                    update = spread ? SpreadRows(scaler, rows, readsRange) : PerLevelRows(scaler, rows);
                }
                update?.Invoke();
            }
            root.TrackSerializedObjectValue(scaler.serializedObject, _ =>
            {
                if (Alive(stretchProp) && Alive(valuesProp)) Refresh();
            });
            Refresh();
            return root;
        }

        // What the value rows are built from: the mode, the level settings, how many values, and the asset's levels. A
        // change to any of them builds the rows again; a change of a value only updates them
        private static string ShapeOf(SerializedProperty scaler, bool readsRange)
        {
            var parts = EachAsset.Of(scaler).Select(own => string.Join(",",
                own.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels))?.boolValue,
                own.FindPropertyRelative(nameof(AbstractScaler.Configuration))?.intValue,
                own.FindPropertyRelative(nameof(AbstractScaler.MaxLevel))?.intValue,
                own.FindPropertyRelative(nameof(AbstractScaler.LevelValues))?.arraySize));
            var range = LevelsOf(scaler.serializedObject);
            return $"{string.Join("|", parts)}|{range.First}-{range.Last}-{range.From}|{readsRange}";
        }

        // Per Level: the levels the values follow, then a value for each level and the curve
        private static Action PerLevelRows(SerializedProperty scaler, VisualElement rows)
        {
            var valuesProp = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            var configProp = scaler.FindPropertyRelative(nameof(AbstractScaler.Configuration));
            var range = LevelsOf(scaler.serializedObject);
            bool follows = range.Known && (ELevelConfig)configProp.intValue == ELevelConfig.LockToLevelProvider;

            var levels = new VisualElement { name = "Levels" };
            levels.AddToClassList("pf-scaler__row");
            if (range.Known)
            {
                var choices = new List<string> { $"{range.From}'s levels ({range.Text})", "Own levels" };
                var source = new PopupField<string>("Levels", choices, follows ? 0 : 1)
                {
                    name = "LevelSource",
                    tooltip = $"Whether the scaler has a value for each of {range.From}'s levels, or levels of its own",
                };
                source.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                source.showMixedValue = configProp.hasMultipleDifferentValues;
                source.RegisterValueChangedCallback(change =>
                {
                    var chosen = change.newValue == choices[0] ? ELevelConfig.LockToLevelProvider : ELevelConfig.Unlocked;
                    EachAsset.Write(configProp, own =>
                    {
                        if (own.intValue != (int)chosen) own.intValue = (int)chosen;
                    });
                });
                levels.Add(source);
            }
            IntegerField count = null;
            if (!follows)
            {
                count = new IntegerField(range.Known ? null : "Levels")
                {
                    name = "LevelCount",
                    tooltip = "How many levels have a value; a level past the end reads the last",
                    isDelayed = true,
                };
                if (!range.Known) count.AddToClassList(IntegerField.alignedFieldUssClassName);
                count.AddToClassList("pf-scaler__row-number");
                count.RegisterValueChangedCallback(change => Resize(scaler, valuesProp, change.newValue));
                levels.Add(count);
            }
            rows.Add(levels);

            int stored = valuesProp.arraySize;
            int columns = follows ? range.Last : stored;
            var table = Table(scaler, valuesProp, columns, "Level", index => (index + 1).ToString(CultureInfo.InvariantCulture),
                out var past, out var preview);
            rows.Add(table);
            if (follows && stored > range.Last)
            {
                var unread = new Label($"{range.From} has {Levels(range.Last)}, so the values past level {range.Last} are never read.")
                {
                    name = "PastLevels",
                };
                unread.AddToClassList("pf-note");
                rows.Add(unread);
            }

            return () =>
            {
                if (count != null)
                {
                    count.SetValueWithoutNotify(valuesProp.arraySize);
                    count.showMixedValue = EachAsset.Differ(valuesProp, each => each.arraySize);
                }
                var values = ValuesOf(scaler);
                float last = values.Length > 0 ? values[values.Length - 1] : 1f;
                foreach (var (index, field) in past) field.SetValueWithoutNotify(last);
                var read = Enumerable.Range(0, columns).Select(i => i < values.Length ? values[i] : last).ToArray();
                ShowCurve(scaler, preview, read, null);
            };
        }

        // Spread Over Levels: the levels the values spread over, how many values, then each value at the level it sits at,
        // the curve, and how a level between two values reads
        private static Action SpreadRows(SerializedProperty scaler, VisualElement rows, bool readsRange)
        {
            var valuesProp = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            var configProp = scaler.FindPropertyRelative(nameof(AbstractScaler.Configuration));
            var maxProp = scaler.FindPropertyRelative(nameof(AbstractScaler.MaxLevel));
            var betweenProp = scaler.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
            var range = LevelsOf(scaler.serializedObject);
            var config = readsRange ? (ELevelConfig)configProp.intValue : ELevelConfig.LockToLevelProvider;
            var whose = range.Known ? $"{range.From}'s" : "The provider's";

            var over = new VisualElement { name = "Over" };
            over.AddToClassList("pf-scaler__row");
            if (readsRange)
            {
                var choices = new List<string>
                {
                    range.Known ? $"{whose} levels ({range.Text})" : $"{whose} levels",
                    "Own levels",
                    $"{whose}, up to a level",
                };
                var configs = new[] { ELevelConfig.LockToLevelProvider, ELevelConfig.Unlocked, ELevelConfig.Clamped };
                var source = new PopupField<string>("Over Levels", choices, Mathf.Max(0, Array.IndexOf(configs, config)))
                {
                    name = "LevelSource",
                    tooltip = "The levels the values spread over: the level provider's, levels of the scaler's own, or the provider's up to a level",
                };
                source.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                source.showMixedValue = configProp.hasMultipleDifferentValues;
                source.RegisterValueChangedCallback(change =>
                {
                    var chosen = configs[Mathf.Max(0, choices.IndexOf(change.newValue))];
                    EachAsset.Write(configProp, own =>
                    {
                        if (own.intValue != (int)chosen) own.intValue = (int)chosen;
                    });
                });
                over.Add(source);
                if (config != ELevelConfig.LockToLevelProvider)
                {
                    var max = new IntegerField { name = "MaxLevel", tooltip = "The last level the values spread to", isDelayed = true };
                    max.AddToClassList("pf-scaler__row-number");
                    max.BindProperty(maxProp);
                    over.Add(max);
                }
            }
            else
            {
                // The kind spreads its values over the provider's levels, whatever it's given
                var fixedRange = new TextField("Over Levels")
                {
                    name = "LevelSource",
                    isReadOnly = true,
                    value = range.Known ? $"{whose} levels ({range.Text})" : $"{whose} levels",
                };
                fixedRange.AddToClassList(TextField.alignedFieldUssClassName);
                fixedRange.AddToClassList("pf-value-field");
                over.Add(fixedRange);
            }
            rows.Add(over);

            var points = new IntegerField("Points")
            {
                name = "PointCount",
                tooltip = "How many values the curve passes through, evenly from the first level to the last",
                isDelayed = true,
            };
            points.AddToClassList(IntegerField.alignedFieldUssClassName);
            points.RegisterValueChangedCallback(change => Resize(scaler, valuesProp, change.newValue));
            rows.Add(points);

            // The points, numbered: they sit evenly along the levels, mostly between two, which a level such as 1.8 named
            // as if it were one (ED-62)
            int stored = valuesProp.arraySize;
            var table = Table(scaler, valuesProp, stored, "Point", index => (index + 1).ToString(CultureInfo.InvariantCulture),
                out _, out var preview, index => $"Point {index + 1} of {stored}");
            rows.Add(table);

            var between = new EnumField("Between Points")
            {
                name = "BetweenLevels",
                tooltip = "How a level between two values reads: the earlier value, a straight line, or a smooth curve",
            };
            between.AddToClassList(EnumField.alignedFieldUssClassName);
            between.BindProperty(betweenProp);
            between.RegisterValueChangedCallback(_ =>
            {
                if (IsTypedInto(between)) KeepCurve(scaler);
            });
            rows.Add(between);

            // What each real level reads, as the runtime reads it, while the levels are known (ED-62). The provider's last
            // level ends a range up to a level that it falls short of (GAS-64)
            var (first, last) = RealLevels(range, config, maxProp.intValue);
            int providerLast = range.Known ? range.Last : maxProp.intValue;
            Label[] reads = null;
            HelpBox outOfDate = null;
            if (last >= first)
            {
                outOfDate = new HelpBox("Its stored curve doesn't follow its points, so each level reads the stored curve.",
                    HelpBoxMessageType.Warning) { name = "CurveOutOfDate" };
                outOfDate.AddToClassList(HiddenClass);
                rows.Add(outOfDate);
                rows.Add(ReadsTable(first, last, out reads));
            }

            void ShowReads()
            {
                if (reads == null) return;
                // Selected scalers whose values differ read differently at each level, so their cells show a mixed value
                bool same = !EachAsset.Differ(scaler, each => string.Join(",", ValuesOf(each)) + "|" + CurveText(each));
                var curve = scaler.FindPropertyRelative(nameof(AbstractScaler.Scaling))?.animationCurveValue;
                var read = same && curve != null
                    ? ReadsAtLevels(curve, first, last, config, maxProp.intValue, providerLast)
                    : null;
                for (int i = 0; i < reads.Length; i++)
                {
                    reads[i].text = read == null ? "\u2014" : Number(read[i]);
                    reads[i].tooltip = read == null ? "The selected scalers read differently" : $"Level {first + i} reads {Number(read[i])}";
                }
                // Stale only where a level reads other than its points would have it read: two curves whose keys differ
                // can read the same at every level
                var wanted = AbstractScaler.CurveFor(ValuesOf(scaler), (EScalerInterpolation)betweenProp.intValue);
                var wantedReads = read == null ? null : ReadsAtLevels(wanted, first, last, config, maxProp.intValue, providerLast);
                bool stale = read != null && read.Where((value, i) => Mathf.Abs(value - wantedReads[i]) > 0.0001f * Mathf.Max(1f, Mathf.Abs(value))).Any();
                outOfDate?.EnableInClassList(HiddenClass, !stale);
            }

            return () =>
            {
                points.SetValueWithoutNotify(valuesProp.arraySize);
                points.showMixedValue = EachAsset.Differ(valuesProp, each => each.arraySize);
                ShowCurve(scaler, preview, ValuesOf(scaler), (EScalerInterpolation)betweenProp.intValue);
                ShowReads();
            };
        }

        /// <summary>
        /// The real levels a spread scaler's values are read at: its level provider's, from its starting level to its last,
        /// while they're known; or, while the scaler takes levels of its own, from 1 to its own last level. None while the
        /// levels come from whatever uses the scaler.
        /// </summary>
        internal static (int First, int Last) RealLevels(LevelRange range, ELevelConfig config, int ownMax)
        {
            int last = range.Known ? range.Last : config != ELevelConfig.LockToLevelProvider ? ownMax : 0;
            int first = range.Known ? range.First : 1;
            last = Mathf.Min(last, first + MostReads - 1);
            return (first, last);
        }

        // The most levels the reads show, from the first
        private const int MostReads = 100;

        /// <summary>
        /// What a spread scaler reads at each level from the first to the last, as the runtime reads it: its stored curve at
        /// the level's place between the starting level and the last its levels reach, which is the provider's, its own,
        /// or, up to a level, its own or the provider's, whichever is lower (AbstractScaler.GetEffectiveRelativeLevel,
        /// GAS-64).
        /// </summary>
        internal static float[] ReadsAtLevels(AnimationCurve curve, int first, int last, ELevelConfig config, int ownMax, int providerLast)
        {
            var reads = new float[Mathf.Max(0, last - first + 1)];
            for (int level = first; level <= last; level++)
            {
                int max = config switch
                {
                    ELevelConfig.Unlocked => ownMax,
                    ELevelConfig.Clamped => Mathf.Min(ownMax, providerLast),
                    _ => providerLast,
                };
                float place = Mathf.Clamp01(Mathf.InverseLerp(first, max, level));
                reads[level - first] = curve.Evaluate(place);
            }
            return reads;
        }

        // A curve's keys as text, so selected scalers can be told apart by the curve they read
        private static string CurveText(SerializedProperty scaler)
        {
            var curve = scaler.FindPropertyRelative(nameof(AbstractScaler.Scaling))?.animationCurveValue;
            return curve == null ? "" : string.Join(",", curve.keys.Select(key => $"{key.time}:{key.value}"));
        }

        /// <summary>
        /// The levels and what each reads, as the table draws its values, read-only: a row of levels over a row of what
        /// each reads, ten to a row (ED-62).
        /// </summary>
        internal static VisualElement ReadsTable(int first, int last, out Label[] cells)
        {
            var root = new VisualElement { name = "LevelReads" };
            root.AddToClassList("pf-scaler-table");
            root.AddToClassList("pf-scaler-table--reads");
            var grid = new VisualElement { name = "Grid" };
            root.Add(grid);
            int count = Mathf.Max(0, last - first + 1);
            cells = new Label[count];
            for (int start = 0; start < count; start += 10)
            {
                var heads = new VisualElement();
                heads.AddToClassList("pf-scaler-table__levels");
                var values = new VisualElement();
                values.AddToClassList("pf-scaler-table__values");
                var levelHead = new Label("Level");
                levelHead.AddToClassList("pf-scaler-table__head");
                heads.Add(levelHead);
                var readsHead = new Label("Reads");
                readsHead.AddToClassList("pf-scaler-table__head");
                values.Add(readsHead);
                for (int i = start; i < Mathf.Min(start + 10, count); i++)
                {
                    var level = new Label((first + i).ToString(CultureInfo.InvariantCulture)) { name = $"ReadsAt{first + i}" };
                    level.AddToClassList("pf-scaler-table__cell");
                    heads.Add(level);
                    var read = new Label { name = $"Reads{first + i}" };
                    read.AddToClassList("pf-scaler-table__cell");
                    read.AddToClassList("pf-scaler-table__read");
                    values.Add(read);
                    cells[i] = read;
                }
                grid.Add(heads);
                grid.Add(values);
            }
            return root;
        }

        /// <summary>
        /// The values as the scaler board's table: a row of heads over a row of values, ten to a row, inside an outline,
        /// with the curve beside them. A column past the values the scaler has shows the last, which a level past the end
        /// reads; typing there gives that level, and each before it, a value of its own.
        /// </summary>
        internal static VisualElement Table(SerializedProperty scaler, SerializedProperty valuesProp, int columns, string head,
            Func<int, string> headOf, out List<(int Index, FloatField Field)> past, out ForgeCurvePreview preview,
            Func<int, string> tipOf = null)
        {
            var root = new VisualElement { name = "LevelTable" };
            root.AddToClassList("pf-scaler-table");
            var grid = new VisualElement { name = "Grid" };
            root.Add(grid);
            preview = new ForgeCurvePreview { name = "Curve" };
            root.Add(preview);
            past = new List<(int, FloatField)>();

            int stored = valuesProp.arraySize;
            for (int start = 0; start < columns; start += 10)
            {
                var heads = new VisualElement();
                heads.AddToClassList("pf-scaler-table__levels");
                var numbers = new VisualElement();
                numbers.AddToClassList("pf-scaler-table__values");
                var headLabel = new Label(head);
                headLabel.AddToClassList("pf-scaler-table__head");
                heads.Add(headLabel);
                var valuesLabel = new Label("Value");
                valuesLabel.AddToClassList("pf-scaler-table__head");
                numbers.Add(valuesLabel);
                for (int i = start; i < Mathf.Min(start + 10, columns); i++)
                {
                    var at = new Label(headOf(i)) { name = $"At{i + 1}" };
                    at.AddToClassList("pf-scaler-table__cell");
                    heads.Add(at);
                    var field = new FloatField { name = $"Level{i + 1}" };
                    field.AddToClassList("pf-scaler-table__cell");
                    if (i < stored)
                    {
                        field.tooltip = tipOf?.Invoke(i) ?? $"The value at level {i + 1}";
                        field.BindProperty(valuesProp.GetArrayElementAtIndex(i));
                        // The curve a spread scaler reads follows a typed value, in the same Undo step. Binding sends the
                        // field change events of its own as it draws, which write nothing (ED-34)
                        field.RegisterValueChangedCallback(_ =>
                        {
                            if (IsTypedInto(field)) KeepCurve(scaler);
                        });
                    }
                    else
                    {
                        int index = i;
                        field.tooltip = $"Level {i + 1} reads level {Mathf.Max(1, stored)}'s value; type one to give it its own";
                        field.isDelayed = true;
                        field.AddToClassList("pf-scaler-table__past");
                        field.RegisterValueChangedCallback(change => WriteAt(scaler, valuesProp, index, change.newValue));
                        past.Add((i, field));
                    }
                    numbers.Add(field);
                }
                grid.Add(heads);
                grid.Add(numbers);
            }
            return root;
        }

        // Gives a level past the values its own value: each level before it takes the last value, which it read
        private static void WriteAt(SerializedProperty scaler, SerializedProperty valuesProp, int index, float value)
        {
            if (!Alive(valuesProp)) return;
            EachAsset.Write(valuesProp, own =>
            {
                int was = own.arraySize;
                float last = was > 0 ? own.GetArrayElementAtIndex(was - 1).floatValue : 1f;
                if (index >= was)
                {
                    own.arraySize = index + 1;
                    for (int i = was; i < index; i++) own.GetArrayElementAtIndex(i).floatValue = last;
                }
                own.GetArrayElementAtIndex(index).floatValue = value;
            });
            KeepCurve(scaler);
        }

        // The curve beside the table, while the selected scalers agree on their values (ED-12)
        private static void ShowCurve(SerializedProperty scaler, ForgeCurvePreview preview, float[] values, EScalerInterpolation? between)
        {
            bool same = !EachAsset.Differ(scaler, each => string.Join(",", ValuesOf(each)));
            preview.EnableInClassList(HiddenClass, !same);
            if (same) preview.Show(values, between: between);
        }

        private static string Levels(int count) => count == 1 ? "1 level" : $"{count} levels";

        // Sets how many values a scaler has: a new one takes the last value, as a level past the end reads it. Each
        // selected table takes its own last value, and keeps the values it has (ED-12)
        private static void Resize(SerializedProperty scaler, SerializedProperty values, int levels)
        {
            if (!Alive(values)) return;
            levels = Mathf.Clamp(levels, 1, 100);
            EachAsset.Write(values, own =>
            {
                int was = own.arraySize;
                if (levels == was) return;
                float last = was > 0 ? own.GetArrayElementAtIndex(was - 1).floatValue : 1f;
                own.arraySize = levels;
                for (int i = was; i < levels; i++) own.GetArrayElementAtIndex(i).floatValue = last;
            });
            KeepCurve(scaler);
        }

        /// <summary>
        /// A Shared scaler's asset, with Open, then the asset's curve, its range and how many fields share it, as the
        /// scaler board's second step draws them.
        /// </summary>
        internal static VisualElement SharedRows(SerializedProperty scaler)
        {
            var root = new VisualElement { name = "Shared" };
            var assetProp = scaler.FindPropertyRelative(nameof(SharedScaler.Asset));
            var row = new VisualElement();
            row.AddToClassList("pf-reference");
            var asset = new ObjectField("Asset")
            {
                name = "ScalerAsset",
                objectType = typeof(ScalerAsset),
                allowSceneObjects = false,
                tooltip = "The Scaler asset this field reads",
            };
            asset.AddToClassList(ObjectField.alignedFieldUssClassName);
            asset.BindProperty(assetProp);
            row.Add(asset);
            var open = new Button(() =>
            {
                if (assetProp.objectReferenceValue != null) EditorUtility.OpenPropertyEditor(assetProp.objectReferenceValue);
            })
            {
                name = "Open",
                text = "Open",
                tooltip = "Shows the Scaler asset in a window of its own",
            };
            open.AddToClassList("pf-reference__action");
            row.Add(open);
            root.Add(row);

            var about = new VisualElement { name = "SharedAbout" };
            about.AddToClassList("pf-scaler-shared");
            var preview = new ForgeCurvePreview { name = "Curve" };
            about.Add(preview);
            var text = new VisualElement();
            text.AddToClassList("pf-scaler-shared__text");
            var range = new Label { name = "SharedRange" };
            range.AddToClassList("pf-scaler-shared__range");
            text.Add(range);
            var uses = new Label { name = "SharedUses" };
            uses.AddToClassList("pf-scaler-shared__uses");
            text.Add(uses);
            about.Add(text);
            root.Add(about);

            void Show()
            {
                // Selected fields that share different assets show the asset as mixed, and no one curve or count (ED-12)
                var shared = assetProp.hasMultipleDifferentValues ? null : assetProp.objectReferenceValue as ScalerAsset;
                open.SetEnabled(shared != null);
                about.style.display = shared != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (shared == null) return;
                var values = shared.Scaler?.LevelValues ?? Array.Empty<float>();
                preview.Show(values);
                range.text = values.Length == 0 ? "No values" : $"{RangeText(values, null)}, levels 1–{values.Length}";
                int count = UsesOf(shared).Count;
                uses.text = count == 1 ? "Used by 1 field" : $"Shared by {count} fields";
            }
            root.TrackSerializedObjectValue(scaler.serializedObject, _ =>
            {
                if (Alive(assetProp)) Show();
            });
            Show();
            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Who shares a Scaler asset
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>One field that shares a Scaler asset: the asset it's in, its path there, and its name.</summary>
        internal readonly struct Use
        {
            public readonly Object Asset;
            public readonly string Path;
            public readonly string Label;

            public Use(Object asset, string path, string label)
            {
                Asset = asset;
                Path = path;
                Label = label;
            }
        }

        private static readonly Dictionary<ScalerAsset, List<Use>> UsesCache = new();
        private static bool cacheHooked;

        /// <summary>
        /// The fields across the project's PlayForge assets that share a Scaler asset, found among the assets that refer to
        /// it. The list is kept until the project changes.
        /// </summary>
        internal static List<Use> UsesOf(ScalerAsset asset)
        {
            if (asset == null) return new List<Use>();
            if (!cacheHooked)
            {
                cacheHooked = true;
                EditorApplication.projectChanged += UsesCache.Clear;
                Undo.undoRedoPerformed += UsesCache.Clear;
            }
            if (UsesCache.TryGetValue(asset, out var cached)) return cached;

            var found = new List<Use>();
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var candidate in AssetDatabase.FindAssets($"t:{nameof(BaseForgeAsset)}").Select(AssetDatabase.GUIDToAssetPath).Distinct())
                {
                    if (candidate == path || !AssetDatabase.GetDependencies(candidate, false).Contains(path)) continue;
                    var user = AssetDatabase.LoadAssetAtPath<ScriptableObject>(candidate);
                    if (user == null) continue;
                    var iterator = new SerializedObject(user).GetIterator();
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                        if (AssetOf(iterator.managedReferenceValue) == asset)
                            found.Add(new Use(user, iterator.propertyPath, FieldLabel(iterator.propertyPath)));
                    }
                }
            }
            UsesCache[asset] = found;
            return found;
        }

        /// <summary>A path as a row reads it: its magnitude's field, as "Duration Specification › Duration".</summary>
        internal static string FieldLabel(string path)
        {
            var parts = path.Replace(".Array.data[", "[").Split('.')
                .Where(part => part != nameof(ScalableFloat.Scaler))
                .Select(part => ObjectNames.NicifyVariableName(part.EndsWith("Operation") ? part.Substring(0, part.Length - "Operation".Length) : part));
            return string.Join(" › ", parts);
        }

        /// <summary>The Scaler asset a shared scaler reads, or null for any other value.</summary>
        internal static ScalerAsset AssetOf(object value) => value switch
        {
            SharedScaler shared => shared.Asset,
            SharedCachedScaler cached => cached.Asset,
            _ => null,
        };

        /// <summary>Forgets who shares what, as after an edit that changes it.</summary>
        internal static void ForgetUses() => UsesCache.Clear();
    }
}
