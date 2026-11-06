using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A projectile: it flies at its target at its speed, the ProjectileSpeed role, and applies its effects as it lands.
    /// Its speed is an attribute it carries, or its origin's (FEAT-24).
    /// </summary>
    public class ProjectileMonoProcess : AbstractTargetedMonoProcess
    {
        protected override async UniTask RunTargetedProcess(CancellationToken token)
        {
            while (Vector3.Distance(transform.position, targeting.position) > .1f)
            {
                token.ThrowIfCancellationRequested();
                
                transform.position = Vector3.MoveTowards(transform.position, targeting.position, GetProjectileSpeed() * ForgeTime.DeltaTimeFor(ProcessRelay?.Handler));
                
                await ForgeTick.NextFrame(token);
            }
        }
        
        // Its own speed, as it carries it, or else its origin's (FEAT-24). It read its origin's alone, so a projectile
        // could only fly as fast as its caster's attribute said, and failed where the caster had none
        protected virtual float GetProjectileSpeed()
        {
            if (TryGetRole(Tags.PROJECTILE_SPEED, out var speed)) return speed;
            ReportStatus(Tags.FAILED_WHILE_ACTIVE);
            return 0f;
        }
    }
}
