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
    [ForgeCategory("Automatic")]
    public class AutoTargetByAffinityTask : AbstractGasTargetingAbilityTask
    {
        [Tooltip("Which affiliations to include relative to the caster")]
        public EAffinityFilter AffinityFilter = EAffinityFilter.Enemies;

        [Tooltip("How targets are selected and sorted")]
        public EAutoTargetMode Mode = EAutoTargetMode.Nearest;

        [Tooltip("Maximum number of targets to select (0 = all matching)")]
        [NumberMin(0f)] public ScalableInt MaxTargets = 1;

        [Tooltip("Detection radius from the caster (0 = infinite)")]
        [NumberMin(0f)] public ScalableFloat Range = 20f;

        [Tooltip("Searches within its ability's Cast Range, where the ability has one and its range is measured from the " +
                 "caster; off, within its own Range (FN-14)")]
        public bool UseCastRange = true;

        [Tooltip("Layers to detect targets on, where it uses physics")]
        [ShownWhen(nameof(UsePhysics))]
        public LayerMask DetectionLayers = ~0;

        [Tooltip("Where its range is measured from: the caster; the activation's target, as an AI hands it or a task before " +
                 "it in a Composite picks it; or the activation's point (TW-10)")]
        public EAreaCentre Around = EAreaCentre.Caster;

        [Tooltip("Measures its range along the ground, leaving height out; off, a sphere (TW-10)")]
        public bool Flat = true;

        [Tooltip("Finds entities with physics, by collider on its Detection Layers, as well as by position. Off, each entity " +
                 "on either hub is found by its position, with no collider needed (TW-10)")]
        public bool UsePhysics;

        [Tooltip("Whether the caster is a candidate, where it's of the sides it takes (TW-10)")]
        public bool IncludeSelf;

        [Tooltip("When true, dead targets are excluded")]
        public bool ExcludeDead = true;

        public override string Description => $"Auto-target {AffinityFilter} ({Mode})";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetSource();
            // From the caster's targeting packet, which every caster has. From its transform, a runtime caster, which has
            // none, searched the whole scene whatever its range, and sorted nothing (GAS-38)
            var origin = owner?.GetTargetingPacket();
            var ownerAffiliation = (owner as ITarget)?.GetAffiliation();

            // Measured from the caster, the activation's target or its point (TW-10)
            var centre = CentreOf(data, origin);
            var candidates = GatherCandidates(centre, owner, RangeFor(data));
            var filtered = FilterByAffinity(candidates, ownerAffiliation);
            var valid = FilterValid(filtered);
            var sorted = SortCandidates(valid, centre);
            var selected = SelectTargets(sorted, Read(MaxTargets, data));

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

        private List<ITarget> GatherCandidates(Vector3? centre, ISource owner, float range)
        {
            // Every live entity, on both hubs, by its position, with no collider needed; physics where it asks, on its
            // layers (TW-10). Its caster where it asks: it was always left out
            var results = new List<ITarget>();
            if (UsePhysics) ForgeEntities.Around(centre, range, DetectionLayers, owner, IncludeSelf, results);
            else ForgeEntities.Within(centre ?? Vector3.zero, centre.HasValue ? range : 0f, Flat, results, owner, IncludeSelf);
            return results;
        }

        // How far it searches: its ability's Cast Range, measured from the caster, where it reads it; else its own (FN-14)
        private float RangeFor(AbilityDataPacket data) =>
            UseCastRange && Around == EAreaCentre.Caster && ForgeCastRange.TryRead(data, out float cast) ? cast : Read(Range, data);

        // Where its range is measured from: the caster, the activation's target, or its point (TW-10)
        private Vector3? CentreOf(AbilityDataPacket data, AbstractTargetingPacket caster) => Around switch
        {
            EAreaCentre.Target when TryGetDataTarget(data, out var target) && target.GetTargetingPacket() is { } packet => packet.position,
            EAreaCentre.Point when TryGetDataPoint(data, out var point) => point,
            _ => caster?.position,
        };

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

        private List<ITarget> SortCandidates(List<ITarget> candidates, Vector3? centre)
        {
            if (!centre.HasValue || candidates.Count <= 1) return candidates;
            var originPos = centre.Value;

            return Mode switch
            {
                EAutoTargetMode.Nearest => candidates.OrderBy(t => DistanceTo(t, originPos)).ToList(),
                EAutoTargetMode.Farthest => candidates.OrderByDescending(t => DistanceTo(t, originPos)).ToList(),
                EAutoTargetMode.Random => candidates.OrderBy(_ => ForgeRandom.Value).ToList(),
                _ => candidates
            };
        }

        private static List<ITarget> SelectTargets(List<ITarget> sorted, int most)
        {
            if (most <= 0 || most >= sorted.Count) return sorted;
            return sorted.GetRange(0, most);
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

        // Flat where its range is, so its nearest is the one its search measured nearest (TW-10)
        private float DistanceTo(ITarget target, Vector3 origin)
        {
            return ForgeEntities.Distance(target, origin, Flat);
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
