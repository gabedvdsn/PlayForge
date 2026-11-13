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
    public class GameRoot : GameplayAbilitySystem, IManagerial
    {
        [Header("Game Root")]
        
        public static GameRoot Instance;

        // Statics outlive play mode when domain reload is off, so the last session's destroyed GameRoot was still
        // Instance when the next began (PC-26)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearInstance() => Instance = null;

        [Tooltip("Keeps GameRoot through scene loads. It's the handler for every Register overload that isn't given one.")]
        [SerializeField] private new bool DontDestroyOnLoad = true;

        [Tooltip("The Forge Project this game runs, whose settings PlayForge reads as it runs: its attributes' roles, and its " +
                 "default crowd control (TW-29)")]
        [SerializeField] private ForgeProject project;

        /// <summary>The Forge Project this game runs, whose settings PlayForge reads where code uses none (TW-29).</summary>
        public ForgeProject Project
        {
            get => project;
            set => project = value;
        }
        
        // Useful for backend systems like observers, audio, etc...
        [SerializeReference]
        public List<AbstractCreateProcessAbilityTask> CreateProcessTasks = new();
        private AbilityDataPacket NativeDataPacket;
        
        public void Bootstrap()
        {
            // Unity's null, so a destroyed GameRoot gives way. `is not null` kept it (PC-26)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                // On its way out, so Instance stays where it is. It took Instance anyway (PC-23)
                return;
            }

            Instance = this;
            if (DontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }
        
        public void DeferredInit()
        {
            NativeDataPacket = AbilityDataPacket.GenerateFrom
            (
                IEffectOrigin.GenerateSourceDerivation(this),
                AbilitySystem.CreateActivationRequest(-1),
                false, default
            );
            
            NativeDataPacket.AddPayload(Tags.TRANSFORM, transform);
            
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
    }
}
