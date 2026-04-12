using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Auto-targeting task that filters by affiliation (ally, enemy, neutral).
    /// Uses OverlapSphere to gather ITarget candidates, then filters
    /// by matching or opposing affiliation tags relative to the caster.
    ///
    /// Common use cases:
    /// - Heal nearest ally
    /// - AoE damage to enemies only
    /// - Buff all allies in range
    /// </summary>
    [Serializable]
    public class AutoTargetByAffinityTask : AbstractGasTargetingAbilityTask
    {
        [Header("Affiliation")]
        [Tooltip("Which affiliations to include relative to the caster")]
        public EAffinityFilter AffinityFilter = EAffinityFilter.Enemies;

        [Header("Selection")]
        [Tooltip("How targets are selected and sorted")]
        public EAutoTargetMode Mode = EAutoTargetMode.Nearest;

        [Tooltip("Maximum number of targets to select (0 = all matching)")]
        public int MaxTargets = 1;

        [Header("Spatial")]
        [Tooltip("Detection radius from the caster (0 = infinite)")]
        public float Range = 20f;

        [Tooltip("Layers to detect targets on")]
        public LayerMask DetectionLayers = ~0;

        [Header("Filters")]
        [Tooltip("When true, dead targets are excluded")]
        public bool ExcludeDead = true;

        public override string Description => $"Auto-target {AffinityFilter} ({Mode})";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetOwner();
            var ownerTransform = owner.ToGAS()?.ToGASObject()?.transform;
            var ownerAffiliation = (owner as ITarget)?.GetAffiliation();

            var candidates = GatherCandidates(ownerTransform, owner);
            var filtered = FilterByAffinity(candidates, ownerAffiliation);
            var valid = FilterValid(filtered);
            var sorted = SortCandidates(valid, ownerTransform);
            var selected = SelectTargets(sorted);

            if (selected.Count == 0)
            {
                WhenTargetingInvalid(data);
                return UniTask.CompletedTask;
            }

            foreach (var target in selected)
            {
                data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
            }

            return UniTask.CompletedTask;
        }

        private List<ITarget> GatherCandidates(Transform origin, ISource owner)
        {
            var results = new List<ITarget>();

            if (Range > 0f && origin != null)
            {
                var colliders = Physics.OverlapSphere(origin.position, Range, DetectionLayers);
                foreach (var col in colliders)
                {
                    var targets = col.GetComponents<ITarget>();
                    foreach (var t in targets)
                    {
                        if (ReferenceEquals(t, owner)) continue;
                        if (!results.Contains(t)) results.Add(t);
                    }
                }
            }
            else
            {
                var systems = UnityEngine.Object.FindObjectsByType<GameplayAbilitySystem>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var sys in systems)
                {
                    if (ReferenceEquals(sys, owner)) continue;
                    results.Add(sys);
                }
            }

            return results;
        }

        private List<ITarget> FilterByAffinity(List<ITarget> candidates, List<Tag> ownerAffiliation)
        {
            if (ownerAffiliation == null || ownerAffiliation.Count == 0)
                return candidates; // No affiliation data — can't filter

            var results = new List<ITarget>();

            foreach (var target in candidates)
            {
                var targetAffiliation = target.GetAffiliation();
                bool sharesAffiliation = HasOverlap(ownerAffiliation, targetAffiliation);

                bool include = AffinityFilter switch
                {
                    EAffinityFilter.Allies => sharesAffiliation,
                    EAffinityFilter.Enemies => !sharesAffiliation,
                    EAffinityFilter.AlliesAndEnemies => true,
                    _ => true
                };

                if (include) results.Add(target);
            }

            return results;
        }

        private List<ITarget> FilterValid(List<ITarget> candidates)
        {
            var results = new List<ITarget>();
            foreach (var target in candidates)
            {
                if (ExcludeDead && target.IsDead) continue;
                if (!TargetIsValid(target)) continue;
                results.Add(target);
            }
            return results;
        }

        private List<ITarget> SortCandidates(List<ITarget> candidates, Transform origin)
        {
            if (origin == null || candidates.Count <= 1) return candidates;
            var originPos = origin.position;

            return Mode switch
            {
                EAutoTargetMode.Nearest => candidates.OrderBy(t => DistanceTo(t, originPos)).ToList(),
                EAutoTargetMode.Farthest => candidates.OrderByDescending(t => DistanceTo(t, originPos)).ToList(),
                EAutoTargetMode.Random => candidates.OrderBy(_ => UnityEngine.Random.value).ToList(),
                _ => candidates
            };
        }

        private List<ITarget> SelectTargets(List<ITarget> sorted)
        {
            if (MaxTargets <= 0 || MaxTargets >= sorted.Count) return sorted;
            return sorted.GetRange(0, MaxTargets);
        }

        private static bool HasOverlap(List<Tag> a, List<Tag> b)
        {
            if (a == null || b == null) return false;
            foreach (var tag in a)
            {
                if (b.Contains(tag)) return true;
            }
            return false;
        }

        private static float DistanceTo(ITarget target, Vector3 origin)
        {
            var transform = target.GetTargetingPacket();
            if (transform == null) return float.MaxValue;
            return Vector3.Distance(origin, transform.position);
        }

        protected override bool ConnectInputHandler(AbilityDataPacket data) => true;
        protected override void DisconnectInputHandler(AbilityDataPacket data) { }
    }

    /// <summary>
    /// Determines which affiliations to include when filtering targets.
    /// </summary>
    public enum EAffinityFilter
    {
        /// <summary>Only targets that share affiliation tags with the caster.</summary>
        Allies,

        /// <summary>Only targets that do NOT share affiliation tags with the caster.</summary>
        Enemies,

        /// <summary>All targets regardless of affiliation.</summary>
        AlliesAndEnemies
    }
}
