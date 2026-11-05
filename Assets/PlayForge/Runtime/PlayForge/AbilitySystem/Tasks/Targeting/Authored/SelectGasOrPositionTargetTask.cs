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
    public class SelectGasOrPositionTargetTask : AbstractGasTargetingAbilityTask
    {
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("Maximum raycast distance (0 = infinite)")]
        public float MaxDistance = Mathf.Infinity;

        [Header("Behaviour")]
        [Tooltip("When true, a completely invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When true, GAS targets are preferred over position targets. " +
                 "When false, position is always used (even if a GAS target was hit).")]
        public bool PreferGasTarget = true;

        public override string Description => "Raycast to select GAS target or position";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var maxDist = MaxDistance > 0 ? MaxDistance : Mathf.Infinity;

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
