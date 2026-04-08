using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Auto-targeting task that explicitly targets the caster (self).
    /// Useful when UseImplicitTargeting is disabled but you still need self as a target,
    /// or when you want to explicitly validate the self-target against tag requirements.
    /// </summary>
    [Serializable]
    public class AutoTargetSelfTask : AbstractGasTargetingAbilityTask
    {
        public override string Description => "Automatically target self";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.Spec.GetOwner();
            if (owner is ITarget selfTarget && TargetIsValid(selfTarget))
            {
                data.SetPrimary(Tags.TARGET_REAL, selfTarget);
            }
            else
            {
                WhenTargetingInvalid(data);
            }

            return UniTask.CompletedTask;
        }

        protected override bool ConnectInputHandler(AbilityDataPacket data) => true;
        protected override void DisconnectInputHandler(AbilityDataPacket data) { }
    }
}
