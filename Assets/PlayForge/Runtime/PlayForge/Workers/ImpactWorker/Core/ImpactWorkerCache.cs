using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cache for impact workers, indexed by impacted attribute.
    /// Supports both inline and deferred execution modes.
    /// </summary>
    public class ImpactWorkerCache
    {
        private Dictionary<IAttribute, List<AbstractImpactWorker>> _cache = new();

        // How many sources give each worker for each attribute: given again, as by a second copy of an item, a worker ran
        // once for each; it runs once and stays until the last source takes it back
        private readonly Dictionary<(IAttribute, AbstractImpactWorker), int> _given = new();
        
        // References for deferred execution
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        
        public ImpactWorkerCache()
        {
            _cache = new Dictionary<IAttribute, List<AbstractImpactWorker>>();
        }
        
        public ImpactWorkerCache(List<AbstractImpactWorker> workers) : this()
        {
            foreach (var worker in workers)
            {
                worker.SubscribeToCache(this);
            }
        }
        
        /// <summary>
        /// Set the action queue and frame summary for deferred execution.
        /// </summary>
        public void SetDeferredContext(ActionQueue actionQueue, FrameSummary frameSummary)
        {
            _actionQueue = actionQueue;
            _frameSummary = frameSummary;
        }
        
        public void ProvideWorker(AbstractImpactWorker worker)
        {
            worker.SubscribeToCache(this);
        }
        
        public void RemoveWorker(AbstractImpactWorker worker)
        {
            worker.UnsubscribeFromCache(this);
        }
        
        public void ProvideWorker(IAttribute attribute, AbstractImpactWorker worker)
        {
            var key = (attribute, worker);
            _given.TryGetValue(key, out int given);
            _given[key] = given + 1;
            if (given == 0) _cache.SafeAdd(attribute, worker);
        }

        public void RemoveWorker(IAttribute attribute, AbstractImpactWorker worker)
        {
            var key = (attribute, worker);
            if (_given.TryGetValue(key, out int given) && given > 1)
            {
                _given[key] = given - 1;
                return;
            }
            _given.Remove(key);
            if (!_cache.ContainsKey(attribute)) return;
            _cache[attribute].Remove(worker);
        }
        
        /// <summary>
        /// Run all impact workers for the given impact data.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        /// <param name="holder">Whose workers these are: the impact's dealer, as impact workers run for the dealer.</param>
        public void RunImpactData(ImpactData impactData, ISource holder = null)
        {
            if (!_cache.ContainsKey(impactData.Attribute)) return;

            // The workers' holder, the dealer. It was the one hit, so a worker that took its context's System for its
            // holder applied its holder's effects to the target
            var context = new ImpactWorkerContext(
                holder ?? impactData.SourcedModifier.Source,
                impactData,
                _frameSummary,
                _actionQueue
            );
            
            // Where the dealer is heard, each worker's run is told, with what it queued
            var callbacks = (holder as IGameplayAbilitySystem)?.GetCallbacks();
            bool told = callbacks is { HasImpactWorkerListeners: true };

            // The kinds that run once for a hit and have run for this one, the first held apart so one kind asks no list
            System.Type ranOnce = null;
            List<System.Type> moreRanOnce = null;

            foreach (var worker in _cache[impactData.Attribute])
            {
                // Check workable flag
                if (!impactData.SourcedModifier.ImpactIsWorkable && !worker.AcceptUnworkableImpact) continue;

                // Pre-validation (fast check)
                if (!worker.PreValidateWorkFor(impactData)) continue;

                // Full validation
                if (!worker.ValidateWorkFor(impactData)) continue;

                // A kind that runs once for a hit, which another of has, runs no more for it
                if (worker.OncePerHit)
                {
                    var kind = worker.GetType();
                    if (kind == ranOnce || moreRanOnce != null && moreRanOnce.Contains(kind)) continue;
                    if (ranOnce == null) ranOnce = kind;
                    else (moreRanOnce ??= new List<System.Type>()).Add(kind);
                }
                
                // Route based on execution mode
                switch (worker.Execution)
                {
                    case EWorkerExecution.Inline:
                        worker.ActivateInline(impactData);
                        if (told) callbacks.ImpactWorkerRan(new ImpactWorkerRun(impactData, worker, worker.Execution, holder as IGameplayAbilitySystem, null));
                        break;
                        
                    case EWorkerExecution.Deferred:
                        Queue(worker, worker.CreateDeferredActions(context), impactData, holder, told ? callbacks : null);
                        break;

                    // Both halves, as attribute workers run it. With no case, it ran neither
                    case EWorkerExecution.InlineAndDeferred:
                        worker.ActivateInline(impactData);
                        Queue(worker, worker.CreateDeferredActions(context), impactData, holder, told ? callbacks : null);
                        break;
                }
            }
        }
        
        // A worker's actions, queued; told with them where its dealer is heard
        private void Queue(AbstractImpactWorker worker, IEnumerable<IRootAction> actions, in ImpactData impactData, ISource holder,
            GameplayAbilitySystemCallbacks callbacks)
        {
            if (callbacks == null)
            {
                _actionQueue?.EnqueueRange(actions);
                return;
            }
            var queued = actions != null ? new List<IRootAction>(actions) : new List<IRootAction>();
            for (int i = 0; i < queued.Count; i++) _actionQueue?.Enqueue(queued[i]);
            callbacks.ImpactWorkerRan(new ImpactWorkerRun(impactData, worker, worker.Execution, holder as IGameplayAbilitySystem, queued));
        }

        /// <summary>
        /// Check if any workers are registered for an attribute.
        /// </summary>
        public bool HasWorkers(IAttribute attribute)
        {
            return _cache.ContainsKey(attribute) && _cache[attribute].Count > 0;
        }
        
        /// <summary>
        /// Get count of workers for an attribute.
        /// </summary>
        public int GetWorkerCount(IAttribute attribute)
        {
            return _cache.TryGetValue(attribute, out var workers) ? workers.Count : 0;
        }
        
        /// <summary>
        /// Clear all workers.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _given.Clear();
        }

    }
}