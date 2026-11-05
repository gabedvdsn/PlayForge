using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ApplyRemoveEffectsAbilityTask : AbstractAbilityTask
    {
        public List<GameplayEffect> Effects;

        public override void Prepare(AbilityDataPacket data)
        {
            if (!data.TryGetFirst(Tags.TARGET_REAL, out ITarget target))
            {
                return;
            }
            
            // With the magnitudes the caller set on the activation's data (FEAT-2)
            foreach (GameplayEffect effect in Effects)
            {
                var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
                spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
                target.ApplyCarrier(spec);
            }
        }

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override void Clean(AbilityDataPacket data)
        {
            if (!data.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out ITarget target))
            {
                return;
            }
            var gas = target.ToGAS();
            foreach (GameplayEffect effect in Effects) gas.RemoveGameplayEffect(effect);
        }
        public override bool IsCriticalSection => false;
    }
}
