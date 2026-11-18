using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A stage task that makes its caster disjoint, as in Dota: each homing shot aimed at it whose data marks it
    /// disjointable loses it, flies on to where the caster stood and lands on nothing. A skill shot, which aims at no
    /// one, isn't touched.
    /// </summary>
    [ForgeMenu("Process", Description = "Makes every disjointable shot homing on the caster lose it, as a dodge.")]
    public class DisjointAbilityTask : AbstractAbilityTask
    {
        public override bool IsCriticalSection => false;
        
        /// <summary>
        /// The owner disjoints: every homing shot aimed at it that's marked disjointable loses it, as
        /// <see cref="DisjointProxyTaskBehaviour"/> describes. Skill shots aren't affected.
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