using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A source of time, which PlayForge's runtime reads instead of Unity's <c>Time</c>. The
    /// default reads Unity's; a test, a replay or a server puts one of its own in with <see cref="ForgeTime.Use"/>. A
    /// sequence or a packet can name one of its own too (<see cref="ProcessDataPacket.Clock"/>,
    /// <see cref="TaskSequenceBuilder.WithClock"/>), as a menu's runs on <see cref="UnscaledClock"/>.
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

        /// <summary>
        /// Whether a sequence on this clock waits for the game's ticks where ProcessControl's tick rate is set: true for a
        /// clock of the game's time. The ticks run on that time and stop with it, so a clock that runs while it stands, as
        /// <see cref="UnscaledClock"/> does, has its sequences wait for Unity's frames instead.
        /// </summary>
        bool FollowsTicks => true;
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
    /// The game's time without its time scale, as the clock in force reads it: a sequence on it runs while the game is
    /// slowed or stopped, as a pause menu's or a hit-stop's own effects do. Its sequences wait for Unity's frames where
    /// the game's ticks would stand with the game's time.
    /// </summary>
    public sealed class UnscaledClock : IForgeClock
    {
        public static readonly UnscaledClock Instance = new();

        public float DeltaTime => ForgeTime.UnscaledDeltaTime;
        public float UnscaledDeltaTime => ForgeTime.UnscaledDeltaTime;
        public float FixedDeltaTime => ForgeTime.FixedDeltaTime;
        public float Time => ForgeTime.UnscaledTime;
        public float UnscaledTime => ForgeTime.UnscaledTime;
        public int FrameCount => ForgeTime.FrameCount;
        public bool FollowsTicks => false;
    }

    /// <summary>
    /// A time scale of its own over another clock, its source: a sequence on it runs at its scale, as a finisher's slow
    /// motion or a flourish at double speed, and the sequences that share one slow, speed up or stop together as it's
    /// set. Its source is the clock given, or, with none, the clock in force: the game's, or a world's or a tick's while
    /// one steps.
    /// </summary>
    public sealed class ScaledClock : IForgeClock
    {
        private IForgeClock source;
        private float scale;

        // Its time at its last change of scale or source, and its source's then, from which it counts on. It starts
        // counting as its time is first read, so making one reads no clock, as a component's field initializer mustn't
        private float since, sinceSource;
        private bool counting;

        public ScaledClock(float scale = 1f, IForgeClock source = null)
        {
            this.source = source;
            this.scale = Math.Max(0f, scale);
        }

        /// <summary>The clock it scales, or null for the clock in force.</summary>
        public IForgeClock Source
        {
            get => source;
            set
            {
                Rebase();
                source = value;
                sinceSource = SourceNow.Time;
            }
        }

        /// <summary>How fast it runs against its source: 1 as that one, 0.5 at half, 0 stopped.</summary>
        public float Scale
        {
            get => scale;
            set
            {
                Rebase();
                scale = Math.Max(0f, value);
            }
        }

        private IForgeClock SourceNow => source ?? ForgeTime.Clock;

        public float DeltaTime => SourceNow.DeltaTime * scale;
        public float UnscaledDeltaTime => SourceNow.UnscaledDeltaTime;
        public float FixedDeltaTime => SourceNow.FixedDeltaTime * scale;

        /// <summary>
        /// The time it has counted since its time was first read: its source's, at each scale it had. With no source given,
        /// it counts whichever clock is in force as it's read, so give it one to count by where a world of its own steps.
        /// </summary>
        public float Time
        {
            get
            {
                if (!counting)
                {
                    counting = true;
                    since = 0f;
                    sinceSource = SourceNow.Time;
                }
                return since + (SourceNow.Time - sinceSource) * scale;
            }
        }

        public float UnscaledTime => SourceNow.UnscaledTime;
        public int FrameCount => SourceNow.FrameCount;
        public bool FollowsTicks => SourceNow.FollowsTicks;

        // Its time so far becomes where it counts on from, before its scale or source changes
        private void Rebase()
        {
            since = Time;
            sinceSource = SourceNow.Time;
        }
    }

    /// <summary>
    /// The clock PlayForge's runtime reads: Unity's, unless <see cref="Use"/> puts another in. The runtime read Unity's
    /// <c>Time</c> in a dozen files, so nothing could step it to a clock of its own, as a deterministic replay, a server
    /// or a test needs. A source-scanning test keeps <c>Time</c> out of the runtime.
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
        /// The clock an owner's time runs by: an entity's own, its time scale times the game's, or the game's for anything
        /// else. Read it as it's needed: the game's is whichever clock is in force then.
        /// </summary>
        public static IForgeClock ClockOf(object owner) =>
            owner is IGameplayAbilitySystem gas && gas.GetClock() is { } entity ? entity : clock;

        /// <summary>
        /// The clock the work done with a packet runs by: the one it names (<see cref="ProcessDataPacket.Clock"/>), or its
        /// owner's: an ability's caster's, or the game's.
        /// </summary>
        public static IForgeClock ClockFor(ProcessDataPacket data) =>
            data?.Clock ?? ClockOf(data is AbilityDataPacket ability ? ability.EffectOrigin?.GetSource() : null);

        /// <summary>
        /// The frame's step of an owner's time: an entity's own clock's, its time scale times the game's, or the game's
        /// for anything else.
        /// </summary>
        public static float DeltaTimeFor(object owner) => ClockOf(owner).DeltaTime;

        /// <summary>The frame's step of the time a packet's work runs by, as <see cref="ClockFor"/> finds it.</summary>
        public static float DeltaTimeFor(ProcessDataPacket data) => ClockFor(data).DeltaTime;

        public static float UnscaledDeltaTime => clock.UnscaledDeltaTime;
        public static float FixedDeltaTime => clock.FixedDeltaTime;
        public static float Time => clock.Time;
        public static float UnscaledTime => clock.UnscaledTime;
        public static int FrameCount => clock.FrameCount;
    }
}
