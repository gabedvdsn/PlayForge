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

        [Tooltip("Layers to detect targets on, where it uses physics")]
        public LayerMask DetectionLayers = ~0;

        [Tooltip("Where its range is measured from: the caster; the activation's target, as an AI hands it or a task before " +
                 "it in a Composite picks it; or the activation's point (TW-10)")]
        public EAreaCentre Around = EAreaCentre.Caster;

        [Tooltip("Measures its range along the ground, leaving height out; off, a sphere (TW-10)")]
        public bool Flat = true;

        [Tooltip("Finds entities with physics, by collider on its Detection Layers, as well as by position. Off, each entity " +
                 "on either hub is found by its position, with no collider needed (TW-10)")]
        public bool UsePhysics;

        [Header("Attribute Conditions")]
        [Tooltip("Optional attribute conditions targets must satisfy")]
        public List<TargetAttributeCondition> AttributeConditions = new();

        [Header("Alive Filter")]
        [Tooltip("When true, dead targets (IsDead) are excluded")]
        public bool ExcludeDead = true;

        public override string Description => $"Auto-target {Mode}";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetSource();
            // From the caster's targeting packet, which every caster has. From its transform, a runtime caster, which has
            // none, searched the whole scene whatever its range, and sorted nothing (GAS-38)
            var origin = owner?.GetTargetingPacket();

            // Measured from the caster, the activation's target or its point (TW-10)
            var centre = CentreOf(data, origin);
            var candidates = GatherCandidates(centre, owner);
            var valid = FilterCandidates(candidates);
            var sorted = SortCandidates(valid, centre);
            var selected = SelectTargets(sorted);

            if (selected.Count == 0)
            {
                WhenTargetingInvalid(data);
                return UniTask.CompletedTask;
            }

            // Every target selected, in order, the first as the primary. Each was set as the primary, so each replaced the
            // last, and the last alone was kept (GAS-62)
            data.SetTargetingPackets(Tags.TARGET, selected);

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Gathers raw ITarget candidates from the world: every live entity, or with a range, what physics finds and each
        /// runtime entity within it (<see cref="ForgeEntities.Around"/>).
        /// </summary>
        private List<ITarget> GatherCandidates(Vector3? centre, ISource owner)
        {
            // Every live entity, on both hubs, by its position, with no collider needed; physics where it asks, on its
            // layers (TW-10)
            var results = new List<ITarget>();
            if (UsePhysics) ForgeEntities.Around(centre, Range, DetectionLayers, owner, IncludeSelf, results);
            else ForgeEntities.Within(centre ?? Vector3.zero, centre.HasValue ? Range : 0f, Flat, results, owner, IncludeSelf);
            return results;
        }

        // Where its range is measured from: the caster, the activation's target, or its point (TW-10)
        private Vector3? CentreOf(AbilityDataPacket data, AbstractTargetingPacket caster) => Around switch
        {
            EAreaCentre.Target when TryGetDataTarget(data, out var target) && target.GetTargetingPacket() is { } packet => packet.position,
            EAreaCentre.Point when TryGetDataPoint(data, out var point) => point,
            _ => caster?.position,
        };

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
        private List<ITarget> SortCandidates(List<ITarget> candidates, Vector3? centre)
        {
            if (!centre.HasValue || candidates.Count <= 1) return candidates;

            var originPos = centre.Value;

            return Mode switch
            {
                EAutoTargetMode.Nearest => candidates
                    .OrderBy(t => DistanceTo(t, originPos))
                    .ToList(),

                EAutoTargetMode.Farthest => candidates
                    .OrderByDescending(t => DistanceTo(t, originPos))
                    .ToList(),

                EAutoTargetMode.Random => candidates
                    .OrderBy(_ => ForgeRandom.Value)
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

        // Flat where its range is, so its nearest is the one its search measured nearest (TW-10)
        private float DistanceTo(ITarget target, Vector3 origin)
        {
            return ForgeEntities.Distance(target, origin, Flat);
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
