using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointProxyTask : AbstractProxyTask
    {
        public override bool IsCriticalSection => false;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.Spec.GetOwner().AsGAS();
            if (owner is null) return;
            
            await owner.CallBehaviour(DisjointCompositeBehaviour.Command, new DisjointCompositeBehaviour(), token);

        }
    }
}