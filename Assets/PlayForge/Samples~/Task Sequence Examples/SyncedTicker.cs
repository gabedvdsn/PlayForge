using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// A tick every few seconds, for good: the simplest synced sequence, a wait and an action, repeating. Its runner's
    /// OnComplete hears each time round, Verbose in ForgeLog. Pause its process to stop the ticks, unpause it to go on.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Ticker")]
    public class SyncedTicker : SyncedSequenceComponent
    {
        [Tooltip("Seconds between ticks")]
        [Min(0f)] public float Interval = 1f;

        private static readonly Tag TICKS = Tag.GenerateAsUnique("Examples.Ticks");

        protected override SyncedTaskSequence BuildRunner()
        {
            var runner = TaskSequenceBuilder.Create("Synced Ticker")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(Interval)))
                .Stage(s => s
                    .WithName("Tick")
                    .Do(d => ForgeLog.Info($"Tick {d.Increment(TICKS)}, at {ForgeTime.Time:F1}s.", this)))
                .BuildSyncRunner();

            runner.OnComplete = (data, completed) => ForgeLog.Verbose($"Round {data.GetPrimary<int>(TICKS)} done.", this);
            return runner;
        }
    }
}
