using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Plays a sound at the effect's target as the effect lands, and on each tick where set (FEAT-1).</summary>
    [Serializable]
    public class SoundCue : AbstractCueHandler
    {
        public AudioClip Clip;

        [NumberRange(0f, 1f)] public ScalableFloat Volume = 1f;

        [Tooltip("Play on each tick too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => Play(cue);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) Play(cue);
        }

        private void Play(CueContext cue)
        {
            if (Clip != null) AudioSource.PlayClipAtPoint(Clip, cue.Position, Mathf.Clamp01(Volume?.Evaluate(NumberContext.Of(in cue)) ?? 1f));
        }
    }
}
