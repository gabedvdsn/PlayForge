using System;
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

    /// <summary>
    /// What activates an ability on its own: a gameplay event sent to its entity, or a tag its entity gains. An ability
    /// activated only when code asked for it (FEAT-4).
    /// </summary>
    [Serializable]
    public class AbilityTrigger
    {
        [Tooltip("What sets the ability off: a gameplay event sent to the entity, or the entity gaining a tag")]
        public EAbilityTriggerSource Source;

        [Tooltip("The event's tag, or the tag gained. One under it sets the ability off too")]
        public Tag Tag;
    }
}
