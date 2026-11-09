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
    /// A number field (FEAT-29), as the canvas's scaler board drew the magnitude field it grew from (ED-33): its name, then
    /// its kind, Constant or another, and either its number or its range, as in "6–8 s", and its field menu. Another kind
    /// adds a one-line summary of what it reads, how the field combines its number with the kind, then the kind's own
    /// content: its values per level and their curve, its Scaler asset, or its fields. The kind menu files kinds by
    /// category and offers only those the field's context resolves (<see cref="NumberContextAttribute"/>), and a field
    /// whose unit is "%" shows and takes its number as a percentage.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScalableFloat), true)]
    public class ScalableFloatDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            MagnitudeField.Build(property, preferredLabel ?? property.displayName, NumberFields.UnitOf(fieldInfo), integer: false,
                provides: NumberFields.ProvidesOf(fieldInfo));
    }

    /// <summary>A whole-number field, such as Ticks: the same field, with its rounding.</summary>
    [CustomPropertyDrawer(typeof(ScalableInt), true)]
    public class ScalableIntDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            MagnitudeField.Build(property, preferredLabel ?? property.displayName, NumberFields.UnitOf(fieldInfo), integer: true,
                provides: NumberFields.ProvidesOf(fieldInfo));
    }

    internal static class MagnitudeField
    {
        private const string ConstantKind = "Constant";

        // The unit that shows and takes a field's number as a percentage (FEAT-29)
        internal const string Percent = "%";

        // How a field combines its number with its scaler, by the names the Combine popup shows
        private static readonly (string name, EMagnitudeOperation real)[] Combines =
        {
            ("Scaler", EMagnitudeOperation.UseScaler),
            ("Scaler + Number", EMagnitudeOperation.AddScaler),
            ("Scaler × Number", EMagnitudeOperation.MultiplyWithScaler),
            ("Number Only", EMagnitudeOperation.UseMagnitude),
        };

        internal static string UnitOf(FieldInfo field) => NumberFields.UnitOf(field);

        /// <summary>
        /// A magnitude's scaler: an operation's Scaler, or an attribute set element's side's Scaling, which the field
        /// draws the same way (ED-49).
        /// </summary>
        internal static SerializedProperty ScalerOf(SerializedProperty operation) =>
            operation.FindPropertyRelative(nameof(ScalableFloat.Scaler))
            ?? operation.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Scaling));

        // Whether a field that reads its scaler reads 0 without one. An element's side reads its number (AttributeBlueprint)
        private static bool ReadsZeroWithoutScaler(SerializedProperty operation) => operation.type != nameof(AttributeMagnitudeSpec);

        // The range a multiple selection shows when its fields give different values, as a mixed field shows
        internal const string MixedRange = "—";

        /// <param name="operation">The magnitude: an operation, or an attribute set element's side.</param>
        /// <param name="label">The field's label.</param>
        /// <param name="unit">The unit after its number, or null.</param>
        /// <param name="integer">Whether it takes whole numbers.</param>
        /// <param name="numberName">The name its number field takes, so a drawer can find it; or null.</param>
        /// <param name="moreItems">Items the drawer adds to its field menu, before Make Constant; or null.</param>
        /// <param name="provides">What the field's context provides, which kinds its menu offers (FEAT-29).</param>
        internal static VisualElement Build(SerializedProperty operation, string label, string unit, bool integer,
            string numberName = null, Action<GenericDropdownMenu> moreItems = null, NumberReads provides = NumberReads.All)
        {
            var root = new VisualElement { name = "MagnitudeField" };
            // The style sheet lays out the field, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-magnitude");

            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            var realProp = operation.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
            var scalerProp = ScalerOf(operation);
            var roundingProp = operation.FindPropertyRelative(nameof(ScalableInt.Rounding));
            // The kinds the field's context resolves, in the menu's order, each with its place in the menu (FEAT-29)
            var kinds = NumberFields.InMenuOrder(ScalerFields.KindsFor(scalerProp).Where(kind => NumberFields.Resolves(provides, kind)));
            bool percent = unit == Percent && !integer;

            var row = new VisualElement { name = "Row" };
            row.AddToClassList("pf-magnitude__row");
            root.Add(row);
            var body = new VisualElement { name = "Body" };
            body.AddToClassList("pf-magnitude__body");
            root.Add(body);

            Type shownType = null;
            bool mixedKinds = false;
            bool built = false;
            Label range = null;
            Label summary = null;
            HelpBox unresolved = null;
            PopupField<string> combine = null;
            VisualElement combineNumber = null;
            FloatField shownNumber = null;
            HelpBox noScaler = null;

            // The field's properties, found again by their paths. One kept since the field was built can point into a
            // managed reference an Undo has since put back, as after a kind is changed and undone, and resizing a table
            // through it crashed the editor (ED-49)
            var operationPath = operation.propertyPath;
            void Refind()
            {
                var fresh = operation.serializedObject.FindProperty(operationPath);
                if (fresh == null) return;
                operation = fresh;
                magnitudeProp = operation.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
                realProp = operation.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                scalerProp = ScalerOf(operation);
                roundingProp = operation.FindPropertyRelative(nameof(ScalableInt.Rounding));
            }

            // A kind's place in the menu, as "Scaler/Clamped"; Constant and the common kinds sit at its top
            PopupField<string> KindPopup(string fieldLabel, List<string> choices, string shown)
            {
                var paths = new Dictionary<string, string>();
                foreach (var each in kinds) paths.TryAdd(ScalerFields.KindName(each), NumberFields.MenuPathOf(each));
                if (shownType != null) paths.TryAdd(shown, NumberFields.MenuPathOf(shownType));
                var popup = fieldLabel == null
                    ? new PopupField<string>(choices, shown)
                    : new PopupField<string>(fieldLabel, choices, shown);
                popup.name = "Kind";
                popup.tooltip = "The field's kind: Constant, or another";
                popup.formatSelectedValueCallback = name => name;
                popup.formatListItemCallback = name => paths.TryGetValue(name, out var path) ? path : name;
                return popup;
            }

            // A number field: bound to the number, or, where the field's unit is "%", showing and taking it times 100
            VisualElement NumberField(string fieldLabel)
            {
                if (!percent)
                    return integer
                        ? Bound(fieldLabel == null ? new IntegerField() : new IntegerField(fieldLabel), magnitudeProp)
                        : Bound(fieldLabel == null ? new FloatField() : new FloatField(fieldLabel), magnitudeProp);
                var shown = fieldLabel == null ? new FloatField() : new FloatField(fieldLabel);
                shown.tooltip = "As a percentage: 30 is 0.3";
                shown.SetValueWithoutNotify(magnitudeProp.floatValue * 100f);
                shown.showMixedValue = magnitudeProp.hasMultipleDifferentValues;
                shown.RegisterValueChangedCallback(change =>
                {
                    if (!ScalerFields.Alive(magnitudeProp)) return;
                    magnitudeProp.floatValue = change.newValue / 100f;
                    operation.serializedObject.ApplyModifiedProperties();
                });
                shownNumber = shown;
                return shown;
            }

            void Draw()
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                Refind();
                row.Clear();
                body.Clear();
                range = null;
                summary = null;
                unresolved = null;
                combine = null;
                combineNumber = null;
                shownNumber = null;
                noScaler = null;
                // Selected fields whose kinds differ show a mixed kind, and neither field's content as both's (ED-12)
                mixedKinds = EachAsset.Differ(scalerProp, ScalerFields.KindOf);
                shownType = ScalerFields.KindOf(scalerProp);
                built = true;

                var choices = new List<string> { ConstantKind };
                choices.AddRange(kinds.Select(ScalerFields.KindName));
                var shownName = shownType == null ? ConstantKind : ScalerFields.KindName(shownType);
                // A kind the field's context doesn't resolve still shows, as one copied from another field does
                if (!choices.Contains(shownName)) choices.Add(shownName);

                PopupField<string> kind;
                if (mixedKinds)
                {
                    kind = KindPopup(label, choices, shownName);
                    kind.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                    kind.AddToClassList("pf-magnitude__kind-labelled");
                    kind.showMixedValue = true;
                    row.Add(kind);
                    var note = new Label("The selected fields' kinds differ. Choose one to give it to each.") { name = "MixedKinds" };
                    note.AddToClassList("pf-note");
                    body.Add(note);
                }
                else if (shownType == null)
                {
                    // Constant: the number, its unit, then the kind
                    var number = NumberField(label);
                    number.name = numberName ?? "Number";
                    number.AddToClassList(BaseField<float>.alignedFieldUssClassName);
                    number.AddToClassList("pf-magnitude__number");
                    row.Add(number);
                    if (!string.IsNullOrEmpty(unit))
                    {
                        var unitLabel = new Label(unit) { name = "Unit" };
                        unitLabel.AddToClassList("pf-magnitude__unit");
                        row.Add(unitLabel);
                    }
                    kind = KindPopup(null, choices, shownName);
                    kind.AddToClassList("pf-magnitude__kind");
                    row.Add(kind);

                    // With no scaler, a field that reads its scaler reads 0
                    if (ReadsZeroWithoutScaler(operation))
                    {
                        noScaler = new HelpBox("With no scaler, this field reads 0. Make it Constant to read its number.", HelpBoxMessageType.Warning)
                        {
                            name = "NoScaler",
                        };
                        body.Add(noScaler);
                    }
                }
                else
                {
                    // Another kind: the kind beside the name, then the range it gives, and under them what it reads
                    kind = KindPopup(label, choices, shownName);
                    kind.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                    kind.AddToClassList("pf-magnitude__kind-labelled");
                    row.Add(kind);
                    range = new Label { name = "Range" };
                    range.AddToClassList("pf-magnitude__range");
                    row.Add(range);
                    summary = new Label { name = "Summary" };
                    summary.AddToClassList("pf-magnitude__summary");
                    body.Add(summary);

                    // A kind its field's context can't resolve, as one copied from another field, says so in place
                    unresolved = new HelpBox("", HelpBoxMessageType.Warning) { name = "Unresolved" };
                    body.Add(unresolved);

                    // Everything the scaler sets for the field sits on one block under it, so its fields read as the
                    // scaler's among the fields around them (ED-51, Decision 36)
                    var block = new VisualElement { name = "ScalerBlock" };
                    block.AddToClassList(ScalerFields.BlockClass);
                    body.Add(block);

                    // How the number joins the scaler
                    var combineRow = new VisualElement { name = "CombineRow" };
                    combineRow.AddToClassList("pf-magnitude__combine");
                    combine = new PopupField<string>("Combine", Combines.Select(each => each.name).ToList(), CombineIndex())
                    {
                        name = "Combine",
                        tooltip = "What the field reads: the scaler's value, that plus or times the field's number, or the number alone",
                    };
                    combine.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                    combine.RegisterValueChangedCallback(change =>
                    {
                        var chosen = Combines.First(each => each.name == change.newValue).real;
                        if (!realProp.hasMultipleDifferentValues && realProp.intValue == (int)chosen) return;
                        realProp.intValue = (int)chosen;
                        operation.serializedObject.ApplyModifiedProperties();
                    });
                    combineRow.Add(combine);
                    combineNumber = NumberField(null);
                    combineNumber.name = numberName ?? "CombineNumber";
                    combineNumber.AddToClassList("pf-magnitude__combine-number");
                    combineRow.Add(combineNumber);
                    block.Add(combineRow);

                    if (integer && roundingProp != null)
                    {
                        var rounding = new EnumField("Rounding") { name = "Rounding", tooltip = "How the value becomes a whole number" };
                        rounding.AddToClassList(EnumField.alignedFieldUssClassName);
                        rounding.BindProperty(roundingProp);
                        block.Add(rounding);
                    }

                    block.Add(ScalerFields.Content(scalerProp));
                }

                kind.RegisterValueChangedCallback(change =>
                {
                    // A mixed kind takes the one chosen, even the first field's
                    if (!mixedKinds && change.newValue == change.previousValue) return;
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
                menu.clicked += () => ShowMenu(operation, menu, label, moreItems);
                row.Add(menu);
                Show();
            }

            int CombineIndex() => Mathf.Max(0, Array.FindIndex(Combines, each => (int)each.real == realProp.intValue));

            // The range, Combine and the number's place in it, and whether a scaler is missing, which follow the asset. A
            // multiple selection shows a mixed value where the fields differ, and a part any of them uses (ED-12)
            void Show()
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                var reals = EachAsset.Of(realProp).Select(each => (EMagnitudeOperation)each.intValue).ToList();
                if (range != null)
                {
                    bool differ = EachAsset.Differ(operation, each => RangeOf(each, unit));
                    range.text = differ ? MixedRange : RangeOf(operation, unit);
                    range.tooltip = differ ? "The selected fields give different values" : "";
                    // A kind with no values per level has no range; its summary says what it reads
                    range.EnableInClassList(ScalerFields.HiddenClass, range.text.Length == 0);
                }
                if (summary != null)
                {
                    bool differ = EachAsset.Differ(operation, each => SummaryOf(each, unit));
                    summary.text = differ ? MixedRange : SummaryOf(operation, unit);
                    summary.tooltip = differ ? "The selected fields read different things" : summary.text;
                }
                if (unresolved != null)
                {
                    var missing = EachAsset.Of(scalerProp)
                        .Select(each => NumberFields.Missing(NumberFields.ReadsOf(each.managedReferenceValue as AbstractScaler), provides))
                        .FirstOrDefault(each => each.Length > 0) ?? "";
                    unresolved.text = $"Its kind reads {missing}, which this field doesn't have, so it reads its number.";
                    unresolved.EnableInClassList(ScalerFields.HiddenClass, missing.Length == 0);
                }
                if (shownNumber != null && ScalerFields.Alive(magnitudeProp) && !ScalerFields.IsTypedInto(shownNumber))
                {
                    shownNumber.SetValueWithoutNotify(magnitudeProp.floatValue * 100f);
                    shownNumber.showMixedValue = magnitudeProp.hasMultipleDifferentValues;
                }
                if (combine != null)
                {
                    combine.SetValueWithoutNotify(Combines[CombineIndex()].name);
                    combine.showMixedValue = realProp.hasMultipleDifferentValues;
                }
                if (combineNumber != null)
                    combineNumber.style.display = reals.Any(real => real != EMagnitudeOperation.UseScaler) ? DisplayStyle.Flex : DisplayStyle.None;
                if (noScaler != null)
                    noScaler.style.display = reals.Any(real => real is EMagnitudeOperation.UseScaler or EMagnitudeOperation.MultiplyWithScaler)
                        ? DisplayStyle.Flex : DisplayStyle.None;
            }

            root.TrackSerializedObjectValue(operation.serializedObject, _ =>
            {
                if (!ScalerFields.Alive(scalerProp)) return;
                // A kind changed elsewhere, as by an Undo, redraws the field; otherwise it only updates what it reads
                bool mixed = EachAsset.Differ(scalerProp, ScalerFields.KindOf);
                if (!built || mixed != mixedKinds || ScalerFields.KindOf(scalerProp) != shownType) Draw();
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
        /// What a field reads across its scaler's levels, through its Combine, as in "6–8 s"; empty for a kind with no
        /// values per level, whose summary says what it reads (FEAT-29).
        /// </summary>
        internal static string RangeOf(SerializedProperty operation, string unit)
        {
            var scalerProp = ScalerOf(operation);
            var values = ScalerFields.ValuesOf(scalerProp);
            // A kind that reads no values of its own, as one that reads an attribute, has no range: its values per level,
            // 1 unless set, aren't what it gives (ED-49)
            var kind = scalerProp.managedReferenceValue as AbstractScaler;
            bool ownValues = kind != null && (ScalerFields.IsShared(kind.GetType()) || (kind.Parts & EScalerParts.LevelValues) != 0);
            if (values.Length == 0 || !ownValues) return "";
            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            float magnitude = magnitudeProp.propertyType == SerializedPropertyType.Integer ? magnitudeProp.intValue : magnitudeProp.floatValue;
            var real = (EMagnitudeOperation)operation.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude)).intValue;
            var read = values.Select(value => ForgeHelper.MagnitudeAndScalerOperation(magnitude, value, real)).ToArray();
            if (unit == Percent) return ScalerFields.RangeText(read.Select(value => value * 100f).ToArray(), Percent).Replace(" %", "%");
            return ScalerFields.RangeText(read, unit);
        }

        /// <summary>
        /// A number field's one-line summary of what it reads, as "0.3 × Target's Health (base)" (FEAT-29), from its
        /// number, its Combine and its kind.
        /// </summary>
        internal static string SummaryOf(SerializedProperty operation, string unit)
        {
            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            var realProp = operation.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
            if (magnitudeProp == null || realProp == null) return "";
            float magnitude = magnitudeProp.propertyType == SerializedPropertyType.Integer ? magnitudeProp.intValue : magnitudeProp.floatValue;
            var kind = ScalerOf(operation)?.managedReferenceValue as AbstractScaler;
            return NumberText.Of(magnitude, (EMagnitudeOperation)realProp.intValue, kind, unit);
        }

        // The field menu: Save as Scaler Asset, Use a Scaler Asset, Import Values, Quick Fill, the drawer's own items,
        // then Make Constant
        private static void ShowMenu(SerializedProperty operation, VisualElement anchor, string label, Action<GenericDropdownMenu> moreItems)
        {
            var scalerProp = ScalerOf(operation);
            // Fields whose kinds differ have no one table to fill (ED-12)
            bool mixedKinds = EachAsset.Differ(scalerProp, ScalerFields.KindOf);
            var scaler = mixedKinds ? null : scalerProp.managedReferenceValue as AbstractScaler;
            bool ownValues = scaler != null && !ScalerFields.IsShared(scaler.GetType()) && (scaler.Parts & EScalerParts.LevelValues) != 0;
            var menu = new GenericDropdownMenu();

            // Saving keeps one field's values, so it waits for one selected asset (ED-12)
            if (ownValues && !operation.serializedObject.isEditingMultipleObjects)
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

            if (moreItems != null)
            {
                menu.AddSeparator("");
                moreItems(menu);
            }

            menu.AddSeparator("");
            if (EachAsset.Of(scalerProp).Any(each => each.managedReferenceValue != null))
                menu.AddItem("Make Constant", false, () => ScalerActions.MakeConstant(operation));
            else menu.AddDisabledItem("Make Constant", false);
            // At the button, as wide as its items: anchored alone, it took the button's 20 px and no item could be read (ED-52)
            menu.DropDown(anchor.worldBound, anchor, true, true);
        }

        // A name for what the field holds, as "Burning Duration"
        internal static string DefaultName(SerializedProperty operation, string label)
        {
            var owner = ForgeImportWindow.NameOf(operation.serializedObject.targetObject);
            return string.IsNullOrEmpty(owner) ? label : $"{owner} {label}";
        }
    }

    /// <summary>
    /// What a magnitude field's kind menu and field menu do, each as one Undo step that reaches every selected asset. Each
    /// asset's field changes from its own values, through a SerializedObject of its own (ED-12, ED-33, Decision 28).
    /// </summary>
    public static class ScalerActions
    {
        private static SerializedProperty ScalerOf(SerializedProperty operation) => MagnitudeField.ScalerOf(operation);

        /// <summary>
        /// Gives the field a scaler of a kind, which maps levels exactly. A level-based one starts with the value the
        /// field read at level 1, and reads its scaler; a Shared one reads its asset times the field's number, 1. A field
        /// that has the kind keeps its scaler.
        /// </summary>
        public static void SetKind(SerializedProperty operation, Type type)
        {
            if (type == null)
            {
                MakeConstant(operation);
                return;
            }
            EachAsset.Write(operation, own =>
            {
                var scaler = ScalerOf(own);
                if (ScalerFields.KindOf(scaler) == type) return;
                float first = ValueAtFirstLevel(own);
                scaler.managedReferenceValue = ScalerFields.Make(type, first);
                var real = own.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                if (ScalerFields.IsShared(type))
                {
                    real.intValue = (int)EMagnitudeOperation.MultiplyWithScaler;
                    SetNumber(own.FindPropertyRelative(nameof(ScalableFloat.Magnitude)), 1f);
                }
                else if (ReadsTimesNumber(type))
                {
                    // A quantity, as × Attribute or a distance, reads times the number: the field's own, else 1 (FEAT-29)
                    real.intValue = (int)EMagnitudeOperation.MultiplyWithScaler;
                    SetNumber(own.FindPropertyRelative(nameof(ScalableFloat.Magnitude)), Mathf.Approximately(first, 0f) ? 1f : first);
                }
                else real.intValue = (int)EMagnitudeOperation.UseScaler;
            });
            ScalerFields.ForgetUses();
        }

        // The kinds the menu makes as "the number times" them: × Attribute and a project's own kinds, as a distance. Set By
        // Caller reads the caller's value as it is
        private static bool ReadsTimesNumber(Type type) =>
            type == typeof(AttributeBackedScaler) || (typeof(NumberKind).IsAssignableFrom(type) && type != typeof(SetByCallerScaler));

        /// <summary>The field reads its number again: no scaler, and the number the field read at level 1.</summary>
        public static void MakeConstant(SerializedProperty operation)
        {
            EachAsset.Write(operation, own =>
            {
                float value = ValueAtFirstLevel(own);
                ScalerOf(own).managedReferenceValue = null;
                own.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude)).intValue = (int)EMagnitudeOperation.UseMagnitude;
                SetNumber(own.FindPropertyRelative(nameof(ScalableFloat.Magnitude)), value);
            });
            ScalerFields.ForgetUses();
        }

        /// <summary>
        /// The field shares a Scaler asset: it reads the asset's scaler, and keeps how it combines it with its number,
        /// unless it read its number alone, when it reads the asset.
        /// </summary>
        public static void UseScalerAsset(SerializedProperty operation, ScalerAsset asset)
        {
            // The cached Shared for a field that takes the cached kinds, as an attribute set's elements do
            var shared = ScalerFields.KindsFor(ScalerOf(operation)).Contains(typeof(SharedCachedScaler)) ? typeof(SharedCachedScaler) : typeof(SharedScaler);
            EachAsset.Write(operation, own =>
            {
                var real = own.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                var keep = (EMagnitudeOperation)real.intValue;
                var made = ScalerFields.Make(shared, 0f);
                switch (made)
                {
                    case SharedScaler each: each.Asset = asset; break;
                    case SharedCachedScaler each: each.Asset = asset; break;
                }
                ScalerOf(own).managedReferenceValue = made;
                real.intValue = (int)(keep == EMagnitudeOperation.UseMagnitude ? EMagnitudeOperation.UseScaler : keep);
            });
            ScalerFields.ForgetUses();
        }

        /// <summary>
        /// Saves the field's scaler as a Scaler asset in the folder, named, and makes the field share it, as the scaler
        /// board's Save and Use does; for a multiple selection, the first asset's scaler, which every selected field then
        /// shares. Returns the asset, or null when the field's scaler has no values of its own.
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
            if (values == null || scaler.FindPropertyRelative(nameof(AbstractScaler.LevelValues)) == null) return;
            EachAsset.Write(scaler, own =>
            {
                var levelValues = own.FindPropertyRelative(nameof(AbstractScaler.LevelValues));
                if (levelValues == null) return;
                levelValues.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) levelValues.GetArrayElementAtIndex(i).floatValue = values[i];
            });
            ScalerFields.KeepCurve(scaler);
        }

        // What the field reads at level 1, through its Combine: a constant's number, or its scaler's first value
        private static float ValueAtFirstLevel(SerializedProperty operation)
        {
            var magnitudeProp = operation.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            float magnitude = magnitudeProp.propertyType == SerializedPropertyType.Integer ? magnitudeProp.intValue : magnitudeProp.floatValue;
            var scaler = ScalerOf(operation);
            if (scaler.managedReferenceValue == null) return magnitude;
            var values = ScalerFields.ValuesOf(scaler);
            if (values.Length == 0) return magnitude;
            var real = (EMagnitudeOperation)operation.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude)).intValue;
            return ForgeHelper.MagnitudeAndScalerOperation(magnitude, values[0], real);
        }

        private static void SetNumber(SerializedProperty magnitude, float value)
        {
            if (magnitude.propertyType == SerializedPropertyType.Integer) magnitude.intValue = Mathf.RoundToInt(value);
            else magnitude.floatValue = value;
        }
    }
}
