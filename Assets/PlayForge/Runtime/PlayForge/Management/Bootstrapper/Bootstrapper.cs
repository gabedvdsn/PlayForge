using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Brings PlayForge up as a scene starts: ProcessControl, then the GameRoot, which runs as the Game Root entity of the
    /// Forge Project the Bootstrapper names. It names prefabs for both, which code makes in the
    /// PlayForge folder's Bootstrap folder as the first Forge Project is made; without them it makes each itself, so a
    /// scene needs nothing but a Bootstrapper. PlayForge ships no prefab. A scene of processes and task sequences alone,
    /// with no entities, can take the <see cref="ProcessBootstrapper"/> instead.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        [Tooltip("The Forge Project this scene plays: the GameRoot runs as its Game Root entity and reads its settings. " +
                 "None, the GameRoot prefab's own")]
        public ForgeProject Project;

        [Tooltip("The ProcessControl it brings up. None, one it makes")]
        public ProcessControl ProcessControlPrefab;

        [Tooltip("The GameRoot it brings up. None, one it makes, stepping in Update and LateUpdate")]
        public GameRoot GameRootPrefab;

        [Tooltip("Or in Start() when false")]
        public bool BootstrapOnAwake = true;

        public List<AttributeSet> RegisterSets;
        
        private void Awake()
        {
            if (BootstrapOnAwake) Bootstrap();
        }

        private void Start()
        {
            if (!BootstrapOnAwake) Bootstrap();
        }

        #region Readable Definition
        
        public virtual string GetName()
        {
            return "Bootstrapper";
        }
        public virtual string GetDescription()
        {
            return "Bootstraps the PlayForge framework during runtime.";
        }
        public virtual Texture2D GetPrimaryIcon()
        {
            return null;
        }
        
        #endregion
        
        private void Bootstrap()
        {
            TagRegistry.Initialize();

            // None where it's made in code and given none
            foreach (var attrSet in RegisterSets ?? new List<AttributeSet>())
            {
                foreach (var attr in attrSet.GetUnique())
                {
                    AttributeRegistry.Add(attr);
                }
            }
            
            // Bootstrap ProcessControl, where none is up, Ready until GameRoot is in, as a StartState that refuses
            // processes refused GameRoot too
            var made = ProcessControl.BringUp(ProcessControlPrefab, deferStartState: true);
            
            // Bootstrap GameRoot, by Unity's null as above
            if (!GameRoot.Instance)
            {
                // The project it plays, which the GameRoot takes on as it starts, and runs as its Game Root entity
                var data = ProcessDataPacket.Default();
                if (Project) data.AddPayload(Tags.FORGE_PROJECT, Project);
                GameObject madeTemplates = null;
                var template = GameRootPrefab ? GameRootPrefab : MadeGameRoot(out madeTemplates);

                // Refused, there's no relay to read, and a bare clone would never initialize or step, so it's
                // reported. Reading the relay threw, and the Bootstrapper never finished
                bool registered = ProcessControl.Register(template).With(data).Run(out var relay);

                // The registration copied the template it made, which goes
                if (madeTemplates) Destroy(madeTemplates);
                if (registered
                    && relay.TryGetProcess<GameRoot>(out var gameRoot))
                {
                    gameRoot.Bootstrap();
                }
                else
                {
                    ForgeLog.Error(ForgeLog.ESystem.Processes, $"{GetName()}: couldn't register GameRoot (ProcessControl is {ProcessControl.Instance.State}), " +
                                   "so the Register overloads that default to it won't work.", this);
                }
            }

            // The StartState the ProcessControl made here was set to, now that GameRoot is in
            if (made) made.EnterStartState();

            BootstrapOverrides();

            ProcessControl.Instance.DeferredInit();
            if (GameRoot.Instance) GameRoot.Instance.DeferredInit();
            
            Destroy(gameObject);
        }
        
        protected virtual void BootstrapOverrides()
        {
            // Any further bootstrap initialization here
        }

        // A GameRoot where it names no prefab, as the Creator Lab's: a template, off, which the registration copies, stepping
        // in Update and LateUpdate, as a GAS must for its end of frame; its project's Game Root entity it takes as it
        // starts
        private static GameRoot MadeGameRoot(out GameObject templates)
        {
            templates = new GameObject("PlayForge Templates") { hideFlags = HideFlags.HideAndDontSave };
            templates.SetActive(false);
            var made = new GameObject("GameRoot");
            made.transform.SetParent(templates.transform, false);
            var gameRoot = made.AddComponent<GameRoot>();
            gameRoot.ProcessLifecycle = EProcessLifecycle.Synchronous;
            gameRoot.ProcessTiming = EProcessStepTiming.UpdateAndLate;
            return gameRoot;
        }
    }
}
