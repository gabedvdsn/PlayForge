using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A kind's field shown only while another field of the same object says so (FN-20): a toggle that's on, or, given
    /// values, an enum or a whole number that's one of them, as a targeting task's Detection Layers under Use Physics. A kind
    /// field, as a task's or a scaler's, draws its object's fields with this read, hiding one where it doesn't apply
    /// (FN-23); it shows again as the field it reads changes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShownWhenAttribute : System.Attribute
    {
        /// <summary>The field it reads, of the same object, by its name.</summary>
        public string Field { get; }

        /// <summary>The values it shows under, where the field is an enum or a whole number; none, a toggle that's on.</summary>
        public int[] Values { get; }

        public ShownWhenAttribute(string field, params int[] values)
        {
            Field = field;
            Values = values ?? Array.Empty<int>();
        }
    }
}
