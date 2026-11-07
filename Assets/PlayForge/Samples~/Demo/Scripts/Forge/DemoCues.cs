using System;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// A cue that floats a word over the entity an effect lands on, as "Stunned" or "Marked", in a color of its own. The
    /// demo's own cue, beside PlayForge's prefab, sound and camera shake cues; the HUD draws it.
    /// </summary>
    [Serializable]
    public class FloatingTextCue : AbstractCueHandler
    {
        [Tooltip("The word it floats; with none, the effect's name")]
        public string Text;

        [Tooltip("The word's color")]
        public Color Color = Color.white;

        [Tooltip("Floats each tick of a lasting effect too")]
        public bool OnEachTick;

        public override void OnApplied(CueContext cue) => Float(cue);

        public override void OnTicked(CueContext cue)
        {
            if (OnEachTick) Float(cue);
        }

        private void Float(CueContext cue)
        {
            var hud = DemoHud.Current;
            if (hud == null || cue.Transform == null) return;
            var word = string.IsNullOrEmpty(Text) ? cue.Spec?.Base?.GetName() : Text;
            if (!string.IsNullOrEmpty(word)) hud.Float(cue.Position + Vector3.up * 2.4f, word, Color);
        }
    }
}
