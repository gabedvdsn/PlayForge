using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// A bar of icons over an entity, one for each effect on it that shows (AC-2): the effect's icon, or the first letters
    /// of its name where it has none; its stacks; and a shade that fills as its time runs out. It reads the entity's
    /// effects each frame and leaves out those marked Not Visible, as cooldowns and items' bonuses are. On a world-space
    /// canvas of its own, turned to face the camera.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayForge/Additional Content/Status Bar")]
    public class StatusBar : EntityFeedback
    {
        [Tooltip("Where the bar sits, from the entity's place")]
        public Vector3 Offset = new(0f, 2.6f, 0f);

        [Tooltip("Each icon's width and height, in world units")]
        [Min(0.05f)] public float IconSize = 0.35f;

        [Tooltip("The gap between two icons, in world units")]
        [Min(0f)] public float Spacing = 0.06f;

        [Tooltip("The most icons it shows, the earliest effects first")]
        [Min(1)] public int MaxIcons = 8;

        [Tooltip("Shows an effect's stacks where it has more than one")]
        public bool ShowStacks = true;

        [Tooltip("Shades each icon as its effect's time runs out")]
        public bool ShowTimeLeft = true;

        [Tooltip("The shade over the time an effect has used")]
        public Color TimeShade = new(0f, 0f, 0f, 0.6f);

        [Tooltip("The font of its letters and stacks; with none, TextMeshPro's default")]
        public TMP_FontAsset Font;

        [Tooltip("The camera the bar faces; with none, the main camera")]
        public Camera Camera;

        // Canvas units in a world unit
        private const float Units = 100f;

        /// <summary>An icon the bar shows: its effect, its stacks, how much of its time is left, and what it shows.</summary>
        public readonly struct Icon
        {
            public readonly GameplayEffect Effect;
            public readonly int Stacks;
            public readonly float TimeLeft;
            public readonly Texture2D Image;
            public readonly string Letters;

            public Icon(GameplayEffect effect, int stacks, float timeLeft, Texture2D image, string letters)
            {
                Effect = effect;
                Stacks = stacks;
                TimeLeft = timeLeft;
                Image = image;
                Letters = letters;
            }
        }

        private sealed class Slot
        {
            public RectTransform Rect;
            public Image Background;
            public Image Shade;
            public TextMeshProUGUI Letters;
            public TextMeshProUGUI Stacks;
        }

        private static readonly Dictionary<Texture2D, Sprite> sprites = new();
        private readonly List<Icon> icons = new();
        private readonly List<Slot> slots = new();
        private readonly Dictionary<GameplayEffect, int> found = new();
        private RectTransform bar;

        /// <summary>The icons it shows now, in their order.</summary>
        public IReadOnlyList<Icon> Icons => icons;

        protected override void OnDisable()
        {
            base.OnDisable();
            if (bar != null) bar.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Gather();
            if (icons.Count == 0 && bar == null) return;
            Build();
            bar.gameObject.SetActive(icons.Count > 0);
            if (icons.Count == 0) return;
            Show();
            bar.position = transform.position + Offset;
            var view = Camera != null ? Camera : Camera.main;
            if (view != null) bar.rotation = view.transform.rotation;
        }

        // The entity's effects that show, one icon an effect: stacks summed, the longest time kept
        private void Gather()
        {
            icons.Clear();
            found.Clear();
            var shelf = Watched?.GetEffectSystem()?.Shelf;
            if (shelf == null) return;
            foreach (var container in shelf)
            {
                var effect = container?.Spec?.Base;
                if (effect == null || effect.Definition == null || effect.Definition.Visibility == Tags.NOT_VISIBLE) continue;
                int stacks = container is AbstractStackingEffectContainer stacking ? Mathf.Max(1, stacking.Stacks) : 1;
                float total = container.TotalDuration;
                float left = total <= 0f || total >= 1e30f ? 1f : Mathf.Clamp01(container.NextDurationRemaining / total);
                if (found.TryGetValue(effect, out int at))
                {
                    var was = icons[at];
                    icons[at] = new Icon(effect, was.Stacks + stacks, Mathf.Max(was.TimeLeft, left), was.Image, was.Letters);
                    continue;
                }
                if (icons.Count >= MaxIcons) continue;
                found[effect] = icons.Count;
                // Its spec's icon, which may be its origin's (FN-25)
                icons.Add(new Icon(effect, stacks, left, container.Spec.GetIcon(), Letters(effect.GetName())));
            }
        }

        // Its first letters, as "Sl" for Slow and "BF" for Burning Fury
        private static string Letters(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var words = name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            var word = words[0];
            return word.Length > 1 ? $"{char.ToUpperInvariant(word[0])}{word[1]}" : word.ToUpperInvariant();
        }

        private void Build()
        {
            if (bar != null) return;
            var go = new GameObject("Status Bar", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            bar = (RectTransform)go.transform;
            bar.localScale = Vector3.one / Units;
            bar.sizeDelta = Vector2.zero;
        }

        private void Show()
        {
            float size = IconSize * Units;
            float gap = Spacing * Units;
            float width = icons.Count * size + (icons.Count - 1) * gap;
            while (slots.Count < icons.Count) slots.Add(MakeSlot(size));
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool on = i < icons.Count;
                slot.Rect.gameObject.SetActive(on);
                if (!on) continue;
                var icon = icons[i];
                slot.Rect.sizeDelta = new Vector2(size, size);
                slot.Rect.anchoredPosition = new Vector2(-width / 2f + size / 2f + i * (size + gap), 0f);
                if (icon.Image != null)
                {
                    slot.Background.sprite = SpriteOf(icon.Image);
                    slot.Background.color = Color.white;
                    slot.Letters.text = "";
                }
                else
                {
                    slot.Background.sprite = null;
                    slot.Background.color = ColorOf(icon.Effect.GetName());
                    slot.Letters.text = icon.Letters;
                }
                slot.Shade.enabled = ShowTimeLeft && icon.TimeLeft < 1f;
                slot.Shade.fillAmount = 1f - icon.TimeLeft;
                slot.Shade.color = TimeShade;
                slot.Stacks.text = ShowStacks && icon.Stacks > 1 ? icon.Stacks.ToString() : "";
            }
        }

        private Slot MakeSlot(float size)
        {
            var root = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(bar, false);
            var slot = new Slot { Rect = (RectTransform)root.transform, Background = root.GetComponent<Image>() };
            slot.Background.raycastTarget = false;

            var shade = new GameObject("Time Used", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            Fill((RectTransform)shade.transform);
            slot.Shade = shade.GetComponent<Image>();
            slot.Shade.type = Image.Type.Filled;
            slot.Shade.fillMethod = Image.FillMethod.Radial360;
            slot.Shade.fillOrigin = (int)Image.Origin360.Top;
            slot.Shade.fillClockwise = true;
            slot.Shade.raycastTarget = false;

            slot.Letters = MakeText(root.transform, "Letters", size * 0.42f, TextAlignmentOptions.Center);
            slot.Stacks = MakeText(root.transform, "Stacks", size * 0.36f, TextAlignmentOptions.BottomRight);
            return slot;
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Fill((RectTransform)go.transform);
            var text = go.AddComponent<TextMeshProUGUI>();
            var font = Font != null ? Font : TMP_Settings.defaultFontAsset;
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite SpriteOf(Texture2D texture)
        {
            if (sprites.TryGetValue(texture, out var sprite) && sprite != null) return sprite;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprites[texture] = sprite;
            return sprite;
        }

        // A color of its own for each name, muted, so effects with no icon tell apart
        private static Color ColorOf(string name)
        {
            int hash = 17;
            foreach (char c in name ?? "") hash = hash * 31 + c;
            float hue = (hash & 0xFFFF) / 65535f;
            return Color.HSVToRGB(hue, 0.55f, 0.6f);
        }
    }
}
