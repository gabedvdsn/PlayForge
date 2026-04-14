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
        
        public void Bootstrap()
        {
            TagHierarchy.Initialize();
            
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
                ProcessControl.Register(GameRootPrefab, ProcessDataPacket.Default(), out var relay);
                
                // If failed to register the game root process, instantiate it manually
                var status = relay.TryGetProcess<GameRoot>(out var gameRoot);
                if (!status) gameRoot = Instantiate(GameRootPrefab, Vector3.zero, Quaternion.identity);
                
                gameRoot.Bootstrap();
            }
            
            BootstrapOverrides();
            
            ProcessControl.Instance.DeferredInit();
            GameRoot.Instance.DeferredInit();
            
            Destroy(gameObject);
        }
        
        protected virtual void BootstrapOverrides()
        {
            // Any further bootstrap initialization here   
        }
    }
}
