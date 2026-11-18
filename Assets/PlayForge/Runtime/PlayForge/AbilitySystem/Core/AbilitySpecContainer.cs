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

        /// <summary>
        /// Whether a run of the ability is choosing its target now, as a click-targeting task waits for the player's
        /// click: what a game shows as the wait, with its targeting's <see cref="AbstractTargetingAbilityTask.Prompt"/>.
        /// One that gave way to another's activation chooses no more.
        /// </summary>
        public bool IsTargeting
        {
            get
            {
                foreach (var handle in _activeHandles)
                    if (handle.IsTargeting && !handle.ClaimReleased) return true;
                return false;
            }
        }

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
        /// frame.
        /// </summary>
        internal bool IsActivating => _activating > 0;

        private int _activating;

        private readonly List<AbilityActivationHandle> _activeHandles = new();

        /// <summary>
        /// The stage the ability's first active run is in, counted from 0, or -1 while none is running or started. The
        /// Debugger shows it as "Stage 2/3".
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
        // proxy is compiled once and comes back here when its activation is over. An activation that finds
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
            // How it ends, which its components hear
            var how = EAbilityEnd.Done;
            // What of its components refused the pick its targeting made, and why, told below
            var pickRefused = EActivationRefusal.None;
            string pickRule = null, pickDetail = null;
            try
            {
                handle.IsTargeting = true;
                await handle.Proxy.ActivateTargetingTask(handle.TargetingCts.Token, handle.Data);

                // A target or a point its targeting picked that one of its components refuses, whichever task picked it,
                // ends it here, as a Cast Range refuses one beyond it: asked before the target is resolved, as a picked
                // point has no entity
                pickRefused = Spec.PickRefusal(handle.Data, out pickRule, out pickDetail);
                if (pickRefused != EActivationRefusal.None)
                {
                    handle.Data.TargetingFailed = true;
                    targetingCancelled = true;
                    how = EAbilityEnd.Cancelled;
                }
            }
            catch (OperationCanceledException)
            {
                targetingCancelled = true;
                how = EAbilityEnd.Cancelled;
            }
            catch (Exception e)
            {
                // Anything else went past targeting without the handle being cleaned, and the activation held its
                // place for good
                ForgeLog.Error(ForgeLog.ESystem.Abilities, $"[Ability] {Spec.Base.GetName()} failed in targeting: {e}");
                targetingCancelled = true;
                how = EAbilityEnd.Failed;
            }
            finally
            {
                handle.IsTargeting = false;

                // Targeting leaves a packet, and what reads a target asks for the entity it points at
                handle.Data.ResolveTarget(Spec.Base.Behaviour.ImplicitTag);

                // Its pick refused, told as a refusal of the kind its component says, in the component's words, as its
                // request said none: nothing of it was paid
                if (pickRefused != EActivationRefusal.None)
                    handle.Data.System?.Refuse(new ActivationRefusal(Spec.Base, handle.Data.Request.Index, pickRefused, pickRule, pickDetail), handle.Data);

                if (handle.Data.TryGetFirstTarget(out var target)
                    && Spec.TargetRefusal(target, handle.Data, out string rule, out string detail) is var why && why != EActivationRefusal.None)
                {
                    // Told as a refusal where its targeting had ended well, as its request said none: nothing of it was
                    // paid, and the check that refused it says why
                    if (!targetingCancelled)
                        handle.Data.System?.Refuse(new ActivationRefusal(Spec.Base, handle.Data.Request.Index, why, rule, detail), handle.Data);
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

                // Its wind-up first, after its target checks and before its cost: its entity's components' and its own,
                // as a turn to face its target. An interrupt cancels it, its cost unpaid
                await WindUp(handle.Data, handle.Cts.Token);

                // Its cost and cooldown, as it starts, after its targeting and its wind-up, where no stage marked
                // Apply Usage Effects takes them: an author who sets a cost expects it paid, and they applied only from
                // such a stage
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

                // An interrupt ends a run through its sequence, with nothing thrown: its token says it was cancelled
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
                // did nothing
                ForgeLog.Error(ForgeLog.ESystem.Abilities, $"[Ability] {Spec.Base.GetName()} failed: {e}");
                how = EAbilityEnd.Failed;
            }
            finally
            {
                handle.IsExecuting = false;
                CleanHandle(handle, how);
                Spec.Source.CompileGrantedTags();
            }
        }

        // The wind-ups of its entity's components and its own, waited on together where any waits; with none waiting,
        // as with no component that winds up, it makes nothing. One that ended already, by a throw or a cancel, says so
        // here
        private UniTask WindUp(AbilityDataPacket data, CancellationToken token)
        {
            List<UniTask> pending = null;
            var ctx = new AbilityComponentContext(Spec, data);
            GASCore.Of(Spec.Source as IGameplayAbilitySystem)?.WindUp(ctx, token, ref pending);
            var components = Spec.Base.Components;
            if (components != null)
            {
                foreach (var component in components)
                {
                    if (component != null) Pend(component.WindUp(ctx, token), ref pending);
                }
            }
            return pending == null ? UniTask.CompletedTask : pending.Count == 1 ? pending[0] : UniTask.WhenAll(pending);
        }

        // A wind-up that ended at once is seen through now, its throw with it; one that waits joins the others
        internal static void Pend(UniTask windUp, ref List<UniTask> pending)
        {
            if (windUp.Status.IsCompleted())
            {
                windUp.GetAwaiter().GetResult();
                return;
            }
            (pending ??= new List<UniTask>()).Add(windUp);
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

        // The stage a run is in, or null while it targets, before its first stage starts
        private AbilityTaskBehaviourStage StageOf(AbilityActivationHandle handle)
        {
            var stages = Spec.Base.Behaviour?.Stages;
            int index = handle.Data?.StageIndex ?? -1;
            return stages != null && index >= 0 && index < stages.Count ? stages[index] : null;
        }

        /// <summary>
        /// Whether a run of this ability is in a stage that keeps <paramref name="incoming"/> from activating: one whose
        /// Blocks names it, or, naming none, whose ability's Blocks does. Blocking was the claim alone.
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
        /// cancels it. Nothing cancelled an ability but an interrupt by slot.
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
        /// Interrupts each run of this ability but one in a critical stage, which is committed, as a task that
        /// cancels abilities by tag does.
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

        /// <summary>
        /// Each run still choosing its target gives way: its targeting is cancelled, nothing of it paid, and its claim
        /// lets go now, so another activation starts at once while the cancelled targeting winds down.
        /// </summary>
        internal void YieldTargeting()
        {
            // Backwards, as an interrupted run can end inside the interrupt and leave the list
            for (int i = _activeHandles.Count - 1; i >= 0; i--)
            {
                if (i >= _activeHandles.Count) continue;
                var handle = _activeHandles[i];
                if (!handle.IsTargeting || handle.ClaimReleased) continue;
                Inject(handle, InterruptSequenceInjection.Instance);
                handle.ReleaseClaimIfNeeded();
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
