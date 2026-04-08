using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Test utility targeting task: finds the first "other" GAS entity in the scene.
    /// Not intended for production use — useful for testing ability pipelines
    /// without interactive targeting.
    /// </summary>
    [Serializable]
    public class GetTestTargetTask : AbstractTargetingAbilityTask
    {
        public override string Description => "Find first non-self GAS target in scene (test only)";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var owner = data.Spec.GetOwner();
            var ownerGas = owner.AsGAS();

            var comps = UnityEngine.Object.FindObjectsByType<GameplayAbilitySystem>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var comp in comps)
            {
                if (comp == (GameplayAbilitySystem)ownerGas) continue;

                data.SetPrimary(Tags.TARGET_REAL, comp);
                return UniTask.CompletedTask;
            }

            // No valid target found
            WhenTargetingInvalid(data);
            return UniTask.CompletedTask;
        }

        protected override bool ConnectInputHandler(AbilityDataPacket data) => true;
        protected override void DisconnectInputHandler(AbilityDataPacket data) { }
    }
}
