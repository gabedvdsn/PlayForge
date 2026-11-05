using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interactive targeting task: click to define a direction from the caster.
    /// Raycasts from the camera to determine the world position clicked,
    /// then computes a normalized direction from the caster to that point.
    ///
    /// Stores the direction in Tags.TARGET_POS as a Vector3 and the
    /// clicked world position in Tags.POSITION.
    /// Useful for skill shots, dashes, line attacks, etc.
    /// </summary>
    [Serializable]
    public class SelectDirectionTargetTask : AbstractTargetingAbilityTask
    {
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against when selecting direction")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("Maximum raycast distance (0 = infinite)")]
        public float MaxDistance = Mathf.Infinity;

        [Header("Direction")]
        [Tooltip("When true, the Y component of the direction is zeroed out (horizontal only)")]
        public bool FlattenY = true;

        [Tooltip("Minimum distance from caster to clicked point for a valid direction. " +
                 "Prevents degenerate directions from clicking too close.")]
        public float MinDistance = 0.5f;

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid = true;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        public override string Description => "Click to select a direction from caster";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var maxDist = MaxDistance > 0 ? MaxDistance : Mathf.Infinity;
            var owner = data.EffectOrigin.GetSource();
            // From the caster's targeting packet, which every caster has. Through its transform, a runtime caster, which
            // has none, always failed (GAS-38)
            var ownerPacket = owner?.GetTargetingPacket();

            if (ownerPacket == null)
            {
                WhenTargetingInvalid(data);
                return;
            }

            var origin = ownerPacket.position;

            while (!token.IsCancellationRequested)
            {
                var ray = await WaitForPointerPress(MouseButton, token);
                if (ray == null || !Physics.Raycast(ray.Value, out var hit, maxDist, RaycastLayers))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                var clickPos = hit.point;
                var toTarget = clickPos - origin;

                if (FlattenY) toTarget.y = 0f;

                if (toTarget.sqrMagnitude < MinDistance * MinDistance)
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                var direction = toTarget.normalized;

                var targeting = new StaticTargetingPacket(hit.point)
                {
                    direction = direction
                };
                data.SetTargetingPacket(Tags.TARGET, targeting);
                return;
            }
        }
    }
}
