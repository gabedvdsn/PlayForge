using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public interface IGameplayProcess : IGameplayProcessHandler
    {
        public void SendProcessData(ProcessDataPacket data, ProcessRelay relay);
        public ProcessRelay ProcessRelay { get; }
        public void WhenInitialize();
        public void WhenUpdate();
        public void WhenLateUpdate();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Waiting
        /// </summary>
        public void WhenWait();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Terminated
        /// </summary>
        public void WhenTerminate();
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Running
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public UniTask RunProcess(CancellationToken token);
        public bool TryHandleResume();
        public void WhenDestroy();
        public bool BehaviourIsApplicable(AbstractProxyTaskBehaviour behaviour);
        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token);
        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] user, CancellationToken token);

    }
}
