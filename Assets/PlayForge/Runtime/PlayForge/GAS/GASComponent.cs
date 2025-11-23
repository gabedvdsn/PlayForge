using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class GASComponent : LazyMonoProcess, ISource, ITagHandler, IProxyTaskBehaviourCaller
    {
        public EntityIdentity Data;
        
        // Subsystems
        [HideInInspector] public AttributeSystemComponent AttributeSystem;
        [HideInInspector] public AbilitySystemComponent AbilitySystem;
        
        // Core
        private List<AbstractGameplayEffectShelfContainer> EffectShelf;
        private List<AbstractGameplayEffectShelfContainer> FinishedEffects;
        private bool needsCleaning;
        
        // Tags
        public TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;
        
        // Store (Coffer)
        // BuriedComponentCoffer, can be replaced with data packet?
        
        protected virtual void Awake()
        {
            AttributeSystem = GetComponent<AttributeSystemComponent>();
            AbilitySystem = GetComponent<AbilitySystemComponent>();
            
            EffectShelf = new List<AbstractGameplayEffectShelfContainer>();
            FinishedEffects = new List<AbstractGameplayEffectShelfContainer>();

            Relays = new Dictionary<int, ProcessRelay>();
        }

        public void Initialize(EntityIdentity data)
        {
            Data = data;
            
            TagCache = new TagCache(this);
            
            Data.Identity.Initialize(this);
            
            AttributeSystem.Initialize(this);
            AbilitySystem.Initialize(this);
        }
        
        #region Process Parameters
        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);

            // Attempt to find affiliation
            if (regData.TryGet(Tags.PAYLOAD_AFFILIATION, EProxyDataValueTarget.Primary, out List<Tag> affiliation))
            {
                Data.Identity.Affiliation = affiliation;
            }
        }

        // Process
        public override void WhenUpdate(ProcessRelay relay)
        {
            TickEffectShelf();
            
            if (needsCleaning) ClearFinishedEffects();
            
            TagCache.TickTagWorkers();
        }
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }
        
        // Handling
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (GASComponent)handler == this;
        }

        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return Relays.ContainsKey(relay.CacheIndex);
        }

        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            Relays[relay.CacheIndex] = relay;
        }

        public bool HandlerVoidProcess(int processIndex)
        {
            return Relays.Remove(processIndex);
        }

        #endregion
        
        #region Source Targeting Parameters
        
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
        public AbstractTransformPacket AsTransform()
        {
            return new DefaultTransformPacket(transform);
        }
        
        #endregion
        
        #region Effect Handling
        
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect)
        {
            return effect.Generate(origin, this);
        }

        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            if (spec is null) return false;
            
            if (!ValidateEffectApplicationRequirements(spec)) return false;
            
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

        public void RemoveGameplayEffect(GameplayEffect effect)
        {
            RemoveGameplayEffect(effect.Tags.AssetTag);
        }

        public void RemoveGameplayEffect(Tag identifier)
        {
            var toRemove = EffectShelf.Where(container => container.Spec.Base.Tags.AssetTag == identifier);
            foreach (AbstractGameplayEffectShelfContainer container in toRemove)
            {
                FinishedEffects.Add(container);
                needsCleaning = true;
            }
        }

        /// <summary>
        /// Applies a durational/infinite gameplay effect to the component
        /// </summary>
        /// <param name="spec"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void ApplyDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            if (!AttributeSystem.DefinesAttribute(spec.Base.ImpactSpecification.AttributeTarget)) return;

            if (TryHandleExistingDurationalGameplayEffect(spec)) return;

            AbstractGameplayEffectShelfContainer container;
            switch (spec.Base.ImpactSpecification.ReApplicationPolicy)
            {
                case EEffectReApplicationPolicy.Refresh:
                case EEffectReApplicationPolicy.Extend:
                case EEffectReApplicationPolicy.Append:
                    container = NonStackingGameplayEffectShelfContainer.Generate(spec, ValidateEffectOngoingRequirements(spec));
                    break;
                case EEffectReApplicationPolicy.Stack:
                case EEffectReApplicationPolicy.StackRefresh:
                case EEffectReApplicationPolicy.StackExtend:
                    container = GetStackingContainer(spec);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            EffectShelf.Add(container);
            TagCache.AddTags(container.Spec.Base.Tags.GrantedTags);
            
            container.RunEffectApplicationWorkers();
                
            if (spec.Base.DurationSpecification.TickOnApplication)
            {
                ApplyInstantGameplayEffect(container);
            }
        }

        AbstractGameplayEffectShelfContainer GetStackingContainer(GameplayEffectSpec spec)
        {
            return spec.Base.DurationSpecification.StackableType switch
            {
                EStackableType.Incremental => IncrementalStackableGameplayShelfContainer.Generate(spec, ValidateEffectOngoingRequirements(spec)),
                EStackableType.Partitioned => PartitionedStackableGameplayShelfContainer.Generate(spec, ValidateEffectOngoingRequirements(spec)),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /// <summary>
        /// Instantly applies the effects of an instant gameplay effect
        /// </summary>
        /// <param name="spec"></param>
        private void ApplyInstantGameplayEffect(GameplayEffectSpec spec)
        {
            if (!AttributeSystem.TryGetAttributeValue(spec.Base.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return;

            TagCache.AddTags(spec.Base.Tags.GrantedTags);
            spec.RunEffectApplicationWorkers();
            
            SourcedModifiedAttributeValue sourcedModifiedValue = spec.SourcedImpact(attributeValue);
            AttributeSystem.ModifyAttribute(spec.Base.ImpactSpecification.AttributeTarget, sourcedModifiedValue);
            
            spec.RunEffectRemovalWorkers();
            TagCache.RemoveTags(spec.Base.Tags.GrantedTags);
            
            HandleGameplayEffects();
        }
        
        /// <summary>
        /// Instantly applies the effects of a durational/infinite gameplay effect
        /// </summary>
        /// <param name="container"></param>
        private void ApplyInstantGameplayEffect(AbstractGameplayEffectShelfContainer container)
        {
            if (!AttributeSystem.TryGetAttributeValue(container.Spec.Base.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return;
            
            SourcedModifiedAttributeValue sourcedModifiedValue = container.Spec.SourcedImpact(container, attributeValue);
            
            AttributeSystem.ModifyAttribute(container.Spec.Base.ImpactSpecification.AttributeTarget, sourcedModifiedValue);
            
            container.RunEffectApplicationWorkers();
            
            foreach (var containedEffect in container.Spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnTick))
            {
                ApplyGameplayEffect(container.Spec.Origin, containedEffect);
            }
        }
        
        private bool TryHandleExistingDurationalGameplayEffect(GameplayEffectSpec spec)
        {
            if (!TryGetEffectContainer(spec.Base, out AbstractGameplayEffectShelfContainer container)) return false;
            
            switch (spec.Base.ImpactSpecification.ReApplicationPolicy)
            {
                case EEffectReApplicationPolicy.Refresh:
                    container.Refresh();
                    return true;
                case EEffectReApplicationPolicy.Extend:
                    container.Extend(spec.Base.DurationSpecification.GetTotalDuration(spec));
                    return true;
                case EEffectReApplicationPolicy.Append:
                    return false;
                case EEffectReApplicationPolicy.Stack:
                    container.Stack();
                    return true;
                case EEffectReApplicationPolicy.StackRefresh:
                    container.Refresh();
                    return true;
                case EEffectReApplicationPolicy.StackExtend:
                    container.Extend(spec.Base.DurationSpecification.GetTotalDuration(spec));
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleGameplayEffects()
        {
            // Validate removal and ongoing requirements
            foreach (AbstractGameplayEffectShelfContainer container in EffectShelf)
            {
                if (ValidateEffectRemovalRequirements(container.Spec))
                {
                    FinishedEffects.Add(container);
                    needsCleaning = true;
                }
                else
                {
                    container.Ongoing = ValidateEffectOngoingRequirements(container.Spec);
                }
            }
        }

        private void TickEffectShelf()
        {
            foreach (AbstractGameplayEffectShelfContainer container in EffectShelf)
            {
                EEffectDurationPolicy durationPolicy = container.Spec.Base.DurationSpecification.DurationPolicy;
                
                if (durationPolicy == EEffectDurationPolicy.Instant) continue;
                
                float deltaTime = Time.deltaTime;
                
                container.RunEffectTickWorkers();
                container.TickPeriodic(deltaTime, out int executeTicks);
                if (executeTicks > 0 && container.Ongoing)
                {
                    for (int _ = 0; _ < executeTicks; _++)
                    {
                        ApplyInstantGameplayEffect(container);
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
            //EffectShelf.RemoveAll(container => container.DurationRemaining <= 0 && container.Spec.Base.DurationSpecification.DurationPolicy != EEffectDurationPolicy.Infinite);
            foreach (AbstractGameplayEffectShelfContainer container in FinishedEffects)
            {
                container.OnRemove();
                EffectShelf.Remove(container);
                
                container.RunEffectRemovalWorkers();
                TagCache.RemoveTags(container.Spec.Base.Tags.GrantedTags);
                
                if (container.AttributeRetention() != Tags.RETENTION_IGNORE) AttributeSystem.RemoveAttributeDerivation(container);
            }
            
            FinishedEffects.Clear();
            needsCleaning = false;
            
            HandleGameplayEffects();
        }

        #endregion

        #region Effect Requirement Validation
        
        /// <summary>
        /// Should the spec be applied?
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        private bool ValidateEffectApplicationRequirements(GameplayEffectSpec spec)
        {
            return ForgeHelper.ValidateAffiliationPolicy(spec.Base.ImpactSpecification.AffiliationPolicy, Data.Identity.Affiliation, spec.Origin.GetAffiliation())
                && spec.Base.ValidateApplicationRequirements(spec);
        }
        
        /// <summary>
        /// Should the spec be ongoing?
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        private bool ValidateEffectOngoingRequirements(GameplayEffectSpec spec)
        {
            return spec.Base.ValidateOngoingRequirements(spec);
        }
        
        /// <summary>
        /// Should the spec be removed?
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        private bool ValidateEffectRemovalRequirements(GameplayEffectSpec spec)
        {
            return spec.Base.ValidateRemovalRequirements(spec);
        }
        
        #endregion
        
        #region Effect Helpers

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractGameplayEffectShelfContainer container)
        {
            foreach (AbstractGameplayEffectShelfContainer _container in EffectShelf.Where(_container => _container.Spec.Base == effect))
            {
                container = _container;
                return true;
            }

            container = null;
            return false;
        }

        public GameplayEffectDuration GetLongestDurationFor(Tag lookForTag)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in from container in EffectShelf from specTag in container.Spec.Base.Tags.GrantedTags.Where(specTag => specTag == lookForTag) select container)
            {
                if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                {
                    return new GameplayEffectDuration(float.MaxValue, float.MaxValue, true);
                }

                if (!(container.TotalDuration > longestDuration)) continue;
                longestDuration = container.TotalDuration;
                longestRemaining = container.DurationRemaining;
            }

            return new GameplayEffectDuration(longestDuration, longestRemaining, longestDuration >= 0f);
        }
        
        public GameplayEffectDuration GetLongestDurationFor(List<Tag> lookForTags)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (AbstractGameplayEffectShelfContainer container in EffectShelf)
            {
                foreach (Tag specTag in container.Spec.Base.Tags.GrantedTags)
                {
                    if (!lookForTags.Contains(specTag)) continue;
                    if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                    {
                        return new GameplayEffectDuration(float.MaxValue, float.MaxValue, true);
                    }

                    if (!(container.TotalDuration > longestDuration)) continue;
                    longestDuration = container.TotalDuration;
                    longestRemaining = container.DurationRemaining;
                }
            }

            return new GameplayEffectDuration(longestDuration, longestRemaining, longestDuration >= 0f);
        }
        
        #endregion

        public override string ToString()
        {
            return $"{Data.Identity}";
        }

        public override async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token)
        {
            if (cmd == DisjointProxyTaskBehaviour.Command)
            {
                if (!regData.TryGet(Tags.TARGETED_INTENT, out DataValue<AbstractGameplayMonoProcess> data))
                {
                    return;
                }

                var arrData = data.ToArray();
                await CallBehaviour(cmd, cb, arrData, token);
            }

            await base.CallBehaviour(cmd, cb, token);
        }

        public override void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller)
        {
            
        }

        public override UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }

        #region Derivation Source
        public Tag[] GetContextTags() => new[] { Tags.CONTEXT_GAS, Tags.CONTEXT_SOURCE };
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => Data.Identity.NameTag;
        public int GetLevel() => Data.Identity.Level;
        public int GetMaxLevel() => Data.Identity.MaxLevel;
        public void SetLevel(int level) => Data.Identity.Level = level;
        public string GetName() => Data.Identity.DistinctName;
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }
        public List<Tag> GetAffiliation()
        {
            return Data.Identity.Affiliation;
        }
        public Tag[] GetAppliedTags()
        {
            return TagCache.GetAppliedTags();
        }

        public int GetWeight(Tag _tag)
        {
            return TagCache.GetWeight(_tag);
        }
        #endregion
    }
}
