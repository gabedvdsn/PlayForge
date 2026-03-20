using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        public RuntimeEntityIdentity Data;
        
        // Subsystems
        protected AttributeSystemComponent AttributeSystem;
        protected AbilitySystemComponent AbilitySystem;
        protected ItemSystemComponent ItemSystem;

        protected SystemLevelsComponent LevelSystem;
        
        // Core
        private List<AbstractEffectContainer> EffectShelf;
        private List<AbstractEffectContainer> FinishedEffects;
        private bool needsCleaning;
        
        // Tags
        protected TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;
        
        // Transform (runtime entities have no Transform component)
        private AbstractTransformPacket _transformPacket;
        
        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.)
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="transformPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            EntityIdentity data,
            AbstractTransformPacket transformPacket = null)
            : base(
                data?.Name ?? "RuntimeGAS",
                EProcessStepPriorityMethod.First,
                0,
                EProcessStepTiming.Update,
                EProcessLifecycle.SelfTerminating)
        {
            _transformPacket = transformPacket 
                               ?? new PlaceholderTransformPacket(Vector3.zero, Quaternion.identity, Vector3.one);
            
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            ItemSystem = new ItemSystemComponent(this);
            
            LevelSystem = new SystemLevelsComponent();
            
            EffectShelf = new List<AbstractEffectContainer>();
            FinishedEffects = new List<AbstractEffectContainer>();

            Relays = new Dictionary<int, ProcessRelay>();
            
            Initialize(new RuntimeEntityIdentity(data));
        }
        
        
        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="transformPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            RuntimeEntityIdentity data,
            AbstractTransformPacket transformPacket = null)
            : base(
                data?.Name ?? "RuntimeGAS",
                EProcessStepPriorityMethod.First,
                0,
                EProcessStepTiming.Update,
                EProcessLifecycle.SelfTerminating)
        {
            _transformPacket = transformPacket 
                               ?? new PlaceholderTransformPacket(Vector3.zero, Quaternion.identity, Vector3.one);
            
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            ItemSystem = new ItemSystemComponent(this);
            
            LevelSystem = new SystemLevelsComponent();
            
            EffectShelf = new List<AbstractEffectContainer>();
            FinishedEffects = new List<AbstractEffectContainer>();

            Relays = new Dictionary<int, ProcessRelay>();
            
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
            : this(data, new PlaceholderTransformPacket(position, rotation, scale))
        {
        }

        protected RuntimeGameplayAbilitySystem()
        {
            
        }

        private void Initialize(RuntimeEntityIdentity data)
        {
            Data = data;

            if (Data is null) return;
            
            TagCache = new TagCache(this);
            
            AbilitySystem.Setup(Data.ActivationPolicy, Data.AllowDuplicateAbilities, Data.MaxAbilitiesOperation);
            AttributeSystem.Setup(Data.AttributeSet);
            ItemSystem.Setup(Data.AllowDuplicateItems, Data.AllowDuplicateEquippedItems, Data.MaxItemsOperation, Data.MaxEquippedItemsOperation);
            
            InitializeEndOfFrameSystem();
            SetupDeferredContexts();
            CollectInitialWorkers();

            LevelSystem.Register(GetAssetTag(), new AttributeValueClamped(Data.GetStartingLevel(), Data.GetMaxLevel()));
            
            AbilitySystem.Initialize(Data.StartingAbilities);
            AttributeSystem.Initialize();
            ItemSystem.Initialize(Data.StartingItems);
            
            CompileGrantedTags();
            
            Callbacks?.SystemInitialized();
        }

        private void CollectInitialWorkers()
        {
            Data.WorkerGroup?.ProvideWorkersTo(this);
            Data.AttributeSet?.WorkerGroup?.ProvideWorkersTo(this);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PROCESS PARAMETERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);

            // Attempt to find affiliation from registration data
            if (regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> affiliation))
            {
                Data.Affiliation = affiliation;
            }
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            TickEffectShelf();
            
            if (needsCleaning) ClearFinishedEffects();
            
            TagCache.TickTagWorkers();
        }

        public override void WhenLateUpdate(ProcessRelay relay)
        {
            EndOfFrame();
        }

        // Handling
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (RuntimeGameplayAbilitySystem)handler == this;
        }

        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return Relays.ContainsKey(relay.CacheIndex);
        }

        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            Relays[relay.CacheIndex] = relay;
        }

        public bool HandlerVoidProcess(ProcessRelay relay)
        {
            return Relays.Remove(relay.CacheIndex);
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
            return AttributeSystem.TryGetAttributeValue(attribute, out value);
        }
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            AttributeSystem.ModifyAttribute(attribute, sourcedModifiedValue, runEvents);
            return true;
        }
        
        /// <summary>
        /// Returns the placeholder transform packet for this runtime entity.
        /// Unlike the MonoBehaviour version, this is not backed by a Unity Transform.
        /// </summary>
        public AbstractTransformPacket AsTransform()
        {
            return _transformPacket;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT HANDLING
        // ═══════════════════════════════════════════════════════════════════════════

        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect)
        {
            return effect.Generate(origin, this);
        }
        
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {            
            if (spec is null) return false;

            if (!ForgeHelper.ValidateEffectApplicationRequirements(spec, Data.Affiliation))
            {
                return false;
            }
            
            switch (spec.Base.DurationSpecification.DurationPolicy)
            {
                case EEffectDurationPolicy.Instant:
                    ApplyInstantGameplayEffect(spec);
                    break;
                case EEffectDurationPolicy.Infinite:
                case EEffectDurationPolicy.Durational:
                    ApplyDurationalGameplayEffect(spec);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            HandleGameplayEffects();
            
            Callbacks?.EffectApplied(spec);
            
            foreach (var containedEffect in spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnApply))
            {
                ApplyGameplayEffect(spec.Origin, containedEffect);
            }
            
            return true;
        }
        
        private bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            GameplayEffectSpec spec = GenerateEffectSpec(origin, GameplayEffect);
            return ApplyGameplayEffect(spec);
        }
        
        public bool RemoveGameplayEffect(GameplayEffect effect)
        {
            return RemoveGameplayEffect(effect.Tags.AssetTag);
        }
        
        public bool RemoveGameplayEffect(Tag identifier)
        {
            var toRemove = EffectShelf.Where(container => container.Spec.Base.Tags.AssetTag == identifier).ToArray();
            foreach (AbstractEffectContainer container in toRemove)
            {
                FinishedEffects.Add(container);
                needsCleaning = true;
            }

            return toRemove.Length > 0;
        }
        
        private void ApplyDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            if (!AttributeSystem.DefinesAttribute(spec.Base.ImpactSpecification.AttributeTarget)) return;

            bool ongoing = ForgeHelper.ValidateEffectOngoingRequirements(spec);
            if (TryHandleExistingDurationalGameplayEffect(spec, ongoing)) return;

            var container = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
            EffectShelf.Add(container);

            var effectContext = new EffectWorkerContext(this, container.Spec, _frameSummary, _actionQueue);
            container.Spec.RunWorkerApplication(effectContext);
            
            if (ongoing && spec.Base.DurationSpecification.TickOnApplication)
            {
                ApplyTickOnApplication(container);
            }
        }

        private void ApplyTickOnApplication(AbstractEffectContainer container)
        {
            for (int _ = 0; _ < container.Spec.Base.DurationSpecification.GetExecuteTicks(container.Spec, container.InstantExecuteTicks); _++)
            {
                ApplyDurationGameplayEffectAsInstant(container);
            }
        }
        
        private void ApplyInstantGameplayEffect(GameplayEffectSpec spec)
        {
            if (!AttributeSystem.TryGetAttributeValue(spec.Base.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return;

            TagCache.AddTags(spec.Base.Tags.GrantedTags);
            spec.RunWorkerApplication(new EffectWorkerContext(this, spec, _frameSummary, _actionQueue));
            
            var sourcedModifiedValue = spec.SourcedImpact(attributeValue);
            var impactData = AttributeSystem.ModifyAttribute(spec.Base.ImpactSpecification.AttributeTarget, sourcedModifiedValue);

            Debug.Log(impactData);
            
            spec.RunWorkerRemoval(new EffectWorkerContext(this, spec, _frameSummary, _actionQueue, 1, impactData));
            TagCache.RemoveTags(spec.Base.Tags.GrantedTags);
            
            HandleGameplayEffects();
        }
        
        private void ApplyDurationGameplayEffectAsInstant(AbstractEffectContainer container)
        {
            if (!AttributeSystem.TryGetAttributeValue(container.Spec.Base.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return;

            var sourcedModifiedValue = container.Spec.SourcedImpact(container, attributeValue);
            
            AttributeSystem.ModifyAttribute(container.Spec.Base.ImpactSpecification.AttributeTarget, sourcedModifiedValue);
            
            container.Spec.RunWorkerApplication(new EffectWorkerContext(this, container.Spec, _frameSummary, _actionQueue));
            
            foreach (var containedEffect in container.Spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnTick))
            {
                ApplyGameplayEffect(container.Spec.Origin, containedEffect);
            }
        }
        
        private bool TryHandleExistingDurationalGameplayEffect(GameplayEffectSpec spec, bool ongoing)
        {
            if (!TryGetEffectContainers(spec.Base, out var _containers)) return false;
            
            bool flag = true;
            switch (spec.Base.DurationSpecification.ReApplicationPolicy)
            {
                case EEffectReApplicationPolicy.DoNothing:
                    break;
                case EEffectReApplicationPolicy.ReplaceOldContainer:
                {
                    var newContainer = spec.Base.DurationSpecification.GenerateContainer(spec, ongoing);
                    foreach (var oldContainer in _containers) oldContainer.ReplaceValuesWith(newContainer);
                    break;
                }
                case EEffectReApplicationPolicy.AppendNewContainer:
                    flag = false;
                    break;
                case EEffectReApplicationPolicy.StackExistingContainers:
                {
                    var containers = _containers.Select(c => c as AbstractStackingEffectContainer).ToArray();
                    var container = spec.Base.DurationSpecification.FindStackingContainer(spec, ongoing, containers);
                    if (container is null)
                    {
                        flag = false;
                        break;
                    }
                    
                    int stacks = spec.Base.DurationSpecification.GetStackAmount(container);
                    container.Stack(stacks);

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            switch (spec.Base.DurationSpecification.ReApplicationInteraction)
            {
                case EEffectInteractionPolicy.DoNothing:
                    break;
                case EEffectInteractionPolicy.RefreshContainerDuration:
                    foreach (var c in _containers)
                    {
                        c.Refresh();
                        if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                    }
                    break;
                case EEffectInteractionPolicy.ExtendContainerDuration:
                    foreach (var c in _containers)
                    {
                        c.Extend(spec.Base.DurationSpecification.GetTotalDuration(spec));
                        if (c.Spec.Base.DurationSpecification.TickOnApplication) ApplyTickOnApplication(c);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (flag) CompileGrantedTags();
            
            return flag;
        }

        private void HandleGameplayEffects()
        {
            foreach (var container in EffectShelf)
            {
                if (ForgeHelper.ValidateEffectRemovalRequirements(container.Spec))
                {
                    FinishedEffects.Add(container);
                    needsCleaning = true;
                }
                else
                {
                    container.Ongoing = ForgeHelper.ValidateEffectOngoingRequirements(container.Spec);
                }
            }
            
            CompileGrantedTags();
        }

        private void TickEffectShelf()
        {
            foreach (var container in EffectShelf)
            {
                var durationPolicy = container.Spec.Base.DurationSpecification.DurationPolicy;
                if (durationPolicy == EEffectDurationPolicy.Instant) continue;
                
                float deltaTime = container.Spec.Base.DurationSpecification.GetDeltaTime(container.Spec);

                if (container.Spec.Base.DurationSpecification.EnablePeriodicTicks)
                {
                    container.TickPeriodic(deltaTime, out int executeTicks);

                    if (executeTicks > 0)
                    {
                        executeTicks = container.Spec.Base.DurationSpecification.GetExecuteTicks(
                            container.Spec, executeTicks
                        );
                    }

                    var effectContext = new EffectWorkerContext(
                        this, container.Spec, 
                        _frameSummary, _actionQueue, 
                        executeTicks);
                    container.Spec.RunWorkerTick(effectContext);
                
                    if (executeTicks > 0 && container.Ongoing)
                    {
                        Callbacks?.EffectTick(container.Spec, executeTicks);
                        
                        for (int _ = 0; _ < executeTicks; _++) ApplyDurationGameplayEffectAsInstant(container);
                    }
                }

                if (durationPolicy == EEffectDurationPolicy.Infinite) continue;
                
                container.UpdateTimeRemaining(deltaTime);

                if (container.DurationRemaining > 0) continue;
                
                FinishedEffects.Add(container);
                needsCleaning = true;
            }
        }
        
        private void ClearFinishedEffects()
        {
            foreach (var container in FinishedEffects)
            {
                container.OnRemove();
                EffectShelf.Remove(container);
                
                var effectContext = new EffectWorkerContext(
                    this, container.Spec, 
                    _frameSummary, _actionQueue);
                container.Spec.RunWorkerRemoval(effectContext);
                
                var nullify = !container.Spec.Base.ImpactSpecification.ReverseImpactOnRemoval && container.Spec.TrackedImpact.Total.BaseValue != 0f;
                var retainCurrent = container.Spec.TrackedImpact.Total.CurrentValue != 0f;
                AttributeSystem.RemoveAttributeDerivation(container.Spec, nullify, retainCurrent);
                
                Callbacks?.EffectRemoved(container.Spec.Base);
            }
            
            FinishedEffects.Clear();
            needsCleaning = false;
            
            HandleGameplayEffects();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
        {
            foreach (var _container in EffectShelf.Where(_container => _container.Spec.Base.Tags.AssetTag == effect.Tags.AssetTag))
            {
                container = _container;
                return true;
            }

            container = null;
            return false;
        }

        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
        {
            containers = EffectShelf.Where(c => c.Spec.Base.Tags.AssetTag == effect.Tags.AssetTag).ToArray();
            return containers.Any();
        }

        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                if (container.GetGrantedTags().All(specTag => specTag != lookForTag)) continue;
                if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                    return new EffectDurationRemaining(float.MaxValue, float.MaxValue, true);
                
                if (!(container.TotalDuration > longestDuration)) continue;
                longestDuration = container.TotalDuration;
                longestRemaining = container.DurationRemaining;
            }

            return new EffectDurationRemaining(longestDuration, longestRemaining, longestDuration >= 0f);
        }
        
        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                foreach (var specTag in container.Spec.Base.Tags.GrantedTags)
                {
                    if (!lookForTags.Contains(specTag)) continue;
                    if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                    {
                        return new EffectDurationRemaining(float.MaxValue, float.MaxValue, true);
                    }

                    if (!(container.TotalDuration > longestDuration)) continue;
                    longestDuration = container.TotalDuration;
                    longestRemaining = container.DurationRemaining;
                }
            }

            return new EffectDurationRemaining(longestDuration, longestRemaining, longestDuration >= 0f);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TOSTRING
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            return $"{Data.GetName()}";
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG HANDLER / SOURCE INTERFACE
        // ═══════════════════════════════════════════════════════════════════════════
        
        public List<Tag> GetContextTags() => Data.ContextTags;
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => Data.AssetTag;
        public int GetLevel() => Data.StartingLevel;
        public int GetMaxLevel() => Data.MaxLevel;
        public void SetLevel(int level) => Data.StartingLevel = Mathf.Clamp(level, 0, Data.MaxLevel);
        public string GetName() => Data.Name;
        
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }
        
        public List<Tag> GetAffiliation()
        {
            return Data.Affiliation;
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
            return query.Validate(TagCache.GetAppliedTags());
        }
        
        public void CompileGrantedTags()
        {
            TagCache.ResetWeights();

            if (Data.AttributeSet)
            {
                foreach (var t in Data.AttributeSet.GetGrantedTags()) TagCache.AddTag(t);
            }

            foreach (var effect in EffectShelf)
            {
                TagCache.AddTag(effect.Spec.Base.Tags.AssetTag);
                foreach (var t in effect.GetGrantedTags())
                {
                    TagCache.AddTag(t);
                }
            }

            foreach (var ability in AbilitySystem.GetAbilityContainers())
            {
                TagCache.AddTag(ability.Spec.Base.Tags.AssetTag);
                foreach (var t in ability.GetGrantedTags()) TagCache.AddTag(t);
            }

            foreach (var item in ItemSystem.GetItemContainers())
            {
                TagCache.AddTag(item.Spec.Base.Tags.AssetTag);
                foreach (var t in item.GetGrantedTags()) TagCache.AddTag(t);
            }

            foreach (var t in Data.GetGrantedTags()) TagCache.AddTag(t);
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

        /// <summary>
        /// Runtime GAS does not derive from GameplayAbilitySystem (MonoBehaviour).
        /// Returns null per interface contract (nullable return type).
        /// </summary>
        public GameplayAbilitySystem ToGASObject()
        {
            return null;
        }
    }

    public class RuntimeEntityIdentity : IHasReadableDefinition, ITagSource
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
        
        public ScalerIntegerMagnitudeOperation MaxAbilitiesOperation;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        public ScalerIntegerMagnitudeOperation MaxItemsOperation;
        public ScalerIntegerMagnitudeOperation MaxEquippedItemsOperation;
        public List<StartingItemContainer> StartingItems = new();
        public bool AllowDuplicateItems;
        public bool AllowDuplicateEquippedItems;
        
        [SerializeReference] public AttributeSet AttributeSet;

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
            AllowDuplicateAbilities = data.AllowDuplicateAbilities;
            MaxItemsOperation = data.MaxItemsOperation;
            MaxEquippedItemsOperation = data.MaxEquippedItemsOperation;
            AllowDuplicateItems = data.AllowDuplicateItems;
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
        public Texture2D GetPrimaryIcon()
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
    }
}
