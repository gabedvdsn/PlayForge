using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity's queue of deferred actions, which its end of frame runs. Actions run by priority, higher first, and in
    /// the order queued within a priority; one queued as the queue runs takes its place by its priority in the same pass.
    /// Each is heard as it's queued, then once more as it's done with: run, dropped unrun, or failed.
    /// </summary>
    public class ActionQueue
    {
        // Priority buckets, highest first, walked by index. In a SortedDictionary, taking each action off walked it, and
        // its enumerator allocated every time, so every deferred action allocated as it ran
        private readonly List<Bucket> _buckets = new();

        private sealed class Bucket
        {
            public readonly int Priority;
            public readonly Queue<IRootAction> Actions = new();

            public Bucket(int priority) => Priority = priority;
        }

        // Actions across every bucket. Kept so the per-frame "anything to do?" check never walks the buckets
        private int _count;

        // Callbacks for pipeline transparency
        public delegate void ActionCallback(IRootAction action);

        /// <summary>A listener of an action that failed: its IsValid or its Execute threw <paramref name="error"/>.</summary>
        public delegate void ActionFailedCallback(IRootAction action, Exception error);

        /// <summary>
        /// Invoked when an action is queued. An action the system makes for itself, such as a rescale, is used again
        /// once the queue has run it, so read it during the call.
        /// </summary>
        public event ActionCallback OnActionQueued;

        /// <summary>
        /// Invoked when an action is successfully executed. An action the system makes for itself is used again once
        /// this returns, so read it during the call.
        /// </summary>
        public event ActionCallback OnActionExecuted;

        /// <summary>
        /// Invoked when an action is dropped unrun: its IsValid said no as its turn came, it was taken off with
        /// <see cref="Remove"/>, <see cref="RemoveWhere"/> or <see cref="Clear"/>, or its entity had ended.
        /// </summary>
        public event ActionCallback OnActionInvalidated;

        /// <summary>
        /// Invoked when an action fails as its turn comes: its IsValid or its Execute threw. It's written to the Console
        /// as well, and the rest of the queue runs on.
        /// </summary>
        public event ActionFailedCallback OnActionFailed;

        /// <summary>
        /// Whether its entity has ended, so it runs no more: an action queued to it now is dropped at once, and heard.
        /// </summary>
        public bool IsClosed { get; private set; }

        /// <summary>
        /// Whether there are any pending actions.
        /// </summary>
        public bool HasActions => _count > 0;

        /// <summary>
        /// Total number of pending actions across all priority levels.
        /// </summary>
        public int TotalCount => _count;

        /// <summary>
        /// Queue an action for deferred execution. Once the queue is closed, it's dropped at once.
        /// </summary>
        /// <param name="action">The action to queue</param>
        /// <param name="priorityOverride">Optional priority override (uses action.Priority if null)</param>
        public void Enqueue(IRootAction action, int? priorityOverride = null)
        {
            if (action == null) return;

            // Its entity has ended, so it's heard queued and then dropped: a listener doesn't wait on it for good
            if (IsClosed)
            {
                Tell(OnActionQueued, action, nameof(OnActionQueued));
                Tell(OnActionInvalidated, action, nameof(OnActionInvalidated));
                return;
            }

            int priority = priorityOverride ?? action.Priority;

            BucketFor(priority).Actions.Enqueue(action);
            _count++;

            Tell(OnActionQueued, action, nameof(OnActionQueued));
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

            // A list or an array by index: a foreach through the interface allocated an enumerator for each
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
        /// New actions queued during processing will be included in the same pass. An action whose IsValid or Execute
        /// throws fails, heard through <see cref="OnActionFailed"/>, and the rest run on.
        /// </summary>
        /// <param name="system">The source system context</param>
        /// <param name="maxIterations">Safety limit to prevent infinite loops: the actions left past it wait for the next pass</param>
        /// <returns>Number of actions processed</returns>
        public int ProcessAll(ISource system, int maxIterations = 10000)
        {
            int iterations = 0;
            int executed = 0;
            int invalidated = 0;
            int failed = 0;

            while (HasActions && iterations < maxIterations)
            {
                var action = DequeueNext();
                if (action == null) break;
                iterations++;

                // An IsValid that throws fails its action, as an Execute that throws does: it took the queue down with it,
                // and with the queue the rest of its entity's end of frame
                bool valid;
                try
                {
                    valid = action.IsValid;
                }
                catch (Exception e)
                {
                    failed++;
                    Fail(action, e, "as its IsValid was read");
                    continue;
                }

                if (!valid)
                {
                    // Action was invalidated (e.g., effect removed)
                    invalidated++;
                    Tell(OnActionInvalidated, action, nameof(OnActionInvalidated));
                    continue;
                }

                try
                {
                    action.Execute(system);
                }
                catch (Exception e)
                {
                    failed++;
                    Fail(action, e, "as it ran");
                    continue;
                }

                executed++;
                Tell(OnActionExecuted, action, nameof(OnActionExecuted));

                // Run, so one the system uses again goes back to it
                if (action is IReusableAction reusable) reusable.Release();
            }

            // Only while actions are left: a pass that ran exactly its limit and emptied the queue stopped nothing
            if (HasActions && iterations >= maxIterations)
            {
                ForgeLog.Error(ForgeLog.ESystem.General, $"[ActionQueue] Max iterations ({maxIterations}) reached - possible infinite loop! " +
                              $"Executed: {executed}, Invalidated: {invalidated}, Failed: {failed}, Remaining: {TotalCount}");
            }

            return executed;
        }

        // An action that threw, written to the Console as it always was, and heard. It isn't used again: it stays with the
        // frame summary that records it
        private void Fail(IRootAction action, Exception error, string when)
        {
            ForgeLog.Error(ForgeLog.ESystem.General, $"[ActionQueue] Action threw {when}: {DescriptionOf(action)}\n{error}");
            if (OnActionFailed == null) return;
            try
            {
                OnActionFailed(action, error);
            }
            catch (Exception e)
            {
                ForgeLog.Error(ForgeLog.ESystem.General, $"[ActionQueue] {nameof(OnActionFailed)} callback threw: {e}");
            }
        }

        // A listener that throws is written to the Console, and the queue goes on
        private static void Tell(ActionCallback listeners, IRootAction action, string name)
        {
            if (listeners == null) return;
            try
            {
                listeners(action);
            }
            catch (Exception e)
            {
                ForgeLog.Error(ForgeLog.ESystem.General, $"[ActionQueue] {name} callback threw: {e}");
            }
        }

        // What an action says it is, or its type's name where saying so throws too
        private static string DescriptionOf(IRootAction action)
        {
            try
            {
                return action.Description;
            }
            catch (Exception)
            {
                return action.GetType().Name;
            }
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
        /// Takes a queued action off unrun, heard through <see cref="OnActionInvalidated"/>, the rest keeping their
        /// order; false where it isn't waiting. One queued twice is taken off once.
        /// </summary>
        public bool Remove(IRootAction action)
        {
            if (action == null || _count == 0) return false;
            for (int i = 0; i < _buckets.Count; i++)
            {
                var actions = _buckets[i].Actions;
                if (!Holds(actions, action)) continue;
                bool taken = false;
                for (int n = actions.Count; n > 0; n--)
                {
                    var each = actions.Dequeue();
                    if (!taken && ReferenceEquals(each, action))
                    {
                        taken = true;
                        continue;
                    }
                    actions.Enqueue(each);
                }
                _count--;
                Tell(OnActionInvalidated, action, nameof(OnActionInvalidated));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes off unrun every queued action <paramref name="match"/> picks, each heard through
        /// <see cref="OnActionInvalidated"/>, the rest keeping their order; how many it took off.
        /// </summary>
        public int RemoveWhere(Predicate<IRootAction> match)
        {
            if (match == null || _count == 0) return 0;

            // Picked first, the queue untouched, so a match that throws leaves it as it was
            List<IRootAction> taken = null;
            for (int i = 0; i < _buckets.Count; i++)
            {
                foreach (var each in _buckets[i].Actions)
                    if (match(each)) (taken ??= new List<IRootAction>()).Add(each);
            }
            if (taken == null) return 0;

            for (int i = 0; i < _buckets.Count; i++)
            {
                var actions = _buckets[i].Actions;
                for (int n = actions.Count; n > 0; n--)
                {
                    var each = actions.Dequeue();
                    if (!Picked(taken, each)) actions.Enqueue(each);
                }
            }
            _count -= taken.Count;
            for (int i = 0; i < taken.Count; i++) Tell(OnActionInvalidated, taken[i], nameof(OnActionInvalidated));
            return taken.Count;
        }

        private static bool Holds(Queue<IRootAction> actions, IRootAction action)
        {
            foreach (var each in actions)
                if (ReferenceEquals(each, action)) return true;
            return false;
        }

        private static bool Picked(List<IRootAction> taken, IRootAction action)
        {
            for (int i = 0; i < taken.Count; i++)
                if (ReferenceEquals(taken[i], action)) return true;
            return false;
        }

        /// <summary>
        /// Drops every pending action unrun, each heard through <see cref="OnActionInvalidated"/>. One a listener queues
        /// as it hears waits as any other.
        /// </summary>
        public void Clear()
        {
            if (_count == 0) return;
            var dropped = new List<IRootAction>(_count);
            while (_count > 0) dropped.Add(DequeueNext());
            for (int i = 0; i < dropped.Count; i++) Tell(OnActionInvalidated, dropped[i], nameof(OnActionInvalidated));
        }

        /// <summary>
        /// Closes the queue as its entity ends: each pending action is dropped unrun, and one queued after is dropped at
        /// once, each heard through <see cref="OnActionInvalidated"/>.
        /// </summary>
        internal void Close()
        {
            IsClosed = true;
            Clear();
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
