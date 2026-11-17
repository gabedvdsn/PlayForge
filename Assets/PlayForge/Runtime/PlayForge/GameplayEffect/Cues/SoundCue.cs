using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Plays a sound where its placement says, as its moment comes, as an effect lands, and on an effect's each tick
    /// where set: on the one the cue is on, its source, an ability's target or each of its targets, or at the cue's
    /// point; attached, it follows them as it plays.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Plays a sound where its placement says as its moment comes, and on an effect's each tick where set.")]
    public class SoundCue : AbstractCueHandler
    {
        public AudioClip Clip;

        [Tooltip("Where it plays, on whom; attached, it follows them as it plays")]
        public CuePlacement Placement = new();

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
            if (Clip == null) return;
            float volume = Mathf.Clamp01(Volume?.Evaluate(NumberContext.Of(in cue)) ?? 1f);
            foreach (var place in (Placement ??= new CuePlacement()).Resolve(cue, lasting: false))
            {
                if (place.Parent == null)
                {
                    AudioSource.PlayClipAtPoint(Clip, place.Position, volume);
                    continue;
                }
                // Attached: a source of its own, following them, gone as the clip ends
                var made = new GameObject($"{Clip.name} (Cue)");
                made.transform.SetParent(place.Parent, false);
                made.transform.SetPositionAndRotation(place.Position, place.Rotation);
                var source = made.AddComponent<AudioSource>();
                source.clip = Clip;
                source.volume = volume;
                source.spatialBlend = 1f;
                source.Play();
                Object.Destroy(made, Clip.length);
            }
        }
    }
}
