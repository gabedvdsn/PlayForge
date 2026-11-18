using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Shakes the camera as its moment comes, in whichever list it's in, through the shaker the game gives
    /// <see cref="ForgeCues.Shaker"/>.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Shakes the camera as its moment comes.")]
    public class CameraShakeCue : AbstractCueHandler
    {
        [NumberMin(0f)] public ScalableFloat Strength = 1f;

        [Tooltip("How long the shake lasts, in seconds")]
        [NumberMin(0f)] public ScalableFloat Duration = 0.2f;

        public override void OnApplied(CueContext cue) => Shake(cue);

        public override void OnTicked(CueContext cue) => Shake(cue);

        // Its strength and duration, read for what raised it as it plays on the cue's entity
        private void Shake(CueContext cue)
        {
            if (ForgeCues.Shaker == null) return;
            var context = NumberContext.Of(in cue);
            ForgeCues.Shaker.Shake(Strength?.Evaluate(context) ?? 0f, Duration?.Evaluate(context) ?? 0f);
        }
    }
}
