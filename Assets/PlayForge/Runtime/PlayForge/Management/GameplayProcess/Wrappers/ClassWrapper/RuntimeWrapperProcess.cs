using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class RuntimeWrapperProcess : AbstractProcessWrapper
    {
        private AbstractRuntimeProcess Process;
        
        public RuntimeWrapperProcess(AbstractRuntimeProcess process, ProcessDataPacket data, IGameplayProcessHandler handler) : base(data, handler)
        {
            Process = process;
            getStatus = () => Process.Lifecycle == EProcessLifecycle.Synchronous ? EProcessStatus.Synchronous : EProcessStatus.Asynchronous;
        }
        
        public override void ConfigureWrapper()
        {
            
        }
        public override void WhenInitialize(ProcessRelay relay)
        {
            Process.SendProcessData(Data, relay);
            Process.WhenInitialize();
        }
        
        public override void WhenUpdate(ProcessRelay relay)
        {
            Process.WhenUpdate();
        }

        public override void WhenFixedUpdate(ProcessRelay relay)
        {
            Process.WhenFixedUpdate();
        }
        
        public override void WhenLateUpdate(ProcessRelay relay)
        {
            Process.WhenLateUpdate();
        }

        public override void WhenWait(ProcessRelay relay)
        {
            Process.WhenWait();
        }
        public override bool TryHandlePause(ProcessRelay relay)
        {
            return Process.TryHandlePause();
        }
        public override bool TryHandleResume(ProcessRelay relay)
        {
            return Process.TryHandleResume();
        }
        public override void WhenTerminate(ProcessRelay relay)
        {
            Process.WhenTerminate();
        }
        public override void WhenTerminateSafe(ProcessRelay relay)
        {
            // Doesn't do anything
        }
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await Process.RunProcess(token);
        }
        public override bool TryGetProcess<T>(out T process)
        {
            if (Process is T cast)
            {
                process = cast;
                return true;
            }

            process = default;
            return false;
        }
        public override bool IsInitialized()
        {
            return Process.IsInitialized;
        }
        public override string ProcessName => getProcessName ?? Process.ProcessName;
        public override EProcessStepPriorityMethod PriorityMethod => Process.PriorityMethod;
        public override int StepPriority => Process.StepPriority;
        public override EProcessStepTiming StepTiming => Process.StepTiming;
        public override EProcessLifecycle Lifecycle => Process.Lifecycle;
    }
}
