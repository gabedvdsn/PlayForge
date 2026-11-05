using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A magnitude field, as the canvas's scaler board draws it (ED-33): its name, then its kind, Constant or a scaler's,
    /// and either its number or its range, as in "6–8 s", and its field menu. A scaler's kind adds how the field combines
    /// its number with the scaler, then the scaler's own content: its values per level and their curve, or its Scaler
    /// asset. The field showed its Magnitude, Real Magnitude and a scaler in a tinted box of its own, one after another.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScalerMagnitudeOperation))]
    public class ScalerMagnitudeOperationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            MagnitudeField.Build(property, preferredLabel ?? property.displayName, MagnitudeField.UnitOf(fieldInfo), integer: false);
    }

    /// <summary>A whole-number magnitude field, such as Ticks: the same field, with its rounding.</summary>
    [CustomPropertyDrawer(typeof(ScalerIntegerMagnitudeOperation))]
    public class ScalerIntegerMagnitudeOperationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            MagnitudeField.Build(property, preferredLabel ?? property.displayName, MagnitudeField.UnitOf(fieldInfo), integer: true);
    }

    internal static class MagnitudeField
    {
        private const string ConstantKind = "Constant";

        // How a field combines its number with its scaler, by the names the Combine popup shows
        private static readonly (string name, EMagnitudeOperation real)[] Combines =
        {
            ("Scaler", EMagnitudeOperation.UseScaler),
            ("Scaler + Number", EMagnitudeOperation.AddScaler),
            ("Scaler × Number", EMagnitudeOperation.MultiplyWithScaler),
            ("Number Only", EMagnitudeOperation.UseMagnitude),
        };

        internal static string UnitOf(FieldInfo field) => field?.GetCustomAttribute<ScalerOperationKeyword>()?.Unit;

        internal static VisualElement Build(SerializedProperty operation, string label, string unit, bool integer)
        {
            var root = new VisualElement { name = "MagnitudeField" };
            // The style sheet lays out the field, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-magnitude");

            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude));
            var realProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude));
            var scalerProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler));
            var roundingProp = operation.FindPropertyRelative(nameof(ScalerIntegerMagnitudeOperation.Rounding));
            var kinds = ScalerFields.KindsFor(scalerProp);

            var row = new VisualElement { name = "Row" };
            row.AddToClassList("pf-magnitude__row");
            root.Add(row);
            var body = new VisualElement { name = "Body" };
            body.AddToClassList("pf-magnitude__body");
            root.Add(body);

            Type shownType = null;
            bool built = false;
            Label range = null;
            VisualElement combineNumber = null;
            HelpBox noScaler = null;

            void Draw()
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                row.Clear();
                body.Clear();
                range = null;
                combineNumber = null;
                noScaler = null;
                shownType = scalerProp.managedReferenceValue?.GetType();
                built = true;

                var choices = new List<string> { ConstantKind };
                choices.AddRange(kinds.Select(ScalerFields.KindName));
                var shownName = shownType == null ? ConstantKind : ScalerFields.KindName(shownType);
                if (!choices.Contains(shownName)) choices.Add(shownName);

                PopupField<string> kind;
                if (shownType == null)
                {
                    // Constant: the number, its unit, then the kind
                    VisualElement number = integer
                        ? Bound(new IntegerField(label), magnitudeProp)
                        : Bound(new FloatField(label), magnitudeProp);
                    number.name = "Number";
                    number.AddToClassList(BaseField<float>.alignedFieldUssClassName);
                    number.AddToClassList("pf-magnitude__number");
                    row.Add(number);
                    if (!string.IsNullOrEmpty(unit))
                    {
                        var unitLabel = new Label(unit) { name = "Unit" };
                        unitLabel.AddToClassList("pf-magnitude__unit");
                        row.Add(unitLabel);
                    }
                    kind = new PopupField<string>(choices, shownName) { name = "Kind", tooltip = "The field's kind: Constant, or a scaler's" };
                    kind.AddToClassList("pf-magnitude__kind");
                    row.Add(kind);

                    // With no scaler, a field that reads its scaler reads 0
                    noScaler = new HelpBox("With no scaler, this field reads 0. Make it Constant to read its number.", HelpBoxMessageType.Warning)
                    {
                        name = "NoScaler",
                    };
                    body.Add(noScaler);
                }
                else
                {
                    // A scaler: the kind beside the name, then the range it gives
                    kind = new PopupField<string>(label, choices, shownName) { name = "Kind", tooltip = "The field's kind: Constant, or a scaler's" };
                    kind.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                    kind.AddToClassList("pf-magnitude__kind-labelled");
                    row.Add(kind);
                    range = new Label { name = "Range" };
                    range.AddToClassList("pf-magnitude__range");
                    row.Add(range);

                    // How the number joins the scaler
                    var combineRow = new VisualElement { name = "CombineRow" };
                    combineRow.AddToClassList("pf-magnitude__combine");
                    var combine = new PopupField<string>("Combine", Combines.Select(each => each.name).ToList(),
                        Mathf.Max(0, Array.FindIndex(Combines, each => (int)each.real == realProp.intValue)))
                    {
                        name = "Combine",
                        tooltip = "What the field reads: the scaler's value, that plus or times the field's number, or the number alone",
                    };
                    combine.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                    combine.RegisterValueChangedCallback(change =>
                    {
                        var chosen = Combines.First(each => each.name == change.newValue).real;
                        if (realProp.intValue == (int)chosen) return;
                        realProp.intValue = (int)chosen;
                        operation.serializedObject.ApplyModifiedProperties();
                    });
                    combineRow.Add(combine);
                    combineNumber = integer
                        ? Bound(new IntegerField(), magnitudeProp)
                        : Bound(new FloatField(), magnitudeProp);
                    combineNumber.name = "CombineNumber";
                    combineNumber.AddToClassList("pf-magnitude__combine-number");
                    combineRow.Add(combineNumber);
                    body.Add(combineRow);

                    if (integer && roundingProp != null)
                    {
                        var rounding = new EnumField("Rounding") { name = "Rounding", tooltip = "How the value becomes a whole number" };
                        rounding.AddToClassList(EnumField.alignedFieldUssClassName);
                        rounding.BindProperty(roundingProp);
                        body.Add(rounding);
                    }

                    body.Add(ScalerFields.Content(scalerProp));
                }

                kind.RegisterValueChangedCallback(change =>
                {
                    if (change.newValue == change.previousValue) return;
                    if (change.newValue == ConstantKind) ScalerActions.MakeConstant(operation);
                    else
                    {
                        var type = kinds.FirstOrDefault(each => ScalerFields.KindName(each) == change.newValue);
                        if (type != null) ScalerActions.SetKind(operation, type);
                    }
                    Draw();
                });

                var menu = new ForgeIconButton(ForgeGlyphShape.Menu, $"{label} field menu", null) { name = "FieldMenu" };
                menu.AddToClassList("pf-magnitude__menu");
                menu.clicked += () => ShowMenu(operation, menu, label);
                row.Add(menu);
                Show();
            }

            // The range, the number's place in Combine, and whether a scaler is missing, which follow the asset
            void Show()
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                var real = (EMagnitudeOperation)realProp.intValue;
                if (range != null) range.text = RangeOf(operation, unit);
                if (combineNumber != null)
                    combineNumber.style.display = real == EMagnitudeOperation.UseScaler ? DisplayStyle.None : DisplayStyle.Flex;
                if (noScaler != null)
                    noScaler.style.display = real is EMagnitudeOperation.UseScaler or EMagnitudeOperation.MultiplyWithScaler
                        ? DisplayStyle.Flex : DisplayStyle.None;
            }

            root.TrackSerializedObjectValue(operation.serializedObject, _ =>
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                // A kind changed elsewhere, as by an Undo, redraws the field; otherwise it only updates what it reads
                if (!built || scalerProp.managedReferenceValue?.GetType() != shownType) Draw();
                else Show();
            });
            Draw();
            return root;
        }

        private static T Bound<T>(T field, SerializedProperty property) where T : VisualElement, IBindable
        {
            field.bindingPath = property.propertyPath;
            if (field is BindableElement bindable) bindable.BindProperty(property);
            return field;
        }

        /// <summary>
        /// What a field reads across its scaler's levels, through its Combine, as in "6–8 s"; a kind with no values per
        /// level reads by what it is.
        /// </summary>
        internal static string RangeOf(SerializedProperty operation, string unit)
        {
            var scalerProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler));
            var values = ScalerFields.ValuesOf(scalerProp);
            if (values.Length == 0) return ScalerFields.KindName(scalerProp.managedReferenceValue?.GetType());
            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude));
            float magnitude = magnitudeProp.propertyType == SerializedPropertyType.Integer ? magnitudeProp.intValue : magnitudeProp.floatValue;
            var real = (EMagnitudeOperation)operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude)).intValue;
            var read = values.Select(value => ForgeHelper.MagnitudeAndScalerOperation(magnitude, value, real)).ToArray();
            return ScalerFields.RangeText(read, unit);
        }

        // The field menu: Save as Scaler Asset, Use a Scaler Asset, Import Values, Quick Fill, then Make Constant
        private static void ShowMenu(SerializedProperty operation, VisualElement anchor, string label)
        {
            var scalerProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler));
            var scaler = scalerProp.managedReferenceValue as AbstractScaler;
            bool ownValues = scaler != null && !ScalerFields.IsShared(scaler.GetType()) && scaler.UseScalingOptions();
            var menu = new GenericDropdownMenu();

            if (ownValues)
                menu.AddItem("Save as Scaler Asset…", false, () => SaveScalerAssetWindow.Show(operation, DefaultName(operation, label)));
            else menu.AddDisabledItem("Save as Scaler Asset…", false);

            // The assets the field can read: a cached kind's for an attribute set's element, any other kind's otherwise
            var kinds = ScalerFields.KindsFor(scalerProp);
            bool cached = kinds.Contains(typeof(SharedCachedScaler));
            var assets = AssetDatabase.FindAssets($"t:{nameof(ScalerAsset)}")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ScalerAsset>)
                .Where(asset => asset != null && asset.Scaler != null && asset.Scaler is AbstractCachedScaler == cached)
                .OrderBy(asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            bool canShare = kinds.Any(ScalerFields.IsShared);
            if (!canShare || assets.Count == 0) menu.AddDisabledItem("Use a Scaler Asset…", false);
            else
            {
                foreach (var asset in assets)
                {
                    var each = asset;
                    menu.AddItem($"Use a Scaler Asset…/{asset.DisplayName}", false, () => ScalerActions.UseScalerAsset(operation, each));
                }
            }

            if (ownValues)
            {
                menu.AddItem("Import Values…", false, () => ScalerValuesWindow.Show(scalerProp, DefaultName(operation, label)));
                menu.AddItem("Quick Fill…", false, () => QuickFillWizard.Show(scalerProp, () => ScalerFields.KeepCurve(scalerProp)));
            }
            else
            {
                menu.AddDisabledItem("Import Values…", false);
                menu.AddDisabledItem("Quick Fill…", false);
            }

            menu.AddSeparator("");
            if (scaler != null) menu.AddItem("Make Constant", false, () => ScalerActions.MakeConstant(operation));
            else menu.AddDisabledItem("Make Constant", false);
            menu.DropDown(anchor.worldBound, anchor, true);
        }

        // A name for what the field holds, as "Burning Duration"
        internal static string DefaultName(SerializedProperty operation, string label)
        {
            var owner = ForgeImportWindow.NameOf(operation.serializedObject.targetObject);
            return string.IsNullOrEmpty(owner) ? label : $"{owner} {label}";
        }
    }

    /// <summary>
    /// What a magnitude field's kind menu and field menu do, each as one Undo step through the field's SerializedObject,
    /// so it reaches every selected asset (ED-33, Decision 28).
    /// </summary>
    public static class ScalerActions
    {
        private static SerializedProperty ScalerOf(SerializedProperty operation) =>
            operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler));

        /// <summary>
        /// Gives the field a scaler of a kind, which maps levels exactly. A level-based one starts with the value the
        /// field read at level 1, and reads its scaler; a Shared one reads its asset times the field's number, 1.
        /// </summary>
        public static void SetKind(SerializedProperty operation, Type type)
        {
            if (type == null)
            {
                MakeConstant(operation);
                return;
            }
            float first = ValueAtFirstLevel(operation);
            ScalerFields.SetKind(ScalerOf(operation), type, first);
            operation.serializedObject.Update();
            var real = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude));
            var magnitude = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude));
            if (ScalerFields.IsShared(type))
            {
                real.intValue = (int)EMagnitudeOperation.MultiplyWithScaler;
                SetNumber(magnitude, 1f);
            }
            else real.intValue = (int)EMagnitudeOperation.UseScaler;
            operation.serializedObject.ApplyModifiedProperties();
            ScalerFields.ForgetUses();
        }

        /// <summary>The field reads its number again: no scaler, and the number the field read at level 1.</summary>
        public static void MakeConstant(SerializedProperty operation)
        {
            float value = ValueAtFirstLevel(operation);
            var scaler = ScalerOf(operation);
            scaler.managedReferenceValue = null;
            operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude)).intValue = (int)EMagnitudeOperation.UseMagnitude;
            SetNumber(operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude)), value);
            operation.serializedObject.ApplyModifiedProperties();
            ScalerFields.ForgetUses();
        }

        /// <summary>
        /// The field shares a Scaler asset: it reads the asset's scaler, and keeps how it combines it with its number,
        /// unless it read its number alone, when it reads the asset.
        /// </summary>
        public static void UseScalerAsset(SerializedProperty operation, ScalerAsset asset)
        {
            var real = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude));
            var keep = (EMagnitudeOperation)real.intValue;
            // The cached Shared for a field that takes the cached kinds, as an attribute set's elements do
            var shared = ScalerFields.KindsFor(ScalerOf(operation)).Contains(typeof(SharedCachedScaler)) ? typeof(SharedCachedScaler) : typeof(SharedScaler);
            ScalerFields.SetKind(ScalerOf(operation), shared, 0f);
            operation.serializedObject.Update();
            ScalerOf(operation).FindPropertyRelative(nameof(SharedScaler.Asset)).objectReferenceValue = asset;
            real.intValue = (int)(keep == EMagnitudeOperation.UseMagnitude ? EMagnitudeOperation.UseScaler : keep);
            operation.serializedObject.ApplyModifiedProperties();
            ScalerFields.ForgetUses();
        }

        /// <summary>
        /// Saves the field's scaler as a Scaler asset in the folder, named, and makes the field share it, as the scaler
        /// board's Save and Use does. Returns the asset, or null when the field's scaler has no values of its own.
        /// </summary>
        public static ScalerAsset SaveAsScalerAsset(SerializedProperty operation, string name, string folder)
        {
            if (!(ScalerOf(operation).managedReferenceValue is AbstractScaler own) || ScalerFields.IsShared(own.GetType())) return null;
            var asset = ScriptableObject.CreateInstance<ScalerAsset>();
            var copy = (AbstractScaler)Activator.CreateInstance(own.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(own), copy);
            copy.Name = name;
            asset.Scaler = copy;
            folder = string.IsNullOrEmpty(folder) ? ForgeAssetCreation.FolderFor(typeof(ScalerAsset)) : folder.TrimEnd('/');
            ForgeAssetCreation.EnsureFolder(folder);
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{ForgeAssetCreation.FileNameOf(name)}.asset"));
            AssetDatabase.SaveAssets();
            UseScalerAsset(operation, asset);
            return asset;
        }

        /// <summary>Copies values per level into a scaler, as Import Values does, with the curve a stretched one reads.</summary>
        public static void CopyValues(SerializedProperty scaler, float[] values)
        {
            var levelValues = scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
            if (levelValues == null || values == null) return;
            levelValues.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) levelValues.GetArrayElementAtIndex(i).floatValue = values[i];
            scaler.serializedObject.ApplyModifiedProperties();
            ScalerFields.KeepCurve(scaler);
        }

        // What the field reads at level 1, through its Combine: a constant's number, or its scaler's first value
        private static float ValueAtFirstLevel(SerializedProperty operation)
        {
            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude));
            float magnitude = magnitudeProp.propertyType == SerializedPropertyType.Integer ? magnitudeProp.intValue : magnitudeProp.floatValue;
            var scaler = ScalerOf(operation);
            if (scaler.managedReferenceValue == null) return magnitude;
            var values = ScalerFields.ValuesOf(scaler);
            if (values.Length == 0) return magnitude;
            var real = (EMagnitudeOperation)operation.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude)).intValue;
            return ForgeHelper.MagnitudeAndScalerOperation(magnitude, values[0], real);
        }

        private static void SetNumber(SerializedProperty magnitude, float value)
        {
            if (magnitude.propertyType == SerializedPropertyType.Integer) magnitude.intValue = Mathf.RoundToInt(value);
            else magnitude.floatValue = value;
        }
    }
}
