using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Handles attribute change events (pre-change and post-change).
    /// Routes workers to appropriate execution mode (Inline vs Deferred).
    /// </summary>
    public class AttributeChangeMomentHandler
    {
        private Dictionary<Attribute, List<AbstractAttributeWorker>> _workers = new();
        
        /// <summary>
        /// Add a worker for a specific attribute.
        /// </summary>
        public bool AddWorker(Attribute attribute, AbstractAttributeWorker worker)
        {
            Debug.Log(worker.GetType().Name);
            if (_workers.ContainsKey(attribute))
            {
                if (_workers[attribute].Contains(worker)) return false;
                _workers[attribute].Add(worker);
            }
            else
            {
                _workers[attribute] = new List<AbstractAttributeWorker> { worker };
            }
            return true;
        }
        
        /// <summary>
        /// Remove a worker for a specific attribute.
        /// </summary>
        public bool RemoveWorker(Attribute attribute, AbstractAttributeWorker worker)
        {
            if (!_workers.ContainsKey(attribute)) return false;
            
            bool removed = _workers[attribute].Remove(worker);
            if (_workers[attribute].Count == 0)
            {
                _workers.Remove(attribute);
            }
            return removed;
        }
        
        /// <summary>
        /// Run all workers for an attribute change event.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void RunWorkers(WorkerContext context)
        {
            var attribute = context.Change.Value.BaseDerivation?.GetAttribute();
            if (attribute is null) return;
            
            if (!_workers.ContainsKey(attribute)) return;
            
            foreach (var worker in _workers[attribute])
            {
                // Fast pre-validation (no system access needed)
                if (!worker.PreValidateWorkFor(context.Change)) continue;
                
                // Full validation with system context
                if (!worker.ValidateWorkFor(context)) continue;
                
                // Route based on execution mode
                switch (worker.Execution)
                {
                    case EWorkerExecution.Inline:
                        // Execute immediately - can modify ChangeValue
                        worker.Intercept(context);
                        break;
                        
                    case EWorkerExecution.Deferred:
                        // Queue actions for end-of-frame
                        var actions = worker.DeferredIntercept(context);
                        context.ActionQueue?.EnqueueRange(actions);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Check if any workers are registered for an attribute.
        /// </summary>
        public bool HasWorkers(Attribute attribute)
        {
            return _workers.ContainsKey(attribute) && _workers[attribute].Count > 0;
        }
        
        /// <summary>
        /// Get count of workers for an attribute.
        /// </summary>
        public int GetWorkerCount(Attribute attribute)
        {
            return _workers.TryGetValue(attribute, out var workers) ? workers.Count : 0;
        }
        
        /// <summary>
        /// Get all registered attributes.
        /// </summary>
        public IEnumerable<Attribute> GetRegisteredAttributes()
        {
            return _workers.Keys;
        }
        
        /// <summary>
        /// Clear all workers.
        /// </summary>
        public void Clear()
        {
            _workers.Clear();
        }
        
        // Legacy property for backwards compatibility
        public Dictionary<Attribute, List<AbstractAttributeWorker>> ChangeEvents
        {
            get => _workers;
            set => _workers = value;
        }
        
        // Legacy method names for backwards compatibility
        public bool AddEvent(Attribute attribute, AbstractAttributeWorker worker) => AddWorker(attribute, worker);
        public bool RemoveEvent(Attribute attribute, AbstractAttributeWorker worker) => RemoveWorker(attribute, worker);
    }
}