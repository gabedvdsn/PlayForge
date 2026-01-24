using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Tag requirements for effect application, ongoing, and removal phases.
    /// Each phase can have its own set of required and avoided tags.
    /// </summary>
    [Serializable]
    public class EffectTagRequirements
    {
        [Tooltip("Tags required/avoided to apply the effect")]
        public AvoidRequireTagGroup ApplicationRequirements;
        
        [Tooltip("Tags required/avoided to keep the effect active")]
        public AvoidRequireTagGroup OngoingRequirements;
        
        [Tooltip("Tags required/avoided to remove the effect")]
        public AvoidRequireTagGroup RemovalRequirements;

        // ═══════════════════════════════════════════════════════════════════════════
        // Validation Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool CheckApplicationRequirements(List<Tag> tags)
        {
            return ApplicationRequirements?.Validate(tags) ?? true;
        }

        public bool CheckOngoingRequirements(List<Tag> tags)
        {
            return OngoingRequirements?.Validate(tags) ?? true;
        }
        
        public bool CheckRemovalRequirements(List<Tag> tags)
        {
            return !(RemovalRequirements?.Validate(tags) ?? false);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if any phase has requirements defined.
        /// </summary>
        public bool HasAnyRequirements
        {
            get
            {
                return (ApplicationRequirements?.HasAnyRequirements ?? false) ||
                       (OngoingRequirements?.HasAnyRequirements ?? false) ||
                       (RemovalRequirements?.HasAnyRequirements ?? false);
            }
        }
        
        /// <summary>
        /// Gets the total count of all requirements across all phases.
        /// </summary>
        public int TotalRequirementCount
        {
            get
            {
                int count = 0;
                count += ApplicationRequirements?.TotalCount ?? 0;
                count += OngoingRequirements?.TotalCount ?? 0;
                count += RemovalRequirements?.TotalCount ?? 0;
                return count;
            }
        }
        
        /// <summary>
        /// Returns true if any phase is linked to a template.
        /// </summary>
        public bool HasLinkedTemplates
        {
            get
            {
                return (ApplicationRequirements?.IsLinkedToTemplate ?? false) ||
                       (OngoingRequirements?.IsLinkedToTemplate ?? false) ||
                       (RemovalRequirements?.IsLinkedToTemplate ?? false);
            }
        }
    }
}