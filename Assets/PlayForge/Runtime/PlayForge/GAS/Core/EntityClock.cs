using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity's own time: the game's, as <see cref="ForgeTime"/> reads it, scaled by the entity's
    /// <see cref="TimeScale"/>: the scale code sets, as a hit-stop's, times the one its Time Scale attribute gives, as a
    /// slow's or a haste's, so the two compose. The entity's effects tick by it, its
    /// cooldowns among them, and so do its abilities' sequences and waits. Every entity ran on Unity's one clock, so
    /// nothing could slow one entity and not the rest. A scale of 0 holds the entity's time still; it isn't a pause,
    /// which ProcessControl's is, and processes keep their own bookkeeping on the game's clock. It's a clock as any
    /// other is, so a sequence that isn't an ability's runs on an entity's time with its packet's
    /// <see cref="ProcessDataPacket.Clock"/> set to it.
    /// </summary>
    public sealed class EntityClock : IForgeClock
    {
        private float ownScale = 1f;
        private float attributeScale = 1f;

        /// <summary>
        /// How fast the entity's time runs against the game's: 1 as the game's, 0.5 at half, 0 held still. It reads the
        /// scale code sets, <see cref="OwnScale"/>, times the one its Time Scale attribute gives,
        /// <see cref="AttributeScale"/>; setting it sets code's, so a hit-stop of 0 holds under a slow.
        /// </summary>
        public float TimeScale
        {
            get => ownScale * attributeScale;
            set => OwnScale = value;
        }

        /// <summary>The scale code sets, as a hit-stop's 0: its part of <see cref="TimeScale"/>.</summary>
        public float OwnScale
        {
            get => ownScale;
            set => ownScale = Mathf.Max(0f, value);
        }

        /// <summary>
        /// The scale the entity's Time Scale attribute gives, read as each of its steps begins: its part of
        /// <see cref="TimeScale"/>, 1 where its identity names no attribute.
        /// </summary>
        public float AttributeScale
        {
            get => attributeScale;
            internal set => attributeScale = Mathf.Max(0f, value);
        }

        /// <summary>The frame's step of the entity's time.</summary>
        public float DeltaTime => ForgeTime.DeltaTime * TimeScale;

        /// <summary>The fixed step of the entity's time.</summary>
        public float FixedDeltaTime => ForgeTime.FixedDeltaTime * TimeScale;

        /// <summary>The entity's time since it started: each of its frames' steps, added up.</summary>
        public float Time { get; private set; }

        /// <summary>The frame's step unscaled, the game's: neither the game's time scale nor the entity's is in it.</summary>
        public float UnscaledDeltaTime => ForgeTime.UnscaledDeltaTime;

        /// <summary>The game's time since it started, unscaled.</summary>
        public float UnscaledTime => ForgeTime.UnscaledTime;

        /// <summary>The game's frame.</summary>
        public int FrameCount => ForgeTime.FrameCount;

        // Once a frame, as the entity steps
        internal void Step() => Time += DeltaTime;
    }
}
