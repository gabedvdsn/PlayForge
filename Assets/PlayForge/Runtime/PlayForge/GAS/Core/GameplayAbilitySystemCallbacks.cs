using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for the GameplayAbilitySystem level events.
    /// These are frame-level and cross-system events, distinct from
    /// AbilitySystemCallbacks (ability events) and AttributeSystemCallbacks (attribute events).
    /// </summary>
    public class GameplayAbilitySystemCallbacks
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // FRAME LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked at the end of each frame after all deferred actions are processed.
        /// Provides a complete summary of all modifications that occurred.
        /// </summary>
        public delegate void FrameCompleteDelegate(FrameSummarySnapshot summary);
        
        private FrameCompleteDelegate _onFrameComplete;
        public event FrameCompleteDelegate OnFrameComplete
        {
            add => _onFrameComplete += value;
            remove => _onFrameComplete -= value;
        }
        
        public void FrameComplete(FrameSummarySnapshot summary) => _onFrameComplete?.Invoke(summary);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ACTION QUEUE EVENTS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked when an action is queued for deferred execution.
        /// </summary>
        public delegate void ActionQueuedDelegate(IRootAction action);
        
        private ActionQueuedDelegate _onActionQueued;
        public event ActionQueuedDelegate OnActionQueued
        {
            add => _onActionQueued += value;
            remove => _onActionQueued -= value;
        }
        
        public void ActionQueued(IRootAction action) => _onActionQueued?.Invoke(action);
        
        /// <summary>
        /// Invoked when an action is executed from the queue.
        /// </summary>
        private ActionQueuedDelegate _onActionExecuted;
        public event ActionQueuedDelegate OnActionExecuted
        {
            add => _onActionExecuted += value;
            remove => _onActionExecuted -= value;
        }
        
        public void ActionExecuted(IRootAction action) => _onActionExecuted?.Invoke(action);
        
        /// <summary>
        /// Invoked when an action is skipped due to being invalidated.
        /// Useful for debugging and understanding what actions were prevented.
        /// </summary>
        private ActionQueuedDelegate _onActionInvalidated;
        public event ActionQueuedDelegate OnActionInvalidated
        {
            add => _onActionInvalidated += value;
            remove => _onActionInvalidated -= value;
        }
        
        public void ActionInvalidated(IRootAction action) => _onActionInvalidated?.Invoke(action);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked when an effect is applied.
        /// </summary>
        public delegate void EffectDelegate(GameplayEffectSpec spec);
        
        private EffectDelegate _onEffectApplied;
        public event EffectDelegate OnEffectApplied
        {
            add => _onEffectApplied += value;
            remove => _onEffectApplied -= value;
        }
        
        public void EffectApplied(GameplayEffectSpec spec) => _onEffectApplied?.Invoke(spec);
        
        /// <summary>
        /// Invoked when an effect is removed.
        /// </summary>
        public delegate void EffectRemovedDelegate(GameplayEffect effect);
        
        private EffectRemovedDelegate _onEffectRemoved;
        public event EffectRemovedDelegate OnEffectRemoved
        {
            add => _onEffectRemoved += value;
            remove => _onEffectRemoved -= value;
        }
        
        public void EffectRemoved(GameplayEffect effect) => _onEffectRemoved?.Invoke(effect);
        
        /// <summary>
        /// Invoked when an effect ticks.
        /// </summary>
        public delegate void EffectTickDelegate(GameplayEffectSpec spec, int tickCount);
        
        private EffectTickDelegate _onEffectTick;
        public event EffectTickDelegate OnEffectTick
        {
            add => _onEffectTick += value;
            remove => _onEffectTick -= value;
        }
        
        public void EffectTick(GameplayEffectSpec spec, int tickCount) => _onEffectTick?.Invoke(spec, tickCount);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG WORKER LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked when a tag worker is activated.
        /// </summary>
        public delegate void TagWorkerDelegate(AbstractTagWorker worker);
        
        private TagWorkerDelegate _onTagWorkerActivated;
        public event TagWorkerDelegate OnTagWorkerActivated
        {
            add => _onTagWorkerActivated += value;
            remove => _onTagWorkerActivated -= value;
        }
        
        public void TagWorkerActivated(AbstractTagWorker worker) => _onTagWorkerActivated?.Invoke(worker);
        
        /// <summary>
        /// Invoked when a tag worker is resolved (deactivated).
        /// </summary>
        private TagWorkerDelegate _onTagWorkerResolved;
        public event TagWorkerDelegate OnTagWorkerResolved
        {
            add => _onTagWorkerResolved += value;
            remove => _onTagWorkerResolved -= value;
        }
        
        public void TagWorkerResolved(AbstractTagWorker worker) => _onTagWorkerResolved?.Invoke(worker);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SYSTEM LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked when the system is initialized.
        /// </summary>
        public delegate void SystemDelegate();
        
        private SystemDelegate _onSystemInitialized;
        public event SystemDelegate OnSystemInitialized
        {
            add => _onSystemInitialized += value;
            remove => _onSystemInitialized -= value;
        }
        
        public void SystemInitialized() => _onSystemInitialized?.Invoke();
        
        /// <summary>
        /// Invoked when the system is disabled.
        /// </summary>
        private SystemDelegate _onSystemDisabled;
        public event SystemDelegate OnSystemDisabled
        {
            add => _onSystemDisabled += value;
            remove => _onSystemDisabled -= value;
        }
        
        public void SystemDisabled() => _onSystemDisabled?.Invoke();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // IMPACT TRACKING (for observer systems)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Invoked for every impact that occurs (attribute modification with real effect).
        /// This is the primary hook for damage tracking systems, combat logs, etc.
        /// </summary>
        public delegate void ImpactDelegate(ImpactData impact);
        
        private ImpactDelegate _onImpact;
        public event ImpactDelegate OnImpact
        {
            add => _onImpact += value;
            remove => _onImpact -= value;
        }
        
        public void Impact(ImpactData impact) => _onImpact?.Invoke(impact);
        
        /// <summary>
        /// Invoked when damage is dealt (negative current value impact).
        /// Convenience event for damage-specific tracking.
        /// </summary>
        private ImpactDelegate _onDamageDealt;
        public event ImpactDelegate OnDamageDealt
        {
            add => _onDamageDealt += value;
            remove => _onDamageDealt -= value;
        }
        
        public void DamageDealt(ImpactData impact)
        {
            if (impact.RealImpact.CurrentValue < 0)
                _onDamageDealt?.Invoke(impact);
        }
        
        /// <summary>
        /// Invoked when healing occurs (positive current value impact).
        /// Convenience event for healing-specific tracking.
        /// </summary>
        private ImpactDelegate _onHealingDone;
        public event ImpactDelegate OnHealingDone
        {
            add => _onHealingDone += value;
            remove => _onHealingDone -= value;
        }
        
        public void HealingDone(ImpactData impact)
        {
            if (impact.RealImpact.CurrentValue > 0)
                _onHealingDone?.Invoke(impact);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SPECIAL EVENT TRACKING
        // ═══════════════════════════════════════════════════════════════════════════

        public delegate void DeathDelegate(IGameplayAbilitySystem system);
        private event DeathDelegate _onEntityDied;
        public event DeathDelegate OnEntityDied { add => _onEntityDied += value; remove => _onEntityDied -= value; }
        
        public void EntityDied(IGameplayAbilitySystem system) => _onEntityDied?.Invoke(system);
    }
}