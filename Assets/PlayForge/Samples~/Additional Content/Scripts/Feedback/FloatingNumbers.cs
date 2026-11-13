using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// Floats a number over an entity for each change to its attribute (AC-2): a loss in one color, a gain in another,
    /// and a critical hit larger and in a color of its own, marked. A hit is critical where its breakdown carries one of
    /// its critical notes, as a Value Pipeline's Chance stage leaves its Note when it comes up. A Floating Word cue floats
    /// its word where the entity's numbers float.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayForge/Additional Content/Floating Numbers")]
    public class FloatingNumbers : EntityFeedback
    {
        [Tooltip("Where its numbers float from, from the entity's place")]
        public Vector3 Offset = new(0f, 2f, 0f);

        [Tooltip("How its numbers look and move")]
        public FloatStyle Style = FloatStyle.Default;

        [Tooltip("A loss's color")]
        public Color LossColor = new(1f, 0.35f, 0.3f);

        [Tooltip("A gain's color")]
        public Color GainColor = new(0.45f, 1f, 0.5f);

        [Tooltip("Floats gains too, as heals")]
        public bool ShowGains = true;

        [Tooltip("The breakdown notes that mark a hit critical, as a Chance stage leaves its Note where it comes up")]
        public List<string> CriticalNotes = new() { "critical" };

        [Tooltip("A critical hit's color")]
        public Color CriticalColor = new(1f, 0.8f, 0.2f);

        [Tooltip("How much larger a critical hit floats")]
        [Min(1f)] public float CriticalScale = 1.5f;

        [Tooltip("What follows a critical hit's number")]
        public string CriticalMark = "!";

        [Tooltip("The decimal places it shows")]
        [Range(0, 3)] public int Decimals;

        [Tooltip("The layer its numbers float on; with none, the scene's")]
        public FloatingTextLayer Layer;

        /// <summary>How many numbers it has floated.</summary>
        public int Floated { get; private set; }

        /// <summary>How many of them were critical.</summary>
        public int Criticals { get; private set; }

        private FloatingTextLayer On => Layer != null ? Layer : FloatingTextLayer.Current;

        // Where its numbers start: the entity's place, offset
        private Vector3 From => transform.position + Offset;

        /// <summary>Floats a word where its numbers float, larger or smaller by a scale.</summary>
        public void FloatWord(string word, Color color, float scale = 1f)
        {
            if (!string.IsNullOrEmpty(word)) On.Float(From, word, color, Style.Scaled(scale));
        }

        /// <summary>Whether a hit was critical: its breakdown carries one of the critical notes.</summary>
        public bool IsCritical(ImpactData impact)
        {
            if (CriticalNotes == null || CriticalNotes.Count == 0) return false;
            if (impact.SourcedModifier.Carrier is not GameplayEffectSpec spec || spec.Breakdown is not { } breakdown) return false;
            foreach (var step in breakdown.Steps)
            {
                if (string.IsNullOrEmpty(step.Note)) continue;
                foreach (var note in CriticalNotes)
                    if (string.Equals(note, step.Note, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        protected override void OnChange(ImpactData impact, float change)
        {
            if (change > 0f && !ShowGains) return;
            bool critical = change < 0f && IsCritical(impact);
            string text = Number(Mathf.Abs(change));
            if (critical)
            {
                text += CriticalMark;
                Criticals++;
            }
            var color = critical ? CriticalColor : change < 0f ? LossColor : GainColor;
            On.Float(From, text, color, critical ? Style.Scaled(CriticalScale) : Style);
            Floated++;
        }

        // Rounded to its decimals; a change too small for them shows what it is, as 0.3 where none are kept
        private string Number(float value)
        {
            string text = value.ToString(Decimals == 0 ? "0" : "0." + new string('0', Decimals), CultureInfo.InvariantCulture);
            return text.Trim('0', '.').Length == 0 ? value.ToString("0.##", CultureInfo.InvariantCulture) : text;
        }
    }
}
