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
            var owner = data.AbilitySpec.GetOwner();
            if (owner is ITarget selfTarget && TargetIsValid(selfTarget))
            {
                data.SetTargetingPacket(Tags.TARGET, selfTarget.GetTargetingPacket());
            }
            else
            {
                WhenTargetingInvalid(data);
            }

            return UniTask.CompletedTask;
        }
        
    }
}
