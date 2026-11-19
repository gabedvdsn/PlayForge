using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>How a floating word looks and moves: its height in world units, how long it lasts, how far it rises.</summary>
    [Serializable]
    public struct FloatStyle
    {
        [Tooltip("Its height, in world units")]
        [Min(0.01f)] public float Size;

        [Tooltip("How long it floats, in seconds")]
        [Min(0.05f)] public float Lifetime;

        [Tooltip("How far it rises over its lifetime, in world units")]
        public float Rise;

        [Tooltip("How far to either side it may start from its place, in world units, so words landing together part")]
        [Min(0f)] public float Spread;

        public static FloatStyle Default => new() { Size = 0.45f, Lifetime = 0.9f, Rise = 1.2f, Spread = 0.3f };

        /// <summary>This style, larger by a factor.</summary>
        public FloatStyle Scaled(float factor) => new() { Size = Size * factor, Lifetime = Lifetime, Rise = Rise, Spread = Spread };
    }

    /// <summary>
    /// The world-space canvas words float on: one a scene, made as the first word floats where the scene has none,
    /// holding TextMeshPro texts it reuses. Each word rises and fades over its lifetime, turned to face the camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("PlayForge/Additional Content/Floating Text Layer")]
    public class FloatingTextLayer : MonoBehaviour
    {
        [Tooltip("The camera the words face; with none, the main camera")]
        public Camera Camera;

        [Tooltip("The font; with none, TextMeshPro's default, which a project has once it imports TextMeshPro's Essential Resources")]
        public TMP_FontAsset Font;

        [Tooltip("A material for the font, as an outlined one; with none, the font's own")]
        public Material Material;

        [Tooltip("The canvas's sorting order, so the words draw over the scene's other world-space canvases")]
        public int SortingOrder = 100;

        // The texts' font size: a word's height in world units scales it
        private const float FontSize = 36f;

        /// <summary>A word floating now: what it says, its color, where it is, and how far through its lifetime.</summary>
        public readonly struct Word
        {
            public readonly string Text;
            public readonly Color Color;
            public readonly Vector3 Position;
            public readonly float Progress;
            public readonly float Size;

            public Word(string text, Color color, Vector3 position, float progress, float size)
            {
                Text = text;
                Color = color;
                Position = position;
                Progress = progress;
                Size = size;
            }
        }

        private sealed class Floater
        {
            public TextMeshProUGUI Text;
            public Vector3 Start;
            public Color Color;
            public FloatStyle Style;
            public float Age;
        }

        private static FloatingTextLayer current;
        private readonly List<Floater> floating = new();
        private readonly Stack<Floater> spare = new();
        private readonly List<Word> words = new();
        private Canvas canvas;
        private bool warned;

        /// <summary>The scene's layer: the one enabled, or one made now.</summary>
        public static FloatingTextLayer Current
        {
            get
            {
                if (current != null) return current;
                current = FindAnyObjectByType<FloatingTextLayer>();
                if (current != null) return current;
                var made = new GameObject("Floating Text", typeof(RectTransform), typeof(Canvas));
                current = made.AddComponent<FloatingTextLayer>();
                return current;
            }
        }

        /// <summary>The words floating now, newest last.</summary>
        public IReadOnlyList<Word> Words
        {
            get
            {
                words.Clear();
                foreach (var f in floating)
                    words.Add(new Word(f.Text.text, f.Color, f.Text.transform.position, f.Age / f.Style.Lifetime, f.Style.Size));
                return words;
            }
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;
            var rect = (RectTransform)transform;
            rect.position = Vector3.zero;
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }

        private void OnEnable()
        {
            if (current == null) current = this;
        }

        private void OnDisable()
        {
            if (current == this) current = null;
        }

        /// <summary>Floats a word from a place in the world, in a color, as the style says.</summary>
        public void Float(Vector3 world, string text, Color color, FloatStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            var f = spare.Count > 0 ? spare.Pop() : Make();
            if (f == null) return;
            f.Style = style;
            f.Color = color;
            f.Age = 0f;
            float spread = style.Spread;
            f.Start = world + (spread > 0f ? new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, UnityEngine.Random.Range(-spread, spread) * 0.5f) : Vector3.zero);
            f.Text.text = text;
            f.Text.color = color;
            f.Text.gameObject.SetActive(true);
            floating.Add(f);
            Place(f);
        }

        // A word's text, in the layer's font or TextMeshPro's default. With neither, as in a project without TextMeshPro's
        // Essential Resources, there's none, and the layer says so once
        private Floater Make()
        {
            var font = Font != null ? Font : TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (font == null)
            {
                if (!warned)
                    ForgeLog.Warning("The Floating Text Layer has no font, so no words float: give it a Font, or import " +
                                     "TextMeshPro's Essential Resources (Window > TextMeshPro > Import TMP Essential Resources)", this);
                warned = true;
                return null;
            }
            var go = new GameObject("Word", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            if (Material != null) text.fontSharedMaterial = Material;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(FontSize * 8f, FontSize * 1.5f);
            return new Floater { Text = text };
        }

        private void LateUpdate()
        {
            for (int i = floating.Count - 1; i >= 0; i--)
            {
                var f = floating[i];
                f.Age += Time.deltaTime;
                if (f.Age >= f.Style.Lifetime)
                {
                    f.Text.gameObject.SetActive(false);
                    floating.RemoveAt(i);
                    spare.Push(f);
                    continue;
                }
                Place(f);
            }
        }

        // Where it is through its lifetime, facing the camera, fading over its last third
        private void Place(Floater f)
        {
            float progress = Mathf.Clamp01(f.Age / f.Style.Lifetime);
            var t = f.Text.transform;
            t.position = f.Start + Vector3.up * (f.Style.Rise * (1f - (1f - progress) * (1f - progress)));
            var view = Camera != null ? Camera : Camera.main;
            if (view != null) t.rotation = view.transform.rotation;
            t.localScale = Vector3.one * (f.Style.Size / FontSize);
            var color = f.Color;
            color.a *= progress < 2f / 3f ? 1f : 1f - (progress - 2f / 3f) * 3f;
            f.Text.color = color;
        }
    }
}
