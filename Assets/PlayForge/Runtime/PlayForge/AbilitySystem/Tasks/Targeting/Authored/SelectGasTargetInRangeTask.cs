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

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid = true;

        [Tooltip("Mouse button index to use for selection (0=Left, 1=Right, 2=Middle)")]
        public int MouseButton;

        public override string Description => $"Select GAS target within {MaxTargetRange}m";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var cam = Camera.main;
            var maxDist = MaxRayDistance > 0 ? MaxRayDistance : Mathf.Infinity;
            var owner = data.EffectOrigin.GetOwner();
            var ownerTransform = owner.ToGAS()?.ToGASObject()?.transform;

            if (ownerTransform == null)
            {
                WhenTargetingInvalid(data);
                return;
            }

            while (!token.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => Input.GetMouseButtonDown(MouseButton), cancellationToken: token);

                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (!Physics.Raycast(ray, out var hit, maxDist, RaycastLayers))
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
                if (!IsWithinRange(ownerTransform, target))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                return;
            }
        }

        private bool IsWithinRange(Transform caster, ITarget target)
        {
            var targetTransform = target.GetTargetingPacket();
            if (targetTransform == null) return false;

            var delta = targetTransform.position - caster.position;
            if (FlattenRangeCheck) delta.y = 0f;

            return delta.sqrMagnitude <= MaxTargetRange * MaxTargetRange;
        }
    }
}
