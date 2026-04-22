using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
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

        public SystemLevelsComponent LevelSystem;
        
        // Core
        private List<AbstractEffectContainer> EffectShelf;
        private List<AbstractEffectContainer> FinishedEffects;
        private bool needsCleaning;
        
        // Tags
        protected TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;

        protected LocalDataStructure localData;
        
        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.)
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; }
        
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
            
            LevelSystem = new SystemLevelsComponent();
            
            EffectShelf = new List<AbstractEffectContainer>();
            FinishedEffects = new List<AbstractEffectContainer>();

            Relays = new Dictionary<int, ProcessRelay>();
            TagCache = new TagCache(this);
            
            InitLocalData(EntityData);
            
            if (EntityData is null) return;

            // Attempt to find affiliation
            if (regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> affiliation))
            {
                EntityData.Affiliation = affiliation;
            }
            
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
            TickEffectShelf();
            
            if (needsCleaning) ClearFinishedEffects();
            
            TagCache.TickTagWorkers();
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
            return AttributeSystem.TryGetAttributeValue(attribute, out value);
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
        /// <param name="origin">The origination of the effect (typically ability or item)</param>
        /// <param name="effect">The effect to generate for</param>
        /// <returns></returns>
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect)
        {
            return effect.Generate(origin, this);
        }
        
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {            
            if (spec is null) return false;

            if (!ForgeHelper.ValidateEffectApplicationRequirements(spec, EntityData.Affiliation))
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
        
        /// <summary>
         /// Applies a gameplay effect to the container with respect to a derivation (effect) that is already applied
         /// </summary>
         /// <param name="origin"></param>
         /// <param name="GameplayEffect"></param>
         /// <returns></returns>
        public bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect GameplayEffect)
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
        
        /// <summary>
        /// Applies a durational/infinite gameplay effect to the component
        /// </summary>
        /// <param name="spec"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
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
        
        /// <summary>
        /// Instantly applies the effects of an instant gameplay effect
        /// </summary>
        /// <param name="spec"></param>
        private void ApplyInstantGameplayEffect(GameplayEffectSpec spec)
        {
            if (!AttributeSystem.TryGetAttributeValue(spec.Base.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return;

            TagCache.AddTags(spec.Base.Tags.GrantedTags);
            spec.RunWorkerApplication(new EffectWorkerContext(this, spec, _frameSummary, _actionQueue));
            
            var sourcedModifiedValue = spec.SourcedImpact(attributeValue);
            var impactData = AttributeSystem.ModifyAttribute(spec.Base.ImpactSpecification.AttributeTarget, sourcedModifiedValue);
            
            spec.RunWorkerRemoval(new EffectWorkerContext(this, spec, _frameSummary, _actionQueue, 1, impactData));
            TagCache.RemoveTags(spec.Base.Tags.GrantedTags);
            
            HandleGameplayEffects();
        }
        
        /// <summary>
        /// Instantly applies the effects of a durational/infinite gameplay effect
        /// </summary>
        /// <param name="container"></param>
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
            // Validate removal and ongoing requirements
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

                if (container.Spec.Base.ImpactSpecification.AttributeTarget)
                {
                    // Retain only if not reverse and if impact includes base
                    var nullify = !container.Spec.Base.ImpactSpecification.ReverseImpactOnRemoval && container.Spec.TrackedImpact.Total.BaseValue != 0f;
                    var retainCurrent = container.Spec.TrackedImpact.Total.CurrentValue != 0f;
                    AttributeSystem.RemoveAttributeDerivation(container.Spec, nullify, retainCurrent);
                }
                
                Callbacks?.EffectRemoved(container.Spec.Base);
            }
            
            FinishedEffects.Clear();
            needsCleaning = false;
            
            HandleGameplayEffects();
        }

        #endregion
        
        #region Effect Helpers
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
        public GameplayAbilitySystemCallbacks GetCallbacks()
        {
            return Callbacks;
        }
        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                if (container.Spec.Base.GetAssetTag() != lookForTag 
                    && container.GetGrantedTags().All(specTag => specTag != lookForTag)) continue;
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
                    if (lookForTags.Contains(container.Spec.Base.GetAssetTag()) && !lookForTags.Contains(specTag)) continue;
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
        #endregion

        public override string ToString()
        {
            if (EntityData is null) return "[GAS] No EntityData Assigned.";
            return $"{EntityData.GetName()}";
        }

        #region IAttributeDerivation
        public ISource GetOwner()
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
            regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }
        public List<Tag> GetAffiliation()
        {
            return EntityData.Affiliation;
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

            if (EntityData.AttributeSet)
            {
                foreach (var t in EntityData.AttributeSet.GetGrantedTags()) TagCache.AddTag(t);
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

            foreach (var t in EntityData.GetGrantedTags()) TagCache.AddTag(t);
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