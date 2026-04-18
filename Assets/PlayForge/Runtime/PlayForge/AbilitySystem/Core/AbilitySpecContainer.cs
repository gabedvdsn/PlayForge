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

        private readonly List<AbilityActivationHandle> _activeHandles = new();

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
            var handle = new AbilityActivationHandle(this, implicitData, abilityName);
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
            catch (Exception ex)
            {
                // Ability in execution is interrupted (cancelled)
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
            handle.CleanTokens();
            RemoveActiveHandle(handle);
            handle.Data.System.EndActivation(this, handle.Data);
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
