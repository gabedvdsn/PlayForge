namespace FarEmerald.PlayForge
{
    public static partial class Tags
    {
        // Retention keys
        public static Tag IgnoreRetention => IGNORE;
        public static Tag DefaultRetention => DEFAULT;
        
        // Impact keys
        public static Tag AllowImpact => ALWAYS_ALLOW;
        public static Tag DisallowImpact => NEVER_ALLOW;
    }
}
