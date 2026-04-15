using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for cached scalers that participate in attribute modification rules.
    /// Used for derived/computed attributes that depend on other attributes.
    /// </summary>
    public abstract class AbstractCachedScaler : AbstractScaler
    {
        /// <summary>
        /// Registers attribute relationships for cache invalidation.
        /// When the contact attribute changes, related attributes need recalculation.
        /// </summary>
        public abstract void Regulate(IAttribute attribute, AttributeModificationRule rules);
        public abstract float Evaluate(IGameplayAbilitySystem gas, AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache);
    }

    /// <summary>
    /// Tracks attribute dependencies for cache invalidation.
    /// When an attribute changes, this tells us which other attributes need recalculation.
    /// </summary>
    public class AttributeModificationRule
    {
        private readonly Dictionary<IAttribute, List<IAttribute>> matrix = new();

        /// <summary>
        /// Register that when 'contact' changes, 'related' needs recalculation.
        /// </summary>
        public void RegisterRelation(IAttribute contact, IAttribute related)
        {
            if (contact == null || related == null) return;
            matrix.SafeAdd(contact, related);
        }

        /// <summary>
        /// When 'attr' changes, get the list of attributes that need re-initialization.
        /// </summary>
        public bool TryGetRelatedAttributes(IAttribute attr, out List<IAttribute> related)
        {
            return matrix.TryGetValue(attr, out related);
        }
        
        /// <summary>
        /// Clear all registered relations.
        /// </summary>
        public void Clear()
        {
            matrix.Clear();
        }
        
        /// <summary>
        /// Get all attributes that have dependencies.
        /// </summary>
        public IEnumerable<IAttribute> GetAllContactAttributes()
        {
            return matrix.Keys;
        }
    }
}