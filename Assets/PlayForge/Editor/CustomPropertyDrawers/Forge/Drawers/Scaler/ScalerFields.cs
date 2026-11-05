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
    /// (ED-33): the kinds a field takes, in the Kind menu's order; a scaler's values per level, shown as a table with a
    /// read-only curve beside them; Stretch Over Levels and what it uses; and a shared scaler's asset, with how many fields
    /// share it. Nothing here writes while it draws (ED-34): every write follows an edit, through the field's own
    /// SerializedObject, so it reaches each selected asset and one Undo takes it back (ED-12).
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
            return TypeCache.GetTypesDerivedFrom<AbstractScaler>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null)
                .Where(type => assignment?.RootType == null || assignment.RootType.IsAssignableFrom(type))
                .Where(type => assignment?.AvoidType == null || !assignment.AvoidType.IsAssignableFrom(type))
                .Where(type => !(inAsset && IsShared(type)))
                .OrderBy(Rank).ThenBy(KindName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int Rank(Type type) =>
            type == typeof(SimpleScaler) || type == typeof(CachedSimpleScaler) ? 0 : IsShared(type) ? 1 : 2;

        /// <summary>Whether a kind reads a Scaler asset: Shared, or the cached Shared an attribute set's elements take.</summary>
        internal static bool IsShared(Type type) => type == typeof(SharedScaler) || type == typeof(SharedCachedScaler);

        /// <summary>A kind's name in the Kind menu: By Level for a simple scaler, Shared for a Scaler asset's.</summary>
        internal static string KindName(Type type)
        {
            if (type == null) return "None";
            if (type == typeof(SimpleScaler) || type == typeof(CachedSimpleScaler)) return "By Level";
            if (IsShared(type)) return "Shared";
            if (type == typeof(ConstantScaler)) return "Fixed Value";
            var name = type.Name;
            if (name.StartsWith("Cached")) name = name.Substring("Cached".Length);
            if (name.EndsWith("Scaler")) name = name.Substring(0, name.Length - "Scaler".Length);
            return name.Length > 0 ? ObjectNames.NicifyVariableName(name) : "Scaler";
        }

        /// <summary>
        /// Gives the field a new scaler of a kind, one per selected asset, which maps levels exactly and starts with one
        /// value. Null takes the scaler away.
        /// </summary>
        internal static void SetKind(SerializedProperty scaler, Type type, float firstValue)
        {
            var path = scaler.propertyPath;
            var serialized = scaler.serializedObject;
            if (serialized.isEditingMultipleObjects)
            {
                // Each asset gets an object of its own: one shared between assets would be one scaler in two places
                foreach (var target in serialized.targetObjects)
                {
                    var own = new SerializedObject(target);
                    var property = own.FindProperty(path);
                    if (property == null) continue;
                    property.managedReferenceValue = Make(type, firstValue);
                    own.ApplyModifiedProperties();
                }
                serialized.Update();
                return;
            }
            scaler.managedReferenceValue = Make(type, firstValue);
            serialized.ApplyModifiedProperties();
        }

        private static AbstractScaler Make(Type type, float firstValue)
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

        /// <summary>A series' range as a field reads it, as in "6–8 s", or "6 s" when every level reads the same.</summary>
        internal static string RangeText(IReadOnlyList<float> values, string unit)
        {
            if (values == null || values.Count == 0) return "";
            float min = values.Min(), max = values.Max();
            var suffix = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
            return Mathf.Approximately(min, max) ? $"{Number(min)}{suffix}" : $"{Number(min)}–{Number(max)}{suffix}";
        }

        internal static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// Writes the curve a stretched scaler reads, from its values, through the field's SerializedObject, so it joins the
        /// edit that changed them in one Undo step. Called after an edit, never while drawing.
        /// </summary>
        internal static void KeepCurve(SerializedProperty scaler)
        {
            if (!Alive(scaler)) return;
            var curve = scaler.FindPropertyRelative(nameof(AbstractScaler.Scaling));
            var interpolation = scaler.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
            if (curve == null || interpolation == null) return;
            var wanted = AbstractScaler.CurveFor(ValuesOf(scaler), (EScalerInterpolation)interpolation.intValue);
            if (SameCurve(curve.animationCurveValue, wanted)) return;
            curve.animationCurveValue = wanted;
            scaler.serializedObject.ApplyModifiedProperties();
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

        /// <summary>
        /// A scaler's content: a Shared scaler's asset; otherwise the kind's own fields, its per-level table and its curve
        /// while it uses them, Stretch Over Levels and what stretching uses, then its behaviours.
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

            foreach (var field in KindFields(scaler)) content.Add(field);
            if (value.UseScalingOptions())
            {
                content.Add(LevelTable(scaler));
                content.Add(StretchRows(scaler));
            }
            var behaviours = scaler.FindPropertyRelative(nameof(AbstractScaler.Behaviours));
            if (behaviours != null)
                content.Add(new PropertyField(behaviours, "Behaviours")
                {
                    name = "Behaviours",
                    tooltip = "Rules that adjust the value the scaler works out, in order",
                });
            return content;
        }

        // The fields a kind adds, from the concrete classes in its chain, as a Threshold scaler's thresholds
        private static IEnumerable<VisualElement> KindFields(SerializedProperty scaler)
        {
            var type = scaler.managedReferenceValue?.GetType();
            if (type == null) yield break;
            var chain = new List<Type>();
            for (var each = type; each != null && !each.IsAbstract; each = each.BaseType) chain.Add(each);
            chain.Reverse();
            var seen = new HashSet<string>();
            foreach (var level in chain)
            {
                foreach (var field in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || !seen.Add(field.Name)) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null && field.GetCustomAttribute<SerializeReference>() == null) continue;
                    var property = scaler.FindPropertyRelative(field.Name);
                    if (property == null) continue;
                    yield return new PropertyField(property) { name = field.Name };
                }
            }
        }

        /// <summary>
        /// The values per level, as the scaler board's table: a row of levels over a row of values, ten to a row, then how
        /// many levels it has, with the curve beside them. A level past the end reads the last value, so a table shorter
        /// than its asset's levels says which levels read which value.
        /// </summary>
        internal static VisualElement LevelTable(SerializedProperty scaler)
        {
            var root = new VisualElement { name = "LevelTable" };
            root.AddToClassList("pf-scaler-table");
            var values = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            if (values == null) return root;

            var grid = new VisualElement { name = "Grid" };
            root.Add(grid);
            var preview = new ForgeCurvePreview { name = "Curve" };
            root.Add(preview);

            var count = new IntegerField("Levels")
            {
                name = "LevelCount",
                tooltip = "How many levels have a value; a level past the end reads the last",
            };
            count.AddToClassList(IntegerField.alignedFieldUssClassName);
            count.isDelayed = true;
            count.RegisterValueChangedCallback(change => Resize(scaler, values, change.newValue));
            root.Add(count);

            int shown = -1;
            void ShowGrid()
            {
                count.SetValueWithoutNotify(values.arraySize);
                preview.Show(ValuesOf(scaler));
                if (values.arraySize == shown) return;
                shown = values.arraySize;
                grid.Clear();
                for (int start = 0; start < values.arraySize; start += 10)
                {
                    var levels = new VisualElement();
                    levels.AddToClassList("pf-scaler-table__levels");
                    var numbers = new VisualElement();
                    numbers.AddToClassList("pf-scaler-table__values");
                    var levelsHead = new Label("Level");
                    levelsHead.AddToClassList("pf-scaler-table__head");
                    levels.Add(levelsHead);
                    var valuesHead = new Label("Value");
                    valuesHead.AddToClassList("pf-scaler-table__head");
                    numbers.Add(valuesHead);
                    for (int i = start; i < Mathf.Min(start + 10, values.arraySize); i++)
                    {
                        var level = new Label((i + 1).ToString(CultureInfo.InvariantCulture));
                        level.AddToClassList("pf-scaler-table__cell");
                        levels.Add(level);
                        var field = new FloatField { name = $"Level{i + 1}", tooltip = $"The value at level {i + 1}" };
                        field.AddToClassList("pf-scaler-table__cell");
                        field.BindProperty(values.GetArrayElementAtIndex(i));
                        // The curve a stretched scaler reads follows a typed value, in the same Undo step. Binding sends
                        // the field change events of its own as it draws, which write nothing (ED-34)
                        field.RegisterValueChangedCallback(_ =>
                        {
                            if (IsTypedInto(field)) KeepCurve(scaler);
                        });
                        numbers.Add(field);
                    }
                    grid.Add(levels);
                    grid.Add(numbers);
                }
            }

            root.TrackSerializedObjectValue(scaler.serializedObject, _ =>
            {
                if (Alive(values)) ShowGrid();
            });
            ShowGrid();
            return root;
        }

        // Sets how many levels have a value: a new level takes the last value, as a level past the end reads it
        private static void Resize(SerializedProperty scaler, SerializedProperty values, int levels)
        {
            if (!Alive(values)) return;
            levels = Mathf.Clamp(levels, 1, 100);
            int was = values.arraySize;
            if (levels == was) return;
            float last = was > 0 ? values.GetArrayElementAtIndex(was - 1).floatValue : 1f;
            values.arraySize = levels;
            for (int i = was; i < levels; i++) values.GetArrayElementAtIndex(i).floatValue = last;
            scaler.serializedObject.ApplyModifiedProperties();
            KeepCurve(scaler);
        }

        /// <summary>
        /// Stretch Over Levels, and while it's on, the level range its values spread over and how a level between two
        /// reads. Off, level 4 reads the 4th value (Decision 28).
        /// </summary>
        internal static VisualElement StretchRows(SerializedProperty scaler)
        {
            var root = new VisualElement { name = "Stretch" };
            var stretchProp = scaler.FindPropertyRelative(nameof(AbstractScaler.StretchOverLevels));
            var rangeProp = scaler.FindPropertyRelative(nameof(AbstractScaler.Configuration));
            var maxProp = scaler.FindPropertyRelative(nameof(AbstractScaler.MaxLevel));
            var betweenProp = scaler.FindPropertyRelative(nameof(AbstractScaler.Interpolation));
            if (stretchProp == null) return root;

            var stretch = new Toggle("Stretch Over Levels")
            {
                name = "StretchOverLevels",
                tooltip = "Off: level 4 reads the 4th value, and a level past the end reads the last. On: the values " +
                          "spread over the level range, and a level between two reads between them",
            };
            stretch.AddToClassList(Toggle.alignedFieldUssClassName);
            stretch.BindProperty(stretchProp);
            root.Add(stretch);

            var range = new EnumField("Level Range") { name = "LevelRange", tooltip = "The levels the values spread over" };
            range.AddToClassList(EnumField.alignedFieldUssClassName);
            range.AddToClassList("pf-dependent");
            range.BindProperty(rangeProp);
            root.Add(range);
            var max = new IntegerField("Max Level") { name = "MaxLevel", tooltip = "The last level of the scaler's own range" };
            max.AddToClassList(IntegerField.alignedFieldUssClassName);
            max.AddToClassList("pf-dependent");
            max.BindProperty(maxProp);
            root.Add(max);
            var between = new EnumField("Between Levels") { name = "BetweenLevels", tooltip = "How a level between two values reads" };
            between.AddToClassList(EnumField.alignedFieldUssClassName);
            between.AddToClassList("pf-dependent");
            between.BindProperty(betweenProp);
            between.RegisterValueChangedCallback(_ =>
            {
                if (IsTypedInto(between)) KeepCurve(scaler);
            });
            root.Add(between);

            void Show()
            {
                bool on = stretchProp.boolValue;
                range.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                between.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                max.style.display = on && (ELevelConfig)rangeProp.intValue != ELevelConfig.LockToLevelProvider ? DisplayStyle.Flex : DisplayStyle.None;
            }
            root.TrackSerializedObjectValue(scaler.serializedObject, _ =>
            {
                if (Alive(stretchProp)) Show();
            });
            Show();
            return root;
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
                var shared = assetProp.objectReferenceValue as ScalerAsset;
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
                .Where(part => part != nameof(ScalerMagnitudeOperation.Scaler))
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
