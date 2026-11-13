using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// Flashes an entity's renderers as it takes a hit (AC-2): each loss to its attribute tints them the flash's color,
    /// fading back over its duration, through a property block, so its materials stay shared. A Flash cue flashes it in
    /// the cue's own color.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayForge/Additional Content/Hit Flash")]
    public class HitFlash : EntityFeedback
    {
        [Tooltip("The flash's color")]
        public Color Color = Color.white;

        [Tooltip("How long a flash takes to fade, in seconds")]
        [Min(0.01f)] public float Duration = 0.12f;

        [Tooltip("Flashes on each loss to the attribute; off, only as Flash cues ask")]
        public bool OnLosses = true;

        [Tooltip("Flashes on each gain too, in the gain color")]
        public bool OnGains;

        [Tooltip("The flash's color on a gain")]
        public Color GainColor = new(0.45f, 1f, 0.5f);

        [Tooltip("The renderers it flashes; with none, every renderer on this object and under it")]
        public List<Renderer> Renderers = new();

        [Tooltip("The material color properties it tints, the first each material has: URP's, then the Built-in pipeline's")]
        public List<string> ColorProperties = new() { "_BaseColor", "_Color" };

        private sealed class Tinted
        {
            public Renderer Renderer;
            public int Property;
            public Color Rest;
            public MaterialPropertyBlock Before;
        }

        private readonly List<Tinted> tinted = new();
        private MaterialPropertyBlock block;
        private Color flash;
        private float length;
        private float left;

        /// <summary>Whether a flash is under way.</summary>
        public bool Flashing => left > 0f;

        /// <summary>How many flashes it has made.</summary>
        public int Flashes { get; private set; }

        /// <summary>The color of the flash under way, which its renderers fade from to their own; clear with none.</summary>
        public Color Current => Flashing ? flash : Color.clear;

        /// <summary>Flashes its renderers in a color, fading over a duration.</summary>
        public void Flash(Color color, float duration)
        {
            if (duration <= 0f) return;
            if (!Flashing) Capture();
            flash = color;
            length = duration;
            left = duration;
            Flashes++;
            Apply();
        }

        protected override void OnChange(ImpactData impact, float change)
        {
            if (change < 0f && OnLosses) Flash(Color, Duration);
            else if (change > 0f && OnGains) Flash(GainColor, Duration);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Flashing) Restore();
            left = 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (!Flashing) return;
            left -= Time.deltaTime;
            if (left <= 0f) Restore();
            else Apply();
        }

        // Each renderer's state as the flash starts, to fade to and put back
        private void Capture()
        {
            tinted.Clear();
            var renderers = Renderers.Count > 0 ? (IEnumerable<Renderer>)Renderers : GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var material = renderer.sharedMaterial;
                int property = -1;
                foreach (var name in ColorProperties)
                {
                    if (string.IsNullOrEmpty(name) || !material.HasProperty(name)) continue;
                    property = Shader.PropertyToID(name);
                    break;
                }
                if (property < 0) continue;
                var before = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(before);
                var rest = before.HasColor(property) ? before.GetColor(property) : material.GetColor(property);
                tinted.Add(new Tinted { Renderer = renderer, Property = property, Rest = rest, Before = before });
            }
        }

        private void Apply()
        {
            float through = 1f - Mathf.Clamp01(left / length);
            block ??= new MaterialPropertyBlock();
            foreach (var t in tinted)
            {
                if (t.Renderer == null) continue;
                t.Renderer.GetPropertyBlock(block);
                block.SetColor(t.Property, Color.Lerp(flash, t.Rest, through * through));
                t.Renderer.SetPropertyBlock(block);
            }
        }

        private void Restore()
        {
            foreach (var t in tinted)
                if (t.Renderer != null) t.Renderer.SetPropertyBlock(t.Before);
            tinted.Clear();
            left = 0f;
        }
    }
}
