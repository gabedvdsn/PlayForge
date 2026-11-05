using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An effect's impact, in Docs/EDITOR_STYLE.md's terms and the Effect mockup's: the attribute, the operation, the
    /// magnitude, then how the impact is delivered, which the duration gives it, and what a periodic one commits (ED-2).
    /// Then its impact types and who it affects, its additional impacts, each with a delivery of its own, with Add Impact
    /// (ED-3), and its contained effects. It no longer draws Reverse On Removal, which the impact model removed (ED-2),
    /// and it takes its look from the style sheet: it drew tinted boxes with colored headings.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayEffectImpact))]
    public class GameplayEffectImpactDrawer : PropertyDrawer
    {
        // The element that holds the additional impacts' entries, which an entry's Remove lets go of
        internal const string EntriesName = "AdditionalImpacts";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "ImpactRoot" };
            // The style sheet lays out the impact, wherever the drawer is drawn (ED-20)
            ForgeTheme.Apply(root);

            var attributeProp    = property.FindPropertyRelative(nameof(GameplayEffectImpact.AttributeTarget));
            var slotProp         = property.FindPropertyRelative(nameof(GameplayEffectImpact.TargetImpact));
            var operationProp    = property.FindPropertyRelative(nameof(GameplayEffectImpact.ImpactOperation));
            var multiplyProp     = property.FindPropertyRelative(nameof(GameplayEffectImpact.MultiplyBehaviour));
            var magnitudeProp    = property.FindPropertyRelative(nameof(GameplayEffectImpact.MagnitudeOperation));
            var typesProp        = property.FindPropertyRelative(nameof(GameplayEffectImpact.ImpactTypes));
            var affiliationProp  = property.FindPropertyRelative(nameof(GameplayEffectImpact.AffiliationPolicy));
            var comparisonProp   = property.FindPropertyRelative(nameof(GameplayEffectImpact.AffiliationComparison));
            var affiliationsProp = property.FindPropertyRelative(nameof(GameplayEffectImpact.Affiliations));
            var additionalProp   = property.FindPropertyRelative(nameof(GameplayEffectImpact.AdditionalImpacts));
            var packetsProp      = property.FindPropertyRelative(nameof(GameplayEffectImpact.Packets));
            bool multiple = property.serializedObject.isEditingMultipleObjects;

            // ─── The first impact ──────────────────────────────────────────────
            root.Add(new PropertyField(attributeProp, "Attribute"));
            var operation = new ImpactFields.OperationField(operationProp, slotProp);
            root.Add(operation);
            // Shown for Multiply, which it qualifies. It followed a stale copy of the operation every 250 ms (ED-11)
            var multiply = new EnumField("Multiply Behaviour")
            {
                name = "MultiplyBehaviour",
                tooltip = "How this multiply combines with others on the attribute: each compounds, or the bonuses add first",
            };
            multiply.AddToClassList(EnumField.alignedFieldUssClassName);
            multiply.AddToClassList("pf-dependent");
            multiply.BindProperty(multiplyProp);
            root.Add(multiply);
            root.Add(new PropertyField(magnitudeProp, "Magnitude"));

            // How the runtime delivers it, which the duration decides, and how often a periodic one lands. The drawer
            // didn't say, and drew Reverse On Removal, which nothing reads since commits stay (ED-2)
            var delivery = ImpactFields.ValueRow("Delivery", "Delivery", "When the impact lands, which the effect's duration gives it");
            root.Add(delivery);
            var commits = ImpactFields.ValueRow("Commits", "Commits", "How many times a periodic impact lands, at the effect's first level");
            root.Add(commits);
            var deliveryNote = new HelpBox("", HelpBoxMessageType.Info) { name = "DeliveryNote" };
            root.Add(deliveryNote);

            root.Add(new PropertyField(typesProp, "Impact Types"));

            // Who it affects: Comparison asks of every choice but Always Allow, and the list only of its own choice
            var affiliation = new EnumField("Affiliation")
            {
                name = "AffiliationPolicy",
                tooltip = "Whom the effect may change, by their affiliation and its source's",
            };
            affiliation.AddToClassList(EnumField.alignedFieldUssClassName);
            affiliation.BindProperty(affiliationProp);
            root.Add(affiliation);
            var comparison = new EnumField("Comparison")
            {
                name = "AffiliationComparison",
                tooltip = "Whether any one affiliation is enough, or all of them must match",
            };
            comparison.AddToClassList(EnumField.alignedFieldUssClassName);
            comparison.AddToClassList("pf-dependent");
            comparison.BindProperty(comparisonProp);
            root.Add(comparison);
            var affiliations = new PropertyField(affiliationsProp, "Affiliations") { name = "Affiliations" };
            root.Add(affiliations);

            // ─── Additional impacts, each its own entry, then Add Impact ─────────
            // Each effect keeps its own, a list a multiple selection can't edit safely: one of another length would be
            // cut (ED-12)
            var entries = new VisualElement { name = EntriesName };
            if (multiple)
            {
                var note = new Label("Select one effect to edit its additional impacts.") { name = "AdditionalImpactsNote" };
                note.AddToClassList("pf-note");
                root.Add(note);
            }
            else
            {
                root.Add(entries);
                var addRow = new VisualElement { name = "AddImpactRow" };
                addRow.AddToClassList("pf-button-row");
                addRow.Add(new Button(() => AddImpact(property))
                {
                    name = "AddImpact",
                    text = "Add Impact",
                    tooltip = "Adds an impact on another attribute, or the same one, with a delivery of its own",
                });
                root.Add(addRow);
            }

            root.Add(new PropertyField(packetsProp, "Contained Effects")
            {
                tooltip = "Effects this one applies as it's applied, on each tick, or as it's removed",
            });

            // ─── Behaviour ─────────────────────────────────────────────────────
            int shownEntries = -1;
            void ShowEntries(bool bind)
            {
                if (multiple) return;
                int count = additionalProp.arraySize;
                if (count == shownEntries && entries.childCount == count) return;
                shownEntries = count;
                entries.Clear();
                for (int i = 0; i < count; i++)
                {
                    var entry = new PropertyField(additionalProp.GetArrayElementAtIndex(i), "");
                    entries.Add(entry);
                    // Drawn again after the panel bound the drawer, so each binds itself
                    if (bind) entry.Bind(property.serializedObject);
                }
            }

            // A multiple selection shows what any selected impact uses (ED-12)
            void Show(bool bind)
            {
                operation.Refresh();
                var each = EachAsset.Of(property).Select(own => (
                    kind: (ECalculationOperation)own.FindPropertyRelative(nameof(GameplayEffectImpact.ImpactOperation)).intValue,
                    policy: (EAffiliationPolicy)own.FindPropertyRelative(nameof(GameplayEffectImpact.AffiliationPolicy)).intValue))
                    .ToList();
                multiply.style.display = each.Any(e => e.kind == ECalculationOperation.Multiply) ? DisplayStyle.Flex : DisplayStyle.None;
                comparison.style.display = each.Any(e => e.policy != EAffiliationPolicy.AlwaysAllow) ? DisplayStyle.Flex : DisplayStyle.None;
                affiliations.style.display = each.Any(e => e.policy == EAffiliationPolicy.UseAffiliationList) ? DisplayStyle.Flex : DisplayStyle.None;

                ShowDelivery(property, delivery, commits, deliveryNote);
                ShowEntries(bind);
            }

            // The rows follow the asset, an Undo among its changes, and the effect's duration; they only read it
            root.TrackSerializedObjectValue(property.serializedObject, _ => Show(true));
            Show(false);
            return root;
        }

        // The first impact's delivery, and how often it lands when it's periodic. Hidden when the first impact changes
        // nothing, as in an effect that only grants tags. A multiple selection shows a mixed value where the selected
        // effects differ (ED-12)
        private static void ShowDelivery(SerializedProperty property, TextField delivery, TextField commits, HelpBox note)
        {
            var each = property.serializedObject.targetObjects.OfType<GameplayEffect>().Select(DeliveryOf).ToList();
            bool authored = each.Any(e => e.delivery != null);
            delivery.style.display = authored ? DisplayStyle.Flex : DisplayStyle.None;
            if (!authored)
            {
                commits.style.display = DisplayStyle.None;
                note.style.display = DisplayStyle.None;
                return;
            }

            Shown(delivery, each.Select(e => e.delivery));
            Shown(commits, each.Select(e => e.commits));
            commits.style.display = each.Any(e => e.commits != null) ? DisplayStyle.Flex : DisplayStyle.None;

            bool trap = each.Any(e => e.trap);
            note.text = trap
                ? "With Tick On Application and no periodic ticks, a multiply or override lasts while the effect does, then reverses. Turn Tick On Application off to say so."
                : "";
            note.style.display = trap ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // A read-only row's text, or a mixed value when the selected effects' differ
        private static void Shown(TextField row, IEnumerable<string> texts)
        {
            var distinct = texts.Distinct().ToList();
            row.SetValueWithoutNotify(distinct.Count == 1 ? distinct[0] ?? "" : "");
            row.showMixedValue = distinct.Count > 1;
        }

        // An effect's first impact: when it lands, how often a periodic one does, and whether Tick On Application alone
        // makes a lasting multiply or override, which the runtime keeps as a modifier, so it reverses as the effect ends,
        // though the setting reads as a single tick. No delivery when the first impact changes nothing
        private static (string delivery, string commits, bool trap) DeliveryOf(GameplayEffect effect)
        {
            var impact = effect.ImpactSpecification;
            var duration = effect.DurationSpecification;
            bool authored = impact != null && (impact.AttributeTarget != null ||
                impact.MagnitudeOperation != null && (impact.MagnitudeOperation.Scaler != null || impact.MagnitudeOperation.Magnitude != 0f));
            if (!authored) return (null, null, false);

            var kind = impact.ResolveDelivery(duration, out _);
            var times = kind == EImpactDelivery.Periodic ? ImpactFields.CommitsText(effect) : null;
            bool trap = kind == EImpactDelivery.Continuous && duration is { TickOnApplication: true, EnablePeriodicTicks: false };
            return (ImpactFields.DeliveryText(kind, duration), times, trap);
        }

        // Adds an impact the effect's duration can make, which uses its own magnitude, in one undoable step
        private static void AddImpact(SerializedProperty property)
        {
            var serialized = property.serializedObject;
            var effects = serialized.targetObjects.OfType<GameplayEffect>().Where(effect => effect.ImpactSpecification != null).ToArray();
            if (effects.Length == 0) return;
            Undo.RecordObjects(effects, "Add Impact");
            foreach (var effect in effects)
            {
                effect.ImpactSpecification.AdditionalImpacts ??= new List<AttributeImpactEntry>();
                effect.ImpactSpecification.AdditionalImpacts.Add(new AttributeImpactEntry
                {
                    Delivery = ImpactFields.DefaultDelivery(effect.DurationSpecification),
                    Magnitude = new ScalerMagnitudeOperation { RealMagnitude = EMagnitudeOperation.UseMagnitude },
                });
                EditorUtility.SetDirty(effect);
            }
            serialized.Update();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ContainedEffectPacket Drawer
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A contained effect, on one row: when it's applied, then the effect. It drew a tinted, rounded box with a stripe in
    /// its policy's color, set 50 ms after it drew (ED-37).
    /// </summary>
    [CustomPropertyDrawer(typeof(ContainedEffectPacket))]
    public class ContainedEffectPacketDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { name = "ContainedEffect" };
            ForgeTheme.Apply(row);
            row.AddToClassList("pf-contained-effect");

            var policy = new EnumField
            {
                name = "Policy",
                tooltip = "When the effect is applied: as this one is applied, on each tick, or as this one is removed",
            };
            policy.AddToClassList("pf-contained-effect__policy");
            policy.BindProperty(property.FindPropertyRelative(nameof(ContainedEffectPacket.Policy)));
            row.Add(policy);

            var effect = new PropertyField(property.FindPropertyRelative(nameof(ContainedEffectPacket.ContainedEffect)), "") { name = "Effect" };
            effect.AddToClassList("pf-contained-effect__effect");
            row.Add(effect);
            return row;
        }
    }
}
