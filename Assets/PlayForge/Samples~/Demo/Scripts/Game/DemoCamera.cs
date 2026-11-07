using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The arena's camera: it follows the player's hero from above and behind, and shakes as a camera shake cue asks, as
    /// Thunderclap's does. It's the game's camera shaker, which PlayForge's cues reach through ForgeCues.
    /// </summary>
    public class DemoCamera : MonoBehaviour, ICameraShaker
    {
        [Tooltip("Where it sits from the hero")]
        public Vector3 Offset = new(0f, 15f, -11f);

        [Tooltip("How quickly it catches up")]
        public float Follow = 5f;

        private Vector3 focus;
        private float strength;
        private float shakeLeft;
        private float shakeLength = 1f;

        private void OnEnable() => ForgeCues.Shaker = this;

        private void OnDisable()
        {
            if (ReferenceEquals(ForgeCues.Shaker, this)) ForgeCues.Shaker = null;
        }

        private void LateUpdate()
        {
            var game = DemoGame.Current;
            var player = game != null ? game.Player : null;
            var want = player != null ? player.transform.position : Vector3.zero;
            focus = Vector3.Lerp(focus, want, 1f - Mathf.Exp(-Follow * Time.deltaTime));

            var at = focus + Offset;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                float fade = Mathf.Clamp01(shakeLeft / shakeLength);
                at += Random.insideUnitSphere * (strength * 0.35f * fade);
            }
            transform.position = at;
            transform.rotation = Quaternion.LookRotation(focus - (focus + Offset));
        }

        public void Shake(float strength, float duration)
        {
            this.strength = Mathf.Max(shakeLeft > 0f ? this.strength : 0f, strength);
            shakeLength = Mathf.Max(0.05f, duration);
            shakeLeft = shakeLength;
        }
    }
}
