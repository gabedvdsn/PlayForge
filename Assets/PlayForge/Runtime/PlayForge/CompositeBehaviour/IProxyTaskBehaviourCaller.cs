using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The system calling the composite behaviour
    /// </summary>
    public interface IProxyTaskBehaviourCaller : IProxyTaskBehaviourUser
    {
        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token);

        public UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] users, CancellationToken token);
    }
}
