using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointAbilityTask : AbstractAbilityTask
    {
        public override bool IsCriticalSection => false;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.Spec.GetOwner().AsGAS();
            if (owner is null) return;
            
            await owner.CallBehaviour(DisjointProxyTaskBehaviour.Command, new DisjointProxyTaskBehaviour(), token);
        }
    }
}