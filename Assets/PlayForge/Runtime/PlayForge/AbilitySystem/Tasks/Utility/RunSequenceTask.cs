using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A stage task that runs a task sequence, or a chain, and waits for it to end: the one the static
    /// <c>[TaskSequenceMethod]</c> method its <c>Sequence</c> names makes, called for each activation, or, for an
    /// ability built in code, a fresh run of its <c>Definition</c>. It runs as a process in the caster's world, by the
    /// <c>Clock</c> picked where one is, and ends as the activation is cancelled; a critical stage in the sequence
    /// makes the task critical, and the ability lets its claim go as the sequence's critical section ends.
    /// </summary>
    [ForgeMenu("Sequence", Description = "Runs a sequence a static method in code makes.")]
    public class RunSequenceTask : AbstractAbilityTask
    {
        public TaskSequenceReference Sequence;

        /// <summary>
        /// The clock the sequence it runs goes by: the caster's, the game's, unscaled, scaled, or one of your own. It's the
        /// run's alone, before the ability's packet's and the sequence's own, so the ability's other tasks keep their time.
        /// None leaves the sequence the clock it would have had: its own, or the caster's.
        /// </summary>
        [Tooltip("The clock the sequence runs by: none for its own or the caster's, or the caster's, the game's, unscaled or scaled")]
        [SerializeReference] public AbstractClockChoice Clock;

        /// <summary>
        /// A sequence made in code, as an ability a game builds as it runs makes one with <see cref="TaskSequenceBuilder"/>.
        /// Set, each activation runs a fresh sequence from it, in place of the one <see cref="Sequence"/>'s method makes.
        /// Run Sequence ran only a sequence a static method made, by name.
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
            // A fresh one from the definition made in code, or the one the method makes
            var seq = Definition != null ? new TaskSequence(Definition) : Sequence.GetActiveSequence();
            if (seq is null) return;

            // The clock the pick gives, the sequence's run's alone: the ability's packet, which its other tasks share,
            // keeps its own
            var clock = Clock?.ClockFor(data);

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
                // In the caster's world, as its ability's own sequence is
                var registration = ProcessControl.Register(new TaskSequenceProcess(taskSeq) { Clock = clock }).With(data)
                    .In(ProcessWorld.Of(data.EffectOrigin?.GetSource())).Run();
                var relay = registration.Relay;
                if (!registration)
                {
                    ForgeLog.Warning(ForgeLog.ESystem.Abilities, $"[RunSequenceTask] ProcessControl refused the inner sequence ({registration.Refusal}).");
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
                    // this way added a frame
                    await relay.WhenTerminated(token);
                }
                catch (System.OperationCanceledException)
                {
                    // External cancellation (e.g., the outer ability was interrupted). End the inner
                    // sequence so it tears down cleanly rather than being left behind as an orphaned
                    // runtime. Through its relay, because an interrupt can be refused by a critical
                    // section, and can't reach a run that hasn't started.
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

            // A chain runs as a process too, in the caster's world, as a sequence does: its process asks the chain's
            // conditions and its sequences' each frame, where awaiting it in place asked none of them
            if (seq is TaskSequenceChain chain)
            {
                var chainRegistration = ProcessControl.Register(new TaskSequenceProcess(chain) { Clock = clock }).With(data)
                    .In(ProcessWorld.Of(data.EffectOrigin?.GetSource())).Run();
                if (!chainRegistration)
                {
                    ForgeLog.Warning(ForgeLog.ESystem.Abilities, $"[RunSequenceTask] ProcessControl refused the inner chain ({chainRegistration.Refusal}).");
                    return;
                }

                try
                {
                    await chainRegistration.Relay.WhenTerminated(token);
                }
                catch (System.OperationCanceledException)
                {
                    chainRegistration.Relay.TerminateImmediate();
                    throw;
                }
                return;
            }

            // Any other kind of sequence: awaited in place, on a packet of its own that carries the pick
            var fullSeqData = new SequenceDataPacket(data);
            if (clock != null) fullSeqData.Clock = clock;
            await seq.Run(fullSeqData, token);
        }
    }
}
