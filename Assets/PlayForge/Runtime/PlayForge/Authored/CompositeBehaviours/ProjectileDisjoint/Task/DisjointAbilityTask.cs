using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointAbilityTask : AbstractAbilityTask
    {
        public override bool IsCriticalSection => false;
        
        /// <summary>
        /// The owner disjoints: every homing shot aimed at it that's marked disjointable loses it, as
        /// <see cref="DisjointProxyTaskBehaviour"/> describes. Skill shots aren't affected (GAS-36).
        /// </summary>
        /// <param name="data"></param>
        /// <param name="token"></param>
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetSource().ToGAS();
            if (owner is null) return;

            if (!owner.Data.TryGetAll(Tags.TARGETED_INTENT, out DataValue<IProxyTaskBehaviourUser> targetingUsers)) return;
            await owner.ApplyBehaviour(new DisjointProxyTaskBehaviour(), targetingUsers.ToArray(), token);
        }
    }
}