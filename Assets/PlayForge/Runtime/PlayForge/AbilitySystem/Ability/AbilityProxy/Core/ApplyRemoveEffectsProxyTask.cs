using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ApplyRemoveEffectsProxyTask : AbstractProxyTask
    {
        public List<GameplayEffect> Effects;

        public override void Prepare(AbilityDataPacket data)
        {
            if (!data.TryGetFirst(Tags.PAYLOAD_TARGET, out ITarget target))
            {
                return;
            }

            var gas = target.AsGAS();
            
            foreach (GameplayEffect effect in Effects) target.ApplyGameplayEffect(gas.GenerateEffectSpec(data.Spec, effect));
        }

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override void Clean(AbilityDataPacket data)
        {
            if (!data.TryGet(Tags.PAYLOAD_TARGET, EProxyDataValueTarget.Primary, out ITarget target))
            {
                return;
            }
            var gas = target.AsGAS();
            foreach (GameplayEffect effect in Effects) gas.RemoveGameplayEffect(effect);
        }
        public override bool IsCriticalSection => false;
    }
}
