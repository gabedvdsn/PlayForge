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
        [Header("Raycast Settings")]
        [Tooltip("Layers to raycast against when selecting positions")]
        public LayerMask RaycastLayers = ~0;

        [Tooltip("Maximum raycast distance (0 = infinite)")]
        public float MaxDistance;

        [Header("Behaviour")]
        [Tooltip("When true, an invalid click re-prompts for another click instead of failing")]
        public bool RetryOnInvalid;

        [Tooltip("Mouse button index to use for selection (0=Left, 1=Right, 2=Middle)")]
        public int MouseButton;

        public override string Description => "Raycast to select a ground/position target";

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

                if (!TargetIsValid(hit.point, out var position))
                {
                    if (RetryOnInvalid) continue;
                    WhenTargetingInvalid(data);
                    return;
                }

                // Valid position acquired
                data.AddPayload(Tags.POSITION, position);
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
