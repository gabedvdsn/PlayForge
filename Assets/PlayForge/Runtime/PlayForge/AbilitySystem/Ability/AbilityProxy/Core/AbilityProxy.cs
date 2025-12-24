using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;

namespace FarEmerald.PlayForge
{
    public class AbilityProxy
    {
        public int StageIndex;
        public readonly AbilityBehaviour Behaviour;

        public Dictionary<int, CancellationTokenSource> stageSources;
        public UniTaskCompletionSource nextStageSignal;
        public int maintainedStages;

        public bool usageEffectsApplied;
        
        public AbilityProxy(AbilityBehaviour behaviour)
        {
            StageIndex = -1;
            Behaviour = behaviour;
        }

        private void Reset()
        {
            StageIndex = -1;
            stageSources = new Dictionary<int, CancellationTokenSource>();
            maintainedStages = 0;
            usageEffectsApplied = false;
        }

        public void Clean()
        {
            if (stageSources is not null)
            {
                foreach (int stageIndex in stageSources.Keys)
                {
                    stageSources[stageIndex]?.Cancel();
                    stageSources[stageIndex]?.Dispose();
                }

                stageSources.Clear();
            }
        }

        public async UniTask ActivateTargetingTask(CancellationToken token, AbilityDataPacket implicitData)
        {
            try
            {
                // If there is a targeting task assigned...
                if (Behaviour.Targeting is not null)
                {
                    Behaviour.Targeting.Prepare(implicitData);
                    await Behaviour.Targeting.Activate(implicitData, token);
                }
            }
            catch (OperationCanceledException)
            {
                //
            }
            finally
            {
                Behaviour.Targeting?.Clean(implicitData);
            }
        }

        public async UniTask Activate(CancellationToken token, AbilityDataPacket implicitData)
        {
            Reset();
            
            await ActivateNextStage(implicitData, token);

            await UniTask.WaitUntil(() => maintainedStages <= 0, cancellationToken: token);
            
            if (!usageEffectsApplied && implicitData.Spec is AbilitySpec spec) spec.ApplyUsageEffects();
        }
        
        private async UniTask ActivateNextStage(AbilityDataPacket data, CancellationToken token)
        {
            StageIndex += 1;
            if (StageIndex < Behaviour.Stages.Count)
            {
                try
                {
                    nextStageSignal = new UniTaskCompletionSource();

                    foreach (var task in Behaviour.Stages[StageIndex].Tasks) task.Prepare(data);

                    ActivateStage(Behaviour.Stages[StageIndex], StageIndex, data, token).Forget();
                    await nextStageSignal.Task.AttachExternalCancellation(token);
                }
                catch (OperationCanceledException)
                {
                    //
                }
                finally
                {
                    foreach (var task in Behaviour.Stages[StageIndex].Tasks) task.Clean(data);
                    if (Behaviour.Stages[StageIndex].ApplyUsageEffects && !usageEffectsApplied && data.Spec is AbilitySpec spec)
                    {
                        spec.ApplyUsageEffects();
                        usageEffectsApplied = true;
                    }
                    
                    await ActivateNextStage(data, token);
                }
            }
        }

        private async UniTask ActivateStage(AbilityTaskBehaviourStage stage, int stageIndex, AbilityDataPacket data, CancellationToken token)
        {
            var asc = data.Spec.GetOwner().AsGAS().GetAbilitySystem();
            if (asc is null)
            {
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

            var canceled = false;
            Exception err = null;

            try
            {
                if (tasks.Length > 0)
                {
                    await stage.StagePolicy.AwaitStageActivation(this, 
                        stage, tasks, data, 
                        asc.Callbacks, stageCts);
                }
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
                stageCts.Dispose();
                stageSources.Remove(stageIndex);
            
                // If this stage is not maintained (hence stageIndex == StageIndex), then set next stage signal
                if (stageIndex == StageIndex) nextStageSignal?.TrySetResult();
                else maintainedStages -= 1;
                
                await UniTask.SwitchToMainThread(stageToken);
                asc.Callbacks.AbilityStageEnded(AbilityCallbackStatus.GenerateForStageEvent(data, stage, !canceled && err is null));
            }
        }
        
        public void Inject(IAbilityInjection injection, AbilityDataPacket implicitData)
        {
            var _success = injection.OnProxyInject(this);

            if (!implicitData.Spec.GetOwner().FindAbilitySystem(out var asc)) return;
            
            var stageSafe = (StageIndex >= 0 && StageIndex < Behaviour.Stages.Count)
                ? Behaviour.Stages[StageIndex]
                : new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };
                
            asc.Callbacks.AbilityInjected(
                AbilityCallbackStatus.GenerateForInjection(
                    implicitData,
                    stageSafe,
                    injection, _success
                )
            );
        }

        public UniTask[] GetWatchedTasks(UniTask[] tasks, AbilityTaskBehaviourStage stage, AbilityDataPacket data, AbilitySystemCallbacks callbacks, bool notifyOnCancel)
        {
            return tasks
                .Select((t, i) => WatchTask(t, callbacks, i, stage, data, notifyOnCancel))
                .ToArray();
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
