using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Circles a center for good, and stops and starts again on a key. Interrupt stops its runner and cleans the stage it
    /// was in; Reset readies it to start again from its first stage.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Orbit")]
    public class SyncedOrbit : SyncedSequenceComponent
    {
        [Tooltip("What it circles")]
        public Transform Center;

        [Tooltip("How far from the center it circles")]
        [Min(0f)] public float Radius = 3f;

        [Tooltip("Its speed round, in degrees a second")]
        public float DegreesPerSecond = 90f;

        [Tooltip("The axis it circles about")]
        public Vector3 Axis = Vector3.up;

        [Tooltip("The key that stops it, and starts it again, read through ForgeInput")]
        public KeyCode ToggleKey = KeyCode.O;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (!Center)
            {
                ForgeLog.Warning("The orbit has no center.", this);
                return null;
            }

            return TaskSequenceBuilder.Create("Synced Orbit")
                .Stage(s => s
                    .WithName("Orbit")
                    .Task(SyncSeq.Orbit(transform, Center, Radius, DegreesPerSecond, Axis)))
                .BuildSyncRunner();
        }

        public override void WhenUpdate()
        {
            if (Runner != null && ForgeInput.Current.KeyPressed(ToggleKey))
            {
                if (Runner.State == SyncedTaskSequence.ERunnerState.Interrupted)
                {
                    Runner.Reset();
                    ForgeLog.Info("The orbit starts again.", this);
                }
                else
                {
                    Runner.Interrupt(Packet);
                    ForgeLog.Info("The orbit stops.", this);
                }
            }

            base.WhenUpdate();
        }
    }
}
