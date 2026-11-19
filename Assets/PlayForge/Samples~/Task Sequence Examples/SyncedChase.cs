using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// A chase: after its target at a steady speed, facing it, until it's in reach or it gives up; then it waits for the
    /// target to get away, and chases again. The chase stage is WhenAny over three tasks: the move, which ends in reach,
    /// the turning, which never ends, and the delay that gives up. Its OnTerminate says which.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Chase")]
    public class SyncedChase : SyncedSequenceComponent
    {
        [Tooltip("What it chases")]
        public Transform Target;

        [Tooltip("Its speed, in units a second")]
        [Min(0f)] public float Speed = 5f;

        [Tooltip("How fast it turns, in degrees a second")]
        [Min(0f)] public float TurnSpeed = 360f;

        [Tooltip("How close it gets before it stops")]
        [Min(0f)] public float Reach = 1.5f;

        [Tooltip("Seconds it chases before it gives up")]
        [Min(0f)] public float GiveUpAfter = 6f;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (!Target)
            {
                ForgeLog.Warning("The chase has no target.", this);
                return null;
            }

            return TaskSequenceBuilder.Create("Synced Chase")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Chase")
                    .WhenAny()
                    .Task(SyncSeq.MoveTowards(transform, Target, Speed, Reach))
                    .Task(SyncSeq.LookAtTracking(transform, Target, TurnSpeed))
                    .Task(SyncSeq.Delay(GiveUpAfter))
                    .OnTerminate((ctx, completed) => ForgeLog.Info(Within(1f) ? "Caught up." : "Gave up.", this)))
                .Stage(s => s
                    .WithName("Wait for it to get away")
                    .WaitUntil(() => !Within(2f)))
                .BuildSyncRunner();
        }

        // Whether the target is within its reach, times a factor
        private bool Within(float times) => Target && Vector3.Distance(transform.position, Target.position) <= Reach * times + 0.01f;
    }
}
