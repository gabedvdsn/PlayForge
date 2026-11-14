using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interactive targeting task: raycast to select either a GAS target or a world position.
    /// Prioritizes GAS targets (ITarget) — if the hit object has no ITarget component,
    /// falls back to using the hit position. Tag requirements apply only to GAS targets.
    /// </summary>
    [Serializable]
    [ForgeCategory("Picked")]
    public class SelectGasOrPositionTargetTask : AbstractGasTargetingAbilityTask
    {
        [Tooltip("Layers to raycast against")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("How far its pointer's ray reaches; 0, any distance")]
        [NumberMin(0f)] public ScalableFloat MaxDistance = 0f;

        [Tooltip("When true, a completely invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When the activation's data already holds a target under TargetReal, or a point under Position, as an AI's "
                 + "or a replay's does, take it rather than wait for a click")]
        public bool TakeTargetFromData = true;

        [Tooltip("When true, GAS targets are preferred over position targets. " +
                 "When false, position is always used (even if a GAS target was hit).")]
        public bool PreferGasTarget = true;

        public override string Description => "Raycast to select GAS target or position";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float reach = Read(MaxDistance, data);
            var maxDist = reach > 0 ? reach : Mathf.Infinity;

            // Handed its target or its point, as an AI's activation hands it, it takes it with no click (GAS-66)
            if (TakeTargetFromData)
            {
                if (PreferGasTarget && TryGetDataTarget(data, out var target) && TargetIsValid(target))
                {
                    data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                    return;
                }
                if (TryGetDataPoint(data, out var point) && TargetIsValid(point, out _))
                {
                    data.SetTargetingPacket(Tags.TARGET, new StaticTargetingPacket(point));
                    return;
                }
                if (TryGetDataTarget(data, out var held) && TargetIsValid(held))
                {
                    data.SetTargetingPacket(Tags.TARGET, held.GetTargetingPacket());
                    return;
                }
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

                // Try GAS target first
                if (PreferGasTarget && TargetIsValid(hit.collider.gameObject, out var target))
                {
                    data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                    return;
                }

                // Fall back to position
                if (TargetIsValid(hit.point, out _))
                {
                    data.SetTargetingPacket(Tags.TARGET, new StaticTargetingPacket(hit.point));
                    return;
                }

                // Neither valid
                if (RetryOnInvalid) continue;
                WhenTargetingInvalid(data);
                return;
            }
        }
    }
}
