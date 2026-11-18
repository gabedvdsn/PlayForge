using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runs a task sequence on a data packet loaded for the cue (<see cref="CuePackets"/>), its Data list last, so the
    /// sequence reads the cue's entity, its asset and its numbers: a burst's runs its course, and one in a list of lasting
    /// cues runs until its cue ends, as a shield's flicker that stops as the shield goes. It runs in the world of the one
    /// it plays on, by the clock its Clock picks, else the sequence's own, else its owner's.
    /// </summary>
    [Serializable]
    [ForgeMenu(Name = "Sequence", Description = "Runs a task sequence on a data packet loaded for the cue.")]
    public class SequenceCue : AbstractCueHandler
    {
        [Tooltip("The task sequence it runs, one a [TaskSequenceMethod] makes")]
        public TaskSequenceReference Sequence;

        [Tooltip("The clock its sequence runs by: none for the sequence's own or the cue's owner's, or the caster's, the game's, unscaled or scaled")]
        [SerializeReference] public AbstractClockChoice Clock;

        [Tooltip("Data its sequence runs with, loaded last, in place of what the cue loads under the same key")]
        public List<DataWrapper> Data = new();

        /// <summary>
        /// A sequence made in code, in place of <see cref="Sequence"/>, as a game that builds its cues as it runs makes one
        /// with <see cref="TaskSequenceBuilder"/>.
        /// </summary>
        [NonSerialized] public TaskSequenceDefinition Definition;

        // The run each lasting cue began, by the key its beginning and its end share
        [NonSerialized] private Dictionary<object, ProcessRelay> running;

        public override void Burst(CueContext cue) => Run(cue);

        public override void Begin(CueContext cue)
        {
            if (KeyOf(cue) is not { } key) return;
            // A cue begun again under its key ends what it began before
            Stop(key);
            if (Run(cue) is { } relay) (running ??= new Dictionary<object, ProcessRelay>())[key] = relay;
        }

        public override void End(CueContext cue)
        {
            if (KeyOf(cue) is { } key) Stop(key);
        }

        // Ends the run a lasting cue began under its key
        private void Stop(object key)
        {
            if (running != null && running.Remove(key, out var relay)) relay?.Terminate();
        }

        private static object KeyOf(in CueContext cue) => cue.Key ?? cue.Spec;

        // Its sequence on a packet loaded for the cue, in the world of the one it's on. The clock its pick gives, else the
        // sequence's own, is the run's own; with neither, the run goes by the packet's, its owner's
        private ProcessRelay Run(in CueContext cue)
        {
            var active = Definition != null ? new TaskSequence(Definition) : Sequence.IsValid ? Sequence.GetActiveSequence() : null;
            var data = CuePackets.For(cue, Data);
            var process = active switch
            {
                TaskSequence sequence => new TaskSequenceProcess(sequence),
                TaskSequenceChain chain => new TaskSequenceProcess(chain),
                _ => null,
            };
            if (process == null) return null;
            if ((Clock?.ClockFor(data) ?? (active as TaskSequence)?.Definition?.Metadata?.Clock) is { } clock) process.Clock = clock;
            return ProcessControl.Register(process).With(data).In(ProcessWorld.Of(cue.Target)).Run(out var relay) ? relay : null;
        }
    }
}
