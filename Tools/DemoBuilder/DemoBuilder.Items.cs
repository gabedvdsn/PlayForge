using System.Collections.Generic;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace FarEmerald.PlayForge.Samples.Demo.Building
{
    public static partial class DemoBuilder
    {
        private static readonly List<Item> AllItems = new();
        private static Item StatItemTemplate;

        private static Item NewItem(string name, string description, int maxLevel = 3, int stack = 1)
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.Definition = new ItemDefinition
            {
                Name = name, Description = description, Visibility = Tags.ALWAYS_VISIBLE, Textures = new List<TextureItem>(), MaxStackSize = stack,
            };
            item.Tags = new ItemTags { AssetTag = T($"Demo.Item.{Key(name)}"), ContextTags = new List<Tag>(), PassiveGrantedTags = new List<Tag>() };
            item.StartingLevel = 1;
            item.MaxLevel = maxLevel;
            item.GrantedEffects = new List<GameplayEffect>();
            item.ActiveAbility = null;
            item.WorkerGroup = new StandardWorkerGroup { Name = "Workers" };
            item.LocalData = new List<DataWrapper>();
            return item;
        }

        /// <summary>An item from the Stat Item template, saved first so its bonuses can take it as their level provider.</summary>
        private static Item StatItem(string name, string description)
        {
            var item = ChildOf(StatItemTemplate);
            item.Definition.Name = name;
            item.Definition.Description = description;
            item.Tags.AssetTag = T($"Demo.Item.{Key(name)}");
            return Save($"Items/{name}", item);
        }

        private static Item Gives(Item item, params GameplayEffect[] bonuses)
        {
            item.GrantedEffects = new List<GameplayEffect>(bonuses);
            AllItems.Add(item);
            return Settle(Dirty(item));
        }

        private static ScalerMagnitudeOperation ByItemLevel(params float[] values) => Scaled(Named(Levels(values), "By the item's level"));

        private static void MakeItems()
        {
            AllItems.Clear();
            var template = NewItem("Stat Item", "The template every stat item takes: three levels, one to a slot, and the Item.Stat context tag. An item sets its own bonuses.");
            template.Tags.ContextTags.Add(StatItemTag);
            StatItemTemplate = Save("Items/Templates/Stat Item", template);

            var gauntlets = StatItem("Iron Gauntlets", "+8 Strength, more by level.");
            Gives(gauntlets, Bonus(gauntlets, Strength, ByItemLevel(8f, 12f, 16f)));

            var edge = StatItem("Duelist's Edge", "+8 Agility and +10% Attack Amplification: two granted effects.");
            Gives(edge, Bonus(edge, Agility, ByItemLevel(8f, 12f, 16f)), Bonus(edge, AttackAmp, ByItemLevel(0.1f, 0.15f, 0.2f)));

            var tome = StatItem("Arcane Tome", "+8 Intelligence and +10% Magic Amplification.");
            Gives(tome, Bonus(tome, Intelligence, ByItemLevel(8f, 12f, 16f)), Bonus(tome, MagicAmp, ByItemLevel(0.1f, 0.15f, 0.2f)));

            var plate = StatItem("Warden Plate", "+30 Armor, more by level.");
            Gives(plate, Bonus(plate, Armor, ByItemLevel(30f, 45f, 60f)));

            var veil = StatItem("Mystic Veil", "+20% Magic Resistance. Magic Resistance's bounds keep it at 75% at most.");
            Gives(veil, Bonus(veil, MagicResist, ByItemLevel(0.2f, 0.25f, 0.3f)));

            var boots = StatItem("Swift Boots", "+1.5 Movement Speed.");
            Gives(boots, Bonus(boots, MoveSpeed, ByItemLevel(1.5f, 2f, 2.5f)));

            var hourglass = StatItem("Hourglass", "+15% Cooldown Reduction, which every ability's cooldown reads.");
            Gives(hourglass, Bonus(hourglass, CooldownReduction, ByItemLevel(0.15f, 0.2f, 0.25f)));

            var fang = StatItem("Vampiric Fang", "+15% Lifesteal: the Hero set's lifesteal worker heals by it.");
            Gives(fang, Bonus(fang, Lifesteal, ByItemLevel(0.15f, 0.2f, 0.25f)));

            // Default attacks set their target burning: the demo's own impact worker, in the item's worker group
            var brand = Save("Items/Ember Brand", NewItem("Ember Brand", "Default attacks apply a stack of Burning, by the demo's own impact worker in the item's worker group.", maxLevel: 1));
            brand.WorkerGroup.ImpactWorkers.Add(new ApplyEffectOnHitWorker
            {
                Effect = BurningEffect, ImpactedAttribute = Health, AnyImpactType = true, ImpactType = new List<Tag>(),
                ImpactTarget = EAttributeTargetExpanded.CurrentOrBase, ImpactSign = ESignPolicy.Negative, AllowSelfImpact = false,
                AnyContextTag = false, ContextMatchingPolicy = EAnyAllPolicy.Any, ImpactAbilityContextTags = new List<Tag> { AttackHit },
                WorkAttribute = null, WorkImpactType = new List<Tag>(), WorkSignPolicy = ESignPolicy.Positive,
            });
            Gives(brand);

            // An item's active ability
            var charm = Save("Items/Blink Charm", NewItem("Blink Charm", "Its ability, Blink, puts its holder at a point a short way off, at once.", maxLevel: 1));
            var blink = NewAbility("Items/Abilities", "Blink", "Blinks to a point up to 6 away.", Kind.Item, maxLevel: 1);
            Cools(blink, "Items/Abilities", true, 12f);
            AiHint(blink, "Low", 0f);
            Finish(blink, PickPoint(), Spend(new BlinkTask { Reach = 6f, Behind = 0f }));
            blink.LinkToProvider(charm);
            charm.ActiveAbility = Dirty(blink);
            Gives(charm);

            // Stacks to three; each use takes one
            var salve = Save("Items/Healing Salve", NewItem("Healing Salve", "Its ability heals over 6 seconds and uses one up. Holds three at most; the last one used, it goes.", stack: 3));
            var salveAbility = NewAbility("Items/Abilities", "Salve", "Heals over 6 seconds, and uses up one Healing Salve.", Kind.Item, maxLevel: 3);
            var mending = NewEffect("Mending", "Health each second for 6 seconds.");
            Lasts(mending, Fixed(6f), EEffectReApplicationPolicy.AppendNewContainer);
            Ticks(mending, 6, 1f);
            mending.ImpactSpecification.AttributeTarget = Health;
            mending.ImpactSpecification.MagnitudeOperation = Healing(new[] { 25f, 35f, 45f });
            Seen(mending, Buff);
            mending.Cues.Add(Burst(HealGlow, 0.6f));
            mending.LinkToProvider(salve);
            mending = Save("Items/Effects/Mending", mending);
            Cools(salveAbility, "Items/Abilities", false, 1f);
            AiHint(salveAbility, "Hurt", 0f);
            Finish(salveAbility, null, Spend(Apply(mending), new ConsumeItemTask { Item = salve }));
            salveAbility.LinkToProvider(salve);
            salve.ActiveAbility = Dirty(salveAbility);
            Gives(salve);

            // +3 Intelligence a shard, by an Item Stacks scaler
            var shard = Save("Items/Soul Shard", NewItem("Soul Shard", "+3 Intelligence for each shard held, by an Item Stacks scaler. Holds ten at most.", maxLevel: 1, stack: 10));
            Gives(shard, Bonus(shard, Intelligence, new ScalerMagnitudeOperation
            {
                Magnitude = 3f, RealMagnitude = EMagnitudeOperation.MultiplyWithScaler,
                Scaler = Named(new ItemStacksScaler { Behaviours = new List<AbstractScalerBehaviour>() }, "Shards held"),
            }, "Soul Shard Intelligence"));

            // Strength and an ability on one item
            var hammer = StatItem("Thunder Hammer", "+6 Strength, and Thunderclap: stuns the enemies around its holder, and shakes the camera.");
            var thunderclap = NewAbility("Items/Abilities", "Thunderclap", "Stuns the enemies within 4 and hits them for magic damage. Auto Target By Affinity finds them.", Kind.Item, maxLevel: 3);
            var thunderStun = Stun("Items/Effects", "Thunderclap Stun", Scaled(Named(Levels(0.75f, 1f, 1.25f), "Seconds, by level")), hammer);
            var thunderBlow = Blow("Items/Effects", "Thunderclap Damage", "Thunderclap's blow: magic, with a camera shake cue.", MagicStrike,
                Damage(new[] { 60f, 90f, 120f }, null, 0f, Magic), hammer, AbilityHit);
            thunderBlow.Cues.Add(new CameraShakeCue { Strength = 0.6f, Duration = 0.3f });
            thunderBlow.Cues.Add(Burst(ThunderRing, 0.5f));
            Settle(Dirty(thunderBlow));
            Cools(thunderclap, "Items/Abilities", true, 15f);
            AiHint(thunderclap, "InRange", 3.5f);
            Finish(thunderclap, new AutoTargetByAffinityTask
            {
                AffinityFilter = EAffinityFilter.Enemies, Mode = EAutoTargetMode.All, MaxTargets = 5, Range = 4f, DetectionLayers = ~0, ExcludeDead = true,
                Requirements = Avoid(Invisible, Untargetable), BreakRuntimeOnInvalid = true,
            }, Spend(Apply(thunderStun, thunderBlow)));
            thunderclap.LinkToProvider(hammer);
            hammer.ActiveAbility = Dirty(thunderclap);
            Gives(hammer, Bonus(hammer, Strength, ByItemLevel(6f, 9f, 12f)));

            // Intelligence by tier, by a Threshold scaler
            var crown = StatItem("Sage's Crown", "+Intelligence by tier: Bronze at level 1, Silver at 2, Gold at 3, by a Threshold scaler.");
            Gives(crown, Bonus(crown, Intelligence, Scaled(new ThresholdScaler
            {
                Name = "Tier, by the item's level", DefaultValue = 0f, Behaviours = new List<AbstractScalerBehaviour>(),
                Thresholds = new[]
                {
                    new LevelThreshold { Level = 1, Value = 5f, TierName = "Bronze" },
                    new LevelThreshold { Level = 2, Value = 10f, TierName = "Silver" },
                    new LevelThreshold { Level = 3, Value = 20f, TierName = "Gold" },
                },
            })));
        }
    }
}
