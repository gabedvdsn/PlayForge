using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runtime (non-MonoBehaviour) version of GameplayAbilitySystem.
    /// Instantiated at runtime as a plain class. Derives from LazyRuntimeProcess
    /// and implements the same IGameplayAbilitySystem interface as the MonoBehaviour version.
    /// 
    /// Usage:
    ///   var gas = new RuntimeGameplayAbilitySystem(entityData);
    ///   ProcessControl.Instance.Register(gas, dataPacket, out var relay);
    /// </summary>
    public partial class RuntimeGameplayAbilitySystem : LazyRuntimeProcess, IGameplayAbilitySystem
    {
        public RuntimeEntityIdentity EntityData;
        
        // Subsystems
        protected AttributeSystemComponent AttributeSystem;
        protected AbilitySystemComponent AbilitySystem;
        protected ItemSystemComponent ItemSystem;
        protected EffectSystemComponent EffectSystem;

        protected SystemLevelsComponent LevelSystem;
        
        // Tags
        protected TagCache TagCache;
        
        // Transform (runtime entities have no Transform component)
        private AbstractTargetingPacket _targetingPacket;

        protected LocalDataStructure localData;

        // This entity's sides: the registration data's, or else a copy of its identity's. They were written onto the
        // identity, which every entity of this kind shares, so one turncoat moved them all (GAS-47)
        private List<Tag> affiliation;
        
        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.). Created with the
        /// GAS and kept by initialization (PF-013). A runtime GAS initializes inside its constructor, so
        /// <c>OnSystemInitialized</c> has fired before anyone can subscribe to it.
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; } = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="targetingPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            EntityIdentity data,
            AbstractTargetingPacket targetingPacket = null)
            : base(
                data?.Name ?? "RuntimeGAS",
                EProcessStepPriorityMethod.First,
                0,
                // Update ticks effects and tag workers; LateUpdate runs the end of frame (GAS-1)
                EProcessStepTiming.UpdateAndLate,
                EProcessLifecycle.SelfTerminating)
        {
            _targetingPacket = targetingPacket 
                               ?? new StaticTargetingPacket(Vector3.zero, Quaternion.identity, Vector3.one);
            // The packet points back at its entity, as the Mono GAS's does, so what targeting finds can be resolved
            // to it (GAS-46)
            _targetingPacket.target ??= this;
            
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            ItemSystem = new ItemSystemComponent(this);
            EffectSystem = new EffectSystemComponent(this);

            LevelSystem = new SystemLevelsComponent();

            Initialize(new RuntimeEntityIdentity(data));
        }
        
        
        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="targetingPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            RuntimeEntityIdentity data,
            AbstractTargetingPacket targetingPacket = null)
            : base(
                data?.Name ?? "RuntimeGAS",
                EProcessStepPriorityMethod.First,
                0,
                // Update ticks effects and tag workers; LateUpdate runs the end of frame (GAS-1)
                EProcessStepTiming.UpdateAndLate,
                EProcessLifecycle.SelfTerminating)
        {
            _targetingPacket = targetingPacket 
                               ?? new StaticTargetingPacket(Vector3.zero, Quaternion.identity, Vector3.one);
            // The packet points back at its entity, as the Mono GAS's does, so what targeting finds can be resolved
            // to it (GAS-46)
            _targetingPacket.target ??= this;
            
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            ItemSystem = new ItemSystemComponent(this);
            EffectSystem = new EffectSystemComponent(this);

            LevelSystem = new SystemLevelsComponent();

            Initialize(data);
        }
        
        /// <summary>
        /// Creates a runtime GAS entity with explicit position/rotation/scale.
        /// </summary>
        public RuntimeGameplayAbilitySystem(
            EntityIdentity data,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
            : this(data, new StaticTargetingPacket(position, rotation, scale))
        {
        }

        protected RuntimeGameplayAbilitySystem()
        {
            
        }

        private void Initialize(RuntimeEntityIdentity data)
        {
            EntityData = data;
            
            InitLocalData(EntityData);
            
            if (EntityData is null) return;
            
            TagCache = new TagCache(this);
            
            AbilitySystem.Setup(EntityData.ActivationPolicy, EntityData.AllowDuplicateAbilities, EntityData.MaxAbilitiesOperation);
            AttributeSystem.Setup(EntityData.AttributeSet);
            ItemSystem.Setup(EntityData.AllowDuplicateItems, EntityData.AllowDuplicateEquippedItems, EntityData.MaxItemsOperation, EntityData.MaxEquippedItemsOperation);
            
            InitializeEndOfFrameSystem();
            SetupDeferredContexts();
            CollectInitialWorkers();

            LevelSystem.Register(GetAssetTag(), new IntValuePairClamped(EntityData.GetStartingLevel(), EntityData.GetMaxLevel()));
            
            AbilitySystem.Initialize(EntityData.StartingAbilities);
            AttributeSystem.Initialize();
            ItemSystem.Initialize(EntityData.StartingItems);

            CompileGrantedTags();

            // Once attributes, items and tags exist, so the rules can check them (GAS-49)
            AbilitySystem.ActivateImmediateAbilities();
            
            Callbacks?.SystemInitialized();
        }

        private void CollectInitialWorkers()
        {
            EntityData.InitWorkers(this);
            if (EntityData.AttributeSet) EntityData.AttributeSet.InitWorkers(this);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PROCESS PARAMETERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override void WhenInitialize()
        {
            base.WhenInitialize();

            // The registration data's sides, or else the identity's, kept on this entity: two entities can share one
            // runtime identity (GAS-47)
            if (regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> registered)) affiliation = registered;
            else if (EntityData.Affiliation != null) affiliation = new List<Tag>(EntityData.Affiliation);
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SOURCE / TARGET PARAMETERS
        // ═══════════════════════════════════════════════════════════════════════════
        
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
        
        /// <summary>
        /// Returns the placeholder transform packet for this runtime entity.
        /// Unlike the MonoBehaviour version, this is not backed by a Unity Transform: the entity's position is this
        /// packet's, which whoever drives the entity keeps current. Targeting and range checks read it (GAS-38).
        /// </summary>
        public AbstractTargetingPacket GetTargetingPacket()
        {
            return _targetingPacket;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT HANDLING
        // ═══════════════════════════════════════════════════════════════════════════

        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect)
        {
            return effect.Generate(origin, this);
        }
        
        // Effect lifecycle lives in EffectSystemComponent — shared verbatim with the
        // MonoBehaviour GameplayAbilitySystem twin. These wrappers preserve the public
        // API surface.

        public bool ApplyGameplayEffect(GameplayEffectSpec spec) => EffectSystem.ApplyGameplayEffect(spec);

        public bool RemoveGameplayEffect(GameplayEffect effect) => EffectSystem.RemoveGameplayEffect(effect);

        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin) => EffectSystem.RemoveGameplayEffect(effect, origin);

        public bool RemoveGameplayEffect(Tag identifier) => EffectSystem.RemoveGameplayEffect(identifier);

        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT HELPERS — delegated to EffectSystemComponent
        // ═══════════════════════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════════════════════
        // TOSTRING
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            return $"{EntityData.GetName()}";
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG HANDLER / SOURCE INTERFACE
        // ═══════════════════════════════════════════════════════════════════════════

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
        public IntValuePairClamped GetLevel() => LevelSystem.GetLevel(GetAssetTag());
        public float GetRelativeLevel()
        {
            return ForgeHelper.RelativeOffsetValue(GetLevel());
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

            // The identity's own, as RuntimeEntityIdentity.GetGrantedTags yields them
            TagCache.AddTag(EntityData.AssetTag);
            GrantedTagCompiler.AddAll(TagCache, EntityData.GrantedTags);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // IGAMEPLAYABILITYSYSTEM
        // ═══════════════════════════════════════════════════════════════════════════
        
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

        /// <summary>
        /// Runtime GAS does not derive from GameplayAbilitySystem (MonoBehaviour).
        /// Returns null per interface contract (nullable return type).
        /// </summary>
        public GameplayAbilitySystem ToGASObject()
        {
            return null;
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

    public class RuntimeEntityIdentity : IHasReadableDefinition, ITagSource, ILocalDataSource, IWorkerGroupSource
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures;
        
        public List<Tag> Affiliation;
        
        public Tag AssetTag;
        public List<Tag> GrantedTags;
        public List<Tag> ContextTags;

        public int StartingLevel = 1;
        public bool CapAtMaxLevel = true;
        public int MaxLevel = 99;
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
        
        // Defaults as EntityIdentity's: unset, these were null, and giving an ability or an item threw (GAS-23)
        public ScalerIntegerMagnitudeOperation MaxAbilitiesOperation = new() { Magnitude = 99 };
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        public ScalerIntegerMagnitudeOperation MaxItemsOperation = new() { Magnitude = 99 };
        public ScalerIntegerMagnitudeOperation MaxEquippedItemsOperation = new() { Magnitude = 99 };
        public List<StartingItemContainer> StartingItems = new();
        public bool AllowDuplicateItems;
        public bool AllowDuplicateEquippedItems;
        
        public AttributeSet AttributeSet;

        public StandardWorkerGroup WorkerGroup;

        public RuntimeEntityIdentity()
        {
        }

        public RuntimeEntityIdentity(EntityIdentity data)
        {
            Name = data.Name;
            Description = data.Description;
            Textures = data.Textures;
            Affiliation = data.Affiliation;
            AssetTag = data.AssetTag;
            GrantedTags = data.GrantedTags;
            ContextTags = data.ContextTags;
            StartingLevel = data.StartingLevel;
            CapAtMaxLevel = data.CapAtMaxLevel;
            MaxLevel = data.MaxLevel;
            ActivationPolicy = data.ActivationPolicy;
            MaxAbilitiesOperation = data.MaxAbilitiesOperation;
            StartingAbilities = data.StartingAbilities;
            AllowDuplicateAbilities = data.AllowDuplicateAbilities;
            MaxItemsOperation = data.MaxItemsOperation;
            MaxEquippedItemsOperation = data.MaxEquippedItemsOperation;
            StartingItems = data.StartingItems;
            AllowDuplicateItems = data.AllowDuplicateItems;
            AllowDuplicateEquippedItems = data.AllowDuplicateEquippedItems;
            AttributeSet = data.AttributeSet;
            WorkerGroup = data.WorkerGroup;
        }
        
        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }
        public int GetMaxLevel()
        {
            return MaxLevel;
        }
        public int GetStartingLevel()
        {
            return StartingLevel;
        }
        public string GetProviderName()
        {
            return GetName();
        }
        public Tag GetProviderTag()
        {
            return AssetTag;
        }

        public IEnumerable<Tag> GetGrantedTags()
        {
            yield return AssetTag;
            foreach (var tag in GrantedTags) yield return tag;
        }
        public List<DataWrapper> GetLocalData()
        {
            return new List<DataWrapper>();
        }
        public void InitWorkers(ISource system)
        {
            WorkerGroup?.ProvideWorkersTo(system);
        }
        public void RemoveWorkers(ISource system)
        {
            WorkerGroup?.RemoveWorkersFrom(system);
        }
    }
}
