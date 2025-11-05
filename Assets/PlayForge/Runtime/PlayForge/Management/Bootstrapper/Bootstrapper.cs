using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class Bootstrapper : MonoBehaviour, IGameplayProcessHandler
    {
        public static Bootstrapper Instance;
        
        public FrameworkIndex Framework;
        
        public ProcessControl ProcessControlPrefab;
        public GameRoot GameRootPrefab;

        public Dictionary<int, ProcessRelay> Relays;
        
        private void Awake()
        {
            if (Framework is null) throw new NullReferenceException("[ FESGAS ] Bootstrapping failed: Framework cannot be null.");
            
            Bootstrap();
        }
        
        public void Bootstrap()
        {
            if (Instance is not null && Instance != this) Destroy(gameObject);
            Instance = this;
            
            DontDestroyOnLoad(gameObject);
            
            // Bootstrap RuntimeStore
            var fp = ForgeStores.LoadFramework(Framework.FrameworkKey, false) ?? FrameworkProject.EmptyDefault();
            RuntimeStore.SetFramework(fp);
            
            // Bootstrap ProcessControl
            if (ProcessControl.Instance is null)
            {
                ProcessControl control;
                if (ProcessControlPrefab) control = Instantiate(ProcessControlPrefab, Vector3.zero, Quaternion.identity);
                else
                {
                    var obj = new GameObject("ProcessControl");
                    control = obj.AddComponent<ProcessControl>();
                }
                
                control.Bootstrap();
            }

            // Bootstrap GameRoot
            if (GameRoot.Instance is null)
            {
                ProcessControl.Instance.Register(GameRootPrefab, AbilityDataPacket.GenerateNull(), out var packet);
                
                var status = packet.TryGetProcess<GameRoot>(out var root);
                if (!status) root = Instantiate(GameRootPrefab, Vector3.zero, Quaternion.identity);
                
                root.Bootstrap();
            }
            
            ProcessControl.Instance.DeferredInit();
            GameRoot.Instance.DeferredInit();

            Initialize();
        }

        private void Initialize()
        {
            // Any further bootstrap initialization here   
            // Bootstrap load framework
        }
        
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (Bootstrapper)handler == this;
        }

        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return true;
        }

        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            Relays[relay.CacheIndex] = relay;
        }
        public bool HandlerVoidProcess(int processIndex)
        {
            return Relays.Remove(processIndex);
        }
    }
}
