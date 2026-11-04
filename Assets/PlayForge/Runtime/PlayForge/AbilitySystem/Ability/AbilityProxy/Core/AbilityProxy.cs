using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Bridge between ability activation and sequence-based execution.
    ///
    /// AbilityProxy compiles the AbilityBehaviour into reusable TaskSequences
    /// at construction time — one for targeting and one for execution stages.
    /// Both phases register with ProcessControl for full visibility.
    ///
    /// Since AbilityDataPacket extends SequenceDataPacket, the same packet
    /// flows through targeting, execution, callbacks, and ProcessControl.
    ///
    /// Injection, callbacks, and usage effects are handled transparently:
    /// - IAbilityInjection calls are translated to ISequenceInjection internally
    /// - AbilitySystemCallbacks fire from sequence stage hooks
    /// - Usage effects apply at configured stage boundaries
    /// </summary>
    public class AbilityProxy
    {
        private int StageIndex;
        private readonly AbilityBehaviour Behaviour;

        /// <summary>The compiled execution TaskSequence (reusable across activations).</summary>
        private TaskSequence CompiledSequence { get; set; }

        /// <summary>The compiled targeting TaskSequence (reusable, null if no targeting task).</summary>
        private TaskSequence CompiledTargeting { get; set; }

        /// <summary>The ProcessRelay for the active execution sequence (null when not running).</summary>
        public ProcessRelay ActiveRelay { get; private set; }

        /// <summary>The ProcessRelay for the active targeting sequence (null when not targeting).</summary>
        public ProcessRelay TargetingRelay { get; private set; }

        // Legacy fields preserved for injection compatibility
        private Dictionary<int, CancellationTokenSource> stageSources;
        private UniTaskCompletionSource nextStageSignal;
        private int maintainedStages;

        private bool usageEffectsApplied;

        private int _activeCriticalStages;
        private int _lastCriticalStageIndex;
        private int _maxFinishedCriticalStageIndex;
        public bool HasAnyCriticalStage { get; private set; }
        public Action OnCriticalSectionExited { get; set; }

        public AbilityProxy(AbilityBehaviour behaviour, string abilityName = null)
        {
            StageIndex = -1;
            Behaviour = behaviour;

            HasAnyCriticalStage = false;
            if (Behaviour.Stages != null)
            {
                for (int i = 0; i < Behaviour.Stages.Count && !HasAnyCriticalStage; i++)
                    HasAnyCriticalStage = HasCriticalTask(Behaviour.Stages[i]);
            }

            CompileSequence(abilityName);
        }

        // Whether a stage holds a critical task, by index. It was asked through LINQ on every activation, whose
        // enumerators allocated (TS-5)
        private static bool HasCriticalTask(AbilityTaskBehaviourStage stage)
        {
            var tasks = stage?.Tasks;
            if (tasks == null) return false;

            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] != null && tasks[i].IsCriticalSection) return true;
            }
            return false;
        }

        /// <summary>
        /// Compiles the behaviour into TaskSequences (targeting + execution).
        /// Called once, typically at grant time. Safe to call multiple times (recompiles).
        /// </summary>
        /// <param name="abilityName">Display name for ProcessControl visibility.</param>
        public void CompileSequence(string abilityName = null)
        {
            CompiledTargeting = AbilitySequenceCompiler.CompileTargeting(Behaviour, abilityName);
            CompiledSequence = AbilitySequenceCompiler.Compile(Behaviour, abilityName);
        }

        /// <summary>
        /// Returns true if the proxy has a compiled execution sequence ready.
        /// </summary>
        public bool IsCompiled => CompiledSequence != null;

        /// <summary>
        /// Returns true if the proxy has a compiled targeting sequence.
        /// </summary>
        public bool HasCompiledTargeting => CompiledTargeting != null;

        /// <summary>
        /// True when nothing of an activation is still running on this proxy, so it can run the next one. Its
        /// compiled sequences hold one run at a time.
        /// </summary>
        public bool IsIdle => ActiveRelay == null && TargetingRelay == null
                              && CompiledSequence?.Runtime == null && CompiledTargeting?.Runtime == null;

        private void Reset()
        {
            StageIndex = -1;
            stageSources = new Dictionary<int, CancellationTokenSource>();
            maintainedStages = 0;
            usageEffectsApplied = false;
            _activeCriticalStages = 0;
            _maxFinishedCriticalStageIndex = -1;

            // Precompute the last stage index that contains a critical task
            _lastCriticalStageIndex = -1;
            if (Behaviour.Stages != null)
            {
                for (int i = Behaviour.Stages.Count - 1; i >= 0; i--)
                {
                    if (HasCriticalTask(Behaviour.Stages[i]))
                    {
                        _lastCriticalStageIndex = i;
                        break;
                    }
                }
            }
        }

        public void Clean()
        {
            // Interrupt any running sequences
            CompiledTargeting?.Interrupt();
            CompiledSequence?.Interrupt();

            if (stageSources is not null)
            {
                foreach (int stageIndex in stageSources.Keys)
                {
                    stageSources[stageIndex]?.Cancel();
                    stageSources[stageIndex]?.Dispose();
                }

                stageSources.Clear();
            }

            ActiveRelay = null;
            TargetingRelay = null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TARGETING (sequence-based when compiled, legacy fallback otherwise)
        // ═══════════════════════════════════════════════════════════════════════════

        public async UniTask ActivateTargetingTask(CancellationToken token, AbilityDataPacket implicitData)
        {
            if (Behaviour.Targeting != null)
            {
                implicitData.AppendPath($"Targeting[{Behaviour.Targeting.GetType().Name}]");
            }

            if (HasCompiledTargeting)
            {
                await ActivateTargetingViaSequence(token, implicitData);
            }
            else
            {
                await ActivateTargetingLegacy(token, implicitData);
            }

            // A targeting task gives up by throwing, which only ends its own sequence, so the ability hears of
            // it here. A task set not to break lets the ability go on (TS-C)
            if (implicitData.TargetingFailed && Behaviour.Targeting is { BreakRuntimeOnInvalid: true })
            {
                throw new OperationCanceledException();
            }

            // Targeting that didn't get to its end has no target to give, whether it threw or something ended its
            // sequence first. The ability went on regardless (TS-V)
            if (HasCompiledTargeting && !implicitData.TargetingFinished)
            {
                throw new OperationCanceledException();
            }
        }

        /// <summary>
        /// Runs the compiled targeting sequence through ProcessControl.
        /// </summary>
        private async UniTask ActivateTargetingViaSequence(CancellationToken token, AbilityDataPacket implicitData)
        {
            var handler = implicitData.EffectOrigin.GetSource();
            // Refused, targeting ends here, and the ability with it. It used to run outside ProcessControl, where no
            // pause or terminate could reach it (TS-AC)
            if (!ProcessControl.Register(CompiledTargeting, handler, implicitData, out var relay))
            {
                throw new OperationCanceledException();
            }

            TargetingRelay = relay;

            try
            {
                // Done in the frame targeting ends. Polling its relay found that a frame later (TS-Z)
                await relay.WhenTerminated(token);
            }
            catch (OperationCanceledException)
            {
                CompiledTargeting?.Interrupt();
                throw;
            }
            finally
            {
                TargetingRelay = null;
            }
        }

        /// <summary>
        /// Legacy targeting: direct Prepare/Activate/Clean without ProcessControl.
        /// </summary>
        private async UniTask ActivateTargetingLegacy(CancellationToken token, AbilityDataPacket implicitData)
        {
            try
            {
                if (Behaviour.Targeting is not null)
                {
                    Behaviour.Targeting.Prepare(implicitData);
                    await Behaviour.Targeting.Activate(implicitData, token);
                }
            }
            finally
            {
                Behaviour.Targeting?.Clean(implicitData);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SEQUENCE-BASED EXECUTION
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Activates the ability via the compiled sequence, registering with ProcessControl.
        /// Falls back to legacy execution if no compiled sequence exists.
        /// </summary>
        public async UniTask Activate(CancellationToken token, AbilityDataPacket implicitData)
        {
            Reset();

            // Run asset loaders before any stages execute.
            // These are synchronous tasks that populate the data packet with
            // pre-configured assets (effects, entities, tags, etc.).
            RunAssetLoaders(implicitData, token);

            if (IsCompiled)
            {
                await ActivateViaSequence(token, implicitData);
            }
            else
            {
                await ActivateLegacy(token, implicitData);
            }
        }

        /// <summary>
        /// Executes all configured asset loading tasks, populating the data packet
        /// with pre-configured assets before ability stages begin.
        /// </summary>
        private void RunAssetLoaders(AbilityDataPacket data, CancellationToken token)
        {
            if (Behaviour.AssetLoaders == null || Behaviour.AssetLoaders.Count == 0) return;

            data.AppendPath($"Assets[{Behaviour.AssetLoaders.Count}]");

            foreach (var loader in Behaviour.AssetLoaders)
            {
                if (loader == null) continue;
                
                loader.Prepare(data);
                loader.Activate(data, token);
                loader.Clean(data);
                
                data.AppendPath($"{loader.GetType().Name}");
            }
        }

        /// <summary>
        /// Runs the compiled execution TaskSequence through ProcessControl.
        /// </summary>
        private async UniTask ActivateViaSequence(CancellationToken token, AbilityDataPacket implicitData)
        {
            // Forward critical section exit from the compiled sequence up through the proxy.
            // AbilitySpecContainer wires OnCriticalSectionExited → handle.ReleaseClaimIfNeeded().
            CompiledSequence.OnCriticalSectionExited = () => OnCriticalSectionExited?.Invoke();

            // AbilityDataPacket extends SequenceDataPacket, so pass it directly.
            var handler = implicitData.EffectOrigin.GetSource();
            // Refused, the ability ends here. It used to run its stages outside ProcessControl instead, where no
            // pause or terminate could reach them (TS-AC)
            if (!ProcessControl.Register(CompiledSequence, handler, implicitData, out var relay))
            {
                ForgeLog.Warning("ProcessControl refused an ability's sequence, so the ability ends without running.");
                throw new OperationCanceledException();
            }

            ActiveRelay = relay;

            try
            {
                // Wait for the process to terminate (SelfTerminating lifecycle).
                // The relay state transitions: Created → Running → Terminated.
                // Done in the frame it does. Polling the relay found that a frame later (TS-Z)
                await relay.WhenTerminated(token);
            }
            catch (OperationCanceledException)
            {
                // External cancellation (e.g., InterruptInjection from container).
                // Interrupt the running sequence so it cleans up properly.
                CompiledSequence?.Interrupt();
            }
            finally
            {
                implicitData.InUse = false;
                ActiveRelay = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION (translates ability injections to sequence injections)
        // ═══════════════════════════════════════════════════════════════════════════

        public void Inject(ISequenceInjection injection, AbilityDataPacket activeData)
        {
            // If running via compiled sequence, translate to sequence injection
            if (IsCompiled && CompiledSequence.IsRunning)
            {
                var success = CompiledSequence.Inject(injection);
                // Fire ability injection callback regardless of translation success
                FireInjectionCallback(injection, activeData, success);
                return;
            }

            // Legacy injection path
            var _success = CompiledSequence.Inject(injection);

            var owner = activeData.EffectOrigin.GetSource();
            if (!owner.FindAbilitySystem(out var asc)) return;

            var stageSafe = (StageIndex >= 0 && StageIndex < Behaviour.Stages.Count)
                ? Behaviour.Stages[StageIndex]
                : new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };

            var status = AbilityCallbackStatus.GenerateForInjection(
                activeData,
                stageSafe,
                injection, _success);
            owner.GetFrameSummary().RecordAbilityInjection(status);

            asc.Callbacks.AbilityInjected(status);
        }

        private void FireInjectionCallback(
            ISequenceInjection injection,
            AbilityDataPacket activeData,
            bool success)
        {
            var owner = activeData.EffectOrigin.GetSource();
            if (!owner.FindAbilitySystem(out var asc)) return;

            var stageSafe = GetCurrentAbilityStage();

            var status = AbilityCallbackStatus.GenerateForInjection(
                activeData, stageSafe, injection, success);
            owner.GetFrameSummary().RecordAbilityInjection(status);

            asc.Callbacks.AbilityInjected(status);
        }

        /// <summary>
        /// Resolves the current AbilityTaskBehaviourStage from the running sequence's stage index.
        /// </summary>
        private AbilityTaskBehaviourStage GetCurrentAbilityStage()
        {
            if (!IsCompiled || CompiledSequence.Runtime == null)
                return new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };

            int idx = CompiledSequence.Runtime.StageIndex;
            if (idx >= 0 && idx < Behaviour.Stages.Count)
            {
                return Behaviour.Stages[idx];
            }

            return new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // LEGACY EXECUTION (preserved for fallback / backward compatibility)
        // ═══════════════════════════════════════════════════════════════════════════

        private async UniTask ActivateLegacy(CancellationToken token, AbilityDataPacket implicitData)
        {
            await ActivateNextStageLegacy(implicitData, token);

            await UniTask.WaitUntil(() => maintainedStages <= 0, cancellationToken: token);

            implicitData.InUse = false;
        }

        private async UniTask ActivateNextStageLegacy(AbilityDataPacket data, CancellationToken token)
        {
            StageIndex += 1;
            if (StageIndex < Behaviour.Stages.Count)
            {
                try
                {
                    nextStageSignal = new UniTaskCompletionSource();

                    foreach (var task in Behaviour.Stages[StageIndex].Tasks) task.Prepare(data);

                    ActivateStageLegacy(Behaviour.Stages[StageIndex], StageIndex, data, token).Forget();
                    await nextStageSignal.Task.AttachExternalCancellation(token);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[AbilityProxy] Stage {StageIndex} error: {ex}");
                }
                finally
                {
                    foreach (var task in Behaviour.Stages[StageIndex].Tasks) task.Clean(data);
                    if (Behaviour.Stages[StageIndex].ApplyUsageEffects && !usageEffectsApplied && data.EffectOrigin is AbilitySpec spec)
                    {
                        spec.ApplyUsageEffects();
                        usageEffectsApplied = true;
                    }

                    await ActivateNextStageLegacy(data, token);
                }
            }
        }

        private async UniTask ActivateStageLegacy(AbilityTaskBehaviourStage stage, int stageIndex, AbilityDataPacket data, CancellationToken token)
        {
            bool isCriticalStage = HasCriticalTask(stage);
            if (isCriticalStage) _activeCriticalStages++;

            var asc = data.EffectOrigin.GetSource().ToGAS().GetAbilitySystem();
            if (asc is null)
            {
                if (isCriticalStage)
                {
                    _maxFinishedCriticalStageIndex = Math.Max(_maxFinishedCriticalStageIndex, stageIndex);
                    if (--_activeCriticalStages == 0 && _maxFinishedCriticalStageIndex >= _lastCriticalStageIndex)
                        OnCriticalSectionExited?.Invoke();
                }
                if (stageIndex == StageIndex) nextStageSignal?.TrySetResult();
                else maintainedStages -= 1;
                return;
            }

            var stageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            stageSources[stageIndex] = stageCts;
            var stageToken = stageCts.Token;

            await UniTask.SwitchToMainThread(stageToken);
            asc.Callbacks.AbilityStageActivated(AbilityCallbackStatus.GenerateForStageEvent(data, stage, true));

            var tasks = new UniTask[stage.Tasks.Count];
            for (int i = 0; i < stage.Tasks.Count; i++)
            {
                tasks[i] = stage.Tasks[i].Activate(data, stageToken);
                await UniTask.SwitchToMainThread(stageToken);
                asc.Callbacks.AbilityTaskActivated(AbilityCallbackStatus.GenerateForTask(data, stage.Tasks[i], stage, true));
            }

            bool canceled = false;
            Exception err = null;

            try
            {
                if (tasks.Length > 0)
                {
                    if (stage.StagePolicy == null)
                    {
                        Debug.LogError($"Stage {stageIndex}: StagePolicy is null! Falling back to WhenAll.");
                        await UniTask.WhenAll(tasks);
                    }
                    else
                    {
                        await stage.StagePolicy.AwaitStageActivation(this, stage, tasks, data, asc.Callbacks, stageCts);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (Exception ex)
            {
                err = ex;
                Debug.LogError($"Stage {stageIndex} error: {ex}");
            }
            finally
            {
                stageSources.Remove(stageIndex);

                // If this stage is not maintained (hence stageIndex == StageIndex), then set next stage signal
                if (stageIndex == StageIndex) nextStageSignal?.TrySetResult();
                else maintainedStages -= 1;

                if (isCriticalStage)
                {
                    _maxFinishedCriticalStageIndex = Math.Max(_maxFinishedCriticalStageIndex, stageIndex);
                    if (--_activeCriticalStages == 0 && _maxFinishedCriticalStageIndex >= _lastCriticalStageIndex)
                        OnCriticalSectionExited?.Invoke();
                }

                await UniTask.SwitchToMainThread(stageToken);
                asc.Callbacks.AbilityStageEnded(AbilityCallbackStatus.GenerateForStageEvent(data, stage, !canceled && err is null));

                stageCts.Dispose();
            }
        }

        public UniTask[] GetWatchedTasksLegacy(UniTask[] tasks, AbilityTaskBehaviourStage stage, AbilityDataPacket data, AbilitySystemCallbacks callbacks, bool notifyOnCancel)
        {
            // By index, into an array of their count: LINQ allocated its enumerators and a list to grow (TS-5)
            var watched = new UniTask[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                watched[i] = WatchTask(tasks[i], callbacks, i, stage, data, notifyOnCancel);
            return watched;
        }

        public UniTask WatchTask(
            UniTask inner,
            AbilitySystemCallbacks callbacks,
            int taskIndex,
            AbilityTaskBehaviourStage stage,
            AbilityDataPacket data,
            bool notifyOnCancel
        )
        {
            return WatchTaskCore();

            async UniTask WatchTaskCore()
            {
                bool canceled = false;
                Exception err = null;

                try
                {
                    await inner;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }
                catch (Exception ex)
                {
                    err = ex;
                }
                finally
                {
                    if (!canceled || notifyOnCancel)
                    {
                        await UniTask.SwitchToMainThread();
                        callbacks.AbilityTaskEnded(AbilityCallbackStatus.GenerateForTask(data, stage.Tasks[taskIndex], stage, err is null && !canceled));
                    }
                }
            }
        }
    }
}
