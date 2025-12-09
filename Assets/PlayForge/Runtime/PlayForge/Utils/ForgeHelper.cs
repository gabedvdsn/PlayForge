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

        public static bool ValidateEffectApplicationRequirements(GameplayEffectSpec spec, List<Tag> affiliation)
        {
            return ValidateAffiliationPolicy(spec.Base.ImpactSpecification.AffiliationPolicy, affiliation, spec.Origin.GetAffiliation())
                   && spec.Base.ValidateApplicationRequirements(spec);
        }

        public static bool ValidateEffectOngoingRequirements(GameplayEffectSpec spec) => spec.Base.ValidateOngoingRequirements(spec);

        public static bool ValidateEffectRemovalRequirements(GameplayEffectSpec spec) => spec.Base.ValidateRemovalRequirements(spec);
        
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
        
        #region Validation Utils

        public static bool ValidateSelfModification(bool allow, ISource a, ISource b)
        {
            if (allow) return true;
            return a != b;
        }

        public static bool ValidateContextTags(bool anyContext, List<Tag> validContexts, List<Tag> outsideContexts)
        {
            return anyContext || validContexts.ContainsAll(outsideContexts);
        }

        public static bool ValidateAffiliationPolicy(EAffiliationPolicy policy, List<Tag> a, List<Tag> b)
        {
            return policy switch
            {
                EAffiliationPolicy.Unaffiliated => !a.Any(b.Contains),
                EAffiliationPolicy.Affiliated => a.Any(b.Contains),
                EAffiliationPolicy.Any => true,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
            };
        }
        
        public static bool ValidateImpactTypes(List<Tag> impactType, List<Tag> validation, EAnyAllPolicy policy = EAnyAllPolicy.Any)
        {
            if (impactType.Contains(Tags.GEN_NOT_APPLICABLE) && !validation.Contains(Tags.GEN_ANY)) return false;

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
        
        #endregion
        
        #region Enum Helpers

        public static EAbilityActivationPolicy Translate(this EAbilityActivationPolicyExtended policy, AbilitySystemComponent asc = null)
        {
            if (policy == EAbilityActivationPolicyExtended.UseLocal)
            {
                if (asc is not null) return asc.DefaultActivationPolicy;
                return EAbilityActivationPolicy.SingleActiveQueue;
            }

            return policy switch
            {
                EAbilityActivationPolicyExtended.NoRestrictions => EAbilityActivationPolicy.NoRestrictions,
                EAbilityActivationPolicyExtended.SingleActive => EAbilityActivationPolicy.SingleActive,
                EAbilityActivationPolicyExtended.SingleActiveQueue => EAbilityActivationPolicy.SingleActiveQueue,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
            };
        }
        
        #endregion
    }
}
