using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class AbilityProxy
    {
        private int StageIndex;
        private readonly AbilityProxySpecification Specification;

        private Dictionary<int, CancellationTokenSource> stageSources;
        private UniTaskCompletionSource nextStageSignal;
        private int maintainedStages;

        private bool appliedUsage;
        
        public AbilityProxy(AbilityProxySpecification specification)
        {
            StageIndex = -1;
            Specification = specification;
        }

        private void Reset()
        {
            StageIndex = -1;
            stageSources = new Dictionary<int, CancellationTokenSource>();
            maintainedStages = 0;
            appliedUsage = false;
        }

        public void Clean()
        {
            if (stageSources is not null)
            {
                foreach (int stageIndex in stageSources.Keys)
                {
                    stageSources[stageIndex]?.Cancel();
                }

                stageSources.Clear();
            }
        }

        public async UniTask ActivateTargetingTask(CancellationToken token, AbilityDataPacket implicitData)
        {
            try
            {
                // If there is a targeting task assigned...
                if (Specification.TargetingProxy is not null)
                {
                    Specification.TargetingProxy.Prepare(implicitData);
                    await Specification.TargetingProxy.Activate(implicitData, token);
                }
            }
            catch (OperationCanceledException)
            {
                //
            }
            finally
            {
                Specification.TargetingProxy?.Clean(implicitData);
            }
        }

        public async UniTask Activate(CancellationToken token, AbilityDataPacket implicitData)
        {
            Reset();
            
            await ActivateNextStage(implicitData, token);

            await UniTask.WaitUntil(() => maintainedStages <= 0, cancellationToken: token);
            
            if (!appliedUsage && implicitData.Spec is AbilitySpec spec) spec.ApplyUsageEffects();
        }
        
        private async UniTask ActivateNextStage(AbilityDataPacket data, CancellationToken token)
        {
            StageIndex += 1;
            if (StageIndex < Specification.Stages.Length)
            {
                try
                {
                    nextStageSignal = new UniTaskCompletionSource();

                    foreach (var task in Specification.Stages[StageIndex].Tasks) task.Prepare(data);

                    ActivateStage(Specification.Stages[StageIndex], StageIndex, data, token).Forget();
                    await nextStageSignal.Task.AttachExternalCancellation(token);
                }
                catch (OperationCanceledException)
                {
                    //
                }
                finally
                {
                    foreach (var task in Specification.Stages[StageIndex].Tasks) task.Clean(data);
                    if (Specification.Stages[StageIndex].ApplyUsageEffects && !appliedUsage && data.Spec is AbilitySpec spec)
                    {
                        spec.ApplyUsageEffects();
                        appliedUsage = true;
                    }
                    
                    await ActivateNextStage(data, token);
                }
            }
        }

        private async UniTask ActivateStage(AbilityProxyStage stage, int stageIndex, AbilityDataPacket data, CancellationToken token)
        {
            var stageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            stageSources[stageIndex] = stageCts;
            var stageToken = stageCts.Token;

            var tasks = stage.Tasks.Select(task => task.Activate(data, stageToken)).ToArray();
            
            try
            {
                if (tasks.Length > 0)
                {
                    switch (stage.TaskPolicy)
                    {
                        case EAnyAllPolicy.Any:
                            await UniTask.WhenAny(tasks);
                            stageCts.Cancel();
                            break;
                        case EAnyAllPolicy.All:
                            await UniTask.WhenAll(tasks);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //
            }
            finally
            {
                stageCts.Dispose();
                stageSources.Remove(stageIndex);
            
                // If this stage is not maintained (hence stageIndex == StageIndex), then set next stage signal
                if (stageIndex == StageIndex) nextStageSignal?.TrySetResult();
                else maintainedStages -= 1;
            }
        }
        
        public void Inject(Tag injection, AbilityDataPacket implicitData)
        {
            bool _success = true;
            if (injection == Tags.INJECT_INTERRUPT)
            {
                // Do nothing -- handled externally via the high-level cts token
            }
            else if (injection == Tags.INJECT_BREAK_STAGE)
            {
                if (StageIndex < 0 || stageSources[StageIndex] is null) _success = false;
                else stageSources[StageIndex].Cancel();
            }
            else if (injection == Tags.INJECT_MAINTAIN_STAGE)
            {
                maintainedStages += 1;
                nextStageSignal?.TrySetResult();
            }
            else if (injection == Tags.INJECT_STOP_MAINTAIN)
            {
                if (StageIndex < 0 || stageSources.Count == 0) _success = false;
                else stageSources[stageSources.Keys.ToArray()[0]]?.Cancel();
            }
            else if (injection == Tags.INJECT_STOP_MAINTAIN_ALL)
            {
                if (StageIndex < 0 || stageSources.Count == 0) _success = false;
                else foreach (int stageIndex in stageSources.Keys) stageSources[stageIndex]?.Cancel();
            }

            HandleInjectionCallback(
                _success,
                Specification.Stages[StageIndex].Tasks,
                Specification.Stages[StageIndex]
            );
            
            return;

            void HandleInjectionCallback(bool success, AbstractProxyTask[] tasks, AbilityProxyStage stage)
            {
                if (implicitData.Spec.GetOwner().FindAbilitySystem(out var asc))
                {
                    asc.Callbacks.AbilityInjected(AbilityCallbackStatus.Generate(implicitData, tasks, stage, injection, success));
                }
            }
        }
    }
}
