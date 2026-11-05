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

        /// <summary>
        /// As an effect lands on the target, the activation's data takes this tag, so a later stage can run on a hit and
        /// another on a miss. None records nothing (TS-7).
        /// </summary>
        [Tooltip("As an effect lands, the activation's data takes this tag, for a later stage's condition")]
        public Tag RecordLandedAs;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // Each target, as an area ability has several. Only the first was struck (GAS-62)
            int targets = data.Count(Tags.TARGET_REAL);
            if (targets == 0) return;

            foreach (GameplayEffect effect in Effects)
            {
                for (int i = 0; i < targets; i++)
                {
                    if (!data.TryGetAt(Tags.TARGET_REAL, i, out ITarget target) || target is null) continue;

                    // With the magnitudes the caller set on the activation's data (FEAT-2)
                    var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
                    spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
                    bool landed = target.ApplyCarrier(spec);
                    if (landed && RecordLandedAs.IsValid) data.SetPrimary(RecordLandedAs, effect);
                }
                await SequenceTaskLibrary.Delay(DelayBetweenApplicationMs / 1000f, token);
            }
        }
        public override bool IsCriticalSection => false;
    }
}
