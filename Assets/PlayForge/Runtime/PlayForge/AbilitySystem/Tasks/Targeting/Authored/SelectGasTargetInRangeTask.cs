using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interactive targeting task: click to select a GAS target within range.
    /// Like SelectGasTargetTask, but adds a maximum distance check from the caster
    /// to the clicked target. Invalid targets outside range trigger retry or failure.
    ///
    /// Useful for melee abilities, short-range spells, or any ability where
    /// the player must select a target within a specific radius.
    /// </summary>
    [Serializable]
    public class SelectGasTargetInRangeTask : AbstractGasTargetingAbilityTask
    {
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against when selecting targets")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("Maximum raycast distance for the camera ray (0 = infinite)")]
        public float MaxRayDistance = Mathf.Infinity;

        [Header("Range")]
        [Tooltip("Maximum allowed distance from caster to selected target")]
        public float MaxTargetRange = 10f;

        [Tooltip("When true, the range check ignores the Y axis (horizontal distance only)")]
        public bool FlattenRangeCheck;

        [Tooltip("Its range is its ability's Cast Range, flat as the Cast Range says, where the ability has one; off, its own " +
                 "Max Target Range (FN-14)")]
        public bool UseCastRange = true;

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid = true;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When the activation's data already holds a target under TargetReal, as an AI's or a replay's does, take "
                 + "it rather than wait for a click")]
        public bool TakeTargetFromData = true;

        public override string Description => $"Select GAS target within {MaxTargetRange}m";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var maxDist = MaxRayDistance > 0 ? MaxRayDistance : Mathf.Infinity;
            var owner = data.EffectOrigin.GetSource();
            // From the caster's targeting packet, which every caster has. Through its transform, a runtime caster, which
            // has none, always failed (GAS-38)
            var ownerPacket = owner?.GetTargetingPacket();

            if (ownerPacket == null)
            {
                WhenTargetingInvalid(data);
                return;
            }

            // Handed its target, as an AI's activation hands it, it takes it with no click, if it's valid and in range
            // (GAS-66)
            if (TakeTargetFromData && TryGetDataTarget(data, out var given))
            {
                if (TargetIsValid(given) && IsWithinRange(data, ownerPacket, given)) data.SetTargetingPacket(Tags.TARGET, given.GetTargetingPacket());
                else WhenTargetingInvalid(data);
                return;
            }

            while (!token.IsCancellationRequested)
            {
                var ray = await WaitForPointerPress(MouseButton, token);
                if (ray == null || !Physics.Raycast(ray.Value, out var hit, maxDist, RaycastLayers))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                if (!TargetIsValid(hit.collider.gameObject, out var target))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                // Range check
                if (!IsWithinRange(data, ownerPacket, target))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                return;
            }
        }

        // Within its range: its ability's Cast Range where it reads it (FN-14), else its own
        private bool IsWithinRange(AbilityDataPacket data, AbstractTargetingPacket caster, ITarget target)
        {
            var targetTransform = target.GetTargetingPacket();
            if (targetTransform == null) return false;

            float castRange = 0f;
            bool castFlat = false;
            bool cast = UseCastRange && ForgeCastRange.TryRead(data, out castRange, out castFlat);
            float range = cast ? castRange : MaxTargetRange;
            var delta = targetTransform.position - caster.position;
            if (cast ? castFlat : FlattenRangeCheck) delta.y = 0f;

            return delta.sqrMagnitude <= range * range;
        }
    }
}
