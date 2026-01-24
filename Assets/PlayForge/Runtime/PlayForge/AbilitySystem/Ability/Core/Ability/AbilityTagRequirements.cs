using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Tag requirements for ability activation.
    /// Specifies what tags the source (caster) and target must have or avoid.
    /// </summary>
    [Serializable]
    public class AbilityTagRequirements
    {
        [Tooltip("Optional name for this requirement group (helps identify when importing)")]
        public string Name;
        
        [Tooltip("Source requirements to use this ability")]
        public AvoidRequireTagGroup SourceRequirements;
        
        [Tooltip("Target requirements to use this ability (n/a for non-targeted abilities, e.g. ground cast)")]
        public AvoidRequireTagGroup TargetRequirements;
        
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
        /// Returns true if either sub-group has any requirements defined.
        /// </summary>
        public bool HasAnyRequirements
        {
            get
            {
                bool hasSource = SourceRequirements?.HasAnyRequirements ?? false;
                bool hasTarget = TargetRequirements?.HasAnyRequirements ?? false;
                return hasSource || hasTarget;
            }
        }
        
        /// <summary>
        /// Gets the total count of all requirements (source + target, require + avoid).
        /// </summary>
        public int TotalRequirementCount
        {
            get
            {
                int count = 0;
                count += SourceRequirements?.TotalCount ?? 0;
                count += TargetRequirements?.TotalCount ?? 0;
                return count;
            }
        }
        
        /// <summary>
        /// Returns true if either sub-group is linked to a template.
        /// </summary>
        public bool HasLinkedTemplates
        {
            get
            {
                return (SourceRequirements?.IsLinkedToTemplate ?? false) || 
                       (TargetRequirements?.IsLinkedToTemplate ?? false);
            }
        }
    }
}