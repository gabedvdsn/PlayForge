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

                // Wire the inner sequence's critical exit to propagate into the outer ability's
                // handle BEFORE kicking off the sequence — this lets the outer claim release as
                // soon as the inner critical section exits, without forcing the outer runtime to
                // end. The outer runtime (this task) continues to await the inner's full
                // completion, which keeps the outer ability's activation handle alive and its
                // process registered with ProcessControl for the entire duration.
                if (hasCritical)
                {
                    var notify = data.NotifyCriticalSectionExit;
                    taskSeq.OnCriticalSectionExited = () => notify?.Invoke();
                }
                else
                {
                    taskSeq.OnCriticalSectionExited = null;
                }

                // Register the inner sequence with ProcessControl in BOTH sync and async paths so
                // the runtime is always visible and manageable. Previously the async+critical path
                // started the sequence via .Forget() outside ProcessControl, producing an orphaned
                // runtime that kept running after the outer ability tore down.
                if (!ProcessControl.Register(taskSeq, data, out var relay))
                {
                    Debug.LogWarning("[RunSequenceTask] Failed to register inner sequence with ProcessControl.");
                    return;
                }

                try
                {
                    // Wait for the inner sequence's process to fully terminate. Critical-section
                    // exit does NOT end the wait — it only releases the outer claim (via the
                    // NotifyCriticalSectionExit callback wired above). This ensures the outer
                    // ability's activation handle is only removed once the inner sequence has
                    // genuinely finished.
                    await UniTask.WaitUntil(
                        () => relay.State == EProcessState.Terminated,
                        cancellationToken: token);
                }
                catch (System.OperationCanceledException)
                {
                    // External cancellation (e.g., the outer ability was interrupted). Propagate
                    // the cancellation to the inner sequence so it tears down cleanly rather than
                    // being left behind as an orphaned runtime.
                    taskSeq.Interrupt();
                    throw;
                }
                finally
                {
                    // Clear the callback so a shared TaskSequence asset doesn't retain a dangling
                    // reference to this activation's handle.
                    taskSeq.OnCriticalSectionExited = null;
                }

                return;
            }

            // Non-TaskSequence path (e.g. chain): await the full run directly.
            var fullSeqData = new SequenceDataPacket(data);
            await seq.Run(fullSeqData, token);
        }
    }
}
