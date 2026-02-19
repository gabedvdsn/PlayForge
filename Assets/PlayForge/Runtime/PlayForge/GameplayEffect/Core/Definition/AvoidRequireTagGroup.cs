using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Represents a group of tag requirements with Avoid (must NOT have) and Require (must HAVE) conditions.
    /// Supports hierarchical tag matching via TagQuery.MatchMode on individual queries.
    /// </summary>
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
        
        [Tooltip("Tags that must NOT be present on the target")]
        public List<TagQuery> AvoidTags;
        
        [Tooltip("Tags that MUST be present on the target")]
        public List<TagQuery> RequireTags;
        
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
        public bool HasAnyRequirements => RequireCount > 0 || AvoidCount > 0;
        
        /// <summary>
        /// Gets the count of required tag queries.
        /// </summary>
        public int RequireCount => RequireTags?.Count ?? 0;
        
        /// <summary>
        /// Gets the count of avoid tag queries.
        /// </summary>
        public int AvoidCount => AvoidTags?.Count ?? 0;
        
        /// <summary>
        /// Gets the total count of all requirements.
        /// </summary>
        public int TotalCount => RequireCount + AvoidCount;
        
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
        // Validation - Using ForgeHelper Utilities
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Validates this group against a list of applied tags.
        /// ALL RequireTags must pass AND NONE of AvoidTags can pass.
        /// </summary>
        public bool Validate(List<Tag> appliedTags)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                appliedTags, 
                RequireTags, 
                AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against a TagCache.
        /// ALL RequireTags must pass AND NONE of AvoidTags can pass.
        /// </summary>
        public bool Validate(TagCache tagCache)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                tagCache, 
                RequireTags, 
                AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against any enumerable of tags.
        /// </summary>
        public bool Validate(IEnumerable<Tag> appliedTags)
        {
            return Validate(appliedTags?.ToList() ?? new List<Tag>());
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Detailed Validation - Returns Which Requirements Failed
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Result of detailed validation showing which requirements failed.
        /// </summary>
        public struct ValidationResult
        {
            public bool IsValid;
            public List<TagQuery> FailedRequirements;  // Required tags that were missing
            public List<TagQuery> TriggeredAvoidances; // Avoid tags that were present
            
            public bool HasFailures => !IsValid;
            public int FailureCount => (FailedRequirements?.Count ?? 0) + (TriggeredAvoidances?.Count ?? 0);
            
            /// <summary>
            /// Gets a human-readable summary of the validation result.
            /// </summary>
            public string GetSummary()
            {
                if (IsValid) return "All requirements met";
                
                var parts = new List<string>();
                if (FailedRequirements?.Count > 0)
                    parts.Add($"Missing {FailedRequirements.Count} required tag(s)");
                if (TriggeredAvoidances?.Count > 0)
                    parts.Add($"Has {TriggeredAvoidances.Count} blocked tag(s)");
                
                return string.Join(", ", parts);
            }
        }
        
        /// <summary>
        /// Performs validation and returns detailed information about failures.
        /// Useful for debugging or showing users why validation failed.
        /// </summary>
        public ValidationResult ValidateDetailed(TagCache tagCache)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                FailedRequirements = new List<TagQuery>(),
                TriggeredAvoidances = new List<TagQuery>()
            };
            
            if (tagCache == null)
            {
                result.IsValid = false;
                return result;
            }
            
            // Check required tags - use ForgeHelper pattern
            if (RequireTags != null)
            {
                foreach (var query in RequireTags)
                {
                    if (!ForgeHelper.ValidateTagQuery(query, tagCache))
                    {
                        result.FailedRequirements.Add(query);
                        result.IsValid = false;
                    }
                }
            }
            
            // Check avoid tags - inverted logic
            if (AvoidTags != null)
            {
                foreach (var query in AvoidTags)
                {
                    if (ForgeHelper.ValidateTagQuery(query, tagCache))
                    {
                        result.TriggeredAvoidances.Add(query);
                        result.IsValid = false;
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Performs validation and returns detailed information about failures.
        /// </summary>
        public ValidationResult ValidateDetailed(List<Tag> appliedTags)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                FailedRequirements = new List<TagQuery>(),
                TriggeredAvoidances = new List<TagQuery>()
            };
            
            var tags = appliedTags ?? new List<Tag>();
            
            // Check required tags
            if (RequireTags != null)
            {
                foreach (var query in RequireTags)
                {
                    if (!ForgeHelper.ValidateTagQuery(query, tags))
                    {
                        result.FailedRequirements.Add(query);
                        result.IsValid = false;
                    }
                }
            }
            
            // Check avoid tags - inverted logic
            if (AvoidTags != null)
            {
                foreach (var query in AvoidTags)
                {
                    if (ForgeHelper.ValidateTagQuery(query, tags))
                    {
                        result.TriggeredAvoidances.Add(query);
                        result.IsValid = false;
                    }
                }
            }
            
            return result;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Query Helpers - Simple Tag Addition
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Adds a required tag query to this group.
        /// </summary>
        public void AddRequirement(Tag tag, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            RequireTags ??= new List<TagQuery>();
            RequireTags.Add(TagQuery.HasTag(tag, matchMode));
        }
        
        /// <summary>
        /// Adds an avoid tag query to this group.
        /// </summary>
        public void AddAvoidance(Tag tag, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            AvoidTags ??= new List<TagQuery>();
            AvoidTags.Add(TagQuery.HasTag(tag, matchMode));
        }
        
        /// <summary>
        /// Adds a requirement for the tag or any of its children.
        /// Example: AddRequirementWithChildren("Status.Debuff") will require 
        /// any debuff tag (Burn, Poison, etc.)
        /// </summary>
        public void AddRequirementWithChildren(Tag tag)
        {
            AddRequirement(tag, ETagMatchMode.IncludeChildren);
        }
        
        /// <summary>
        /// Adds an avoidance for the tag or any of its children.
        /// Example: AddAvoidanceWithChildren("Status.Debuff") will block 
        /// if ANY debuff is present.
        /// </summary>
        public void AddAvoidanceWithChildren(Tag tag)
        {
            AddAvoidance(tag, ETagMatchMode.IncludeChildren);
        }
        
        /// <summary>
        /// Removes all queries for a specific tag from both Require and Avoid lists.
        /// </summary>
        public void RemoveTag(Tag tag)
        {
            RequireTags?.RemoveAll(q => q.Tag.Equals(tag));
            AvoidTags?.RemoveAll(q => q.Tag.Equals(tag));
        }
        
        /// <summary>
        /// Clears all requirements and avoidances.
        /// </summary>
        public void Clear()
        {
            RequireTags?.Clear();
            AvoidTags?.Clear();
        }
        
        /// <summary>
        /// Gets all unique tags referenced by this group (both require and avoid).
        /// </summary>
        public IEnumerable<Tag> GetAllReferencedTags()
        {
            var seen = new HashSet<Tag>();
            
            if (RequireTags != null)
            {
                foreach (var query in RequireTags)
                {
                    if (seen.Add(query.Tag))
                        yield return query.Tag;
                }
            }
            
            if (AvoidTags != null)
            {
                foreach (var query in AvoidTags)
                {
                    if (seen.Add(query.Tag))
                        yield return query.Tag;
                }
            }
        }
        
        /// <summary>
        /// Gets all referenced tags grouped by their root category.
        /// </summary>
        public Dictionary<Tag, List<Tag>> GetReferencedTagsByRoot()
        {
            return ForgeHelper.GroupByRoot(GetAllReferencedTags());
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Copy / Clone
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a deep copy of this group.
        /// </summary>
        public AvoidRequireTagGroup Clone()
        {
            var clone = new AvoidRequireTagGroup
            {
                Name = Name,
                SourceTemplate = SourceTemplate,
                SyncWithTemplate = SyncWithTemplate,
                RequireTags = RequireTags?.Select(q => new TagQuery
                {
                    Tag = q.Tag,
                    Operator = q.Operator,
                    Magnitude = q.Magnitude,
                    MatchMode = q.MatchMode
                }).ToList(),
                AvoidTags = AvoidTags?.Select(q => new TagQuery
                {
                    Tag = q.Tag,
                    Operator = q.Operator,
                    Magnitude = q.Magnitude,
                    MatchMode = q.MatchMode
                }).ToList()
            };
            
            return clone;
        }
        
        /// <summary>
        /// Copies values from another group into this one.
        /// </summary>
        public void CopyFrom(AvoidRequireTagGroup source)
        {
            if (source == null) return;
            
            Name = source.Name;
            
            RequireTags = source.RequireTags?.Select(q => new TagQuery
            {
                Tag = q.Tag,
                Operator = q.Operator,
                Magnitude = q.Magnitude,
                MatchMode = q.MatchMode
            }).ToList() ?? new List<TagQuery>();
            
            AvoidTags = source.AvoidTags?.Select(q => new TagQuery
            {
                Tag = q.Tag,
                Operator = q.Operator,
                Magnitude = q.Magnitude,
                MatchMode = q.MatchMode
            }).ToList() ?? new List<TagQuery>();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Debug / Display
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (HasName)
                parts.Add($"[{Name}]");
            
            if (RequireCount > 0)
                parts.Add($"Require:{RequireCount}");
            
            if (AvoidCount > 0)
                parts.Add($"Avoid:{AvoidCount}");
            
            if (IsLinkedToTemplate)
                parts.Add($"(Linked:{SourceTemplate.name})");
            
            return parts.Count > 0 ? string.Join(" ", parts) : "(Empty)";
        }
        
        /// <summary>
        /// Gets a detailed string representation for debugging.
        /// </summary>
        public string ToDebugString()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"AvoidRequireTagGroup: {(HasName ? Name : "(unnamed)")}");
            
            if (IsLinkedToTemplate)
                sb.AppendLine($"  Linked to: {SourceTemplate.name} (Sync: {SyncWithTemplate})");
            
            sb.AppendLine($"  Required ({RequireCount}):");
            if (RequireTags != null)
            {
                foreach (var q in RequireTags)
                    sb.AppendLine($"    + {q}");
            }
            
            sb.AppendLine($"  Avoid ({AvoidCount}):");
            if (AvoidTags != null)
            {
                foreach (var q in AvoidTags)
                    sb.AppendLine($"    - {q}");
            }
            
            return sb.ToString();
        }
    }
}