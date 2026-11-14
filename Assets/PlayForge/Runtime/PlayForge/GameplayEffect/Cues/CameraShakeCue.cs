using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Shakes the camera as the effect lands, and on each tick where set, through the shaker the game gives
    /// <see cref="ForgeCues.Shaker"/> (FEAT-1).
    /// </summary>
    [Serializable]
    public class CameraShakeCue : AbstractCueHandler
    {
        [NumberMin(0f)] public ScalableFloat Strength = 1f;

        [Tooltip("How long the shake lasts, in seconds")]
        [NumberMin(0f)] public ScalableFloat Duration = 0.2f;

        [Tooltip("Shake on each tick too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => Shake(cue);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) Shake(cue);
        }

        // Its strength and duration, read for the effect as it lands on the cue's entity (FN-19)
        private void Shake(CueContext cue)
        {
            if (ForgeCues.Shaker == null) return;
            var context = NumberContext.Of(in cue);
            ForgeCues.Shaker.Shake(Strength?.Evaluate(context) ?? 0f, Duration?.Evaluate(context) ?? 0f);
        }
    }
}
