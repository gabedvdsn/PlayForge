using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>One change a sheet makes: a number of an asset, what it reads now, and what it will (TW-27).</summary>
    public sealed class SpreadsheetChange
    {
        /// <summary>The asset the sheet's row names: the asset whose number changes, or the item whose effect's does.</summary>
        public Object Asset { get; internal set; }

        /// <summary>The number, as the sheet names it: its field, or an item's stat and the effect that gives it.</summary>
        public string Field { get; internal set; }

        /// <summary>What it reads now, and what it will: a number, or one for each level, as 10/15/20.</summary>
        public string Was { get; internal set; }
        public string Becomes { get; internal set; }

        /// <summary>Whether <see cref="SpreadsheetImport.Apply"/> makes it: each is, until the window's list says otherwise.</summary>
        public bool Chosen { get; set; } = true;

        // The asset written, and the write
        internal Object Target;
        internal Action<SerializedObject> Write;
    }

    /// <summary>What a sheet would change in the project's assets, and what of it can't be read (TW-27).</summary>
    public sealed class SpreadsheetImport
    {
        internal readonly List<SpreadsheetChange> changes = new();
        internal readonly List<string> problems = new();

        /// <summary>The sheet's kind: <see cref="ForgeSpreadsheets.NumbersSheet"/> or <see cref="ForgeSpreadsheets.StatsSheet"/>.</summary>
        public string Kind { get; internal set; } = ForgeSpreadsheets.NumbersSheet;

        public IReadOnlyList<SpreadsheetChange> Changes => changes;

        /// <summary>What the sheet asked that can't be done, row by row: an asset it can't find, a field it hasn't got.</summary>
        public IReadOnlyList<string> Problems => problems;

        /// <summary>Makes the chosen changes, as one Undo step.</summary>
        public void Apply() => ForgeSpreadsheets.Apply(changes.Where(change => change.Chosen).ToList(), Kind);
    }

    /// <summary>
    /// Numbers by level and item stats, to and from CSV, so a game is balanced in a spreadsheet (TW-27, Decision 67).
    /// <para>
    /// A sheet of numbers by level has a row for each number of an asset that reads a Table or By Level: the asset's tag
    /// and name, the field as the inspector labels it, its kind, and what it reads at each level, its values times its
    /// number, as Laguna Blade's -450/-600/-750. A sheet of item stats has a row for each item and a column for each
    /// attribute its granted effects add to: a number, or one for each level, as 10/15/20.
    /// </para>
    /// <para>
    /// A sheet read back finds each row's asset by its asset tag, or by its name where it gives none; a name two assets
    /// share, an asset that isn't there and a field it hasn't got are problems, not guesses. Its changes, listed, apply as
    /// one Undo step, each number's values set so it reads what the row says.
    /// </para>
    /// </summary>
    public static class ForgeSpreadsheets
    {
        /// <summary>The kinds of sheet.</summary>
        public const string NumbersSheet = "Numbers by Level";
        public const string StatsSheet = "Item Stats";

        private const string TagColumn = "Asset Tag";
        private const string NameColumn = "Name";
        private const string FieldColumn = "Field";
        private const string KindColumn = "Kind";
        private const string LevelColumn = "Level";

        // ═══════════════════════════════════════════════════════════════════════════
        // Writing
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A sheet of these assets' numbers by level, a row for each.</summary>
        public static string NumbersOf(IEnumerable<Object> assets)
        {
            var rows = new List<string[]>();
            int levels = 1;
            foreach (var asset in Assets(assets))
            {
                using var serialized = new SerializedObject(asset);
                foreach (var number in NumbersIn(serialized))
                {
                    var reads = number.Reads();
                    levels = Mathf.Max(levels, reads.Length);
                    rows.Add(new[] { TagOf(asset), NameOf(asset), number.Label, number.Kind }.Concat(reads.Select(Text)).ToArray());
                }
            }

            var header = new[] { TagColumn, NameColumn, FieldColumn, KindColumn }
                .Concat(Enumerable.Range(1, levels).Select(level => $"{LevelColumn} {level}")).ToArray();
            return Csv(header, rows, header.Length);
        }

        /// <summary>A sheet of these items' stats: a row for each, and a column for each attribute their granted effects add to.</summary>
        public static string StatsOf(IEnumerable<Item> items)
        {
            var list = items.Where(item => item != null).Distinct().ToList();
            var attributes = list.SelectMany(item => StatsIn(item).Select(stat => stat.Attribute)).Distinct()
                .OrderBy(AttributeName, StringComparer.OrdinalIgnoreCase).ToList();

            var rows = new List<string[]>();
            foreach (var item in list)
            {
                var stats = StatsIn(item);
                var row = new List<string> { TagOf(item), NameOf(item) };
                foreach (var attribute in attributes)
                {
                    var adding = stats.Where(stat => stat.Attribute == attribute).ToList();
                    // Two effects adding to it are set in a sheet of numbers, each its own row
                    row.Add(adding.Count == 1 ? StatText(adding[0]) : "");
                }
                rows.Add(row.ToArray());
            }

            var header = new[] { TagColumn, NameColumn }.Concat(attributes.Select(AttributeName)).ToArray();
            return Csv(header, rows, header.Length);
        }

        /// <summary>
        /// Writes a sheet of numbers by level to a file the user picks: the selected assets', or every asset's given. UTF-8,
        /// marked so, as a spreadsheet opens it.
        /// </summary>
        public static void ExportNumbers(IEnumerable<Object> assets)
        {
            var path = EditorUtility.SaveFilePanel("Export Numbers by Level", "", "Numbers by Level.csv", "csv");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, NumbersOf(assets), new UTF8Encoding(true));
        }

        /// <summary>Writes a sheet of these items' stats to a file the user picks.</summary>
        public static void ExportStats(IEnumerable<Item> items)
        {
            var path = EditorUtility.SaveFilePanel("Export Item Stats", "", "Item Stats.csv", "csv");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, StatsOf(items), new UTF8Encoding(true));
        }

        /// <summary>Reads a sheet the user picks and lists its changes in a window, which applies those chosen.</summary>
        public static void Import()
        {
            var path = EditorUtility.OpenFilePanel("Import a Sheet", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            ForgeSpreadsheetWindow.Show(Read(File.ReadAllText(path)), Path.GetFileName(path));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Reading
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>What a sheet would change in the project's PlayForge assets: a sheet of numbers by level, or of item stats.</summary>
        public static SpreadsheetImport Read(string csv) => Read(csv, ForgeValidator.ProjectAssets());

        /// <summary>What a sheet would change in these assets.</summary>
        public static SpreadsheetImport Read(string csv, IEnumerable<Object> assets)
        {
            var import = new SpreadsheetImport();
            var rows = Parse(csv ?? "");
            if (rows.Count == 0)
            {
                import.problems.Add("The sheet is empty.");
                return import;
            }

            var header = rows[0].Select(cell => cell.Trim()).ToList();
            if (header.Count < 2 || header[0] != TagColumn || header[1] != NameColumn)
            {
                import.problems.Add($"The sheet's first columns aren't {TagColumn} and {NameColumn}, as PlayForge writes them.");
                return import;
            }

            var finder = new Finder(Assets(assets));
            bool numbers = header.Count > 2 && header[2] == FieldColumn;
            import.Kind = numbers ? NumbersSheet : StatsSheet;
            var written = new Dictionary<(Object, string), (string Row, string Becomes)>();
            for (int i = 1; i < rows.Count; i++)
            {
                if (numbers) ReadNumbers(import, rows[i], i + 1, finder, written);
                else ReadStats(import, header, rows[i], i + 1, finder, written);
            }
            return import;
        }

        private static void ReadNumbers(SpreadsheetImport import, List<string> row, int line, Finder finder,
            Dictionary<(Object, string), (string, string)> written)
        {
            var asset = finder.Find(Cell(row, 0), Cell(row, 1), line, import.problems);
            if (asset == null) return;
            var label = Cell(row, 2);
            var values = new List<float>();
            for (int column = 4; column < row.Count; column++)
            {
                var cell = row[column].Trim();
                if (cell.Length == 0) continue;
                if (!TryNumber(cell, out var value))
                {
                    import.problems.Add($"Row {line}: \"{cell}\" isn't a number.");
                    return;
                }
                values.Add(value);
            }
            if (values.Count == 0) return;

            using var serialized = new SerializedObject(asset);
            var matching = NumbersIn(serialized).Where(each => each.Label == label).ToList();
            if (matching.Count != 1)
            {
                import.problems.Add(matching.Count == 0
                    ? $"Row {line}: {NameOf(asset)} has no number by level that reads \"{label}\"."
                    : $"Row {line}: {NameOf(asset)} has {matching.Count} numbers that read \"{label}\".");
                return;
            }
            Plan(import, asset, asset, label, matching[0], values.ToArray(), line, written);
        }

        private static void ReadStats(SpreadsheetImport import, List<string> header, List<string> row, int line, Finder finder,
            Dictionary<(Object, string), (string, string)> written)
        {
            var asset = finder.Find(Cell(row, 0), Cell(row, 1), line, import.problems);
            if (asset == null) return;
            if (asset is not Item item)
            {
                import.problems.Add($"Row {line}: {NameOf(asset)} isn't an item.");
                return;
            }

            var stats = StatsIn(item);
            for (int column = 2; column < header.Count; column++)
            {
                var cell = Cell(row, column);
                if (cell.Length == 0) continue;
                var attribute = header[column];
                var values = cell.Split('/').Select(part => part.Trim()).ToList();
                var parsed = new float[values.Count];
                bool numbers = true;
                for (int i = 0; i < values.Count && numbers; i++) numbers = TryNumber(values[i], out parsed[i]);
                if (!numbers)
                {
                    import.problems.Add($"Row {line}: {NameOf(item)}'s {attribute}, \"{cell}\", isn't a number, or one for each level.");
                    continue;
                }

                var adding = stats.Where(stat => string.Equals(AttributeName(stat.Attribute), attribute, StringComparison.OrdinalIgnoreCase)).ToList();
                if (adding.Count != 1)
                {
                    import.problems.Add(adding.Count == 0
                        ? $"Row {line}: {NameOf(item)} has no effect that adds to {attribute}."
                        : $"Row {line}: {adding.Count} of {NameOf(item)}'s effects add to {attribute}, so set each in a sheet of numbers.");
                    continue;
                }

                var stat = adding[0];
                using var serialized = new SerializedObject(stat.Effect);
                var number = new Number(serialized.FindProperty(stat.Path), stat.Path, null);
                Plan(import, item, stat.Effect, $"{attribute} · {NameOf(stat.Effect)}", number, parsed, line, written);
            }
        }

        // Adds the change a row makes to a number, where it makes one: its values set so it reads the row's, a constant's
        // number where the row gives one value, or a Table where it gives one for each level
        private static void Plan(SpreadsheetImport import, Object rowAsset, Object target, string field, Number number, float[] values, int line,
            Dictionary<(Object, string), (string Row, string Becomes)> written)
        {
            var reads = number.Reads();
            if (reads.Length == values.Length && reads.Zip(values, Same).All(same => same)) return;

            var becomes = string.Join("/", values.Select(Text));
            if (written.TryGetValue((target, number.Path), out var earlier))
            {
                if (earlier.Becomes != becomes)
                    import.problems.Add($"Row {line}: {NameOf(target)}'s {field} is set otherwise by {earlier.Row}, which it shares, so this row's goes unread.");
                return;
            }

            var write = number.WriteReads(values, out var problem);
            if (write == null)
            {
                import.problems.Add($"Row {line}: {NameOf(target)}'s {field} {problem}");
                return;
            }
            written[(target, number.Path)] = ($"row {line}", becomes);
            import.changes.Add(new SpreadsheetChange
            {
                Asset = rowAsset,
                Field = field,
                Was = string.Join("/", reads.Select(Text)),
                Becomes = becomes,
                Target = target,
                Write = write,
            });
        }

        /// <summary>Makes changes, as one Undo step named for the sheet.</summary>
        internal static void Apply(IReadOnlyList<SpreadsheetChange> changes, string kind)
        {
            if (changes.Count == 0) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (var target in changes.GroupBy(change => change.Target))
            {
                if (target.Key == null) continue;
                var serialized = new SerializedObject(target.Key);
                foreach (var change in target) change.Write(serialized);
                serialized.ApplyModifiedProperties();
            }
            Undo.SetCurrentGroupName($"Import {kind}");
            Undo.CollapseUndoOperations(group);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Numbers by level
        // ═══════════════════════════════════════════════════════════════════════════

        // A number field, or a scaler of its own, whose kind is a number by level, as a sheet's row names it
        private sealed class Number
        {
            public readonly string Path;
            public readonly string Label;
            private readonly SerializedProperty property;

            // The field that holds its scaler: a number's Scaler, an attribute set element's starting value's Scaling
            private readonly string scalerName;

            public Number(SerializedProperty property, string path, string label, string scalerName = nameof(ScalableFloat.Scaler))
            {
                this.property = property;
                Path = path;
                Label = label;
                this.scalerName = scalerName;
            }

            // Its scaler: the number's own, or the property itself where it's a scaler of its own
            private SerializedProperty ScalerProperty =>
                property.propertyType == SerializedPropertyType.ManagedReference ? property : property.FindPropertyRelative(scalerName);

            private AbstractScaler Scaler => ScalerProperty?.managedReferenceValue as AbstractScaler;

            public string Kind => Scaler is { } scaler ? NumberText.KindName(scaler) : "Constant";

            private SerializedProperty MagnitudeProperty => property.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            private SerializedProperty RealProperty => property.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));

            private float Magnitude => MagnitudeProperty is { } magnitude
                ? magnitude.propertyType == SerializedPropertyType.Integer ? magnitude.intValue : magnitude.floatValue
                : 0f;

            // How it combines its scaler with its number; a scaler of its own reads its values alone
            private EMagnitudeOperation Real => RealProperty is { } real ? (EMagnitudeOperation)real.intValue : EMagnitudeOperation.UseScaler;

            /// <summary>What it reads at each level: its values combined with its number, or its number alone.</summary>
            public float[] Reads()
            {
                var scaler = Scaler;
                if (scaler == null || AbstractScaler.IsNone(scaler) || Real == EMagnitudeOperation.UseMagnitude) return new[] { Magnitude };
                var values = scaler.LevelValues ?? Array.Empty<float>();
                return values.Select(value => ForgeHelper.MagnitudeAndScalerOperation(Magnitude, value, Real)).ToArray();
            }

            /// <summary>
            /// The write that makes it read these, or null and why it can't: a number by level's values found from what it's
            /// to read, its number left; a constant's number, or a Table where there's one for each level.
            /// </summary>
            public Action<SerializedObject> WriteReads(float[] reads, out string problem)
            {
                problem = null;
                var path = Path;
                var scaler = Scaler;
                if (scaler == null || AbstractScaler.IsNone(scaler) || Real == EMagnitudeOperation.UseMagnitude)
                {
                    // A constant reads its number; one for each level makes it a Table, reading its values alone
                    if (property.propertyType == SerializedPropertyType.ManagedReference || MagnitudeProperty == null)
                    {
                        problem = "has no number to set.";
                        return null;
                    }
                    if (reads.Length == 1)
                        return serialized =>
                        {
                            var number = serialized.FindProperty(path);
                            SetMagnitude(number.FindPropertyRelative(nameof(ScalableFloat.Magnitude)), reads[0]);
                            number.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude)).intValue = (int)EMagnitudeOperation.UseMagnitude;
                        };
                    // An attribute set element's starting value takes the cached kinds
                    var held = scalerName;
                    return serialized =>
                    {
                        var number = serialized.FindProperty(path);
                        AbstractScaler table = held == nameof(ScalableFloat.Scaler) ? new SimpleScaler() : new CachedSimpleScaler();
                        table.StretchOverLevels = false;
                        table.LevelValues = reads.ToArray();
                        table.Scaling = AbstractScaler.CurveFor(table.LevelValues, table.Interpolation);
                        number.FindPropertyRelative(held).managedReferenceValue = table;
                        number.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude)).intValue = (int)EMagnitudeOperation.UseScaler;
                    };
                }

                if (!ScalerFields.IsLevelKind(scaler.GetType()))
                {
                    problem = $"is a {NumberText.KindName(scaler)}, not a Table or By Level.";
                    return null;
                }

                // Its values, from what it's to read: the inverse of how it combines them with its number
                float magnitude = Magnitude;
                var real = Real;
                if (real == EMagnitudeOperation.MultiplyWithScaler && Mathf.Approximately(magnitude, 0f))
                {
                    problem = "reads its values times 0, so what it reads can't give them.";
                    return null;
                }
                var values = reads.Select(read => real switch
                {
                    EMagnitudeOperation.MultiplyWithScaler => read / magnitude,
                    EMagnitudeOperation.AddScaler => read - magnitude,
                    _ => read,
                }).ToArray();
                bool scalerOwn = property.propertyType == SerializedPropertyType.ManagedReference;
                var holder = scalerName;
                return serialized =>
                {
                    var own = serialized.FindProperty(path);
                    var held = scalerOwn ? own : own.FindPropertyRelative(holder);
                    var list = held.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
                    list.arraySize = values.Length;
                    for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
                    // A curve's points follow its values
                    var curve = held.FindPropertyRelative(nameof(AbstractScaler.Scaling));
                    var between = held.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
                    if (curve != null && between != null) curve.animationCurveValue = AbstractScaler.CurveFor(values, (EScalerInterpolation)between.intValue);
                };
            }

            private static void SetMagnitude(SerializedProperty magnitude, float value)
            {
                if (magnitude.propertyType == SerializedPropertyType.Integer) magnitude.intValue = Mathf.RoundToInt(value);
                else magnitude.floatValue = value;
            }
        }

        // Every number of an asset whose kind is a number by level, and each scaler of its own of that kind, in order
        private static IEnumerable<Number> NumbersIn(SerializedObject serialized)
        {
            var found = new List<Number>();
            var covered = new HashSet<string>();
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                var path = iterator.propertyPath;
                bool spec = IsMagnitudeSpec(iterator);
                if (spec || NumberFields.IsNumber(iterator))
                {
                    var scalerName = spec ? nameof(AttributeMagnitudeSpec.Scaling) : nameof(ScalableFloat.Scaler);
                    var scaler = iterator.FindPropertyRelative(scalerName);
                    if (scaler == null) continue;
                    covered.Add(scaler.propertyPath);
                    var real = iterator.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                    if (scaler.managedReferenceValue is AbstractScaler kind && ScalerFields.IsLevelKind(kind.GetType())
                        && (real == null || (EMagnitudeOperation)real.intValue != EMagnitudeOperation.UseMagnitude))
                        found.Add(new Number(iterator.Copy(), path, LabelOf(serialized, path), scalerName));
                }
                else if (iterator.propertyType == SerializedPropertyType.ManagedReference && !covered.Contains(path)
                         && iterator.managedReferenceValue is AbstractScaler own && ScalerFields.IsLevelKind(own.GetType()))
                    found.Add(new Number(iterator.Copy(), path, LabelOf(serialized, path)));
            }
            return found;
        }

        // An attribute set element's starting value, which holds a number and a scaler as a number field does
        private static bool IsMagnitudeSpec(SerializedProperty property) =>
            property.propertyType == SerializedPropertyType.Generic && property.type == nameof(AttributeMagnitudeSpec);

        /// <summary>
        /// A number's field as a sheet names it: its path's parts, spaced, without "Operation" or a scaler's own part; a
        /// list's element by the attribute it names, as an attribute set's Health, or else by its place from 1.
        /// </summary>
        internal static string LabelOf(SerializedObject serialized, string path)
        {
            var parts = new List<string>();
            var segments = path.Split('.');
            var at = new StringBuilder();
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == "Array" && i + 1 < segments.Length && segments[i + 1].StartsWith("data["))
                {
                    var data = segments[++i];
                    at.Append(".Array.").Append(data);
                    int index = int.Parse(data.Substring(5, data.Length - 6), CultureInfo.InvariantCulture);
                    var named = AttributeNamedBy(serialized.FindProperty(at.ToString()));
                    if (parts.Count > 0) parts[parts.Count - 1] = named != null ? $"{parts[parts.Count - 1]} › {named}" : $"{parts[parts.Count - 1]} {index + 1}";
                    continue;
                }
                if (at.Length > 0) at.Append('.');
                at.Append(segment);
                if (segment == nameof(ScalableFloat.Scaler)) continue;
                var name = segment.EndsWith("Operation") && segment.Length > "Operation".Length ? segment.Substring(0, segment.Length - "Operation".Length) : segment;
                parts.Add(ObjectNames.NicifyVariableName(name));
            }
            return parts.Count == 0 ? "Values" : string.Join(" › ", parts);
        }

        // The attribute a list's element names, as an attribute set's element or an additional impact does
        private static string AttributeNamedBy(SerializedProperty element)
        {
            if (element == null || element.propertyType != SerializedPropertyType.Generic) return null;
            var reference = element.FindPropertyRelative("Attribute") ?? element.FindPropertyRelative("AttributeTarget");
            return reference is { propertyType: SerializedPropertyType.ObjectReference, objectReferenceValue: Attribute attribute }
                ? AttributeName(attribute)
                : null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Item stats
        // ═══════════════════════════════════════════════════════════════════════════

        // An impact of an item's granted effect that adds to an attribute, its number's path
        private readonly struct Stat
        {
            public readonly Attribute Attribute;
            public readonly GameplayEffect Effect;
            public readonly string Path;

            public Stat(Attribute attribute, GameplayEffect effect, string path)
            {
                Attribute = attribute;
                Effect = effect;
                Path = path;
            }
        }

        // What an item's granted effects add to: each first impact and additional impact that adds to an attribute
        private static List<Stat> StatsIn(Item item)
        {
            var stats = new List<Stat>();
            if (item?.GrantedEffects == null) return stats;
            foreach (var effect in item.GrantedEffects.Where(effect => effect != null).Distinct())
            {
                var impact = effect.ImpactSpecification;
                if (impact == null) continue;
                const string root = nameof(GameplayEffect.ImpactSpecification);
                if (impact.AttributeTarget != null && impact.ImpactOperation == ECalculationOperation.Add)
                    stats.Add(new Stat(impact.AttributeTarget, effect, $"{root}.{nameof(GameplayEffectImpact.MagnitudeOperation)}"));
                var entries = impact.AdditionalImpacts ?? new List<AttributeImpactEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i]?.AttributeTarget == null || entries[i].Operation != ECalculationOperation.Add) continue;
                    stats.Add(new Stat(entries[i].AttributeTarget, effect,
                        $"{root}.{nameof(GameplayEffectImpact.AdditionalImpacts)}.Array.data[{i}].{nameof(AttributeImpactEntry.Magnitude)}"));
                }
            }
            return stats;
        }

        // A stat's cell: its number, or one for each level; nothing for a number of another kind, which a sheet can't set
        private static string StatText(Stat stat)
        {
            using var serialized = new SerializedObject(stat.Effect);
            var property = serialized.FindProperty(stat.Path);
            if (property == null) return "";
            var scaler = property.FindPropertyRelative(nameof(ScalableFloat.Scaler))?.managedReferenceValue as AbstractScaler;
            if (scaler != null && !AbstractScaler.IsNone(scaler) && !ScalerFields.IsLevelKind(scaler.GetType())
                && property.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude))?.intValue != (int)EMagnitudeOperation.UseMagnitude)
                return "";
            return string.Join("/", new Number(property, stat.Path, null).Reads().Select(Text));
        }

        private static string AttributeName(Attribute attribute)
        {
            if (attribute == null) return "";
            var name = attribute.GetName();
            return string.IsNullOrWhiteSpace(name) ? attribute.name : name;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Assets
        // ═══════════════════════════════════════════════════════════════════════════

        // The assets a sheet reads and writes: PlayForge's, and Scaler assets, whose tables are shared
        private static IEnumerable<Object> Assets(IEnumerable<Object> assets) =>
            (assets ?? Enumerable.Empty<Object>()).Where(asset => asset is BaseForgeAsset or ScalerAsset).Distinct();

        /// <summary>An asset's tag, as a sheet's row names it: its asset tag's path, or nothing for one that has none.</summary>
        internal static string TagOf(Object asset) => asset switch
        {
            Ability ability => ability.Tags.AssetTag.Name ?? "",
            GameplayEffect effect => effect.Tags.AssetTag.Name ?? "",
            Item item => item.Tags.AssetTag.Name ?? "",
            EntityIdentity entity => entity.AssetTag.Name ?? "",
            AttributeSet set => set.AssetTag.Name ?? "",
            _ => "",
        };

        /// <summary>An asset's name, as a sheet's row names it: its display name, or its file's.</summary>
        internal static string NameOf(Object asset) => asset switch
        {
            null => "",
            BaseForgeAsset forge when !string.IsNullOrWhiteSpace(forge.GetName()) => forge.GetName(),
            _ => asset.name,
        };

        // Finds a row's asset: by its asset tag where it gives one, else by its name, which must be one asset's
        private sealed class Finder
        {
            private readonly Dictionary<string, List<Object>> byTag = new(StringComparer.Ordinal);
            private readonly Dictionary<string, List<Object>> byName = new(StringComparer.Ordinal);

            public Finder(IEnumerable<Object> assets)
            {
                foreach (var asset in assets)
                {
                    var tag = TagOf(asset);
                    if (tag.Length > 0) Add(byTag, tag, asset);
                    Add(byName, NameOf(asset), asset);
                }
            }

            private static void Add(Dictionary<string, List<Object>> into, string key, Object asset)
            {
                if (!into.TryGetValue(key, out var list)) into[key] = list = new List<Object>();
                list.Add(asset);
            }

            public Object Find(string tag, string name, int line, List<string> problems)
            {
                if (tag.Length > 0)
                {
                    if (byTag.TryGetValue(tag, out var tagged) && tagged.Count == 1) return tagged[0];
                    problems.Add(tagged == null ? $"Row {line}: no asset has the tag {tag}." : $"Row {line}: {tagged.Count} assets have the tag {tag}.");
                    return null;
                }
                if (name.Length == 0)
                {
                    problems.Add($"Row {line}: it names no asset, by tag or by name.");
                    return null;
                }
                if (byName.TryGetValue(name, out var named) && named.Count == 1) return named[0];
                problems.Add(named == null
                    ? $"Row {line}: no asset is named {name}."
                    : $"Row {line}: {named.Count} assets are named {name}; give the row its asset tag.");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CSV
        // ═══════════════════════════════════════════════════════════════════════════

        private static string Csv(string[] header, List<string[]> rows, int columns)
        {
            var text = new StringBuilder();
            Line(text, header, columns);
            foreach (var row in rows) Line(text, row, columns);
            return text.ToString();
        }

        private static void Line(StringBuilder text, IReadOnlyList<string> cells, int columns)
        {
            for (int i = 0; i < columns; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(Quoted(i < cells.Count ? cells[i] ?? "" : ""));
            }
            text.Append('\n');
        }

        // A cell in quotes where it needs them: a comma, a quote, a line break, or space at either end; its quotes doubled
        private static string Quoted(string cell) =>
            cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || cell.Length > 0 && (char.IsWhiteSpace(cell[0]) || char.IsWhiteSpace(cell[cell.Length - 1]))
                ? $"\"{cell.Replace("\"", "\"\"")}\""
                : cell;

        /// <summary>A sheet's rows and their cells, as CSV writes them: quoted cells, doubled quotes, and line breaks in quotes.</summary>
        internal static List<List<string>> Parse(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            int start = csv.Length > 0 && csv[0] == '﻿' ? 1 : 0;
            for (int i = start; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else if (c != '\r') cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            // A line with nothing in it is no row
            return rows.Where(each => each.Any(text => text.Trim().Length > 0)).ToList();
        }

        private static string Cell(List<string> row, int index) => index < row.Count ? row[index].Trim() : "";

        private static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static bool Same(float a, float b) => Mathf.Abs(a - b) <= 1e-5f * Mathf.Max(1f, Mathf.Abs(a));
    }
}
