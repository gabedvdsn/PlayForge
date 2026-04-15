using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Partial class for RuntimeGameplayAbilitySystem adding end-of-frame processing.
    /// This handles the ActionQueue, FrameSummary, and deferred worker execution.
    /// Mirrors GameplayAbilitySystem.EndOfFrame.cs but with runtime-appropriate destruction.
    /// </summary>
    public partial class RuntimeGameplayAbilitySystem
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME STATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        private AnalysisWorkerCache AnalysisCache;
        
        // Death/destruction state
        private bool _isDead;
        private bool _pendingDestruction;

        public AnalysisWorkerCache GetAnalysisCache()
        {
            return AnalysisCache;
        }
        public bool IsDead => _isDead;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Initialize the end-of-frame processing system.
        /// Called during construction via Initialize().
        /// </summary>
        private void InitializeEndOfFrameSystem()
        {
            _actionQueue = new();
            _frameSummary = new();
            Callbacks = new();
            AnalysisCache = new();
            
            // Wire up action queue callbacks to GAS callbacks
            _actionQueue.OnActionQueued += action => Callbacks.ActionQueued(action);
            _actionQueue.OnActionExecuted += action => Callbacks.ActionExecuted(action);
            _actionQueue.OnActionInvalidated += action =>
            {
                _frameSummary.RecordInvalidatedAction(action);
                Callbacks.ActionInvalidated(action);
            };
        }
        
        private void SetupDeferredContexts()
        {
            TagCache.SetDeferredContext(this, _actionQueue, _frameSummary);
            AnalysisCache.SetDeferredContext(this, _actionQueue, _frameSummary);
            
            AttributeSystem.SetDeferredContext(this, _actionQueue, _frameSummary);
            AbilitySystem.SetDeferredContext(this, _actionQueue, _frameSummary);
            ItemSystem.SetDeferredContext(this, _actionQueue, _frameSummary);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ACTION QUEUE ACCESS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Queue an action for end-of-frame execution.
        /// </summary>
        public void QueueAction(IRootAction action)
        {
            _actionQueue?.Enqueue(action);
        }
        
        /// <summary>
        /// Queue multiple actions for end-of-frame execution.
        /// </summary>
        public void QueueActions(IEnumerable<IRootAction> actions)
        {
            _actionQueue?.EnqueueRange(actions);
        }
        
        /// <summary>
        /// Get the action queue (for advanced usage).
        /// </summary>
        public ActionQueue GetActionQueue() => _actionQueue;
        
        /// <summary>
        /// Get the current frame summary (for debugging/analysis).
        /// </summary>
        public FrameSummary GetFrameSummary() => _frameSummary;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DEATH HANDLING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Mark this entity as dead. Does NOT destroy immediately.
        /// The frame will complete normally, then destruction occurs.
        /// </summary>
        public void MarkDead()
        {
            if (_isDead) return;
            
            _isDead = true;
    
            // Fire death callback immediately so listeners can react
            Callbacks?.EntityDied(this);
    
            // Record in frame summary
            _frameSummary?.RecordDeath(this);
            
            _pendingDestruction = true;
        }

        /// <summary>
        /// Called after frame is fully complete. Terminates the process via ProcessControl.
        /// Unlike the MonoBehaviour version, there is no GameObject to destroy.
        /// </summary>
        protected virtual void HandleDestruction()
        {
            Callbacks?.SystemDisabled();
            
            ProcessControl.Instance.Terminate(Relay.CacheIndex);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // IMPACT RECORDING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Record an impact for the frame summary.
        /// Call this from ModifyAttribute after impact is calculated.
        /// </summary>
        public void RecordFrameImpact(ImpactData impact)
        {
            _frameSummary?.RecordImpact(impact);
            
            // Fire immediate callbacks
            Callbacks?.Impact(impact);
            Callbacks?.ReductionDealt(impact);
            Callbacks?.IncreaseDealt(impact);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME PROCESSING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Process all deferred work for this frame.
        /// Called from WhenLateUpdate via the process system.
        /// </summary>
        public void EndOfFrame()
        {
            if (_actionQueue == null) return;
            
            int totalExecuted = 0;
            int totalInvalidated = 0;
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 1: Process queued actions (may queue more actions)
            // ═══════════════════════════════════════════════════════════════════════
            int executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 2: Evaluate tag workers (queues activate/resolve actions)
            // ═══════════════════════════════════════════════════════════════════════
            TagCache.EvaluateTagWorkers();
            
            // Process tag worker activate/resolve actions
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 3: Tick active tag workers (may queue more actions)
            // ═══════════════════════════════════════════════════════════════════════
            TagCache.TickTagWorkers();
            
            // Process any actions queued by ticks
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 4: Run analysis workers
            // ═══════════════════════════════════════════════════════════════════════
            AnalysisCache.Analyze(this);
            
            // Process any actions queued by analysis workers
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 5: Fire frame complete callback
            // ═══════════════════════════════════════════════════════════════════════
            var snapshot = _frameSummary.CreateSnapshot(totalExecuted);
            Callbacks?.FrameComplete(snapshot);
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 6: Reset for next frame
            // ═══════════════════════════════════════════════════════════════════════
            _frameSummary.Clear();
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 7: Handle pending destruction
            // ═══════════════════════════════════════════════════════════════════════

            if (_pendingDestruction)
            {
                HandleDestruction();
            }
        }
    }
}
