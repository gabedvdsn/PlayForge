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

        public bool IsActive { get; private set; }
        public bool IsTargeting { get; private set; }
        public bool IsClaiming => IsTargeting || IsActive;

        private AbilityDataPacket activeData;

        private AbilityProxy Proxy;
        public CancellationTokenSource cts;
        private CancellationTokenSource targetingCts;

        public AbilitySpecContainer(AbilitySpec spec, int abilityIndex)
        {
            Spec = spec;
            Index = abilityIndex;
            IsActive = false;

            Proxy = Spec.Base.Behaviour.GenerateProxy();

            // Compile the ability behaviour into a reusable TaskSequence at grant time.
            // This enables ProcessControl visibility and sequence-based execution.
            var abilityName = Spec.Base?.GetName() ?? $"Anon-Ability[{abilityIndex}]";
            Proxy.CompileSequence(abilityName);

            ResetTokens();
        }

        public bool ActivateAbility(AbilityDataPacket implicitData)
        {
            Debug.Log($"{IsClaiming}" +
                      $"{!Spec.Source.AsData().AbilitySystem.ClaimActive(this, implicitData)}");
            if (IsClaiming) return false; // Prevent reactivation mid-use
            if (!Spec.Source.AsData().AbilitySystem.ClaimActive(this, implicitData)) return false;

            Debug.Log($"Activated!");
            activeData = implicitData;
            activeData.SetPrimary(Tags.DERIVATION, Spec);

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
                // Targeting is cancelled (either externally or via BreakAbilityRuntime)
                targetingCancelled = true;
            }
            finally
            {
                IsTargeting = false;

                // Check the explicit targeting failure flag set by targeting tasks
                if (activeData.TargetingFailed)
                {
                    targetingCancelled = true;
                }

                // Validate that the target meets activation requirements
                if (!targetingCancelled && activeData.TryGetFirstTarget(out var target)
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
                IsActive = true;

                Spec.Source.CompileGrantedTags();

                await Proxy.Activate(cts.Token, activeData);
            }
            catch (Exception ex)
            {
                // Ability in execution is interrupted (cancelled)
                // ALWAYS as a result of an injection (break or
                Debug.LogException(ex);
            }
            finally
            {
                IsActive = false;

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

            var asc = Spec.Source.AsData().AbilitySystem;
            asc.ReleaseClaim(this, activeData);
            activeData = null;
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
