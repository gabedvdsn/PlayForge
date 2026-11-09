using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The magnitude field's old name: it is <see cref="ScalableFloat"/> now (FEAT-29). It stays a version, so code that
    /// names it still compiles, and goes in the next.
    /// </summary>
    [Serializable]
    [Obsolete("ScalerMagnitudeOperation is ScalableFloat now (FEAT-29); this name goes in the next version.")]
    public class ScalerMagnitudeOperation : ScalableFloat
    {
    }

    /// <summary>
    /// The whole-number magnitude field's old name: it is <see cref="ScalableInt"/> now (FEAT-29). It stays a version, so
    /// code that names it still compiles, and goes in the next.
    /// </summary>
    [Serializable]
    [Obsolete("ScalerIntegerMagnitudeOperation is ScalableInt now (FEAT-29); this name goes in the next version.")]
    public class ScalerIntegerMagnitudeOperation : ScalableInt
    {
    }
}
