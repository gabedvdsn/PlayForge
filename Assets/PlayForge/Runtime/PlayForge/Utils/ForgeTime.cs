using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A source of time, which PlayForge's runtime reads instead of Unity's <c>Time</c> (FEAT-13, Decision 30). The
    /// default reads Unity's; a test, a replay or a server puts one of its own in with <see cref="ForgeTime.Use"/>.
    /// </summary>
    public interface IForgeClock
    {
        /// <summary>The frame's time step, scaled, as <c>Time.deltaTime</c>.</summary>
        float DeltaTime { get; }

        /// <summary>The frame's time step, unscaled, as <c>Time.unscaledDeltaTime</c>.</summary>
        float UnscaledDeltaTime { get; }

        /// <summary>The fixed step, as <c>Time.fixedDeltaTime</c>.</summary>
        float FixedDeltaTime { get; }

        /// <summary>The time since the game started, scaled, as <c>Time.time</c>.</summary>
        float Time { get; }

        /// <summary>The time since the game started, unscaled, as <c>Time.unscaledTime</c>.</summary>
        float UnscaledTime { get; }

        /// <summary>The frame's number, as <c>Time.frameCount</c>.</summary>
        int FrameCount { get; }
    }

    /// <summary>Unity's time, the clock PlayForge reads unless another is put in.</summary>
    public sealed class UnityClock : IForgeClock
    {
        public static readonly UnityClock Instance = new();

        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public float Time => UnityEngine.Time.time;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
        public int FrameCount => UnityEngine.Time.frameCount;
    }

    /// <summary>
    /// The clock PlayForge's runtime reads: Unity's, unless <see cref="Use"/> puts another in. The runtime read Unity's
    /// <c>Time</c> in a dozen files, so nothing could step it to a clock of its own, as a deterministic replay, a server
    /// or a test needs (FEAT-13). A source-scanning test keeps <c>Time</c> out of the runtime.
    /// </summary>
    public static class ForgeTime
    {
        private static IForgeClock clock = UnityClock.Instance;

        /// <summary>The clock read now.</summary>
        public static IForgeClock Clock => clock;

        /// <summary>Puts a clock in, or Unity's back with null. Returns the one it replaced, to put back.</summary>
        public static IForgeClock Use(IForgeClock next)
        {
            var previous = clock;
            clock = next ?? UnityClock.Instance;
            return previous;
        }

        public static float DeltaTime => clock.DeltaTime;

        /// <summary>
        /// The frame's step of an owner's time: an entity's own clock's, its time scale times the game's, or the game's
        /// for anything else (FEAT-7).
        /// </summary>
        public static float DeltaTimeFor(object owner) =>
            owner is IGameplayAbilitySystem gas && gas.GetClock() is { } entity ? entity.DeltaTime : clock.DeltaTime;

        /// <summary>The frame's step of a packet's owner's time: an ability's packet's source's clock, or the game's.</summary>
        public static float DeltaTimeFor(ProcessDataPacket data) =>
            DeltaTimeFor(data is AbilityDataPacket ability ? ability.EffectOrigin?.GetSource() : (object)null);
        public static float UnscaledDeltaTime => clock.UnscaledDeltaTime;
        public static float FixedDeltaTime => clock.FixedDeltaTime;
        public static float Time => clock.Time;
        public static float UnscaledTime => clock.UnscaledTime;
        public static int FrameCount => clock.FrameCount;
    }
}
