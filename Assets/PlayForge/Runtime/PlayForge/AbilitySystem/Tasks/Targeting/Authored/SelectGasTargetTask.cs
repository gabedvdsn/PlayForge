using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interactive targeting task: raycast to select a GAS target (ITarget).
    /// Waits for the player to click on a valid target in the world.
    /// Validates the hit object has an ITarget component and passes tag requirements.
    /// </summary>
    [Serializable]
    public class SelectGasTargetTask : AbstractGasTargetingAbilityTask
    {
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against when selecting targets")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("Maximum raycast distance (0 = infinite)")]
        public float MaxDistance;

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts for another click instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("Mouse button index to use for selection (0=Left, 1=Right, 2=Middle)")]
        public int MouseButton;

        public override string Description => "Raycast to select a GAS target";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var cam = Camera.main;
            var maxDist = MaxDistance > 0 ? MaxDistance : Mathf.Infinity;

            while (!token.IsCancellationRequested)
            {
                // Wait for a click
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

                // Valid target acquired
                data.SetPrimary(Tags.TARGET_REAL, target);
                return;
            }
        }
    }
}
