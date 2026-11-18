using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A process as ProcessControl runs it: the packet and relay it's handed as it registers (<c>SendProcessData</c>),
    /// its hooks as it initializes, waits, terminates and is destroyed, its steps at its timing (<c>WhenUpdate</c>,
    /// <c>WhenLateUpdate</c>, <c>WhenFixedUpdate</c>), the run ProcessControl awaits (<c>RunProcess</c>), and whether
    /// it keeps its run through a pause (<c>TryHandlePause</c>, <c>TryHandleResume</c>). It's a process handler too, so
    /// the processes registered under it end with it. <c>AbstractMonoProcess</c> and <c>AbstractRuntimeProcess</c>
    /// implement it, and a process of your own derives from one of them.
    /// </summary>
    public interface IGameplayProcess : IGameplayProcessHandler
    {
        public void SendProcessData(ProcessDataPacket data, ProcessRelay relay);
        public ProcessRelay ProcessRelay { get; }
        
        /// <summary>
        /// Called via ProcessControl after the process is Created
        /// </summary>
        public void WhenInitialize();
        
        public void WhenUpdate();
        public void WhenLateUpdate();
        public void WhenFixedUpdate();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Waiting
        /// </summary>
        public void WhenWait();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Terminated
        /// </summary>
        public void WhenTerminate();

        /// <summary>
        /// Called via ProcessControl when a Terminate is requested while the process runs. It applies once the
        /// run ends, so a run that is waiting on nothing else has to end here.
        /// </summary>
        public void WhenTerminateQueued();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Running
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public UniTask RunProcess(CancellationToken token);

        /// <summary>
        /// Called via ProcessControl when a running process is paused. Return true to keep the run and hold it
        /// yourself, which it does by watching its relay. Return false, as the default does, and the run is
        /// cancelled and a new one starts on resume.
        /// </summary>
        public bool TryHandlePause();

        /// <summary>
        /// Called via ProcessControl on resume, for a run that was kept. Return true once it has picked up where
        /// it stopped. Return false and it's cancelled, and a new one starts.
        /// </summary>
        public bool TryHandleResume();
        
        public void WhenDestroy();
        
        public bool BehaviourIsApplicable(AbstractProxyTaskBehaviour behaviour);
        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token);
        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] user, CancellationToken token);

    }
}
