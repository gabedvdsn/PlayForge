using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Bobs up and down for good, eased by a curve of the inspector's: two timed moves, repeating.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Bob")]
    public class SyncedBob : SyncedSequenceComponent
    {
        [Tooltip("How far it rises")]
        public float Height = 0.5f;

        [Tooltip("Seconds each way")]
        [Min(0f)] public float Duration = 1f;

        [Tooltip("The easing each way; with none, linear")]
        public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Bob")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Up")
                    .Task(SyncSeq.MoveBy(transform, Vector3.up * Height, Duration, Ease)))
                .Stage(s => s
                    .WithName("Down")
                    .Task(SyncSeq.MoveBy(transform, Vector3.down * Height, Duration, Ease)))
                .BuildSyncRunner();
        }
    }
}
