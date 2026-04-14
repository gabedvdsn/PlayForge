using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge
{
    public static class ForgeHelper
    {
        #region GAS Utils

        public static bool ValidateEffectApplicationRequirements(GameplayEffectSpec spec, List<Tag> targetAffiliation)
        {
            return ValidateAffiliationPolicy(spec, targetAffiliation)
                   && spec.Base.ValidateApplicationRequirements(spec);
        }

        public static bool ValidateEffectOngoingRequirements(GameplayEffectSpec spec) => spec.Base.ValidateOngoingRequirements(spec);

        public static bool ValidateEffectRemovalRequirements(GameplayEffectSpec spec) => spec.Base.ValidateRemovalRequirements(spec);

        public static Texture2D GetTextureItem(IEnumerable<TextureItem> textures, Tag tag)
        {
            if (textures is null || string.IsNullOrEmpty(tag.GetLeafName())) return null;
            
            var _textures = textures.ToArray();
            foreach (var ti in _textures)
            {
                if (ti.Tag == tag) return ti.Texture;
            }
            return _textures.Length > 0 ? _textures[0].Texture : null;
        }

        public static RuntimeAttribute ConfigureLevelAttributeFor(BasicEffectOrigin spec)
        {
            return ConfigureLevelAttributeFor(spec.GetAssetTag());
        }

        public static RuntimeAttribute ConfigureLevelAttributeFor(Tag assetTag)
        {
            const string LevelAttributePrefix = "LVL_ATTRIBUTE_";
            
            string input = AttributeRegistry.RefactorByNamingConvention(assetTag.Name);
            var result = Tag.GenerateAsUnique(input, LevelAttributePrefix);

            return new RuntimeAttribute(result.Name, assetTag.Name, input);
        }

        public static float RelativeOffsetValue(float value, float max, float offset = 1f)
        {
            return max > offset ? (value - offset) / (max - offset) : offset;
        }

        public static float RelativeValue(float value, float minValue, float maxValue)
        {
            return Mathf.Lerp(minValue, maxValue, value);
        }
        
        #endregion
        
        #region Sign Related Utils
        
        public static ESignPolicy SignPolicy(params float[] magnitudes)
        {
            float sum = magnitudes.Sum();
            return sum switch
            {
                > 0 => ESignPolicy.Positive,
                < 0 => ESignPolicy.Negative,
                0 when magnitudes.Any(mag => mag != 0) => ESignPolicy.ZeroBiased,
                _ => ESignPolicy.ZeroNeutral
            };
        }
        
        public static ESignPolicyExtended SignPolicyExtended(params float[] magnitudes)
        {
            float sum = magnitudes.Sum();
            return sum switch
            {
                > 0 => ESignPolicyExtended.Positive,
                < 0 => ESignPolicyExtended.Negative,
                0 when magnitudes.Any(mag => mag != 0) => ESignPolicyExtended.ZeroBiased,
                _ => ESignPolicyExtended.ZeroNeutral
            };
        }
        
        public static int SignInt(ESignPolicy signPolicy)
        {
            return signPolicy switch
            {

                ESignPolicy.Negative => -1,
                ESignPolicy.Positive => 1,
                ESignPolicy.ZeroBiased => 0,
                ESignPolicy.ZeroNeutral => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(signPolicy), signPolicy, null)
            };
        }
        
        public static float SignFloat(ESignPolicy signPolicy)
        {
            return signPolicy switch
            {

                ESignPolicy.Negative => -1f,
                ESignPolicy.Positive => 1f,
                ESignPolicy.ZeroBiased => 0f,
                ESignPolicy.ZeroNeutral => 0f,
                _ => throw new ArgumentOutOfRangeException(nameof(signPolicy), signPolicy, null)
            };
        }

        public static int AlignedSignInt(params ESignPolicy[] signPolicies)
        {
            return signPolicies.Aggregate(1, (current, signPolicy) => current * SignInt(signPolicy));
        }
        
        public static float AlignedSignFloat(params ESignPolicy[] signPolicies)
        {
            return signPolicies.Aggregate(1f, (current, signPolicy) => current * SignFloat(signPolicy));
        }

        public static int AlignToSign(int value, ESignPolicy signPolicy)
        {
            int _value = Mathf.Abs(value);
            return signPolicy switch
            {

                ESignPolicy.Negative => -_value,
                ESignPolicy.Positive => _value,
                ESignPolicy.ZeroBiased => _value,
                ESignPolicy.ZeroNeutral => _value,
                _ => throw new ArgumentOutOfRangeException(nameof(signPolicy), signPolicy, null)
            };
        }
        
        public static float AlignToSign(float value, ESignPolicy signPolicy)
        {
            float _value = Mathf.Abs(value);
            return signPolicy switch
            {

                ESignPolicy.Negative => -_value,
                ESignPolicy.Positive => _value,
                ESignPolicy.ZeroBiased => _value,
                ESignPolicy.ZeroNeutral => _value,
                _ => throw new ArgumentOutOfRangeException(nameof(signPolicy), signPolicy, null)
            };
        }
        
        public static AttributeValue AlignToSign(AttributeValue attributeValue, ESignPolicy signPolicy)
        {
            float _curr = Mathf.Abs(attributeValue.CurrentValue);
            float _base= Mathf.Abs(attributeValue.BaseValue);

            switch (signPolicy)
            {
                case ESignPolicy.Negative:
                    return new AttributeValue(-_curr, -_base);
                case ESignPolicy.Positive:
                case ESignPolicy.ZeroBiased:
                case ESignPolicy.ZeroNeutral:
                    return new AttributeValue(_curr, _base);
                default:
                    throw new ArgumentOutOfRangeException(nameof(signPolicy), signPolicy, null);
            }
        }

        #endregion
        
        #region Tag-Related Pipeline Validation Utils

        public static bool ValidateSelfModification(bool allow, ISource a, ISource b)
        {
            if (allow) return true;
            return a != b;
        }

        public static bool ValidateContextTags(bool anyContext, List<Tag> validContexts, ImpactDerivationContext outsideContexts, EAnyAllPolicy compPolicy)
        {
            if (anyContext) return true;
            return compPolicy switch
            {

                EAnyAllPolicy.Any => outsideContexts.All().Any(validContexts.Contains),
                EAnyAllPolicy.All => outsideContexts.All().All(validContexts.Contains),
                _ => throw new ArgumentOutOfRangeException(nameof(compPolicy), compPolicy, null)
            };

        }

        public static bool ValidateAffiliationPolicy(
            GameplayEffectSpec spec, List<Tag> targetAffiliation)
        {
            if (spec.Base.ImpactSpecification.AffiliationComparison == EAnyAllPolicy.Any)
            {
                return spec.Base.ImpactSpecification.AffiliationPolicy switch
                {
                    EAffiliationPolicy.UseAffiliationList => spec.Base.ImpactSpecification.Affiliations.Any(targetAffiliation.Contains),
                    EAffiliationPolicy.Unaffiliated => !spec.Origin.GetAffiliation().Any(targetAffiliation.Contains),
                    EAffiliationPolicy.Affiliated => spec.Origin.GetAffiliation().Any(targetAffiliation.Contains),
                    EAffiliationPolicy.AlwaysAllow => true,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            
            return spec.Base.ImpactSpecification.AffiliationPolicy switch
            {
                EAffiliationPolicy.UseAffiliationList => spec.Base.ImpactSpecification.Affiliations.All(targetAffiliation.Contains),
                EAffiliationPolicy.Unaffiliated => !spec.Origin.GetAffiliation().All(targetAffiliation.Contains),
                EAffiliationPolicy.Affiliated => spec.Origin.GetAffiliation().All(targetAffiliation.Contains),
                EAffiliationPolicy.AlwaysAllow => true,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static bool ValidateImpactTypes(bool anyType, List<Tag> impactType, List<Tag> validation, EAnyAllPolicy policy = EAnyAllPolicy.Any)
        {
            if (impactType.Contains(Tags.DisallowImpact)) return false;

            if (anyType || impactType.Contains(Tags.AllowImpact)) return true;
            
            return policy switch
            {
                EAnyAllPolicy.Any => impactType.Any(validation.Contains),
                EAnyAllPolicy.All => impactType.All(validation.Contains),
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
            };
        }

        public static bool ValidateImpactTargets(EEffectImpactTargetExpanded impactTarget, AttributeValue attributeValue, bool exclusive)
        {
            if (exclusive && impactTarget is EEffectImpactTargetExpanded.Current or EEffectImpactTargetExpanded.Base)
            {
                return impactTarget switch
                {
                    EEffectImpactTargetExpanded.Current => attributeValue.CurrentValue != 0 && attributeValue.BaseValue == 0,
                    EEffectImpactTargetExpanded.Base => attributeValue.CurrentValue == 0 && attributeValue.BaseValue != 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
                };
            }
            
            return impactTarget switch
            {
                EEffectImpactTargetExpanded.Current => attributeValue.CurrentValue != 0,
                EEffectImpactTargetExpanded.Base => attributeValue.BaseValue != 0,
                EEffectImpactTargetExpanded.CurrentAndBase => attributeValue.CurrentValue != 0 && attributeValue.BaseValue != 0,
                EEffectImpactTargetExpanded.CurrentOrBase => attributeValue.CurrentValue != 0 || attributeValue.BaseValue != 0,
                _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
            };
        }

        public static bool ValidateSignPolicy(ESignPolicy signPolicy, EEffectImpactTargetExpanded impactTarget, AttributeValue attributeValue)
        {
            return impactTarget switch
            {
                EEffectImpactTargetExpanded.Current => SignPolicy(attributeValue.CurrentValue) == signPolicy,
                EEffectImpactTargetExpanded.Base => SignPolicy(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTargetExpanded.CurrentAndBase => SignPolicy(attributeValue.CurrentValue) == signPolicy && SignPolicy(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTargetExpanded.CurrentOrBase => SignPolicy(attributeValue.CurrentValue) == signPolicy || SignPolicy(attributeValue.BaseValue) == signPolicy,
                _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
            };
        }
        
        public static bool ValidateSignPolicy(ESignPolicy signPolicy, EEffectImpactTarget impactTarget, AttributeValue attributeValue)
        {
            return impactTarget switch
            {

                EEffectImpactTarget.Current => SignPolicy(attributeValue.CurrentValue) == signPolicy,
                EEffectImpactTarget.Base => SignPolicy(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTarget.CurrentAndBase => SignPolicy(attributeValue.CurrentValue) == signPolicy && SignPolicy(attributeValue.BaseValue) == signPolicy,
                _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
            };
        }
        
        public static bool ValidateSignPolicy(ESignPolicyExtended signPolicy, EEffectImpactTarget impactTarget, AttributeValue attributeValue)
        {
            if (signPolicy == ESignPolicyExtended.Any) return true;
            return impactTarget switch
            {

                EEffectImpactTarget.Current => SignPolicyExtended(attributeValue.CurrentValue) == signPolicy,
                EEffectImpactTarget.Base => SignPolicyExtended(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTarget.CurrentAndBase => SignPolicyExtended(attributeValue.CurrentValue) == signPolicy && SignPolicyExtended(attributeValue.BaseValue) == signPolicy,
                _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
            };
        }
        
        public static bool ValidateSignPolicy(ESignPolicyExtended signPolicy, EEffectImpactTargetExpanded impactTarget, AttributeValue attributeValue)
        {
            if (signPolicy == ESignPolicyExtended.Any) return true;
            return impactTarget switch
            {

                EEffectImpactTargetExpanded.Current => SignPolicyExtended(attributeValue.CurrentValue) == signPolicy,
                EEffectImpactTargetExpanded.Base => SignPolicyExtended(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTargetExpanded.CurrentAndBase => SignPolicyExtended(attributeValue.CurrentValue) == signPolicy && SignPolicyExtended(attributeValue.BaseValue) == signPolicy,
                EEffectImpactTargetExpanded.CurrentOrBase => SignPolicyExtended(attributeValue.CurrentValue) == signPolicy || SignPolicyExtended(attributeValue.BaseValue) == signPolicy,
                _ => throw new ArgumentOutOfRangeException(nameof(impactTarget), impactTarget, null)
            };
        }
        
        #endregion
        
        #region Tag Avoid/Require Validation
        
        /// <summary>
        /// Validates an Avoid/Require pattern:
        /// - ALL "Required" tags must be present
        /// - NONE of the "Avoid" tags must be present
        /// </summary>
        public static bool ValidateAvoidRequire(
            IEnumerable<Tag> tags,
            IEnumerable<Tag> required,
            IEnumerable<Tag> avoid,
            ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            var tagList = tags?.ToList() ?? new List<Tag>();
            
            // Check required tags
            if (required != null)
            {
                foreach (var req in required)
                {
                    if (!HasTag(tagList, req, matchMode))
                        return false;
                }
            }
            
            // Check avoid tags
            if (avoid != null)
            {
                foreach (var avd in avoid)
                {
                    if (HasTag(tagList, avd, matchMode))
                        return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates an Avoid/Require pattern against a TagCache.
        /// </summary>
        public static bool ValidateAvoidRequire(
            TagCache tagCache,
            IEnumerable<Tag> required,
            IEnumerable<Tag> avoid,
            ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tagCache == null) return false;
            
            // Check required tags
            if (required != null)
            {
                foreach (var req in required)
                {
                    if (!tagCache.HasTag(req, matchMode))
                        return false;
                }
            }
            
            // Check avoid tags
            if (avoid != null)
            {
                foreach (var avd in avoid)
                {
                    if (tagCache.HasTag(avd, matchMode))
                        return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates using TagQuery lists for more complex requirements.
        /// </summary>
        public static bool ValidateAvoidRequireQueries(
            TagCache tagCache,
            IEnumerable<TagQuery> requiredQueries,
            IEnumerable<TagQuery> avoidQueries)
        {
            // All required queries must pass
            if (!ValidateAllQueries(requiredQueries, tagCache))
                return false;
            
            // Avoid queries are inverted - they must all FAIL
            if (avoidQueries != null)
            {
                foreach (var query in avoidQueries)
                {
                    if (query.Validate(tagCache))
                        return false;  // If avoid query passes, validation fails
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates using TagQuery lists for more complex requirements (List version).
        /// </summary>
        public static bool ValidateAvoidRequireQueries(
            List<Tag> appliedTags,
            IEnumerable<TagQuery> requiredQueries,
            IEnumerable<TagQuery> avoidQueries)
        {
            var tags = appliedTags ?? new List<Tag>();
            
            // All required queries must pass
            if (!ValidateAllQueries(requiredQueries, tags))
                return false;
            
            // Avoid queries are inverted - they must all FAIL
            if (avoidQueries != null)
            {
                foreach (var query in avoidQueries)
                {
                    if (query.Validate(tags))
                        return false;  // If avoid query passes, validation fails
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates using TagQuery lists (IEnumerable version).
        /// </summary>
        public static bool ValidateAvoidRequireQueries(
            IEnumerable<Tag> appliedTags,
            IEnumerable<TagQuery> requiredQueries,
            IEnumerable<TagQuery> avoidQueries)
        {
            return ValidateAvoidRequireQueries(appliedTags?.ToList(), requiredQueries, avoidQueries);
        }
        
        #endregion
        
        #region Tag Query Validation
        
        /// <summary>
        /// Validates a single TagQuery against a tag collection.
        /// </summary>
        public static bool ValidateTagQuery(TagQuery query, IEnumerable<Tag> tags)
        {
            if (query == null) return true;
            return query.Validate(tags?.ToList() ?? new List<Tag>());
        }
        
        /// <summary>
        /// Validates a single TagQuery against a TagCache.
        /// </summary>
        public static bool ValidateTagQuery(TagQuery query, TagCache tagCache)
        {
            if (query == null) return true;
            return query.Validate(tagCache);
        }
        
        /// <summary>
        /// Validates ALL queries must pass.
        /// </summary>
        public static bool ValidateAllQueries(IEnumerable<TagQuery> queries, IEnumerable<Tag> tags)
        {
            if (queries == null) return true;
            
            var tagList = tags?.ToList() ?? new List<Tag>();
            
            foreach (var query in queries)
            {
                if (!query.Validate(tagList))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Validates ALL queries must pass against a TagCache.
        /// </summary>
        public static bool ValidateAllQueries(IEnumerable<TagQuery> queries, TagCache tagCache)
        {
            if (queries == null) return true;
            
            foreach (var query in queries)
            {
                if (!query.Validate(tagCache))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Validates ANY query must pass.
        /// </summary>
        public static bool ValidateAnyQuery(IEnumerable<TagQuery> queries, IEnumerable<Tag> tags)
        {
            if (queries == null) return true;
            
            var tagList = tags?.ToList() ?? new List<Tag>();
            var queryList = queries.ToList();
            
            if (queryList.Count == 0) return true;
            
            foreach (var query in queryList)
            {
                if (query.Validate(tagList))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Validates ANY query must pass against a TagCache.
        /// </summary>
        public static bool ValidateAnyQuery(IEnumerable<TagQuery> queries, TagCache tagCache)
        {
            if (queries == null) return true;
            
            var queryList = queries.ToList();
            if (queryList.Count == 0) return true;
            
            foreach (var query in queryList)
            {
                if (query.Validate(tagCache))
                    return true;
            }
            return false;
        }
        
        #endregion
        
        #region Tag Hierarchy Utils
        
        /// <summary>
        /// Checks if the tag collection contains the query tag.
        /// Supports hierarchical matching.
        /// </summary>
        public static bool HasTag(IEnumerable<Tag> tags, Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) return false;
            
            foreach (var tag in tags)
            {
                if (MatchesTag(tag, query, matchMode))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Checks if the tag matches the query according to the match mode.
        /// </summary>
        public static bool MatchesTag(Tag tag, Tag query, ETagMatchMode matchMode)
        {
            return matchMode switch
            {
                ETagMatchMode.Exact => tag.Equals(query),
                ETagMatchMode.IncludeChildren => tag.MatchesOrIsChildOf(query),
                ETagMatchMode.IncludeParents => tag.MatchesOrIsParentOf(query),
                ETagMatchMode.SameRoot => tag.SharesAncestorWith(query),
                _ => tag.Equals(query)
            };
        }
        
        /// <summary>
        /// Checks if ANY of the query tags are present in the collection.
        /// </summary>
        public static bool HasAnyTag(IEnumerable<Tag> tags, IEnumerable<Tag> queries, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null || queries == null) return false;
            
            var tagList = tags as IList<Tag> ?? tags.ToList();
            
            foreach (var query in queries)
            {
                if (HasTag(tagList, query, matchMode))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Checks if ALL of the query tags are present in the collection.
        /// </summary>
        public static bool HasAllTags(IEnumerable<Tag> tags, IEnumerable<Tag> queries, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null || queries == null) return false;
            
            var tagList = tags as IList<Tag> ?? tags.ToList();
            
            foreach (var query in queries)
            {
                if (!HasTag(tagList, query, matchMode))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Checks if NONE of the query tags are present in the collection.
        /// </summary>
        public static bool HasNoneOfTags(IEnumerable<Tag> tags, IEnumerable<Tag> queries, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) return true;
            if (queries == null) return true;
            
            var tagList = tags as IList<Tag> ?? tags.ToList();
            
            foreach (var query in queries)
            {
                if (HasTag(tagList, query, matchMode))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Counts how many tags in the collection match the query.
        /// </summary>
        public static int CountMatchingTags(IEnumerable<Tag> tags, Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) return 0;
            
            int count = 0;
            foreach (var tag in tags)
            {
                if (MatchesTag(tag, query, matchMode))
                    count++;
            }
            return count;
        }
        
        /// <summary>
        /// Gets all tags in the collection that match the query.
        /// </summary>
        public static IEnumerable<Tag> GetMatchingTags(IEnumerable<Tag> tags, Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) yield break;
            
            foreach (var tag in tags)
            {
                if (MatchesTag(tag, query, matchMode))
                    yield return tag;
            }
        }
        
        #endregion
        
        #region Tag Path Utils

        /// <summary>
        /// Checks if a tag path is valid (properly formatted).
        /// </summary>
        public static bool IsValidTagPath(string path)
        {
            return TagHierarchy.IsValidPath(path);
        }
        
        /// <summary>
        /// Creates a tag path from segments.
        /// </summary>
        public static string BuildTagPath(params string[] segments)
        {
            if (segments == null || segments.Length == 0) return "";
            return string.Join(".", segments.Where(s => !string.IsNullOrEmpty(s)));
        }
        
        /// <summary>
        /// Gets all unique root categories from a tag collection.
        /// </summary>
        public static IEnumerable<Tag> GetUniqueRoots(IEnumerable<Tag> tags)
        {
            if (tags == null) yield break;
            
            var seen = new HashSet<string>();
            foreach (var tag in tags)
            {
                var root = tag.GetRoot();
                if (seen.Add(root.Name))
                {
                    yield return root;
                }
            }
        }
        
        /// <summary>
        /// Groups tags by their root category.
        /// </summary>
        public static Dictionary<Tag, List<Tag>> GroupByRoot(IEnumerable<Tag> tags)
        {
            var result = new Dictionary<Tag, List<Tag>>();
            
            if (tags == null) return result;
            
            foreach (var tag in tags)
            {
                var root = tag.GetRoot();
                if (!result.TryGetValue(root, out var list))
                {
                    list = new List<Tag>();
                    result[root] = list;
                }
                list.Add(tag);
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets the deepest common ancestor of two tags.
        /// Returns null if they share no common ancestor.
        /// </summary>
        public static Tag? GetCommonAncestor(Tag a, Tag b)
        {
            if (string.IsNullOrEmpty(a.Name) || string.IsNullOrEmpty(b.Name))
                return null;
            
            var partsA = a.GetSegments();
            var partsB = b.GetSegments();
            
            int minLen = Math.Min(partsA.Length, partsB.Length);
            int commonDepth = 0;
            
            for (int i = 0; i < minLen; i++)
            {
                if (partsA[i] == partsB[i])
                    commonDepth = i + 1;
                else
                    break;
            }
            
            if (commonDepth == 0) return null;
            
            return Tag.GenerateAsUnique(string.Join(".", partsA.Take(commonDepth)));
        }
        
        #endregion
        
        #region Attribute Utils

        /// <summary>
        /// Performs logical operations on Attribute Values
        /// </summary>
        /// <param name="value">The left operand</param>
        /// <param name="operand">The right operand</param>
        /// <param name="operation"> The operation to apply</param>
        /// <param name="target"></param>
        /// <param name="policy"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static AttributeValue AttributeMathEvent(AttributeValue value, AttributeValue operand, ECalculationOperation operation, EEffectImpactTarget target,
            EMathApplicationPolicy policy) 
        {
            AttributeValue _operand;
            switch (policy)
            {
                case EMathApplicationPolicy.AsIs:
                    _operand = operand;
                    break;
                case EMathApplicationPolicy.OnePlus:
                    _operand = 1 + operand;
                    break;
                case EMathApplicationPolicy.OneMinus:
                    _operand = 1 - operand;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
            }

            AttributeValue result = PerformOperation(value, _operand);
            return target switch
            {
                EEffectImpactTarget.Current => new AttributeValue(result.CurrentValue, 0f),
                EEffectImpactTarget.Base => new AttributeValue(0, result.BaseValue),
                EEffectImpactTarget.CurrentAndBase => new AttributeValue(result.CurrentValue, result.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };

            AttributeValue PerformOperation(AttributeValue a, AttributeValue b)
            {
                return operation switch
                {

                    ECalculationOperation.Add => a + b,
                    ECalculationOperation.Multiply => a * b,
                    ECalculationOperation.Override => b,
                    ECalculationOperation.FlatBonus => a + b,
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
                };
            }
        }
        public static AttributeValue LogicalAdd(AttributeValue value, AttributeValue operand, AttributeOverflowData overflow)
        {
            return overflow.Policy switch
            {

                EAttributeOverflowPolicy.ZeroToBase => new AttributeValue(Mathf.Clamp(value.CurrentValue + operand.CurrentValue, 0, value.BaseValue),
                    Mathf.Clamp(value.BaseValue + operand.BaseValue, 0, value.BaseValue)),
                EAttributeOverflowPolicy.FloorToBase => new AttributeValue(Mathf.Clamp(value.CurrentValue + operand.CurrentValue, overflow.Floor.CurrentValue, value.BaseValue),
                    Mathf.Clamp(value.BaseValue + operand.BaseValue, overflow.Floor.BaseValue, value.BaseValue)),
                EAttributeOverflowPolicy.ZeroToCeil => new AttributeValue(Mathf.Clamp(value.CurrentValue + operand.CurrentValue, 0, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(value.BaseValue + operand.BaseValue, 0, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.FloorToCeil => new AttributeValue(
                    Mathf.Clamp(value.CurrentValue + operand.CurrentValue, overflow.Floor.CurrentValue, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(value.BaseValue + operand.BaseValue, overflow.Floor.BaseValue, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.Unlimited => new AttributeValue(value.CurrentValue + operand.CurrentValue, value.BaseValue + operand.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static AttributeValue LogicalMultiply(AttributeValue value, AttributeValue operand, AttributeOverflowData overflow)
        {
            return overflow.Policy switch
            {

                EAttributeOverflowPolicy.ZeroToBase => new AttributeValue(Mathf.Clamp(value.CurrentValue * operand.CurrentValue, 0, value.BaseValue),
                    Mathf.Clamp(value.BaseValue * operand.BaseValue, 0, value.BaseValue)),
                EAttributeOverflowPolicy.FloorToBase => new AttributeValue(Mathf.Clamp(value.CurrentValue * operand.CurrentValue, overflow.Floor.CurrentValue, value.BaseValue),
                    Mathf.Clamp(value.BaseValue * operand.BaseValue, overflow.Floor.BaseValue, value.BaseValue)),
                EAttributeOverflowPolicy.ZeroToCeil => new AttributeValue(Mathf.Clamp(value.CurrentValue * operand.CurrentValue, 0, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(value.BaseValue * operand.BaseValue, 0, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.FloorToCeil => new AttributeValue(
                    Mathf.Clamp(value.CurrentValue * operand.CurrentValue, overflow.Floor.CurrentValue, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(value.BaseValue * operand.BaseValue, overflow.Floor.BaseValue, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.Unlimited => new AttributeValue(value.CurrentValue + operand.CurrentValue, value.BaseValue + operand.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static AttributeValue LogicalOverride(AttributeValue value, AttributeValue operand, AttributeOverflowData overflow)
        {
            return overflow.Policy switch
            {

                EAttributeOverflowPolicy.ZeroToBase => new AttributeValue(Mathf.Clamp(operand.CurrentValue, 0, value.BaseValue),
                    Mathf.Clamp(operand.BaseValue, 0, value.BaseValue)),
                EAttributeOverflowPolicy.FloorToBase => new AttributeValue(Mathf.Clamp(operand.CurrentValue, overflow.Floor.CurrentValue, value.BaseValue),
                    Mathf.Clamp(operand.BaseValue, overflow.Floor.BaseValue, value.BaseValue)),
                EAttributeOverflowPolicy.ZeroToCeil => new AttributeValue(Mathf.Clamp(operand.CurrentValue, 0, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(operand.BaseValue, 0, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.FloorToCeil => new AttributeValue(
                    Mathf.Clamp(operand.CurrentValue, overflow.Floor.CurrentValue, overflow.Ceil.CurrentValue),
                    Mathf.Clamp(operand.BaseValue, overflow.Floor.BaseValue, overflow.Ceil.BaseValue)),
                EAttributeOverflowPolicy.Unlimited => new AttributeValue(operand.CurrentValue, operand.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        #endregion
        
        #region Collection Utils
        
        public static T RandomChoice<T>(this List<T> list) => list is null ? default : list[Mathf.FloorToInt(Random.value * list.Count)];

        public static void SafeAdd<K, V>(this Dictionary<K, V> dict, K key, V value, bool overrideValue)
        {
            if (!dict.ContainsKey(key)) dict[key] = value;
            else if (overrideValue) dict[key] = value;
        }
        
        public static void SafeAdd<K, V>(this Dictionary<K, List<V>> dict, K key, V value)
        {
            if (dict.ContainsKey(key)) dict[key].Add(value);
            else dict[key] = new List<V>() { value };
        }
        
        public static void SafeAdd<K, V>(this Dictionary<K, HashSet<V>> dict, K key, V value)
        {
            if (dict.ContainsKey(key)) dict[key].Add(value);
            else dict[key] = new HashSet<V>() { value };
        }

        public static void SafeAddRange<K, V>(this Dictionary<K, List<V>> dict, K key, List<V> values)
        {
            if (dict.ContainsKey(key)) dict[key].AddRange(values);
            else dict[key] = values;
        }

        public static bool SafeRemove<K, V>(this Dictionary<K, V> dict, K key)
        {
            return dict.Remove(key);
        }

        public static bool SafeRemove<K, V>(this Dictionary<K, List<V>> dict, K key, V value)
        {
            return dict.ContainsKey(key) && dict[key].Remove(value);
        }

        public static void Shuffle<T>(this List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Mathf.FloorToInt(Random.value * (n + 1));
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static List<T> WeightedSample<T>(this List<T> list, int N, List<float> weights)
        {
            float weightSum = weights.Sum();
            List<float> normalizedWeights = weights.Select(w => w / weightSum).ToList();
            List<T> selected = new List<T>();

            for (int _ = 0; _ < N; _++)
            {
                float randomValue = Random.value;
                float cumulative = 0f;

                for (int i = 0; i < list.Count; i++)
                {
                    cumulative += normalizedWeights[i];
                    if (randomValue <= cumulative)
                    {
                        selected.Add(list[i]);
                        break;
                    }
                }
            }

            return selected;
        }

        public static bool ContainsAll<T>(this IEnumerable<T> list, IEnumerable<T> match)
        {
            return match.All(list.Contains);
        }

        public static bool TrueForAny<T>(this IEnumerable<T> list, Predicate<T> condition)
        {
            foreach (var i in list)
            {
                if (condition?.Invoke(i) ?? false) return true;
            }

            return false;
        }
        
        #endregion
        
        #region Enum Helpers

        public static EAbilityActivationPolicy Translate(this EAbilityActivationPolicyExtended policy, AbilitySystemComponent asc = null)
        {
            if (policy == EAbilityActivationPolicyExtended.UseLocalPolicy)
            {
                if (asc is not null) return asc.DefaultActivationPolicy;
                return EAbilityActivationPolicy.QueueActivationIfBusy;
            }

            return policy switch
            {
                EAbilityActivationPolicyExtended.AlwaysActivate => EAbilityActivationPolicy.AlwaysActivate,
                EAbilityActivationPolicyExtended.ActivateIfIdle => EAbilityActivationPolicy.ActivateIfIdle,
                EAbilityActivationPolicyExtended.QueueActivationIfBusy => EAbilityActivationPolicy.QueueActivationIfBusy,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
            };
        }
        
        #endregion
        
        #region Computations

        public static int MagnitudeAndScalerOperation(int value, float scaleValue, EMagnitudeOperation operation, ERoundingOperation rounding)
        {
            float v = MagnitudeAndScalerOperation(value, scaleValue, operation);

            return rounding switch
            {
                ERoundingOperation.Floor => Mathf.FloorToInt(v),
                ERoundingOperation.Ceil => Mathf.CeilToInt(v),
                _ => throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null)
            };
        }

        public static float MagnitudeAndScalerOperation(float value, float scaleValue, EMagnitudeOperation operation)
        {
            return operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => value * scaleValue,
                EMagnitudeOperation.AddScaler => value + scaleValue,
                EMagnitudeOperation.UseMagnitude => value,
                EMagnitudeOperation.UseScaler => scaleValue,
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
        }
        
        #endregion
        
        #region Utility

        public static Vector3 RandomPointWithinCircle(float radius, float minRadius = 0f)
        {
            var p = Random.insideUnitCircle * radius;
            if (p.magnitude > minRadius) return p;

            return p.normalized * minRadius;
        }
        
        public static Vector3 RandomPointWithinSphere(float radius, float minRadius = 0f)
        {
            var p = Random.insideUnitSphere * radius;
            if (p.normalized.magnitude > minRadius) return p;

            return p.normalized * minRadius;
        }
        
        
        #endregion
    }
}
