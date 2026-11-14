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
        [Tooltip("Layers to raycast against when selecting direction")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("How far its pointer's ray reaches; 0, any distance")]
        [NumberMin(0f)] public ScalableFloat MaxDistance = 0f;

        [Tooltip("When true, the Y component of the direction is zeroed out (horizontal only)")]
        public bool FlattenY = true;

        [Tooltip("Minimum distance from caster to clicked point for a valid direction. " +
                 "Prevents degenerate directions from clicking too close.")]
        [NumberMin(0f)] public ScalableFloat MinDistance = 0.5f;

        [Tooltip("When true, an invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid = true;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When the activation's data already holds a point under Position, or a target under TargetReal, as an AI's "
                 + "or a replay's does, aim at it rather than wait for a click")]
        public bool TakeTargetFromData = true;

        public override string Description => "Click to select a direction from caster";

        // Where the data aims it: its point, or else its target's place (GAS-66)
        private static bool TryGetDataAim(AbilityDataPacket data, out Vector3 point)
        {
            if (TryGetDataPoint(data, out point)) return true;
            if (TryGetDataTarget(data, out var target) && target.GetTargetingPacket() is { } packet)
            {
                point = packet.position;
                return true;
            }
            point = default;
            return false;
        }

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float reach = Read(MaxDistance, data);
            var maxDist = reach > 0 ? reach : Mathf.Infinity;
            float least = Read(MinDistance, data);
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

            // Handed a point or a target, as an AI's activation hands it, it aims at it with no click (GAS-66)
            if (TakeTargetFromData && TryGetDataAim(data, out var aimAt))
            {
                var toward = aimAt - origin;
                if (FlattenY) toward.y = 0f;
                if (toward.sqrMagnitude < least * least)
                {
                    WhenTargetingInvalid(data);
                    return;
                }
                data.SetTargetingPacket(Tags.TARGET, new StaticTargetingPacket(aimAt) { direction = toward.normalized });
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

                var clickPos = hit.point;
                var toTarget = clickPos - origin;

                if (FlattenY) toTarget.y = 0f;

                if (toTarget.sqrMagnitude < least * least)
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
