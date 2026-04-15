using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class RunSequenceTask : AbstractAbilityTask
    {
        [FormerlySerializedAs("SequenceReference")] public TaskSequenceReference Sequence;

        /// <summary>
        /// True if the referenced sequence has any critical sections — either the whole sequence
        /// is marked critical via builder-level WithCriticalFlag, or any individual stage is.
        /// This drives the outer compiled ability stage's critical flag.
        /// </summary>
        public override bool IsCriticalSection =>
            Sequence.GetActiveSequence() is TaskSequence ts &&
            ((ts.Definition.Metadata?.IsCritical ?? false) ||
             ts.Definition.Stages.Any(s => s.Metadata?.IsCritical ?? false));

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var seq = Sequence.GetActiveSequence();
            if (seq is null) return;

            if (seq is TaskSequence taskSeq)
            {
                bool hasCritical = (taskSeq.Definition.Metadata?.IsCritical ?? false) ||
                                   taskSeq.Definition.Stages.Any(s => s.Metadata?.IsCritical ?? false);

                // Synchronous lifecycle: register with ProcessControl.
                if (taskSeq.Definition.Metadata?.Lifecycle == EProcessLifecycle.Synchronous)
                {
                    if (hasCritical)
                    {
                        // Wire critical section exit before registering so the callback
                        // propagates into the runtime when ProcessControl starts the sequence.
                        bool criticalDone = false;
                        taskSeq.OnCriticalSectionExited = () => criticalDone = true;

                        ProcessControl.Register(taskSeq, data, out var relay);

                        // Return as soon as critical sections exit OR the process terminates.
                        await UniTask.WaitUntil(
                            () => criticalDone || !relay.ProcessActive,
                            cancellationToken: token);
                        return;
                    }

                    ProcessControl.Register(taskSeq, data, out var syncRelay);
                    await UniTask.WaitWhile(() => syncRelay.ProcessActive, cancellationToken: token);
                    return;
                }

                // Async lifecycle with critical sections: start the sequence in the
                // background and return as soon as the critical sections are done.
                // The non-critical tail continues running until the ability token is cancelled.
                if (hasCritical)
                {
                    bool criticalDone = false;
                    taskSeq.OnCriticalSectionExited = () => criticalDone = true;

                    var seqData = new SequenceDataPacket(data);
                    taskSeq.Run(seqData, token).Forget();

                    await UniTask.WaitUntil(
                        () => criticalDone || !taskSeq.IsRunning,
                        cancellationToken: token);
                    return;
                }
            }

            // No critical sections (or chain): await the full run.
            var fullSeqData = new SequenceDataPacket(data);
            await seq.Run(fullSeqData, token);
        }
    }
}
