using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whose workers a Worker number asks (FEAT-29).</summary>
    public enum EWorkerNumberFrom
    {
        [Tooltip("The workers of the effect whose number it is, asked about that application of the effect")]
        [InspectorName("This Effect")] ThisEffect,

        [Tooltip("The caster's workers: those its worker groups give it, and those code gives it")]
        Caster,

        [Tooltip("The target's workers: those its worker groups give it, and those code gives it")]
        Target,
    }

    /// <summary>
    /// A value a worker answers under a key (FEAT-29, NUM-12): the number asks, and a worker that implements
    /// <see cref="INumberWorker"/> answers from what it knows, as how much health its effect's target has lost since the
    /// effect began. So work a number can't do alone goes to a worker, and the number reads the result: a curse whose
    /// every fourth tick deals a share of the damage its target took since it landed is one effect, with one worker.
    /// <para>
    /// It asks the workers of the effect whose number it is, about that application of the effect, or the caster's or
    /// the target's own workers. The first worker that answers the key gives the value. Where none answers, as where
    /// the number isn't an effect's or the entity has no such worker, it reads its <see cref="Default"/>.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Worker")]
    public class WorkerNumberKind : NumberKind
    {
        [Tooltip("The key it asks for. A worker answers the keys it knows; the first worker that answers this one gives the value")]
        [ForgeTagContext(ForgeContext.WorkerKey)]
        public Tag Key;

        [Tooltip("Whose workers it asks: those of the effect whose number it is, or the caster's or the target's own")]
        public EWorkerNumberFrom From = EWorkerNumberFrom.ThisEffect;

        [Tooltip("What it reads where no worker answers the key")]
        public float Default;

        /// <summary>What it needs: the carrier for its effect's workers, else the entity whose workers it asks.</summary>
        public override NumberReads Reads => From switch
        {
            EWorkerNumberFrom.Caster => NumberReads.Caster,
            EWorkerNumberFrom.Target => NumberReads.Target,
            _ => NumberReads.Carrier,
        };

        protected override float Fallback => Default;

        protected override float Evaluate(in NumberContext context)
        {
            bool answered = From == EWorkerNumberFrom.ThisEffect
                ? NumberWorkers.TryAsk(context.Carrier as GameplayEffectSpec, Key, out float value)
                : NumberWorkers.TryAsk(context.EntityOn(From == EWorkerNumberFrom.Caster ? NumberSide.Caster : NumberSide.Target), Key, in context, out value);
            return answered ? value : Default;
        }

        // "Data.Lost from this effect's workers, else 0"
        public override string Describe()
        {
            var whose = From switch
            {
                EWorkerNumberFrom.Caster => "the caster's",
                EWorkerNumberFrom.Target => "the target's",
                _ => "this effect's",
            };
            return $"{(Key.IsValid ? Key.ToString() : "a key")} from {whose} workers, else {NumberText.Number(Default)}";
        }
    }
}
