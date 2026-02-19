using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, IProxyTaskBehaviourCaller
    {
        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();
        public AnalysisWorkerCache GetAnalysisCache();
        //public List<ItemSpecContainer> GetItemShelf();
        
        [CanBeNull] public GameplayAbilitySystem ToGASObject();

        public void RemoveGameplayEffect(Tag identifier);
        
        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container);
        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers);
    }

    public class GameSystemInstance : IGameplayAbilitySystem
    {
        public List<Tag> GetAppliedTags()
        {
            return new List<Tag>();
        }
        public int GetTagWeight(Tag _tag)
        {
            return 0;
        }
        public bool QueryTags(TagQuery query)
        {
            return false;
        }
        public void CompileGrantedTags()
        {
            
        }
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            throw new System.NotImplementedException();
        }
        public List<Tag> GetAffiliation()
        {
            throw new System.NotImplementedException();
        }
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            throw new System.NotImplementedException();
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
        public ActionQueue GetActionQueue()
        {
            throw new System.NotImplementedException();
        }
        public FrameSummary GetFrameSummary()
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
        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
        {
            throw new System.NotImplementedException();
        }
        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags)
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
        public AnalysisWorkerCache GetAnalysisCache()
        {
            throw new System.NotImplementedException();
        }
        public List<ItemSpecContainer> GetItemShelf()
        {
            throw new System.NotImplementedException();
        }
        public GameplayAbilitySystem ToGASObject()
        {
            throw new System.NotImplementedException();
        }
        public void RemoveGameplayEffect(Tag identifier)
        {
            throw new System.NotImplementedException();
        }
        public void MarkDead()
        {
            throw new System.NotImplementedException();
        }
        public bool IsDead { get; }
        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
        {
            throw new System.NotImplementedException();
        }
        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
        {
            throw new System.NotImplementedException();
        }
    }
}
