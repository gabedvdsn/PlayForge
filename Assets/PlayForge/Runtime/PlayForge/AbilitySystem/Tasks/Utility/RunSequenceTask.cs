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
        /// A sequence made in code, as an ability a game builds as it runs makes one with <see cref="TaskSequenceBuilder"/>.
        /// Set, each activation runs a fresh sequence from it, in place of the one <see cref="Sequence"/>'s method makes.
        /// Run Sequence ran only a sequence a static method made, by name (FEAT-21).
        /// </summary>
        [System.NonSerialized] public TaskSequenceDefinition Definition;

        /// <summary>
        /// True if the referenced sequence has any critical sections — either the whole sequence
        /// is marked critical via builder-level WithCriticalFlag, or any individual stage is.
        /// This drives the outer compiled ability stage's critical flag.
        /// </summary>
        public override bool IsCriticalSection => Definition != null
            ? IsCritical(Definition)
            : Sequence.GetActiveSequence() is TaskSequence ts && IsCritical(ts.Definition);

        private static bool IsCritical(TaskSequenceDefinition definition) =>
            (definition.Metadata?.IsCritical ?? false) || definition.Stages.Any(s => s.Metadata?.IsCritical ?? false);

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // A fresh one from the definition made in code, or the one the method makes (FEAT-21)
            var seq = Definition != null ? new TaskSequence(Definition) : Sequence.GetActiveSequence();
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
                // In the caster's world, as its ability's own sequence is (FEAT-10)
                var registration = ProcessControl.Register(taskSeq).With(data).In(ProcessWorld.Of(data.EffectOrigin?.GetSource())).Run();
                var relay = registration.Relay;
                if (!registration)
                {
                    Debug.LogWarning($"[RunSequenceTask] ProcessControl refused the inner sequence ({registration.Refusal}).");
                    return;
                }

                try
                {
                    // Wait for the inner sequence's process to fully terminate. Critical-section
                    // exit does NOT end the wait — it only releases the outer claim (via the
                    // NotifyCriticalSectionExit callback wired above). This ensures the outer
                    // ability's activation handle is only removed once the inner sequence has
                    // genuinely finished.
                    //
                    // Done in the frame it ends. Polling the relay found that a frame later, so each sequence nested
                    // this way added a frame (TS-Z)
                    await relay.WhenTerminated(token);
                }
                catch (System.OperationCanceledException)
                {
                    // External cancellation (e.g., the outer ability was interrupted). End the inner
                    // sequence so it tears down cleanly rather than being left behind as an orphaned
                    // runtime. Through its relay, because an interrupt can be refused by a critical
                    // section, and can't reach a run that hasn't started (TS-G).
                    relay.TerminateImmediate();
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
