using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A mono process that takes part in gameplay, as a projectile an ability spawns. It knows its origin, the ability
    /// or entity that spawned it, and it carries attributes of its own (FEAT-24): a projectile's speed is the
    /// projectile's, set on its prefab, by the level of what spawned it, or from the caster, and its code can change it
    /// as it flies. A role, as ProjectileSpeed, names the attribute a process reads for a purpose.
    /// </summary>
    public abstract class AbstractGameplayMonoProcess : LazyMonoProcess
    {
        [Tooltip("The attributes the process carries of its own, as a projectile's speed. Each starts as its own value, read at the level of what spawned the process, or as its origin's attribute, and the process's code can change it as it runs")]
        public List<ProcessAttribute> Attributes = new();

        [Tooltip("Roles read from the origin's attributes, as ProjectileSpeed from the caster's Projectile Speed. A role among the process's own attributes comes first")]
        public List<TagAttributePair> AttributePairs = new();

        protected IEffectOrigin Origin;
        protected SystemComponentData Source;

        // Each own attribute's value now, by its place in Attributes
        private float[] values = Array.Empty<float>();

        /// <summary>Raised as one of the process's own attributes changes, with the attribute and its new value.</summary>
        public event Action<ProcessAttribute, float> AttributeChanged;

        /// <summary>The data that spawned the process, which its numbers read (FEAT-29).</summary>
        internal ProcessDataPacket SpawnData => regData;

        /// <summary>The ability or entity that spawned the process, whose caster and level its numbers read (FEAT-29).</summary>
        internal IEffectOrigin SpawnOrigin => Origin;

        public override void WhenInitialize()
        {
            base.WhenInitialize();

            if (!regData.TryGet(Tags.DERIVATION, EDataTarget.Primary, out Origin))
            {
                Origin = GameRoot.Instance;
            }

            Source = Origin.GetSource().ToGASComponentData();
            StartAttributes();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Its own attributes (FEAT-24)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The value of one of the process's own attributes, by the attribute it is.</summary>
        public bool TryGetAttribute(IAttribute attribute, out float value)
        {
            int at = IndexOf(attribute);
            value = at >= 0 ? values[at] : 0f;
            return at >= 0;
        }

        /// <summary>
        /// The value a role reads: the process's own attribute in that role, or else the origin's attribute that
        /// <see cref="AttributePairs"/> names for it.
        /// </summary>
        public bool TryGetRole(Tag role, out float value)
        {
            int at = IndexOfRole(role);
            if (at >= 0)
            {
                value = values[at];
                return true;
            }

            if (AttributePairs != null)
            {
                foreach (var pair in AttributePairs)
                {
                    if (pair.Key == role && TryGetOrigins(pair.Attribute, out value)) return true;
                }
            }

            value = 0f;
            return false;
        }

        /// <summary>
        /// Gives an effect spec the process applies the set-by-caller magnitudes it should read: those of the data that
        /// spawned it, then each of its own attributes under its role, so an effect scales by what the process carries,
        /// as a fire's burn by its heat. Its own win, as the nearer. Only its caller's reached its effects, so what it
        /// carried stopped at the process (FEAT-28).
        /// </summary>
        public void GiveMagnitudes(GameplayEffectSpec spec)
        {
            if (spec == null) return;
            if (regData != null) spec.TakeSetByCaller(regData.SetByCallerMagnitudes);
            if (Attributes == null || values == null) return;
            for (int i = 0; i < Attributes.Count && i < values.Length; i++)
                if (Attributes[i] != null && Attributes[i].Role.IsValid) spec.SetByCaller(Attributes[i].Role, values[i]);
        }

        /// <summary>Sets one of the process's own attributes, by the attribute it is. False where it carries none.</summary>
        public bool SetAttribute(IAttribute attribute, float value) => Set(IndexOf(attribute), value);

        /// <summary>Sets the process's own attribute in a role. False where it carries none in that role.</summary>
        public bool SetRole(Tag role, float value) => Set(IndexOfRole(role), value);

        // Each own attribute's value as the process starts
        private void StartAttributes()
        {
            int count = Attributes?.Count ?? 0;
            values = new float[count];
            for (int i = 0; i < count; i++) values[i] = StartingValue(Attributes[i]);
        }

        // What spawned it can set it under its role, as an ability's data does; else its origin's attribute, or its own
        // value at the level of what spawned it
        private float StartingValue(ProcessAttribute attribute)
        {
            if (attribute == null) return 0f;
            if (attribute.Role.IsValid && regData != null && regData.TryGet(attribute.Role, EDataTarget.Primary, out float set)) return set;

            if (attribute.StartsFrom != EProcessAttributeStart.Own && TryGetOrigins(attribute.Attribute, out var origins)) return origins;
            if (attribute.StartsFrom == EProcessAttributeStart.Origin)
            {
                ForgeLog.Warning($"{name}: its origin has no {attribute.Attribute?.GetName() ?? "attribute"} to start {RoleName(attribute)} as, so it starts at 0.", this);
                return 0f;
            }

            if (attribute.Value == null) return 0f;
            // Read in the process's context: its origin's caster and level, and the magnitudes its data carries, so a Set By
            // Caller reads what its caller set. The leveler carrier it was read through allocated each time, and read only
            // the default (FEAT-29)
            return attribute.Value.Evaluate(NumberContext.Of(this));
        }

        private bool TryGetOrigins(IAttribute attribute, out float value)
        {
            value = 0f;
            if (attribute == null || Source.AttributeSystem == null) return false;
            if (!Source.AttributeSystem.TryGetCachedValue(attribute, out CachedAttributeValue cached) || cached.RetainedGroups is null) return false;
            value = cached.ActiveValue.CurrentValue;
            return true;
        }

        private bool Set(int at, float value)
        {
            if (at < 0) return false;
            bool changed = !Mathf.Approximately(values[at], value);
            values[at] = value;
            if (changed) AttributeChanged?.Invoke(Attributes[at], value);
            return true;
        }

        private int IndexOf(IAttribute attribute)
        {
            if (attribute == null || Attributes == null) return -1;
            for (int i = 0; i < Attributes.Count && i < values.Length; i++)
                if (Attributes[i]?.Attribute != null && Attributes[i].Attribute.Equals(attribute)) return i;
            return -1;
        }

        private int IndexOfRole(Tag role)
        {
            if (!role.IsValid || Attributes == null) return -1;
            for (int i = 0; i < Attributes.Count && i < values.Length; i++)
                if (Attributes[i] != null && Attributes[i].Role == role) return i;
            return -1;
        }

        private static string RoleName(ProcessAttribute attribute) => attribute.Role.IsValid ? attribute.Role.ToString() : "it";

        // ═══════════════════════════════════════════════════════════════════════════
        // The origin's attributes, by role
        // ═══════════════════════════════════════════════════════════════════════════

        protected CachedAttributeValue GetAttributeValue(Tag key)
        {
            if (Source.AttributeSystem == null || AttributePairs == null) return CachedAttributeValue.GenerateNull();
            foreach (var pair in AttributePairs)
            {
                if (pair.Key == key && Source.AttributeSystem.TryGetCachedValue(
                        pair.Attribute,
                        out CachedAttributeValue value)
                   ) return value;
            }

            return CachedAttributeValue.GenerateNull();
        }

        public virtual void ReportStatus(Tag status)
        {
            // Named, so a process that fails says which, and why. Both messages were the origin alone
            if (status == Tags.FAILED_TO_INITIALIZE) throw new InvalidOperationException($"{GetType().Name} \"{name}\" failed to initialize (origin {Origin}).");
            if (status == Tags.FAILED_WHILE_ACTIVE) throw new InvalidOperationException($"{GetType().Name} \"{name}\" failed while active (origin {Origin}).");
        }
    }

    [Serializable]
    public struct TagAttributePair
    {
        public Tag Key;
        public Attribute Attribute;

        public TagAttributePair(Tag key, Attribute attribute)
        {
            Key = key;
            Attribute = attribute;
        }
    }

    /// <summary>
    /// An attribute a gameplay mono process carries of its own, as a projectile's speed (FEAT-24): the attribute it is,
    /// the role the process reads it by, and where its value starts. It changes as the process runs.
    /// </summary>
    [Serializable]
    public class ProcessAttribute
    {
        [Tooltip("The attribute it is, as Projectile Speed. Starting from the origin, it starts as the origin's value of this attribute")]
        public Attribute Attribute;

        [Tooltip("The role the process reads it by, as ProjectileSpeed for a projectile's speed. What spawns the process can set it under this tag in its data")]
        [ForgeTagContext(ForgeContext.Data)]
        public Tag Role;

        [Tooltip("Where its value starts: its own Value, the origin's attribute, or the origin's where it has one and its own where not")]
        public EProcessAttributeStart StartsFrom = EProcessAttributeStart.Own;

        [Tooltip("Its own value: a number, or a scaler read at the level of what spawned the process")]
        public ScalableFloat Value = new() { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude };
    }

    /// <summary>Where a process's own attribute starts (FEAT-24).</summary>
    public enum EProcessAttributeStart
    {
        [InspectorName("Its Own Value")] Own,
        [InspectorName("The Origin's")] Origin,
        [InspectorName("The Origin's, Else Its Own")] OriginElseOwn,
    }
}
