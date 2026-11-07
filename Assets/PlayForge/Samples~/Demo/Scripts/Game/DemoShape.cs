using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// A primitive shape in a color, made as the object wakes, in the editor too: the arena's floor and pillars, a
    /// projectile's look, an aura. The shape is made, never saved, so it takes the render pipeline's own default material,
    /// and the demo looks the same under the Built-in pipeline and under URP. A saved material would show magenta under the
    /// pipeline it wasn't made for.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DemoShape : MonoBehaviour
    {
        [Tooltip("The primitive it makes")]
        public PrimitiveType Shape = PrimitiveType.Sphere;

        [Tooltip("The shape's size, by axis")]
        public Vector3 Size = Vector3.one;

        [Tooltip("The shape's place, from this object")]
        public Vector3 Offset = Vector3.zero;

        [Tooltip("Its color")]
        public Color Color = Color.white;

        [Tooltip("Keep the primitive's collider, as the floor does for the clicks that move a hero")]
        public bool KeepCollider;

        [Tooltip("Grows from nothing and fades out over this many seconds, as a hit's spark does; 0 stays as it is")]
        [Min(0f)] public float Pulse;

        private GameObject made;
        private float born;

        private void OnEnable()
        {
            if (made) return;
            made = GameObject.CreatePrimitive(Shape);
            made.name = $"{name} Shape";
            made.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            made.transform.SetParent(transform, false);
            made.transform.localPosition = Offset;
            made.transform.localScale = Size;
            if (!KeepCollider && made.TryGetComponent<Collider>(out var collider))
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            Tint(made.GetComponent<Renderer>(), Color);
            born = Time.time;
        }

        private void OnDisable()
        {
            if (!made) return;
            if (Application.isPlaying) Destroy(made);
            else DestroyImmediate(made);
            made = null;
        }

        private void Update()
        {
            if (!made || Pulse <= 0f || !Application.isPlaying) return;
            float t = Mathf.Clamp01((Time.time - born) / Pulse);
            float grow = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f * 0.6f;
            made.transform.localScale = Size * Mathf.Max(0.05f, grow);
        }

        /// <summary>Tints a renderer through a property block, in whichever pipeline's color property it has.</summary>
        public static void Tint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
