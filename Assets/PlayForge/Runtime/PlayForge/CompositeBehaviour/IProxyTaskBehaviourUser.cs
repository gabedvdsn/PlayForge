using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public interface IProxyTaskBehaviourUser
    {
        public void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller);
        public UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token);
    }
}
