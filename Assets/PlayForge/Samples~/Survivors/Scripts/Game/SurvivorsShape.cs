using UnityEngine;
using UnityEngine.Rendering;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A primitive shape in a color, made as the object wakes: a projectile's body, a crystal, the arena's floor. It's made,
    /// never saved, so it takes the render pipeline's own default material, and the demo looks the same under the Built-in
    /// pipeline and under URP, where a saved material shows magenta under the pipeline it wasn't made for.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivorsShape : MonoBehaviour
    {
        [Tooltip("The primitive it makes")]
        public PrimitiveType Shape = PrimitiveType.Sphere;

        [Tooltip("The shape's size, by axis")]
        public Vector3 Size = Vector3.one;

        [Tooltip("The shape's place, from this object")]
        public Vector3 Offset = Vector3.zero;

        [Tooltip("The shape's turn, from this object, in degrees")]
        public Vector3 Rotation = Vector3.zero;

        [Tooltip("Its color")]
        public Color Color = Color.white;

        [Tooltip("Degrees a second it spins about its up, as a crystal turns")]
        public float Spin;

        private GameObject made;

        /// <summary>The shape it made, or null before it wakes.</summary>
        public Transform Made => made ? made.transform : null;

        private void Awake() => Make();

        /// <summary>
        /// A shape made in code: its object made inactive, so it takes its fields before it wakes and makes its primitive.
        /// A shape added to an active object wakes as it's added, before its fields are set, as a plain sphere.
        /// </summary>
        public static SurvivorsShape Create(string name, Transform parent, PrimitiveType shape, Vector3 size, Color color,
            Vector3 offset = default, Vector3 rotation = default, float spin = 0f)
        {
            var host = new GameObject(name);
            host.SetActive(false);
            if (parent) host.transform.SetParent(parent, false);
            var made = host.AddComponent<SurvivorsShape>();
            made.Shape = shape;
            made.Size = size;
            made.Color = color;
            made.Offset = offset;
            made.Rotation = rotation;
            made.Spin = spin;
            host.SetActive(true);
            return made;
        }

        private void Make()
        {
            if (made) return;
            made = GameObject.CreatePrimitive(Shape);
            made.name = $"{name} Shape";
            made.transform.SetParent(transform, false);
            made.transform.localPosition = Offset;
            made.transform.localRotation = Quaternion.Euler(Rotation);
            made.transform.localScale = Size;
            if (made.TryGetComponent<Collider>(out var collider)) Destroy(collider);
            var look = made.GetComponent<Renderer>();
            look.shadowCastingMode = ShadowCastingMode.Off;
            Tint(look, Color);
        }

        private void Update()
        {
            if (Spin != 0f && made) made.transform.Rotate(0f, Spin * Time.deltaTime, 0f, Space.World);
        }

        /// <summary>Tints its shape again, as a crystal worth more turns another color.</summary>
        public void Recolor(Color color)
        {
            Color = color;
            Make();
            Tint(made.GetComponent<Renderer>(), color);
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
