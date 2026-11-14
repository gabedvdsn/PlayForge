using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interactive targeting task: raycast to select a world position.
    /// Waits for the player to click on a valid surface in the world.
    /// Stores the hit position in the data packet under Tags.POSITION.
    /// </summary>
    [Serializable]
    public class SelectPositionTargetTask : AbstractTargetingAbilityTask
    {
        [Tooltip("Layers to raycast against when selecting positions")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("How far its pointer's ray reaches; 0, any distance")]
        [NumberMin(0f)] public ScalableFloat MaxDistance = 0f;

        [Tooltip("When true, an invalid click re-prompts for another click instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("The pointer button that selects, as the game's input reads it: 0 the primary (left, or a touch), "
                 + "1 the secondary (right), 2 the middle")]
        public int MouseButton;

        [Tooltip("When the activation's data already holds a point under Position, as an AI's or a replay's does, take it "
                 + "rather than wait for a click")]
        public bool TakeTargetFromData = true;

        public override string Description => "Raycast to select a ground/position target";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float reach = Read(MaxDistance, data);
            var maxDist = reach > 0 ? reach : Mathf.Infinity;

            // Handed its point, as an AI's activation hands it, it takes it with no click (GAS-66)
            if (TakeTargetFromData && TryGetDataPoint(data, out var given))
            {
                if (TargetIsValid(given, out _)) data.SetTargetingPacket(Tags.TARGET, new StaticTargetingPacket(given));
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

                if (!TargetIsValid(hit.point, out _))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                // Valid position acquired
                data.SetTargetingPacket(Tags.TARGET, new StaticTargetingPacket(hit.point));
                return;
            }
        }
    }
}
