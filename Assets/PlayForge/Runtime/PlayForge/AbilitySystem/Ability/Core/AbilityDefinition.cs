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
        
        public List<TextureItem> Textures;
    }
}
