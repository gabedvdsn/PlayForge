using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProjectileMonoProcess : AbstractTargetedMonoProcess
    {
        protected override async UniTask RunTargetedProcess(ProcessRelay relay, CancellationToken token)
        {
            while (Vector3.Distance(transform.position, targetTransform.position) > .1f)
            {
                token.ThrowIfCancellationRequested();
                
                transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, GetProjectileSpeed() * Time.deltaTime);
                
                await UniTask.NextFrame(token);
            }
        }
        
        protected virtual float GetProjectileSpeed()
        {
            if (!AttributeLibrary.TryGetByName("projectile_speed", out var projSpeed)) return 10f;
            return Source.AttributeSystem.TryGetAttributeValue(projSpeed, out AttributeValue val) ? val.CurrentValue : 10f;
        }
    }
}
