using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Examples
{
    /// <summary>
    /// The Demo's outline. Each renderer under the object draws once more with an outline material, scaled up from its
    /// pivot and in one color, behind the object itself. It replaced QuickOutline, whose licence reserves all rights
    /// (Decision 15). Scaling from the pivot, not pushing along normals, keeps a cube's corners closed.
    /// </summary>
    [DisallowMultipleComponent]
    public class DemoOutline : MonoBehaviour
    {
        private const string MaterialName = "PlayForgeDemoOutline";
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");

        [Tooltip("The outline's color")]
        [SerializeField] private Color outlineColor = Color.white;

        [Tooltip("How far the outline reaches past the object, as a share of its size")]
        [Range(0f, 0.25f)]
        [SerializeField] private float outlineWidth = 0.06f;

        private Material _material;
        private readonly List<Renderer> _renderers = new();

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                Apply();
            }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            if (!_material)
            {
                // From Resources, so a build includes the shader
                var source = Resources.Load<Material>(MaterialName);
                if (!source) return;
                _material = new Material(source) { name = MaterialName, hideFlags = HideFlags.HideAndDontSave };
            }

            _renderers.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;

                var materials = r.sharedMaterials;
                if (Array.IndexOf(materials, _material) >= 0) continue;

                Array.Resize(ref materials, materials.Length + 1);
                materials[^1] = _material;
                r.sharedMaterials = materials;
                _renderers.Add(r);
            }

            Apply();
        }

        private void OnDisable()
        {
            foreach (var r in _renderers)
            {
                if (!r) continue;

                var materials = new List<Material>(r.sharedMaterials);
                if (materials.Remove(_material)) r.sharedMaterials = materials.ToArray();
            }
            _renderers.Clear();
        }

        private void OnDestroy()
        {
            if (_material) Destroy(_material);
        }

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (!_material) return;
            _material.SetColor(ColorId, outlineColor);
            _material.SetFloat(WidthId, outlineWidth);
        }
    }
}
