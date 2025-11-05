using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public interface ICompositeBehaviourUser
    {
        public UniTask RunCompositeBehaviour(Tag command, AbstractCompositeBehaviour cb, ICompositeBehaviourCaller caller, CancellationToken token);
    }
}
