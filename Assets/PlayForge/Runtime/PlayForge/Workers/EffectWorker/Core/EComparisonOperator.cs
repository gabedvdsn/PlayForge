namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How one number compares with another: less than, less or equal, equal, not equal, greater or equal, or greater
    /// than. A tag query compares the tag's count with its number by it, and conditions, validation rules, waits,
    /// auto-targeting and the threshold effect worker compare their numbers by it.
    /// </summary>
    public enum EComparisonOperator
    {
        LessThan,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        GreaterThan
    }
}
