using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Priority queue for deferred actions.
    /// Actions are processed in priority order (higher first), with FIFO within same priority.
    /// Provides callbacks for full pipeline transparency.
    /// </summary>
    public class ActionQueue
    {
        // Priority buckets - sorted descending (higher priority first)
        private readonly SortedDictionary<int, Queue<IRootAction>> _buckets;
        
        // Callbacks for pipeline transparency
        public delegate void ActionCallback(IRootAction action);
        
        /// <summary>Invoked when an action is queued.</summary>
        public event ActionCallback OnActionQueued;
        
        /// <summary>Invoked when an action is successfully executed.</summary>
        public event ActionCallback OnActionExecuted;
        
        /// <summary>Invoked when an action is skipped due to IsValid returning false.</summary>
        public event ActionCallback OnActionInvalidated;
        
        public ActionQueue()
        {
            // Use descending comparer so higher priorities come first
            _buckets = new SortedDictionary<int, Queue<IRootAction>>(
                Comparer<int>.Create((a, b) => b.CompareTo(a))
            );
        }
        
        /// <summary>
        /// Whether there are any pending actions.
        /// </summary>
        public bool HasActions => _buckets.Values.Any(q => q.Count > 0);
        
        /// <summary>
        /// Total number of pending actions across all priority levels.
        /// </summary>
        public int TotalCount => _buckets.Values.Sum(q => q.Count);
        
        /// <summary>
        /// Queue an action for deferred execution.
        /// </summary>
        /// <param name="action">The action to queue</param>
        /// <param name="priorityOverride">Optional priority override (uses action.Priority if null)</param>
        public void Enqueue(IRootAction action, int? priorityOverride = null)
        {
            if (action == null) return;
            
            int priority = priorityOverride ?? action.Priority;
            
            if (!_buckets.ContainsKey(priority))
                _buckets[priority] = new Queue<IRootAction>();
            
            _buckets[priority].Enqueue(action);
            
            try
            {
                OnActionQueued?.Invoke(action);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ActionQueue] OnActionQueued callback threw: {e}");
            }
        }
        
        /// <summary>
        /// Queue multiple actions.
        /// </summary>
        public void EnqueueRange(IEnumerable<IRootAction> actions)
        {
            if (actions == null) return;
            
            foreach (var action in actions)
            {
                Enqueue(action);
            }
        }
        
        /// <summary>
        /// Process all queued actions.
        /// New actions queued during processing will be included in the same pass.
        /// </summary>
        /// <param name="system">The gameplay ability system context</param>
        /// <param name="maxIterations">Safety limit to prevent infinite loops</param>
        /// <returns>Number of actions processed</returns>
        public int ProcessAll(IGameplayAbilitySystem system, int maxIterations = 10000)
        {
            int iterations = 0;
            int executed = 0;
            int invalidated = 0;
            
            while (HasActions && iterations++ < maxIterations)
            {
                var action = DequeueNext();
                if (action == null) break;

                Debug.Log($"[ActionQ] {action.Description}");
                
                if (action.IsValid)
                {
                    try
                    {
                        action.Execute(system);
                        executed++;
                        
                        try
                        {
                            OnActionExecuted?.Invoke(action);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[ActionQueue] OnActionExecuted callback threw: {e}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ActionQueue] Action execution threw: {action.Description}\n{e}");
                    }
                }
                else
                {
                    // Action was invalidated (e.g., effect removed)
                    invalidated++;
                    
                    try
                    {
                        OnActionInvalidated?.Invoke(action);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ActionQueue] OnActionInvalidated callback threw: {e}");
                    }
                }
            }
            
            if (iterations >= maxIterations)
            {
                Debug.LogError($"[ActionQueue] Max iterations ({maxIterations}) reached - possible infinite loop! " +
                              $"Executed: {executed}, Invalidated: {invalidated}, Remaining: {TotalCount}");
            }
            
            return executed;
        }
        
        /// <summary>
        /// Dequeue the next highest-priority action.
        /// </summary>
        private IRootAction DequeueNext()
        {
            foreach (var kvp in _buckets)
            {
                if (kvp.Value.Count > 0)
                    return kvp.Value.Dequeue();
            }
            return null;
        }
        
        /// <summary>
        /// Clear all pending actions.
        /// </summary>
        public void Clear()
        {
            foreach (var bucket in _buckets.Values)
                bucket.Clear();
        }
        
        /// <summary>
        /// Get a snapshot of pending actions for debugging.
        /// </summary>
        public List<(int priority, string description)> GetPendingActionsDebug()
        {
            var result = new List<(int, string)>();
            foreach (var kvp in _buckets)
            {
                foreach (var action in kvp.Value)
                {
                    result.Add((kvp.Key, action.Description));
                }
            }
            return result;
        }
    }
}