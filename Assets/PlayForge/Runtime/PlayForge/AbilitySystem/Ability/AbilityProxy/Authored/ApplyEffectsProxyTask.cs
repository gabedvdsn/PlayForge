using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ApplyEffectsProxyTask : AbstractProxyTask
    {
        public List<GameplayEffect> Effects;
        public int BetweenApplicationDelayMilliseconds;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (!data.TryGetFirstTarget(out var target))
            {
                return;
            }
            
            foreach (GameplayEffect effect in Effects)
            {
                target.ApplyGameplayEffect(target.GenerateEffectSpec(data.Spec, effect));
                await UniTask.Delay(BetweenApplicationDelayMilliseconds, cancellationToken: token);
            }
        }
        public override bool IsCriticalSection => false;
    }
}
