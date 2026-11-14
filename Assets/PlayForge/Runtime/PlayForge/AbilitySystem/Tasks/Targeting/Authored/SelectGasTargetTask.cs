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
        [Tooltip("Layers to raycast against when selecting targets")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("How far its pointer's ray reaches; 0, any distance")]
        [NumberMin(0f)] public ScalableFloat MaxDistance = 0f;

        [Tooltip("When true, an invalid click re-prompts for another click instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When the activation's data already holds a target under TargetReal, as an AI's or a replay's does, take "
                 + "it rather than wait for a click")]
        public bool TakeTargetFromData = true;

        public override string Description => "Raycast to select a GAS target";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float reach = Read(MaxDistance, data);
            var maxDist = reach > 0 ? reach : Mathf.Infinity;

            // Handed its target, as an AI's activation hands it, it takes it with no click (GAS-66)
            if (TakeTargetFromData && TryGetDataTarget(data, out var given))
            {
                if (TargetIsValid(given)) data.SetTargetingPacket(Tags.TARGET, given.GetTargetingPacket());
                else WhenTargetingInvalid(data);
                return;
            }

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
