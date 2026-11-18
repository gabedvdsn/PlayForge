using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An ability's Definition: its <c>Name</c>, <c>Description</c> and <c>Textures</c>, and how it activates: its When
    /// Busy (<c>ActivationPolicy</c>) and <c>WhileTargeting</c>; under Queue, its input buffer's <c>BufferWindow</c>
    /// and <c>BufferPriority</c>; Activate At Start (<c>ActivateImmediately</c>), for a Starting Ability; its
    /// <c>Triggers</c>, the gameplay events and gained tags that set it off on its own; its <c>ChanceToActivate</c> and
    /// how that rolls (<c>ActivationRoll</c>); the <c>ProcCoefficient</c> its effects' hits carry where an effect sets
    /// none of its own; and the crowd control it ignores (<c>IgnoresCrowdControl</c>).
    /// </summary>
    [Serializable]
    public class AbilityDefinition
    {
        public string Name;
        public string Description;
        
        public EAbilityActivationPolicyExtended ActivationPolicy = EAbilityActivationPolicyExtended.UseLocalPolicy;

        /// <summary>
        /// While it chooses its target, as a click-targeting task waits for the player, whether another of its entity's
        /// abilities activating cancels it: its entity's rule, or its own. A player who starts one ability's targeting and
        /// presses another's key means the other.
        /// </summary>
        [Tooltip("While it chooses its target, whether another of its entity's abilities activating cancels it, nothing of it paid: its entity's rule, or its own")]
        public EWhileTargeting WhileTargeting = EWhileTargeting.EntitysRule;

        /// <summary>
        /// Under the Queue policy, how long a request that can't start yet waits in the input buffer, in seconds of the
        /// caster's time: a press while another ability holds the claim, or while a running stage blocks this one,
        /// fires once it can, if that comes in time. A request can name a window of its own. The Queue policy waited for
        /// good, behind every earlier press. Read at the ability's level.
        /// </summary>
        [NumberMin(0f)] public ScalableFloat BufferWindow = 0.3f;

        /// <summary>
        /// Which buffered request fires first: the highest, then the newest of equals. Each ability holds one slot, which
        /// a new press refreshes.
        /// </summary>
        [Tooltip("Which buffered request fires first: the highest, then the newest of equals. Each ability holds one slot, which a new press refreshes")]
        public ScalableInt BufferPriority = 0;

        public bool ActivateImmediately;

        /// <summary>
        /// What activates the ability on its own: a gameplay event sent to its entity, or a tag its entity gains, under the
        /// trigger's tag or one under it. Each tries the ability as a request would, so its checks and its When Busy still
        /// hold. An ability activated only when code asked for it.
        /// </summary>
        public List<AbilityTrigger> Triggers = new();

        [Tooltip("The chance it activates, from 0 to 1, rolled once for each activation, a player's or a trigger's")]
        [NumberRange(0f, 1f)] 
        public ScalableFloat ChanceToActivate = 1f;

        [Tooltip("How its chance to activate rolls: pseudo-random, and its luck")]
        public ChanceRoll ActivationRoll = new();

        [Tooltip("What its effects' hits carry for their on-hit effects, where an effect sets none of its own")]
        [NumberMin(0f)] 
        public ScalableFloat ProcCoefficient = 1f;

        [Tooltip("The crowd control it ignores, by its tags, as Silenced: the other rules still stop it, and still interrupt " +
                 "it as it runs")]
        [ForgeTagContext(ForgeContext.Granted)]
        public List<Tag> IgnoresCrowdControl = new();
        
        public List<TextureItem> Textures;
    }
}
