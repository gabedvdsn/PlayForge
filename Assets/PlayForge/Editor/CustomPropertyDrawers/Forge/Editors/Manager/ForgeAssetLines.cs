using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// One line for each asset (TW-21, Decision 61), which its inspector's header and The Forge's list show: an ability's
    /// numbers by level, then its costs and cooldown, as "-450/-600/-750 Health; 280/420/680 Mana; 70/60/50 s"; an item's
    /// what it changes while equipped and the ability it grants, as "+20 Armor; Blink"; an entity's first attributes as it
    /// starts and its abilities, as "Health 500, Mana 300, Armor 5; Fireball, Blink"; an effect's first impact by level, as
    /// its Levels section reads it. Numbers that read what an editor can't give, as the target, are marked with a *.
    /// </summary>
    public static class ForgeAssetLines
    {
        // The most attributes an entity's line names, its set's first
        private const int MostAttributes = 3;

        /// <summary>The asset's line, or empty where it has nothing to say.</summary>
        public static string Of(Object asset) => asset switch
        {
            Ability ability => Of(ability),
            Item item => Of(item),
            EntityIdentity entity => Of(entity),
            GameplayEffect effect => Of(effect),
            _ => "",
        };

        /// <summary>An ability's numbers by level: each effect it applies, by its first impact; then its costs and its cooldown.</summary>
        public static string Of(Ability ability)
        {
            var breakdown = ability ? AbilityInspector.Build(ability) : null;
            if (breakdown == null) return "";
            var parts = new List<string>();

            var seen = new HashSet<GameplayEffect>();
            foreach (var usage in breakdown.GameplayUsages)
            {
                if (usage.Effect == null || !seen.Add(usage.Effect)) continue;
                var impact = FirstImpact(usage.Series);
                if (impact != null) parts.Add(ImpactText(impact));
            }

            // What its Cost effect spends, as amounts, then each of its attribute costs (TW-29)
            if (breakdown.Cost?.Series is { } cost)
            {
                foreach (var impact in cost.Impacts)
                    if (impact.Attribute != null && impact.Magnitude != null)
                        parts.Add($"{Series(impact.Magnitude.Select(Mathf.Abs).ToArray())} {impact.Attribute.GetName()}{Needs(impact)}");
            }
            if (ability.AttributeCosts != null)
            {
                foreach (var each in ability.AttributeCosts)
                {
                    if (each?.Amount == null) continue;
                    var amounts = new float[breakdown.LevelCount];
                    for (int i = 0; i < amounts.Length; i++)
                        amounts[i] = Mathf.Max(0f, AbilityInspector.EvaluateAtLevel(each.Amount, breakdown.MinLevel + i, breakdown.MinLevel, breakdown.MaxLevel, out _));
                    if (amounts.All(amount => amount <= 0f)) continue;
                    parts.Add($"{Series(amounts)} {CostName(each)}");
                }
            }

            if (breakdown.Cooldown?.Series?.Duration is { } cooldown && cooldown.Any(seconds => seconds > 0f))
                parts.Add($"{Series(cooldown)} s");
            return string.Join("; ", parts);
        }

        /// <summary>An item's: what each effect it gives while equipped changes, by its first impact, at its levels; then its ability.</summary>
        public static string Of(Item item)
        {
            if (!item) return "";
            var parts = new List<string>();
            var (first, last) = AttributeStartValues.LevelsOf(item);
            if (item.GrantedEffects != null)
            {
                foreach (var effect in item.GrantedEffects)
                {
                    if (!effect) continue;
                    var impact = FirstImpact(AbilityInspector.BuildEffectSeries(effect, first, last, null, null));
                    if (impact != null) parts.Add(ImpactText(impact));
                }
            }
            if (item.ActiveAbility) parts.Add(NameOf(item.ActiveAbility));
            return string.Join("; ", parts);
        }

        /// <summary>An entity's: its set's first attributes as it starts, at its starting level; then its starting abilities.</summary>
        public static string Of(EntityIdentity entity)
        {
            if (!entity) return "";
            var parts = new List<string>();
            if (entity.AttributeSet)
            {
                int level = Mathf.Max(1, entity.StartingLevel);
                var starts = AttributeStartValues.Of(entity.AttributeSet, level, level);
                var attributes = starts.Attributes
                    .Where(start => start.Attribute)
                    .Take(MostAttributes)
                    .Select(start => $"{start.Attribute.GetName()} {ScalerFields.Number(start.At(level).CurrentValue)}")
                    .ToList();
                if (attributes.Count > 0) parts.Add(string.Join(", ", attributes));
            }
            var abilities = (entity.StartingAbilities ?? new List<Ability>()).Where(ability => ability).Select(NameOf).ToList();
            if (abilities.Count > 0) parts.Add(string.Join(", ", abilities));
            return string.Join("; ", parts);
        }

        /// <summary>An effect's: its first impact, at its provider's levels where it's linked to one, or else at five, as its Levels section reads it.</summary>
        public static string Of(GameplayEffect effect)
        {
            if (!effect) return "";
            int top = Mathf.Clamp(effect.IsLinked ? effect.GetMaxLevel() : 5, 1, AttributeStartValues.MostLevels);
            var impact = FirstImpact(AbilityInspector.BuildEffectSeries(effect, 1, top, null, null));
            return impact == null ? "" : ImpactText(impact);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Forge's list, which reads a line for each row it binds
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<Object, string> kept = new();
        private static bool listening;

        /// <summary>The asset's line, kept until anything in the project is edited, undone or changes on disk.</summary>
        internal static string Kept(Object asset)
        {
            if (!asset) return "";
            Listen();
            if (kept.TryGetValue(asset, out var line)) return line;
            return kept[asset] = Of(asset);
        }

        // An edit to one asset changes another's line, as an effect its ability applies, so any change lets them all go
        private static void Listen()
        {
            if (listening) return;
            listening = true;
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => kept.Clear();
            Undo.undoRedoPerformed += kept.Clear;
            EditorApplication.projectChanged += kept.Clear;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Its parts
        // ═══════════════════════════════════════════════════════════════════════════

        private static ImpactSeries FirstImpact(EffectLevelSeries series) =>
            series?.Impacts.FirstOrDefault(impact => impact.Attribute != null && impact.Magnitude is { Length: > 0 });

        // An impact by level, as an effect's line says it: an add signed, as "+20" or "-450/-600/-750", a multiply as "× 1.5",
        // an override as "= 5"
        private static string ImpactText(ImpactSeries impact)
        {
            var values = impact.Magnitude;
            string amount = impact.Operation switch
            {
                ECalculationOperation.Multiply => $"× {Series(values)}",
                ECalculationOperation.Override => $"= {Series(values)}",
                _ => Series(values, signed: true),
            };
            return $"{amount} {impact.Attribute.GetName()}{Needs(impact)}";
        }

        private static string Needs(ImpactSeries impact) => impact.Needs != null ? " *" : "";

        // A number at each level: once where every level reads the same; each, by "/", up to as many as a series spells out;
        // past that, its first and last
        private static string Series(IReadOnlyList<float> values, bool signed = false)
        {
            if (values == null || values.Count == 0) return "0";
            string Text(float value) => signed && value > 0f ? $"+{ScalerFields.Number(value)}" : ScalerFields.Number(value);
            if (values.All(value => Mathf.Approximately(value, values[0]))) return Text(values[0]);
            if (values.Count <= AttributeStartValues.MostInSeries) return string.Join("/", values.Select(Text));
            return $"{Text(values[0])} to {Text(values[values.Count - 1])}";
        }

        // What an attribute cost spends: its attribute, or the attribute its role names in the project, or the role itself
        private static string CostName(AttributeCost cost)
        {
            var spends = cost.Spends;
            if (spends) return spends.GetName();
            return cost.Role.IsValid ? TheForge.TagText(cost.Role) : "nothing";
        }

        private static string NameOf(Object asset)
        {
            string name = asset is IHasReadableDefinition readable ? readable.GetName() : null;
            return string.IsNullOrEmpty(name) ? asset.name : name;
        }
    }
}
