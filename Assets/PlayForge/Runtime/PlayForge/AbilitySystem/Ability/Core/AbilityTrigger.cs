using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What sets an ability off on its own (FEAT-4).</summary>
    public enum EAbilityTriggerSource
    {
        /// <summary>A gameplay event sent to the entity under the trigger's tag, or a tag under it.</summary>
        GameplayEvent,

        /// <summary>The entity gaining the trigger's tag, or a tag under it, which it didn't have.</summary>
        TagAdded,
    }

    /// <summary>Which way an impact goes, for a trigger that takes impact events (TW-5).</summary>
    public enum ETriggerImpactSign
    {
        /// <summary>Either way.</summary>
        Any,

        /// <summary>One that lowers the attribute: a blow.</summary>
        Loss,

        /// <summary>One that raises it: a heal.</summary>
        Gain,
    }

    /// <summary>What an ability a trigger sets off targets (TW-5).</summary>
    public enum ETriggerTarget
    {
        /// <summary>What its own targeting finds, as an area spin finds the enemies around it.</summary>
        Own,

        /// <summary>The event's instigator, its Source: the one who dealt the blow, as a counterattack targets.</summary>
        Instigator,

        /// <summary>The event's subject: the one hit, as an on-hit ability targets; the one killed; the entity itself.</summary>
        Subject,
    }

    /// <summary>
    /// What activates an ability on its own: a gameplay event sent to its entity, or a tag its entity gains. An ability
    /// activated only when code asked for it (FEAT-4).
    /// <para>
    /// The core sends events of its own to an entity where an ability listens for them (TW-5): <see cref="Tags.IMPACT_DEALT"/>
    /// and <see cref="Tags.IMPACT_TAKEN"/> as an impact lands between two entities, <see cref="Tags.KILL"/> to the killer and
    /// <see cref="Tags.DIED"/> to the one who died, and <see cref="Tags.REVIVED"/>. Their data hold the instigator as
    /// <see cref="Tags.SOURCE"/>, the entity it was done to as <see cref="Tags.SUBJECT"/>, and an impact's
    /// <see cref="ImpactData"/> as <see cref="Tags.IMPACT"/>. A trigger can take only some impacts, roll a chance, and target
    /// the instigator or the subject.
    /// </para>
    /// </summary>
    [Serializable]
    public class AbilityTrigger
    {
        [Tooltip("What sets the ability off: a gameplay event sent to the entity, or the entity gaining a tag")]
        public EAbilityTriggerSource Source;

        [Tooltip("The event's tag, or the tag gained. One under it sets the ability off too. The core's own: Impact Dealt, " +
                 "Impact Taken, Kill, Died, Revived")]
        public Tag Tag;

        [Tooltip("For an impact event, only impacts on this attribute. None, any (TW-5)")]
        public Attribute Attribute;

        [Tooltip("For an impact event, only impacts that lower the attribute, that raise it, or either (TW-5)")]
        public ETriggerImpactSign Sign = ETriggerImpactSign.Any;

        [Tooltip("For an impact event, only impacts of one of these types. None, any (TW-5)")]
        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes = new();

        [Tooltip("The chance the event sets the ability off, from 0 to 1, rolled once for each event (TW-5)")]
        public ScalableFloat Chance = 1f;

        [Tooltip("What the ability targets: what its own targeting finds; the event's instigator, as a counterattack; or " +
                 "its subject, as an on-hit (TW-5)")]
        public ETriggerTarget Target = ETriggerTarget.Own;

        /// <summary>
        /// Whether an event's <paramref name="data"/> passes the trigger's impact filters, and its chance, rolled now, for
        /// <paramref name="receiver"/>. A tag gained, or an event with no impact, passes the filters.
        /// </summary>
        public bool Accepts(ProcessDataPacket data, IGameplayAbilitySystem receiver)
        {
            if (Source == EAbilityTriggerSource.GameplayEvent && data != null && data.TryGetFirst<ImpactData>(Tags.IMPACT, out var impact))
            {
                if (Attribute != null && !Attribute.Equals(impact.Attribute)) return false;
                float change = impact.RealImpact.CurrentValue != 0f ? impact.RealImpact.CurrentValue : impact.RealImpact.BaseValue;
                if (Sign == ETriggerImpactSign.Loss && change >= 0f) return false;
                if (Sign == ETriggerImpactSign.Gain && change <= 0f) return false;
                if (ImpactTypes is { Count: > 0 } && !ForgeHelper.ValidateImpactTypes(false, impact.SourcedModifier.Carrier?.GetImpactTypes(), ImpactTypes))
                    return false;
            }

            float chance = Chance?.Evaluate(NumberContext.Of(receiver)) ?? 1f;
            if (chance >= 1f) return true;
            return chance > 0f && ForgeRandom.Value < chance;
        }

        /// <summary>What the ability targets in <paramref name="data"/>, as the trigger says; null for its own targeting.</summary>
        public ITarget TargetIn(ProcessDataPacket data)
        {
            if (data == null || Target == ETriggerTarget.Own) return null;
            var key = Target == ETriggerTarget.Instigator ? Tags.SOURCE : Tags.SUBJECT;
            return data.TryGetFirst<ITarget>(key, out var target) ? target : null;
        }
    }
}
