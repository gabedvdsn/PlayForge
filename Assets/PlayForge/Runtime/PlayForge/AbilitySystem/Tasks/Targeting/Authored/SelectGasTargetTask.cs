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
        public float MaxDistance = Mathf.Infinity;

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts for another click instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        public override string Description => "Raycast to select a GAS target";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var maxDist = MaxDistance > 0 ? MaxDistance : Mathf.Infinity;

            while (!token.IsCancellationRequested)
            {
                // Wait for a click
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

                // Valid target acquired
                data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                return;
            }
        }
    }
}
