using UnityEngine;
using UnityEngine.Rendering;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// The camera PlayForge's Camera Shake cue shakes (AC-2). On a camera, it's the project's shaker while it's enabled,
    /// and it moves the camera off its place by a noise that fades over each shake, only while the camera renders, so
    /// whatever else moves the camera keeps doing so. A shake asked during another takes the stronger of the two.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayForge/Additional Content/Camera Shaker")]
    public class CameraShaker : MonoBehaviour, ICameraShaker
    {
        [Tooltip("How far a shake of strength 1 moves the camera, in world units")]
        [Min(0f)] public float Distance = 0.15f;

        [Tooltip("How far a shake of strength 1 turns the camera about its view, in degrees; 0, it only moves")]
        [Min(0f)] public float Roll = 0.6f;

        [Tooltip("How fast it shakes, in shakes a second")]
        [Min(0.1f)] public float Frequency = 22f;

        [Tooltip("The strongest a shake gets, however strong a cue asks")]
        [Min(0f)] public float MaxStrength = 3f;

        private float strength;
        private float duration;
        private float left;
        private float seed;
        private Camera own;
        private bool applied;
        private Vector3 appliedOffset;
        private Quaternion restRotation;

        /// <summary>Whether a shake is under way.</summary>
        public bool Shaking => left > 0f;

        /// <summary>How hard it shakes now: the last shake's strength, fading to 0 as it ends.</summary>
        public float Strength => left > 0f && duration > 0f ? strength * (left / duration) : 0f;

        /// <summary>Where the shake puts the camera this frame, from its place.</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>How far the shake turns the camera this frame, in degrees.</summary>
        public float Angle { get; private set; }

        private void Awake() => own = GetComponent<Camera>();

        private void OnEnable()
        {
            seed = Random.value * 100f;
            ForgeCues.Shaker = this;
            RenderPipelineManager.beginCameraRendering += BeginRendering;
            RenderPipelineManager.endCameraRendering += EndRendering;
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginRendering;
            RenderPipelineManager.endCameraRendering -= EndRendering;
            Camera.onPreCull -= PreCull;
            Camera.onPostRender -= PostRender;
            Remove();
            left = 0f;
            Offset = Vector3.zero;
            Angle = 0f;
            if (ReferenceEquals(ForgeCues.Shaker, this)) ForgeCues.Shaker = null;
        }

        /// <summary>Shakes the camera, as hard as the strength says, for the duration in seconds.</summary>
        public void Shake(float strength, float duration)
        {
            strength = Mathf.Min(strength, MaxStrength);
            if (strength <= 0f || duration <= 0f || strength < Strength) return;
            this.strength = strength;
            this.duration = duration;
            left = duration;
        }

        private void LateUpdate()
        {
            if (left <= 0f)
            {
                Offset = Vector3.zero;
                Angle = 0f;
                return;
            }
            left = Mathf.Max(0f, left - Time.deltaTime);
            float now = Strength;
            float t = Time.time * Frequency;
            Offset = new Vector3(Noise(t, 0f), Noise(t, 1f), 0f) * (Distance * now);
            Angle = Noise(t, 2f) * Roll * now;
        }

        // From -1 to 1, smooth in time, a strand of its own for each axis
        private float Noise(float t, float strand) => Mathf.PerlinNoise(seed + strand * 13.7f, t) * 2f - 1f;

        private void BeginRendering(ScriptableRenderContext context, Camera camera) => PreCull(camera);

        private void EndRendering(ScriptableRenderContext context, Camera camera) => PostRender(camera);

        private void PreCull(Camera camera)
        {
            if (camera != own || applied || (Offset == Vector3.zero && Angle == 0f)) return;
            restRotation = transform.localRotation;
            appliedOffset = transform.localRotation * Offset;
            transform.localPosition += appliedOffset;
            transform.localRotation *= Quaternion.Euler(0f, 0f, Angle);
            applied = true;
        }

        private void PostRender(Camera camera)
        {
            if (camera == own) Remove();
        }

        private void Remove()
        {
            if (!applied) return;
            transform.localPosition -= appliedOffset;
            transform.localRotation = restRotation;
            applied = false;
        }
    }
}
