using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A value the caller passes in under a tag, as Unreal's SetByCaller is (FEAT-2): code that knows the amount as it
    /// applies an effect or activates an ability sets it, and the number reads it.
    /// <para>
    /// What it checks: whether the context it's read in holds a value under exactly <see cref="Key"/>. A context holds
    /// the values set on what it was built from: an effect's spec (<c>spec.SetByCaller(tag, value)</c>, before it's
    /// applied), an ability activation's data (<c>data.SetByCaller(tag, value)</c>, which each effect the ability makes
    /// takes as it's made, and which the ability's tasks read), or the data that spawned a process. Only the key itself
    /// matches: a value set under a tag above or below it in the tag tree doesn't. A value set to 0 is a value, and is
    /// read. Where the context holds no value under the key, or holds no values at all, as an entity on its own, it reads
    /// <see cref="Default"/>.
    /// </para>
    /// <para>
    /// It is a number kind (FEAT-29), so it reads from any context that holds values: it read only an effect spec's, so a
    /// task's or a process's number read its default. Its default is a number of any kind, read with the scaler as its
    /// owner, so where nothing is set it can read a value by level.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Set By Caller")]
    public class SetByCallerScaler : NumberKind, ISerializationCallbackReceiver
    {
        [Tooltip("The tag the caller sets the value under, on the effect's spec or the activation's data. Only this tag " +
                 "matches, not one above or below it")]
        public Tag Key;

        [Tooltip("What it reads where the caller set no value under the key")]
        public ScalableFloat Default = 0f;

        protected override float Evaluate(in NumberContext context) =>
            context.TryGetSetByCaller(Key, out float magnitude) ? magnitude : Default?.Evaluate(in context, this) ?? 0f;

        public override void Initialize(IAttributeImpactCarrier deriv) => Default?.Initialize(deriv);

        // "the caller's Damage, else 0": the key by its display name, and what it reads where nothing is set
        public override string Describe() =>
            $"the caller's {(Key.IsValid ? Key.ToString() : "value")}, else {(Default != null ? NumberText.Of(Default) : NumberText.Number(0f))}";

        // Its default was a float (FEAT-29)
        [FormerlySerializedAs("Default"), SerializeField, HideInInspector] private float legacyDefault = LegacyNumber.None;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => LegacyNumber.Move(ref legacyDefault, ref Default);
    }
}
