using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Context passed to workers during validation and execution.
    /// Provides access to the system, attribute cache, and current change value.
    /// </summary>
    public readonly struct WorkerContext
    {
        /// <summary>
        /// The gameplay ability system being modified.
        /// </summary>
        public readonly IGameplayAbilitySystem System;
        
        /// <summary>
        /// Read-only access to the attribute cache.
        /// For inline workers that need to modify cache, cast back to Dictionary.
        /// </summary>
        public readonly IReadOnlyDictionary<Attribute, CachedAttributeValue> AttributeCache;
        
        /// <summary>
        /// The change value being processed.
        /// Inline workers can modify this via Override/Add/Multiply.
        /// </summary>
        public readonly ChangeValue Change;
        
        /// <summary>
        /// The current frame summary for recording impacts.
        /// </summary>
        public readonly FrameSummary FrameSummary;
        
        /// <summary>
        /// The action queue for deferred workers to enqueue actions.
        /// </summary>
        public readonly ActionQueue ActionQueue;
        
        public WorkerContext(
            IGameplayAbilitySystem system,
            Dictionary<Attribute, CachedAttributeValue> cache,
            ChangeValue change,
            FrameSummary frameSummary,
            ActionQueue actionQueue)
        {
            System = system;
            AttributeCache = cache;
            Change = change;
            FrameSummary = frameSummary;
            ActionQueue = actionQueue;
        }
        
        /// <summary>
        /// Get the mutable attribute cache (for inline workers that modify cache directly).
        /// </summary>
        public Dictionary<Attribute, CachedAttributeValue> GetMutableCache()
        {
            return (Dictionary<Attribute, CachedAttributeValue>)AttributeCache;
        }
    }
    
    /// <summary>
    /// Context for impact workers.
    /// </summary>
    public readonly struct ImpactWorkerContext
    {
        public readonly IGameplayAbilitySystem System;
        public readonly ImpactData ImpactData;
        public readonly FrameSummary FrameSummary;
        public readonly ActionQueue ActionQueue;
        
        public ImpactWorkerContext(
            IGameplayAbilitySystem system,
            ImpactData impactData,
            FrameSummary frameSummary,
            ActionQueue actionQueue)
        {
            System = system;
            ImpactData = impactData;
            FrameSummary = frameSummary;
            ActionQueue = actionQueue;
        }
    }
    
    /// <summary>
    /// Context for effect workers.
    /// </summary>
    public readonly struct EffectWorkerContext
    {
        public readonly IGameplayAbilitySystem System;
        public readonly IAttributeImpactDerivation Derivation;
        public readonly FrameSummary FrameSummary;
        public readonly ActionQueue ActionQueue;
        
        /// <summary>
        /// For tick events - the number of executed ticks.
        /// </summary>
        public readonly int ExecuteTicks;
        
        /// <summary>
        /// For impact events - the impact data.
        /// </summary>
        public readonly ImpactData? ImpactData;
        
        public EffectWorkerContext(
            IGameplayAbilitySystem system,
            IAttributeImpactDerivation derivation,
            FrameSummary frameSummary,
            ActionQueue actionQueue,
            int executeTicks = 0,
            ImpactData? impactData = null)
        {
            System = system;
            Derivation = derivation;
            FrameSummary = frameSummary;
            ActionQueue = actionQueue;
            ExecuteTicks = executeTicks;
            ImpactData = impactData;
        }
    }
}