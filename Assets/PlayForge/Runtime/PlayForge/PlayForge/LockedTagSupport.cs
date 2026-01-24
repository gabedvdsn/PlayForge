using System;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Locked Tag Provider Interface
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Implement this interface on classes containing Tag lists to specify which tags are locked.
    /// Locked tags cannot be edited or removed from the list in the inspector.
    /// </summary>
    /// <example>
    /// public class MyEffect : GameplayEffect, ILockedTagProvider
    /// {
    ///     public List&lt;Tag&gt; GrantedTags;
    ///     
    ///     public bool IsTagLocked(string fieldPath, int index)
    ///     {
    ///         // Lock first 2 tags in GrantedTags
    ///         if (fieldPath == "GrantedTags" &amp;&amp; index &lt; 2)
    ///             return true;
    ///         return false;
    ///     }
    ///     
    ///     public string GetLockedReason(string fieldPath, int index)
    ///     {
    ///         return "System-defined tag";
    ///     }
    /// }
    /// </example>
    public interface ILockedTagProvider
    {
        /// <summary>
        /// Determines if a tag at the specified field path and index is locked.
        /// </summary>
        /// <param name="fieldPath">The property path of the list field (e.g., "GrantedTags")</param>
        /// <param name="index">The index of the tag in the list</param>
        /// <returns>True if the tag should be locked (non-editable, non-removable)</returns>
        bool IsTagLocked(string fieldPath, int index);
        
        /// <summary>
        /// Gets the reason why a tag is locked (shown as tooltip).
        /// Default implementation returns "This tag is locked".
        /// </summary>
        string GetLockedReason(string fieldPath, int index) => "This tag is locked";
    }
    
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
