using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
        /// Attach update calls to ASC callbacks as needed to appropriately evaluate and maintain the cached attribute
        /// </summary>
        /// <param name="deriv"></param>
        public override void Initialize(IAttributeImpactDerivation deriv)
        {
            // By default, cached attribute scalers do not initialize anything
        }

        /// <summary>
        /// Cached scalers do not care about the default scaler Evaluate(...). Use EvaluateActiveValue(...) instead.
        /// </summary>
        /// <param name="deriv"></param>
        /// <returns></returns>
        public override float Evaluate(IAttributeImpactDerivation deriv)
        {
            return 0f;
        }

        /// <summary>
        /// Registers attribute relationships for cache invalidation.
        /// When the contact attribute (to set) changes, related attributes (the locally associated attribute) need recalculation.
        /// </summary>
        public virtual void RegulateContactWith(IAttribute related, AttributeRegulationCache rules)
        {
        }

        public abstract AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache);
        
        public virtual AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return blueprint.RootValue;
        }
    }

    /// <summary>
    /// Tracks attribute dependencies for cache invalidation.
    /// When an attribute changes, this tells us which other attributes need recalculation.
    /// </summary>
    public class AttributeRegulationCache
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

        public IReadOnlyDictionary<IAttribute, List<IAttribute>> GetCache() => matrix;
    }
}