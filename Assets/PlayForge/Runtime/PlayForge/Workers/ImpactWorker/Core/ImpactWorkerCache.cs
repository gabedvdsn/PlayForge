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
        private Dictionary<Attribute, List<AbstractImpactWorker>> _cache = new();
        
        // References for deferred execution
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        
        public ImpactWorkerCache()
        {
            _cache = new Dictionary<Attribute, List<AbstractImpactWorker>>();
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
        
        public void ProvideWorker(Attribute attribute, AbstractImpactWorker worker)
        {
            _cache.SafeAdd(attribute, worker);
        }
        
        public void RemoveWorker(Attribute attribute, AbstractImpactWorker worker)
        {
            if (!_cache.ContainsKey(attribute)) return;
            _cache[attribute].Remove(worker);
        }
        
        /// <summary>
        /// Run all impact workers for the given impact data.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void RunImpactData(ImpactData impactData)
        {
            if (!_cache.ContainsKey(impactData.Attribute)) return;
            
            var context = new ImpactWorkerContext(
                impactData.Target?.AsGAS(),
                impactData,
                _frameSummary,
                _actionQueue
            );
            
            foreach (var worker in _cache[impactData.Attribute])
            {
                // Check workable flag
                if (!impactData.SourcedModifier.ImpactIsWorkable && !worker.AcceptUnworkableImpact) continue;
                
                // Pre-validation (fast check)
                if (!worker.PreValidateWorkFor(impactData)) continue;
                
                // Full validation
                if (!worker.ValidateWorkFor(impactData)) continue;
                
                // Route based on execution mode
                switch (worker.Execution)
                {
                    case EWorkerExecution.Inline:
                        worker.Activate(impactData);
                        break;
                        
                    case EWorkerExecution.Deferred:
                        var actions = worker.CreateActions(context);
                        _actionQueue?.EnqueueRange(actions);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Check if any workers are registered for an attribute.
        /// </summary>
        public bool HasWorkers(Attribute attribute)
        {
            return _cache.ContainsKey(attribute) && _cache[attribute].Count > 0;
        }
        
        /// <summary>
        /// Get count of workers for an attribute.
        /// </summary>
        public int GetWorkerCount(Attribute attribute)
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
        public Dictionary<Attribute, List<AbstractImpactWorker>> Cache => _cache;
    }
}