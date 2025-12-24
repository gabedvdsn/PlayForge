using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class ApplyEffectsAbilityTask : AbstractAbilityTask
    {
        public List<GameplayEffect> Effects;
        public int DelayBetweenApplicationMs;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (!data.TryGetFirstTarget(out var target))
            {
                return;
            }
            
            foreach (GameplayEffect effect in Effects)
            {
                target.ApplyGameplayEffect(target.GenerateEffectSpec(data.Spec, effect));
                await UniTask.Delay(DelayBetweenApplicationMs, cancellationToken: token);
            }
        }
        public override bool IsCriticalSection => false;
    }
}
