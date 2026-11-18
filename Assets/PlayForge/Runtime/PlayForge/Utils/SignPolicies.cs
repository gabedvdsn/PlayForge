namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The sign of a change or a value: <c>Positive</c>, <c>Negative</c>, <c>ZeroBiased</c>, 0 from values that cancel
    /// out, or <c>ZeroNeutral</c>, 0 from nothing at all. <c>ForgeHelper.SignPolicy</c> reads it from magnitudes and an
    /// attribute change's <c>SignPolicy</c> gives it; a context impact worker takes impacts of one sign (Impact Sign)
    /// and gives its own change one (Work Sign Policy).
    /// </summary>
    public enum ESignPolicy
    {
        Positive,
        Negative,
        ZeroBiased,
        ZeroNeutral
    }

    /// <summary>
    /// A sign policy with <c>Any</c>, which every sign matches: a focused attribute worker's Sign Policy, the sign of
    /// the changes it watches, is one.
    /// </summary>
    public enum ESignPolicyExtended
    {
        Any,
        Positive,
        Negative,
        ZeroBiased,
        ZeroNeutral
    }
}
