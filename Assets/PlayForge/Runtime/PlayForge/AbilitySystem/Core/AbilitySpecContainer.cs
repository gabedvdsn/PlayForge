using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpecContainer : ITagSource
    {
        public AbilitySpec Spec;
        public readonly int Index;

        public bool IsActive => _activeHandles.Count > 0;
        public bool IsClaiming => _activeHandles.Any(h => !h.ClaimReleased && (h.IsTargeting || h.IsExecuting));

        /// <summary>True if an activation made under the policy is still running.</summary>
        public bool IsActiveUnder(EAbilityActivationPolicy policy)
        {
            foreach (var handle in _activeHandles)
            {
                if (handle.Data.Request.Policy == policy) return true;
            }

            return false;
        }

        public bool IsCooldown => Spec.Base.Cooldown is not null && Spec.Source.GetLongestDurationFor(Spec.Base.Cooldown.Tags.AssetTag).FoundDuration;

        /// <summary>
        /// True while a call to <see cref="ActivateAbility"/> runs, nested or not: as long as the activation runs before
        /// it first waits, which is all of it for an instant ability. Asked for again meanwhile, the ability starts next
        /// frame (TS-8).
        /// </summary>
        internal bool IsActivating => _activating > 0;

        private int _activating;

        private readonly List<AbilityActivationHandle> _activeHandles = new();

        /// <summary>
        /// The stage the ability's first active run is in, counted from 0, or -1 while none is running or started. The
        /// Debugger shows it as "Stage 2/3" (ED-4).
        /// </summary>
        public int ActiveStageIndex
        {
            get
            {
                foreach (var handle in _activeHandles)
                    if (handle.Data != null && handle.Data.StageIndex >= 0) return handle.Data.StageIndex;
                return -1;
            }
        }

        /// <summary>How many times this ability's behaviour has been compiled into a proxy for an activation. Diagnostics.</summary>
        public int CompiledProxies { get; private set; }

        // Compiled behaviours waiting for the next activation. Compiling is most of what an activation costs, so a
        // proxy is compiled once and comes back here when its activation is over (PF-012). An activation that finds
        // none idle, such as one starting while an earlier activation of this ability still runs, compiles another.
        private readonly List<AbilityProxy> _idleProxies = new();

        private Dictionary<int, ActiveRuntimes> activeRuntimes;
        
        private struct ActiveRuntimes
        {
            public ProcessRelay Relay;
            public CancellationTokenSource RuntimeCts;
            public CancellationTokenSource TargetingCts;
        }

        public AbilitySpecContainer(AbilitySpec spec, int abilityIndex)
        {
            Spec = spec;
            Index = abilityIndex;
        }

        private void AddActiveHandle(AbilityActivationHandle handle)
        {
            _activeHandles.Add(handle);
        }

        private void RemoveActiveHandle(AbilityActivationHandle handle)
        {
            _activeHandles.Remove(handle);
        }

        public bool ActivateAbility(AbilityDataPacket implicitData)
        {
            if (IsClaiming) return false;

            // Open from the claim on, as its callback can ask for the ability too
            _activating++;
            try
            {
                if (!implicitData.System.ClaimActive(this, implicitData)) return false;

                string abilityName = $"{Spec.Base?.GetName() ?? "Anonymous"}";
                var handle = new AbilityActivationHandle(this, implicitData, TakeProxy(abilityName));
                AddActiveHandle(handle);

                AwaitAbility(handle).Forget();

                return true;
            }
            finally
            {
                _activating--;
            }
        }

        private async UniTaskVoid AwaitAbility(AbilityActivationHandle handle)
        {
            bool targetingCancelled = false;
            // How it ends, which its components hear (FN-14)
            var how = EAbilityEnd.Done;
            try
            {
                handle.IsTargeting = true;
                await handle.Proxy.ActivateTargetingTask(handle.TargetingCts.Token, handle.Data);
            }
            catch (OperationCanceledException)
            {
                targetingCancelled = true;
                how = EAbilityEnd.Cancelled;
            }
            catch (Exception e)
            {
                // Anything else went past targeting without the handle being cleaned, and the activation held its
                // place for good (TS-V)
                Debug.LogError($"[Ability] {Spec.Base.GetName()} failed in targeting: {e}");
                targetingCancelled = true;
                how = EAbilityEnd.Failed;
            }
            finally
            {
                handle.IsTargeting = false;

                // Targeting leaves a packet, and what reads a target asks for the entity it points at (GAS-46)
                handle.Data.ResolveTarget(Spec.Base.Behaviour.ImplicitTag);

                if (handle.Data.TryGetFirstTarget(out var target) && !Spec.ValidateAllActivationRequirements(target, handle.Data))
                {
                    targetingCancelled = true;
                    if (how == EAbilityEnd.Done) how = EAbilityEnd.Cancelled;
                }

                if (targetingCancelled)
                {
                    CleanHandle(handle, how);
                }
            }

            if (targetingCancelled) return;

            try
            {
                handle.IsExecuting = true;

                // Its cost and cooldown, as it starts, after its targeting, where no stage marked Apply Usage Effects takes
                // them: an author who sets a cost expects it paid, and they applied only from such a stage (TW-17)
                if (!Spec.Base.StageTakesUsage && !handle.Data.UsageEffectsApplied)
                {
                    handle.Data.UsageEffectsApplied = true;
                    Spec.ApplyUsageEffects(handle.Data);
                }

                Spec.Source.CompileGrantedTags();

                // Wire critical section callback before activation
                handle.Proxy.OnCriticalSectionExited = () => handle.ReleaseClaimIfNeeded();

                // Also allow nested sub-sequences (e.g. RunSequenceTask) to release the claim
                // when *their* critical section exits, without waiting for their outer stage to pop.
                // This preserves the early-claim-release behavior even when the outer ability
                // is effectively just a wrapper around a nested critical-containing sequence.
                handle.Data.NotifyCriticalSectionExit = () => handle.ReleaseClaimIfNeeded();

                // If no critical stages, release claim immediately
                if (!handle.Proxy.HasAnyCriticalStage)
                {
                    handle.ReleaseClaimIfNeeded();
                }

                await handle.Proxy.Activate(handle.Cts.Token, handle.Data);

                // An interrupt ends a run through its sequence, with nothing thrown: its token says it was cancelled (FN-14)
                if (handle.Cts != null && handle.Cts.IsCancellationRequested) how = EAbilityEnd.Cancelled;
            }
            catch (OperationCanceledException)
            {
                // Ability in execution is interrupted (cancelled)
                how = EAbilityEnd.Cancelled;
            }
            catch (Exception e)
            {
                // Anything else is a broken ability. Report it, rather than leave it looking like one that
                // did nothing (PF-008)
                Debug.LogError($"[Ability] {Spec.Base.GetName()} failed: {e}");
                how = EAbilityEnd.Failed;
            }
            finally
            {
                handle.IsExecuting = false;
                CleanHandle(handle, how);
                Spec.Source.CompileGrantedTags();
            }
        }

        public void Inject(ISequenceInjection injection)
        {
            if (!IsActive) return;

            // Take a snapshot since injection may modify the list
            var handles = _activeHandles.ToArray();
            foreach (var handle in handles) Inject(handle, injection);
        }

        private static void Inject(AbilityActivationHandle handle, ISequenceInjection injection)
        {
            handle.Proxy.Inject(injection, handle.Data);

            // For interrupt injections, cancel the appropriate tokens
            if (injection is InterruptSequenceInjection)
            {
                if (handle.IsTargeting) handle.TargetingCts?.Cancel();
                if (handle.IsExecuting) handle.Cts?.Cancel();
            }
        }

        // The stage a run is in, or null while it targets, before its first stage starts (FEAT-5)
        private AbilityTaskBehaviourStage StageOf(AbilityActivationHandle handle)
        {
            var stages = Spec.Base.Behaviour?.Stages;
            int index = handle.Data?.StageIndex ?? -1;
            return stages != null && index >= 0 && index < stages.Count ? stages[index] : null;
        }

        /// <summary>
        /// Whether a run of this ability is in a stage that keeps <paramref name="incoming"/> from activating: one whose
        /// Blocks names it, or, naming none, whose ability's Blocks does. Blocking was the claim alone (FEAT-5).
        /// </summary>
        internal bool Blocks(Ability incoming)
        {
            var behaviour = Spec.Base.Behaviour;
            if (behaviour == null) return false;
            for (int i = 0; i < _activeHandles.Count; i++)
            {
                var stage = StageOf(_activeHandles[i]);
                if (AbilityTags.Names(stage != null ? stage.BlocksOr(behaviour.Blocks) : behaviour.Blocks, incoming)) return true;
            }
            return false;
        }

        /// <summary>
        /// Interrupts each run of this ability in a stage that <paramref name="incoming"/> cancels as it activates: one
        /// whose Cancelled By names it, or, naming none, whose ability's does. A critical stage is committed, so nothing
        /// cancels it (Decision 30). Nothing cancelled an ability but an interrupt by slot (FEAT-5).
        /// </summary>
        internal void CancelFor(Ability incoming)
        {
            var behaviour = Spec.Base.Behaviour;
            if (behaviour == null) return;

            // Backwards, as an interrupted run can end inside the interrupt and leave the list
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                if (i >= _activeHandles.Count) continue;
                var handle = _activeHandles[i];
                var stage = StageOf(handle);
                if (stage != null && stage.IsCritical) continue;
                if (!AbilityTags.Names(stage != null ? stage.CancelledByOr(behaviour.CancelledBy) : behaviour.CancelledBy, incoming)) continue;
                Inject(handle, InterruptSequenceInjection.Instance);
            }
        }

        /// <summary>
        /// Interrupts each run of this ability but one in a critical stage, which is committed (Decision 30), as a task that
        /// cancels abilities by tag does (FN-24).
        /// </summary>
        public void Cancel()
        {
            // Backwards, as an interrupted run can end inside the interrupt and leave the list
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                if (i >= _activeHandles.Count) continue;
                var handle = _activeHandles[i];
                var stage = StageOf(handle);
                if (stage != null && stage.IsCritical) continue;
                Inject(handle, InterruptSequenceInjection.Instance);
            }
        }

        internal void OnHandleClaimReleased(AbilityActivationHandle handle)
        {
            handle.Data.System.ReleaseClaim(this, handle.Data);
        }

        private void CleanHandle(AbilityActivationHandle handle, EAbilityEnd how)
        {
            handle.ReleaseClaimIfNeeded();
            handle.Proxy.Clean();
            _idleProxies.Add(handle.Proxy);
            handle.CleanTokens();
            RemoveActiveHandle(handle);
            handle.Data.System.EndActivation(this, handle.Data, how);
        }

        /// <summary>An idle compiled proxy if there is one, or a newly compiled one.</summary>
        private AbilityProxy TakeProxy(string abilityName)
        {
            for (int i = _idleProxies.Count - 1; i >= 0; i--)
            {
                var proxy = _idleProxies[i];
                _idleProxies.RemoveAt(i);

                // One still winding down from an interrupt is let go, rather than run two activations at once
                if (proxy.IsIdle) return proxy;
            }

            CompiledProxies++;
            return Spec.Base.Behaviour.GenerateProxy(abilityName);
        }

        public override string ToString()
        {
            return $"{Spec}";
        }
        public IEnumerable<Tag> GetGrantedTags()
        {
            foreach (var t in Spec.Base.Tags.PassiveGrantedTags) yield return t;
            if (!IsActive) yield break;
            foreach (var t in Spec.Base.Tags.ActiveGrantedTags) yield return t;
        }
    }
}
