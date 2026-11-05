using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What the effect's impact drawer and its entries' drawer share: the Operation field, which sets the operation and
    /// the value it changes in one, as the Effect mockup's "Add to Current" does, and how an impact's delivery, magnitude
    /// and commits read (ED-2, ED-3).
    /// </summary>
    internal static class ImpactFields
    {
        private static readonly ECalculationOperation[] Operations = (ECalculationOperation[])Enum.GetValues(typeof(ECalculationOperation));
        private static readonly EAttributeTarget[] Slots = (EAttributeTarget[])Enum.GetValues(typeof(EAttributeTarget));
        private static readonly EImpactDelivery[] Deliveries = (EImpactDelivery[])Enum.GetValues(typeof(EImpactDelivery));

        /// <summary>
        /// One popup for the operation and the value it changes, as in "Add to Current" or "Multiply Base". Choosing one
        /// writes both properties in one undoable step. <see cref="Refresh"/> shows the properties' values again, as after an
        /// Undo; its owner calls it as the asset changes.
        /// </summary>
        internal sealed class OperationField : PopupField<int>
        {
            private readonly SerializedProperty operation;
            private readonly SerializedProperty slot;

            public OperationField(SerializedProperty operation, SerializedProperty slot)
                : base("Operation", Enumerable.Range(0, Operations.Length * Slots.Length).ToList(), 0, NameOf, NameOf)
            {
                this.operation = operation;
                this.slot = slot;
                name = "Operation";
                tooltip = "How the impact changes the attribute, and which of its values: Current, Base, or both";
                AddToClassList(alignedFieldUssClassName);
                this.RegisterValueChangedCallback(change =>
                {
                    this.operation.intValue = (int)Operations[change.newValue / Slots.Length];
                    this.slot.intValue = (int)Slots[change.newValue % Slots.Length];
                    this.operation.serializedObject.ApplyModifiedProperties();
                });
                Refresh();
            }

            public void Refresh()
            {
                int op = Array.IndexOf(Operations, (ECalculationOperation)operation.intValue);
                int at = Array.IndexOf(Slots, (EAttributeTarget)slot.intValue);
                SetValueWithoutNotify(Mathf.Max(0, op) * Slots.Length + Mathf.Max(0, at));
                // Selected impacts whose operations differ show a mixed value (ED-12)
                showMixedValue = operation.hasMultipleDifferentValues || slot.hasMultipleDifferentValues;
            }

            private static string NameOf(int choice) => OperationName(Operations[choice / Slots.Length], Slots[choice % Slots.Length]);
        }

        internal static string OperationName(ECalculationOperation operation, EAttributeTarget slot)
        {
            var value = SlotName(slot);
            return operation switch
            {
                ECalculationOperation.Add => $"Add to {value}",
                ECalculationOperation.Multiply => $"Multiply {value}",
                ECalculationOperation.Override => $"Override {value}",
                ECalculationOperation.FlatBonus => $"Flat Bonus to {value}",
                _ => $"{ObjectNames.NicifyVariableName(operation.ToString())} {value}",
            };
        }

        private static string SlotName(EAttributeTarget slot) => slot switch
        {
            EAttributeTarget.CurrentAndBase => "Current and Base",
            _ => slot.ToString(),
        };

        /// <summary>A delivery's name, as an entry's Delivery offers it: Continuous, On Apply or Periodic.</summary>
        internal static string DeliveryName(EImpactDelivery delivery) => ObjectNames.NicifyVariableName(delivery.ToString());

        /// <summary>When an impact lands, as the first impact's Delivery reads, such as "Each tick, and on applying".</summary>
        internal static string DeliveryText(EImpactDelivery delivery, GameplayEffectDuration duration) => delivery switch
        {
            EImpactDelivery.OnApply => "On applying",
            EImpactDelivery.Continuous => "While the effect lasts",
            EImpactDelivery.Periodic => duration is { TickOnApplication: true } ? "Each tick, and on applying" : "Each tick",
            _ => DeliveryName(delivery),
        };

        /// <summary>
        /// The deliveries the duration can make, in the enum's order, then the one an entry names if the duration can't
        /// make it, so the field can still show it.
        /// </summary>
        internal static List<EImpactDelivery> DeliveriesFor(GameplayEffectDuration duration, EImpactDelivery current)
        {
            var valid = Deliveries.Where(each => CanMake(duration, each)).ToList();
            if (!valid.Contains(current)) valid.Add(current);
            return valid;
        }

        /// <summary>The delivery a new impact takes: the first the duration can make, Continuous on a lasting effect.</summary>
        internal static EImpactDelivery DefaultDelivery(GameplayEffectDuration duration) =>
            Deliveries.FirstOrDefault(each => CanMake(duration, each));

        internal static bool CanMake(GameplayEffectDuration duration, EImpactDelivery delivery) =>
            new AttributeImpactEntry { Delivery = delivery }.IsDeliveryValidFor(duration);

        /// <summary>Why an impact with this delivery never applies, or null when it can.</summary>
        internal static string DeliveryProblem(GameplayEffectDuration duration, EImpactDelivery delivery)
        {
            if (CanMake(duration, delivery)) return null;
            return duration.DurationPolicy == EEffectDurationPolicy.Instant
                ? $"An Instant effect makes no {DeliveryName(delivery)} impact, so this one never applies."
                : $"The effect doesn't tick, so this {DeliveryName(delivery)} impact never applies.";
        }

        /// <summary>
        /// How often a periodic impact lands, at the effect's first level, as in "7 over 6 s, level 1": its ticks, and one
        /// more when it ticks on applying. An infinite effect says how far apart they are.
        /// </summary>
        internal static string CommitsText(GameplayEffect effect)
        {
            var duration = effect.DurationSpecification;
            int level = Mathf.Max(1, effect.GetStartingLevel());
            if (duration.DurationPolicy == EEffectDurationPolicy.Durational)
            {
                var series = AbilityInspector.BuildEffectSeries(effect, level, level);
                int commits = series.Ticks[0] + (duration.TickOnApplication ? 1 : 0);
                return $"{commits} over {Number(series.Duration[0])} s, level {level}";
            }
            float every = AbilityInspector.EvaluateAtLevel(duration.TickIntervalOperation, level, level, level, out _);
            return duration.TickOnApplication
                ? $"One on applying, then one every {Number(every)} s, level {level}"
                : $"One every {Number(every)} s, level {level}";
        }

        /// <summary>
        /// What an impact does to its value, as an entry's line reads it: "−10" or "+5" for Add, "×1.5" for Multiply,
        /// "= 50" for Override, and its scaler where it takes one, as in "−10 × Level Curve".
        /// </summary>
        internal static string MagnitudeText(ECalculationOperation operation, SerializedProperty magnitude)
        {
            var value = magnitude.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Magnitude)).floatValue;
            var real = (EMagnitudeOperation)magnitude.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude)).intValue;
            var scaler = magnitude.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler)).managedReferenceValue is AbstractScaler s
                ? ScalerName(s) : null;

            var number = operation switch
            {
                ECalculationOperation.Multiply => $"×{Number(value)}",
                ECalculationOperation.Override => $"= {Number(value)}",
                _ => value < 0 ? $"−{Number(-value)}" : $"+{Number(value)}",
            };
            if (scaler == null || real == EMagnitudeOperation.UseMagnitude) return number;
            return real switch
            {
                EMagnitudeOperation.UseScaler => scaler,
                EMagnitudeOperation.AddScaler => $"{number} + {scaler}",
                EMagnitudeOperation.MultiplyWithScaler => $"{number} × {scaler}",
                _ => number,
            };
        }

        /// <summary>A scaler's kind, as a line names it: Level Curve for a CachedLevelCurveScaler.</summary>
        internal static string ScalerName(AbstractScaler scaler)
        {
            var name = scaler.GetType().Name;
            if (name.StartsWith("Cached")) name = name.Substring("Cached".Length);
            if (name.EndsWith("Scaler")) name = name.Substring(0, name.Length - "Scaler".Length);
            return name.Length > 0 ? ObjectNames.NicifyVariableName(name) : "Scaler";
        }

        /// <summary>An attribute's name as a line shows it, or "No Attribute".</summary>
        internal static string AttributeName(Object attribute) => attribute is BaseForgeAsset asset
            ? string.IsNullOrEmpty(asset.GetName()) ? asset.name : asset.GetName()
            : "No Attribute";

        /// <summary>A value the drawer shows but doesn't edit: Unity's text field, read-only, in the style sheet's look.</summary>
        internal static TextField ValueRow(string label, string name, string tooltip)
        {
            var row = new TextField(label) { name = name, isReadOnly = true, tooltip = tooltip };
            row.AddToClassList(TextField.alignedFieldUssClassName);
            row.AddToClassList(ForgeAssetTagField.ValueFieldClass);
            return row;
        }

        internal static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
