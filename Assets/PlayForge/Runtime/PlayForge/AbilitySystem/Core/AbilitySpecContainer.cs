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
        
        private readonly List<AbilityActivationHandle> _activeHandles = new();

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
            if (!implicitData.System.ClaimActive(this, implicitData)) return false;

            string abilityName = $"{Spec.Base?.GetName() ?? "Anonymous"}";
            var handle = new AbilityActivationHandle(this, implicitData, TakeProxy(abilityName));
            AddActiveHandle(handle);

            AwaitAbility(handle).Forget();

            return true;
        }

        private async UniTaskVoid AwaitAbility(AbilityActivationHandle handle)
        {
            bool targetingCancelled = false;
            try
            {
                handle.IsTargeting = true;
                await handle.Proxy.ActivateTargetingTask(handle.TargetingCts.Token, handle.Data);
            }
            catch (OperationCanceledException)
            {
                targetingCancelled = true;
            }
            catch (Exception e)
            {
                // Anything else went past targeting without the handle being cleaned, and the activation held its
                // place for good (TS-V)
                Debug.LogError($"[Ability] {Spec.Base.GetName()} failed in targeting: {e}");
                targetingCancelled = true;
            }
            finally
            {
                handle.IsTargeting = false;

                if (handle.Data.TryGetFirstTarget(out var target) && !Spec.ValidateAllActivationRequirements(target, handle.Data))
                {
                    targetingCancelled = true;
                }

                if (targetingCancelled)
                {
                    CleanHandle(handle);
                }
            }

            if (targetingCancelled) return;

            try
            {
                handle.IsExecuting = true;

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
            }
            catch (OperationCanceledException)
            {
                // Ability in execution is interrupted (cancelled)
            }
            catch (Exception e)
            {
                // Anything else is a broken ability. Report it, rather than leave it looking like one that
                // did nothing (PF-008)
                Debug.LogError($"[Ability] {Spec.Base.GetName()} failed: {e}");
            }
            finally
            {
                handle.IsExecuting = false;
                CleanHandle(handle);
                Spec.Source.CompileGrantedTags();
            }
        }

        public void Inject(ISequenceInjection injection)
        {
            if (!IsActive) return;

            // Take a snapshot since injection may modify the list
            var handles = _activeHandles.ToArray();
            foreach (var handle in handles)
            {
                handle.Proxy.Inject(injection, handle.Data);

                // For interrupt injections, cancel the appropriate tokens
                if (injection is InterruptSequenceInjection)
                {
                    if (handle.IsTargeting) handle.TargetingCts?.Cancel();
                    if (handle.IsExecuting) handle.Cts?.Cancel();
                }
            }
        }

        internal void OnHandleClaimReleased(AbilityActivationHandle handle)
        {
            handle.Data.System.ReleaseClaim(this, handle.Data);
        }

        private void CleanHandle(AbilityActivationHandle handle)
        {
            handle.ReleaseClaimIfNeeded();
            handle.Proxy.Clean();
            _idleProxies.Add(handle.Proxy);
            handle.CleanTokens();
            RemoveActiveHandle(handle);
            handle.Data.System.EndActivation(this, handle.Data);
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
