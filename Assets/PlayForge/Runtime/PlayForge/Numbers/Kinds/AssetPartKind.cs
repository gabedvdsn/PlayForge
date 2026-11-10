using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What an Asset Part number looks for on an entity (FEAT-29).</summary>
    public enum EAssetPartOf
    {
        [Tooltip("Effects on the entity, by their asset tag, a context tag or a tag they grant")]
        Effect,

        [Tooltip("Abilities the entity knows, by their asset tag or a context tag")]
        Ability,

        [Tooltip("Items the entity holds, by their asset tag or a context tag")]
        Item,
    }

    /// <summary>What an Asset Part number reads of an effect (FEAT-29).</summary>
    public enum EEffectPart
    {
        [Tooltip("How many such effects are on the entity; an effect that stacks in one container counts once")]
        Count,

        [Tooltip("The effect's stacks; 1 for an effect that doesn't stack")]
        Stacks,

        [Tooltip("The level the effect was applied at")]
        Level,

        [Tooltip("The last impact the effect dealt, as dealt: damage is negative")]
        [InspectorName("Last Impact")] LastImpact,

        [Tooltip("Every impact the effect dealt, added up, as dealt: damage is negative")]
        [InspectorName("Total Impact")] TotalImpact,

        [Tooltip("How many impacts the effect dealt")]
        [InspectorName("Impact Count")] ImpactCount,

        [Tooltip("The time the effect has left")]
        [InspectorName("Duration Remaining")] DurationRemaining,

        [Tooltip("The effect's whole duration")]
        Duration,

        [Tooltip("The time until the effect's next tick")]
        [InspectorName("Time Until Tick")] TimeUntilTick,

        [Tooltip("What the effect's workers answer under a key, about that application of it")]
        [InspectorName("Worker Value")] WorkerValue,
    }

    /// <summary>What an Asset Part number reads of an ability (FEAT-29).</summary>
    public enum EAbilityPart
    {
        [Tooltip("How many such abilities the entity knows")]
        Count,

        [Tooltip("The ability's level")]
        Level,

        [Tooltip("1 while the ability is running, else 0")]
        [InspectorName("Is Active")] IsActive,

        [Tooltip("1 while the ability's cooldown effect is on the entity, else 0")]
        [InspectorName("Is on Cooldown")] IsOnCooldown,

        [Tooltip("The time the ability's cooldown has left; 0 where it isn't cooling down")]
        [InspectorName("Cooldown Remaining")] CooldownRemaining,
    }

    /// <summary>What an Asset Part number reads of an item (FEAT-29).</summary>
    public enum EItemPart
    {
        [Tooltip("How many such items the entity holds; a stack counts once")]
        Count,

        [Tooltip("How many of the item are in its stack")]
        Stacks,

        [Tooltip("The item's level")]
        Level,

        [Tooltip("1 while the item is equipped, else 0")]
        [InspectorName("Is Equipped")] IsEquipped,
    }

    /// <summary>How an Asset Part number reads one value from several that match (FEAT-29).</summary>
    public enum EAssetPartAmong
    {
        [Tooltip("All of them, added up")]
        Sum,

        [Tooltip("The first one found")]
        First,

        [Tooltip("The highest of them")]
        Highest,

        [Tooltip("The lowest of them")]
        Lowest,
    }

    /// <summary>
    /// A part of an effect, an ability or an item on the caster or the target, found by a tag (FEAT-29, NUM-13): a
    /// Poison effect's stacks, how many Fire effects are on the target, what every Burn there has dealt, the time left
    /// on a shield, an ability's level or whether it's cooling down, how many potions are in a stack, whether a ring is
    /// equipped. An effect matches by its asset tag, a context tag or a tag it grants; an ability or an item by its
    /// asset tag or a context tag; a tag under the one given matches too.
    /// <para>
    /// Where several match, <see cref="Among"/> says how they read as one: added up, the first, the highest or the
    /// lowest. A count reads 0 where none matches; any other part reads <see cref="Default"/>. A true-or-false part
    /// reads 1 or 0, so added up it's how many are so. An effect's Worker Value asks that effect's workers for a key
    /// (<see cref="INumberWorker"/>), about that application of it, and counts only the effects whose workers answer.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Asset Part")]
    public class AssetPartKind : NumberKind
    {
        [Tooltip("What it looks for: effects on the entity, abilities it knows or items it holds")]
        public EAssetPartOf What = EAssetPartOf.Effect;

        [Tooltip("The tag they carry: an effect's asset tag, a context tag or a tag it grants; an ability's or an item's " +
                 "asset tag or a context tag. A tag under this one matches too")]
        [ForgeTagContext(ForgeContext.AssetIdentifier, ForgeContext.ContextIdentifier, ForgeContext.Granted)]
        public Tag Tagged;

        [Tooltip("Whose: the caster's or the target's")]
        public NumberSide On = NumberSide.Target;

        [Tooltip("What it reads of each effect")]
        [ForgeShowWhen(nameof(What), EAssetPartOf.Effect)]
        public EEffectPart EffectPart = EEffectPart.Stacks;

        [Tooltip("The key it asks each effect's workers for")]
        [ForgeShowWhen(nameof(What), EAssetPartOf.Effect)]
        [ForgeShowWhen(nameof(EffectPart), EEffectPart.WorkerValue)]
        [ForgeTagContext(ForgeContext.Data)]
        public Tag WorkerKey;

        [Tooltip("What it reads of each ability")]
        [ForgeShowWhen(nameof(What), EAssetPartOf.Ability)]
        public EAbilityPart AbilityPart = EAbilityPart.Level;

        [Tooltip("What it reads of each item")]
        [ForgeShowWhen(nameof(What), EAssetPartOf.Item)]
        public EItemPart ItemPart = EItemPart.Stacks;

        [Tooltip("Where several match, how they read as one: added up, the first found, the highest or the lowest. A " +
                 "count is a count whatever this says")]
        public EAssetPartAmong Among = EAssetPartAmong.Sum;

        [Tooltip("What it reads where none matches. A count reads 0 there")]
        public float Default;

        /// <summary>What it needs: the entity whose effects, abilities or items it reads.</summary>
        public override NumberReads Reads => On == NumberSide.Caster ? NumberReads.Caster : NumberReads.Target;

        protected override float Fallback => IsCount ? 0f : Default;

        private bool IsCount => What switch
        {
            EAssetPartOf.Effect => EffectPart == EEffectPart.Count,
            EAssetPartOf.Ability => AbilityPart == EAbilityPart.Count,
            _ => ItemPart == EItemPart.Count,
        };

        protected override float Evaluate(in NumberContext context)
        {
            var system = context.SystemOf(On);
            int found = 0;
            float value = 0f;
            if (system != null && Tagged.IsValid)
            {
                switch (What)
                {
                    case EAssetPartOf.Effect:
                        Effects(system.GetEffectSystem(), ref found, ref value);
                        break;
                    case EAssetPartOf.Ability:
                        Abilities(system, ref found, ref value);
                        break;
                    default:
                        Items(system.GetItemSystem(), ref found, ref value);
                        break;
                }
            }
            if (IsCount) return found;
            return found > 0 ? value : Default;
        }

        // One more that matches, joined to those before it as Among says
        private void Join(float read, ref int found, ref float value)
        {
            if (found == 0) value = read;
            else
            {
                switch (Among)
                {
                    case EAssetPartAmong.Sum:
                        value += read;
                        break;
                    case EAssetPartAmong.Highest:
                        if (read > value) value = read;
                        break;
                    case EAssetPartAmong.Lowest:
                        if (read < value) value = read;
                        break;
                }
            }
            found++;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects
        // ═══════════════════════════════════════════════════════════════════════════

        // By index over the shelf, which allocates nothing
        private void Effects(EffectSystemComponent effects, ref int found, ref float value)
        {
            if (effects == null) return;
            var shelf = effects.Shelf;
            for (int i = 0; i < shelf.Count; i++)
            {
                var container = shelf[i];
                var spec = container?.Spec;
                if (spec == null || !GameplayEffectTags.Names(Tagged, spec.Base)) continue;
                if (EffectPart == EEffectPart.WorkerValue)
                {
                    if (NumberWorkers.TryAsk(spec, WorkerKey, out float answer)) Join(answer, ref found, ref value);
                    continue;
                }
                Join(Read(container, spec), ref found, ref value);
            }
        }

        private float Read(AbstractEffectContainer container, GameplayEffectSpec spec)
        {
            switch (EffectPart)
            {
                case EEffectPart.Stacks: return container is AbstractStackingEffectContainer stacking ? stacking.Stacks : 1;
                case EEffectPart.Level: return spec.Origin != null ? spec.Origin.GetLevel().CurrentValue : 0;
                case EEffectPart.LastImpact: return spec.TrackedImpact != null ? spec.TrackedImpact.Last.CurrentValue : 0f;
                case EEffectPart.TotalImpact: return spec.TrackedImpact != null ? spec.TrackedImpact.Total.CurrentValue : 0f;
                case EEffectPart.ImpactCount: return spec.TrackedImpact != null ? spec.TrackedImpact.Count : 0;
                case EEffectPart.DurationRemaining: return container.DurationRemaining;
                case EEffectPart.Duration: return container.TotalDuration;
                case EEffectPart.TimeUntilTick: return container.TimeUntilPeriodTick;
                default: return 1f;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities
        // ═══════════════════════════════════════════════════════════════════════════

        private void Abilities(IGameplayAbilitySystem system, ref int found, ref float value)
        {
            var abilities = system.GetAbilitySystem();
            if (abilities == null) return;
            // The dictionary's own enumerator, which allocates nothing
            foreach (var container in abilities.Containers)
            {
                var ability = container?.Spec?.Base;
                if (ability == null || !Carries(ability.Tags.AssetTag, ability.Tags.ContextTags)) continue;
                Join(Read(container, ability, system.GetEffectSystem()), ref found, ref value);
            }
        }

        private float Read(AbilitySpecContainer container, Ability ability, EffectSystemComponent effects)
        {
            switch (AbilityPart)
            {
                case EAbilityPart.Level: return container.Spec.GetLevel().CurrentValue;
                case EAbilityPart.IsActive: return container.IsActive ? 1f : 0f;
                case EAbilityPart.IsOnCooldown: return Cooldown(ability, effects, out _) ? 1f : 0f;
                case EAbilityPart.CooldownRemaining: return Cooldown(ability, effects, out float remaining) ? remaining : 0f;
                default: return 1f;
            }
        }

        // The ability's cooldown effect on the shelf, and the longest time one has left; by index, as the shelf's own
        // lookup by tag allocates
        private static bool Cooldown(Ability ability, EffectSystemComponent effects, out float remaining)
        {
            remaining = 0f;
            if (ability.Cooldown == null || effects == null) return false;
            bool cooling = false;
            var shelf = effects.Shelf;
            for (int i = 0; i < shelf.Count; i++)
            {
                var container = shelf[i];
                if (container?.Spec == null || container.Spec.Base != ability.Cooldown) continue;
                cooling = true;
                if (container.DurationRemaining > remaining) remaining = container.DurationRemaining;
            }
            return cooling;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Items
        // ═══════════════════════════════════════════════════════════════════════════

        private void Items(ItemSystemComponent items, ref int found, ref float value)
        {
            if (items == null || items.ItemCount == 0) return;
            var containers = items.GetItemContainers();
            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                var item = container?.Spec?.Base;
                if (item == null || item.Tags == null || !Carries(item.Tags.AssetTag, item.Tags.ContextTags)) continue;
                Join(Read(container), ref found, ref value);
            }
        }

        private float Read(ItemSpecContainer container)
        {
            switch (ItemPart)
            {
                case EItemPart.Stacks: return container.ItemStacks;
                case EItemPart.Level: return container.Spec.GetLevel().CurrentValue;
                case EItemPart.IsEquipped: return container.IsEquipped ? 1f : 0f;
                default: return 1f;
            }
        }

        // Whether an ability or an item carries the tag, or one under it: as its asset tag or a context tag
        private bool Carries(Tag asset, List<Tag> context)
        {
            if (asset.MatchesOrIsChildOf(Tagged)) return true;
            if (context == null) return false;
            for (int i = 0; i < context.Count; i++)
                if (context[i].MatchesOrIsChildOf(Tagged)) return true;
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // In words
        // ═══════════════════════════════════════════════════════════════════════════

        // "the number of Fire effects on the target", "the total stacks of Poison effects on the target",
        // "how many Fireball abilities on the caster are active", "the highest level of Sword items on the caster"
        public override string Describe()
        {
            var tag = Tagged.IsValid ? Tagged.ToString() : "tagged";
            var nouns = What switch
            {
                EAssetPartOf.Effect => "effects",
                EAssetPartOf.Ability => "abilities",
                _ => "items",
            };
            var those = $"{tag} {nouns} on the {(On == NumberSide.Caster ? "caster" : "target")}";
            if (IsCount) return $"the number of {those}";

            string state = What switch
            {
                EAssetPartOf.Ability when AbilityPart == EAbilityPart.IsActive => "active",
                EAssetPartOf.Ability when AbilityPart == EAbilityPart.IsOnCooldown => "on cooldown",
                EAssetPartOf.Item when ItemPart == EItemPart.IsEquipped => "equipped",
                _ => null,
            };
            if (state != null)
            {
                return Among == EAssetPartAmong.Sum ? $"how many {those} are {state}" : $"whether {those} are {state}";
            }

            var part = What switch
            {
                EAssetPartOf.Effect when EffectPart == EEffectPart.WorkerValue => WorkerKey.IsValid ? WorkerKey.ToString() : "worker value",
                EAssetPartOf.Effect => NumberText.Spaced(EffectPart.ToString()).ToLowerInvariant(),
                EAssetPartOf.Ability => NumberText.Spaced(AbilityPart.ToString()).ToLowerInvariant(),
                _ => NumberText.Spaced(ItemPart.ToString()).ToLowerInvariant(),
            };
            var among = Among switch
            {
                EAssetPartAmong.First => "the first",
                EAssetPartAmong.Highest => "the highest",
                EAssetPartAmong.Lowest => "the lowest",
                _ => "the total",
            };
            return $"{among} {part} of {those}";
        }
    }
}
