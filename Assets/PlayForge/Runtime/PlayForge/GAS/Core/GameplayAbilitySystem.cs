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
        public EntityIdentity Data;
        
        // Subsystems
        private AttributeSystemComponent AttributeSystem;
        private AbilitySystemComponent AbilitySystem;
        
        // Core
        private List<AbstractEffectContainer> EffectShelf;
        private List<AbstractEffectContainer> FinishedEffects;
        private bool needsCleaning;
        
        // Tags
        protected TagCache TagCache;
        
        // Process
        protected Dictionary<int, ProcessRelay> Relays;
        
        protected virtual void Awake()
        {
            AttributeSystem = new AttributeSystemComponent(this);
            AbilitySystem = new AbilitySystemComponent(this);
            
            EffectShelf = new List<AbstractEffectContainer>();
            FinishedEffects = new List<AbstractEffectContainer>();

            Relays = new Dictionary<int, ProcessRelay>();
            
            Initialize(Data);
        }

        private void Initialize(EntityIdentity data)
        {
            Data = data;
            
            TagCache = new TagCache(this);
            
            AbilitySystem.Setup(Data.ActivationPolicy, Data.AllowDuplicateAbilities);
            AttributeSystem.Setup(Data.AttributeSet);
            
            InitializeEndOfFrameSystem();
            SetupDeferredContexts();
            CollectInitialWorkers();
            
            AbilitySystem.Initialize(Data.StartingAbilities);
            AttributeSystem.Initialize();
            
            CompileGrantedTags();
        }

        private void CollectInitialWorkers()
        {
            Data.WorkerGroup?.ProvideWorkersTo(this);
            Data.AttributeSet?.WorkerGroup?.ProvideWorkersTo(this);
        }
        
        #region Process Parameters
        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);

            // Attempt to find affiliation
            if (regData.TryGet(Tags.AFFILIATION, EProxyDataValueTarget.Primary, out List<Tag> affiliation))
            {
                Data.Affiliation = affiliation;
            }
        }

        // Process
        public override void WhenUpdate(ProcessRelay relay)
        {
            TickEffectShelf();
            
            if (needsCleaning) ClearFinishedEffects();
            
            TagCache.TickTagWorkers();

            if (Input.GetKeyDown(KeyCode.K))
            {
                foreach (var attr in AttributeSystem.GetAttributeCache())
                {
                    Debug.Log($"[Attr] {attr.Key.GetName()} ({attr.Value.Value})");
                    foreach (var derivation in attr.Value.DerivedValues)
                    {
                        Debug.Log($"\t\t{derivation.Key.GetEffectDerivation().GetName()} ({derivation.Key.AttributeRetention()}) -> {derivation.Value}");
                    }
                }
            }
        }

        public override void WhenLateUpdate(ProcessRelay relay)
        {
            EndOfFrame();
        }

        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }

        // Handling
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (GameplayAbilitySystem)handler == this;
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
        public bool TryGetAttributeValue(Attribute attribute, out AttributeValue value)
        {
            return AttributeSystem.TryGetAttributeValue(attribute, out value);
        }
        public bool TryModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            AttributeSystem.ModifyAttribute(attribute, sourcedModifiedValue, runEvents);
            return true;
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
        private bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect GameplayEffect)
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
            foreach (AbstractEffectContainer container in toRemove)
            {
                FinishedEffects.Add(container);
                needsCleaning = true;
            }
        }
        
        private void ApplyEffectTags(GameplayEffectSpec spec)
        {
            TagCache.AddTags(spec.Base.Tags.GrantedTags);
            TagCache.AddTag(spec.Base.Tags.AssetTag);
            TagCache.AddTag(spec.Origin.GetAssetTag());
        }

        private void RemoveEffectTags(GameplayEffectSpec spec)
        {
            TagCache.RemoveTags(spec.Base.Tags.GrantedTags);
            TagCache.RemoveTag(spec.Base.Tags.AssetTag);
            TagCache.RemoveTag(spec.Origin.GetAssetTag());
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

            var effectContext = new EffectWorkerContext(this, container, _frameSummary, _actionQueue);
            container.RunWorkerApplication(effectContext);
            
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
            
            container.RunWorkerApplication(new EffectWorkerContext(this, container, _frameSummary, _actionQueue));
            
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

                //if (!container.Spec.Base.DurationSpecification.EnablePeriodicTicks) continue;
                
                float deltaTime = container.Spec.Base.DurationSpecification.GetDeltaTime(container.Spec);

                if (container.Spec.Base.DurationSpecification.EnablePeriodicTicks)
                {
                    container.TickPeriodic(deltaTime, out int executeTicks);
                    
                    //Debug.Log($"\t\tA) Found execute ticks {executeTicks}");

                    if (executeTicks > 0)
                    {
                        executeTicks = container.Spec.Base.DurationSpecification.GetExecuteTicks(
                            container.Spec, executeTicks
                        );
                    }
                    
                    //Debug.Log($"\t\tB) Found execute ticks {executeTicks}");

                    var effectContext = new EffectWorkerContext(
                        this, container, 
                        _frameSummary, _actionQueue, 
                        executeTicks);
                    container.RunWorkerTick(effectContext);
                
                    if (executeTicks > 0 && container.Ongoing)
                    {
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
            //EffectShelf.RemoveAll(container => container.DurationRemaining <= 0 && container.Spec.Base.DurationSpecification.DurationPolicy != EEffectDurationPolicy.Infinite);
            foreach (var container in FinishedEffects)
            {
                container.OnRemove();
                EffectShelf.Remove(container);
                
                var effectContext = new EffectWorkerContext(
                    this, container, 
                    _frameSummary, _actionQueue);
                container.RunWorkerRemoval(effectContext);
                
                //RemoveEffectTags(container.Spec);
                
                if (container.AttributeRetention() != Tags.IGNORE) AttributeSystem.RemoveAttributeDerivation(container);
            }
            
            FinishedEffects.Clear();
            needsCleaning = false;
            
            HandleGameplayEffects();
        }

        #endregion
        
        #region Effect Helpers

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
        {
            //if (effect.)
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

        public GameplayEffectDuration GetLongestDurationFor(Tag lookForTag)
        {
            float longestDuration = float.MinValue;
            float longestRemaining = float.MinValue;
            foreach (var container in EffectShelf)
            {
                if (container.GetGrantedTags().All(specTag => specTag != lookForTag)) continue;
                if (container.Spec.Base.DurationSpecification.DurationPolicy == EEffectDurationPolicy.Infinite)
                    return new GameplayEffectDuration(float.MaxValue, float.MaxValue, true);
                
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
            foreach (var container in EffectShelf)
            {
                foreach (var specTag in container.Spec.Base.Tags.GrantedTags)
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
            return $"{Data.GetName()}";
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
        public override UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }

        #region Derivation Source
        public List<Tag> GetContextTags() => new(){ Tags.GAS, Tags.SOURCE };
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => Data.AssetTag;
        public int GetLevel() => Data.Level;
        public int GetMaxLevel() => Data.MaxLevel;
        public void SetLevel(int level) => Data.Level = Mathf.Clamp(level, 0, Data.MaxLevel);
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
        public int GetWeight(Tag _tag)
        {
            return TagCache.GetWeight(_tag);
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
                foreach (var t in effect.GetGrantedTags())
                {
                    //Debug.Log($"\t\tEffect tag ({effect.Spec.Base.GetName()}): {t}");
                    TagCache.AddTag(t);
                }
            }

            foreach (var ability in AbilitySystem.GetAbilityContainers())
            {
                foreach (var t in ability.GetGrantedTags()) TagCache.AddTag(t);
            }

            foreach (var t in Data.GetGrantedTags()) TagCache.AddTag(t);
        }
        #endregion
        public AttributeSystemComponent GetAttributeSystem()
        {
            return AttributeSystem;
        }
        public AbilitySystemComponent GetAbilitySystem()
        {
            return AbilitySystem;
        }
        public GameplayAbilitySystem ToGASObject()
        {
            return this;
        }
    }

    public class RootActionRequest
    {
        public readonly Action Action;
        public RootActionRequest(Action action)
        {
            Action = action;
        }
    }

    public class RootActionQueue
    {
        private class Node
        {
            public Node Next;
            public Node Prev;
            public RootActionRequest Data;
        }

        private Node Root;
        private Node End;
        
        public int Count { get; private set; }
        
        public void Enqueue(RootActionRequest request)
        {
            if (Root is null)
            {
                Root = new Node() { Next = null, Prev = null, Data = request };
                End = Root;
                
                Count = 1;
                return;
            }
            
            var node = new Node() { Next = End, Prev = null, Data = request };
            End = node;
            node.Next.Prev = node;
            
            Count += 1;
        }

        public RootActionRequest Dequeue()
        {
            if (Root is null) return null;
            
            var request = Root;
            Root = request.Prev;
            Count -= 1;

            return request.Data;
        }
        
    }
}
