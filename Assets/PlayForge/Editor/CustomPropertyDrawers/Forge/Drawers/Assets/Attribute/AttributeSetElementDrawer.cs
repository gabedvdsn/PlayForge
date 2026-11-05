using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute set element, in Docs/EDITOR_STYLE.md's terms: a foldout row with the attribute, and while it's folded
    /// one line of how it starts; open, its retention group and collision, then Current and Base, one at a time, each with
    /// its Magnitude, Real Magnitude and Scaling, then its bounds and constraints. It takes its look from the style sheet:
    /// it drew a tinted, rounded box with a border in its collision policy's color, tab buttons and 9 px text (ED-39).
    /// It writes nothing while it draws. It clamped a magnitude outside the bounds into the asset 50 ms after it drew, and
    /// on every change to the set; a help box says how the runtime clamps it, and only a typed magnitude is kept within the
    /// bounds, as part of the same edit (ED-39).
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeSetElement))]
    public class AttributeSetElementDrawer : PropertyDrawer
    {
        public const string CollapsedClass = "pf-element--collapsed";

        // Kept per asset for the editor session (ED-18)
        private static bool IsCollapsed(SerializedProperty property) =>
            DrawerCollapseState.IsCollapsed(property, collapsedByDefault: false);
        private static void SetCollapsed(SerializedProperty property, bool collapsed) =>
            DrawerCollapseState.SetCollapsed(property, collapsed);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AttributeSetElementRoot" };
            // The style sheet lays out the element, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-element");

            var attributeProp   = property.FindPropertyRelative(nameof(AttributeSetElement.Attribute));
            var currentProp     = property.FindPropertyRelative(nameof(AttributeSetElement.Current));
            var baseProp        = property.FindPropertyRelative(nameof(AttributeSetElement.Base));
            var linkProp        = property.FindPropertyRelative(nameof(AttributeSetElement.LinkCurrentToBase));
            var overflowProp    = property.FindPropertyRelative(nameof(AttributeSetElement.Overflow));
            var collisionProp   = property.FindPropertyRelative(nameof(AttributeSetElement.CollisionPolicy));
            var constraintsProp = property.FindPropertyRelative(nameof(AttributeSetElement.Constraints));
            var retGroupProp    = property.FindPropertyRelative(nameof(AttributeSetElement.RetentionGroup));

            var curMagProp      = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Magnitude));
            var curScalingProp  = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Scaling));
            var curRealMagProp  = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.RealMagnitude));
            var baseMagProp     = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Magnitude));
            var baseScalingProp = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Scaling));
            var baseRealMagProp = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.RealMagnitude));

            // ─── The foldout row: the arrow and the attribute ───────────────────
            var header = new VisualElement { name = "HeaderRow" };
            header.AddToClassList("pf-element__header");
            root.Add(header);
            var arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, "Collapse", null) { name = "CollapseBtn" };
            arrow.AddToClassList("pf-element__arrow");
            header.Add(arrow);
            var attributeField = new PropertyField(attributeProp, "") { name = "Attribute" };
            attributeField.AddToClassList("pf-element__attribute");
            header.Add(attributeField);

            // The one line a folded element shows
            var summary = new Label { name = "Summary" };
            summary.AddToClassList("pf-element__summary");
            root.Add(summary);

            var content = new VisualElement { name = "ExpandedContent" };
            content.AddToClassList("pf-element__content");
            root.Add(content);

            content.Add(new PropertyField(retGroupProp, "Retention Group"));
            content.Add(new PropertyField(collisionProp, "Collision"));
            var linkToggle = new Toggle("Link Current to Base")
            {
                name = "LinkCurrentToBase",
                tooltip = "Current takes Base's Magnitude, Real Magnitude and Scaling, and its own are disabled",
            };
            linkToggle.AddToClassList(Toggle.alignedFieldUssClassName);
            linkToggle.BindProperty(linkProp);
            content.Add(linkToggle);

            // ─── Current and Base, one at a time, on a two-button switch ─────────
            var sides = new VisualElement { name = "Sides" };
            sides.AddToClassList("pf-segmented");
            var currentTab = new Button { name = "CurrentTab", text = "Current", focusable = false };
            var baseTab = new Button { name = "BaseTab", text = "Base", focusable = false };
            sides.Add(currentTab);
            sides.Add(baseTab);
            content.Add(sides);

            var linkedNote = CreateNote("Current takes Base's settings.");
            linkedNote.name = "LinkedNote";
            content.Add(linkedNote);
            var currentSide = Side("Current", curMagProp, curRealMagProp, curScalingProp);
            var baseSide = Side("Base", baseMagProp, baseRealMagProp, baseScalingProp);
            content.Add(currentSide);
            content.Add(baseSide);

            // Copy the other side's Magnitude, or all three of its settings, into the side shown
            var copyRow = new VisualElement { name = "CopyRow" };
            copyRow.AddToClassList("pf-button-row");
            var copyMagnitude = new Button { name = "CopyMagnitude", focusable = false };
            var copyAll = new Button { name = "CopyAll", focusable = false };
            copyRow.Add(copyMagnitude);
            copyRow.Add(copyAll);
            content.Add(copyRow);

            var outOfBounds = new HelpBox("", HelpBoxMessageType.Warning) { name = "OutOfBounds" };
            content.Add(outOfBounds);

            content.Add(new PropertyField(overflowProp, "Bounds"));
            content.Add(new PropertyField(constraintsProp, "Constraints"));

            // ─── Behaviour ─────────────────────────────────────────────────────
            bool currentShown = true;
            void ShowSide(bool current)
            {
                currentShown = current;
                currentSide.style.display = current ? DisplayStyle.Flex : DisplayStyle.None;
                baseSide.style.display = current ? DisplayStyle.None : DisplayStyle.Flex;
                currentTab.EnableInClassList("pf-segmented__selected", current);
                baseTab.EnableInClassList("pf-segmented__selected", !current);
                var other = current ? "Base" : "Current";
                copyMagnitude.text = $"Copy {other}'s Magnitude";
                copyMagnitude.tooltip = $"Copy {other}'s Magnitude into {(current ? "Current" : "Base")}";
                copyAll.text = $"Copy {other}'s Settings";
                copyAll.tooltip = $"Copy {other}'s Magnitude, Real Magnitude and Scaling into {(current ? "Current" : "Base")}";
                ShowLinked();
            }

            // While Current takes Base's settings, Current's own are disabled, and copying between them does nothing
            void ShowLinked()
            {
                var linked = linkProp.boolValue;
                currentSide.SetEnabled(!linked);
                linkedNote.style.display = linked && currentShown ? DisplayStyle.Flex : DisplayStyle.None;
                copyMagnitude.SetEnabled(!linked);
                copyAll.SetEnabled(!linked);
            }

            void Copy(bool all)
            {
                var (from, to) = currentShown ? (baseProp, currentProp) : (currentProp, baseProp);
                Prop(to, nameof(AttributeMagnitudeSpec.Magnitude)).floatValue = Prop(from, nameof(AttributeMagnitudeSpec.Magnitude)).floatValue;
                if (all)
                {
                    Prop(to, nameof(AttributeMagnitudeSpec.RealMagnitude)).enumValueIndex = Prop(from, nameof(AttributeMagnitudeSpec.RealMagnitude)).enumValueIndex;
                    // A managed reference: both sides share the one scaler after the copy
                    Prop(to, nameof(AttributeMagnitudeSpec.Scaling)).managedReferenceValue = Prop(from, nameof(AttributeMagnitudeSpec.Scaling)).managedReferenceValue;
                }
                property.serializedObject.ApplyModifiedProperties();
            }

            // A typed magnitude stays within the bounds, as part of the same edit; a change to Base keeps Current within
            // the bounds Base sets. Nothing else writes: it clamped on every change to the set, and as it drew (ED-39)
            void KeepTypedInBounds(bool current, float typed, FloatField field)
            {
                // Only a value typed into the field: binding the field sends it change events of its own as it draws
                if (!IsTypedInto(field)) return;
                if (!(Prop(constraintsProp, nameof(AttributeConstraints.AutoClamp))?.boolValue ?? true)) return;
                var overflow = ReadOverflowFromProperty(overflowProp);
                var baseValue = current ? baseMagProp.floatValue : overflow.ClampBase(typed);
                var kept = current ? overflow.ClampCurrent(typed, baseValue) : baseValue;
                if (!Mathf.Approximately(kept, typed)) field.value = kept;
                if (current) return;
                var currentKept = overflow.ClampCurrent(curMagProp.floatValue, kept);
                if (!Mathf.Approximately(currentKept, curMagProp.floatValue))
                {
                    curMagProp.floatValue = currentKept;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            // How the element starts, and whether the runtime clamps a magnitude outside the bounds
            void Show()
            {
                var overflow = ReadOverflowFromProperty(overflowProp);
                var clamped = Prop(constraintsProp, nameof(AttributeConstraints.AutoClamp))?.boolValue ?? true;
                var currentText = linkProp.boolValue ? "Current takes Base's" : $"Current {SideText(curMagProp, curScalingProp, curRealMagProp)}";
                summary.text = $"{currentText} · Base {SideText(baseMagProp, baseScalingProp, baseRealMagProp)} · {ForgeAttributeTable.RangeOf(overflow, clamped).text}";

                var messages = BoundsMessages(overflow, clamped, baseMagProp.floatValue, linkProp.boolValue, curMagProp.floatValue);
                outOfBounds.text = string.Join(" ", messages);
                outOfBounds.style.display = messages.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void SetFolded(bool folded)
            {
                root.EnableInClassList(CollapsedClass, folded);
                arrow.Glyph.Shape = folded ? ForgeGlyphShape.ArrowRight : ForgeGlyphShape.ArrowDown;
                arrow.tooltip = folded ? "Expand" : "Collapse";
            }

            arrow.clicked += () =>
            {
                var folded = !IsCollapsed(property);
                SetCollapsed(property, folded);
                SetFolded(folded);
            };
            currentTab.clicked += () => ShowSide(true);
            baseTab.clicked += () => ShowSide(false);
            copyMagnitude.clicked += () => Copy(false);
            copyAll.clicked += () => Copy(true);
            linkToggle.RegisterValueChangedCallback(_ => ShowLinked());
            currentSide.Q<FloatField>("CurrentMagnitude").RegisterValueChangedCallback(change =>
                KeepTypedInBounds(true, change.newValue, (FloatField)change.target));
            baseSide.Q<FloatField>("BaseMagnitude").RegisterValueChangedCallback(change =>
                KeepTypedInBounds(false, change.newValue, (FloatField)change.target));

            // The line and the help box follow the set, an Undo among its changes; they only read it
            root.TrackSerializedObjectValue(property.serializedObject, _ => { Show(); ShowLinked(); });

            SetFolded(IsCollapsed(property));
            ShowSide(true);
            Show();
            return root;
        }

        private static SerializedProperty Prop(SerializedProperty parent, string name) => parent.FindPropertyRelative(name);

        // Whether the field has the focus, as it does while a value is typed into it
        private static bool IsTypedInto(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        // One side's Magnitude, Real Magnitude and Scaling
        private static VisualElement Side(string side, SerializedProperty magnitude, SerializedProperty real, SerializedProperty scaling)
        {
            var box = new VisualElement { name = $"{side}Side" };
            var magnitudeField = new FloatField("Magnitude") { name = $"{side}Magnitude", tooltip = $"The value {side} starts at" };
            magnitudeField.AddToClassList(FloatField.alignedFieldUssClassName);
            magnitudeField.BindProperty(magnitude);
            box.Add(magnitudeField);
            box.Add(new PropertyField(real, "Real Magnitude"));
            box.Add(new PropertyField(scaling, "Scaling"));
            return box;
        }

        /// <summary>Reconstructs an <see cref="AttributeOverflowData"/> from its serialized form, to use its clamp helpers.</summary>
        private static AttributeOverflowData ReadOverflowFromProperty(SerializedProperty overflowProp)
        {
            var policyProp = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Policy));
            var floorProp  = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Floor));
            var ceilProp   = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Ceil));

            return new AttributeOverflowData
            {
                Policy = (EAttributeOverflowPolicy)policyProp.enumValueIndex,
                Floor = new AttributeValue(
                    floorProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue)).floatValue,
                    floorProp.FindPropertyRelative(nameof(AttributeValue.BaseValue)).floatValue),
                Ceil = new AttributeValue(
                    ceilProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue)).floatValue,
                    ceilProp.FindPropertyRelative(nameof(AttributeValue.BaseValue)).floatValue),
            };
        }

        // A side as the line shows it: its Magnitude, or how it combines with its scaler, as in "10 + Level"
        private static string SideText(SerializedProperty magProp, SerializedProperty scalingProp, SerializedProperty realMagProp)
        {
            var magnitude = Number(magProp.floatValue);
            var scaler = scalingProp.managedReferenceValue is AbstractCachedScaler s ? ScalerName(s) : null;
            var real = (EMagnitudeOperation)realMagProp.enumValueIndex;
            if (string.IsNullOrEmpty(scaler) || real == EMagnitudeOperation.UseMagnitude) return magnitude;
            return real switch
            {
                EMagnitudeOperation.AddScaler => $"{magnitude} + {scaler}",
                EMagnitudeOperation.MultiplyWithScaler => $"{magnitude} × {scaler}",
                EMagnitudeOperation.UseScaler => scaler,
                _ => magnitude,
            };
        }

        private static string ScalerName(AbstractCachedScaler scaler)
        {
            var name = scaler.GetType().Name;
            if (name.StartsWith("Cached")) name = name.Substring("Cached".Length);
            if (name.EndsWith("Scaler")) name = name.Substring(0, name.Length - "Scaler".Length);
            return name.Length > 0 ? ObjectNames.NicifyVariableName(name) : "Scaler";
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// What the help box says of an element whose magnitudes are outside its bounds: that the runtime clamps them. The
        /// validator says the same, so the Problems view and the element agree (ED-15).
        /// </summary>
        internal static List<string> BoundsMessages(AttributeOverflowData overflow, bool clamped, float storedBase, bool linked, float storedCurrent)
        {
            var messages = new List<string>();
            if (!clamped) return messages;
            float keptBase = overflow.ClampBase(storedBase);
            if (!Mathf.Approximately(storedBase, keptBase))
                messages.Add($"Base {Number(storedBase)} is outside its bounds, so the runtime clamps it to {Number(keptBase)}.");
            if (linked) return messages;
            float keptCurrent = overflow.ClampCurrent(storedCurrent, keptBase);
            if (!Mathf.Approximately(storedCurrent, keptCurrent))
                messages.Add($"Current {Number(storedCurrent)} is outside its bounds, so the runtime clamps it to {Number(keptCurrent)}.");
            return messages;
        }
    }
}
