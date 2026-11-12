using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// One of an effect's additional impacts, in Docs/EDITOR_STYLE.md's terms: a foldout row with its attribute and a
    /// Remove button, and while it's folded one line such as "Mana −10 · Continuous"; open, its operation, magnitude and
    /// delivery, then its order, retention group and impact types. Its Delivery offers what the effect's duration can
    /// make, and a help box says when the one it names can't happen. An impact entry had no drawer, so an effect couldn't
    /// be given a second impact (ED-3).
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeImpactEntry))]
    public class AttributeImpactEntryDrawer : PropertyDrawer
    {
        public const string CollapsedClass = "pf-element--collapsed";

        // Kept per asset for the editor session (ED-18)
        private static bool IsCollapsed(SerializedProperty property) =>
            DrawerCollapseState.IsCollapsed(property, collapsedByDefault: false);
        private static void SetCollapsed(SerializedProperty property, bool collapsed) =>
            DrawerCollapseState.SetCollapsed(property, collapsed);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "ImpactEntry" };
            // The style sheet lays out the entry, wherever the drawer is drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-element");

            var attributeProp = property.FindPropertyRelative(nameof(AttributeImpactEntry.AttributeTarget));
            var deliveryProp  = property.FindPropertyRelative(nameof(AttributeImpactEntry.Delivery));
            var slotProp      = property.FindPropertyRelative(nameof(AttributeImpactEntry.TargetSlot));
            var operationProp = property.FindPropertyRelative(nameof(AttributeImpactEntry.Operation));
            var multiplyProp  = property.FindPropertyRelative(nameof(AttributeImpactEntry.MultiplyBehaviour));
            var addProp = property.FindPropertyRelative(nameof(AttributeImpactEntry.AddBehaviour));
            var orderProp     = property.FindPropertyRelative(nameof(AttributeImpactEntry.SortOrder));
            var magnitudeProp = property.FindPropertyRelative(nameof(AttributeImpactEntry.Magnitude));
            var groupProp     = property.FindPropertyRelative(nameof(AttributeImpactEntry.RetentionGroupOverride));
            var typesProp     = property.FindPropertyRelative(nameof(AttributeImpactEntry.ImpactTypes));

            // ─── The foldout row: the arrow, the attribute and Remove ────────────
            var header = new VisualElement { name = "HeaderRow" };
            header.AddToClassList("pf-element__header");
            root.Add(header);
            var arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, "Collapse", null) { name = "CollapseBtn" };
            arrow.AddToClassList("pf-element__arrow");
            header.Add(arrow);
            var attributeField = new PropertyField(attributeProp, "") { name = "Attribute" };
            attributeField.AddToClassList("pf-element__attribute");
            header.Add(attributeField);
            // Its delivery, named on the right of its row, as EDITOR_STYLE has an entry's (Q19). It named none (ED-50)
            var headerDelivery = new Label { name = "HeaderDelivery", tooltip = "When the impact lands" };
            headerDelivery.AddToClassList("pf-element__delivery");
            header.Add(headerDelivery);
            var remove = new ForgeIconButton(ForgeGlyphShape.Trash, "Remove this impact", () => Remove(root, property))
            {
                name = "RemoveImpact",
            };
            remove.AddToClassList("pf-element__action");
            header.Add(remove);

            // The one line a folded entry shows
            var summary = new Label { name = "Summary" };
            summary.AddToClassList("pf-element__summary");
            root.Add(summary);

            var content = new VisualElement { name = "ExpandedContent" };
            content.AddToClassList("pf-element__content");
            root.Add(content);

            var operation = new ImpactFields.OperationField(operationProp, slotProp);
            content.Add(operation);
            var multiply = new EnumField("Multiply Behaviour")
            {
                name = "MultiplyBehaviour",
                tooltip = "How this multiply combines with others on the attribute: each compounds, or the bonuses add first",
            };
            multiply.AddToClassList(EnumField.alignedFieldUssClassName);
            multiply.AddToClassList("pf-dependent");
            multiply.BindProperty(multiplyProp);
            content.Add(multiply);
            // Shown for an Add on a Resistance, whose adds diminish, or one that says its own way (TW-1)
            var add = new EnumField("Add Behaviour")
            {
                name = "AddBehaviour",
                tooltip = "How this add combines with others on the attribute: as its role says (a Resistance's diminish), a " +
                          "plain sum, or on what's left",
            };
            add.AddToClassList(EnumField.alignedFieldUssClassName);
            add.AddToClassList("pf-dependent");
            add.BindProperty(addProp);
            content.Add(add);
            content.Add(new PropertyField(magnitudeProp, "Magnitude"));
            // The calculation each hit goes through as it lands (TW-2)
            content.Add(new PropertyField(property.FindPropertyRelative(nameof(AttributeImpactEntry.Pipeline)), "Pipeline") { name = "Pipeline" });

            var delivery = new PopupField<EImpactDelivery>("Delivery",
                ImpactFields.DeliveriesFor(Duration(property), (EImpactDelivery)deliveryProp.intValue),
                (EImpactDelivery)deliveryProp.intValue, ImpactFields.DeliveryName, ImpactFields.DeliveryName)
            {
                name = "Delivery",
                tooltip = "When the impact lands: Continuous while the effect lasts, then it reverses; On Apply once, as the " +
                          "effect is applied; Periodic on each tick. On Apply and Periodic impacts stay.",
            };
            delivery.AddToClassList(PopupField<EImpactDelivery>.alignedFieldUssClassName);
            delivery.RegisterValueChangedCallback(change =>
            {
                deliveryProp.intValue = (int)change.newValue;
                deliveryProp.serializedObject.ApplyModifiedProperties();
            });
            content.Add(delivery);
            // What a periodic entry commits, as the first impact says it (ED-50)
            var commits = ImpactFields.ValueRow("Commits", "EntryCommits", "How many times the impact lands, at the effect's first level");
            content.Add(commits);
            var problem = new HelpBox("", HelpBoxMessageType.Warning) { name = "DeliveryProblem" };
            content.Add(problem);

            var order = new PropertyField(orderProp, "Order")
            {
                name = "Order",
                tooltip = "Among overrides of one attribute, the lowest order wins",
            };
            content.Add(order);
            content.Add(new PropertyField(groupProp, "Retention Group")
            {
                tooltip = "The group the change counts toward, such as bonus health. Empty, it takes the effect's",
            });
            content.Add(new PropertyField(typesProp, "Impact Types"));

            // ─── Behaviour ─────────────────────────────────────────────────────
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

            // The line, the deliveries offered and the help box follow the asset, an Undo among its changes, and the
            // effect's duration; they only read it
            void Show()
            {
                var duration = Duration(property);
                var current = (EImpactDelivery)deliveryProp.intValue;
                var kind = (ECalculationOperation)operationProp.intValue;

                operation.Refresh();
                delivery.choices = ImpactFields.DeliveriesFor(duration, current);
                delivery.SetValueWithoutNotify(current);
                headerDelivery.text = ImpactFields.DeliveryName(current);
                var effect = property.serializedObject.targetObject as GameplayEffect;
                bool periodic = current == EImpactDelivery.Periodic && effect != null && duration != null;
                commits.SetValueWithoutNotify(periodic ? ImpactFields.CommitsText(effect) : "");
                commits.EnableInClassList("pf-hidden", !periodic);
                var why = duration == null ? null : ImpactFields.DeliveryProblem(duration, current);
                problem.text = why ?? "";
                problem.style.display = why != null ? DisplayStyle.Flex : DisplayStyle.None;

                multiply.style.display = kind == ECalculationOperation.Multiply ? DisplayStyle.Flex : DisplayStyle.None;
                add.style.display = GameplayEffectImpactDrawer.ShowsAdd(attributeProp, addProp, kind == ECalculationOperation.Add)
                    ? DisplayStyle.Flex : DisplayStyle.None;
                order.style.display = kind == ECalculationOperation.Override ? DisplayStyle.Flex : DisplayStyle.None;

                var slot = (EAttributeTarget)slotProp.intValue;
                var value = slot == EAttributeTarget.Current ? "" : slot == EAttributeTarget.Base ? " Base" : " Current and Base";
                summary.text = $"{ImpactFields.AttributeName(attributeProp.objectReferenceValue)}{value} " +
                               $"{ImpactFields.MagnitudeText(kind, magnitudeProp)} · {ImpactFields.DeliveryName(current)}";
            }
            root.TrackSerializedObjectValue(property.serializedObject, _ => Show());

            SetFolded(IsCollapsed(property));
            Show();
            return root;
        }

        // The duration of the effect the impact belongs to, or null when the drawer is drawn elsewhere
        private static GameplayEffectDuration Duration(SerializedProperty property) =>
            (property.serializedObject.targetObject as GameplayEffect)?.DurationSpecification;

        // Removes the entry from its list, in one undoable step. The list's entries are let go first, so none stays bound to
        // an element that's gone; the impact drawer draws them again as the list changes
        private static void Remove(VisualElement root, SerializedProperty property)
        {
            var path = property.propertyPath;
            int at = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (at < 0) return;
            var list = property.serializedObject.FindProperty(path.Substring(0, at));
            if (list == null || !int.TryParse(path.Substring(at + ".Array.data[".Length).TrimEnd(']'), out int index)) return;

            var entries = root.GetFirstAncestorOfType<PropertyField>()?.parent;
            if (entries?.name == GameplayEffectImpactDrawer.EntriesName) entries.Clear();
            list.DeleteArrayElementAtIndex(index);
            list.serializedObject.ApplyModifiedProperties();
        }
    }
}
