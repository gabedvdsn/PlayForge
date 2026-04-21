using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor.PackageManager;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    public class MonoWrapperProcess : AbstractProcessWrapper
    {
        /// <summary>
        /// The original Object process (prefab OR instance)
        /// </summary>
        private AbstractMonoProcess StoredMono;
        
        /// <summary>
        /// The active Object process
        /// </summary>
        private AbstractMonoProcess activeMono;
        
        public MonoWrapperProcess(AbstractMonoProcess storedMono, ProcessDataPacket data, IGameplayProcessHandler handler) : base(data, handler)
        {
            StoredMono = storedMono;
            ParticipateInSiblingCascade = storedMono.ParticipateInSiblingCascade;
            getStatus = () => StoredMono.ProcessLifecycle == EProcessLifecycle.Synchronous ? EProcessStatus.Synchronous : EProcessStatus.Asynchronous;
        }

        /// <summary>
        /// Regulates the instantiation of the MonoBehaviour process
        /// </summary>
        public override void InitializeWrapper()
        {
            if (!StoredMono) return;

            var inst = Handler?.GetInstantiator(StoredMono);
            if (StoredMono.UseHandlerInstantiator && inst is not null)
            {
                activeMono = inst.Create(StoredMono, Data, StoredMono.gameObject.scene.IsValid());
            }
            else if (StoredMono.Instantiator is not null)
            {
                activeMono = StoredMono.Instantiator.Create(StoredMono, Data, StoredMono.gameObject.scene.IsValid());
            }
            if (activeMono && activeMono.gameObject.scene.IsValid()) return;
            
            activeMono = StoredMono.gameObject.scene.IsValid() ? StoredMono : Object.Instantiate(StoredMono);
            activeMono.name = activeMono.name.Replace("(Clone)", "");
        }
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            activeMono.SendProcessData(Data, relay);
            activeMono.WhenInitialize();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            activeMono.WhenUpdate();
        }

        public override void WhenFixedUpdate(ProcessRelay relay)
        {
            activeMono.WhenFixedUpdate();
        }
        public override void WhenLateUpdate(ProcessRelay relay)
        {
            activeMono.WhenLateUpdate();
        }

        public override void WhenWait(ProcessRelay relay)
        {
            activeMono.WhenWait();
        }
        public override bool TryHandlePause(ProcessRelay relay)
        {
            return activeMono.TryHandlePause();
        }
        public override bool TryHandleResume(ProcessRelay relay)
        {
            return activeMono.TryHandleResume();
        }

        /// <summary>
        /// Terminates the behaviour of the process, then Destroys the process object if it still exists.
        /// </summary>
        /// <param name="relay"></param>
        public override void WhenTerminate(ProcessRelay relay)
        {
            WhenTerminateSafe(relay);
            
            if (activeMono.Instantiator is not null) activeMono.Instantiator.CleanProcess(activeMono);
            else
            {
                try { Object.Destroy(activeMono.gameObject); }
                catch { }
            }
        }
        /// <summary>
        /// Terminates the behaviour of the process, without Destroying the process object
        /// </summary>
        /// <param name="relay"></param>
        public override void WhenTerminateSafe(ProcessRelay relay)
        {
            activeMono.WhenTerminate();
        }

        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await activeMono.RunProcess(token);
        }

        public override bool TryGetProcess<T>(out T process)
        {
            if (activeMono is T cast)
            {
                process = cast;
                return true;
            }

            process = default;
            return false;
        }
        public override bool IsInitialized()
        {
            return activeMono && activeMono.IsInitialized;
        }

        public override string ProcessName => getProcessName ?? (activeMono ? activeMono.name : $"[<Is Destroyed>] - {(StoredMono ? StoredMono.name : "<Unknown>")}");
        public override EProcessStepPriorityMethod PriorityMethod => activeMono.PriorityMethod;

        public override int StepPriority => activeMono.ProcessStepPriority;
        public override EProcessStepTiming StepTiming => activeMono.ProcessTiming;
        public override EProcessLifecycle Lifecycle => activeMono.ProcessLifecycle;
        public override string ToString() => ProcessName;
    }
}
