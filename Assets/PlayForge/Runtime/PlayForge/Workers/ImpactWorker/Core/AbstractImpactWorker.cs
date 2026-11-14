using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for impact workers that react to attribute impacts.
    /// Impact workers run on the source system after an impact is calculated.
    /// 
    /// Can be either Inline (for simple transformations) or Deferred (for system modifications).
    /// </summary>
    [Serializable]
    public abstract class AbstractImpactWorker
    {
        [Header("Impact Worker")]
        [Tooltip("Also take unworkable impacts, which only the system makes: an attribute's first pulse, and rescaling " +
                 "Current to Base. They're typed DisallowImpact too, so a worker that checks impact types still skips them (Decision 9)")]
        public bool AcceptUnworkableImpact = false;
        
        /// <summary>
        /// Execution mode for this worker.
        /// Inline: Executes immediately via Activate()
        /// Deferred: Queues actions via CreateActions()
        /// Default is Deferred for safety (prevents re-entrancy issues).
        /// </summary>
        public virtual EWorkerExecution Execution => EWorkerExecution.Deferred;

        /// <summary>
        /// Whether a kind of worker runs once for a hit, however many of it its holder has, as the Lifesteal worker, which
        /// would heal twice for one blow from its project's and its own (FN-15). The first of the kind to take the hit runs.
        /// </summary>
        public virtual bool OncePerHit => false;
        
        /// <summary>
        /// Fast pre-validation check using only the impact data.
        /// </summary>
        public abstract bool PreValidateWorkFor(ImpactData impactData);
        
        /// <summary>
        /// Full validation with impact data.
        /// </summary>
        public abstract bool ValidateWorkFor(ImpactData impactData);
        
        /// <summary>
        /// For INLINE workers: Execute immediately in response to the impact.
        /// Override this for simple transformations that don't call system methods.
        /// </summary>
        /// <param name="impactData">The impact data</param>
        public virtual void ActivateInline(ImpactData impactData) { }
        
        /// <summary>
        /// For DEFERRED workers: Create actions to queue for end-of-frame execution.
        /// Override this for workers that need to call ModifyAttribute or other system methods.
        /// </summary>
        /// <param name="ctx">The impact worker context</param>
        /// <returns>Actions to queue for end-of-frame execution</returns>
        public virtual IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            return Array.Empty<IRootAction>();
        }
        
        /// <summary>
        /// Subscribe this worker to the impact worker cache.
        /// </summary>
        public abstract void SubscribeToCache(ImpactWorkerCache cache);
        
        /// <summary>
        /// Unsubscribe this worker from the impact worker cache.
        /// </summary>
        public abstract void UnsubscribeFromCache(ImpactWorkerCache cache);
    }
}