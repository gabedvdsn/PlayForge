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
        public abstract void Regulate(Attribute attribute, AttributeModificationRule rules);
        public abstract void Evaluate(CachedAttributeValue value);
    }

    /// <summary>
    /// Tracks attribute dependencies for cache invalidation.
    /// When an attribute changes, this tells us which other attributes need recalculation.
    /// </summary>
    public class AttributeModificationRule
    {
        private readonly Dictionary<Attribute, List<Attribute>> matrix = new();

        /// <summary>
        /// Register that when 'contact' changes, 'related' needs recalculation.
        /// </summary>
        public void RegisterRelation(Attribute contact, Attribute related)
        {
            if (contact == null || related == null) return;
            matrix.SafeAdd(contact, related);
        }

        /// <summary>
        /// When 'attr' changes, get the list of attributes that need re-initialization.
        /// </summary>
        public bool RelatedAttributes(Attribute attr, out List<Attribute> related)
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
        public IEnumerable<Attribute> GetAllContactAttributes()
        {
            return matrix.Keys;
        }
    }
}