using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The system calling the composite behaviour
    /// </summary>
    public interface ICompositeBehaviourCaller : ICompositeBehaviourUser
    {
        public UniTask CallBehaviour(Tag cmd, AbstractCompositeBehaviour cb, CancellationToken token);
        
        public UniTask CallBehaviour(Tag cmd, AbstractCompositeBehaviour cb, ICompositeBehaviourUser user, CancellationToken token);

        public UniTask CallBehaviour(Tag cmd, AbstractCompositeBehaviour cb, ICompositeBehaviourUser[] users, CancellationToken token);
    }
}
