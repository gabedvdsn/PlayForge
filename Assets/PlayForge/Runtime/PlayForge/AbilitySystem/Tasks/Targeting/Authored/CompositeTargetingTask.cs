using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runs multiple targeting tasks in sequence, accumulating all their results
    /// into the same data packet. Each sub-task runs to completion before the next begins.
    ///
    /// Common use cases:
    /// - Select a target entity, then select a position (teleport target to location)
    /// - Select multiple distinct targets across phases
    /// - Auto-target self + select an enemy (buff self, then apply debuff)
    ///
    /// If any sub-task fails and StopOnFailure is true, the entire composite fails.
    /// </summary>
    [Serializable]
    public class CompositeTargetingTask : AbstractTargetingAbilityTask
    {
        [Tooltip("Ordered list of targeting tasks to execute sequentially")]
        [SerializeReference]
        public List<AbstractTargetingAbilityTask> Tasks = new();

        [Tooltip("When true, failure in any sub-task stops the entire sequence. " +
                 "When false, failed sub-tasks are skipped and the rest continue.")]
        public bool StopOnFailure = true;

        public override string Description
        {
            get
            {
                if (Tasks == null || Tasks.Count == 0) return "Composite (empty)";
                return $"Composite ({Tasks.Count} steps)";
            }
        }

        public override void Prepare(AbilityDataPacket data)
        {
            // Prepare each sub-task
            foreach (var task in Tasks)
            {
                if (task == null) continue;
                task.Prepare(data);
            }
        }

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            foreach (var task in Tasks)
            {
                if (task == null) continue;

                // Reset the failure flag before each sub-task
                data.TargetingFailed = false;

                await task.Activate(data, token);

                if (data.TargetingFailed && StopOnFailure)
                {
                    WhenTargetingInvalid(data);
                    return;
                }
            }
        }

        public override void Clean(AbilityDataPacket data)
        {
            foreach (var task in Tasks)
            {
                if (task == null) continue;
                task.Clean(data);
            }
        }
        
    }
}
