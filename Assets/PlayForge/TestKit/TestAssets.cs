using System.Collections.Generic;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>What <see cref="TestAssets.Effect"/> builds. Each field is the plain default unless a test needs otherwise.</summary>
    public sealed class EffectConfig
    {
        public EEffectDurationPolicy Policy = EEffectDurationPolicy.Infinite;
        public ForgeAttribute Target;
        public ECalculationOperation Op = ECalculationOperation.Add;
        public EAttributeTarget Slot = EAttributeTarget.Current;
        public EMultiplyBehaviour MultiplyBehaviour = EMultiplyBehaviour.Multiplicative;
        public float Magnitude;
        public AbstractScaler MagnitudeScaler;
        public EMagnitudeOperation MagnitudeOp = EMagnitudeOperation.UseMagnitude;
        public bool ReverseOnRemoval;

        public float Duration;
        public bool PeriodicTicks;
        public int Ticks;
        public bool TickOnApplication;
        public float FixedDeltaTime;

        public EEffectReApplicationPolicy Reapply = EEffectReApplicationPolicy.AppendNewContainer;
        public EEffectInteractionPolicy Interaction = EEffectInteractionPolicy.DoNothing;
        public EEffectStackPolicy StackPolicy = EEffectStackPolicy.StacksShareOneDuration;
        public int StackAmount = 1;

        public List<Tag> ImpactTypes = new();
        public List<Tag> GrantedTags = new();
        public List<AbstractEffectWorker> Workers = new();
    }

    /// <summary>
    /// Builds PlayForge's assets in code, for one test world (TW-26): attributes, sets, identities, effects, items and
    /// abilities, each ready to use. Everything it makes is destroyed with the world, and the tag names it gives are
    /// unique within it. A game's own assets, loaded from the project, are the test's to load.
    /// </summary>
    public sealed class TestAssets
    {
        private readonly List<ScriptableObject> made = new();
        private int unique;

        internal void DestroyAll()
        {
            foreach (var asset in made)
            {
                if (asset) Object.Destroy(asset);
            }
            made.Clear();
        }

        private T Own<T>(T asset) where T : ScriptableObject
        {
            made.Add(asset);
            return asset;
        }

        /// <summary>A name unique in the world: the stem and a number, as "Set_3".</summary>
        public string Unique(string stem) => $"{stem}_{unique++}";

        /// <summary>A tag of a name unique in the world.</summary>
        public Tag NewTag(string stem) => Tag.Generate(Unique(stem));

        public ForgeAttribute Attribute(string name)
        {
            var attribute = Own(ScriptableObject.CreateInstance<ForgeAttribute>());
            attribute.name = name;
            attribute.Name = name;
            attribute.Textures = new List<TextureItem>();
            return attribute;
        }

        /// <summary>
        /// An attribute's element in a set: its current and base, and how it overflows. A pool, as health, stays between 0
        /// and its base with <see cref="EAttributeOverflowPolicy.ZeroToBase"/>; a stat is unlimited.
        /// </summary>
        public AttributeSetElement Element(
            ForgeAttribute attribute, float current, float @base,
            EAttributeOverflowPolicy overflow = EAttributeOverflowPolicy.Unlimited,
            AttributeMagnitudeSpec currentSpecOverride = null,
            AttributeMagnitudeSpec baseSpecOverride = null,
            bool linkCurrentToBase = false)
        {
            return new AttributeSetElement
            {
                Attribute = attribute,
                Current = currentSpecOverride ?? new AttributeMagnitudeSpec { Magnitude = current, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Base = baseSpecOverride ?? new AttributeMagnitudeSpec { Magnitude = @base, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                LinkCurrentToBase = linkCurrentToBase,
                Overflow = new AttributeOverflowData { Policy = overflow },
                RetentionGroup = Tags.DEFAULT,
                Constraints = new AttributeConstraints { AutoClamp = true, RoundingMode = EAttributeRoundingPolicy.None },
            };
        }

        /// <summary>A pool's element, as health: between 0 and its base.</summary>
        public AttributeSetElement Pool(ForgeAttribute attribute, float current, float @base) =>
            Element(attribute, current, @base, EAttributeOverflowPolicy.ZeroToBase);

        /// <summary>A stat's element, its current its base.</summary>
        public AttributeSetElement Stat(ForgeAttribute attribute, float value) => Element(attribute, value, value);

        public AttributeSet Set(params AttributeSetElement[] elements)
        {
            var set = Own(ScriptableObject.CreateInstance<AttributeSet>());
            set.name = Unique("Set");
            set.Name = set.name;
            set.AssetTag = NewTag("Test.Set");
            set.Attributes = new List<AttributeSetElement>(elements);
            set.SubSets = new List<AttributeSet>();
            set.WorkerGroup = new StandardWorkerGroup();
            return set;
        }

        /// <summary>A set of one attribute nothing reads, for an entity whose attributes don't matter.</summary>
        public AttributeSet UnusedSet() => Set(Element(Attribute("Unused"), 1f, 1f));

        /// <summary>An entity identity asset, of its set, on no side, with room for 8 abilities and 8 items.</summary>
        public EntityIdentity Identity(string name, AttributeSet set)
        {
            var entity = Own(ScriptableObject.CreateInstance<EntityIdentity>());
            entity.name = entity.Name = Unique(name);
            entity.Description = "";
            entity.Textures = new List<TextureItem>();
            entity.AssetTag = NewTag("Test.Entity");
            entity.Affiliation = new List<Tag>();
            entity.GrantedTags = new List<Tag>();
            entity.ContextTags = new List<Tag>();
            entity.MaxLevel = 1;
            entity.MaxAbilitiesOperation = new ScalableInt { Magnitude = 8 };
            entity.MaxItemsOperation = new ScalableInt { Magnitude = 8 };
            entity.MaxEquippedItemsOperation = new ScalableInt { Magnitude = 8 };
            entity.StartingAbilities = new List<Ability>();
            entity.StartingItems = new List<StartingItemContainer>();
            entity.AttributeSet = set;
            entity.WorkerGroup = new StandardWorkerGroup();
            return entity;
        }

        /// <summary>A runtime GAS's identity, of its set, as <see cref="Identity"/> makes an asset's.</summary>
        public RuntimeEntityIdentity RuntimeIdentity(string name, AttributeSet set)
        {
            return new RuntimeEntityIdentity
            {
                Name = Unique(name),
                AssetTag = NewTag("Test.Entity"),
                GrantedTags = new List<Tag>(),
                ContextTags = new List<Tag>(),
                Affiliation = new List<Tag>(),
                AttributeSet = set,
                MaxAbilitiesOperation = new ScalableInt { Magnitude = 8 },
                MaxItemsOperation = new ScalableInt { Magnitude = 8 },
                MaxEquippedItemsOperation = new ScalableInt { Magnitude = 8 },
            };
        }

        /// <summary>An effect as <paramref name="cfg"/> says: one impact, its duration, and how it takes another application.</summary>
        public GameplayEffect Effect(string name, EffectConfig cfg)
        {
            var fx = Own(ScriptableObject.CreateInstance<GameplayEffect>());
            fx.name = name;
            fx.Definition = new GameplayEffectDefinition { Name = name, Textures = new List<TextureItem>() };
            fx.Tags = new GameplayEffectTags
            {
                AssetTag = NewTag($"Test.Effect.{name.Replace(" ", "")}"),
                ContextTags = new List<Tag>(),
                GrantedTags = cfg.GrantedTags,
            };
            fx.Workers = cfg.Workers;
            fx.SourceRequirements = EmptyRequirements();
            fx.TargetRequirements = EmptyRequirements();

            fx.ImpactSpecification = new GameplayEffectImpact
            {
                AttributeTarget = cfg.Target,
                TargetImpact = cfg.Slot,
                ImpactOperation = cfg.Op,
                MultiplyBehaviour = cfg.MultiplyBehaviour,
                AffiliationPolicy = EAffiliationPolicy.AlwaysAllow,
                ImpactTypes = new List<Tag>(cfg.ImpactTypes),
                ReverseImpactOnRemoval = cfg.ReverseOnRemoval,
                MagnitudeOperation = new ScalableFloat
                {
                    Magnitude = cfg.Magnitude,
                    Scaler = cfg.MagnitudeScaler,
                    RealMagnitude = cfg.MagnitudeOp,
                },
                Packets = System.Array.Empty<ContainedEffectPacket>(),
            };

            fx.DurationSpecification = new GameplayEffectDuration
            {
                DurationPolicy = cfg.Policy,
                DurationOperation = new ScalableFloat { Magnitude = cfg.Duration, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                EnablePeriodicTicks = cfg.PeriodicTicks,
                TickOnApplication = cfg.TickOnApplication,
                TicksOperation = new ScalableInt { Magnitude = cfg.Ticks },
                TickIntervalOperation = new ScalableFloat { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                AdditionalExecuteTicksOperation = new ScalableInt { Magnitude = 0 },
                StackAmountOperation = new ScalableInt { Magnitude = cfg.StackAmount },
                StackingConfigs = new List<AbstractStackingConfig>(),
                ReApplicationPolicy = cfg.Reapply,
                ReApplicationInteraction = cfg.Interaction,
                StackPolicy = cfg.StackPolicy,
                RealDeltaTime = cfg.FixedDeltaTime > 0f ? EMagnitudeOperation.UseScaler : EMagnitudeOperation.AddScaler,
                DeltaTimeScaler = cfg.FixedDeltaTime > 0f ? new ConstantScaler { Value = cfg.FixedDeltaTime } : null,
            };

            return fx;
        }

        // Groups with no queries: it applies, and nothing removes it on its own. A null group means removal at once
        // (EffectTagRequirements.CheckRemovalRequirements)
        private static EffectTagRequirements EmptyRequirements()
        {
            return new EffectTagRequirements
            {
                ApplicationRequirements = new AvoidRequireTagGroup(),
                OngoingRequirements = new AvoidRequireTagGroup(),
                RemovalRequirements = new AvoidRequireTagGroup(),
            };
        }

        /// <summary>An item, with nothing granted yet: add its effects, its ability and its On Hits.</summary>
        public Item Item(string name)
        {
            var item = Own(ScriptableObject.CreateInstance<Item>());
            item.name = name;
            item.Definition.Name = name;
            item.Tags.AssetTag = NewTag($"Test.Item.{name.Replace(" ", "")}");
            return item;
        }

        /// <summary>One stage of an ability's tasks.</summary>
        public AbilityTaskBehaviourStage Stage(params AbstractAbilityTask[] tasks)
        {
            return new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>(tasks) };
        }

        /// <summary>An ability of these stages, with no tag requirements, cost or cooldown until the test gives them.</summary>
        public Ability Ability(string name, params AbilityTaskBehaviourStage[] stages)
        {
            var ability = Own(ScriptableObject.CreateInstance<Ability>());
            ability.name = name;
            ability.Definition = new AbilityDefinition { Name = name, Textures = new List<TextureItem>() };
            ability.Tags = new AbilityTags
            {
                AssetTag = NewTag($"Test.Ability.{name.Replace(" ", "")}"),
                ContextTags = new List<Tag>(),
                PassiveGrantedTags = new List<Tag>(),
                ActiveGrantedTags = new List<Tag>(),
                TagRequirements = new AbilityTagRequirements
                {
                    SourceRequirements = new AvoidRequireTagGroup { AvoidTags = new List<TagQuery>(), RequireTags = new List<TagQuery>() },
                    TargetRequirements = new AvoidRequireTagGroup { AvoidTags = new List<TagQuery>(), RequireTags = new List<TagQuery>() },
                },
            };
            ability.Behaviour = new AbilityBehaviour { Stages = new List<AbilityTaskBehaviourStage>(stages) };
            ability.WorkerGroup = new StandardWorkerGroup();
            return ability;
        }
    }
}
