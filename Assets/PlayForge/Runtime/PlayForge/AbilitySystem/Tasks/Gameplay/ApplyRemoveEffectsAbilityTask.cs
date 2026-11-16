using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [ForgeMenu("Effects", Description = "Applies its effects as its stage starts and takes them off as the stage ends.")]
    public class ApplyRemoveEffectsAbilityTask : AbstractAbilityTask
    {
        public List<GameplayEffect> Effects;

        [Tooltip("Lands on the targets in the project's resolve order; off, in the order the targeting gave them")]
        public bool UseResolveOrder = true;

        public override void Prepare(AbilityDataPacket data)
        {
            // On each target, as an area ability has several. Only the first took them. In the project's resolve
            // order, or the targeting's where it's off
            foreach (var target in ForgeResolveOrder.TargetsOf(data, UseResolveOrder))
            {
                // With the magnitudes the caller set on the activation's data
                foreach (GameplayEffect effect in Effects)
                {
                    var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
                    spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
                    target.ApplyCarrier(spec);
                }
            }
        }

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override void Clean(AbilityDataPacket data)
        {
            // Off each target it put them on
            int targets = data.Count(Tags.TARGET_REAL);
            for (int i = 0; i < targets; i++)
            {
                if (!data.TryGetAt(Tags.TARGET_REAL, i, out ITarget target) || target is null) continue;

                var gas = target.ToGAS();
                foreach (GameplayEffect effect in Effects) gas.RemoveGameplayEffect(effect);
            }
        }
        public override bool IsCriticalSection => false;
    }
}
