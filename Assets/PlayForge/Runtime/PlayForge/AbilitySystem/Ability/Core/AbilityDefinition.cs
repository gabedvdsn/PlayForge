using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AbilityDefinition
    {
        public string Name;
        public string Description;
        
        public EAbilityActivationPolicyExtended ActivationPolicy = EAbilityActivationPolicyExtended.UseLocalPolicy; 

        /// <summary>
        /// Under the Queue policy, how long a request that can't start yet waits in the input buffer, in seconds of the
        /// caster's time: a press while another ability holds the claim, or while a running stage blocks this one,
        /// fires once it can, if that comes in time. A request can name a window of its own. The Queue policy waited for
        /// good, behind every earlier press (FEAT-6).
        /// </summary>
        [Min(0f)] public float BufferWindow = 0.3f;

        /// <summary>
        /// Which buffered request fires first: the highest, then the newest of equals. Each ability holds one slot, which
        /// a new press refreshes (FEAT-6).
        /// </summary>
        public int BufferPriority;

        public bool ActivateImmediately;

        /// <summary>
        /// What activates the ability on its own: a gameplay event sent to its entity, or a tag its entity gains, under the
        /// trigger's tag or one under it. Each tries the ability as a request would, so its checks and its When Busy still
        /// hold. An ability activated only when code asked for it (FEAT-4).
        /// </summary>
        public List<AbilityTrigger> Triggers = new();

        [Tooltip("The chance it activates, from 0 to 1, rolled once for each activation, a player's or a trigger's (TW-8)")]
        public ScalableFloat ChanceToActivate = 1f;

        [Tooltip("How its chance to activate rolls: pseudo-random, and its luck")]
        public ChanceRoll ActivationRoll = new();

        [Tooltip("What its effects' hits carry for their on-hit effects, where an effect sets none of its own (TW-8)")]
        [Min(0f)] public float ProcCoefficient = 1f;
        
        public List<TextureItem> Textures;
    }
}
