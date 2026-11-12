using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Where an area search is measured from (TW-10).</summary>
    public enum EAreaCentre
    {
        /// <summary>The caster.</summary>
        Caster,

        /// <summary>The activation's target, as an AI hands it or a task before it in a Composite picks it; else the caster.</summary>
        Target,

        /// <summary>The activation's point, under Position; else the caster.</summary>
        Point,
    }

    /// <summary>
    /// An effect's area (TW-10, Decision 50): as the effect lands on its target, it lands on each the area takes near that
    /// target too, as Storm Hammer stuns everyone near the one it hits. Each is found by its position, on either hub, with
    /// no collider needed; an effect that lands by an area spreads no further.
    /// </summary>
    [Serializable]
    public class EffectArea
    {
        [Tooltip("How far from its target it reaches, read as it lands; 0, no area: it lands on its target alone")]
        public ScalableFloat Radius = 0f;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        [Tooltip("Whom it takes, from its caster's sides: enemies, allies, or both. A caster with no sides takes anyone")]
        public EAffinityFilter Takes = EAffinityFilter.Enemies;

        [Tooltip("Its caster too, where the area reaches it and takes its side")]
        public bool IncludeCaster;

        [Tooltip("At most this many besides its target, the nearest first; 0, every one it reaches")]
        [Min(0)] public int MaxTargets;

        /// <summary>Whether it reaches past its target at all: a radius that's more than a plain 0.</summary>
        public bool Reaches => Radius != null && (Radius.Magnitude > 0f || !AbstractScaler.IsNone(Radius.Scaler));
    }
}
