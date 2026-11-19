using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Pulses its object's scale for good, a punch and a pause. With its Clock picked as Unscaled it goes on while the game's
    /// time is slowed or stopped, as a menu's highlight should, its runner stepping by the clock its packet carries.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Pulse")]
    public class SyncedPulse : SyncedSequenceComponent
    {
        [Tooltip("How far it swells, as a share of its size")]
        [Min(0f)] public float Intensity = 0.3f;

        [Tooltip("Seconds a pulse takes")]
        [Min(0.01f)] public float Duration = 0.6f;

        [Tooltip("Seconds between pulses")]
        [Min(0f)] public float PauseBetween = 0.4f;

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Pulse")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Punch")
                    .Task(SyncSeq.PunchScale(transform, Intensity, Duration)))
                .Stage(s => s
                    .WithName("Pause")
                    .SyncDelay(PauseBetween))
                .BuildSyncRunner();
        }
    }
}
