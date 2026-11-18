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
    [ForgeMenu("Automatic", Description = "Targets the entities in range of an affiliation, as the nearest ally to heal or every enemy in reach.")]
    public class AutoTargetByAffinityTask : AbstractGasTargetingAbilityTask
    {
        [Tooltip("Whom it takes, by how each stands to its caster: its enemies, its allies, its other allies, itself, everyone else or everyone. A caster with no sides takes everyone as its allies and enemies")]
        public EAffiliationFilter Takes = EAffiliationFilter.Enemies;

        [Tooltip("How targets are selected and sorted")]
        public EAutoTargetMode Mode = EAutoTargetMode.Nearest;

        [Tooltip("Maximum number of targets to select (0 = all matching)")]
        [NumberMin(0f)] public ScalableInt MaxTargets = 1;

        [Tooltip("Detection radius from the caster (0 = infinite)")]
        [NumberMin(0f)] public ScalableFloat Range = 20f;

        [Tooltip("Searches within its ability's reach, its Cast Range or a reach of your own, where the ability has one and its " +
                 "range is measured from the caster; off, within its own Range")]
        public bool UseCastRange = true;

        [Tooltip("Layers to detect targets on, where it uses physics")]
        [ShownWhen(nameof(UsePhysics))]
        public LayerMask DetectionLayers = ~0;

        [Tooltip("Where its range is measured from: the caster; the activation's target, as an AI hands it or a task before " +
                 "it in a Composite picks it; or the activation's point")]
        public EAreaCentre Around = EAreaCentre.Caster;

        [Tooltip("Measures its range along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        [Tooltip("Finds entities with physics, by collider on its Detection Layers, as well as by position. Off, each entity " +
                 "on either hub is found by its position, with no collider needed")]
        public bool UsePhysics;

        [Tooltip("When true, dead targets are excluded")]
        public bool ExcludeDead = true;

        public override string Description => $"Auto-target {Takes} ({Mode})";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.EffectOrigin.GetSource();
            // From the caster's targeting packet, which every caster has. From its transform, a runtime caster, which has
            // none, searched the whole scene whatever its range, and sorted nothing
            var origin = owner?.GetTargetingPacket();
            var ownerAffiliation = (owner as ITarget)?.GetAffiliation();

            // Measured from the caster, the activation's target or its point
            var centre = CentreOf(data, origin);
            var candidates = GatherCandidates(centre, owner, RangeFor(data));
            var filtered = FilterByAffinity(candidates, owner, ownerAffiliation);
            var valid = FilterValid(filtered);
            var sorted = SortCandidates(valid, centre);
            var selected = SelectTargets(sorted, Read(MaxTargets, data));

            if (selected.Count == 0)
            {
                WhenTargetingInvalid(data);
                return UniTask.CompletedTask;
            }

            // Every target selected, in order, the first as the primary. Each was set as the primary, so each replaced the
            // last, and the last alone was kept
            data.SetTargetingPackets(Tags.TARGET, selected);

            return UniTask.CompletedTask;
        }

        private List<ITarget> GatherCandidates(Vector3? centre, ISource owner, float range)
        {
            // Every live entity, on both hubs, by its position, with no collider needed; physics where it asks, on its
            // layers. Its caster among them, which its filter takes or leaves
            var results = new List<ITarget>();
            if (UsePhysics) ForgeEntities.Around(centre, range, DetectionLayers, owner, true, results);
            else ForgeEntities.Within(centre ?? Vector3.zero, centre.HasValue ? range : 0f, Flat, results, owner);
            return results;
        }

        // How far it searches: its ability's reach, its Cast Range or another, measured from the caster, where it reads it;
        // else its own
        private float RangeFor(AbilityDataPacket data) =>
            UseCastRange && Around == EAreaCentre.Caster && ForgeReach.TryRead(data, out float cast) ? cast : Read(Range, data);

        // Where its range is measured from: the caster, the activation's target, or its point
        private Vector3? CentreOf(AbilityDataPacket data, AbstractTargetingPacket caster) => Around switch
        {
            EAreaCentre.Target when TryGetDataTarget(data, out var target) && target.GetTargetingPacket() is { } packet => packet.position,
            EAreaCentre.Point when TryGetDataPoint(data, out var point) => point,
            _ => caster?.position,
        };

        // Those it takes, by how each stands to its caster
        private List<ITarget> FilterByAffinity(List<ITarget> candidates, ISource owner, List<Tag> ownerAffiliation)
        {
            var results = new List<ITarget>();
            foreach (var target in candidates)
                if (ForgeEntities.Takes(Takes, owner, ownerAffiliation, target)) results.Add(target);
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

        // Flat where its range is, so its nearest is the one its search measured nearest
        private float DistanceTo(ITarget target, Vector3 origin)
        {
            return ForgeEntities.Distance(target, origin, Flat);
        }

        protected override bool ConnectInputHandler(AbilityDataPacket data) => true;
        protected override void DisconnectInputHandler(AbilityDataPacket data) { }
    }

}
