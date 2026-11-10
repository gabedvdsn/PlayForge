using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The least a number field's constant takes in the inspector (NUM-15), as Unity's <c>[Min]</c> is for a float:
    /// <code>
    /// [NumberMin(0)] public ScalableFloat Duration = 2f;
    /// </code>
    /// <para>
    /// A number field takes these in place of Unity's <c>[Min]</c> and <c>[Range]</c>. Unity gives a field that has one
    /// of its own to that attribute's drawer, which draws "Use Min with float, int or Vector" where the number was, so
    /// the console names a number field that has one as scripts load.
    /// </para>
    /// <para>
    /// The limit is the inspector's, as Unity's is: it keeps the constant typed in. A number of a kind is worked out as
    /// the game runs, so where what it reads must stay inside limits, give it the Clamped kind, or clamp what the code
    /// reads. On a field whose unit is "%" the limit is in what the field stores: <c>[NumberRange(0, 1)]</c> is 0% to
    /// 100%. A whole-number field takes the whole numbers inside the limits.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NumberMinAttribute : System.Attribute
    {
        /// <summary>The least the constant takes.</summary>
        public float Min { get; }

        public NumberMinAttribute(float min)
        {
            Min = min;
        }
    }

    /// <summary>
    /// The most a number field's constant takes in the inspector (NUM-15): <c>[NumberMax(10)]</c>. With
    /// <see cref="NumberMinAttribute"/> beside it, the field takes what lies between the two. See
    /// <see cref="NumberMinAttribute"/> for what a limit reaches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NumberMaxAttribute : System.Attribute
    {
        /// <summary>The most the constant takes.</summary>
        public float Max { get; }

        public NumberMaxAttribute(float max)
        {
            Max = max;
        }
    }

    /// <summary>
    /// A number field whose constant is set on a slider between two values (NUM-15), as Unity's <c>[Range]</c> is for a
    /// float: <c>[NumberRange(0, 1)] public ScalableFloat Chance = 0.5f;</c>. See <see cref="NumberMinAttribute"/> for
    /// what a limit reaches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NumberRangeAttribute : System.Attribute
    {
        /// <summary>The slider's least value.</summary>
        public float Min { get; }

        /// <summary>The slider's most value.</summary>
        public float Max { get; }

        public NumberRangeAttribute(float min, float max)
        {
            Min = Math.Min(min, max);
            Max = Math.Max(min, max);
        }
    }
}
