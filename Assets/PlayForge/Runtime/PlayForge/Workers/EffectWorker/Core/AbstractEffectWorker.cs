using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for effect lifecycle workers.
    /// Effect workers are attached to specific effects and react to their lifecycle events.
    /// All actions from effect workers are scoped to the effect's lifetime.
    /// </summary>
    [Serializable]
    public abstract class AbstractEffectWorker
    {
        /// <summary>
        /// Called when the effect is first applied.
        /// Return actions to queue for deferred execution.
        /// </summary>
        public virtual IEnumerable<IRootAction> OnEffectApplication(EffectWorkerContext ctx)
            => Array.Empty<IRootAction>();
        
        /// <summary>
        /// Called when the effect ticks (for periodic effects).
        /// Return actions to queue for deferred execution.
        /// </summary>
        public virtual IEnumerable<IRootAction> OnEffectTick(EffectWorkerContext ctx)
            => Array.Empty<IRootAction>();
        
        /// <summary>
        /// Called when the effect is removed.
        /// Return actions to queue for deferred execution.
        /// Note: Actions should NOT be effect-scoped since the effect is being removed.
        /// </summary>
        public virtual IEnumerable<IRootAction> OnEffectRemoval(EffectWorkerContext ctx)
            => Array.Empty<IRootAction>();
        
        /// <summary>
        /// Called when the effect causes an impact (attribute modification).
        /// Return actions to queue for deferred execution.
        /// </summary>
        public virtual IEnumerable<IRootAction> OnEffectImpact(EffectWorkerContext ctx)
            => Array.Empty<IRootAction>();
    }
    
    /// <summary>
    /// Effect worker that tracks accumulated impact and performs work on removal.
    /// Useful for effects like "explode for accumulated damage when removed".
    /// </summary>
    [Serializable]
    public abstract class AbstractAccumulatingEffectWorker : AbstractEffectWorker
    {
        /// <summary>
        /// Per-effect instance data for tracking accumulation.
        /// </summary>
        protected class AccumulationData
        {
            public AttributeValue AccumulatedImpact;
            public int ImpactCount;
            
            public void Add(AttributeValue impact)
            {
                AccumulatedImpact += impact;
                ImpactCount++;
            }
            
            public void Reset()
            {
                AccumulatedImpact = default;
                ImpactCount = 0;
            }
        }
        
        // Note: Actual per-effect tracking would need to be managed by the effect container
        // This is a pattern for implementation, not a complete solution
        
        /// <summary>
        /// Get accumulated data for an effect instance.
        /// Implementation should store this in the effect container or derivation.
        /// </summary>
        protected abstract AccumulationData GetAccumulationData(EffectWorkerContext ctx);
        
        public override IEnumerable<IRootAction> OnEffectImpact(EffectWorkerContext ctx)
        {
            if (ctx.ImpactData.HasValue)
            {
                var data = GetAccumulationData(ctx);
                data?.Add(ctx.ImpactData.Value.RealImpact);
            }
            
            return Array.Empty<IRootAction>();
        }
    }
}