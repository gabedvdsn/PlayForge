using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class Bootstrapper : MonoBehaviour
    {
        public ProcessControl ProcessControlPrefab;
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

            foreach (var attrSet in RegisterSets)
            {
                foreach (var attr in attrSet.GetUnique())
                {
                    AttributeRegistry.Add(attr);
                }
            }
            
            // Bootstrap ProcessControl
            if (ProcessControl.Instance is null)
            {
                ProcessControl control;
                if (ProcessControlPrefab) control = Instantiate(ProcessControlPrefab, Vector3.zero, Quaternion.identity);
                else
                {
                    var obj = new GameObject();
                    control = obj.AddComponent<ProcessControl>();
                }

                control.name = "ProcessControl";
                control.Bootstrap();
            }
            
            // Bootstrap GameRoot
            if (GameRoot.Instance is null)
            {
                // Refused, there's no relay to read, and a bare clone would never initialize or step, so it's
                // reported. Reading the relay threw, and the Bootstrapper never finished (PC-23)
                if (ProcessControl.Register(GameRootPrefab, ProcessDataPacket.Default(), out var relay)
                    && relay.TryGetProcess<GameRoot>(out var gameRoot))
                {
                    gameRoot.Bootstrap();
                }
                else
                {
                    ForgeLog.Error($"{GetName()}: couldn't register GameRoot (ProcessControl is {ProcessControl.Instance.State}), " +
                                   "so the Register overloads that default to it won't work.", this);
                }
            }

            BootstrapOverrides();

            ProcessControl.Instance.DeferredInit();
            if (GameRoot.Instance) GameRoot.Instance.DeferredInit();
            
            Destroy(gameObject);
        }
        
        protected virtual void BootstrapOverrides()
        {
            // Any further bootstrap initialization here   
        }
    }
}
