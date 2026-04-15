using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Represents a query condition for a tag, with support for hierarchical matching.
    /// Used to validate whether an entity has/doesn't have certain tags.
    /// </summary>
    [Serializable]
    public class TagQuery
    {
        /// <summary>
        /// The tag to query for.
        /// </summary>
        [ForgeTagContext(ForgeContext.Required, ForgeContext.Granted, ForgeContext.AssetIdentifier)]
        public Tag Tag;
        
        /// <summary>
        /// The comparison operator for the magnitude check.
        /// </summary>
        public EComparisonOperator Operator = EComparisonOperator.GreaterThan;
        
        /// <summary>
        /// The magnitude to compare against.
        /// For most cases, use GreaterThan 0 to mean "has tag".
        /// </summary>
        public int Magnitude = 0;
        
        /// <summary>
        /// How to match tags in the hierarchy.
        /// Exact = only match the exact tag.
        /// IncludeChildren = match tag or any of its children.
        /// </summary>
        public ETagMatchMode MatchMode = ETagMatchMode.Exact;

        // ═══════════════════════════════════════════════════════════════════════════
        // Validation Methods
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Validates the query against a list of tags.
        /// </summary>
        public bool Validate(List<Tag> appliedTags)
        {
            if (appliedTags == null) return ValidateCount(0);
            
            int count = CountMatches(appliedTags);
            return ValidateCount(count);
        }
        
        /// <summary>
        /// Validates the query against a TagCache (uses weights).
        /// </summary>
        public bool Validate(TagCache tags)
        {
            if (tags == null) return ValidateCount(0);
            
            int count = CountMatches(tags);
            return ValidateCount(count);
        }
        
        /// <summary>
        /// Validates the query against any enumerable of tags.
        /// </summary>
        public bool Validate(IEnumerable<Tag> appliedTags)
        {
            if (appliedTags == null) return ValidateCount(0);
            
            int count = CountMatches(appliedTags);
            return ValidateCount(count);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Count Methods - Handle Hierarchical Matching
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Counts matching tags in a list according to the MatchMode.
        /// </summary>
        public int CountMatches(List<Tag> appliedTags)
        {
            if (appliedTags == null || appliedTags.Count == 0) return 0;
            
            return MatchMode switch
            {
                ETagMatchMode.Exact => appliedTags.Count(t => t.Equals(Tag)),
                ETagMatchMode.IncludeChildren => appliedTags.Count(t => t.MatchesOrIsChildOf(Tag)),
                ETagMatchMode.IncludeParents => appliedTags.Count(t => t.MatchesOrIsParentOf(Tag)),
                ETagMatchMode.SameRoot => CountSameRoot(appliedTags),
                _ => 0
            };
        }
        
        /// <summary>
        /// Counts matching tags in a TagCache according to the MatchMode.
        /// Uses weights for exact matching, iterates for hierarchical.
        /// </summary>
        public int CountMatches(TagCache tags)
        {
            if (tags == null) return 0;
            
            // For exact matching, use the fast weight lookup
            if (MatchMode == ETagMatchMode.Exact)
            {
                int weight = tags.GetWeight(Tag);
                return weight >= 0 ? weight : 0;
            }
            
            // For hierarchical matching, we need to check all applied tags
            return CountMatches(tags.GetAppliedTags());
        }
        
        /// <summary>
        /// Counts matching tags in an enumerable according to the MatchMode.
        /// </summary>
        public int CountMatches(IEnumerable<Tag> appliedTags)
        {
            if (appliedTags == null) return 0;
            
            return MatchMode switch
            {
                ETagMatchMode.Exact => appliedTags.Count(t => t.Equals(Tag)),
                ETagMatchMode.IncludeChildren => appliedTags.Count(t => t.MatchesOrIsChildOf(Tag)),
                ETagMatchMode.IncludeParents => appliedTags.Count(t => t.MatchesOrIsParentOf(Tag)),
                ETagMatchMode.SameRoot => appliedTags.Count(t => t.SharesAncestorWith(Tag)),
                _ => 0
            };
        }
        
        private int CountSameRoot(List<Tag> appliedTags)
        {
            var queryRoot = Tag.GetRoot();
            return appliedTags.Count(t => t.GetRoot().Equals(queryRoot));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Internal Validation
        // ═══════════════════════════════════════════════════════════════════════════
        
        private bool ValidateCount(int count)
        {
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Convenience Static Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a query that checks if a tag is present (count > 0).
        /// </summary>
        public static TagQuery HasTag(Tag tag, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            return new TagQuery
            {
                Tag = tag,
                Operator = EComparisonOperator.GreaterThan,
                Magnitude = 0,
                MatchMode = matchMode
            };
        }
        
        /// <summary>
        /// Creates a query that checks if a tag is not present (count == 0).
        /// </summary>
        public static TagQuery DoesNotHaveTag(Tag tag, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            return new TagQuery
            {
                Tag = tag,
                Operator = EComparisonOperator.Equal,
                Magnitude = 0,
                MatchMode = matchMode
            };
        }
        
        /// <summary>
        /// Creates a query that checks if the entity has a specific number of the tag.
        /// </summary>
        public static TagQuery HasExactCount(Tag tag, int count, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            return new TagQuery
            {
                Tag = tag,
                Operator = EComparisonOperator.Equal,
                Magnitude = count,
                MatchMode = matchMode
            };
        }
        
        /// <summary>
        /// Creates a query for "has tag or any child of tag".
        /// Example: HasTagOrChildren("Status.Debuff") matches "Status.Debuff", "Status.Debuff.Burn", etc.
        /// </summary>
        public static TagQuery HasTagOrChildren(Tag tag)
        {
            return HasTag(tag, ETagMatchMode.IncludeChildren);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Display / Debug
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override string ToString()
        {
            string modeStr = MatchMode != ETagMatchMode.Exact ? $" [{MatchMode}]" : "";
            return $"{Tag.Name} {Operator} {Magnitude}{modeStr}";
        }
    }
}