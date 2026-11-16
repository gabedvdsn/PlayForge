using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The game's own entity, a Mono hub every scene has one of, which ProcessControl hands any registration that names no
    /// handler. It runs as its Forge Project's Game Root entity: the project a scene's Bootstrapper plays, or else its own,
    /// or else the entity it names (EP-11). It keeps through scene loads.
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
        
        /// <summary>
        /// Takes on its Forge Project as it starts: the one its registration names, as the Bootstrapper names the one a
        /// scene plays, or else its own; and runs as that project's Game Root entity, where it has one (EP-11).
        /// </summary>
        public override void WhenInitialize()
        {
            if (regData != null && regData.TryGet(Tags.FORGE_PROJECT, EDataTarget.Primary, out ForgeProject playing) && playing) project = playing;
            if (project && project.GameRootEntity) EntityData = project.GameRootEntity;
            else if (!EntityData) EntityData = DefaultEntity;
            base.WhenInitialize();
        }

        // The Game Root entity a GameRoot runs as where neither its project nor it names one: the game's own, on the System
        // side, as the one PlayForge shipped was; made once, and again where it's gone. It holds no state, so a session
        // with domain reload off reuses the last one's
        private static EntityIdentity defaultEntity;

        /// <summary>The Game Root entity a GameRoot runs as where neither its Forge Project nor it names one (EP-11).</summary>
        internal static EntityIdentity DefaultEntity
        {
            get
            {
                if (defaultEntity) return defaultEntity;
                defaultEntity = ScriptableObject.CreateInstance<EntityIdentity>();
                defaultEntity.hideFlags = HideFlags.HideAndDontSave;
                defaultEntity.name = defaultEntity.Name = "Game Root";
                defaultEntity.Affiliation = new List<Tag> { Tags.SYSTEM };
                defaultEntity.AssetTag = Tag.Generate("Entity.GameRoot", "GameRoot");
                return defaultEntity;
            }
        }

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
