using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity's own time: the game's, as <see cref="ForgeTime"/> reads it, scaled by the entity's
    /// <see cref="TimeScale"/>, which a slow or a haste sets (FEAT-7, Decision 30). The entity's effects tick by it, its
    /// cooldowns among them, and so do its abilities' sequences and waits. Every entity ran on Unity's one clock, so
    /// nothing could slow one entity and not the rest. A scale of 0 holds the entity's time still; it isn't a pause,
    /// which ProcessControl's is, and processes keep their own bookkeeping on the game's clock.
    /// </summary>
    public sealed class EntityClock
    {
        private float timeScale = 1f;

        /// <summary>How fast the entity's time runs against the game's: 1 as the game's, 0.5 at half, 0 held still.</summary>
        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Max(0f, value);
        }

        /// <summary>The frame's step of the entity's time.</summary>
        public float DeltaTime => ForgeTime.DeltaTime * timeScale;

        /// <summary>The fixed step of the entity's time.</summary>
        public float FixedDeltaTime => ForgeTime.FixedDeltaTime * timeScale;

        /// <summary>The entity's time since it started: each of its frames' steps, added up.</summary>
        public float Time { get; private set; }

        // Once a frame, as the entity steps
        internal void Step() => Time += DeltaTime;
    }
}
