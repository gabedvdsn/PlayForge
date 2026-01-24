using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Partial class for GameplayAbilitySystem adding end-of-frame processing.
    /// This handles the ActionQueue, FrameSummary, and deferred worker execution.
    /// </summary>
    public partial class GameplayAbilitySystem
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME STATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        private AnalysisWorkerCache AnalysisCache;
        
        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.)
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; }
        
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
        /// Call this during GAS initialization.
        /// </summary>
        private void InitializeEndOfFrameSystem()
        {
            _actionQueue = new ActionQueue();
            _frameSummary = new FrameSummary();
            Callbacks = new GameplayAbilitySystemCallbacks();
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
            AnalysisCache.SetDeferredContext(_actionQueue, _frameSummary);
            
            AttributeSystem.SetDeferredContext(_actionQueue, _frameSummary);
            AbilitySystem.SetDeferredContext(_actionQueue, _frameSummary);
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
        }

        /// <summary>
        /// Queue destruction for after the current frame completes.
        /// </summary>
        public void QueueDestruction()
        {
            _pendingDestruction = true;
        }

        /// <summary>
        /// Called after frame is fully complete. Override for custom cleanup.
        /// </summary>
        protected virtual void HandleDestruction()
        {
            if (ProcessControl.Instance.Terminate(Relay.CacheIndex)) return;
            
            Destroy(gameObject);
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
            Callbacks?.DamageDealt(impact);
            Callbacks?.HealingDone(impact);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME PROCESSING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Process all deferred work for this frame.
        /// Call this at end of frame (e.g., in LateUpdate or via a manager).
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
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // EXTENSION METHODS FOR EXISTING SYSTEMS
    // ═══════════════════════════════════════════════════════════════════════════════
}
