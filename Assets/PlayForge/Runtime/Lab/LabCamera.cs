using System;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// The lab's camera (LAB-3): it looks down at an angle at a point on the ground, from the distance and tilt it's set
    /// to. Scroll pulls it back, a middle drag pans it, and <see cref="ResetView"/> puts it back as it started. It follows
    /// a unit when given one, as the Demo's follows its hero, and shakes as a cue asks, as the game's camera shaker
    /// PlayForge's cues reach through ForgeCues.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LabCamera : MonoBehaviour, ICameraShaker
    {
        [Tooltip("How far it stands from the point it looks at")]
        public float Distance = 24f;

        [Tooltip("How steeply it looks down, in degrees from the ground")]
        [Range(5f, 89f)] public float Tilt = 55f;

        [Tooltip("Which way it faces, in degrees about the up axis")]
        public float Yaw;

        [Tooltip("The nearest and the farthest it stands")]
        public float MinDistance = 6f, MaxDistance = 90f;

        [Tooltip("How much nearer or farther a notch of the wheel takes it, as a share of its distance")]
        public float ScrollShare = 0.12f;

        [Tooltip("How quickly it catches up with the unit it follows")]
        public float FollowSpeed = 5f;

        [Tooltip("Whether it reads the wheel and the middle button itself; off, only its owner moves it")]
        public bool ReadsInput = true;

        /// <summary>The point on the ground it looks at.</summary>
        public Vector3 Focus;

        /// <summary>What it follows, or null to stay where it's put.</summary>
        public Transform Following;

        /// <summary>Whether the pointer is over a panel, where the wheel and a drag are the panel's.</summary>
        public Func<Vector2, bool> PointerOverPanel;

        private Camera eye;
        private float startDistance, startTilt, startYaw;
        private Vector3 startFocus;
        private Vector2? dragFrom;
        private float strength, shakeLeft, shakeLength = 1f;

        /// <summary>The camera it moves.</summary>
        public Camera Camera => eye ? eye : eye = GetComponent<Camera>();

        protected virtual void Awake() => Remember();

        /// <summary>Keeps the view as it is now, for <see cref="ResetView"/> to put back.</summary>
        public void Remember()
        {
            startDistance = Distance;
            startTilt = Tilt;
            startYaw = Yaw;
            startFocus = Focus;
        }

        /// <summary>Puts the view back as it was kept, and stops following.</summary>
        public void ResetView()
        {
            Distance = startDistance;
            Tilt = startTilt;
            Yaw = startYaw;
            Focus = startFocus;
            Following = null;
            Place();
        }

        protected virtual void OnEnable() => ForgeCues.Shaker = this;

        protected virtual void OnDisable()
        {
            if (ReferenceEquals(ForgeCues.Shaker, this)) ForgeCues.Shaker = null;
        }

        protected virtual void LateUpdate()
        {
            if (ReadsInput) ReadInput();
            Track(ForgeTime.UnscaledDeltaTime);
            Place();
        }

        // The wheel and a middle drag, unless the pointer is over a panel
        private void ReadInput()
        {
            var input = ForgeInput.Current;
            bool hasPointer = input.TryGetPointerPosition(out var pointer);
            bool overPanel = hasPointer && PointerOverPanel != null && PointerOverPanel(pointer);

            float scroll = input.ScrollDelta;
            if (!overPanel && Mathf.Abs(scroll) > 0.001f) Zoom(scroll);

            if (hasPointer && input.PointerHeld(2))
            {
                if (dragFrom.HasValue) Pan(dragFrom.Value, pointer);
                else if (!overPanel) dragFrom = pointer;
                if (dragFrom.HasValue) dragFrom = pointer;
            }
            else dragFrom = null;
        }

        /// <summary>Nearer by a share of its distance for each notch toward the ground, farther for each away.</summary>
        public void Zoom(float notches) =>
            Distance = Mathf.Clamp(Distance * Mathf.Max(0.1f, 1f - notches * ScrollShare), MinDistance, MaxDistance);

        /// <summary>Moves the view so the ground under one point of the screen comes under another, and stops following.</summary>
        public void Pan(Vector2 from, Vector2 to)
        {
            if (!GroundAt(from, out var a) || !GroundAt(to, out var b)) return;
            Focus += a - b;
            Following = null;
            Place();
        }

        /// <summary>The point on the ground under a point of the screen.</summary>
        public bool GroundAt(Vector2 screen, out Vector3 point)
        {
            var ray = Camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
            {
                point = default;
                return false;
            }
            float along = -ray.origin.y / ray.direction.y;
            point = ray.origin + ray.direction * along;
            return along > 0f;
        }

        // Toward what it follows, a little each frame
        private void Track(float deltaTime)
        {
            if (!Following) return;
            var want = new Vector3(Following.position.x, 0f, Following.position.z);
            Focus = Vector3.Lerp(Focus, want, 1f - Mathf.Exp(-FollowSpeed * deltaTime));
        }

        /// <summary>Puts the camera where its focus, distance, tilt and yaw say, shaking while a shake lasts.</summary>
        public void Place()
        {
            var rotation = Quaternion.Euler(Tilt, Yaw, 0f);
            var at = Focus + rotation * new Vector3(0f, 0f, -Distance);
            if (shakeLeft > 0f)
            {
                shakeLeft -= ForgeTime.UnscaledDeltaTime;
                float fade = Mathf.Clamp01(shakeLeft / shakeLength);
                at += ForgeRandom.InsideUnitSphere * (strength * 0.35f * fade);
            }
            transform.SetPositionAndRotation(at, rotation);
        }

        public void Shake(float strength, float duration)
        {
            this.strength = Mathf.Max(shakeLeft > 0f ? this.strength : 0f, strength);
            shakeLength = Mathf.Max(0.05f, duration);
            shakeLeft = shakeLength;
        }
    }
}
