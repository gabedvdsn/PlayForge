using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A magnitude the caller sets under a tag as it applies the effect, as Unreal's SetByCaller is: on the spec, or on an
    /// ability activation's data, which each effect the ability applies takes as it's made. Where none is set, it reads its
    /// Default. An effect's amount came only from its asset and its scalers, so code that knew the amount as it applied
    /// the effect couldn't pass it in (FEAT-2, PF-C01).
    /// <para>
    /// It reads from any context that holds magnitudes, as a number kind (FEAT-29): an effect's spec, an activation's data
    /// as a task reads it, the data that spawned a process, and an effect's cue or worker. It read only an effect spec's,
    /// so a task's or a process's number read its default.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Set By Caller")]
    public class SetByCallerScaler : NumberKind
    {
        [Tooltip("The tag the caller sets the magnitude under")]
        public Tag Key;

        [Tooltip("What the scaler reads where the caller set nothing under the tag")]
        public float Default;

        protected override float Evaluate(in NumberContext context) =>
            context.TryGetSetByCaller(Key, out float magnitude) ? magnitude : Default;

        public override string Describe() => Key.IsValid ? $"the caller's {Key.GetName()}" : "the caller's value";
    }
}
