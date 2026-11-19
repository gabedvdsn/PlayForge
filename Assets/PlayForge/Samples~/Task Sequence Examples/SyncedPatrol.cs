using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// A patrol: to each waypoint in turn at a steady speed, turning to face where it's going, with a pause at each, for
    /// good. A builder is plain code, so the stages are made in a loop, two to a waypoint. Each move stage is WhenAny:
    /// the move ends it, and the turning, which never ends by itself, stops with it.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Patrol")]
    public class SyncedPatrol : SyncedSequenceComponent
    {
        [Tooltip("The points it walks between, in order")]
        public Transform[] Waypoints;

        [Tooltip("Its speed, in units a second")]
        [Min(0f)] public float Speed = 3f;

        [Tooltip("How fast it turns, in degrees a second")]
        [Min(0f)] public float TurnSpeed = 360f;

        [Tooltip("Seconds it waits at each waypoint")]
        [Min(0f)] public float PauseAtEach = 0.5f;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (Waypoints == null || Waypoints.Length == 0)
            {
                ForgeLog.Warning("The patrol has no waypoints.", this);
                return null;
            }

            var builder = TaskSequenceBuilder.Create("Synced Patrol").WithRepeat(true);
            foreach (var waypoint in Waypoints)
            {
                if (!waypoint) continue;

                builder
                    .Stage(s => s
                        .WithName($"To {waypoint.name}")
                        .WhenAny()
                        .Task(SyncSeq.MoveTowards(transform, waypoint, Speed, 0.05f))
                        .Task(SyncSeq.LookAtTracking(transform, waypoint, TurnSpeed)))
                    .Stage(s => s
                        .WithName($"At {waypoint.name}")
                        .SyncDelay(PauseAtEach));
            }

            return builder.BuildSyncRunner();
        }
    }
}
