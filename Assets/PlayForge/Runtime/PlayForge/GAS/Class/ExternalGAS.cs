
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ExternalGAS : LazyRuntimeProcess, IGameplayAbilitySystem
    {
        public EntityIdentity Data;
        
        // Subsystems
        private AttributeSystemComponent AttributeSystem;
        private AbilitySystemComponent AbilitySystem;
        
        // Core
        private List<AbstractGameplayEffectShelfContainer> EffectShelf;
        private List<AbstractGameplayEffectShelfContainer> FinishedEffects;
        private bool needsCleaning;
        
        // Tags
        protected TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;

        public List<Tag> GetAppliedTags()
        {
            return TagCache.GetAppliedTags();
        }
        public int GetWeight(Tag _tag)
        {
            return TagCache.GetWeight(_tag);
        }
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }
        public List<Tag> GetAffiliation()
        {
            return Data.Identity.Affiliation;
        }
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            if (spec is null) return false;
            if (!ForgeHelper.ValidateEffectApplicationRequirements(spec, Data.Identity.Affiliation)) return false;

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
                    container = NonStackingGameplayEffectShelfContainer.Generate(spec, ForgeHelper.ValidateEffectOngoingRequirements(spec));
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
                ApplyDurationGameplayEffectAsInstant(container);
            }
        }

        AbstractGameplayEffectShelfContainer GetStackingContainer(GameplayEffectSpec spec)
        {
            return spec.Base.DurationSpecification.StackableType switch
            {
                EStackableType.Incremental => IncrementalStackableGameplayShelfContainer.Generate(spec, ForgeHelper.ValidateEffectOngoingRequirements(spec)),
                EStackableType.Partitioned => PartitionedStackableGameplayShelfContainer.Generate(spec, ForgeHelper.ValidateEffectOngoingRequirements(spec)),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /// <summary>
        /// Instantly applies the effects of a durational/infinite gameplay effect
        /// </summary>
        /// <param name="container"></param>
        private void ApplyDurationGameplayEffectAsInstant(AbstractGameplayEffectShelfContainer container)
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
        }
        
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
        
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            return GameplayEffect.Generate(origin, this);
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            attrSystem = AttributeSystem;
            return true;
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            abilSystem = AbilitySystem;
            return true;
        }
        public bool TryGetAttributeValue(Attribute attribute, out AttributeValue value)
        {
            return AttributeSystem.TryGetAttributeValue(attribute, out value);
        }
        public bool TryModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            AttributeSystem.ModifyAttribute(attribute, sourcedModifiedValue, runEvents);
            return true;
        }
        public AbstractTransformPacket AsTransform() => new NullTransformPacket();
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler) => (ExternalGAS)handler == this;
        public bool HandlerProcessIsSubscribed(ProcessRelay relay) => Relays.ContainsKey(relay.CacheIndex);

        public void HandlerSubscribeProcess(ProcessRelay relay) => Relays[relay.CacheIndex] = relay;

        public bool HandlerVoidProcess(int processIndex) => Relays.Remove(processIndex);
        public List<Tag> GetContextTags() => new(){ Tags.CONTEXT_GAS, Tags.CONTEXT_SOURCE };
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => Data.Identity.NameTag;
        public int GetLevel() => Data.Identity.Level;
        public int GetMaxLevel() => Data.Identity.MaxLevel;
        public void SetLevel(int level) => Data.Identity.Level = Mathf.Clamp(level, 0, Data.Identity.MaxLevel);
        public string GetName() => Data.Identity.DistinctName;
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
        
        public override async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token)
        {
            if (cmd == DisjointProxyTaskBehaviour.Command)
            {
                if (!regData.TryGet(Tags.TARGETED_INTENT, out DataValue<IProxyTaskBehaviourUser> data))
                {
                    return;
                }

                await CallBehaviour(cmd, cb, data.ToArray(), token);
            }

            await base.CallBehaviour(cmd, cb, token);
        }
        public async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }
        public async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] users, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }
        public AttributeSystemComponent GetAttributeSystem()
        {
            throw new System.NotImplementedException();
        }
        public AbilitySystemComponent GetAbilitySystem()
        {
            throw new System.NotImplementedException();
        }
        public GameplayAbilitySystem ToGASObject()
        {
            throw new System.NotImplementedException();
        }
    }
}
