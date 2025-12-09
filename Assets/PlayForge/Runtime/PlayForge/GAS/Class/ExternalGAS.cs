
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

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
        }
        public void RemoveGameplayEffect(GameplayEffect effect)
        {
            throw new System.NotImplementedException();
        }
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            throw new System.NotImplementedException();
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            throw new System.NotImplementedException();
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            throw new System.NotImplementedException();
        }
        public bool TryGetAttributeValue(Attribute attribute, out AttributeValue value)
        {
            throw new System.NotImplementedException();
        }
        public bool TryModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            throw new System.NotImplementedException();
        }
        public AbstractTransformPacket AsTransform()
        {
            throw new System.NotImplementedException();
        }
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            throw new System.NotImplementedException();
        }
        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            throw new System.NotImplementedException();
        }
        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            throw new System.NotImplementedException();
        }
        public bool HandlerVoidProcess(int processIndex)
        {
            throw new System.NotImplementedException();
        }
        public List<Tag> GetContextTags()
        {
            throw new System.NotImplementedException();
        }
        public TagCache GetTagCache()
        {
            throw new System.NotImplementedException();
        }
        public Tag GetAssetTag()
        {
            throw new System.NotImplementedException();
        }
        public int GetLevel()
        {
            throw new System.NotImplementedException();
        }
        public int GetMaxLevel()
        {
            throw new System.NotImplementedException();
        }
        public void SetLevel(int level)
        {
            throw new System.NotImplementedException();
        }
        public string GetName()
        {
            throw new System.NotImplementedException();
        }
        public GameplayEffectDuration GetLongestDurationFor(Tag lookForTag)
        {
            throw new System.NotImplementedException();
        }
        public GameplayEffectDuration GetLongestDurationFor(List<Tag> lookForTags)
        {
            throw new System.NotImplementedException();
        }
        public void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller)
        {
            throw new System.NotImplementedException();
        }
        public async UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            throw new System.NotImplementedException();
        }
        public async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token)
        {
            throw new System.NotImplementedException();
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
