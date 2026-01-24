using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for attribute change workers.
    /// Workers can be Inline (modify change value synchronously) or Deferred (queue actions for end-of-frame).
    /// </summary>
    [Serializable]
    public abstract class AbstractAttributeWorker
    {
        /// <summary>
        /// Defines how this worker executes.
        /// Inline: Runs synchronously, can modify ChangeValue, must not call system methods.
        /// Deferred: Queues actions for end-of-frame, cannot modify ChangeValue.
        /// </summary>
        public abstract EWorkerExecution Execution { get; }
        
        /// <summary>
        /// When this worker runs in the modification pipeline.
        /// </summary>
        public abstract EChangeEventTiming Timing { get; }
        
        /// <summary>
        /// Fast pre-validation using only the change data (no system access needed).
        /// Used for quick filtering before full validation.
        /// </summary>
        /// <param name="change">The change value being processed</param>
        /// <returns>True if this worker might apply to this change</returns>
        public abstract bool PreValidateWorkFor(ChangeValue change);
        
        /// <summary>
        /// Full validation with complete context.
        /// </summary>
        /// <param name="ctx">The worker context with system access</param>
        /// <returns>True if this worker should execute for this change</returns>
        public abstract bool ValidateWorkFor(WorkerContext ctx);
        
        /// <summary>
        /// For Inline workers: directly modify the change value or attribute cache.
        /// Called synchronously during the modification pipeline.
        /// MUST NOT call system methods that trigger other workers (e.g., ModifyAttribute).
        /// </summary>
        /// <param name="ctx">The worker context</param>
        public virtual void Intercept(WorkerContext ctx) { }
        
        /// <summary>
        /// For Deferred workers: return action(s) to queue for end-of-frame execution.
        /// Called during the modification pipeline, but actions execute later.
        /// Safe to include actions that call any system method.
        /// </summary>
        /// <param name="ctx">The worker context</param>
        /// <returns>Actions to queue</returns>
        public virtual IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx) 
            => Array.Empty<IRootAction>();
        
        /// <summary>
        /// Register this worker with the attribute change handlers.
        /// </summary>
        public abstract bool RegisterWithHandler(
            AttributeChangeMomentHandler preChange, 
            AttributeChangeMomentHandler postChange);
        
        /// <summary>
        /// Unregister this worker from the attribute change handlers.
        /// </summary>
        public abstract bool DeRegisterFromHandler(
            AttributeChangeMomentHandler preChange, 
            AttributeChangeMomentHandler postChange);
        
        /// <summary>
        /// Timing options for attribute workers.
        /// </summary>
        public enum EChangeEventTiming
        {
            /// <summary>Before the modification is applied to the cache</summary>
            PreChange,
            /// <summary>After the modification is applied to the cache</summary>
            PostChange,
            /// <summary>Both pre and post change</summary>
            Both
        }
    }
}