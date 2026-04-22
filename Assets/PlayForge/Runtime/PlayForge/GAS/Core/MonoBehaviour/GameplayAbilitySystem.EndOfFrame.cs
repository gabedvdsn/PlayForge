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
        
        /// <summary>
        /// Initialize the end-of-frame processing system.
        /// Call this during GAS initialization.
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
        /// Called after frame is fully complete. Override for custom cleanup.
        /// </summary>
        private void HandleDestruction()
        {
            Callbacks?.SystemDisabled();
            
            if (ProcessControl.Instance.TerminateImmediate(Relay.CacheIndex)) return;
            
            
            Destroy(gameObject);
        }

        protected virtual bool HandleDestructionInternally()
        {
            return false;
        }
        
        /// <summary>
        /// Record an impact this GAS DEALT (this GAS is the ISource).
        /// Call this from the source path after impact is calculated.
        /// </summary>
        public void RecordFrameImpactDealt(ImpactData impact)
        {
            _frameSummary?.RecordImpactDealt(impact);
            Callbacks?.ImpactDealt(impact);
        }

        /// <summary>
        /// Record an impact this GAS RECEIVED (this GAS is the ITarget).
        /// Call this from ModifyAttribute after impact is calculated.
        /// </summary>
        public void RecordFrameImpactReceived(ImpactData impact)
        {
            _frameSummary?.RecordImpactReceived(impact);
            Callbacks?.ImpactReceived(impact);
        }
        
        /// <summary>
        /// Process all deferred work for this frame.
        /// Call this at end of frame (e.g., in LateUpdate or via a manager).
        /// </summary>
        private void EndOfFrame()
        {
            if (_actionQueue == null) return;
            
            int totalExecuted = 0;
            
            int executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            TagCache.EvaluateTagWorkers();
            
            // Process tag worker activate/resolve actions
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;

            TagCache.TickTagWorkers();
            
            // Process any actions queued by ticks
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            
            AnalysisCache.Analyze(this);
            
            // Process any actions queued by analysis workers
            executed = _actionQueue.ProcessAll(this);
            totalExecuted += executed;
            var snapshot = _frameSummary.CreateSnapshot(totalExecuted);
            Callbacks?.FrameComplete(snapshot);
            
            _frameSummary.Clear();
            
            if (_pendingDestruction)
            {
                HandleDestruction();
            }
        }
    }
}