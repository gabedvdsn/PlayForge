using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// The middle dock's lines (LAB-5): an asset's numbers level by level, from FEAT-32's query, as "−80/−120/−160 Health",
    /// and a unit's attributes and effects as they are now.
    /// </summary>
    public static class LabSummary
    {
        /// <summary>The most levels a line shows.</summary>
        public const int MostLevels = 10;

        /// <summary>An asset's lines: what it is, and its numbers at each level.</summary>
        public static List<string> Of(ScriptableObject asset)
        {
            var lines = new List<string>();
            switch (asset)
            {
                case GameplayEffect effect:
                    AddEffect(lines, effect, "");
                    break;
                case Ability ability:
                    AddAbility(lines, ability);
                    break;
                case Item item:
                    AddDescription(lines, item.GetDescription());
                    lines.Add($"Levels {item.GetStartingLevel()}–{Mathf.Max(item.GetStartingLevel(), item.GetMaxLevel())}");
                    if (item.ActiveAbility) lines.Add($"Active: {item.ActiveAbility.GetName()}");
                    foreach (var granted in item.GrantedEffects.Where(each => each))
                        AddEffect(lines, granted, "Grants ");
                    break;
                case EntityIdentity entity:
                    AddDescription(lines, entity.GetDescription());
                    lines.Add($"Levels {entity.GetStartingLevel()}–{Mathf.Max(entity.GetStartingLevel(), entity.GetMaxLevel())}");
                    if (entity.AttributeSet) AddSet(lines, entity.AttributeSet);
                    if (entity.StartingAbilities is { Count: > 0 })
                        lines.Add($"Abilities: {string.Join(", ", entity.StartingAbilities.Where(a => a).Select(a => a.GetName()))}");
                    break;
                case AttributeSet set:
                    AddSet(lines, set);
                    break;
                case Attribute attribute:
                    lines.Add($"{attribute.GetName()} ({attribute.GetAbbreviation()})");
                    break;
                case ScalerAsset scaler:
                    lines.Add(scaler.Scaler != null ? NumberText.Describe(scaler.Scaler) : "No scaler");
                    break;
            }
            return lines;
        }

        private static void AddDescription(List<string> lines, string description)
        {
            if (!string.IsNullOrWhiteSpace(description)) lines.Add(description.Trim());
        }

        private static void AddAbility(List<string> lines, Ability ability)
        {
            AddDescription(lines, ability.GetDescription());
            var breakdown = AbilityInspector.Build(ability);
            foreach (var usage in breakdown.Usages)
            {
                if (usage?.Effect == null) continue;
                string role = usage.Role switch
                {
                    EAbilityEffectRole.Cost => "Cost: ",
                    EAbilityEffectRole.Cooldown => "Cooldown: ",
                    _ => usage.StageIndex >= 0 ? $"Stage {usage.StageIndex + 1}: " : "",
                };
                AddEffect(lines, usage.Effect, role, Mathf.Max(1, ability.GetStartingLevel()), Mathf.Max(1, ability.GetMaxLevel()));
            }
        }

        /// <summary>An effect's lines over its levels: its time, its chance, and each impact's number at each level.</summary>
        private static void AddEffect(List<string> lines, GameplayEffect effect, string prefix, int min = 0, int max = 0)
        {
            if (min <= 0) min = Mathf.Max(1, effect.GetStartingLevel());
            if (max <= 0) max = Mathf.Max(min, effect.GetMaxLevel());
            max = Mathf.Min(max, min + MostLevels - 1);
            var series = AbilityInspector.BuildEffectSeries(effect, min, max);
            lines.Add($"{prefix}{effect.GetName()} · {Levels(min, max)}");
            if (series.DurationPolicy == EEffectDurationPolicy.Durational)
                lines.Add($"  Lasts {series.FormatDuration()} s");
            if (series.Chance != null && series.Chance.Any(chance => chance < 0.999f))
                lines.Add($"  Lands {ImpactSeries.FormatSeries(series.Chance.Select(c => c * 100f).ToArray(), "0")}% of the time");
            foreach (var impact in series.Impacts)
            {
                string attribute = impact.Attribute ? impact.Attribute.GetName() : "?";
                string how = impact.Delivery switch
                {
                    EImpactDelivery.Periodic => " each tick",
                    EImpactDelivery.Continuous => " while it lasts",
                    _ => "",
                };
                string number = impact.IsDynamic && !string.IsNullOrEmpty(impact.Line) ? impact.Line : impact.FormatMagnitude();
                string needs = string.IsNullOrEmpty(impact.Needs) ? "" : $" (reads {impact.Needs})";
                lines.Add($"  {Operation(impact.Operation)}{number} {attribute}{how}{needs}");
            }
            foreach (var contained in series.Contained)
                if (contained?.Series?.Effect) lines.Add($"  and {contained.Series.Effect.GetName()} {When(contained.Policy)}");
        }

        private static string Levels(int min, int max) => min == max ? $"level {min}" : $"levels {min}–{max}";

        private static string Operation(ECalculationOperation operation) => operation switch
        {
            ECalculationOperation.Multiply => "×",
            ECalculationOperation.Override => "= ",
            _ => "",
        };

        private static string When(EApplyTickRemove policy) => policy switch
        {
            EApplyTickRemove.OnTick => "each tick",
            EApplyTickRemove.OnRemove => "as it ends",
            _ => "as it lands",
        };

        private static void AddSet(List<string> lines, AttributeSet set)
        {
            foreach (var element in Elements(set))
            {
                if (element?.Attribute == null) continue;
                float current = element.Current?.Magnitude ?? 0f;
                float most = element.Base?.Magnitude ?? 0f;
                lines.Add(Mathf.Approximately(current, most)
                    ? $"{element.Attribute.GetName()} {LabPanels.Number(most)}"
                    : $"{element.Attribute.GetName()} {LabPanels.Number(current)} of {LabPanels.Number(most)}");
            }
        }

        // A set's elements with its sub-sets', each set once
        private static IEnumerable<AttributeSetElement> Elements(AttributeSet set, HashSet<AttributeSet> seen = null)
        {
            seen ??= new HashSet<AttributeSet>();
            if (set == null || !seen.Add(set)) yield break;
            if (set.Attributes != null)
                foreach (var element in set.Attributes) yield return element;
            if (set.SubSets == null) yield break;
            foreach (var sub in set.SubSets)
            foreach (var element in Elements(sub, seen))
                yield return element;
        }

        /// <summary>A unit's attributes now: each value, and its base where they differ.</summary>
        public static List<string> AttributesOf(LabUnit unit)
        {
            var lines = new List<string>();
            var system = unit != null && unit.Gas != null ? unit.Gas.GetAttributeSystem() : null;
            if (system == null) return lines;
            foreach (var each in system.GetDefinedAttributes())
            {
                if (each is not Attribute attribute || !unit.Gas.TryGetAttributeValue(attribute, out var value)) continue;
                lines.Add(Mathf.Approximately(value.CurrentValue, value.BaseValue)
                    ? $"{attribute.GetName()} {LabPanels.Number(value.CurrentValue)}"
                    : $"{attribute.GetName()} {LabPanels.Number(value.CurrentValue)} of {LabPanels.Number(value.BaseValue)}");
            }
            lines.Sort(System.StringComparer.OrdinalIgnoreCase);
            return lines;
        }

        /// <summary>The effects on a unit now: each with its stacks and the time it has left.</summary>
        public static List<string> EffectsOf(LabUnit unit)
        {
            var lines = new List<string>();
            var shelf = unit != null && unit.Gas != null ? unit.Gas.GetEffectSystem()?.Shelf : null;
            if (shelf == null) return lines;
            foreach (var container in shelf)
            {
                var effect = container?.Spec?.Base;
                if (effect == null) continue;
                int stacks = container is AbstractStackingEffectContainer stacking ? stacking.Stacks : 1;
                float left = container.DurationRemaining;
                string time = left > 0f && left < 9999f ? $" · {left:0.0} s" : "";
                lines.Add(stacks > 1 ? $"{effect.GetName()} ×{stacks}{time}" : $"{effect.GetName()}{time}");
            }
            if (lines.Count == 0) lines.Add("None");
            return lines;
        }
    }
}
