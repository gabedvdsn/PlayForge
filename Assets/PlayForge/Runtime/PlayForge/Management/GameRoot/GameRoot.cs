using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// GameRoot
    /// </summary>
    public class GameRoot : GameplayAbilitySystem, IEffectOrigin, IManagerial
    {
        [Header("Game Root")]
        
        public static GameRoot Instance;
        
        // Useful for backend systems like observers, audio, etc...
        public List<AbstractCreateProcessAbilityTask> CreateProcessTasks = new();
        private AbilityDataPacket NativeDataPacket;

        public void Bootstrap()
        {
            if (Instance is not null && Instance != this)
            {
                Destroy(gameObject);
            }

            Instance = this;
            
            base.Awake();
        }
        
        public void DeferredInit()
        {
            NativeDataPacket = AbilityDataPacket.GenerateFrom
            (
                IEffectOrigin.GenerateSourceDerivation(this),
                false
            );
            
            NativeDataPacket.AddPayload(Tags.PARENT_TRANSFORM, transform);
            
            RunProcessTasks(CreateProcessTasks);
        }
        
        #region Process Tasks
        
        public void RunProcessTask(AbstractCreateProcessAbilityTask task)
        {
            RunProcessTask(task, NativeDataPacket);
        }
        
        public void RunProcessTask(AbstractCreateProcessAbilityTask task, AbilityDataPacket _data)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            
            ActivateProcess(_data, task, cts.Token).Forget();
        }
        
        public void RunProcessTasks(List<AbstractCreateProcessAbilityTask> tasks)
        {
            RunProcessTasks(tasks, NativeDataPacket);
        }

        public void RunProcessTasks(List<AbstractCreateProcessAbilityTask> tasks, AbilityDataPacket _data)
        {
            foreach (var task in tasks)
            {
                RunProcessTask(task, _data);
            }
        }

        private async UniTask ActivateProcess(AbilityDataPacket _data, AbstractCreateProcessAbilityTask task, CancellationToken token)
        {
            task.Prepare(_data);
            await task.Activate(_data, token);
            task.Clean(_data);
        }
        
        #endregion
        
        public ISource GetOwner()
        {
            return this;
        }
        
        public float GetRelativeLevel()
        {
            return Data.RelativeLevel;
        }
        public bool IsActive()
        {
            return true;
        }
    }
}
