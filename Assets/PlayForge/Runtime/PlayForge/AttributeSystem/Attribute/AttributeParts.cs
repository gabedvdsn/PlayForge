namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The part of an attribute's value a number reads (TW-9): its current, its base, what's missing of it (base − current),
    /// or its share (current ÷ base). × Attribute, the cached attribute scaler and the Attribute condition read through it.
    /// </summary>
    public static class AttributeParts
    {
        /// <summary>The part of <paramref name="value"/> that <paramref name="what"/> names; a share of a base of 0 is 0.</summary>
        public static float Read(in AttributeValue value, EAttributeTargetBinary what) => what switch
        {
            EAttributeTargetBinary.Base => value.BaseValue,
            EAttributeTargetBinary.Missing => value.BaseValue - value.CurrentValue,
            EAttributeTargetBinary.Share => value.BaseValue != 0f ? value.CurrentValue / value.BaseValue : 0f,
            _ => value.CurrentValue,
        };

        /// <summary>What a line says after the attribute's name for the part: nothing for its current, " (base)", " (missing)" or " (share)".</summary>
        public static string Suffix(EAttributeTargetBinary what) => what switch
        {
            EAttributeTargetBinary.Base => " (base)",
            EAttributeTargetBinary.Missing => " (missing)",
            EAttributeTargetBinary.Share => " (share)",
            _ => "",
        };
    }
}
