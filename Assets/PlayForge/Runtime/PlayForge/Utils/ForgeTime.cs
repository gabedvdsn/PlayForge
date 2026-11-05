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
        public static float UnscaledDeltaTime => clock.UnscaledDeltaTime;
        public static float FixedDeltaTime => clock.FixedDeltaTime;
        public static float Time => clock.Time;
        public static float UnscaledTime => clock.UnscaledTime;
        public static int FrameCount => clock.FrameCount;
    }

    /// <summary>
    /// A source of random numbers, which PlayForge's runtime reads instead of <c>UnityEngine.Random</c> (FEAT-13). The
    /// default reads Unity's, so <c>Random.InitState</c> still seeds it; <see cref="SeededRandom"/> is one of its own.
    /// </summary>
    public interface IForgeRandom
    {
        /// <summary>A number from 0 to 1, as <c>Random.value</c>.</summary>
        float Value { get; }

        /// <summary>A point in a circle of radius 1, as <c>Random.insideUnitCircle</c>.</summary>
        UnityEngine.Vector2 InsideUnitCircle { get; }

        /// <summary>A point in a sphere of radius 1, as <c>Random.insideUnitSphere</c>.</summary>
        UnityEngine.Vector3 InsideUnitSphere { get; }
    }

    /// <summary>Unity's random numbers, the source PlayForge reads unless another is put in.</summary>
    public sealed class UnityRandom : IForgeRandom
    {
        public static readonly UnityRandom Instance = new();

        public float Value => UnityEngine.Random.value;
        public UnityEngine.Vector2 InsideUnitCircle => UnityEngine.Random.insideUnitCircle;
        public UnityEngine.Vector3 InsideUnitSphere => UnityEngine.Random.insideUnitSphere;
    }

    /// <summary>
    /// Random numbers of their own, from a seed, which nothing else draws from: two runs from one seed draw the same
    /// numbers, whatever else in the game draws from Unity's.
    /// </summary>
    public sealed class SeededRandom : IForgeRandom
    {
        private readonly Random random;

        public SeededRandom(int seed) => random = new Random(seed);

        public float Value => (float)random.NextDouble();

        public UnityEngine.Vector2 InsideUnitCircle
        {
            get
            {
                // The square's points that fall in the circle, which are spread evenly over it
                while (true)
                {
                    var point = new UnityEngine.Vector2(Value * 2f - 1f, Value * 2f - 1f);
                    if (point.sqrMagnitude <= 1f) return point;
                }
            }
        }

        public UnityEngine.Vector3 InsideUnitSphere
        {
            get
            {
                while (true)
                {
                    var point = new UnityEngine.Vector3(Value * 2f - 1f, Value * 2f - 1f, Value * 2f - 1f);
                    if (point.sqrMagnitude <= 1f) return point;
                }
            }
        }
    }

    /// <summary>
    /// The random numbers PlayForge's runtime draws: Unity's, unless <see cref="Use"/> puts another source in, as
    /// <see cref="SeededRandom"/>. The runtime drew from <c>UnityEngine.Random</c> in five files, so a roll could be
    /// repeated only by seeding the whole game's random numbers (FEAT-13).
    /// </summary>
    public static class ForgeRandom
    {
        private static IForgeRandom source = UnityRandom.Instance;

        /// <summary>The source drawn from now.</summary>
        public static IForgeRandom Source => source;

        /// <summary>Puts a source in, or Unity's back with null. Returns the one it replaced, to put back.</summary>
        public static IForgeRandom Use(IForgeRandom next)
        {
            var previous = source;
            source = next ?? UnityRandom.Instance;
            return previous;
        }

        public static float Value => source.Value;
        public static UnityEngine.Vector2 InsideUnitCircle => source.InsideUnitCircle;
        public static UnityEngine.Vector3 InsideUnitSphere => source.InsideUnitSphere;
    }
}
