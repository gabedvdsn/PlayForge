using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class AbilitySpecContainer
    {
        public AbilitySpec Spec;

        public bool IsActive => _activeHandles.Count > 0;
        public bool IsClaiming => _activeHandles.Any(h => !h.ClaimReleased && (h.IsTargeting || h.IsExecuting));

        private readonly List<AbilityActivationHandle> _activeHandles = new();

        public AbilitySpecContainer(AbilitySpec spec)
        {
            Spec = spec;
        }

        public bool ActivateAbility(AbilityDataPacket implicitData)
        {
            if (IsClaiming) return false;
            if (!Spec.Owner.AsData().AbilitySystem.ClaimActive(this, implicitData)) return false;

            implicitData.AddPayload(Tags.PAYLOAD_DERIVATION, Spec);

            var handle = new AbilityActivationHandle(this, implicitData);
            _activeHandles.Add(handle);

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

                if (handle.Data.TryGetFirstTarget(out var target) && !Spec.ValidateActivationRequirements(target, handle.Data))
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

                Spec.Owner.GetTagCache().AddTags(Spec.Base.Tags.ActiveGrantedTags);

                // Wire critical section callback before activation
                handle.Proxy.OnCriticalSectionExited = () => handle.ReleaseClaimIfNeeded();

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
            finally
            {
                handle.IsExecuting = false;
                Spec.Owner.GetTagCache().RemoveTags(Spec.Base.Tags.ActiveGrantedTags);

                CleanHandle(handle);
            }
        }

        public void Inject(IAbilityInjection injection)
        {
            if (!IsActive) return;

            // Take a snapshot since injection may modify the list
            var handles = _activeHandles.ToArray();
            foreach (var handle in handles)
            {
                injection.OnContainerInject(this);
                handle.Proxy.Inject(injection, handle.Data);

                // For interrupt injections, cancel the appropriate tokens
                if (injection is InterruptInjection)
                {
                    if (handle.IsTargeting) handle.TargetingCts?.Cancel();
                    if (handle.IsExecuting) handle.Cts?.Cancel();
                }
            }
        }

        internal void OnHandleClaimReleased(AbilityActivationHandle handle)
        {
            Spec.Owner.AsData().AbilitySystem.ReleaseClaim(this, handle.Data);
        }

        private void CleanHandle(AbilityActivationHandle handle)
        {
            handle.ReleaseClaimIfNeeded();
            handle.Proxy.Clean();
            handle.CleanTokens();
            _activeHandles.Remove(handle);
        }

        public override string ToString()
        {
            return $"{Spec}";
        }
    }
}
