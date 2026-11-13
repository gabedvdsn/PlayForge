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
        [ForgeTagContext(ForgeContext.Granted, ForgeContext.AssetTag, ForgeContext.Affiliation)]
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

            // A plain loop: this runs on every requirement check, and LINQ here allocated a closure and an
            // enumerator each time (PF-007)
            int count = 0;
            for (int i = 0; i < appliedTags.Count; i++) if (Matches(appliedTags[i])) count++;
            return count;
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
            
            // A hierarchical match counts each matching tag by its weight, as an exact match does. Counting the tags
            // made a magnitude mean grants in one mode and different tags in the others (GAS-16)
            var applied = tags.GetAppliedTags();
            int count = 0;
            for (int i = 0; i < applied.Count; i++)
            {
                if (Matches(applied[i])) count += Math.Max(0, tags.GetWeight(applied[i]));
            }
            return count;
        }
        
        /// <summary>
        /// Counts matching tags in an enumerable according to the MatchMode.
        /// </summary>
        public int CountMatches(IEnumerable<Tag> appliedTags)
        {
            if (appliedTags == null) return 0;
            if (appliedTags is List<Tag> list) return CountMatches(list);
            
            // Through the match the other overloads use. SameRoot was written two ways (GAS-16)
            int count = 0;
            foreach (var applied in appliedTags) if (Matches(applied)) count++;
            return count;
        }

        // Whether one applied tag answers this query
        private bool Matches(Tag applied) => MatchMode switch
        {
            ETagMatchMode.Exact => applied.Equals(Tag),
            ETagMatchMode.IncludeChildren => applied.MatchesOrIsChildOf(Tag),
            ETagMatchMode.IncludeParents => applied.MatchesOrIsParentOf(Tag),
            ETagMatchMode.SameRoot => applied.SharesAncestorWith(Tag),
            _ => false
        };
        
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
            return $"{Tag} {Operator} {Magnitude}{modeStr}";
        }
    }
}