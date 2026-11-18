using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runs a MonoBehaviour process where its placement says: a burst's runs its own course, and one in a list of lasting
    /// cues runs for as long as its cue lasts, as a shield's that ends as the shield goes. Each runs with a data packet
    /// loaded for the cue (<see cref="CuePackets"/>), its Data list last, so the process reads the cue's entity, its
    /// asset and its numbers. Pooled, its objects come from <see cref="ForgeCuePool"/> through
    /// <see cref="CuePoolInstantiator"/>, and each goes back as its process ends.
    /// </summary>
    [Serializable]
    [ForgeMenu(Name = "Process", Description = "Runs a MonoBehaviour process where its placement says, with a data packet loaded for the cue.")]
    public class ProcessCue : AbstractCueHandler
    {
        [Tooltip("The process it runs: a prefab with a MonoBehaviour process on it")]
        public AbstractMonoProcess Process;

        [Tooltip("Where its process runs: on whom, at what socket, moved and scattered how, following them or not, and facing which way")]
        public CuePlacement Placement = new();

        [Tooltip("Take its objects from the cue pool and give them back as each process ends, in place of making and destroying one each time")]
        public bool Pooled;

        [Tooltip("How many objects its pool makes ahead, as it first plays")]
        [Min(0)] public int Preallocate;

        [Tooltip("Data its process runs with, loaded last, in place of what the cue loads under the same key")]
        public List<DataWrapper> Data = new();

        // The processes each lasting cue runs, by the key its beginning and its end share
        [NonSerialized] private Dictionary<object, List<ProcessRelay>> running;

        // Whether its pool has made its Preallocate objects ahead, as it does the first time it plays
        [NonSerialized] private bool warmed;

        public override void Burst(CueContext cue)
        {
            if (Process == null) return;
            foreach (var place in PlacementOf().Resolve(cue, lasting: false)) Run(cue, place);
        }

        public override void Begin(CueContext cue)
        {
            if (Process == null || KeyOf(cue) is not { } key) return;
            // A cue begun again under its key ends what it began before
            Stop(key);
            var relays = new List<ProcessRelay>();
            foreach (var place in PlacementOf().Resolve(cue, lasting: true))
                if (Run(cue, place) is { } relay) relays.Add(relay);
            (running ??= new Dictionary<object, List<ProcessRelay>>())[key] = relays;
        }

        public override void End(CueContext cue)
        {
            if (KeyOf(cue) is { } key) Stop(key);
        }

        // Ends what a lasting cue began under its key
        private void Stop(object key)
        {
            if (running == null || !running.Remove(key, out var relays)) return;
            foreach (var relay in relays) relay?.Terminate();
        }

        private static object KeyOf(in CueContext cue) => cue.Key ?? cue.Spec;

        private CuePlacement PlacementOf() => Placement ??= new CuePlacement();

        // Its process at a place, on a packet loaded for the cue there, in the world of the one it's on, its object from
        // the cue pool where it pools
        private ProcessRelay Run(in CueContext cue, CuePlace place)
        {
            var registration = ProcessControl.Register(Process).With(CuePackets.For(cue, Data, place))
                .In(ProcessWorld.Of(cue.Target));
            if (Pooled)
            {
                if (!warmed)
                {
                    warmed = true;
                    if (Preallocate > 0) ForgeCuePool.Preallocate(Process.gameObject, Preallocate);
                }
                registration = registration.InstantiatedBy(CuePoolInstantiator.Shared);
            }
            return registration.Run(out var relay) ? relay : null;
        }
    }
}
