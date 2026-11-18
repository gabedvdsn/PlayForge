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
        [Tooltip("Tag requirements for this worker to be active")]
        public TagWorkerRequirements Requirements;
        
        [Space(5)]
        [Tooltip("Frames between Tick calls, read at its entity's level. 0 = every frame, -1 = no ticking")]
        public ScalableInt TickPause = -1;
        
        /// <summary>
        /// Whether the worker is evaluated at the end of every frame, as every worker was. Off, as it is by default, it's
        /// evaluated at the end of a frame in which the entity's tags changed, since <see cref="ValidateWorkFor"/> reads
        /// them. A worker whose validity reads more than the tags turns it on. Read as the worker is provided.
        /// </summary>
        public virtual bool EvaluateEveryFrame => false;

        /// <summary>
        /// Validate if this worker should be active based on current tag state.
        /// </summary>
        public virtual bool ValidateWorkFor(ITagHandler handler)
        {
            if (Requirements?.TagPackets == null || Requirements.TagPackets.Count == 0) 
                return true;
            
            foreach (var packet in Requirements.TagPackets)
            {
                int weight = handler.GetTagWeight(packet.Tag);
                int required = packet.RequiredWeight?.Evaluate(NumberContext.Of(handler as ISource)) ?? 0;
                
                switch (packet.Policy)
                {
                    case ERequireAvoidPolicy.Require:
                        if (weight < required) return false;
                        break;
                    case ERequireAvoidPolicy.Avoid:
                        if (weight >= required) return false;
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
    
    /// <summary>
    /// A tag worker's requirements: its packets, each a tag and a weight the entity's must reach or stay under. The
    /// worker is active while every packet holds, and always where it has none.
    /// </summary>
    [Serializable]
    public class TagWorkerRequirements
    {
        public List<TagWorkerRequirementPacket> TagPackets = new();
    }
    
    /// <summary>
    /// One of a tag worker's requirements: a tag, whether the entity must carry it, Require, or must not, Avoid, and
    /// the weight that decides, read at the entity's level. Require holds while the tag's weight is at least
    /// <c>RequiredWeight</c>; Avoid while it's below.
    /// </summary>
    [Serializable]
    public struct TagWorkerRequirementPacket
    {
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;
        public ERequireAvoidPolicy Policy;
        public ScalableInt RequiredWeight;

        public TagWorkerRequirementPacket(Tag tag, ERequireAvoidPolicy policy, int requiredWeight = 1)
        {
            Tag = tag;
            Policy = policy;
            RequiredWeight = requiredWeight;
        }
    }

    /// <summary>
    /// Which way a tag worker's requirement holds: Require, while the tag's weight on the entity is at least the
    /// required weight; Avoid, while it's below it.
    /// </summary>
    public enum ERequireAvoidPolicy
    {
        /// <summary>Tag weight must be >= RequiredWeight for worker to activate</summary>
        Require,
        /// <summary>Tag weight must be &lt; RequiredWeight for worker to activate</summary>
        Avoid
    }
}