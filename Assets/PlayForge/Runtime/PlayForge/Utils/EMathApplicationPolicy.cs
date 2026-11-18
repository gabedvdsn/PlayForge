namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How a value is used as a factor: <c>AsIs</c> as it is, <c>OnePlus</c> as 1 + x, and <c>OneMinus</c> as 1 − x, so
    /// a 30% bonus multiplies by 1.3 and a 30% reduction by 0.7. An Attenuate scaler behaviour's Policy turns its value
    /// so, and a Math attribute worker's Operation Policy the attribute it changes by.
    /// </summary>
    public enum EMathApplicationPolicy
    {
        AsIs,
        OneMinus,
        OnePlus
    }
}
