using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class AbilitySpecContainer
    {
        public AbilitySpec Spec;

        public bool IsActive { get; private set; }
        public bool IsTargeting { get; private set; }
        public bool IsClaiming => IsTargeting || IsActive;

        private AbilityDataPacket activeData;
        
        private AbilityProxy Proxy;
        public CancellationTokenSource cts;
        private CancellationTokenSource targetingCts;

        public AbilitySpecContainer(AbilitySpec spec)
        {
            Spec = spec;
            IsActive = false;

            Proxy = Spec.Base.Proxy.GenerateProxy();
            ResetTokens();
        }

        public bool ActivateAbility(AbilityDataPacket implicitData)
        {
            if (IsClaiming) return false; // Prevent reactivation mid-use
            if (!Spec.Owner.AsData().AbilitySystem.ClaimActive(this, implicitData)) return false;

            activeData = implicitData;
            activeData.AddPayload(Tags.PAYLOAD_DERIVATION, Spec);

            Reset();

            AwaitAbility().Forget();

            return true;
        }

        private void Reset()
        {
            IsActive = false;
            IsTargeting = false;

            ResetTokens();
        }

        private async UniTaskVoid AwaitAbility()
        {
            bool targetingCancelled = false;
            try
            {
                IsTargeting = true;
                await Proxy.ActivateTargetingTask(targetingCts.Token, activeData);
            }
            catch (OperationCanceledException)
            {
                // Targeting is cancelled
                targetingCancelled = true;
            }
            finally
            {
                IsTargeting = false;
                
                if (activeData.TryGetFirstTarget(out var target) && !Spec.ValidateActivationRequirements(target, activeData))
                {
                    targetingCancelled = true;
                }
                
                if (targetingCancelled)
                {
                    CleanAndRelease();
                }
            }

            if (targetingCancelled) return;

            try
            {
                IsActive = true;

                Spec.Owner.GetTagCache().AddTags(Spec.Base.Tags.ActiveGrantedTags);

                await Proxy.Activate(cts.Token, activeData);
            }
            catch (OperationCanceledException)
            {
                // Ability in execution is interrupted (cancelled)
                // ALWAYS as a result of an injection (break or
            }
            finally
            {
                IsActive = false;
                Spec.Owner.GetTagCache().RemoveTags(Spec.Base.Tags.ActiveGrantedTags);
                
                CleanAndRelease();
            }
        }

        public void Inject(IAbilityInjection injection)
        {
            if (!IsClaiming) return;

            injection.OnContainerInject(this);
            Proxy.Inject(injection, activeData);
        }

        public void CleanAndRelease()
        {
            Proxy.Clean();

            CleanTargetingToken();
            CleanActivationToken();

            Spec.Owner.AsData().AbilitySystem.ReleaseClaim(this, activeData);

            activeData = null;
        }

        private void CleanTargetingToken()
        {
            if (targetingCts is null) return;

            if (!targetingCts.IsCancellationRequested) targetingCts?.Cancel();
            targetingCts?.Dispose();
            targetingCts = null;
        }

        private void CleanActivationToken()
        {
            if (cts is null) return;

            if (!cts.IsCancellationRequested) cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private void ResetTokens()
        {
            CleanTargetingToken();
            CleanActivationToken();

            cts = new CancellationTokenSource();
            targetingCts = new CancellationTokenSource();
        }

        public override string ToString()
        {
            return $"{Spec}";
        }
    }
}