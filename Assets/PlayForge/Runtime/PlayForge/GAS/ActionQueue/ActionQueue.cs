using System;
using System.Collections.Generic;
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
        // Priority buckets, highest first, walked by index. In a SortedDictionary, taking each action off walked it, and
        // its enumerator allocated every time, so every deferred action allocated as it ran (GAS-P9)
        private readonly List<Bucket> _buckets = new();

        private sealed class Bucket
        {
            public readonly int Priority;
            public readonly Queue<IRootAction> Actions = new();

            public Bucket(int priority) => Priority = priority;
        }

        // Actions across every bucket. Kept so the per-frame "anything to do?" check never walks the buckets (PF-006)
        private int _count;

        // Callbacks for pipeline transparency
        public delegate void ActionCallback(IRootAction action);

        /// <summary>
        /// Invoked when an action is queued. An action the system makes for itself, such as a rescale, is used again
        /// once the queue has run it, so read it during the call (GAS-P9).
        /// </summary>
        public event ActionCallback OnActionQueued;

        /// <summary>
        /// Invoked when an action is successfully executed. An action the system makes for itself is used again once
        /// this returns, so read it during the call (GAS-P9).
        /// </summary>
        public event ActionCallback OnActionExecuted;

        /// <summary>Invoked when an action is skipped due to IsValid returning false.</summary>
        public event ActionCallback OnActionInvalidated;

        /// <summary>
        /// Whether there are any pending actions.
        /// </summary>
        public bool HasActions => _count > 0;

        /// <summary>
        /// Total number of pending actions across all priority levels.
        /// </summary>
        public int TotalCount => _count;

        /// <summary>
        /// Queue an action for deferred execution.
        /// </summary>
        /// <param name="action">The action to queue</param>
        /// <param name="priorityOverride">Optional priority override (uses action.Priority if null)</param>
        public void Enqueue(IRootAction action, int? priorityOverride = null)
        {
            if (action == null) return;

            int priority = priorityOverride ?? action.Priority;

            BucketFor(priority).Actions.Enqueue(action);
            _count++;

            try
            {
                OnActionQueued?.Invoke(action);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ActionQueue] OnActionQueued callback threw: {e}");
            }
        }

        // The bucket for a priority, made in its place among the others the first time an action comes in at it
        private Bucket BucketFor(int priority)
        {
            int i = 0;
            for (; i < _buckets.Count; i++)
            {
                int existing = _buckets[i].Priority;
                if (existing == priority) return _buckets[i];
                if (existing < priority) break;
            }

            var bucket = new Bucket(priority);
            _buckets.Insert(i, bucket);
            return bucket;
        }

        /// <summary>
        /// Queue multiple actions.
        /// </summary>
        public void EnqueueRange(IEnumerable<IRootAction> actions)
        {
            if (actions == null) return;

            // A list or an array by index: a foreach through the interface allocated an enumerator for each (GAS-P9)
            if (actions is IReadOnlyList<IRootAction> list)
            {
                for (int i = 0; i < list.Count; i++) Enqueue(list[i]);
                return;
            }

            foreach (var action in actions)
            {
                Enqueue(action);
            }
        }

        /// <summary>
        /// Process all queued actions.
        /// New actions queued during processing will be included in the same pass.
        /// </summary>
        /// <param name="system">The source system context</param>
        /// <param name="maxIterations">Safety limit to prevent infinite loops</param>
        /// <returns>Number of actions processed</returns>
        public int ProcessAll(ISource system, int maxIterations = 10000)
        {
            int iterations = 0;
            int executed = 0;
            int invalidated = 0;

            while (HasActions && iterations++ < maxIterations)
            {
                var action = DequeueNext();
                if (action == null) break;

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

                    // Run, so one the system uses again goes back to it (GAS-P9)
                    if (action is IReusableAction reusable) reusable.Release();
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
            if (_count == 0) return null;

            for (int i = 0; i < _buckets.Count; i++)
            {
                var actions = _buckets[i].Actions;
                if (actions.Count > 0)
                {
                    _count--;
                    return actions.Dequeue();
                }
            }
            return null;
        }

        /// <summary>
        /// Clear all pending actions.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _buckets.Count; i++)
                _buckets[i].Actions.Clear();
            _count = 0;
        }

        /// <summary>
        /// Get a snapshot of pending actions for debugging.
        /// </summary>
        public List<(int priority, string description)> GetPendingActionsDebug()
        {
            var result = new List<(int, string)>();
            foreach (var bucket in _buckets)
            {
                foreach (var action in bucket.Actions)
                {
                    result.Add((bucket.Priority, action.Description));
                }
            }
            return result;
        }
    }
}
