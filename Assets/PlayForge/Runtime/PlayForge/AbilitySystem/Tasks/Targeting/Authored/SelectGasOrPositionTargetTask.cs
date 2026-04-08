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
        public float MaxDistance;

        [Header("Behaviour")]
        [Tooltip("When true, a completely invalid click re-prompts instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("Mouse button index to use for selection (0=Left, 1=Right, 2=Middle)")]
        public int MouseButton;

        [Tooltip("When true, GAS targets are preferred over position targets. " +
                 "When false, position is always used (even if a GAS target was hit).")]
        public bool PreferGasTarget = true;

        public override string Description => "Raycast to select GAS target or position";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var cam = Camera.main;
            var maxDist = MaxDistance > 0 ? MaxDistance : Mathf.Infinity;

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

                // Try GAS target first
                if (PreferGasTarget && TargetIsValid(hit.collider.gameObject, out var target))
                {
                    data.SetPrimary(Tags.TARGET_REAL, target);
                    return;
                }

                // Fall back to position
                if (TargetIsValid(hit.point, out var position))
                {
                    data.AddPayload(Tags.POSITION, position);
                    return;
                }

                // Neither valid
                if (RetryOnInvalid) continue;
                WhenTargetingInvalid(data);
                return;
            }
        }

        protected override bool ConnectInputHandler(AbilityDataPacket data)
        {
            return true;
        }

        protected override void DisconnectInputHandler(AbilityDataPacket data)
        {
        }
    }
}
