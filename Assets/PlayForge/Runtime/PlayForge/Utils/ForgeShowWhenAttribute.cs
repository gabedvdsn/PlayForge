using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Shows a field in its inspector only while another field of the same object holds one of the values given, as a
    /// randomized scaler's multipliers show only for its Multiplier mode (ED-51). The inspector reads it; nothing at
    /// runtime does. The other field is an enum or a bool.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ForgeShowWhenAttribute : System.Attribute
    {
        /// <summary>The field whose value decides, by its name.</summary>
        public string Field { get; }

        /// <summary>The values that show this field.</summary>
        public object[] Values { get; }

        public ForgeShowWhenAttribute(string field, params object[] values)
        {
            Field = field;
            Values = values ?? Array.Empty<object>();
        }
    }
}
