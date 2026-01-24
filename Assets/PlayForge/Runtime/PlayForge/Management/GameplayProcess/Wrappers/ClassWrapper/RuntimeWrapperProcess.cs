using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class RuntimeWrapperProcess : AbstractProcessWrapper
    {
        private AbstractRuntimeProcess Process;
        private ProcessDataPacket Data;
        

        public RuntimeWrapperProcess(AbstractRuntimeProcess process, ProcessDataPacket data)
        {
            Process = process;
            Data = data;
        }

        public override void InitializeWrapper()
        {
            
        }
        public override void WhenInitialize(ProcessRelay relay)
        {
            Process.SendProcessData(Data);
            Process.WhenInitialize(relay);
        }
        
        public override void WhenUpdate(ProcessRelay relay)
        {
            Process.WhenUpdate(relay);
        }

        public override void WhenFixedUpdate(ProcessRelay relay)
        {
            Process.WhenFixedUpdate(relay);
        }
        
        public override void WhenLateUpdate(ProcessRelay relay)
        {
            Process.WhenLateUpdate(relay);
        }

        public override void WhenWait(ProcessRelay relay)
        {
            Process.WhenWait(relay);
        }
        public override void WhenTerminate(ProcessRelay relay)
        {
            Process.WhenTerminate(relay);
        }
        public override void WhenTerminateSafe(ProcessRelay relay)
        {
            // Doesn't do anything
        }
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await Process.RunProcess(relay, token);
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
        public override string ProcessName => Process.ProcessName;
        public override EProcessStepPriorityMethod PriorityMethod => Process.PriorityMethod;
        public override int StepPriority => Process.StepPriority;
        public override EProcessStepTiming StepTiming => Process.StepTiming;
        public override EProcessLifecycle Lifecycle => Process.Lifecycle;
    }
}
