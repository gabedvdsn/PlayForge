using System.Collections.Generic;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Samples.Battles
{
    /// <summary>What an effect the battle builds holds. Each field is the plain default unless the battle needs otherwise.</summary>
    public sealed class EffectConfig
    {
        public EEffectDurationPolicy Policy = EEffectDurationPolicy.Infinite;
        public ForgeAttribute Target;
        public ECalculationOperation Op = ECalculationOperation.Add;
        public float Magnitude;

        public float Duration;
        public bool PeriodicTicks;
        public int Ticks;

        public EEffectReApplicationPolicy Reapply = EEffectReApplicationPolicy.AppendNewContainer;
        public EEffectInteractionPolicy Interaction = EEffectInteractionPolicy.DoNothing;

        public List<Tag> GrantedTags = new();
    }

    /// <summary>
    /// The battle's assets, built in code rather than saved in the project: its attributes and their sets, the effects
    /// and abilities its fighters use, and each fighter's identity. Each is made the way an asset's inspector fills it
    /// in, and goes when the battle does.
    /// </summary>
    public sealed class BattleAssets
    {
        private readonly List<ScriptableObject> made = new();
        private int unique;

        /// <summary>Destroys every asset this made.</summary>
        public void DestroyAll()
        {
            foreach (var asset in made)
                if (asset) Object.Destroy(asset);
            made.Clear();
        }

        private T Own<T>(T asset) where T : ScriptableObject
        {
            made.Add(asset);
            return asset;
        }

        /// <summary>A name made unique with a number, so each battle's tags are its own.</summary>
        public string Unique(string stem) => $"{stem}_{unique++}";

        public Tag NewTag(string stem) => Tag.Generate(Unique(stem));

        public ForgeAttribute Attribute(string name)
        {
            var attribute = Own(ScriptableObject.CreateInstance<ForgeAttribute>());
            attribute.name = attribute.Name = name;
            attribute.Textures = new List<TextureItem>();
            return attribute;
        }

        /// <summary>An attribute's start in a set: its current and base values, and how far past its base it may go.</summary>
        public AttributeSetElement Element(ForgeAttribute attribute, float current, float @base,
            EAttributeOverflowPolicy overflow = EAttributeOverflowPolicy.Unlimited)
        {
            return new AttributeSetElement
            {
                Attribute = attribute,
                Current = new AttributeMagnitudeSpec { Magnitude = current, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Base = new AttributeMagnitudeSpec { Magnitude = @base, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Overflow = new AttributeOverflowData { Policy = overflow },
                RetentionGroup = Tags.DEFAULT,
                Constraints = new AttributeConstraints { AutoClamp = true, RoundingMode = EAttributeRoundingPolicy.None },
            };
        }

        public AttributeSet Set(params AttributeSetElement[] elements)
        {
            var set = Own(ScriptableObject.CreateInstance<AttributeSet>());
            set.name = set.Name = Unique("Battle Set");
            set.AssetTag = NewTag("Battle.Set");
            set.Attributes = new List<AttributeSetElement>(elements);
            set.SubSets = new List<AttributeSet>();
            set.WorkerGroup = new StandardWorkerGroup();
            return set;
        }

        public EntityIdentity Identity(string name, AttributeSet set)
        {
            var entity = Own(ScriptableObject.CreateInstance<EntityIdentity>());
            entity.name = entity.Name = name;
            entity.Description = "";
            entity.Textures = new List<TextureItem>();
            entity.AssetTag = NewTag("Battle.Entity");
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

        public GameplayEffect Effect(string name, EffectConfig config)
        {
            var effect = Own(ScriptableObject.CreateInstance<GameplayEffect>());
            effect.name = name;
            effect.Definition = new GameplayEffectDefinition { Name = name, Textures = new List<TextureItem>() };
            effect.Tags = new GameplayEffectTags
            {
                AssetTag = NewTag($"Battle.Effect.{name.Replace(" ", "")}"),
                ContextTags = new List<Tag>(),
                GrantedTags = config.GrantedTags,
            };
            effect.Workers = new List<AbstractEffectWorker>();
            effect.SourceRequirements = NoRequirements();
            effect.TargetRequirements = NoRequirements();

            effect.ImpactSpecification = new GameplayEffectImpact
            {
                AttributeTarget = config.Target,
                TargetImpact = EAttributeTarget.Current,
                ImpactOperation = config.Op,
                AffiliationPolicy = EAffiliationPolicy.AlwaysAllow,
                ImpactTypes = new List<Tag>(),
                MagnitudeOperation = new ScalableFloat { Magnitude = config.Magnitude, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Packets = System.Array.Empty<ContainedEffectPacket>(),
            };

            effect.DurationSpecification = new GameplayEffectDuration
            {
                DurationPolicy = config.Policy,
                DurationOperation = new ScalableFloat { Magnitude = config.Duration, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                EnablePeriodicTicks = config.PeriodicTicks,
                TicksOperation = new ScalableInt { Magnitude = config.Ticks },
                TickIntervalOperation = new ScalableFloat { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                AdditionalExecuteTicksOperation = new ScalableInt { Magnitude = 0 },
                StackAmountOperation = new ScalableInt { Magnitude = 1 },
                StackingConfigs = new List<AbstractStackingConfig>(),
                ReApplicationPolicy = config.Reapply,
                ReApplicationInteraction = config.Interaction,
                StackPolicy = EEffectStackPolicy.StacksShareOneDuration,
                RealDeltaTime = EMagnitudeOperation.AddScaler,
            };
            return effect;
        }

        public AbilityTaskBehaviourStage Stage(params AbstractAbilityTask[] tasks) =>
            new() { Tasks = new List<AbstractAbilityTask>(tasks) };

        public Ability Ability(string name, params AbilityTaskBehaviourStage[] stages)
        {
            var ability = Own(ScriptableObject.CreateInstance<Ability>());
            ability.name = name;
            ability.Definition = new AbilityDefinition { Name = name, Textures = new List<TextureItem>() };
            ability.Tags = new AbilityTags
            {
                AssetTag = NewTag($"Battle.Ability.{name.Replace(" ", "")}"),
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

        // Groups with no queries: an application passes, and nothing removes the effect early
        private static EffectTagRequirements NoRequirements() => new()
        {
            ApplicationRequirements = new AvoidRequireTagGroup(),
            OngoingRequirements = new AvoidRequireTagGroup(),
            RemovalRequirements = new AvoidRequireTagGroup(),
        };
    }

    /// <summary>
    /// Where the battle's entities and shots live: each fighter an entity in the scene, registered with ProcessControl,
    /// and each shot's template an object outside any scene, as a prefab is, which ProcessControl clones. When the battle
    /// ends, everything it made goes.
    /// </summary>
    public sealed class BattleWorld
    {
        private readonly BattleAssets make;
        private readonly List<ProcessRelay> owned = new();
        private readonly List<GameObject> objects = new();
        private GameObject templates;

        public BattleWorld(BattleAssets make) => this.make = make;

        /// <summary>A stand-in for a prefab: a process on an object that's in no scene and not active, which ProcessControl clones.</summary>
        public T Template<T>(string name) where T : AbstractMonoProcess
        {
            if (!templates)
            {
                templates = new GameObject("Battle Templates") { hideFlags = HideFlags.HideAndDontSave };
                templates.SetActive(false);
                objects.Add(templates);
            }

            var host = new GameObject(name);
            host.transform.SetParent(templates.transform, false);
            return host.AddComponent<T>();
        }

        /// <summary>An entity in the scene, a GameplayAbilitySystem on an object of its own, registered with ProcessControl.</summary>
        public GameplayAbilitySystem Entity(AttributeSet set, string name)
        {
            var host = new GameObject(name);
            objects.Add(host);
            var gas = GameplayAbilitySystem.AddToGameObject(host, make.Identity(name, set));
            gas.ProcessLifecycle = EProcessLifecycle.Synchronous;
            gas.ProcessTiming = EProcessStepTiming.UpdateAndLate;
            if (!ProcessControl.Register(gas).With(ProcessDataPacket.Default()).Run(out var relay))
            {
                ForgeLog.Error($"ProcessControl refused {name}");
                return gas;
            }
            owned.Add(relay);
            return gas;
        }

        /// <summary>Applies an effect from one entity to another.</summary>
        public bool Apply(IGameplayAbilitySystem source, IGameplayAbilitySystem target, GameplayEffect effect)
        {
            var origin = IEffectOrigin.GenerateSourceDerivation(source);
            return target.ApplyGameplayEffect(target.GenerateEffectSpec(origin, effect));
        }

        /// <summary>Keeps a process this didn't register, so it's ended with the battle.</summary>
        public void Own(ProcessRelay relay) => owned.Add(relay);

        /// <summary>Ends what the battle left running and takes away what it made.</summary>
        public void Dispose()
        {
            var control = ProcessControl.Instance;
            if (control)
            {
                foreach (var relay in owned)
                    if (control.IsRegistered(relay.CacheIndex)) control.TerminateImmediate(relay.CacheIndex, false);
            }
            owned.Clear();

            foreach (var thing in objects)
                if (thing) Object.Destroy(thing);
            objects.Clear();
            templates = null;
            make.DestroyAll();
        }
    }
}
