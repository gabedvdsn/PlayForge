using System;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// Floats a word over the entity an effect lands on, as "Slowed" or "Shielded", in a color of its own (AC-2): through
    /// the entity's Floating Numbers where it has them, so the word floats where its numbers do, else from its place.
    /// </summary>
    [Serializable]
    public class FloatingWordCue : AbstractCueHandler
    {
        [Tooltip("The word it floats; with none, the effect's name")]
        public string Text;

        [Tooltip("The word's color")]
        public Color Color = new(1f, 0.85f, 0.35f);

        [Tooltip("How large it floats, against the entity's numbers")]
        [Min(0.1f)] public float Scale = 1f;

        [Tooltip("Floats each tick of a lasting effect too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => Float(cue);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) Float(cue);
        }

        private void Float(CueContext cue)
        {
            var word = string.IsNullOrEmpty(Text) ? cue.Spec?.Base?.GetName() : Text;
            if (string.IsNullOrEmpty(word) || cue.Transform == null) return;
            var numbers = cue.Transform.GetComponentInChildren<FloatingNumbers>();
            if (numbers != null) numbers.FloatWord(word, Color, Scale);
            else FloatingTextLayer.Current.Float(cue.Position + Vector3.up * 2f, word, Color, FloatStyle.Default.Scaled(Scale));
        }
    }

    /// <summary>
    /// Flashes the entity an effect lands on in the cue's own color, as a freeze's blue (AC-2): through its Hit Flash, or
    /// one the cue adds where it has none, which flashes only as cues ask.
    /// </summary>
    [Serializable]
    public class FlashCue : AbstractCueHandler
    {
        [Tooltip("The flash's color")]
        public Color Color = new(0.45f, 0.75f, 1f);

        [Tooltip("How long the flash takes to fade, in seconds")]
        [Min(0.01f)] public float Duration = 0.25f;

        [Tooltip("Flashes each tick of a lasting effect too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => Flash(cue);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) Flash(cue);
        }

        private void Flash(CueContext cue)
        {
            if (cue.Transform == null) return;
            var flash = cue.Transform.GetComponentInChildren<HitFlash>();
            if (flash == null)
            {
                flash = cue.Transform.gameObject.AddComponent<HitFlash>();
                flash.OnLosses = false;
            }
            flash.Flash(Color, Duration);
        }
    }
}
