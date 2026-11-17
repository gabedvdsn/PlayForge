using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Where a floating text's number comes from.</summary>
    public enum EFloatingValue
    {
        /// <summary>
        /// The cue's magnitude: a hit's value, from an effect's On Hit; its stacks, from On Stack Change; a Play Cue
        /// task's number.
        /// </summary>
        Magnitude,

        /// <summary>Its Number field, read as it plays, as a Tracked Impact's for the gold an instant effect gave.</summary>
        Number,
    }

    /// <summary>A floating text's number, and how it's written.</summary>
    [Serializable]
    public class FloatingTextValue
    {
        [Tooltip("Where its number comes from: the cue's magnitude, as a hit's value, or the Number below")]
        public EFloatingValue From = EFloatingValue.Magnitude;

        [Tooltip("Its number, read as it plays, where it comes from here")]
        public ScalableFloat Number = 0f;

        [Tooltip("The most decimals it's written with; trailing zeros are dropped")]
        [Range(0, 4)] public int Decimals;

        [Tooltip("Written without its sign, as damage taken shown as 35")]
        public bool Absolute = true;

        [Tooltip("A plus before a number above 0, as +50")]
        public bool PlusSign;

        [Tooltip("Shortened past a thousand, as 1.2k, 3.4M and 5B")]
        public bool Abbreviate;

        [Tooltip("No text for a number of 0, as a hit absorbed whole")]
        public bool HideZero = true;

        /// <summary>The number the cue shows.</summary>
        public float Read(in CueContext cue) =>
            From == EFloatingValue.Number ? Number?.Evaluate(NumberContext.Of(in cue)) ?? 0f : cue.Magnitude;

        /// <summary>A number, written as this says.</summary>
        public string Write(float value)
        {
            float shown = Absolute ? Mathf.Abs(value) : value;
            string suffix = "";
            if (Abbreviate)
            {
                float size = Mathf.Abs(shown);
                if (size >= 1e9f)
                {
                    shown /= 1e9f;
                    suffix = "B";
                }
                else if (size >= 1e6f)
                {
                    shown /= 1e6f;
                    suffix = "M";
                }
                else if (size >= 1e3f)
                {
                    shown /= 1e3f;
                    suffix = "k";
                }
            }
            // A shortened number keeps a decimal, as 1.2k
            int decimals = Mathf.Clamp(suffix.Length > 0 ? Mathf.Max(Decimals, 1) : Decimals, 0, 4);
            var text = shown.ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), CultureInfo.CurrentCulture) + suffix;
            return PlusSign && shown > 0f ? "+" + text : text;
        }
    }

    /// <summary>How a floating text looks.</summary>
    [Serializable]
    public class FloatingTextLook
    {
        [Tooltip("Its color")]
        public Color Color = Color.white;

        [Tooltip("Colored by its number's sign: its Color for 0 and above, its Negative Color below, as damage taken")]
        public bool BySign;

        [Tooltip("Its color for a number below 0, where By Sign is on")]
        public Color NegativeColor = new(1f, 0.35f, 0.3f);

        [Tooltip("Its size, as TextMeshPro's font size")]
        [Min(0.01f)] public float Size = 4f;

        [Tooltip("Its font; TextMeshPro's default where none is given")]
        public TMP_FontAsset Font;

        [Tooltip("Written in bold")]
        public bool Bold = true;

        [Tooltip("Its outline's width, 0 for none")]
        [Range(0f, 1f)] public float Outline = 0.2f;

        [Tooltip("Its outline's color")]
        public Color OutlineColor = Color.black;

        [Tooltip("Turned to face the camera as it floats: ForgeCues.Camera, or the main camera")]
        public bool FaceCamera = true;

        [Tooltip("The order it draws in among others, higher on top")]
        public int SortingOrder;

        [Tooltip("A prefab to show it with, holding a TextMeshPro text, in place of the text the cue makes; its font, size " +
                 "and outline are its own, its color the cue's")]
        public GameObject Prefab;
    }

    /// <summary>How a floating text moves and fades over its life.</summary>
    [Serializable]
    public class FloatingTextMotion
    {
        [Tooltip("How long it floats, in seconds")]
        [Min(0.01f)] public float Lifetime = 1.2f;

        [Tooltip("How it moves as it appears, in the world's units a second")]
        public Vector3 Velocity = new(0f, 1.5f, 0f);

        [Tooltip("And by a random amount, up to this much either way on each axis")]
        public Vector3 RandomVelocity = new(0.4f, 0.2f, 0.4f);

        [Tooltip("Pulled down by this much a second, each second, for an arc; 0 keeps its course")]
        public float Gravity;

        [Tooltip("Slowed by this share of its speed each second; 0 keeps its speed")]
        [Min(0f)] public float Drag = 1f;

        [Tooltip("Its opacity over its life, its start at 0 and its end at 1")]
        public AnimationCurve Fade = new(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

        [Tooltip("Its scale over its life, as a pop as it appears")]
        public AnimationCurve Scale = new(new Keyframe(0f, 0.5f), new Keyframe(0.12f, 1.25f), new Keyframe(0.25f, 1f),
            new Keyframe(1f, 1f));

        [Tooltip("On the game's clock, slowed and paused with it; off, in real time")]
        public bool GameTime = true;
    }

    /// <summary>How the floating texts on one entity merge, stagger, and how many show.</summary>
    [Serializable]
    public class FloatingTextStacking
    {
        [Tooltip("A number raised on the same one within this many seconds adds to the last one's, which shows the sum " +
                 "and floats again, as a burn's ticks; 0 never merges")]
        [Min(0f)] public float MergeWithin;

        [Tooltip("Each text floats this much above the one below it on the same one, each in the lowest place free, so " +
                 "they don't overlap")]
        [Min(0f)] public float Stagger;

        [Tooltip("At most this many float on one at once, the oldest going first; 0 for any number")]
        [Min(0)] public int MaxShown;
    }

    /// <summary>
    /// Floats a text where its placement says, above the one the cue is on by default: a hit's damage from an effect's
    /// On Hit, the gold a kill gave, a status's name as it lands. It says its Text, where {value} is its number, written
    /// as its Value says, {name} the name of what raised it, as an effect's, and {tag} its cue tag; it moves, fades and
    /// scales as its Motion says, looks as its Look says, and merges, staggers and caps as its Stacking says. Its texts
    /// come from <see cref="ForgeCuePool"/> and go back to it.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Floats a text, as a hit's damage, the gold a kill gave or a status's name, where its placement says.")]
    public class FloatingTextCue : AbstractCueHandler
    {
        [Tooltip("What it says: {value} for its number, {name} for the name of what raised it, as an effect's, {tag} for " +
                 "its cue tag")]
        public string Text = "{value}";

        [Tooltip("Its number, and how it's written")]
        public FloatingTextValue Value = new();

        [Tooltip("Where it appears, on whom: above them by default, scattered as asked")]
        public CuePlacement Placement = new() { Offset = new Vector3(0f, 2f, 0f) };

        [Tooltip("How it looks")]
        public FloatingTextLook Look = new();

        [Tooltip("How it moves and fades")]
        public FloatingTextMotion Motion = new();

        [Tooltip("How its texts on one entity merge, stagger, and how many show")]
        public FloatingTextStacking Stacking = new();

        // The texts still floating, on each entity, of this cue
        [NonSerialized] private Dictionary<object, List<ForgeFloatingText>> shown;

        // Every burst floats it, whatever raised it: the list it's in says when
        public override void Burst(CueContext cue) => Float(cue);

        // A lasting cue floats it as it begins, as a status's name as it lands
        public override void Begin(CueContext cue) => Float(cue);

        private void Float(in CueContext cue)
        {
            var value = Value ??= new FloatingTextValue();
            var look = Look ??= new FloatingTextLook();
            var motion = Motion ??= new FloatingTextMotion();
            var stacking = Stacking ??= new FloatingTextStacking();
            float number = value.Read(cue);
            bool numbered = Text != null && Text.Contains("{value}");
            if (numbered && value.HideZero && Mathf.Approximately(number, 0f)) return;

            foreach (var place in (Placement ??= new CuePlacement()).Resolve(cue, lasting: false))
            {
                object key = (object)place.On ?? cue.Target ?? (object)this;
                var live = LiveOn(key);

                // A number close behind the last on them adds to it, which shows the sum and floats again
                if (numbered && stacking.MergeWithin > 0f && live.Count > 0 && live[^1].Age <= stacking.MergeWithin)
                {
                    var last = live[^1];
                    float sum = last.Value + number;
                    last.Merge(Say(cue, sum), sum);
                    continue;
                }

                // Past its cap, the oldest goes first
                while (stacking.MaxShown > 0 && live.Count >= stacking.MaxShown)
                {
                    var oldest = live[0];
                    live.RemoveAt(0);
                    oldest.Finish();
                }

                // In the lowest place free on them, each a Stagger above the last
                int slot = 0;
                while (live.Exists(each => each.Slot == slot)) slot++;
                var text = ForgeFloatingText.Take(look, place.Position + Vector3.up * (stacking.Stagger * slot), place.Rotation,
                    place.Parent);
                text.Slot = slot;
                live.Add(text);
                text.Show(Say(cue, number), number, look, motion, done =>
                {
                    live.Remove(done);
                    if (live.Count == 0) shown?.Remove(key);
                });
            }
        }

        // The texts of this cue still floating on one, those ended or destroyed dropped
        private List<ForgeFloatingText> LiveOn(object key)
        {
            shown ??= new Dictionary<object, List<ForgeFloatingText>>();
            if (!shown.TryGetValue(key, out var live)) shown[key] = live = new List<ForgeFloatingText>();
            live.RemoveAll(each => each == null || !each.Floating);
            return live;
        }

        // Its Text, its number, name and tag put in
        private string Say(in CueContext cue, float number)
        {
            var text = Text ?? "";
            if (text.Contains("{value}")) text = text.Replace("{value}", Value.Write(number));
            if (text.Contains("{name}")) text = text.Replace("{name}", NameOf(cue));
            if (text.Contains("{tag}")) text = text.Replace("{tag}", cue.Tag.IsValid ? cue.Tag.ToString() : "");
            return text;
        }

        // The name of what raised it: an effect's for an effect's cue; else its origin's, as an ability's or an item's
        private static string NameOf(in CueContext cue) =>
            cue.Spec?.Base?.GetName() ?? cue.Origin?.GetReadableDefinition()?.GetName() ?? "";
    }
}
