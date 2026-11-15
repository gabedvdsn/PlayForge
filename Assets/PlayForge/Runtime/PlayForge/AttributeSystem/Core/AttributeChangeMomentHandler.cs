using System;
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
        private Dictionary<IAttribute, List<AbstractAttributeWorker>> _workers = new();

        // How many sources give each worker for each attribute: two copies of an item share its workers, so one stays
        // until the last copy goes, and runs once (GAS-83)
        private readonly Dictionary<(IAttribute, AbstractAttributeWorker), int> _given = new();

        /// <summary>
        /// Add a worker for a specific attribute. A worker given again is counted, not added again, and stays until each
        /// source that gave it takes it back (GAS-83). True where it's new.
        /// </summary>
        public bool AddWorker(IAttribute attribute, AbstractAttributeWorker worker)
        {
            var key = (attribute, worker);
            _given.TryGetValue(key, out int given);
            _given[key] = given + 1;
            if (given > 0) return false;

            if (_workers.TryGetValue(attribute, out var workers)) workers.Add(worker);
            else _workers[attribute] = new List<AbstractAttributeWorker> { worker };
            return true;
        }

        /// <summary>
        /// Remove a worker for a specific attribute: one source's giving of it, the worker going with the last (GAS-83).
        /// True where a source gave it.
        /// </summary>
        public bool RemoveWorker(IAttribute attribute, AbstractAttributeWorker worker)
        {
            var key = (attribute, worker);
            if (!_given.TryGetValue(key, out int given)) return false;
            if (given > 1)
            {
                _given[key] = given - 1;
                return true;
            }
            _given.Remove(key);

            if (!_workers.TryGetValue(attribute, out var workers)) return false;
            bool removed = workers.Remove(worker);
            if (workers.Count == 0)
            {
                _workers.Remove(attribute);
            }
            return removed;
        }
        
        /// <summary>
        /// Run all workers for an attribute change event.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void RunWorkers(WorkerContext context) => RunWorkers(context, null);

        // With a recorder, each worker's step is recorded and told, its change before and after and the actions it queued
        // (LAB-13)
        internal void RunWorkers(WorkerContext context, ChangeStepRecorder steps)
        {
            if (steps is { Recording: false }) steps = null;
            // The attribute changed, not its carrier's: an effect's spec carries every impact it makes (GAS-78)
            var attribute = context.Change.Attribute ?? context.Change.Value.Carrier?.GetAttribute();
            if (attribute is null) return;
            
            if (!_workers.ContainsKey(attribute)) return;
            
            foreach (var worker in _workers[attribute])
            {
                // Fast pre-validation (no system access needed)
                if (!worker.PreValidateWorkFor(context.Change)) continue;
                
                // Full validation with system context
                if (!worker.ValidateWorkFor(context)) continue;

                var before = steps != null ? context.Change.Value.ToAttributeValue() : default;
                int queued = steps != null && context.ActionQueue != null ? context.ActionQueue.TotalCount : 0;
                
                // Route based on execution mode
                switch (worker.Execution)
                {
                    case EWorkerExecution.Inline:
                        // Execute immediately - can modify ChangeValue
                        worker.Intercept(context);
                        break;
                        
                    // A deferred worker's actions run later and may keep the change, so its commit doesn't reuse it (GAS-P6)
                    case EWorkerExecution.Deferred:
                        // Queue actions for end-of-frame
                        context.Change.Kept = true;
                        var actions = worker.DeferredIntercept(context);
                        context.ActionQueue?.EnqueueRange(actions);
                        break;
                    case EWorkerExecution.InlineAndDeferred:
                        worker.Intercept(context);
                        context.Change.Kept = true;
                        context.ActionQueue?.EnqueueRange(worker.DeferredIntercept(context));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (steps == null) continue;
                int now = context.ActionQueue != null ? context.ActionQueue.TotalCount : 0;
                steps.Add(new ChangeStep(EChangeStepKind.Worker, attribute, worker, ForgeWorkerNames.Of(worker), steps.Timing,
                    worker.Execution, before, context.Change.Value.ToAttributeValue(), now - queued));
            }
        }
        
        /// <summary>
        /// Check if any workers are registered for an attribute.
        /// </summary>
        public bool HasWorkers(IAttribute attribute)
        {
            return _workers.ContainsKey(attribute) && _workers[attribute].Count > 0;
        }
        
        /// <summary>
        /// Get count of workers for an attribute.
        /// </summary>
        public int GetWorkerCount(IAttribute attribute)
        {
            return _workers.TryGetValue(attribute, out var workers) ? workers.Count : 0;
        }
        
        /// <summary>
        /// Get all registered attributes.
        /// </summary>
        public IEnumerable<IAttribute> GetRegisteredAttributes()
        {
            return _workers.Keys;
        }
        
        /// <summary>
        /// Clear all workers.
        /// </summary>
        public void Clear()
        {
            _workers.Clear();
            _given.Clear();
        }

    }
}