using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for tag-based workers that activate/deactivate based on tag state.
    /// Tag workers run as long as their tag requirements are met.
    /// 
    /// Lifecycle:
    /// - Activate() called once when requirements become met
    /// - Tick() called periodically while active (based on TickPause)
    /// - Resolve() called once when requirements are no longer met
    /// 
    /// Common use cases:
    /// - Show fire particles when Fire tag weight >= 3
    /// - Play warning sound when LowHealth tag is present
    /// - Grant temporary ability while Empowered tag is active
    /// </summary>
    [Serializable]
    public abstract class AbstractTagWorker
    {
        [Header("Tag Worker")]
        [Tooltip("Tag requirements for this worker to be active")]
        public TagWorkerRequirements Requirements;
        
        [Space(5)]
        [Tooltip("Frames between Tick calls. 0 = every frame, -1 = no ticking")]
        public int TickPause = -1;
        
        /// <summary>
        /// Validate if this worker should be active based on current tag state.
        /// </summary>
        public virtual bool ValidateWorkFor(ITagHandler handler)
        {
            if (Requirements?.TagPackets == null || Requirements.TagPackets.Count == 0) 
                return true;
            
            foreach (var packet in Requirements.TagPackets)
            {
                int weight = handler.GetWeight(packet.Tag);
                
                switch (packet.Policy)
                {
                    case ERequireAvoidPolicy.Require:
                        if (weight < packet.RequiredWeight) return false;
                        break;
                    case ERequireAvoidPolicy.Avoid:
                        if (weight >= packet.RequiredWeight) return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return true;
        }
        
        /// <summary>
        /// Called once when tag requirements become met.
        /// Override to spawn particles, apply buffs, grant abilities, etc.
        /// </summary>
        public virtual void Activate(IGameplayAbilitySystem system) { }
        
        /// <summary>
        /// Called periodically while active (if TickPause >= 0).
        /// Override for ongoing effects that need periodic updates.
        /// </summary>
        public virtual void Tick(IGameplayAbilitySystem system) { }
        
        /// <summary>
        /// Called once when tag requirements are no longer met.
        /// Override to clean up particles, remove buffs, revoke abilities, etc.
        /// </summary>
        public virtual void Resolve(IGameplayAbilitySystem system) { }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // SUPPORTING TYPES
    // ═══════════════════════════════════════════════════════════════════════════
    
    [Serializable]
    public class TagWorkerRequirements
    {
        [Header("Requirements")]
        public List<TagWorkerRequirementPacket> TagPackets = new();
    }
    
    [Serializable]
    public struct TagWorkerRequirementPacket
    {
        public Tag Tag;
        public ERequireAvoidPolicy Policy;
        public int RequiredWeight;

        public TagWorkerRequirementPacket(Tag tag, ERequireAvoidPolicy policy, int requiredWeight = 1)
        {
            Tag = tag;
            Policy = policy;
            RequiredWeight = requiredWeight;
        }
    }

    public enum ERequireAvoidPolicy
    {
        /// <summary>Tag weight must be >= RequiredWeight for worker to activate</summary>
        Require,
        /// <summary>Tag weight must be < RequiredWeight for worker to activate</summary>
        Avoid
    }
}