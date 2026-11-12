using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The arena's camera: the Creator Lab's (LAB-7), following the player's hero from above and behind, at the offset its
    /// scene gives, and shaking as a camera shake cue asks, as Thunderclap's does.
    /// </summary>
    public class DemoCamera : LabCamera
    {
        [Tooltip("Where it sits from the hero")]
        public Vector3 Offset = new(0f, 15f, -11f);

        [Tooltip("How quickly it catches up")]
        public float Follow = 5f;

        protected override void Awake()
        {
            // The offset as the lab's camera keeps a view: how far, how steeply down, and which way
            Distance = Offset.magnitude;
            Tilt = Mathf.Atan2(Offset.y, new Vector2(Offset.x, Offset.z).magnitude) * Mathf.Rad2Deg;
            Yaw = Mathf.Atan2(-Offset.x, -Offset.z) * Mathf.Rad2Deg;
            MinDistance = Mathf.Min(MinDistance, Distance);
            MaxDistance = Mathf.Max(MaxDistance, Distance);
            FollowSpeed = Follow;
            ReadsInput = false;
            base.Awake();
        }

        protected override void LateUpdate()
        {
            var game = DemoGame.Current;
            var player = game != null ? game.Player : null;
            Following = player != null ? player.transform : null;
            base.LateUpdate();
        }
    }
}
