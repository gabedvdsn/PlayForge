using UnityEngine;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>A clock whose frames each take a fixed step, as <see cref="ForgeTestWorld.Clock"/> sets one (TW-26).</summary>
    public sealed class SteppingClock : IForgeClock
    {
        public SteppingClock(float step) => Step = step;

        /// <summary>The seconds each frame takes.</summary>
        public float Step { get; set; }

        public float DeltaTime => Step;
        public float UnscaledDeltaTime => Step;
        public float FixedDeltaTime => Step;
        public float Time => UnityEngine.Time.time;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
        public int FrameCount => UnityEngine.Time.frameCount;
    }

    /// <summary>
    /// Rolls that come out as the test writes them, one after another and round again, and how many were drawn, as
    /// <see cref="ForgeTestWorld.Rolls"/> sets them (TW-26). A chance below a roll misses, so 0 always lands and 0.99
    /// almost never does.
    /// </summary>
    public sealed class ScriptedRandom : IForgeRandom
    {
        private readonly float[] rolls;

        public ScriptedRandom(params float[] rolls) => this.rolls = rolls is { Length: > 0 } ? rolls : new[] { 0f };

        /// <summary>The rolls drawn so far.</summary>
        public int Drawn { get; private set; }

        public float Value => rolls[Drawn++ % rolls.Length];
        public Vector2 InsideUnitCircle => Vector2.zero;
        public Vector3 InsideUnitSphere => Vector3.zero;
    }
}
