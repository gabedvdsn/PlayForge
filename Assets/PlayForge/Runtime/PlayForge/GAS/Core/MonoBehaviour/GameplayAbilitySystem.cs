using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public partial class GameplayAbilitySystem : LazyMonoProcess, IGameplayAbilitySystem
    {
        public EntityIdentity EntityData;
        
        // Subsystems
        public AttributeSystemComponent AttributeSystem;
        public AbilitySystemComponent AbilitySystem;
        public ItemSystemComponent ItemSystem;
        public EffectSystemComponent EffectSystem;

        public SystemLevelsComponent LevelSystem;
        
        // Tags
        protected TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;

        protected LocalDataStructure localData;

        // This entity's sides: the registration data's, or else a copy of its identity's. They were written onto the
        // identity, which every entity of this kind shares, so one turncoat moved them all (GAS-47)
        private List<Tag> affiliation;
        
        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.). Created with the
        /// component, so a caller can subscribe (to <c>OnSystemInitialized</c> above all) before the GAS is
        /// registered and initialized; initialization keeps this instance (PF-013).
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; } = new();
        
        private void CollectInitialWorkers()
        {
            EntityData.InitWorkers(this);
            if (EntityData.AttributeSet) EntityData.AttributeSet.InitWorkers(this);
        }
        
        #region Process Parameters
        public override void WhenInitialize()
        {
            base.WhenInitialize();
            
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            ItemSystem = new ItemSystemComponent(this);
            EffectSystem = new EffectSystemComponent(this);

            LevelSystem = new SystemLevelsComponent();

            Relays = new Dictionary<int, ProcessRelay>();
            TagCache = new TagCache(this);
            
            InitLocalData(EntityData);
            
            if (EntityData is null) return;

            // The registration data's sides, or else the asset's, kept on this entity (GAS-47)
            if (regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> registered)) affiliation = registered;
            else if (EntityData.Affiliation != null) affiliation = new List<Tag>(EntityData.Affiliation);
            
            AbilitySystem.Setup(EntityData.ActivationPolicy, EntityData.AllowDuplicateAbilities, EntityData.MaxAbilitiesOperation);
            AttributeSystem.Setup(EntityData.AttributeSet);
            ItemSystem.Setup(EntityData.AllowDuplicateItems, EntityData.AllowDuplicateEquippedItems, EntityData.MaxItemsOperation, EntityData.MaxEquippedItemsOperation);
            
            InitializeEndOfFrameSystem();
            SetupDeferredContexts();
            CollectInitialWorkers();

            LevelSystem.Register(
                GetAssetTag(),
                new IntValuePairClamped(EntityData.GetStartingLevel(), EntityData.GetMaxLevel())
            );
            
            AbilitySystem.Initialize(EntityData.StartingAbilities);
            AttributeSystem.Initialize();
            ItemSystem.Initialize(EntityData.StartingItems);
            
            CompileGrantedTags();
            
            Callbacks?.SystemInitialized();
        }

        public override void WhenUpdate()
        {
            EffectSystem.Tick();

            // Tag workers tick once a frame, at its end, once their activations are settled. Ticked here as well,
            // they ran at twice their rate (GAS-19)
        }

        public override void WhenLateUpdate()
        {
            EndOfFrame();
        }

        #endregion
        
        #region Source Targeting Parameters
        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem)
        {
            lvlSystem = LevelSystem;
            return LevelSystem is not null;
        }
        public bool FindItemSystem(out ItemSystemComponent itemSystem)
        {
            itemSystem = ItemSystem;
            return ItemSystem is not null;
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            attrSystem = AttributeSystem;
            return AttributeSystem is not null;
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            abilSystem = AbilitySystem;
            return AbilitySystem is not null;
        }
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value)
        {
            return AttributeSystem.TryGetValue(attribute, out value);
        }
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            AttributeSystem.ModifyAttribute(attribute, sourcedModifiedValue, runEvents);
            return true;
        }
        public AbstractTargetingPacket GetTargetingPacket()
        {
            return new TargetingPacket(transform, this);
        }
    
        #endregion
        
        #region Effect Handling

        /// <summary>
        /// Generates a gameplay effect spec with the calling GAS as the target.
        /// </summary>
        /// <param name="origin">The origination of the effect (typically entity, ability, or item)</param>
        /// <param name="effect">The effect to generate for</param>
        /// <returns></returns>
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect)
        {
            return effect.Generate(origin, this);
        }
        
        // Effect lifecycle lives in EffectSystemComponent — shared verbatim with the
        // RuntimeGameplayAbilitySystem twin. These wrappers preserve the public API surface.

        public bool ApplyGameplayEffect(GameplayEffectSpec spec) => EffectSystem.ApplyGameplayEffect(spec);

        /// <summary>
        /// Applies a gameplay effect with respect to an origin (typically entity, ability, or item).
        /// </summary>
        public bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            var spec = GenerateEffectSpec(origin, GameplayEffect);
            return ApplyGameplayEffect(spec);
        }

        public bool RemoveGameplayEffect(GameplayEffect effect) => EffectSystem.RemoveGameplayEffect(effect);

        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin) => EffectSystem.RemoveGameplayEffect(effect, origin);

        public bool RemoveGameplayEffect(Tag identifier) => EffectSystem.RemoveGameplayEffect(identifier);

        #endregion

        #region Effect Helpers
        // Delegated to EffectSystemComponent (shared with the runtime twin).

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
            => EffectSystem.TryGetEffectContainer(effect, out container);

        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
            => EffectSystem.TryGetEffectContainers(effect, out containers);

        public GameplayAbilitySystemCallbacks GetCallbacks()
        {
            return Callbacks;
        }

        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
            => EffectSystem.GetLongestDurationFor(lookForTag);

        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags)
            => EffectSystem.GetLongestDurationFor(lookForTags);
        #endregion

        public override string ToString()
        {
            if (EntityData is null) return "[GAS] No EntityData Assigned.";
            return $"{EntityData.GetName()}";
        }

        #region IAttributeDerivation
        public ISource GetSource()
        {
            return this;
        }
        public IHasReadableDefinition GetReadableDefinition()
        {
            return this;
        }
        public List<Tag> GetContextTags() => EntityData.ContextTags;
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => EntityData.AssetTag;
        public IntValuePairClamped GetLevel() => GetLevel(GetAssetTag());
        public IntValuePairClamped GetLevel(Tag key)
        {
            return LevelSystem.GetLevel(key);
        }
        public LevelCallbackStatus SetLevel(Tag key, IntValuePair level)
        {
            return LevelSystem.TrySetLevel(key, level);
        }
        public LevelCallbackStatus ModifyLevel(Tag key, IntValuePair delta)
        {
            return LevelSystem.TryModifyLevel(key, delta);
        }
        public bool IsActive()
        {
            return !IsDead;
        }
        public override string GetName() => EntityData.Name;
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            // Once: a process that runs again after a pause says it again (GAS-26)
            if (!regData.Contains(entity, Tags.TARGETED_INTENT)) regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }

        public void WithdrawTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.Remove(Tags.TARGETED_INTENT, entity);
        }
        public List<Tag> GetAffiliation()
        {
            return affiliation ?? EntityData?.Affiliation;
        }
        public List<Tag> GetAppliedTags()
        {
            return TagCache.GetAppliedTags();
        }
        public int GetTagWeight(Tag _tag)
        {
            return TagCache.GetWeight(_tag);
        }
        public bool QueryTags(TagQuery query)
        {
            // The cache, which has the weights. The list counted each tag once, however often it was granted (GAS-16)
            return query.Validate(TagCache);
        }
        public void CompileGrantedTags()
        {
            TagCache.ResetWeights();
            GrantedTagCompiler.Compile(TagCache, EntityData.AttributeSet, EffectSystem, AbilitySystem, ItemSystem);

            // The identity's own, as EntityIdentity.GetGrantedTags yields them
            TagCache.AddTag(EntityData.AssetTag);
            GrantedTagCompiler.AddAll(TagCache, GetAffiliation());
            GrantedTagCompiler.AddAll(TagCache, EntityData.GrantedTags);
        }
        #endregion
        
        #region IGameplaySystem
        
        public AttributeSystemComponent GetAttributeSystem()
        {
            return AttributeSystem;
        }
        public AbilitySystemComponent GetAbilitySystem()
        {
            return AbilitySystem;
        }
        public ItemSystemComponent GetItemSystem()
        {
            return ItemSystem;
        }
        public EffectSystemComponent GetEffectSystem()
        {
            return EffectSystem;
        }
        public GameplayAbilitySystem ToGASObject()
        {
            return this;
        }
        
        #endregion

        public static GameplayAbilitySystem AddToGameObject(GameObject obj, EntityIdentity entity)
        {
            var gas = obj.AddComponent<GameplayAbilitySystem>();
            gas.EntityData = entity;
            return gas;
        }
        public void InitLocalData(ILocalDataSource source)
        {
            localData ??= new LocalDataStructure();
            localData.Init(source);
        }
        public void SetLocalData(Tag key, DataWrapper data)
        {
            localData.Set(key, data);
        }
        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            return localData.TryGet(key, out data);
        }
        public LocalDataStructure GetLocalDataStructure()
        {
            return localData;
        }
    }
}
