using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute set element, in Docs/EDITOR_STYLE.md's terms: a foldout row with the attribute, and while it's folded
    /// one line of what it starts at; open, what it starts at at each level, then Base and Current, each a magnitude field
    /// as an effect's are, then its bounds, its constraints, its retention group and its collision (ED-49).
    /// <para>
    /// What it starts at is worked out by the runtime (<see cref="AttributeStartValues"/>): a stand-in entity at each level
    /// of the set it's previewed in, with the set's sub-sets. A note says when another element's value is the one used, and
    /// a message when a scaler reads an attribute the set doesn't give. The element showed its magnitudes as a formula, as
    /// "10 + Level", and Current and Base one at a time on a switch.
    /// </para>
    /// It writes nothing while it draws, and only a typed magnitude is kept within the bounds, as part of the same edit
    /// (ED-39).
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeSetElement))]
    public class AttributeSetElementDrawer : PropertyDrawer
    {
        public const string CollapsedClass = "pf-element--collapsed";
        private const string Hidden = "pf-hidden";

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
            var baseMagProp     = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Magnitude));
            var set = property.serializedObject.targetObject as AttributeSet;
            var path = property.propertyPath;

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

            // ─── What it starts at, at each level ───────────────────────────────
            var startsRow = new VisualElement { name = "StartsAtRow" };
            startsRow.AddToClassList("pf-element__starts");
            var starts = ImpactFields.ValueRow("Starts At", "StartsAt", "");
            starts.AddToClassList("pf-value-field--strong");
            starts.AddToClassList("pf-element__starts-value");
            startsRow.Add(starts);
            var levels = new Label { name = "StartsAtLevels" };
            levels.AddToClassList("pf-element__starts-levels");
            startsRow.Add(levels);
            content.Add(startsRow);
            var note = new Label { name = "StartsAtNote" };
            note.AddToClassList("pf-note");
            note.AddToClassList(Hidden);
            content.Add(note);
            var warning = new HelpBox("", HelpBoxMessageType.Warning) { name = "StartsAtWarning" };
            warning.AddToClassList(Hidden);
            content.Add(warning);

            // ─── Base, then Current, which can take Base's settings ────────────
            content.Add(MagnitudeField.Build(baseProp, "Base", null, false, "BaseMagnitude",
                menu => CopyItem(menu, "Copy Current's Settings", currentProp, baseProp, linkProp, true)));
            var linkToggle = new Toggle("Link Current to Base")
            {
                name = "LinkCurrentToBase",
                tooltip = "Current takes Base's magnitude and scaler",
            };
            linkToggle.AddToClassList(Toggle.alignedFieldUssClassName);
            linkToggle.BindProperty(linkProp);
            content.Add(linkToggle);
            var currentField = MagnitudeField.Build(currentProp, "Current", null, false, "CurrentMagnitude",
                menu => CopyItem(menu, "Copy Base's Settings", baseProp, currentProp, linkProp, false));
            currentField.name = "CurrentField";
            content.Add(currentField);

            var outOfBounds = new HelpBox("", HelpBoxMessageType.Warning) { name = "OutOfBounds" };
            content.Add(outOfBounds);

            content.Add(new PropertyField(overflowProp, "Bounds"));
            content.Add(new PropertyField(constraintsProp, "Constraints"));
            content.Add(new PropertyField(retGroupProp, "Retention Group"));
            content.Add(new PropertyField(collisionProp, "Collision"));

            // ─── Behaviour ─────────────────────────────────────────────────────
            // While Current takes Base's settings, its own don't count, so they don't show
            void ShowLinked()
            {
                if (!Alive(property)) return;
                currentField.EnableInClassList(Hidden, linkProp.boolValue && !linkProp.hasMultipleDifferentValues);
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

            // What it starts at, its line, and whether the runtime clamps a magnitude outside the bounds
            void Show()
            {
                if (!Alive(property) || set == null) return;
                var element = ElementOf(set, path);
                var result = AttributeStartValues.Shown(set);
                var start = result.Of(element?.Attribute);
                var overflow = ReadOverflowFromProperty(overflowProp);
                var clamped = Prop(constraintsProp, nameof(AttributeConstraints.AutoClamp))?.boolValue ?? true;
                var range = ForgeAttributeTable.RangeOf(overflow, clamped).text;

                if (result.Problem != null || start == null)
                {
                    starts.SetValueWithoutNotify("—");
                    starts.tooltip = element?.Attribute == null ? "An element with no attribute gives nothing" : "";
                    levels.text = "";
                    summary.text = element?.Attribute == null ? "No attribute" : $"Starts at — · {range}";
                }
                else
                {
                    var text = AttributeStartValues.Text(start);
                    starts.SetValueWithoutNotify(text);
                    starts.tooltip = AttributeStartValues.Detail(start);
                    levels.text = start.Varies ? AttributeStartValues.LevelsText(result.FirstLevel, result.LastLevel) : "";
                    levels.tooltip = result.LastLevel < result.Levels
                        ? $"The first {result.LastLevel - result.FirstLevel + 1} of levels {result.FirstLevel}–{result.Levels}" : "";
                    summary.text = $"Starts at {text} · {range}";
                }

                var said = NoteOf(element, start, result);
                note.text = said ?? "";
                note.EnableInClassList(Hidden, said == null);
                var problem = WarningOf(element, result);
                warning.text = problem ?? "";
                warning.EnableInClassList(Hidden, problem == null);

                var messages = BoundsMessages(overflow, clamped, baseMagProp.floatValue, linkProp.boolValue, curMagProp.floatValue);
                outOfBounds.text = string.Join(" ", messages);
                outOfBounds.EnableInClassList(Hidden, messages.Count == 0);
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
            linkToggle.RegisterValueChangedCallback(_ => ShowLinked());
            // The magnitude fields redraw their number as their kind changes, so their typing is heard as it bubbles up
            content.RegisterCallback<ChangeEvent<float>>(change =>
            {
                if (change.target is not FloatField field) return;
                if (field.name == "CurrentMagnitude") KeepTypedInBounds(true, change.newValue, field);
                else if (field.name == "BaseMagnitude") KeepTypedInBounds(false, change.newValue, field);
            });

            // What it starts at follows the set, an Undo among its changes; the set it's previewed in, as that changes; and
            // the other sets and scalers it reads, as they change elsewhere. It only reads them
            root.TrackSerializedObjectValue(property.serializedObject, _ => { Show(); ShowLinked(); });
            void PreviewChanged(AttributeSet changed)
            {
                if (changed == set) Show();
            }
            void OthersChanged(ref ObjectChangeEventStream stream)
            {
                if (ReadsAChange(ref stream, set)) Show();
            }
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AttributeStartValues.PreviewChanged += PreviewChanged;
                ObjectChangeEvents.changesPublished += OthersChanged;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                AttributeStartValues.PreviewChanged -= PreviewChanged;
                ObjectChangeEvents.changesPublished -= OthersChanged;
            });

            SetFolded(IsCollapsed(property));
            ShowLinked();
            Show();
            return root;
        }

        // The element at a path, read from the set as it is now
        private static AttributeSetElement ElementOf(AttributeSet set, string path)
        {
            const string prefix = nameof(AttributeSet.Attributes) + ".Array.data[";
            if (set == null || set.Attributes == null || !path.StartsWith(prefix)) return null;
            var end = path.IndexOf(']', prefix.Length);
            return end > prefix.Length && int.TryParse(path.Substring(prefix.Length, end - prefix.Length), out int index) &&
                   index >= 0 && index < set.Attributes.Count
                ? set.Attributes[index]
                : null;
        }

        // Whether a change elsewhere is to something the element's values read: another attribute set, a Scaler asset,
        // or an attribute
        private static bool ReadsAChange(ref ObjectChangeEventStream stream, AttributeSet set)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                stream.GetChangeAssetObjectPropertiesEvent(i, out var change);
                var changed = EditorUtility.InstanceIDToObject(change.instanceId);
                if (changed != set && changed is AttributeSet or ScalerAsset or Attribute) return true;
            }
            return false;
        }

        /// <summary>
        /// What the note under Starts At says of an element: that another element gives its attribute too, and whose value
        /// is used, or that the values add up; or null.
        /// </summary>
        internal static string NoteOf(AttributeSetElement element, AttributeStartValues.Start start, AttributeStartValues.Result result)
        {
            if (element?.Attribute == null || start == null || result == null || result.Problem != null) return null;
            var others = start.Elements.Where(other => other != element).ToList();
            if (others.Count == 0) return null;
            var attribute = AttributeStartValues.NameOf(element.Attribute);
            var mine = result.SetOf(element);
            var otherSet = result.SetOf(start.From.Contains(element) ? others[0] : start.From.FirstOrDefault() ?? others[0]);
            var who = otherSet == mine ? $"This set gives {attribute} twice" : $"{AttributeStartValues.NameOf(otherSet)} gives {attribute} too";
            if (!start.From.Contains(element)) return otherSet == mine ? $"{who}, and the other value is the one used." : $"{who}, and its value is the one used.";
            if (start.From.Count > 1)
                return start.From.All(part => part.CollisionPolicy == EAttributeElementCollisionPolicy.Combine)
                    ? $"{who}, and the values add up."
                    : $"{who}, and the values are averaged.";
            return null;
        }

        /// <summary>
        /// What the message under Starts At says of an element: what the runtime threw working out the set's values, or an
        /// attribute its scalers read that the set doesn't give, which reads 0; or null.
        /// </summary>
        internal static string WarningOf(AttributeSetElement element, AttributeStartValues.Result result)
        {
            if (result == null) return null;
            if (result.Problem != null) return $"The values can't be worked out: {result.Problem}";
            var missing = AttributeStartValues.MissingReads(element, result);
            if (missing.Count == 0) return null;
            var names = string.Join(" and ", missing.Select(AttributeStartValues.NameOf));
            var reads = missing.Count == 1 ? "so it reads 0" : "so each reads 0";
            return $"Reads {names}, which {AttributeStartValues.NameOf(result.Root)} doesn't give, {reads}.";
        }

        // The menu item that copies one side's magnitude and scaler into the other, a shared scaler after the copy
        private static void CopyItem(ForgeFieldMenu menu, string label, SerializedProperty from, SerializedProperty to,
            SerializedProperty link, bool intoBase)
        {
            // While Current takes Base's settings, a copy into Current changes nothing it reads
            if (!intoBase && link.boolValue)
            {
                menu.AddDisabledItem(label, false);
                return;
            }
            menu.AddItem(label, false, () =>
            {
                Prop(to, nameof(AttributeMagnitudeSpec.Magnitude)).floatValue = Prop(from, nameof(AttributeMagnitudeSpec.Magnitude)).floatValue;
                Prop(to, nameof(AttributeMagnitudeSpec.RealMagnitude)).enumValueIndex = Prop(from, nameof(AttributeMagnitudeSpec.RealMagnitude)).enumValueIndex;
                Prop(to, nameof(AttributeMagnitudeSpec.Scaling)).managedReferenceValue = Prop(from, nameof(AttributeMagnitudeSpec.Scaling)).managedReferenceValue;
                to.serializedObject.ApplyModifiedProperties();
            });
        }

        private static SerializedProperty Prop(SerializedProperty parent, string name) => parent.FindPropertyRelative(name);

        // Whether a property still reads its asset: a drawer outlives an element removed from its list
        private static bool Alive(SerializedProperty property)
        {
            try
            {
                return property.serializedObject.targetObject != null && property.serializedObject.FindProperty(property.propertyPath) != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // Whether the field has the focus, as it does while a value is typed into it
        private static bool IsTypedInto(VisualElement field) =>
            field.panel?.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        /// <summary>Reconstructs an <see cref="AttributeOverflowData"/> from its serialized form, to use its clamp helpers.</summary>
        internal static AttributeOverflowData ReadOverflowFromProperty(SerializedProperty overflowProp)
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
