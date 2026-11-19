using UnityEngine;
using UnityEngine.Rendering;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>What a <see cref="SurvivorsLine"/> draws.</summary>
    public enum ESurvivorsLineShape
    {
        /// <summary>A circle on the ground at its radius, as a nova's edge or a slam's warning.</summary>
        Ring,

        /// <summary>A part of a circle in front of it, its width in degrees, as a cleave's sweep.</summary>
        Arc,

        /// <summary>A jagged bolt from above down to it, as lightning.</summary>
        Bolt,
    }

    /// <summary>
    /// A line drawn as the object wakes, made at play time as <see cref="SurvivorsShape"/> is, with an unlit material of
    /// Unity's sprites that every pipeline draws: a ring, an arc or a bolt, in a color, fading out if it says so. A cue
    /// that's pooled draws it afresh each time it comes back.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivorsLine : MonoBehaviour
    {
        private const int Points = 48;

        public ESurvivorsLineShape Shape = ESurvivorsLineShape.Ring;

        [Tooltip("The ring's or the arc's radius")]
        [Min(0f)] public float Radius = 1f;

        [Tooltip("The arc's width, in degrees")]
        [Range(1f, 360f)] public float Arc = 140f;

        [Tooltip("How high the bolt starts")]
        [Min(0f)] public float Height = 9f;

        [Tooltip("The line's width")]
        [Min(0.01f)] public float Width = 0.12f;

        public Color Color = Color.white;

        [Tooltip("Seconds it fades out over; 0 stays as it is")]
        [Min(0f)] public float Fade;

        private LineRenderer line;
        private float born;
        private static Material material;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            if (!line) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            if (!material)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) material = new Material(shader) { name = "Survivors Line", hideFlags = HideFlags.DontSave };
            }
            line.sharedMaterial = material;
        }

        private void OnEnable()
        {
            born = Time.time;
            Draw(Radius);
        }

        private void Update()
        {
            if (Fade <= 0f || !line) return;
            float left = 1f - Mathf.Clamp01((Time.time - born) / Fade);
            var color = new Color(Color.r, Color.g, Color.b, Color.a * left);
            line.startColor = line.endColor = color;
        }

        /// <summary>Draws it again at a radius, as a nova widens.</summary>
        public void Draw(float radius)
        {
            if (!line) Awake();
            line.widthMultiplier = Width;
            line.startColor = line.endColor = Color;
            switch (Shape)
            {
                case ESurvivorsLineShape.Ring:
                    line.loop = true;
                    line.positionCount = Points;
                    for (int i = 0; i < Points; i++)
                    {
                        float angle = i * Mathf.PI * 2f / Points;
                        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius));
                    }
                    break;
                case ESurvivorsLineShape.Arc:
                    line.loop = false;
                    line.positionCount = Points / 2;
                    for (int i = 0; i < Points / 2; i++)
                    {
                        float angle = (90f - Arc / 2f + Arc * i / (Points / 2 - 1)) * Mathf.Deg2Rad;
                        line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius));
                    }
                    break;
                case ESurvivorsLineShape.Bolt:
                    line.loop = false;
                    const int segments = 8;
                    line.positionCount = segments + 1;
                    for (int i = 0; i <= segments; i++)
                    {
                        float along = 1f - (float)i / segments;
                        var jitter = i == 0 || i == segments ? Vector2.zero : Random.insideUnitCircle * 0.45f;
                        line.SetPosition(i, new Vector3(jitter.x, Height * along, jitter.y));
                    }
                    break;
            }
        }
    }
}
