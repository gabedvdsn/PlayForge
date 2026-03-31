using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages abilities for an entity.
    /// Integrates with the worker system for impact processing.
    /// </summary>
    public class AbilitySystemComponent : DeferredContextSystem
    {
        private EAbilityActivationPolicy activationPolicy;
        public EAbilityActivationPolicy DefaultActivationPolicy => activationPolicy;
        private bool allowDuplicateAbilities;
        private ScalerIntegerMagnitudeOperation maxAbilitiesOperation;

        public readonly IGameplayAbilitySystem Self;

        private Dictionary<int, AbilitySpecContainer> AbilityCache;
        private Dictionary<EAbilityActivationPolicy, HashSet<int>> ActiveCache;

        protected ImpactWorkerCache ImpactWorkerCache;

        public AbilitySystemCallbacks Callbacks = new();
        
        public AbilitySystemComponent(IGameplayAbilitySystem self)
        {
            Self = self;
        }

        public bool IsExecuting => ActiveCache.Keys.Any(IsExecutingPolicy);
        
        public bool IsExecutingPolicy(EAbilityActivationPolicy policy) 
            => ActiveCache.ContainsKey(policy) && ActiveCache[policy].Count > 0;
        
        public bool IsExecutingCritical => IsExecuting && ActiveCache.Keys.Any(IsExecutingPolicyCritical);
        
        public bool IsExecutingPolicyCritical(EAbilityActivationPolicy policy)
        {
            return IsExecutingPolicy(policy) && ActiveCache[policy].Any(IsCritical);
        }

        public bool IsCritical(int index) 
            => AbilityCache[index].Spec.Base.Behaviour.Stages.Any(
                stage => stage.Tasks.Any(task => task.IsCriticalSection));

        public int AbilityCount => AbilityCache.Count;
        
        private Queue<AbilityActivationRequest> activationQueue = new();

        public AbilityActivationRequest CreateActivationRequest(int index, EAbilityActivationPolicyExtended? policy = null)
        {
            return new AbilityActivationRequest(index, this, policy);
        }
        
        public struct AbilityActivationRequest
        {
            public int Index;
            public EAbilityActivationPolicyExtended? Policy;
            public AbilitySystemComponent System;
            
            public AbilityActivationRequest(int index, AbilitySystemComponent asc, EAbilityActivationPolicyExtended? policy = null)
            {
                Index = index;
                System = asc;
                Policy = policy;
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

        #region Initialization
        
        public void Setup(
            EAbilityActivationPolicy activationPolicy, 
            bool allowDuplicates,
            ScalerIntegerMagnitudeOperation maxAbilitiesOperation)
        {
            this.activationPolicy = activationPolicy;
            this.allowDuplicateAbilities = allowDuplicates;
            this.maxAbilitiesOperation = maxAbilitiesOperation;
            
            ImpactWorkerCache = new ImpactWorkerCache();
        }
        
        /// <summary>
        /// Set the deferred execution context.
        /// Must be called after PreInitialize.
        /// </summary>
        public override void SetDeferredContext(IGameplayAbilitySystem self, ActionQueue actionQueue, FrameSummary frameSummary)
        {
            base.SetDeferredContext(self, actionQueue, frameSummary);
            ImpactWorkerCache?.SetDeferredContext(actionQueue, frameSummary);
        }
        
        public void Initialize(List<Ability> startingAbilities)
        {
            Enabled = true;
            Locked = false;
            
            AbilityCache = new Dictionary<int, AbilitySpecContainer>();
            ActiveCache = new()
            {
                { EAbilityActivationPolicy.AlwaysActivate, new() },
                { EAbilityActivationPolicy.ActivateIfIdle, new() },
                { EAbilityActivationPolicy.QueueActivationIfBusy, new() }
            };
            
            foreach (var ability in startingAbilities ?? new List<Ability>())
            {
                var result = GiveAbility(ability, ability.StartingLevel, out _);
                // Debug.Log($"[{Root.GetName()}] Ability {ability.GetName()} given ({result}) at level {ability.StartingLevel}");
            }

            foreach (var idx in AbilityCache)
            {
                InitializeNewAbility(idx.Key, idx.Value.Spec);
            }
        }
        
        #endregion

        #region Ability Managing
        
        public bool HasAbility(Ability ability)
        {
            return AbilityCache.Values.Any(c => c.Spec.Base == ability);
        }

        public List<AbilitySpecContainer> GetAbilityContainers() => AbilityCache.Values.ToList();
        
        public bool TryGetAbilityContainer(int index, out AbilitySpecContainer container)
        {
            return AbilityCache.TryGetValue(index, out container);
        }
        
        public bool TryGetAbilityContainer(Ability ability, out AbilitySpecContainer container)
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

            if (!CanGiveAbility(ability)) return false;
            
            abilityIndex = GetFirstAvailableCacheIndex();
            if (abilityIndex < 0) return false;

            var container = new AbilitySpecContainer(ability.Generate(Self, level), abilityIndex);
            AbilityCache[abilityIndex] = container;
            
            ability.WorkerGroup?.ProvideWorkersTo(Self);
            
            Self.CompileGrantedTags();

            return true;
        }

        public bool RemoveAbility(Ability ability)
        {
            if (!TryGetCacheIndexOf(ability, out int index)) return false;
            
            if (AbilityCache[index].IsClaiming) 
                Inject(index, new InterruptInjection());
            
            Self.CompileGrantedTags();

            return AbilityCache.Remove(index);
        }

        public bool CanGiveAbility(Ability ability)
        {
            if (!Enabled) return false;
            if (!allowDuplicateAbilities && HasAbility(ability)) return false;
            if (AbilityCount >= maxAbilitiesOperation.Evaluate(IAttributeImpactDerivation.GenerateLevelerDerivation(Self, Self.GetLevel(), Self.GetMaxLevel()))) return false;

            return true;
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
            
            TryActivateAbility(CreateActivationRequest(abilityIndex));
        }

        #endregion

        #region Ability Handling

        public bool CanActivateAbility(int index)
        {
            return Enabled 
                   && !Locked
                   && AbilityCache.TryGetValue(index, out var container)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.GetLevel() > 0); 
        }
        
        private bool CanActivateAbility(AbilitySpecContainer container, AbilityDataPacket data)
        {
            return Enabled 
                   && !Locked
                   && container.Spec.ValidateSourceActivationRequirements(data)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.GetLevel() > 0);
        }
        
        public bool TryActivateAbility(AbilityActivationRequest req)
        {
            if (!AbilityCache.TryGetValue(req.Index, out var container)) return false;
            var data = AbilityDataPacket.GenerateFrom(container.Spec, req, container.Spec.Base.Behaviour.UseImplicitTargeting);
            return CanActivateAbility(container, data) && ProcessActivationRequest(data);
        }
        
        private bool ProcessActivationRequest(AbilityDataPacket data)
        {
            var policy = data.Request.Policy?.Translate(this) 
                         ?? AbilityCache[data.Request.Index].Spec.Base.Definition.ActivationPolicy.Translate(this);
            
            return policy switch
            {
                EAbilityActivationPolicy.AlwaysActivate => 
                    AlwaysActivateTargetingValidation(data.Request.Index) && 
                    ActivateAbility(AbilityCache[data.Request.Index], data),
                    
                EAbilityActivationPolicy.ActivateIfIdle => 
                    !IsExecutingPolicyCritical(EAbilityActivationPolicy.ActivateIfIdle) && 
                    ActivateAbility(AbilityCache[data.Request.Index], data),
                    
                EAbilityActivationPolicy.QueueActivationIfBusy => 
                    !IsExecutingPolicyCritical(EAbilityActivationPolicy.QueueActivationIfBusy) 
                        ? ActivateAbility(AbilityCache[data.Request.Index], data) 
                        : QueueAbilityActivation(data.Request),
                        
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private bool AlwaysActivateTargetingValidation(int abilityIndex)
        {
            if (!IsExecutingPolicyCritical(EAbilityActivationPolicy.AlwaysActivate)) return true;
            return !IsCritical(abilityIndex);
        }

        private bool ActivateAbility(AbilitySpecContainer container, AbilityDataPacket data)
        {
            return container.ActivateAbility(data);
        }

        private bool QueueAbilityActivation(AbilityActivationRequest req)
        {
            activationQueue.Enqueue(req);
            return true;
        }

        private void ClearAbilityCache()
        {
            if (AbilityCache == null) return;

            foreach (var policy in ActiveCache.Keys)
            {
                foreach (int index in ActiveCache[policy]) 
                    AbilityCache[index].Inject(new InterruptInjection());
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
                if (!AbilityCache[index].IsClaiming) 
                    ReleaseClaim(AbilityCache[index], null);
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

        public bool ClaimActive(AbilitySpecContainer container, AbilityDataPacket data)
        {
            if (!TryGetCacheIndexOf(container.Spec.Base, out int index)) return false;
            
            TimeUtility.Start(container.Spec.Base.Tags.AssetTag);
            Callbacks.AbilityActivated(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            
            ActiveCache[AbilityCache[index].Spec.Base.Definition.ActivationPolicy.Translate(this)].Add(index);
            
            return true;
        }

        public void ReleaseClaim(AbilitySpecContainer container, AbilityDataPacket data)
        {
            if (!TryGetCacheIndexOf(container.Spec.Base, out int index)) return;

            var policy = data?.Request.Policy?.Translate(this) 
                         ?? container.Spec.Base.Definition.ActivationPolicy.Translate(this);
            ActiveCache[policy].Remove(index);

            TimeUtility.End(container.Spec.Base.Tags.AssetTag, out _);

            if (data is null) return;
            
            Callbacks.AbilityEnded(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            
            if (policy == EAbilityActivationPolicy.QueueActivationIfBusy && activationQueue.Count > 0) 
                TryActivateAbility(activationQueue.Dequeue());
        }

        #endregion

        #region Impact Workers

        /// <summary>
        /// Process impact data through workers and callbacks.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void ProvideFrameImpactDealt(ImpactData impactData, bool runWorkers = true)
        {
            // Track impact on derivation
            impactData.SourcedModifier.Derivation.TrackImpact(impactData);
            
            // Run effect-specific workers
            var effectContext = new EffectWorkerContext(
                Self, impactData.SourcedModifier.Derivation, 
                Self.GetFrameSummary(), Self.GetActionQueue(), 
                1, impactData);
            impactData.SourcedModifier.Derivation.RunWorkerImpact(effectContext);
            
            // Run global impact workers
            if (runWorkers)
            {
                ImpactWorkerCache.RunImpactData(impactData);
            }
        }
        
        /// <summary>
        /// Register an impact worker.
        /// </summary>
        public void ProvideWorker(AbstractImpactWorker worker)
        {
            ImpactWorkerCache?.ProvideWorker(worker);
        }
        
        /// <summary>
        /// Unregister an impact worker.
        /// </summary>
        public void RemoveWorker(AbstractImpactWorker worker)
        {
            ImpactWorkerCache?.RemoveWorker(worker);
        }

        #endregion
    }

    public enum EAbilityActivationPolicy
    {
        /// <summary>
        /// Do not restrict activation attempts to this ability
        /// </summary>
        AlwaysActivate,
        
        /// <summary>
        /// Activate this ability only if ability system is idle
        /// </summary>
        ActivateIfIdle, 
        
        /// <summary>
        /// If ability system is busy, queue activation attempt
        /// </summary>
        QueueActivationIfBusy
    }

    public enum EAbilityActivationPolicyExtended
    {
        /// <summary>
        /// Use whatever the local source policy is
        /// </summary>
        UseLocalPolicy,
        
        /// <summary>
        /// Do not restrict activation attempts to this ability
        /// </summary>
        AlwaysActivate,
        
        /// <summary>
        /// Activate this ability only if ability system is idle
        /// </summary>
        ActivateIfIdle,
        
        /// <summary>
        /// If ability system is busy, queue activation attempt
        /// </summary>
        QueueActivationIfBusy
    }
}