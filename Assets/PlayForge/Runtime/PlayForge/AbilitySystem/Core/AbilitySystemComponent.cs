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

        /// <summary>True if any ability is currently executing (in ActiveCache).</summary>
        public bool IsExecuting => ActiveCache.Keys.Any(IsExecutingPolicy);

        /// <summary>True if any ability in this policy is currently executing (in ActiveCache).</summary>
        public bool IsExecutingPolicy(EAbilityActivationPolicy policy)
            => ActiveCache.ContainsKey(policy) && ActiveCache[policy].Count > 0;

        /// <summary>True if any ability is currently claiming (blocking reactivation).</summary>
        public bool IsClaiming => ActiveCache.Keys.Any(IsClaimingPolicy);

        /// <summary>True if any ability in this policy is currently claiming (blocking reactivation).</summary>
        public bool IsClaimingPolicy(EAbilityActivationPolicy policy)
            => ActiveCache.ContainsKey(policy) && ActiveCache[policy].Any(idx => AbilityCache[idx].IsClaiming);

        public bool IsExecutingCritical => IsExecuting && ActiveCache.Keys.Any(IsExecutingPolicyCritical);

        public bool IsExecutingPolicyCritical(EAbilityActivationPolicy policy)
        {
            return IsExecutingPolicy(policy) && ActiveCache[policy].Any(IsCritical);
        }

        /// <summary>True if any claiming ability in this policy is critical.</summary>
        public bool IsClaimingPolicyCritical(EAbilityActivationPolicy policy)
            => IsClaimingPolicy(policy) && ActiveCache[policy].Any(idx => AbilityCache[idx].IsClaiming && IsCritical(idx));

        public bool IsCritical(int index)
            => AbilityCache[index].Spec.Base.Behaviour.Stages.Any(
                stage => stage.Tasks.Any(task => task.IsCriticalSection));

        //public bool CriticalPolicyExecutionCheck()
        
        public int AbilityCount => AbilityCache.Count;
        
        private Queue<AbilityActivationRequest> activationQueue = new();

        public AbilityActivationRequest CreateActivationRequest(int index, ProcessDataPacket _data = null, EAbilityActivationPolicyExtended? policy = null)
        {
            var _policy = policy?.Translate(this) 
                         ?? (AbilityCache.TryGetValue(index, out var container) 
                             ? container.Spec.Base.Definition.ActivationPolicy.Translate(this) 
                             : DefaultActivationPolicy);
            
            return new AbilityActivationRequest(index, this, _policy, _data);
        }
        
        public struct AbilityActivationRequest
        {
            public int Index;
            public EAbilityActivationPolicy Policy;
            public ProcessDataPacket Data;
            public AbilitySystemComponent System;
            
            public AbilityActivationRequest(int index, AbilitySystemComponent asc, EAbilityActivationPolicy policy, ProcessDataPacket data)
            {
                Index = index;
                System = asc;
                Policy = policy;
                Data = data;
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
                    InjectAll(InterruptSequenceInjection.Instance);
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

        /// <summary>The granted abilities, without copying them into a list as <see cref="GetAbilityContainers"/> does.</summary>
        public Dictionary<int, AbilitySpecContainer>.ValueCollection Containers => AbilityCache.Values;
        
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

            ability.InitWorkers(Self);
            // ability.WorkerGroup?.ProvideWorkersTo(Self);
            
            Self.CompileGrantedTags();

            return true;
        }

        public bool RemoveAbility(Ability ability)
        {
            if (!TryGetCacheIndexOf(ability, out int index)) return false;

            if (AbilityCache[index].IsActive)
                Inject(index, InterruptSequenceInjection.Instance);

            ability.RemoveWorkers(Self);

            // Off the active list too, where the next claim check looked its slot up and threw, and gone before
            // the tags are compiled, which counted it still granted (GAS-20)
            foreach (var active in ActiveCache.Values) active.Remove(index);
            AbilityCache.Remove(index);

            Self.CompileGrantedTags();

            return true;
        }

        public bool CanGiveAbility(Ability ability)
        {
            if (ability == null) return false;
            if (!Enabled) return false;
            if (!allowDuplicateAbilities && HasAbility(ability)) return false;
            if (AbilityCount >= maxAbilitiesOperation.Evaluate(IAttributeImpactCarrier.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

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
            return TryGetAbilityContainer(index, out var container)
                   && CanActivateAbility(container, AbilityDataPacket.GenerateFrom(container, this));
            /*return Enabled 
                   && !Locked
                   && AbilityCache.TryGetValue(index, out var container)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.GetLevel() > 0); */
        }
        
        private bool CanActivateAbility(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // An ability runs as a process, so it can't start while ProcessControl refuses new ones (TS-AC)
            return Enabled
                   && !Locked
                   && ProcessControl.Instance is not { AcceptsNewProcesses: false }
                   && container.Spec.ValidateSourceActivationRequirements(data)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.GetLevel().CurrentValue > 0);
        }
        
        public bool TryActivateAbility(AbilityActivationRequest req)
        {
            if (!AbilityCache.TryGetValue(req.Index, out var container)) return false;
            var data = AbilityDataPacket.GenerateFrom(container.Spec, 
                req, container.Spec.Base.Behaviour.UseImplicitTargeting, container.Spec.Base.Behaviour.ImplicitTag);
            return CanActivateAbility(container, data) && ProcessActivationRequest(data);
        }
        
        private bool ProcessActivationRequest(AbilityDataPacket data)
        {
            return data.Request.Policy switch
            {
                EAbilityActivationPolicy.AlwaysActivate => 
                    AlwaysActivateTargetingValidation(data.Request.Index) && 
                    ActivateAbility(AbilityCache[data.Request.Index], data),
                    
                EAbilityActivationPolicy.ActivateIfIdle =>
                    !IsClaimingPolicy(EAbilityActivationPolicy.ActivateIfIdle) &&
                    ActivateAbility(AbilityCache[data.Request.Index], data),

                EAbilityActivationPolicy.QueueActivationIfBusy =>
                    !IsClaimingPolicy(EAbilityActivationPolicy.QueueActivationIfBusy)
                        ? ActivateAbility(AbilityCache[data.Request.Index], data)
                        : QueueAbilityActivation(data.Request),
                        
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private bool AlwaysActivateTargetingValidation(int abilityIndex)
        {
            if (!IsClaimingPolicyCritical(EAbilityActivationPolicy.AlwaysActivate)) return true;
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
                    AbilityCache[index].Inject(InterruptSequenceInjection.Instance);
                ActiveCache[policy].Clear();
            }

            AbilityCache.Clear();
        }

        public void Inject(int index, ISequenceInjection injection)
        {
            if (!AbilityCache.TryGetValue(index, out var container) || !container.IsActive) return;
            container.Inject(injection);
        }

        public void Inject(Ability ability, ISequenceInjection injection)
        {
            if (!TryGetAbilityContainer(ability, out var container) || !container.IsActive) return;
            container.Inject(injection);
        }
        
        public void Inject(EAbilityActivationPolicy policy, ISequenceInjection injection)
        {
            foreach (int index in ActiveCache[policy])
            {
                if (!AbilityCache[index].IsActive) continue;
                AbilityCache[index].Inject(injection);
            }
        }

        public void InjectAll(ISequenceInjection injection)
        {
            foreach (var policy in ActiveCache.Keys)
            {
                Inject(policy, injection);
            }
        }

        public bool ClaimActive(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // The container's own slot, under the policy the activation asked for. The claim went to the first slot
            // holding the same ability (GAS-21), and under the definition's policy, where EndActivation never
            // looked (TS-T)
            if (!AbilityCache.TryGetValue(container.Index, out var cached) || cached != container) return false;

            Callbacks.AbilityClaimed(AbilityCallbackStatus.GenerateForAbilityEvent(data));

            ActiveCache[data.Request.Policy].Add(container.Index);

            return true;
        }

        /// <summary>
        /// Called once the ability has completed all critical sections. Once released, the ability system will re-enable ability activation.
        /// The ability remains in ActiveCache (still executing) — only the reactivation gate opens.
        /// </summary>
        public void ReleaseClaim(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // A removed ability lets go too, or what's queued behind it waits on someone else (GAS-20)
            if (data != null)
            {
                Callbacks.AbilityClaimReleased(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            }

            // Dequeue waiting activations now that the claim is released
            if (activationQueue.Count > 0)
                TryActivateAbility(activationQueue.Dequeue());
        }

        /// <summary>
        /// Called when the ability has fully completed (all stages done, handle cleaned up).
        /// Removes the ability from ActiveCache.
        /// </summary>
        public void EndActivation(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // A removed ability's slot left the active list with it, and may hold another ability by now (GAS-20)
            if (AbilityCache.TryGetValue(container.Index, out var cached) && cached == container)
            {
                var policy = data?.Request.Policy
                             ?? container.Spec.Base.Definition.ActivationPolicy.Translate(this);

                // Overlapping activations share the slot, which stays while one under this policy still runs (TS-T)
                if (!container.IsActiveUnder(policy)) ActiveCache[policy].Remove(container.Index);
            }

            // After the active list is told, so that a listener finds the ability over (TS-9)
            if (data != null)
            {
                Callbacks.AbilityEnded(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            }
        }

        #endregion

        #region Impact Workers

        /// <summary>
        /// Process impact data dealt by this GAS through source-side workers, record it
        /// to the source's frame summary, and fire source-side GAS callbacks.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void ProvideFrameImpactDealt(ImpactData impactData, bool runWorkers = true)
        {
            // Track impact on derivation
            impactData.SourcedModifier.Carrier.TrackImpact(impactData);

            // Run effect-specific workers — only ICarrierApplication carriers have a
            // worker lifecycle. Synthetic delta carriers (SourceAttributeImpact, etc.)
            // skip this entirely.
            if (impactData.SourcedModifier.Carrier is ICarrierApplication appCarrier)
            {
                var effectContext = new EffectWorkerContext(
                    Self, impactData.SourcedModifier.Carrier,
                    Self.GetFrameSummary(), Self.GetActionQueue(),
                    1, impactData);
                appCarrier.RunWorkerImpact(effectContext);
            }

            // Run global impact workers (source-side)
            if (runWorkers)
            {
                ImpactWorkerCache.RunImpactData(impactData);
            }

            // Record DEALT impact on this (source) GAS — fires source-side callbacks
            Self.RecordFrameImpactDealt(impactData);
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