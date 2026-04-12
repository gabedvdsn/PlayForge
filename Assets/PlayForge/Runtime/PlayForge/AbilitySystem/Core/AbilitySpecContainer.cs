using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpecContainer : ITagSource
    {
        public AbilitySpec Spec;
        public readonly int Index;

        public int ActiveCount { get; private set; }
        public bool IsActive => ActiveCount > 0;
        public bool IsTargeting { get; private set; }
        public bool IsClaiming => IsTargeting || IsActive;

        private AbilityDataPacket activeData;

        private List<AbilityProxy> Proxies;

        private AbilityProxy Proxy;
        public CancellationTokenSource cts;
        private CancellationTokenSource targetingCts;

        public AbilitySpecContainer(AbilitySpec spec, int abilityIndex)
        {
            Spec = spec;
            Index = abilityIndex;
            ActiveCount = 0;

            Proxies = new List<AbilityProxy>();
            
            Proxy = Spec.Base.Behaviour.GenerateProxy();

            // Compile the ability behaviour into a reusable TaskSequence at grant time.
            // This enables ProcessControl visibility and sequence-based execution.
            var abilityName = Spec.Base?.GetName() ?? $"Anon-Ability[{abilityIndex}]";
            Proxy.CompileSequence(abilityName);

            ResetTokens();
        }

        public bool ActivateAbility(AbilityDataPacket implicitData)
        {
            if (IsClaiming) return false; // Prevent reactivation mid-use
            if (!Spec.Source.ToGASComponentData().AbilitySystem.ClaimActive(this, implicitData)) return false;

            activeData = implicitData;
            activeData.SetPrimary(Tags.DERIVATION, Spec);

            Reset();

            AwaitAbility().Forget();

            return true;
        }

        private void Reset()
        {
            ActiveCount = 0;
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
                // Targeting is cancelled (either externally or via BreakAbilityRuntime)
                targetingCancelled = true;
            }
            finally
            {
                IsTargeting = false;

                // Check the explicit targeting failure flag set by targeting tasks
                if (activeData != null && activeData.TargetingFailed)
                {
                    targetingCancelled = true;
                }

                // Validate that the target meets activation requirements
                if (!targetingCancelled && activeData != null
                                        && activeData.TryGetFirstTarget(out var target)
                                        && !Spec.ValidateAllActivationRequirements(target, activeData))
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
                ActiveCount += 1;

                Spec.Source.CompileGrantedTags();

                await Proxy.Activate(cts.Token, activeData);
            }
            catch (Exception ex)
            {
                // Ability in execution is interrupted (cancelled)
                // ALWAYS as a result of an injection (break or interrupt)
                Debug.LogException(ex);
            }
            finally
            {
                ActiveCount -= 1;

                Spec.Source.CompileGrantedTags();

                CleanAndRelease();
            }
        }

        public void Inject(IAbilityInjection injection)
        {
            if (!IsClaiming) return;

            injection.OnContainerInject(this, activeData);
            Proxy.Inject(injection, activeData);
        }

        public void CleanAndRelease()
        {
            Proxy.Clean();

            CleanTargetingToken();
            CleanActivationToken();

            // Capture data before nulling — ReleaseClaim needs it for ActiveCache cleanup.
            var data = activeData;
            activeData = null;

            var asc = Spec.Source.ToGASComponentData().AbilitySystem;
            asc.ReleaseClaim(this, data);

            // Process the activation queue AFTER this container is fully clean.
            // This prevents re-entrant activation from corrupting activeData:
            // without this separation, EndAbilityActivation could trigger ActivateAbility
            // on this same container (setting activeData to new data), and then the caller
            // of CleanAndRelease would continue executing with the wrong activeData.
            asc.EndAbilityActivation();
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
        public IEnumerable<Tag> GetGrantedTags()
        {
            foreach (var t in Spec.Base.Tags.PassiveGrantedTags) yield return t;
            if (!IsActive) yield break;
            foreach (var t in Spec.Base.Tags.ActiveGrantedTags) yield return t;
        }
    }
}
