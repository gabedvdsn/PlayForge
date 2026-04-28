using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProjectileMonoProcess : AbstractTargetedMonoProcess
    {
        protected override async UniTask RunTargetedProcess(CancellationToken token)
        {
            while (Vector3.Distance(transform.position, targeting.position) > .1f)
            {
                token.ThrowIfCancellationRequested();
                
                transform.position = Vector3.MoveTowards(transform.position, targeting.position, GetProjectileSpeed() * Time.deltaTime);
                
                await UniTask.NextFrame(token);
            }
        }
        
        protected virtual float GetProjectileSpeed()
        {
            var speed = GetAttributeValue(Tags.PROJECTILE_SPEED);
            if (speed.RetainedValues is null) ReportStatus(Tags.FAILED_WHILE_ACTIVE);

            return speed.ActiveValue.CurrentValue;
        }
    }
}
