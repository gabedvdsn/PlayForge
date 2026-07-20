using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for tag requirement groups.
    /// Provides common functionality for requirement validation and property access.
    /// </summary>
    [Serializable]
    public abstract class AbstractTagRequirements
    {
        [Tooltip("Optional name for this requirement group (helps identify when importing)")]
        public string Name;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if this requirement group has a custom name set.
        /// </summary>
        public bool HasName => !string.IsNullOrEmpty(Name);
        
        /// <summary>
        /// Gets the display name - returns Name if set, empty string otherwise.
        /// </summary>
        public string GetDisplayName() => HasName ? Name : "";
        
        /// <summary>
        /// Returns true if any sub-group has requirements defined.
        /// </summary>
        public abstract bool HasAnyRequirements { get; }
        
        /// <summary>
        /// Gets the total count of all requirements across all sub-groups.
        /// </summary>
        public abstract int TotalRequirementCount { get; }
        
        /// <summary>
        /// Returns true if any sub-group is linked to a template.
        /// </summary>
        public abstract bool HasLinkedTemplates { get; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected static bool GroupHasRequirements(AvoidRequireTagGroup group)
        {
            return group?.HasAnyRequirements ?? false;
        }
        
        protected static int GetGroupCount(AvoidRequireTagGroup group)
        {
            return group?.TotalCount ?? 0;
        }
        
        protected static bool GroupIsLinked(AvoidRequireTagGroup group)
        {
            return group?.IsLinkedToTemplate ?? false;
        }
    }
}
