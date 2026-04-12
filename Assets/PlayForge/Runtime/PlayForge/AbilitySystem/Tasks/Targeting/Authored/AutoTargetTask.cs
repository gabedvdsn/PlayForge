using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Auto-targeting task that finds targets automatically based on spatial queries,
    /// tag requirements, and optional attribute conditions.
    ///
    /// Uses Physics.OverlapSphere to find candidate colliders, then filters
    /// for ITarget components that pass tag and attribute validation.
    /// </summary>
    [Serializable]
    public class AutoTargetTask : AbstractGasTargetingAbilityTask
    {
        [Header("Selection")]
        [Tooltip("How targets are selected and sorted")]
        public EAutoTargetMode Mode = EAutoTargetMode.Nearest;

        [Tooltip("Maximum number of targets to select (0 = all matching)")]
        public int MaxTargets = 1;

        [Tooltip("Whether to include the caster as a candidate")]
        public bool IncludeSelf;

        [Header("Spatial")]
        [Tooltip("Detection radius from the caster (0 = infinite, uses FindObjectsByType)")]
        public float Range = 20f;

        [Tooltip("Layers to detect targets on")]
        public LayerMask DetectionLayers = ~0;

        [Header("Attribute Conditions")]
        [Tooltip("Optional attribute conditions targets must satisfy")]
        public List<TargetAttributeCondition> AttributeConditions = new();

        [Header("Alive Filter")]
        [Tooltip("When true, dead targets (IsDead) are excluded")]
        public bool ExcludeDead = true;

        public override string Description => $"Auto-target {Mode}";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetOwner();
            var ownerTransform = owner.ToGAS()?.ToGASObject()?.transform;

            var candidates = GatherCandidates(ownerTransform, owner);
            var valid = FilterCandidates(candidates);
            var sorted = SortCandidates(valid, ownerTransform);
            var selected = SelectTargets(sorted);

            if (selected.Count == 0)
            {
                WhenTargetingInvalid(data);
                return UniTask.CompletedTask;
            }

            // Add all selected targets to data
            foreach (var target in selected)
            {
                data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Gathers raw ITarget candidates from the world.
        /// Uses OverlapSphere when Range > 0, otherwise FindObjectsByType.
        /// </summary>
        private List<ITarget> GatherCandidates(Transform origin, ISource owner)
        {
            var results = new List<ITarget>();

            if (Range > 0f && origin != null)
            {
                var colliders = new Collider[MaxTargets];
                var size = Physics.OverlapSphereNonAlloc(origin.position, Range, colliders, DetectionLayers);
                for (int i = 0; i < size; i++)
                {
                    var targets = colliders[i].GetComponents<ITarget>();
                    foreach (var t in targets)
                    {
                        if (!IncludeSelf && ReferenceEquals(t, owner)) continue;
                        if (!results.Contains(t)) results.Add(t);
                    }
                }
            }
            else
            {
                // Unlimited range — find all ITarget in scene
                // Use GameplayAbilitySystem as proxy since it implements ITarget
                var systems = UnityEngine.Object.FindObjectsByType<GameplayAbilitySystem>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var sys in systems)
                {
                    if (!IncludeSelf && ReferenceEquals(sys, owner)) continue;
                    results.Add(sys);
                }
            }

            return results;
        }

        /// <summary>
        /// Filters candidates by tag requirements, attribute conditions, and alive status.
        /// </summary>
        private List<ITarget> FilterCandidates(List<ITarget> candidates)
        {
            var results = new List<ITarget>();

            foreach (var target in candidates)
            {
                // Alive filter
                if (ExcludeDead && target.IsDead) continue;

                // Tag requirements (from AbstractGasTargetingAbilityTask.TargetIsValid → Requirements.Validate)
                if (!TargetIsValid(target)) continue;

                // Attribute conditions
                if (!PassesAttributeConditions(target)) continue;

                results.Add(target);
            }

            return results;
        }

        /// <summary>
        /// Sorts candidates based on the configured mode.
        /// </summary>
        private List<ITarget> SortCandidates(List<ITarget> candidates, Transform origin)
        {
            if (origin == null || candidates.Count <= 1) return candidates;

            var originPos = origin.position;

            return Mode switch
            {
                EAutoTargetMode.Nearest => candidates
                    .OrderBy(t => DistanceTo(t, originPos))
                    .ToList(),

                EAutoTargetMode.Farthest => candidates
                    .OrderByDescending(t => DistanceTo(t, originPos))
                    .ToList(),

                EAutoTargetMode.Random => candidates
                    .OrderBy(_ => UnityEngine.Random.value)
                    .ToList(),

                // All — no sorting needed
                _ => candidates
            };
        }

        /// <summary>
        /// Selects the final target set from sorted candidates.
        /// </summary>
        private List<ITarget> SelectTargets(List<ITarget> sorted)
        {
            if (MaxTargets <= 0 || MaxTargets >= sorted.Count)
                return sorted;

            return sorted.GetRange(0, MaxTargets);
        }

        /// <summary>
        /// Checks whether a target passes all configured attribute conditions.
        /// </summary>
        private bool PassesAttributeConditions(ITarget target)
        {
            if (AttributeConditions == null || AttributeConditions.Count == 0) return true;

            foreach (var condition in AttributeConditions)
            {
                if (condition.Attribute == null) continue;
                if (!target.TryGetAttributeValue(condition.Attribute, out var value)) return false;
                if (!condition.Evaluate(value.CurrentValue)) return false;
            }

            return true;
        }

        private static float DistanceTo(ITarget target, Vector3 origin)
        {
            var transform = target.GetTargetingPacket();
            if (transform == null) return float.MaxValue;
            return Vector3.Distance(origin, transform.position);
        }

    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SUPPORTING TYPES
    // ═══════════════════════════════════════════════════════════════════════════

    public enum EAutoTargetMode
    {
        /// <summary>Select the N nearest targets within range.</summary>
        Nearest,

        /// <summary>Select the N farthest targets within range.</summary>
        Farthest,

        /// <summary>Select N random targets from those within range.</summary>
        Random,

        /// <summary>Select all matching targets within range.</summary>
        All
    }

    /// <summary>
    /// A single attribute condition used to filter auto-targeting candidates.
    /// Checks the target's current attribute value against a threshold.
    /// </summary>
    [Serializable]
    public class TargetAttributeCondition
    {
        [Tooltip("The attribute to check on the target")]
        public Attribute Attribute;

        [Tooltip("Comparison operator")]
        public EComparisonOperator Operator = EComparisonOperator.GreaterThan;

        [Tooltip("The value to compare against")]
        public float CompareValue;

        /// <summary>
        /// Evaluates whether the given attribute value passes this condition.
        /// </summary>
        public bool Evaluate(float currentValue)
        {
            return Operator switch
            {
                EComparisonOperator.LessThan => currentValue < CompareValue,
                EComparisonOperator.LessOrEqual => currentValue <= CompareValue,
                EComparisonOperator.Equal => Mathf.Approximately(currentValue, CompareValue),
                EComparisonOperator.NotEqual => !Mathf.Approximately(currentValue, CompareValue),
                EComparisonOperator.GreaterOrEqual => currentValue >= CompareValue,
                EComparisonOperator.GreaterThan => currentValue > CompareValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
