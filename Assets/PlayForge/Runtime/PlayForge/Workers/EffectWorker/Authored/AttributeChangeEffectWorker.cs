using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What an Attribute Change worker answers about its attribute (FEAT-29).</summary>
    public enum EAttributeChangeWhat
    {
        [Tooltip("How much lower the attribute is than as the effect began; 0 where it's higher")]
        Lost,

        [Tooltip("How much higher the attribute is than as the effect began; 0 where it's lower")]
        Gained,

        [Tooltip("The attribute now less the attribute as the effect began: negative where it fell")]
        Change,

        [Tooltip("The attribute as the effect began")]
        [InspectorName("At Start")] AtStart,
    }

    /// <summary>
    /// Answers how far an attribute has moved since its effect began (FEAT-29, NUM-12), for a number of the Worker kind
    /// to read: how much health the target has lost since the curse landed, from every source. It notes the attribute as
    /// each application of its effect begins, and answers under its <see cref="Key"/>: the amount lost, gained or
    /// changed, or the attribute as it was.
    /// <para>
    /// With <see cref="EveryTicks"/> above 0, it answers only on each Nth tick of its effect, and 0 on the others, so an
    /// impact that reads it lands every Nth tick: a curse dealing 5 a second, and every fourth second a quarter of what
    /// its target has lost since it began, is one periodic effect with a second impact of −0.25 × this worker's answer.
    /// Its tick is counted before the tick's impacts are worked out, so on its Nth tick the amount lost is the amount
    /// before that tick lands.
    /// </para>
    /// </summary>
    [Serializable]
    public class AttributeChangeEffectWorker : AbstractEffectWorker, INumberWorker
    {
        [Tooltip("The key it answers under: a number of the Worker kind with this key reads its answer")]
        [ForgeTagContext(ForgeContext.WorkerKey)]
        public Tag Key;

        [Tooltip("The attribute it watches")]
        public Attribute Attribute;

        [Tooltip("Whose attribute: the target's, the entity the effect is on, or the caster's")]
        public NumberSide Of = NumberSide.Target;

        [Tooltip("What it answers: how much the attribute has lost, gained or changed since the effect began, or the " +
                 "attribute as it began")]
        public EAttributeChangeWhat What = EAttributeChangeWhat.Lost;

        [Tooltip("The attribute's current value, or its base")]
        public EAttributeTargetBinary Part = EAttributeTargetBinary.Current;

        [Tooltip("0: it answers at every read. Above 0, it answers only on each Nth tick of its effect, and 0 on the " +
                 "others, so an impact that reads it lands every Nth tick")]
        [Min(0)]
        public int EveryTicks;

        /// <summary>What it keeps for one application of its effect.</summary>
        public class State
        {
            /// <summary>Whether the attribute was noted as the effect began.</summary>
            public bool Began;

            /// <summary>The attribute as the effect began.</summary>
            public float AtStart;

            /// <summary>How many times the effect has ticked.</summary>
            public int Ticks;

            /// <summary>Whether the last tick was an Nth one, where it answers only on those.</summary>
            public bool Due;
        }

        public override IEnumerable<IRootAction> OnEffectApplication(EffectWorkerContext ctx)
        {
            var state = ctx.GetState<State>(this);
            if (state != null && !state.Began)
            {
                state.AtStart = NumberContext.Of(in ctx).Attribute(Of, Attribute, Part);
                state.Began = true;
            }
            return base.OnEffectApplication(ctx);
        }

        public override IEnumerable<IRootAction> OnEffectTick(EffectWorkerContext ctx)
        {
            // A periodic effect's workers are called every frame, with the ticks that frame executes: most frames, none
            var state = ctx.ExecuteTicks > 0 ? ctx.GetState<State>(this) : null;
            if (state != null)
            {
                int before = state.Ticks;
                state.Ticks += ctx.ExecuteTicks;
                state.Due = EveryTicks > 0 && state.Ticks / EveryTicks > before / EveryTicks;
            }
            return base.OnEffectTick(ctx);
        }

        /// <summary>Its answer under its key, about the application of its effect the context carries.</summary>
        public bool TryGetNumber(Tag key, in NumberContext context, out float value)
        {
            value = 0f;
            if (!Key.IsValid || key != Key) return false;
            var state = (context.Carrier as GameplayEffectSpec)?.GetWorkerState<State>(this);
            if (state == null || !state.Began) return false;
            if (EveryTicks > 0 && !state.Due) return true;

            float now = context.Attribute(Of, Attribute, Part);
            value = What switch
            {
                EAttributeChangeWhat.Lost => Math.Max(0f, state.AtStart - now),
                EAttributeChangeWhat.Gained => Math.Max(0f, now - state.AtStart),
                EAttributeChangeWhat.Change => now - state.AtStart,
                _ => state.AtStart,
            };
            return true;
        }
    }
}
