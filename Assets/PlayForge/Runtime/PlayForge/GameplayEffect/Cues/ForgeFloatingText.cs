using System;
using TMPro;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A text a Floating Text cue floats: it moves, fades and scales over its life by its cue's motion, on the game's
    /// clock or in real time, faces the camera where asked, and goes back to its pool as its life ends. A number merged
    /// into it starts its life again.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ForgeFloatingText : MonoBehaviour, IPooledCue
    {
        // What the cue's own texts are made from: a TextMeshPro text of its own, made the first time one floats
        private static GameObject template;

        private TMP_Text label;
        private FloatingTextLook look;
        private FloatingTextMotion motion;
        private Action<ForgeFloatingText> ended;
        private Vector3 velocity;
        private Vector3 scale = Vector3.one;
        private Color color = Color.white;
        private float lifetime = 1f;

        /// <summary>How long it's floated, in seconds, since it appeared or a number last merged into it.</summary>
        public float Age { get; private set; }

        /// <summary>The number it shows, the sum of those merged into it.</summary>
        public float Value { get; private set; }

        /// <summary>What it says.</summary>
        public string Text => label != null ? label.text : null;

        /// <summary>Whether it's floating, not yet back in its pool.</summary>
        public bool Floating { get; private set; }

        /// <summary>Its place among the texts its cue floats on one entity, each a Stagger above the last.</summary>
        public int Slot { get; internal set; }

        // Its scale as it was made, which its Scale curve scales
        private void Awake() => scale = transform.localScale;

        /// <summary>A text from its pool, made from the look's prefab or the cue's own, placed.</summary>
        internal static ForgeFloatingText Take(FloatingTextLook look, Vector3 position, Quaternion rotation, Transform parent)
        {
            var made = ForgeCuePool.Take(look.Prefab != null ? look.Prefab : Template(), position, rotation, parent);
            var text = made.GetComponent<ForgeFloatingText>();
            if (text == null) text = made.AddComponent<ForgeFloatingText>();
            if (text.label == null) text.label = made.GetComponentInChildren<TMP_Text>(true);
            return text;
        }

        private static GameObject Template()
        {
            if (template != null) return template;
            template = new GameObject("PlayForge Floating Text");
            template.SetActive(false);
            if (Application.isPlaying) DontDestroyOnLoad(template);
            var text = template.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            template.AddComponent<ForgeFloatingText>();
            return template;
        }

        /// <summary>Shows its words and starts its life, told as it ends.</summary>
        internal void Show(string words, float value, FloatingTextLook look, FloatingTextMotion motion, Action<ForgeFloatingText> ended)
        {
            this.look = look;
            this.motion = motion;
            this.ended = ended;
            Floating = true;
            lifetime = Mathf.Max(0.01f, motion.Lifetime);
            var spread = motion.RandomVelocity;
            velocity = motion.Velocity + new Vector3(Spread(spread.x), Spread(spread.y), Spread(spread.z));
            if (label != null && look.Prefab == null) Style();
            Say(words, value);
        }

        /// <summary>A number merged into it: it shows the sum, and its life starts again.</summary>
        internal void Merge(string words, float value) => Say(words, value);

        private void Say(string words, float value)
        {
            Value = value;
            Age = 0f;
            color = look != null && look.BySign && value < 0f ? look.NegativeColor : look?.Color ?? Color.white;
            if (label != null) label.text = words;
            Apply();
        }

        // Its own text's look: the cue's size, font, weight, outline and order; a prefab's text keeps its own
        private void Style()
        {
            label.fontSize = look.Size;
            if (look.Font != null) label.font = look.Font;
            label.fontStyle = look.Bold ? FontStyles.Bold : FontStyles.Normal;
            if (label.font != null)
            {
                label.outlineWidth = look.Outline;
                label.outlineColor = look.OutlineColor;
            }
            if (label.TryGetComponent<Renderer>(out var drawn)) drawn.sortingOrder = look.SortingOrder;
        }

        private void Update()
        {
            if (!Floating || motion == null) return;
            float dt = motion.GameTime ? ForgeTime.DeltaTime : ForgeTime.UnscaledDeltaTime;
            Age += dt;
            velocity += Vector3.down * (motion.Gravity * dt);
            if (motion.Drag > 0f) velocity /= 1f + motion.Drag * dt;
            transform.position += velocity * dt;
            Apply();
            if (Age >= lifetime) Finish();
        }

        // Its opacity, its scale and its facing at its age
        private void Apply()
        {
            float t = Mathf.Clamp01(Age / lifetime);
            if (label != null)
            {
                var shown = color;
                if (motion?.Fade != null && motion.Fade.length > 0) shown.a *= Mathf.Clamp01(motion.Fade.Evaluate(t));
                label.color = shown;
            }
            float size = motion?.Scale != null && motion.Scale.length > 0 ? motion.Scale.Evaluate(t) : 1f;
            transform.localScale = scale * size;
            if (look == null || !look.FaceCamera) return;
            var camera = ForgeInput.CameraOr(ForgeCues.Camera);
            if (camera != null) transform.rotation = camera.transform.rotation;
        }

        /// <summary>Ends it now: its cue told, and back to its pool.</summary>
        public void Finish()
        {
            if (!Floating) return;
            Floating = false;
            var told = ended;
            ended = null;
            told?.Invoke(this);
            ForgeCuePool.Give(gameObject);
        }

        // A number up to this much either way, from the cues' own random numbers
        private static float Spread(float most) => most == 0f ? 0f : ForgeCues.Random.Value * 2f * most - most;

        void IPooledCue.Taken() { }

        void IPooledCue.Returned()
        {
            Floating = false;
            ended = null;
        }
    }
}
