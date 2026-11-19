using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// A fire that burns for a number of ticks, then goes out and takes its object with it: a finite synced sequence,
    /// whose process ends as it finishes, as a mono process's end destroys its object. The burning is a repeating stage
    /// that counts its rounds in the packet and breaks its own repeat from OnRepeat, through the packet, as an async
    /// sequence's would. A key douses it early, breaking the repeat through the runner.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Burn")]
    public class SyncedBurn : SyncedSequenceComponent
    {
        [Tooltip("Seconds between burns")]
        [Min(0.01f)] public float Interval = 0.5f;

        [Tooltip("How many times it burns")]
        [Min(1)] public int Burns = 6;

        [Tooltip("The damage each burn does")]
        public float DamagePerBurn = 5f;

        [Tooltip("The key that douses it early, read through ForgeInput")]
        public KeyCode DouseKey = KeyCode.F;

        [Tooltip("Ends its process as it goes out, which destroys its object")]
        public bool DestroyWhenOut = true;

        private static readonly Tag BURNED = Tag.GenerateAsUnique("Examples.Burned");

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Burn")
                .Stage(s => s
                    .WithName("Burning")
                    .WithRepeat(true)
                    .OnInit(d => d.SetPrimary(BURNED, 0))
                    .OnRepeat(ctx =>
                    {
                        if (ctx.Data.GetPrimary<int>(BURNED) >= Burns) ctx.Data.BreakStageRepeat();
                    })
                    // Each round burns as it starts, then waits its interval
                    .Do(d => ForgeLog.Info($"Burn {d.Increment(BURNED)}, for {DamagePerBurn}.", this))
                    .Task(SyncSeq.Delay(Interval)))
                .Do(d => ForgeLog.Info($"The fire goes out after {d.GetPrimary<int>(BURNED)} burns.", this))
                .BuildSyncRunner();
        }

        public override void WhenUpdate()
        {
            if (Runner != null && ForgeInput.Current.KeyPressed(DouseKey) && Runner.BreakStageRepeat())
            {
                ForgeLog.Info("Doused: this burn is the last.", this);
            }

            base.WhenUpdate();
        }

        protected override void WhenFinished()
        {
            if (DestroyWhenOut) Relay.Terminate();
        }
    }
}
