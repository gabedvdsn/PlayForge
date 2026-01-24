using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AvoidRequireTagGroup
    {
        [Tooltip("Optional name for this requirement group (helps identify when importing)")]
        public string Name;
        
        [Header("Template Link")]
        [Tooltip("Optional: Link to a shared RequirementTemplate. When linked, this group can sync its values from the template.")]
        public RequirementTemplate SourceTemplate;
        
        [Tooltip("When enabled, this group's values will be kept in sync with the linked template.\n" +
                 "Disable to make local modifications while keeping the template reference.")]
        public bool SyncWithTemplate;
        
        [Header("Tags")]
        public List<AvoidRequireContainer> AvoidTags;
        public List<AvoidRequireContainer> RequireTags;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if this group has a custom name set.
        /// </summary>
        public bool HasName => !string.IsNullOrEmpty(Name);
        
        /// <summary>
        /// Gets the display name - returns Name if set, empty string otherwise.
        /// </summary>
        public string GetDisplayName() => HasName ? Name : "";
        
        /// <summary>
        /// Returns true if this group is linked to a template.
        /// </summary>
        public bool IsLinkedToTemplate => SourceTemplate != null;
        
        /// <summary>
        /// Returns true if this group should sync from its template.
        /// </summary>
        public bool ShouldSyncFromTemplate => IsLinkedToTemplate && SyncWithTemplate;
        
        /// <summary>
        /// Returns true if this group has any requirements defined.
        /// </summary>
        public bool HasAnyRequirements => (RequireTags?.Count ?? 0) > 0 || (AvoidTags?.Count ?? 0) > 0;
        
        /// <summary>
        /// Gets the total count of all requirements.
        /// </summary>
        public int TotalCount => (RequireTags?.Count ?? 0) + (AvoidTags?.Count ?? 0);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Syncs values from the linked template if SyncWithTemplate is enabled.
        /// </summary>
        public void TrySyncFromTemplate()
        {
            if (ShouldSyncFromTemplate)
            {
                SourceTemplate.CopyTo(this);
            }
        }
        
        /// <summary>
        /// Links this group to a template and optionally syncs immediately.
        /// </summary>
        public void LinkToTemplate(RequirementTemplate template, bool syncImmediately = true)
        {
            SourceTemplate = template;
            SyncWithTemplate = true;
            if (syncImmediately && template != null)
            {
                template.CopyTo(this);
            }
        }
        
        /// <summary>
        /// Unlinks from the current template, keeping current values.
        /// </summary>
        public void UnlinkFromTemplate()
        {
            SourceTemplate = null;
            SyncWithTemplate = false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Validation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool Validate(List<Tag> appliedTags)
        {
            // ALL RequireTags must validate AND NONE of the AvoidTags can validate
            bool allRequirementsMet = RequireTags?.All(arc => arc.Validate(appliedTags)) ?? true;
            bool noAvoidancesMet = AvoidTags?.All(arc => !arc.Validate(appliedTags)) ?? true;
    
            return allRequirementsMet && noAvoidancesMet;
        }
    }

    [Serializable]
    public class AvoidRequireContainer
    {
        [ForgeTagContext(ForgeContext.Required, ForgeContext.Granted, ForgeContext.AssetIdentifier)]
        public Tag Tag;
        public EComparisonOperator Operator = EComparisonOperator.GreaterThan;
        public int Magnitude = 0;

        public bool Validate(List<Tag> appliedTags)
        {
            int count = appliedTags?.Count(t => t == Tag) ?? 0;
    
            return Operator switch
            {
                EComparisonOperator.GreaterThan => count > Magnitude,
                EComparisonOperator.LessThan => count < Magnitude,
                EComparisonOperator.GreaterOrEqual => count >= Magnitude,
                EComparisonOperator.LessOrEqual => count <= Magnitude,
                EComparisonOperator.Equal => count == Magnitude,
                EComparisonOperator.NotEqual => count != Magnitude,
                _ => throw new ArgumentOutOfRangeException(nameof(Operator), Operator, null)
            };
        }
    }
}