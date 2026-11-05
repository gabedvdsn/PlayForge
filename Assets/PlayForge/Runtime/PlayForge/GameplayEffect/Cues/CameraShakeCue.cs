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
        [Min(0f)] public float Strength = 1f;

        [Tooltip("How long the shake lasts, in seconds")]
        [Min(0f)] public float Duration = 0.2f;

        [Tooltip("Shake on each tick too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => ForgeCues.Shaker?.Shake(Strength, Duration);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) ForgeCues.Shaker?.Shake(Strength, Duration);
        }
    }
}
