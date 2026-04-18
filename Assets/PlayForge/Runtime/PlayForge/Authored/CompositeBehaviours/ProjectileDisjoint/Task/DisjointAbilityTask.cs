using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointAbilityTask : AbstractAbilityTask
    {
        public override bool IsCriticalSection => false;
        
        /// <summary>
        /// Owner disjoints incoming targeted projectiles!
        /// Disjoint behaviour is called
        /// </summary>
        /// <param name="data"></param>
        /// <param name="token"></param>
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetOwner().ToGAS();
            if (owner is null) return;

            if (!owner.Data.TryGetAll(Tags.TARGETED_INTENT, out DataValue<IProxyTaskBehaviourUser> targetingUsers)) return;
            await owner.ApplyBehaviour(new DisjointProxyTaskBehaviour(), targetingUsers.ToArray(), token);
        }
    }
}