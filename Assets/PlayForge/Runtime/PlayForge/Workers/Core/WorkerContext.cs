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
        public readonly IReadOnlyDictionary<IAttribute, CachedAttributeValue> AttributeCache;
        
        /// <summary>
        /// The change value being processed.
        /// Inline workers can modify this via Override/Add/Multiply.
        /// The commit reuses it once it's done, so an inline worker reads it during the call. A deferred worker's
        /// actions may keep it: a change handed to one is left to them (GAS-P6).
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
            Dictionary<IAttribute, CachedAttributeValue> cache,
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
        public Dictionary<IAttribute, CachedAttributeValue> GetMutableCache()
        {
            return (Dictionary<IAttribute, CachedAttributeValue>)AttributeCache;
        }
    }
    
    /// <summary>
    /// Context for impact workers.
    /// </summary>
    public readonly struct ImpactWorkerContext
    {
        public readonly ISource System;
        public readonly ImpactData ImpactData;
        public readonly FrameSummary FrameSummary;
        public readonly ActionQueue ActionQueue;
        
        public ImpactWorkerContext(
            ISource system,
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
        public readonly IAttributeImpactCarrier Carrier;
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

        /// <summary>
        /// For an instant effect's removal, every impact it made, one per attribute, of which ImpactData is the
        /// last. Its removal workers saw only that one (GAS-28). Empty otherwise.
        /// </summary>
        public readonly IReadOnlyList<ImpactData> Impacts;

        /// <summary>The effect's spec, where the carrier is one; null for another carrier.</summary>
        public GameplayEffectSpec Spec => Carrier as GameplayEffectSpec;

        /// <summary>
        /// The worker's state for this application of its effect (<see cref="GameplayEffectSpec.GetWorkerState{T}"/>);
        /// null where the carrier isn't an effect's spec (FEAT-29).
        /// </summary>
        public T GetState<T>(AbstractEffectWorker worker) where T : class, new() => Spec?.GetWorkerState<T>(worker);

        public EffectWorkerContext(
            IGameplayAbilitySystem system,
            IAttributeImpactCarrier carrier,
            FrameSummary frameSummary,
            ActionQueue actionQueue,
            int executeTicks = 0,
            ImpactData? impactData = null,
            IReadOnlyList<ImpactData> impacts = null)
        {
            System = system;
            this.Carrier = carrier;
            FrameSummary = frameSummary;
            ActionQueue = actionQueue;
            ExecuteTicks = executeTicks;
            ImpactData = impactData;
            Impacts = impacts ?? global::System.Array.Empty<ImpactData>();
        }
    }
}