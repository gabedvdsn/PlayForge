using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public partial class ExternalGAS
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
        public GameplayAbilitySystemCallbacks GASCallbacks { get; private set; }
        
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
        protected void InitializeEndOfFrameSystem()
        {
            _actionQueue = new ActionQueue();
            _frameSummary = new FrameSummary();
            GASCallbacks = new GameplayAbilitySystemCallbacks();
            
            // Wire up action queue callbacks to GAS callbacks
            _actionQueue.OnActionQueued += action => GASCallbacks.ActionQueued(action);
            _actionQueue.OnActionExecuted += action => GASCallbacks.ActionExecuted(action);
            _actionQueue.OnActionInvalidated += action =>
            {
                _frameSummary.RecordInvalidatedAction(action);
                GASCallbacks.ActionInvalidated(action);
            };
            
            // Give subsystems access to the deferred execution context
            SetupDeferredContexts();
        }
        
        /// <summary>
        /// Initialize with analysis workers.
        /// </summary>
        protected void InitializeEndOfFrameSystem(List<AbstractAnalysisWorker> analysisWorkers)
        {
            InitializeEndOfFrameSystem();
            
            AnalysisCache = new(analysisWorkers ?? new List<AbstractAnalysisWorker>());
            AnalysisCache.SetDeferredContext(_actionQueue, _frameSummary);
        }
        
        private void SetupDeferredContexts()
        {
            // Give TagCache access to deferred execution
            GetTagCache()?.SetDeferredContext(this, _actionQueue, _frameSummary);
            
            // Give ImpactWorkerCache access to deferred execution
            GetAbilitySystem()?.SetDeferredContext(_actionQueue, _frameSummary);
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
            GASCallbacks?.EntityDied(this);
    
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
            
            // Nothing else to do
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
            GASCallbacks?.Impact(impact);
            GASCallbacks?.DamageDealt(impact);
            GASCallbacks?.HealingDone(impact);
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
            GASCallbacks?.FrameComplete(snapshot);
            
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
