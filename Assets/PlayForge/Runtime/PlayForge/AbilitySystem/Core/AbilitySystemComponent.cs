using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySystemComponent
    {
        private EAbilityActivationPolicy activationPolicy;
        public EAbilityActivationPolicy DefaultActivationPolicy => activationPolicy;
        private bool allowDuplicateAbilities;

        public readonly IGameplayAbilitySystem Root;

        private Dictionary<int, AbilitySpecContainer> AbilityCache = new();
        private Dictionary<EAbilityActivationPolicy, HashSet<int>> ActiveCache = new()
        {
            { EAbilityActivationPolicy.NoRestrictions, new() },
            { EAbilityActivationPolicy.SingleActive, new() },
            { EAbilityActivationPolicy.SingleActiveQueue, new() }
        };

        protected ImpactWorkerCache ImpactWorkerCache;

        public AbilitySystemCallbacks Callbacks = new();

        public AbilitySystemComponent(IGameplayAbilitySystem root)
        {
            Root = root;
        }

        public bool IsExecuting() => ActiveCache.Keys.Any(IsExecuting);
        public bool IsExecuting(EAbilityActivationPolicy policy) => ActiveCache[policy].Count > 0;
        
        public bool IsExecutingCritical()
        {
            return IsExecuting() && ActiveCache.Keys.Any(IsExecutingCritical);
        }
        public bool IsExecutingCritical(EAbilityActivationPolicy policy)
        {
            return IsExecuting(policy) && ActiveCache[policy].Any(IsCritical);
        }

        public bool IsCritical(int index) => AbilityCache[index].Spec.Base.Proxy.Stages.Any(stage => stage.Tasks.Any(task => task.IsCriticalSection));
        
        private Queue<AbilityActivationRequest> activationQueue = new();

        public AbilityActivationRequest CreateActivationRequest(int index, EAbilityActivationPolicyExtended policy = EAbilityActivationPolicyExtended.UseLocalPolicy)
        {
            return new AbilityActivationRequest(policy.Translate(this), index);
        }
        
        public struct AbilityActivationRequest
        {
            public EAbilityActivationPolicy Policy;
            public int Index;

            public AbilityActivationRequest(EAbilityActivationPolicy policy, int index)
            {
                Policy = policy;
                Index = index;
            }

            public AbilityActivationRequest(AbilitySpec ability, int index, AbilitySystemComponent asc = null)
            {
                Policy = ability.Base.Definition.ActivationPolicy.Translate(asc);
                Index = index;
            }
        }

        private bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value == _enabled) return;
                if (!value)
                {
                    activationQueue.Clear();
                    InjectAll(new InterruptInjection());
                }

                _enabled = value;
            }
        }

        private bool _locked;

        public bool Locked
        {
            get => _locked;
            set => _locked = value;
        }

        public void Initialize(EAbilityActivationPolicy activationPolicy, bool allowDuplicateAbilities, List<AbstractImpactWorker> impactWorkers,
            List<Ability> startingAbilities)
        {
            this.activationPolicy = activationPolicy;
            this.allowDuplicateAbilities = allowDuplicateAbilities;

            ImpactWorkerCache = new ImpactWorkerCache(impactWorkers);
            
            AbilityCache = new Dictionary<int, AbilitySpecContainer>();
            ActiveCache = new Dictionary<EAbilityActivationPolicy, HashSet<int>>();
            
            foreach (Ability ability in startingAbilities)
            {
                GiveAbility(ability, ability.StartingLevel, out _);
            }

            foreach (var idx in AbilityCache)
            {
                InitializeNewAbility(idx.Key, idx.Value.Spec);
            }

            Enabled = true;
            Locked = false;
        }
        
        public void SetAbilitiesLevel(int level)
        {
            foreach (var container in AbilityCache.Values)
            {
                container.Spec.SetLevel(Mathf.Min(level, container.Spec.Base.MaxLevel));
            }
        }

        #region Ability Managing
        
        public bool HasAbility(Ability ability)
        {
            return AbilityCache.Values.Any(c => c.Spec.Base == ability);
        }

        private bool TryGetAbilityContainer(Ability ability, out AbilitySpecContainer container)
        {
            foreach (var _container in AbilityCache.Values.Where(_container => _container.Spec.Base == ability))
            {
                container = _container;
                return true;
            }

            container = default;
            return false;
        }
        
        public bool GiveAbility(Ability ability, int level, out int abilityIndex)
        {
            abilityIndex = -1;
            
            if (!Enabled) return false;
            if (!allowDuplicateAbilities && HasAbility(ability)) return false;
            
            abilityIndex = GetFirstAvailableCacheIndex();
            if (abilityIndex < 0) return false;

            AbilitySpecContainer container = new AbilitySpecContainer(ability.Generate(Root, level));
            AbilityCache[abilityIndex] = container;

            HandleTags(ability.Tags.PassiveGrantedTags, true);

            return true;
        }

        public bool RemoveAbility(Ability ability)
        {
            if (!TryGetCacheIndexOf(ability, out int index)) return false;
            
            if (AbilityCache[index].IsClaiming) Inject(index, new InterruptInjection());
            
            HandleTags(ability.Tags.PassiveGrantedTags, false);

            return AbilityCache.Remove(index);
        }

        private bool TryGetCacheIndexOf(Ability ability, out int cacheIndex)
        {
            cacheIndex = -1;
            foreach (int index in AbilityCache.Keys.Where(index => AbilityCache[index].Spec.Base == ability))
            {
                cacheIndex = index;
                return true;
            }

            return false;
        }

        private int GetFirstAvailableCacheIndex()
        {
            for (int i = AbilityCache.Count; i >= 0; i--)
            {
                if (!AbilityCache.ContainsKey(i)) return i;
            }

            return -1;
        }

        private void InitializeNewAbility(int abilityIndex, AbilitySpec ability)
        {
            if (!ability.Base.Definition.ActivateImmediately) return;
            
            var req = new AbilityActivationRequest(ability, abilityIndex, this);
            TryActivateAbility(req);
        }

        private void HandleTags(IEnumerable<Tag> tags, bool flag)
        {
            if (flag) Root.GetTagCache().AddTags(tags);
            else Root.GetTagCache().RemoveTags(tags);
        }

        #endregion

        #region Ability Handling

        public bool CanActivateAbility(int index, AbilityDataPacket data)
        {
            return Enabled 
                   && !Locked
                   && AbilityCache.TryGetValue(index, out AbilitySpecContainer container)
                   && container.Spec.ValidateActivationRequirements(data)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.Level > 0);
        }

        public bool TryActivateAbility(AbilityActivationRequest req)
        {
            if (!AbilityCache.TryGetValue(req.Index, out var container)) return false;
            var data = AbilityDataPacket.GenerateFrom(container.Spec, container.Spec.Base.Proxy.UseImplicitTargeting);
            return CanActivateAbility(req.Index, data) && ProcessActivationRequest(req, data);
        }
        
        private bool ProcessActivationRequest(AbilityActivationRequest req, AbilityDataPacket data)
        {
            return req.Policy switch
            {
                EAbilityActivationPolicy.NoRestrictions => NoRestrictionsTargetingValidation(req.Index) && ActivateAbility(AbilityCache[req.Index], data),
                EAbilityActivationPolicy.SingleActive => ! IsExecutingCritical(EAbilityActivationPolicy.SingleActive) && ActivateAbility(AbilityCache[req.Index], data),
                EAbilityActivationPolicy.SingleActiveQueue => !IsExecutingCritical(EAbilityActivationPolicy.SingleActiveQueue) ? ActivateAbility(AbilityCache[req.Index], data) : QueueAbilityActivation(req.Index),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /// <summary>
        /// Ensures that the same targeting proxy task is not active at the same time when using NoRestrictions policy.
        /// </summary>
        /// <param name="abilityIndex">Index of the activated ability</param>
        /// <returns></returns>
        private bool NoRestrictionsTargetingValidation(int abilityIndex)
        {
            if (!IsExecutingCritical(EAbilityActivationPolicy.NoRestrictions)) return true;
            return !IsCritical(abilityIndex);
        }

        private bool ActivateAbility(AbilitySpecContainer container, AbilityDataPacket data)
        {
            container.Spec.ApplyUsageEffects();
            return container.ActivateAbility(data);
        }

        private bool QueueAbilityActivation(int abilityIndex)
        {
            activationQueue.Enqueue(new AbilityActivationRequest(AbilityCache[abilityIndex].Spec, abilityIndex));

            return true;
        }

        private void ClearAbilityCache()
        {
            if (AbilityCache is null) return;

            foreach (var policy in ActiveCache.Keys)
            {
                foreach (int index in ActiveCache[policy]) AbilityCache[index].Inject(new InterruptInjection());
                ActiveCache[policy].Clear();
            }

            AbilityCache.Clear();
        }

        public void Inject(int index, IAbilityInjection injection)
        {
            if (!AbilityCache.TryGetValue(index, out var container) || !container.IsClaiming) return;
            container.Inject(injection);
        }
        
        public void Inject(Ability ability, IAbilityInjection injection)
        {
            if (!TryGetAbilityContainer(ability, out var container) || !container.IsClaiming) return;
            container.Inject(injection);
        }
        
        public void Inject(EAbilityActivationPolicy policy, IAbilityInjection injection)
        {
            foreach (int index in ActiveCache[policy])
            {
                // Cleanup -- make sure non-active, claiming abilities are released (this should never occur anyway)
                if (!AbilityCache[index].IsClaiming) ReleaseClaim(AbilityCache[index], null);
                AbilityCache[index].Inject(injection);
            }
        }

        public void InjectAll(IAbilityInjection injection)
        {
            foreach (var policy in ActiveCache.Keys)
            {
                Inject(policy, injection);
            }
        }

        /// <summary>
        /// The ability container claims runtime over the ASC
        /// </summary>
        /// <param name="container">The claiming container</param>
        /// <param name="data"></param>
        /// <returns>Whether or not the ASC was successfully claimed</returns>
        public bool ClaimActive(AbilitySpecContainer container, AbilityDataPacket data)
        {
            if (!TryGetCacheIndexOf(container.Spec.Base, out var index)) return false;
            
            TimeUtility.Start(container.Spec.Base.Tags.AssetTag);
            Callbacks.AbilityActivated(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            
            ActiveCache[AbilityCache[index].Spec.Base.Definition.ActivationPolicy.Translate(this)].Add(index);
            
            return true;
        }

        public void ReleaseClaim(AbilitySpecContainer container, AbilityDataPacket data)
        {
            if (!TryGetCacheIndexOf(container.Spec.Base, out var index)) return;

            var policy = AbilityCache[index].Spec.Base.Definition.ActivationPolicy.Translate(this);
            ActiveCache[policy].Remove(index);

            TimeUtility.End(container.Spec.Base.Tags.AssetTag, out _);
            
            if (data is null) return;
            
            Callbacks.AbilityEnded(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            
            if (policy == EAbilityActivationPolicy.SingleActiveQueue && activationQueue.Count > 0) TryActivateAbility(activationQueue.Dequeue());
        }

        #endregion

        #region Impact Workers

        public void ProvideFrameImpactDealt(AbilityImpactData impactData)
        {
            impactData.SourcedModifier.BaseDerivation.TrackImpact(impactData);
            impactData.SourcedModifier.BaseDerivation.RunEffectImpactWorkers(impactData);
            
            ImpactWorkerCache.RunImpactData(impactData);
        }

        #endregion

        #region Native

        private void OnDestroy()
        {
            ClearAbilityCache();
        }

        #endregion
        
        private abstract class AbstractAbilityCacheLayer
        {
            public bool locked;

            public abstract AbilitySpecContainer[] GetActiveContainer();
            public abstract void SetActiveContainer(AbilitySpecContainer container);
        }

        private class SingleActiveAbilityCacheLayer : AbstractAbilityCacheLayer
        {
            private AbilitySpecContainer activeContainer;

            public override AbilitySpecContainer[] GetActiveContainer()
            {
                return new[] { activeContainer };
            }
            public override void SetActiveContainer(AbilitySpecContainer container)
            {
                activeContainer = container;
            }
        }

        private class ManyActiveAbilityCacheLayer : AbstractAbilityCacheLayer
        {
            private List<AbilitySpecContainer> activeContainers = new();
            
            public override AbilitySpecContainer[] GetActiveContainer()
            {
                return activeContainers.ToArray();
            }
            public override void SetActiveContainer(AbilitySpecContainer container)
            {
                activeContainers.Add(container);
            }
        }
    }

    public enum EAbilityActivationPolicy
    {
        NoRestrictions,  // Always able to activate any available ability
        SingleActive,  // Only able to activate one ability at a time
        SingleActiveQueue  // Only able to activate one ability at a time, but subsequent activations are queued (queue is cleared in the same moment that targeting tasks are cancelled)
    }

    public enum EAbilityActivationPolicyExtended
    {
        UseLocalPolicy,
        NoRestrictions,
        SingleActive,
        SingleActiveQueue
    }
}
