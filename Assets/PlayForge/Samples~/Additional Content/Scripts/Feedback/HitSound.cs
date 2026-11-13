using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// Plays a sound as an entity takes a hit (AC-2): one of its clips at random, its pitch varied a little, on each loss
    /// to its attribute, at most once in a short interval, so a burst of hits in one frame plays once. With no clip, a
    /// short click made in code. A Sound cue on an effect plays that effect's own.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayForge/Additional Content/Hit Sound")]
    public class HitSound : EntityFeedback
    {
        [Tooltip("The clips it plays, one at random each hit; with none, a short click made in code")]
        public List<AudioClip> Clips = new();

        [Tooltip("How loud")]
        [Range(0f, 1f)] public float Volume = 0.6f;

        [Tooltip("How far each play's pitch may stray from 1, either way")]
        [Range(0f, 0.5f)] public float PitchVariation = 0.08f;

        [Tooltip("The least time between two plays, in seconds")]
        [Min(0f)] public float MinInterval = 0.05f;

        [Tooltip("Plays on gains too")]
        public bool OnGains;

        [Tooltip("How much the sound comes from the entity's place: 0 heard alike everywhere, 1 wholly from there")]
        [Range(0f, 1f)] public float SpatialBlend = 0.8f;

        private static AudioClip click;
        private AudioSource source;
        private float last = float.NegativeInfinity;

        /// <summary>How many times it has played.</summary>
        public int Played { get; private set; }

        /// <summary>The click it plays where it has no clip: a short burst of noise that dies away.</summary>
        public static AudioClip Click
        {
            get
            {
                if (click != null) return click;
                const int rate = 44100;
                int length = rate / 16;
                var samples = new float[length];
                var random = new System.Random(7);
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)length;
                    samples[i] = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 9f) * 0.8f;
                }
                click = AudioClip.Create("Hit Click", length, 1, rate, false);
                click.SetData(samples, 0);
                return click;
            }
        }

        protected override void OnChange(ImpactData impact, float change)
        {
            if (change > 0f && !OnGains) return;
            if (Time.time - last < MinInterval) return;
            last = Time.time;
            Play();
        }

        /// <summary>Plays one of its clips now.</summary>
        public void Play()
        {
            var clip = Clips.Count > 0 ? Clips[Random.Range(0, Clips.Count)] : Click;
            if (clip == null) return;
            if (source == null)
            {
                source = GetComponent<AudioSource>();
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                }
            }
            source.spatialBlend = SpatialBlend;
            source.pitch = 1f + Random.Range(-PitchVariation, PitchVariation);
            source.PlayOneShot(clip, Volume);
            Played++;
        }
    }
}
