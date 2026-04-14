using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interface for assets that can provide level information to linked effects.
    /// Implemented by Ability and EntityIdentity to support "Lock to Source" level mode.
    /// 
    /// When a GameplayEffect is linked to an ILevelProvider, the effect's modifiers
    /// can derive their MaxLevel from the provider, ensuring consistent scaling.
    /// 
    /// Common use cases:
    /// - Ability Cost effect linked to its parent Ability
    /// - Ability Cooldown effect linked to its parent Ability
    /// - Entity-specific effects linked to EntityIdentity
    /// </summary>
    public interface ILevelProvider
    {
        /// <summary>
        /// Gets the maximum level this provider supports.
        /// Used by linked effects for "Lock to Source" modifier scaling.
        /// </summary>
        int GetMaxLevel();
        
        /// <summary>
        /// Gets the starting/default level for this provider.
        /// </summary>
        int GetStartingLevel();
        
        /// <summary>
        /// Gets a display name for the provider (for editor display).
        /// </summary>
        string GetProviderName();
        
        /// <summary>
        /// Gets the asset tag identifying this provider.
        /// </summary>
        Tag GetProviderTag();
    }
    
    /// <summary>
    /// Defines how an effect is linked to its level provider.
    /// </summary>
    public enum EEffectLinkMode
    {
        /// <summary>
        /// Effect operates independently with its own level tracking.
        /// </summary>
        Standalone,
        
        /// <summary>
        /// Effect is linked to an Ability or Entity and derives level from it.
        /// </summary>
        LinkedToProvider
    }
    
    /// <summary>
    /// Attribute to mark fields that should use the LevelProviderDrawer.
    /// Apply to ScriptableObject fields that should only accept ILevelProvider types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class LinkedSourceAttribute : PropertyAttribute { }
}
