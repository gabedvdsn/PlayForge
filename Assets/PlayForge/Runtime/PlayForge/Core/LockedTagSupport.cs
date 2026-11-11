using System;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Locked Tag Attributes
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Attribute to mark a Tag list field as having locked items determined by count.
    /// Tags at indices less than LockedCount are locked.
    /// </summary>
    /// <example>
    /// [ForgeTagLockedCount(2, "Core effect tags")]
    /// public List&lt;Tag&gt; GrantedTags; // First 2 tags are locked
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class ForgeTagLockedCountAttribute : System.Attribute
    {
        public int LockedCount { get; }
        public string Reason { get; }
        
        public ForgeTagLockedCountAttribute(int lockedCount, string reason = "System-defined tag")
        {
            LockedCount = lockedCount;
            Reason = reason;
        }
    }
    
    /// <summary>
    /// Attribute to mark specific indices in a Tag list as locked.
    /// </summary>
    /// <example>
    /// [ForgeTagLockedIndices("Required tags", 0, 2, 4)]
    /// public List&lt;Tag&gt; RequiredTags; // Tags at index 0, 2, and 4 are locked
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class ForgeTagLockedIndicesAttribute : System.Attribute
    {
        public int[] LockedIndices { get; }
        public string Reason { get; }
        
        public ForgeTagLockedIndicesAttribute(string reason, params int[] lockedIndices)
        {
            LockedIndices = lockedIndices ?? Array.Empty<int>();
            Reason = reason;
        }
    }
}
