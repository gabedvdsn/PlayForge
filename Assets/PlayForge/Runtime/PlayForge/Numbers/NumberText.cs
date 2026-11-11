using System;
using System.Globalization;
using System.Text;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A number's one-line summary, as its field shows it under its row (FEAT-29): "0.3 × Target's Health (base)", "Fire
    /// Duration + 2", "the caller's Data.Damage". A kind of your own gives its part through <see cref="NumberKind.Describe"/>.
    /// A field whose unit is "%" reads its numbers as percentages: "30% × Target's Health (base)".
    /// </summary>
    public static class NumberText
    {
        /// <summary>A number's summary, in a unit, as its field shows it.</summary>
        public static string Of(ScalableFloat number, string unit = null) =>
            number == null ? "" : Of(number.Magnitude, number.RealMagnitude, number.Scaler, unit);

        /// <summary>A whole number's summary, as its field shows it.</summary>
        public static string Of(ScalableInt number, string unit = null) =>
            number == null ? "" : Of(number.Magnitude, number.RealMagnitude, number.Scaler, unit);

        /// <summary>A summary from a number's parts: its number, how it combines, and its kind.</summary>
        public static string Of(float magnitude, EMagnitudeOperation combine, AbstractScaler kind, string unit = null)
        {
            bool percent = unit == "%";
            string suffix = percent || string.IsNullOrEmpty(unit) ? "" : $" {unit}";
            string number = Number(magnitude, percent);
            // A kind that is none, as a Group with no members, reads as no kind: the number (ED-80)
            if (AbstractScaler.IsNone(kind) || combine == EMagnitudeOperation.UseMagnitude)
                return kind == null && combine is EMagnitudeOperation.UseScaler or EMagnitudeOperation.MultiplyWithScaler
                    ? $"{Number(0f, percent)}{suffix}"
                    : $"{number}{suffix}";

            string part = Describe(kind, percent);
            return combine switch
            {
                EMagnitudeOperation.UseScaler => $"{part}{suffix}",
                EMagnitudeOperation.MultiplyWithScaler => Approximately(magnitude, 1f) && !percent ? $"{part}{suffix}" : $"{number} × {part}{suffix}",
                EMagnitudeOperation.AddScaler => Approximately(magnitude, 0f) ? $"{part}{suffix}"
                    : magnitude < 0f ? $"{part} − {Number(-magnitude, percent)}{suffix}" : $"{part} + {number}{suffix}",
                _ => $"{number}{suffix}",
            };
        }

        /// <summary>
        /// A kind's part of a summary: a kind of your own's <see cref="NumberKind.Describe"/>, a scaler's values or what it
        /// reads, or else its name.
        /// </summary>
        public static string Describe(AbstractScaler kind, bool percent = false)
        {
            switch (kind)
            {
                case null:
                    return "";
                case NumberKind own:
                    var described = own.Describe();
                    return string.IsNullOrEmpty(described) ? KindName(kind.GetType()) : described;
                case SharedScaler shared:
                    return shared.Asset != null ? shared.Asset.DisplayName : "no Scaler asset";
                case ConstantScaler constant:
                    return Number(constant.Value, percent);
                case AttributeBackedScaler attribute:
                    var side = attribute.CaptureFrom == ESourceTarget.Target ? "Target's" : "Caster's";
                    var name = attribute.CaptureAttribute != null ? attribute.CaptureAttribute.GetName() : "attribute";
                    return attribute.CaptureWhat == EAttributeTargetBinary.Base ? $"{side} {name} (base)" : $"{side} {name}";
                case EffectStacksScaler:
                    return "the effect's stacks";
                case ItemStacksScaler:
                    return "the item's stacks";
                case SimpleScaler simple:
                    return simple.LevelValues is { Length: > 1 }
                        ? $"{Series(simple.LevelValues, percent)} by level"
                        : $"{Series(simple.LevelValues, percent)} at every level";
                default:
                    return $"a {KindName(kind.GetType())}";
            }
        }

        /// <summary>
        /// A kind's name, as the kind menu shows it: its <see cref="NumberKindAttribute.Name"/>, or else its class's name
        /// without "Cached" and "Scaler", spaced out.
        /// </summary>
        public static string KindName(Type type)
        {
            if (type == null) return "None";
            if (type == typeof(CachedSimpleScaler)) return "By Level";
            if (type == typeof(SharedCachedScaler)) return "Shared";
            var declared = NumberKindAttribute.Of(type);
            if (declared != null && !string.IsNullOrEmpty(declared.Name)) return declared.Name;
            var name = type.Name;
            if (name.StartsWith("Cached")) name = name.Substring("Cached".Length);
            if (name.EndsWith("Scaler")) name = name.Substring(0, name.Length - "Scaler".Length);
            return name.Length > 0 ? Spaced(name) : "Scaler";
        }

        /// <summary>
        /// A condition's one line, as a Conditional scaler's summary reads it: "Target's Health &lt; 0.3 × Target's Health
        /// (base)". A condition of your own gives its line through <see cref="GameplayCondition.Describe"/>, or else it
        /// reads as its name.
        /// </summary>
        public static string Of(GameplayCondition condition)
        {
            if (condition == null) return "no condition";
            var described = condition.Describe();
            return string.IsNullOrEmpty(described) ? ConditionName(condition.GetType()) : described;
        }

        /// <summary>
        /// A condition's name, as the condition menu shows it: its <see cref="GameplayConditionAttribute.Name"/>, or else
        /// its class's name without "Condition", spaced out.
        /// </summary>
        public static string ConditionName(Type type)
        {
            if (type == null) return "None";
            var declared = GameplayConditionAttribute.Of(type);
            if (declared != null && !string.IsNullOrEmpty(declared.Name)) return declared.Name;
            var name = type.Name;
            if (name.EndsWith("Condition")) name = name.Substring(0, name.Length - "Condition".Length);
            return name.Length > 0 ? Spaced(name) : "Condition";
        }

        /// <summary>
        /// What a number reads, by name, as a sentence ends: "the target", "the caster and the target", "a level". Null
        /// for none (FEAT-32).
        /// </summary>
        public static string Reads(NumberReads reads)
        {
            if (reads == NumberReads.None) return null;
            var names = new System.Collections.Generic.List<string>();
            if ((reads & NumberReads.Level) != 0) names.Add("a level");
            if ((reads & NumberReads.Caster) != 0) names.Add("the caster");
            if ((reads & NumberReads.Target) != 0) names.Add("the target");
            if ((reads & NumberReads.Caller) != 0) names.Add("the caller's values");
            if ((reads & NumberReads.LocalData) != 0) names.Add("Local Data");
            if ((reads & NumberReads.Carrier) != 0) names.Add("an effect");
            if ((reads & NumberReads.Data) != 0) names.Add("an activation's data");
            if ((reads & NumberReads.Owner) != 0) names.Add("its owner");
            return Join(names);
        }

        /// <summary>Names joined as a sentence joins them: "a", "a and b", "a, b and c".</summary>
        public static string Join(System.Collections.Generic.IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            var text = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) text.Append(i == names.Count - 1 ? " and " : ", ");
                text.Append(names[i]);
            }
            return text.ToString();
        }

        // A series as the style writes one, "8/9/10/12", or its range past six values, "8–20"
        private static string Series(float[] values, bool percent)
        {
            if (values == null || values.Length == 0) return Number(1f, percent);
            if (values.Length == 1) return Number(values[0], percent);
            if (values.Length > 6)
            {
                float min = values[0], max = values[0];
                foreach (var value in values)
                {
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
                return $"{Number(min, percent)}–{Number(max, percent)}";
            }
            var text = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append('/');
                text.Append(Number(values[i], percent));
            }
            return text.ToString();
        }

        /// <summary>A number as a field shows it: up to two decimals, and as a percentage in a "%" field.</summary>
        public static string Number(float value, bool percent = false) =>
            percent ? $"{(value * 100f).ToString("0.##", CultureInfo.CurrentCulture)}%" : value.ToString("0.##", CultureInfo.CurrentCulture);

        private static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-6f;

        // "AttributeBacked" as "Attribute Backed"
        internal static string Spaced(string name)
        {
            var spaced = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) spaced.Append(' ');
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }
    }
}
