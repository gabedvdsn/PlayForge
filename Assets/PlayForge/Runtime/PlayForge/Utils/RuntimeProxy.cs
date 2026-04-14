/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class RuntimeProxy<B, S, T, D> 
        where B : ProxyBehaviour<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where S : ProxyBehaviourStage<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where T : IProxyRelatedTask<D> 
        where D : ProcessDataPacket
    {
        public int StageIndex;
        public readonly B Behaviour;

        public Dictionary<int, CancellationTokenSource> stageSources;
        public UniTaskCompletionSource nextStageSignal;
        public int maintainedStages;

        public RuntimeProxy(B behaviour)
        {
            StageIndex = -1;
            Behaviour = behaviour;
        }

        protected virtual void Reset()
        {
            StageIndex = -1;
            stageSources = new Dictionary<int, CancellationTokenSource>();
            maintainedStages = 0;
        }

        public virtual void Clean()
        {
            if (stageSources is null) return;

            foreach (int stageIndex in stageSources.Keys)
            {
                stageSources[stageIndex]?.Cancel();
                stageSources[stageIndex]?.Dispose();
            }
            
            stageSources.Clear();
        }

        public async UniTask Activate(D data, CancellationToken token)
        {
            Reset();

            await ActivateNextStage(data, token);

            await UniTask.WaitUntil(() => maintainedStages <= 0, cancellationToken: token);
            
            data.InUse = false;
        }

        private async UniTask ActivateNextStage(D data, CancellationToken token)
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
                catch (OperationCanceledException ex)
                {
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    foreach (var task in Behaviour.Stages[StageIndex].Tasks) task.Clean(data);
                    await ActivateNextStage(data, token);
                }
            }
        }

        private async UniTask ActivateStage(S stage, int stageIndex, D data, CancellationToken token)
        {
            var stageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            stageSources[stageIndex] = stageCts;
            var stageToken = stageCts.Token;

            var tasks = new UniTask[stage.Tasks.Count];
            for (int i = 0; i < stage.Tasks.Count; i++)
            {
                tasks[i] = stage.Tasks[i].Activate(data, stageToken);
            }

            bool cancelled = false;
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
                        await stage.StagePolicy.AwaitStageActivation(this, stage, tasks, data)
                    }
                }
            }
        }
        
        public UniTask[] GetWatchedTasks(UniTask[] tasks, S stage, D data, Action<S, T, D, bool> onFinal = null, bool notifyOnCancel = true)
        {
            return tasks
                .Select((t, i) => WatchTask(t, i, stage, data, onFinal, notifyOnCancel))
                .ToArray();
        }
        
        public UniTask WatchTask(
            UniTask inner, int taskIndex,
            S stage, D data, 
            Action<S, T, D, bool> onFinal = null, bool notifyOnCancel = true
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
                    if ((!canceled || notifyOnCancel) && onFinal is not null)
                    {
                        await UniTask.SwitchToMainThread();
                        onFinal.Invoke(stage, stage.Tasks[taskIndex], data, err is null && !canceled);
                    }
                }
            }
        }
        
    }

    public class ProxyBehaviour<P, B, S, T, D> 
        where P : RuntimeProxy<B, S, T, D> 
        where B : ProxyBehaviour<P, B, S, T, D> 
        where S : ProxyBehaviourStage<P, B, S, T, D> 
        where T : IProxyRelatedTask<D> 
        where D : ProcessDataPacket
    {
        public List<S> Stages = new();
    }

    public class ProxyBehaviourStage<P, B, S, T, D> 
        where P : RuntimeProxy<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where B : ProxyBehaviour<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where S : ProxyBehaviourStage<P, B, S, T, D> 
        where T : IProxyRelatedTask<D> 
        where D : ProcessDataPacket
    {
        //public IProxyStagePolicy StagePolicy = new AllProxyStagePolicy();
        public List<T> Tasks = new();
    }

    public interface IProxyRelatedTask<D> where D : ProcessDataPacket
    {
        public void Prepare(D data);
        public UniTask Activate(D data, CancellationToken token);
        public void Clean(D data);
    }

    public interface IProxyStagePolicy<P, B, S, T, D> 
        where P : RuntimeProxy<B, S, T, D> 
        where B : ProxyBehaviour<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where S : ProxyBehaviourStage<RuntimeProxy<B, S, T, D>, B, S, T, D> 
        where T : IProxyRelatedTask<D> 
        where D : ProcessDataPacket
    {
        public UniTask AwaitStageActivation(P proxy, S stage, UniTask[] tasks, D data, CancellationTokenSource stageCts);
    }
}
*/
