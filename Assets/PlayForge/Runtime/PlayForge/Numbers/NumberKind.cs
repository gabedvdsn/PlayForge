using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A kind of number of your own (FEAT-29): one class with one method. Override <see cref="Evaluate(in NumberContext)"/>
    /// to work out the kind's value from what the context offers, and <see cref="Describe"/> for the field's one-line
    /// summary. Name it, file it and say what it reads with <see cref="NumberKindAttribute"/>, and the kind menu offers it
    /// in every number field whose context provides what it reads:
    /// <code>
    /// [Serializable]
    /// [NumberKind("Distance to Target", Category = "Space", Reads = NumberReads.Caster | NumberReads.Target)]
    /// public class DistanceToTarget : NumberKind
    /// {
    ///     [Tooltip("Along the ground, leaving height out")]
    ///     public bool Flat = true;
    ///
    ///     protected override float Evaluate(in NumberContext context)
    ///     {
    ///         var from = context.Caster.GetTargetingPacket().position;
    ///         var to = context.Target.GetTargetingPacket().position;
    ///         if (Flat) from.y = to.y = 0f;
    ///         return Vector3.Distance(from, to);
    ///     }
    ///
    ///     public override string Describe() => "the distance to the target";
    /// }
    /// </code>
    /// <para>
    /// A kind is a scaler, so it also works wherever a scaler does: inside a Group, a Conditional or a Clamped, and in an
    /// effect's magnitude. The base answers the scaler's carrier methods by building a <see cref="NumberContext"/> and
    /// calling yours. It reads no values per level. Its fields draw in the inspector with no editor code, as a scaler's do,
    /// with <c>[Tooltip]</c> and <c>[ForgeShowWhen]</c>.
    /// </para>
    /// <para>
    /// A kind is saved by its class's name, so renaming the class needs Unity's <c>[MovedFrom]</c>, as any
    /// <c>[SerializeReference]</c> value does. Test one without an effect through <see cref="NumberContext.For(ISource, ITarget, int, int)"/>.
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class NumberKind : AbstractScaler
    {
        // What it reads, from its [NumberKind], looked up once
        [NonSerialized] private bool readsKnown;
        [NonSerialized] private NumberReads reads;

        /// <summary>
        /// Works out the kind's value. The context holds what the field's site has: the caster, the target, the level, the
        /// carrier, the caller's magnitudes, Local Data, the activation's data and the field's owner; what it reads, as
        /// <see cref="NumberKindAttribute.Reads"/> declares, is there.
        /// </summary>
        protected virtual float Evaluate(in NumberContext context) => 0f;

        /// <summary>
        /// The kind's part of its field's one-line summary, as "the distance to the target" in "0.5 × the distance to the
        /// target". With none, the field shows the kind's name.
        /// </summary>
        public virtual string Describe() => null;

        /// <summary>
        /// What the kind needs of its context to be worked out: by default, what its <see cref="NumberKindAttribute"/>
        /// says. Where the context lacks it, a field reads its number and the kind logs once. A kind whose needs follow its
        /// settings overrides this.
        /// </summary>
        public virtual NumberReads Reads
        {
            get
            {
                if (!readsKnown)
                {
                    reads = NumberKindAttribute.Of(GetType())?.Reads ?? NumberReads.None;
                    readsKnown = true;
                }
                return reads;
            }
        }

        /// <summary>What a kind reads where its context lacks what it needs, inside another scaler; 0 by default.</summary>
        protected virtual float Fallback => 0f;

        /// <summary>
        /// Whether the kind can be worked out in this context: by default, whether the context provides what it reads
        /// (<see cref="Reads"/>). A kind that needs more overrides it, as one that reads another field of its object needs
        /// that field to be there. Where it can't, its field reads its own number, and the kind logs once.
        /// </summary>
        public virtual bool CanEvaluate(in NumberContext context) => context.Has(Reads);

        /// <summary>Why the kind can't be worked out in this context, for the log: what it reads that isn't there.</summary>
        protected internal virtual string WhyNot(in NumberContext context) =>
            $"it reads {Reads & ~context.Provides}, which isn't there";

        // A kind reads none of every scaler's parts unless it asks to (ED-51)
        public override EScalerParts Parts => EScalerParts.None;
        public override bool UseScalingOptions() => false;

        public override void Initialize(IAttributeImpactCarrier deriv) { }

        /// <summary>A kind evaluated as a scaler, inside an effect or another scaler: its context is the carrier's.</summary>
        public sealed override float Evaluate(IAttributeImpactCarrier deriv) => Read(NumberContext.Of(deriv));

        /// <summary>A kind evaluated against an effect's container, as a stacking effect's are.</summary>
        public sealed override float Evaluate(AbstractStackingEffectContainer container) => Read(NumberContext.Of(container));

        /// <summary>Reads the kind in a context, or its fallback where it can't be worked out there.</summary>
        internal float Read(in NumberContext context)
        {
            if (!CanEvaluate(in context))
            {
                NumberContext.LogUnresolved(this, in context);
                return Fallback;
            }
            return Evaluate(in context);
        }
    }

    /// <summary>
    /// Names a kind of number in the kind menu, files it under a category, and says what it reads of a field's context
    /// (FEAT-29). The inspector offers the kind only in fields whose context provides what it reads, and the Problems
    /// window flags it where it's found in one that doesn't. Without it, a kind takes its class's name, as a scaler does, and
    /// is offered everywhere.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NumberKindAttribute : System.Attribute
    {
        /// <summary>The kind's name in the kind menu, as "Distance to Target".</summary>
        public string Name { get; }

        /// <summary>The kind menu's group the kind sits in, as "Space"; with none, the menu's top level.</summary>
        public string Category { get; set; }

        /// <summary>What the kind needs of a field's context, as <c>NumberReads.Caster | NumberReads.Target</c>.</summary>
        public NumberReads Reads { get; set; }

        public NumberKindAttribute(string name)
        {
            Name = name;
        }

        private static readonly Dictionary<Type, NumberKindAttribute> Known = new();

        /// <summary>A kind's attribute, or null where it has none. Looked up once a type.</summary>
        public static NumberKindAttribute Of(Type type)
        {
            if (type == null) return null;
            if (Known.TryGetValue(type, out var found)) return found;
            found = (NumberKindAttribute)GetCustomAttribute(type, typeof(NumberKindAttribute), false);
            Known[type] = found;
            return found;
        }
    }

    /// <summary>
    /// Says what a number field's context provides, which kinds its kind menu then offers, and which the Problems window
    /// flags (FEAT-29). A targeting task's range has no target yet, as the target is what it looks for:
    /// <c>[NumberContext(NumberReads.Level | NumberReads.Caster | NumberReads.Caller | NumberReads.LocalData)]</c>. A field
    /// without it offers every kind but one that reads its owner, as an effect's numbers have everything else. An owner
    /// is given only by the code that reads the field, <c>Max.Evaluate(context, this)</c>, so a field whose code gives
    /// one says so, <c>[NumberContext(NumberReads.All)]</c>, and Relative to a Field joins its menu (NUM-9). It also
    /// takes the field's unit: a field whose unit is "%" stores 0.3 and shows and takes it as 30%.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NumberContextAttribute : System.Attribute
    {
        /// <summary>What the field's context provides.</summary>
        public NumberReads Provides { get; }

        /// <summary>The field's unit, as "s" or "%"; none when it has none.</summary>
        public string Unit { get; set; }

        public NumberContextAttribute(NumberReads provides)
        {
            Provides = provides;
        }
    }

    /// <summary>A number a function works out, which <see cref="ScalableFloat.From"/> makes for an asset built in code.</summary>
    [Serializable]
    internal sealed class FunctionNumberKind : NumberKind
    {
        // A function can't be saved: a copy loaded from a save has none, and reads 0
        [NonSerialized] private Func<NumberContext, float> function;

        private FunctionNumberKind() { }

        public FunctionNumberKind(Func<NumberContext, float> function)
        {
            this.function = function;
        }

        protected override float Evaluate(in NumberContext context) => function?.Invoke(context) ?? 0f;

        public override string Describe() => "a value worked out in code";
    }
}
