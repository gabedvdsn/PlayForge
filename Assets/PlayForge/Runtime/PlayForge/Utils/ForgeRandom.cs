using System;

namespace FarEmerald.PlayForge
{
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
