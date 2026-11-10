using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Another number field of the same object (FEAT-29, NUM-9): the number joins it as its Combine says, times it or plus
    /// it, so a Clamped scaler's Max can be <c>2 × Min</c> and a stack bound's Max <c>Min + 3</c>. The field is a number
    /// (<see cref="ScalableFloat"/> or <see cref="ScalableInt"/>), read in the same context, so a relative Max follows a
    /// Min that scales by level, or a plain float or int.
    /// <para>
    /// It reads its object through the owner its object's own code gives as it reads its numbers,
    /// <c>MaxValue.Evaluate(context, this)</c>, and a field whose code gives one declares
    /// <see cref="NumberReads.Owner"/> in its <see cref="NumberContextAttribute"/>. With no owner, with a field name its
    /// object no longer has, or in a loop of numbers that read each other, which stops 8 deep, its number reads its own
    /// value and logs once. It finds its field by name through an accessor made once for each type and field, so a read
    /// allocates nothing after the first.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Relative to a Field", Reads = NumberReads.Owner)]
    public class RelativeToFieldKind : NumberKind
    {
        [Tooltip("The other number field of the same object it reads, plain or a number. Its own number joins it as its " +
                 "Combine says: times it, or plus it")]
        [NumberSiblingField]
        public string Field;

        // How deep relative numbers read one another before a loop is assumed
        private const int MostDepth = 8;
        private static int depth;

        // The accessor last found, for the type it was found on and the name it was found by
        [NonSerialized] private Type foundOn;
        [NonSerialized] private string foundBy;
        [NonSerialized] private NumberSibling found;

        public override bool CanEvaluate(in NumberContext context) =>
            context.Owner != null && depth < MostDepth && SiblingOf(context.Owner) != null;

        protected internal override string WhyNot(in NumberContext context)
        {
            if (context.Owner == null) return "it reads another field of its object, and its object's code gave it none";
            if (depth >= MostDepth) return $"numbers read one another {MostDepth} deep, as a loop does";
            return $"{context.Owner.GetType().Name} has no number field named {Field}";
        }

        protected override float Evaluate(in NumberContext context)
        {
            var sibling = SiblingOf(context.Owner);
            depth++;
            try
            {
                return sibling.Read(context.Owner, in context);
            }
            finally
            {
                depth--;
            }
        }

        // "its Min Value"
        public override string Describe() => string.IsNullOrEmpty(Field) ? "another of its fields" : $"its {NumberText.Spaced(Field)}";

        private NumberSibling SiblingOf(object owner)
        {
            var type = owner.GetType();
            if (type == foundOn && string.Equals(foundBy, Field, StringComparison.Ordinal)) return found;
            found = NumberSibling.Of(type, Field);
            foundOn = type;
            foundBy = Field;
            return found;
        }

        /// <summary>
        /// The field of a type a relative number can read by a name: an instance field, its own or a base class's, that
        /// is a number or a plain float or int. Null where there's none.
        /// </summary>
        public static FieldInfo SiblingField(Type owner, string name) => NumberSibling.Of(owner, name)?.Field;

        /// <summary>Whether a field is one a relative number can read: a number, or a plain float or int.</summary>
        public static bool IsNumberField(FieldInfo field) => field != null && NumberSibling.KindOf(field.FieldType) != null;
    }

    /// <summary>Marks the string field that names a relative number's sibling, which the inspector picks from a menu (FEAT-29).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NumberSiblingFieldAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// How a relative number reads one field of its object: a number, evaluated in the reader's context, or a plain float or
    /// int. One is made for each type and field and kept (FEAT-29).
    /// </summary>
    internal sealed class NumberSibling
    {
        internal enum EKind
        {
            Number,
            Whole,
            Float,
            Int,
        }

        public readonly FieldInfo Field;
        private readonly EKind kind;
        private readonly Func<object, float> readFloat;
        private readonly Func<object, int> readInt;

        private static readonly Dictionary<Type, Dictionary<string, NumberSibling>> Known = new();

        private NumberSibling(FieldInfo field, EKind kind)
        {
            Field = field;
            this.kind = kind;
            if (kind == EKind.Float) readFloat = Getter<float>(field);
            else if (kind == EKind.Int) readInt = Getter<int>(field);
        }

        /// <summary>The field's value, in the context: a number evaluated, or a plain value read.</summary>
        public float Read(object owner, in NumberContext context)
        {
            switch (kind)
            {
                case EKind.Number: return Field.GetValue(owner) is ScalableFloat number ? number.Evaluate(in context) : 0f;
                case EKind.Whole: return Field.GetValue(owner) is ScalableInt whole ? whole.Evaluate(in context) : 0;
                case EKind.Float: return readFloat(owner);
                default: return readInt(owner);
            }
        }

        /// <summary>The way to read a type's field by name, made the first time and kept; null where it has no such field.</summary>
        public static NumberSibling Of(Type owner, string name)
        {
            if (owner == null || string.IsNullOrEmpty(name)) return null;
            if (!Known.TryGetValue(owner, out var byName)) Known[owner] = byName = new Dictionary<string, NumberSibling>();
            if (byName.TryGetValue(name, out var found)) return found;

            found = null;
            for (var each = owner; each != null && each != typeof(object) && found == null; each = each.BaseType)
            {
                var field = each.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null && KindOf(field.FieldType) is { } kind) found = new NumberSibling(field, kind);
            }
            byName[name] = found;
            return found;
        }

        /// <summary>What a field of a type reads as: a number, a whole number, a float or an int; null for another type.</summary>
        internal static EKind? KindOf(Type type)
        {
            if (typeof(ScalableFloat).IsAssignableFrom(type)) return EKind.Number;
            if (typeof(ScalableInt).IsAssignableFrom(type)) return EKind.Whole;
            if (type == typeof(float)) return EKind.Float;
            if (type == typeof(int)) return EKind.Int;
            return null;
        }

        // A plain field's getter. Compiled where code can be, so a read neither reflects nor boxes; IL2CPP can't compile
        // code, so there it reads through reflection, which boxes the value
        private static Func<object, T> Getter<T>(FieldInfo field)
        {
#if ENABLE_IL2CPP
            return owner => (T)field.GetValue(owner);
#else
            try
            {
                var owner = System.Linq.Expressions.Expression.Parameter(typeof(object), "owner");
                var read = System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Convert(owner, field.DeclaringType), field);
                return System.Linq.Expressions.Expression.Lambda<Func<object, T>>(read, owner).Compile();
            }
            catch (Exception)
            {
                return owner => (T)field.GetValue(owner);
            }
#endif
        }
    }
}
