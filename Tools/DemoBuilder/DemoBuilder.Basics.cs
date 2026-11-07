using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Samples.Demo.Building
{
    public static partial class DemoBuilder
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes
        // ═══════════════════════════════════════════════════════════════════════════

        private static ForgeAttribute Health, Mana, Armor, MagicResist, MoveSpeed, Strength, Agility, Intelligence;
        private static ForgeAttribute AttackAmp, MagicAmp, Lifesteal, CooldownReduction;
        private static ForgeAttribute ProjectileSpeed, AreaRadius, Lifetime, TickInterval, Heat;

        private static ForgeAttribute Attribute(string name, string abbreviation, string description, string folder = "Attributes")
        {
            var attribute = ScriptableObject.CreateInstance<ForgeAttribute>();
            attribute.Name = name;
            attribute.Abbreviation = abbreviation;
            attribute.Description = description;
            attribute.Textures = new List<TextureItem>();
            return Save($"{folder}/{name}", attribute);
        }

        private static void MakeAttributes()
        {
            Health = Attribute("Health", "HP", "Life. Its base is the hero's base plus 10 × Strength; at 0 the hero dies.");
            Mana = Attribute("Mana", "MP", "What abilities cost. Its base is the hero's base plus 12 × Intelligence.");
            Armor = Attribute("Armor", "ARM", "Reduces physical damage taken: a blow does 100 / (100 + Armor) of itself. Its base is 8 × Agility.");
            MagicResist = Attribute("Magic Resistance", "MR", "Reduces magic damage taken, by its share: 0.25 takes a quarter off. At most 0.75.");
            MoveSpeed = Attribute("Movement Speed", "MS", "How fast the hero walks, in units a second.");
            Strength = Attribute("Strength", "STR", "A primary attribute. Grows each level, and adds 10 Health each.");
            Agility = Attribute("Agility", "AGI", "A primary attribute. Grows each level, and adds 8 Armor each.");
            Intelligence = Attribute("Intelligence", "INT", "A primary attribute. Grows each level, and adds 12 Mana each.");
            AttackAmp = Attribute("Attack Amplification", "AA", "Amplifies physical damage dealt: × (1 + AA).");
            MagicAmp = Attribute("Magic Amplification", "MA", "Amplifies magic damage dealt: × (1 + MA).");
            Lifesteal = Attribute("Lifesteal", "LS", "Heals by this share of the physical damage dealt.");
            CooldownReduction = Attribute("Cooldown Reduction", "CDR", "Shortens cooldowns: × (1 − CDR). Every hero starts at 0. At most 0.6.");

            // What a mono process carries, by role (FEAT-24): a projectile's speed, a fire's size, life and pace
            ProjectileSpeed = Attribute("Projectile Speed", "SPD", "How fast a projectile flies, in units a second. The projectile carries it.", "Attributes/Process");
            AreaRadius = Attribute("Area Radius", "RAD", "How far a fire on the ground reaches. The fire carries it, by its ability's level.", "Attributes/Process");
            Lifetime = Attribute("Lifetime", "LIFE", "How many seconds a fire on the ground burns. The fire carries it.", "Attributes/Process");
            TickInterval = Attribute("Tick Interval", "TICK", "The seconds between a fire's burns. The fire carries it.", "Attributes/Process");
            Heat = Attribute("Heat", "HEAT", "How hard a fire's burns hit: its burns' damage times it. The fire carries it, by its ability's level, and the burns it applies read it by its role.", "Attributes/Process");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Shared scalers
        // ═══════════════════════════════════════════════════════════════════════════

        private static ScalerAsset AttackGrowth;

        private static void MakeScalers()
        {
            var growth = ScriptableObject.CreateInstance<ScalerAsset>();
            growth.Description = "How much harder every hero's default attack hits at each of its levels. Each attack's damage reads it, so one edit changes every hero's attack.";
            growth.Category = "Demo";
            growth.Scaler = Named(Levels(1f, 1.15f, 1.3f, 1.45f), "Attack growth");
            AttackGrowth = Save("Scalers/Attack Growth", growth);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The cues' looks
        // ═══════════════════════════════════════════════════════════════════════════

        private static GameObject HitSpark, MagicSpark, StunHalo, GoldAura, SteelAura, SmokeAura, HealGlow, FireBurst, ThunderRing;

        private static GameObject CueLook(string name, PrimitiveType shape, Vector3 size, Vector3 offset, Color color, float pulse = 0f) =>
            Prefab<Transform>($"Cues/{name}", (host, _) => Look(host, shape, size, offset, color, pulse)).gameObject;

        private static void MakePrefabsForCues()
        {
            HitSpark = CueLook("Hit Spark", PrimitiveType.Sphere, Vector3.one * 0.55f, new Vector3(0f, 1.2f, 0f), new Color(1f, 0.92f, 0.7f), 0.35f);
            MagicSpark = CueLook("Magic Spark", PrimitiveType.Sphere, Vector3.one * 0.7f, new Vector3(0f, 1.2f, 0f), new Color(0.55f, 0.7f, 1f), 0.4f);
            StunHalo = CueLook("Stun Halo", PrimitiveType.Cylinder, new Vector3(0.9f, 0.03f, 0.9f), new Vector3(0f, 2.35f, 0f), new Color(1f, 0.85f, 0.2f));
            GoldAura = CueLook("Gold Aura", PrimitiveType.Cylinder, new Vector3(2.4f, 0.02f, 2.4f), new Vector3(0f, 0.04f, 0f), new Color(1f, 0.8f, 0.3f));
            SteelAura = CueLook("Steel Aura", PrimitiveType.Cylinder, new Vector3(1.8f, 0.02f, 1.8f), new Vector3(0f, 0.05f, 0f), new Color(0.75f, 0.8f, 0.85f));
            SmokeAura = CueLook("Smoke Aura", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.04f, 0f), new Color(0.25f, 0.18f, 0.32f));
            HealGlow = CueLook("Heal Glow", PrimitiveType.Sphere, new Vector3(1.4f, 2.2f, 1.4f), new Vector3(0f, 1f, 0f), new Color(0.45f, 1f, 0.55f), 0.6f);
            FireBurst = CueLook("Fire Burst", PrimitiveType.Sphere, Vector3.one * 3f, new Vector3(0f, 1f, 0f), new Color(1f, 0.45f, 0.1f), 0.5f);
            ThunderRing = CueLook("Thunder Ring", PrimitiveType.Cylinder, new Vector3(8f, 0.02f, 8f), new Vector3(0f, 0.06f, 0f), new Color(0.6f, 0.8f, 1f), 0.45f);
        }

        private static PrefabCue Burst(GameObject look, float seconds = 0.6f) => new() { Prefab = look, WhileActive = false, Lifetime = seconds };
        private static PrefabCue WhileOn(GameObject look) => new() { Prefab = look, WhileActive = true, Lifetime = 0f };
        private static FloatingTextCue Word(string text, Color color) => new() { Text = text, Color = color, OnEachTick = false };

        private static readonly Color StunYellow = new(1f, 0.85f, 0.3f), DebuffRed = new(1f, 0.5f, 0.45f), BuffGreen = new(0.5f, 1f, 0.6f);
        private static readonly Color FireOrange = new(1f, 0.6f, 0.25f), ArcaneBlue = new(0.6f, 0.75f, 1f);

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An effect with every list empty and every field the plain default: instant, changing nothing, unseen.</summary>
        private static GameplayEffect NewEffect(string name, string description)
        {
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.Definition = new GameplayEffectDefinition
            {
                Name = name, Description = description, Visibility = Tags.NOT_VISIBLE, RetentionGroup = Tags.IgnoreRetention,
                Textures = new List<TextureItem>(),
            };
            effect.Tags = new GameplayEffectTags
            {
                AssetTag = T($"Demo.Effect.{Key(name)}"), ContextTags = new List<Tag>(), GrantedTags = new List<Tag>(),
                RemovesEffectsTagged = new List<Tag>(), BlocksEffectsTagged = new List<Tag>(),
            };
            effect.GrantedAbilities = new List<Ability>();
            effect.ImpactSpecification = new GameplayEffectImpact
            {
                AttributeTarget = null, TargetImpact = EAttributeTarget.Current, ImpactOperation = ECalculationOperation.Add,
                AffiliationPolicy = EAffiliationPolicy.AlwaysAllow, Affiliations = new List<Tag>(), ChanceToApply = Fixed(1f),
                ImpactTypes = new List<Tag>(), MagnitudeOperation = Fixed(0f), AdditionalImpacts = new List<AttributeImpactEntry>(),
                Packets = System.Array.Empty<ContainedEffectPacket>(),
            };
            effect.DurationSpecification = new GameplayEffectDuration
            {
                DurationPolicy = EEffectDurationPolicy.Instant, DurationOperation = Fixed(0f), RealDeltaTime = EMagnitudeOperation.AddScaler,
                EnablePeriodicTicks = false, TickOnApplication = false, TicksOperation = Count(0), TickIntervalOperation = Fixed(1f),
                AdditionalExecuteTicksOperation = Count(0), ReApplicationPolicy = EEffectReApplicationPolicy.AppendNewContainer,
                ReApplicationInteraction = EEffectInteractionPolicy.DoNothing, StackPolicy = EEffectStackPolicy.StacksShareOneDuration,
                StackingConfigs = new List<AbstractStackingConfig>(), StackAmountOperation = Count(1),
            };
            effect.Workers = new List<AbstractEffectWorker>();
            effect.Cues = new List<AbstractCueHandler>();
            effect.SourceRequirements = NoRequirements();
            effect.TargetRequirements = NoRequirements();
            return effect;
        }

        private static void Seen(GameplayEffect effect, Tag kind)
        {
            effect.Definition.Visibility = Tags.ALWAYS_VISIBLE;
            var tags = effect.Tags;
            tags.ContextTags.Add(kind);
            effect.Tags = tags;
        }

        private static void Lasts(GameplayEffect effect, ScalerMagnitudeOperation seconds, EEffectReApplicationPolicy again = EEffectReApplicationPolicy.ReplaceOldContainer)
        {
            effect.DurationSpecification.DurationPolicy = EEffectDurationPolicy.Durational;
            effect.DurationSpecification.DurationOperation = seconds;
            effect.DurationSpecification.ReApplicationPolicy = again;
        }

        private static void Ticks(GameplayEffect effect, int ticks, float interval)
        {
            effect.DurationSpecification.EnablePeriodicTicks = true;
            effect.DurationSpecification.TicksOperation = Count(ticks);
            effect.DurationSpecification.TickIntervalOperation = Fixed(interval);
        }

        private static void Grants(GameplayEffect effect, params Tag[] granted)
        {
            var tags = effect.Tags;
            tags.GrantedTags.AddRange(granted);
            effect.Tags = tags;
        }

        private static void Context(GameplayEffect effect, params Tag[] context)
        {
            var tags = effect.Tags;
            foreach (var tag in context)
                if (!tags.ContextTags.Contains(tag)) tags.ContextTags.Add(tag);
            effect.Tags = tags;
        }

        /// <summary>A lasting change to an attribute's current value while the effect is on, which goes as it ends.</summary>
        private static void Changes(GameplayEffect effect, ForgeAttribute attribute, ScalerMagnitudeOperation by)
        {
            effect.ImpactSpecification.AttributeTarget = attribute;
            effect.ImpactSpecification.TargetImpact = EAttributeTarget.Current;
            effect.ImpactSpecification.MagnitudeOperation = by;
            effect.ImpactSpecification.ReverseImpactOnRemoval = true;
        }

        private static GameplayEffect PhysicalStrike, MagicStrike, PureStrike, StatBonus;

        /// <summary>A blow's template: an instant change to Health of its damage type, to the other side only, with a spark.</summary>
        private static GameplayEffect StrikeTemplate(string name, Tag kind, GameObject spark)
        {
            var effect = NewEffect(name, $"The template every {kind.GetLeafName().ToLowerInvariant()} blow takes: an instant blow to Health of its damage type, which lands only on the other side. A blow sets only its own magnitude and context.");
            effect.ImpactSpecification.AttributeTarget = Health;
            effect.ImpactSpecification.ImpactTypes = new List<Tag> { kind };
            effect.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            effect.ImpactSpecification.MagnitudeOperation = Damage(new[] { 50f }, null, 0f, kind);
            effect.Cues.Add(Burst(spark, 0.4f));
            return Save($"Effects/Templates/{name}", effect);
        }

        private static void MakeTemplates()
        {
            PhysicalStrike = StrikeTemplate("Physical Strike", Physical, HitSpark);
            MagicStrike = StrikeTemplate("Magic Strike", Magic, MagicSpark);
            PureStrike = StrikeTemplate("Pure Strike", Pure, HitSpark);

            var bonus = NewEffect("Stat Bonus", "The template every item's bonus takes: a change to an attribute while the item is equipped, in the Bonus retention group, so the HUD shows it apart as \"(+8)\", and never shown as a buff.");
            bonus.DurationSpecification.DurationPolicy = EEffectDurationPolicy.Infinite;
            bonus.Definition.RetentionGroup = Tags.ADDITIONAL;
            Changes(bonus, Strength, Scaled(Named(Levels(8f, 12f, 16f), "Bonus, by the item's level")));
            StatBonus = Save("Effects/Templates/Stat Bonus", bonus);
        }

        /// <summary>A blow from a template: its own name, magnitude, context and level provider.</summary>
        private static GameplayEffect Blow(string folder, string name, string description, GameplayEffect template, ScalerMagnitudeOperation damage,
            BaseForgeLevelProvider by, Tag hit)
        {
            var effect = ChildOf(template);
            effect.Definition.Name = name;
            effect.Definition.Description = description;
            var tags = effect.Tags;
            tags.AssetTag = T($"Demo.Effect.{Key(name)}");
            tags.ContextTags = new List<Tag> { hit };
            effect.Tags = tags;
            effect.ImpactSpecification.MagnitudeOperation = damage;
            if (by != null) effect.LinkToProvider(by);
            return Settle(Save($"{folder}/{name}", effect));
        }

        /// <summary>An item's bonus to an attribute, from the Stat Bonus template, linked to its item.</summary>
        private static GameplayEffect Bonus(Item item, ForgeAttribute attribute, ScalerMagnitudeOperation by, string name = null)
        {
            var effect = ChildOf(StatBonus);
            name ??= $"{item.GetName()} {attribute.Name}";
            effect.Definition.Name = name;
            effect.Definition.Description = $"{item.GetName()}'s bonus to {attribute.Name}, while it's equipped.";
            var tags = effect.Tags;
            tags.AssetTag = T($"Demo.Effect.{Key(name)}");
            effect.Tags = tags;
            effect.ImpactSpecification.AttributeTarget = attribute;
            effect.ImpactSpecification.MagnitudeOperation = by;
            effect.LinkToProvider(item);
            return Settle(Save($"Items/Effects/{name}", effect));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attribute sets
        // ═══════════════════════════════════════════════════════════════════════════

        private static AttributeSet HeroSet, DummySet;

        private static AttributeSetElement Element(ForgeAttribute attribute, AttributeMagnitudeSpec @base,
            EAttributeOverflowPolicy overflow = EAttributeOverflowPolicy.Unlimited, float ceiling = 0f, bool scaleWithBase = false)
        {
            return new AttributeSetElement
            {
                Attribute = attribute,
                Current = new AttributeMagnitudeSpec { Magnitude = 0f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Base = @base,
                LinkCurrentToBase = true,
                Overflow = new AttributeOverflowData
                {
                    Policy = overflow, Floor = new AttributeValue(0f, 0f), Ceil = new AttributeValue(ceiling, ceiling),
                },
                RetentionGroup = Tags.DEFAULT,
                CollisionPolicy = EAttributeElementCollisionPolicy.UseSetCollisionSetting,
                Constraints = new AttributeConstraints { AutoClamp = true, AutoScaleWithBase = scaleWithBase, RoundingMode = EAttributeRoundingPolicy.None },
            };
        }

        private static AttributeMagnitudeSpec Flat(float value) => new() { Magnitude = value, RealMagnitude = EMagnitudeOperation.UseMagnitude };

        /// <summary>A base of a number plus a primary attribute times a constant, which follows the primary as it changes.</summary>
        private static AttributeMagnitudeSpec PlusPrimary(float value, ForgeAttribute primary, float each) => new()
        {
            Magnitude = value,
            RealMagnitude = EMagnitudeOperation.AddScaler,
            Scaling = Named(Table(new CachedAttributeBackedScaler
            {
                CaptureAttribute = primary, CaptureWhat = EAttributeTargetBinary.Current, RelativeOperation = EScalerRelativeOperation.Multiply,
            }, Enumerable.Repeat(each, 10).ToArray()), $"{each} × {primary.Name}"),
        };

        /// <summary>A primary's base by the hero's level, 1 to 10.</summary>
        private static AttributeMagnitudeSpec ByLevel(float first, float each) => new()
        {
            Magnitude = 0f,
            RealMagnitude = EMagnitudeOperation.UseScaler,
            Scaling = Named(Table(new CachedSimpleScaler(), Enumerable.Range(0, 10).Select(level => Mathf.Round(first + each * level)).ToArray()), "By the hero's level"),
        };

        private static AttributeSet NewSet(string name, string description, params AttributeSetElement[] elements)
        {
            var set = ScriptableObject.CreateInstance<AttributeSet>();
            set.Name = name;
            set.Description = description;
            set.Textures = new List<TextureItem>();
            set.AssetTag = T($"Demo.Set.{Key(name)}");
            set.Attributes = elements.ToList();
            set.SubSets = new List<AttributeSet>();
            set.CollisionResolutionPolicy = EValueCollisionPolicy.UseFirst;
            set.WorkerGroup = new StandardWorkerGroup { Name = "Workers" };
            set.StartingLevel = 1;
            set.MaxLevel = 10;
            return set;
        }

        /// <summary>The damage model, as each hero's and the dummy's own workers: what resists a blow, and what amplifies it.</summary>
        private static void DamageModel(StandardWorkerGroup workers)
        {
            workers.AttributeWorkers.Add(Mitigation(Armor, EMitigation.Armor, Physical));
            workers.AttributeWorkers.Add(Mitigation(MagicResist, EMitigation.Share, Magic));
            workers.AttributeWorkers.Add(Amplification(AttackAmp, Physical));
            workers.AttributeWorkers.Add(Amplification(MagicAmp, Magic));
        }

        private static MitigationWorker Mitigation(ForgeAttribute resistance, EMitigation kind, Tag type) => new()
        {
            TimingConfig = AbstractAttributeWorker.EChangeEventTiming.PreChange, TargetAttribute = Health, AllowSelfModification = true,
            TargetModification = EAttributeTargetExpanded.Current, SignPolicy = ESignPolicyExtended.Negative,
            AnyImpactType = false, ImpactTypes = new List<Tag> { type }, AnyContextTag = true, ValidContextTags = new List<Tag>(),
            Resistance = resistance, Kind = kind,
        };

        private static AmplificationWorker Amplification(ForgeAttribute amplification, Tag type) => new()
        {
            TimingConfig = AbstractAttributeWorker.EChangeEventTiming.PreChange, TargetAttribute = Health, AllowSelfModification = true,
            TargetModification = EAttributeTargetExpanded.Current, SignPolicy = ESignPolicyExtended.Negative,
            AnyImpactType = false, ImpactTypes = new List<Tag> { type }, AnyContextTag = true, ValidContextTags = new List<Tag>(),
            Amplification = amplification,
        };

        private static void MakeAttributeSets()
        {
            var vitals = Save("Attribute Sets/Hero/Vitals", NewSet("Vitals", "Health, Mana and Armor, each a base plus a primary attribute times its constant, which follows the primary as items and levels change it. Health and Mana stay between 0 and their base.",
                Element(Health, PlusPrimary(150f, Strength, 10f), EAttributeOverflowPolicy.ZeroToBase, scaleWithBase: true),
                Element(Mana, PlusPrimary(100f, Intelligence, 12f), EAttributeOverflowPolicy.ZeroToBase, scaleWithBase: true),
                Element(Armor, PlusPrimary(0f, Agility, 8f))));
            var primaries = Save("Attribute Sets/Hero/Primaries", NewSet("Primaries", "Strength, Agility and Intelligence by the hero's level. Each hero's own set gives its own growth.",
                Element(Strength, ByLevel(15f, 2f)), Element(Agility, ByLevel(15f, 2f)), Element(Intelligence, ByLevel(15f, 2f))));
            var offense = Save("Attribute Sets/Hero/Offense", NewSet("Offense", "Attack Amplification, Magic Amplification and Lifesteal, at 0 until items add to them.",
                Element(AttackAmp, Flat(0f)), Element(MagicAmp, Flat(0f)), Element(Lifesteal, Flat(0f))));
            var defense = Save("Attribute Sets/Hero/Defense", NewSet("Defense", "Magic Resistance, kept between 0 and 0.75 by its bounds.",
                Element(MagicResist, Flat(0.1f), EAttributeOverflowPolicy.ZeroToCeil, 0.75f)));
            var utility = Save("Attribute Sets/Hero/Utility", NewSet("Utility", "Movement Speed, and Cooldown Reduction at 0, kept below 0.6 by its bounds.",
                Element(MoveSpeed, Flat(5.5f)), Element(CooldownReduction, Flat(0f), EAttributeOverflowPolicy.ZeroToCeil, 0.6f)));

            var hero = NewSet("Hero", "The template every hero's set takes: its sub-sets, and the workers of the damage model. A hero's own set gives only its primaries' growth, its speed and what of its vitals differs. A change here reaches all five.");
            hero.SubSets = new List<AttributeSet> { vitals, primaries, offense, defense, utility };
            DamageModel(hero.WorkerGroup);
            // A share of each physical blow back as health, by the hitter's Lifesteal: the framework's own worker
            hero.WorkerGroup.ImpactWorkers.Add(new RelativeOperationContextImpactWorker
            {
                ImpactedAttribute = Health, AnyImpactType = false, ImpactType = new List<Tag> { Physical },
                ImpactTarget = EAttributeTargetExpanded.CurrentOrBase, ImpactSign = ESignPolicy.Negative,
                AnyContextTag = true, ImpactAbilityContextTags = new List<Tag>(),
                WorkAttribute = Health, WorkImpactType = new List<Tag>(), WorkSignPolicy = ESignPolicy.Positive,
                RelativeAttribute = Lifesteal, Operation = ECalculationOperation.Multiply, WithRespectTo = ESourceTarget.Source,
                ExecutionMode = EWorkerExecution.Deferred,
            });
            HeroSet = Save("Attribute Sets/Hero/Hero", hero);

            var dummy = NewSet("Training Dummy Attributes", "The dummy's own: a deep pool of Health, some Armor and Magic Resistance, and the damage model's workers, so it takes blows as a hero does. No sub-sets.",
                Element(Health, Flat(4000f), EAttributeOverflowPolicy.ZeroToBase),
                Element(Armor, Flat(60f)),
                Element(MagicResist, Flat(0.2f), EAttributeOverflowPolicy.ZeroToCeil, 0.75f),
                Element(MoveSpeed, Flat(0f)));
            DamageModel(dummy.WorkerGroup);
            DummySet = Save("Attribute Sets/Training Dummy Attributes", dummy);
        }

        /// <summary>A hero's own set: a child of Hero, with its own primaries' growth and speed, and any vital it changes.</summary>
        private static AttributeSet HeroAttributes(string hero, (float first, float each) strength, (float first, float each) agility,
            (float first, float each) intelligence, float speed, params AttributeSetElement[] vitals)
        {
            var set = ChildOf(HeroSet);
            set.Name = $"{hero} Attributes";
            set.Description = $"The {hero}'s own: its primaries' growth, its speed and its vitals' bases. The rest comes from the Hero template.";
            set.AssetTag = T($"Demo.Set.{Key(hero)}");
            set.Attributes = new List<AttributeSetElement>
            {
                Element(Strength, ByLevel(strength.first, strength.each)),
                Element(Agility, ByLevel(agility.first, agility.each)),
                Element(Intelligence, ByLevel(intelligence.first, intelligence.each)),
                Element(MoveSpeed, Flat(speed)),
            };
            set.Attributes.AddRange(vitals);
            return Settle(Save($"Attribute Sets/{hero} Attributes", set));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effects several share
        // ═══════════════════════════════════════════════════════════════════════════

        private static GameplayEffect BurningEffect, FlashpointEffect, StunEffect, DummyMend, Regeneration;

        private static void MakeSharedEffects()
        {
            var flashpoint = NewEffect("Flashpoint", "Burning's overflow: a sixth stack bursts, for magic damage, and the burning ends.");
            flashpoint.ImpactSpecification.AttributeTarget = Health;
            flashpoint.ImpactSpecification.ImpactTypes = new List<Tag> { Magic };
            flashpoint.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            flashpoint.ImpactSpecification.MagnitudeOperation = Damage(new[] { 90f, 120f, 150f, 180f }, Intelligence, 0.8f, Magic);
            Context(flashpoint, AbilityHit);
            flashpoint.Cues.Add(Word("Flashpoint!", FireOrange));
            flashpoint.Cues.Add(Burst(FireBurst, 0.6f));
            flashpoint.Cues.Add(new CameraShakeCue { Strength = 0.5f, Duration = 0.25f });
            FlashpointEffect = Save("Effects/Flashpoint", flashpoint);

            var burning = NewEffect("Burning", "Magic damage each second for 4 seconds, by its stacks: each Fireball, Flame Wall burn or Ember Brand hit adds one and starts its time again. A sixth stack bursts as Flashpoint.");
            Lasts(burning, Fixed(4f), EEffectReApplicationPolicy.StackExistingContainers);
            burning.DurationSpecification.ReApplicationInteraction = EEffectInteractionPolicy.RefreshContainerDuration;
            Ticks(burning, 4, 1f);
            burning.ImpactSpecification.AttributeTarget = Health;
            burning.ImpactSpecification.ImpactTypes = new List<Tag> { Magic };
            burning.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            // Its stacks, times the heat of a fire that applied it, which the fire carries and its burns take (FEAT-28)
            burning.ImpactSpecification.MagnitudeOperation = Damage(new[] { 8f, 10f, 12f, 14f }, null, 0f, Magic,
                extra: Named(Group((Named(new EffectStacksScaler { Behaviours = new List<AbstractScalerBehaviour>() }, "Its stacks"), Add),
                    (Named(new SetByCallerScaler { Key = FireHeat, Default = 1f, Behaviours = new List<AbstractScalerBehaviour>() }, "A fire's heat"), Multiply)),
                    "Its stacks, times a fire's heat"));
            burning.DurationSpecification.StackingConfigs.Add(new StackBoundsConfig
            {
                Min = 0, Max = 5, OnMaxPolicy = EStackBoundsOnMaxPolicy.DoNothing, OverflowEffects = new List<GameplayEffect> { FlashpointEffect },
                ClearStacksOnOverflow = true,
            });
            Seen(burning, Debuff);
            Context(burning, AbilityHit);
            Grants(burning, Burning);
            burning.Cues.Add(Word("Burning", FireOrange));
            BurningEffect = Save("Effects/Burning", burning);

            var stun = NewEffect("Stunned", "Can't move, attack or cast for a moment. Iron Skin blocks it and takes it off.");
            Lasts(stun, Fixed(1f));
            stun.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            Seen(stun, Debuff);
            Grants(stun, Stunned);
            stun.Cues.Add(Word("Stunned", StunYellow));
            stun.Cues.Add(WhileOn(StunHalo));
            StunEffect = Save("Effects/Stunned", stun);

            var mend = NewEffect("Dummy Mend", "Mends the training dummy to full Health after it rests a few seconds.");
            mend.ImpactSpecification.AttributeTarget = Health;
            mend.ImpactSpecification.MagnitudeOperation = Fixed(100000f);
            DummyMend = Save("Effects/Dummy Mend", mend);
        }

        /// <summary>A stun of its own length and level provider, from the shared one.</summary>
        private static GameplayEffect Stun(string folder, string name, ScalerMagnitudeOperation seconds, BaseForgeLevelProvider by)
        {
            var stun = Object.Instantiate(StunEffect);
            stun.Definition.Name = name;
            var tags = stun.Tags;
            tags.AssetTag = T($"Demo.Effect.{Key(name)}");
            stun.Tags = tags;
            stun.DurationSpecification.DurationOperation = seconds;
            stun.LinkToProvider(by);
            return Save($"{folder}/{name}", stun);
        }
    }
}
