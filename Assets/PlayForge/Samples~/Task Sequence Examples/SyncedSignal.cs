using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// A signal cycling green, amber and red for good, steered by hand: one key skips the light it's on, and others jump
    /// to a light. Amber is a critical stage, and the runner's OnCriticalSectionExited hears it end each time round. A
    /// synced runner has no injections, so it refuses none of this: steering one is its owner's to get right.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Signal")]
    public class SyncedSignal : SyncedSequenceComponent
    {
        [Tooltip("Seconds on green")]
        [Min(0f)] public float Green = 4f;

        [Tooltip("Seconds on amber")]
        [Min(0f)] public float Amber = 1.5f;

        [Tooltip("Seconds on red")]
        [Min(0f)] public float Red = 4f;

        [Tooltip("The key that skips the light it's on, read through ForgeInput")]
        public KeyCode SkipKey = KeyCode.Tab;

        [Tooltip("The keys that jump to green, amber and red")]
        public KeyCode[] JumpKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        protected override SyncedTaskSequence BuildRunner()
        {
            var runner = TaskSequenceBuilder.Create("Synced Signal")
                .WithRepeat(true)
                .Stage(s => Lamp(s, "Green", Green))
                .Stage(s => Lamp(s, "Amber", Amber).WithCriticalFlag(true))
                .Stage(s => Lamp(s, "Red", Red))
                .BuildSyncRunner();

            runner.OnCriticalSectionExited = () => ForgeLog.Info("Past amber.", this);
            return runner;
        }

        private StageBuilder Lamp(StageBuilder stage, string color, float seconds) => stage
            .WithName(color)
            .OnInit(_ => ForgeLog.Info($"{color}.", this))
            .SyncDelay(seconds);

        public override void WhenUpdate()
        {
            if (Runner != null)
            {
                var input = ForgeInput.Current;
                if (input.KeyPressed(SkipKey) && Runner.SkipStage(Packet))
                {
                    ForgeLog.Info($"Skipped to light {Runner.StageIndex}.", this);
                }

                for (int i = 0; i < JumpKeys.Length; i++)
                {
                    if (input.KeyPressed(JumpKeys[i]) && Runner.JumpToStage(Packet, i)) ForgeLog.Info($"Jumped to light {i}.", this);
                }
            }

            base.WhenUpdate();
        }
    }
}
