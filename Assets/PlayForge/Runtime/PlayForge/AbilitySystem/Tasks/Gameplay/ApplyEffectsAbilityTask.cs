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

        [Tooltip("Lands on the targets in the project's resolve order; off, in the order the targeting gave them (TW-31)")]
        public bool UseResolveOrder = true;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // Each target, as an area ability has several. Only the first was struck (GAS-62)
            int targets = data.Count(Tags.TARGET_REAL);
            if (targets == 0) return;

            for (int e = 0; e < Effects.Count; e++)
            {
                var effect = Effects[e];

                // In the project's resolve order, read as each effect lands, or the targeting's where it's off (TW-31)
                var ordered = ForgeResolveOrder.TargetsOf(data, UseResolveOrder);
                for (int i = 0; i < ordered.Count; i++)
                {
                    var target = ordered[i];

                    // With the magnitudes the caller set on the activation's data (FEAT-2)
                    var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
                    spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
                    bool landed = target.ApplyCarrier(spec);
                    if (landed && RecordLandedAs.IsValid) data.SetPrimary(RecordLandedAs, effect);
                }
                // Between effects, where there's a delay: a delay of 0 waited a frame, so a second effect, as a heal's
                // lockout, landed a frame after the first; and the last waited for nothing to follow (GAS-74)
                if (DelayBetweenApplicationMs > 0 && e < Effects.Count - 1)
                    await SequenceTaskLibrary.Delay(DelayBetweenApplicationMs / 1000f, token);
            }
        }
        public override bool IsCriticalSection => false;
    }
}
