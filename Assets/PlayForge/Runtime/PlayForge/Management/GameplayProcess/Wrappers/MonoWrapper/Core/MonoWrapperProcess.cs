using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class MonoWrapperProcess : AbstractProcessWrapper
    {
        private AbstractMonoProcess StoredMono;
        private ProcessDataPacket Data;
        
        private AbstractMonoProcess activeMono;

        public MonoWrapperProcess(AbstractMonoProcess storedMono, ProcessDataPacket data)
        {
            StoredMono = storedMono;
            Data = data;
        }

        /// <summary>
        /// Regulates the instantiation of the MonoBehaviour process
        /// </summary>
        public override void InitializeWrapper()
        {
            if (StoredMono)
            {
                if (StoredMono.Instantiator is not null)
                {
                    activeMono = StoredMono.Instantiator.Create(StoredMono, Data);
                    if (activeMono.gameObject.scene.isLoaded) return;
                }

                activeMono = StoredMono.gameObject.scene.isLoaded ? StoredMono : Object.Instantiate(StoredMono);
            }

            activeMono.name = activeMono.name.Replace("(Clone)", "");
        }
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            activeMono.SendProcessData(Data);
            activeMono.WhenInitialize(relay);
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            activeMono.WhenUpdate(relay);
        }
        
        public override void WhenWait(ProcessRelay relay)
        {
            activeMono.WhenWait(relay);
        }

        /// <summary>
        /// Terminates the behaviour of the process, then Destroys the process object if it still exists.
        /// </summary>
        /// <param name="relay"></param>
        public override void WhenTerminate(ProcessRelay relay)
        {
            WhenTerminateSafe(relay);
            if (activeMono.Instantiator is not null)
            {
                activeMono.Instantiator.CleanProcess(activeMono);
            }
            else Object.Destroy(activeMono.gameObject);
        }
        /// <summary>
        /// Terminates the behaviour of the process, without Destroying the process object
        /// </summary>
        /// <param name="relay"></param>
        public override void WhenTerminateSafe(ProcessRelay relay)
        {
            activeMono.WhenTerminate(relay);
            ProcessControl.Instance.RemoveMonoProcess(activeMono);
        }

        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await activeMono.RunProcess(relay, token);
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

        public override string ProcessName => activeMono ? activeMono.name : "[ ]";
        public override EProcessStepPriorityMethod PriorityMethod => activeMono.PriorityMethod;

        public override int StepPriority => activeMono.ProcessStepPriority;
        public override EProcessStepTiming StepTiming => activeMono.ProcessTiming;
        public override EProcessLifecycle Lifecycle => activeMono.ProcessLifecycle;
        public override string ToString() => ProcessName;
    }
}
