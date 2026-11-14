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
            _cache.SafeAdd(attribute, worker);
        }
        
        public void RemoveWorker(IAttribute attribute, AbstractImpactWorker worker)
        {
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
            // holder applied its holder's effects to the target (GAS-72)
            var context = new ImpactWorkerContext(
                holder ?? impactData.SourcedModifier.Source,
                impactData,
                _frameSummary,
                _actionQueue
            );
            
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

                // A kind that runs once for a hit, which another of has, runs no more for it (FN-15)
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
                        break;
                        
                    case EWorkerExecution.Deferred:
                        var actions = worker.CreateDeferredActions(context);
                        _actionQueue?.EnqueueRange(actions);
                        break;

                    // Both halves, as attribute workers run it. With no case, it ran neither (GAS-27)
                    case EWorkerExecution.InlineAndDeferred:
                        worker.ActivateInline(impactData);
                        _actionQueue?.EnqueueRange(worker.CreateDeferredActions(context));
                        break;
                }
            }
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
        }
        
        // Legacy property access
        public Dictionary<IAttribute, List<AbstractImpactWorker>> Cache => _cache;
    }
}