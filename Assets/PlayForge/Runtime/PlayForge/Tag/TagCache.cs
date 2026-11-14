using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages tag state and tag workers for an entity.
    /// Supports hierarchical tag matching with O(1) exact lookups and O(n) hierarchical lookups.
    /// Tag worker activation/resolution is deferred to end-of-frame.
    /// </summary>
    public class TagCache
    {
        private readonly ITagHandler _handler;
        private readonly List<AbstractTagWorker> _tagWorkers;
        // Not readonly: a rebuild swaps it with the weights it had before (FEAT-3)
        private Dictionary<Tag, int> _tagWeights;

        // The applied tags as a list, rebuilt only when a tag is added or removed: requirement checks
        // ask for it every time an effect is applied, removed or re-validated (PF-007)
        private readonly List<Tag> _appliedTags = new();
        private bool _appliedTagsDirty = true;
        
        // Active worker tracking (no instances needed)
        private readonly HashSet<AbstractTagWorker> _activeWorkers;
        private readonly Dictionary<AbstractTagWorker, int> _tickCounters;
        
        // ═══ Tag-change events (FEAT-3) ═══

        /// <summary>
        /// Each change to a tag's weight, as it happens, or a rebuild's, as it ends. Nothing told anyone a tag came or
        /// went: a tag worker polled for it every frame (FEAT-3).
        /// </summary>
        public event Action<TagChange> TagChanged;

        private readonly Dictionary<Tag, List<TagListener>> _listeners = new();
        private int _listenerCount;

        // A listener stopped while a change is told is taken off once the telling is over, so no list changes under it
        private int _dispatching;
        private readonly List<TagListener> _stopped = new();

        // A rebuild's weights before it, which its end compares with the weights after it, and the changes it tells.
        // The changes are a stack, so a listener that rebuilds the tags while it hears one has its rebuild told in turn
        private Dictionary<Tag, int> _rebuildBefore = new();
        private bool _rebuilding;
        private readonly List<TagChange> _rebuildChanges = new();

        // Whether a tag changed since the effect system last checked its effects' requirements (GAS-56), and whether
        // the workers need evaluating: the tags changed, a worker came or went, or the last evaluation queued something
        private bool _changedSinceChecked;
        private bool _workersDirty = true;
        private int _everyFrameWorkers;

        // Each tag's ancestors' paths, for the listeners on a parent, made once a tag; and the tags listened to, by path,
        // so a listener on a unique tag, whose name is padded, hears the tags under its path (TAG-1)
        private static readonly Dictionary<Tag, string[]> ancestors = new();
        private readonly Dictionary<string, List<Tag>> _listenedByPath = new(StringComparer.Ordinal);

        // Deferred execution context
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        private IGameplayAbilitySystem _system;
        
        public TagCache(ITagHandler handler)
        {
            _handler = handler;
            _tagWorkers = new List<AbstractTagWorker>();
            _tagWeights = new Dictionary<Tag, int>();
            _activeWorkers = new HashSet<AbstractTagWorker>();
            _tickCounters = new Dictionary<AbstractTagWorker, int>();
        }
        
        public TagCache(ITagHandler handler, List<AbstractTagWorker> workers) : this(handler)
        {
            if (workers != null)
                _tagWorkers.AddRange(workers);
            CountEveryFrameWorkers();
        }
        
        /// <summary>
        /// Set the deferred execution context.
        /// </summary>
        public void SetDeferredContext(IGameplayAbilitySystem system, ActionQueue actionQueue, FrameSummary frameSummary)
        {
            _system = system;
            _actionQueue = actionQueue;
            _frameSummary = frameSummary;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns all currently applied tags.
        /// </summary>
        /// <summary>
        /// The tags applied now. The list belongs to the cache and is reused until a tag is added or
        /// removed: read it, don't change it, and copy it to keep it across tag changes.
        /// </summary>
        public List<Tag> GetAppliedTags()
        {
            if (_appliedTagsDirty)
            {
                _appliedTags.Clear();
                _appliedTags.AddRange(_tagWeights.Keys);
                _appliedTagsDirty = false;
            }
            return _appliedTags;
        }
        
        /// <summary>
        /// Gets the weight (stack count) of a specific tag. Returns -1 if tag not present.
        /// This is an EXACT match only.
        /// </summary>
        public int GetWeight(Tag tag) => _tagWeights.GetValueOrDefault(tag, -1);
        
        /// <summary>
        /// Returns true if the tag is present with weight > 0.
        /// This is an EXACT match only.
        /// </summary>
        public bool HasTagExact(Tag tag) => _tagWeights.ContainsKey(tag) && _tagWeights[tag] > 0;
        
        /// <summary>
        /// Returns true if the tag is present with weight > 0.
        /// Supports hierarchical matching via matchMode parameter.
        /// </summary>
        public bool HasTag(Tag tag, ETagMatchMode matchMode)
        {
            if (matchMode == ETagMatchMode.Exact)
            {
                return HasTagExact(tag);
            }
            
            // Hierarchical check - must iterate through all tags
            foreach (var kvp in _tagWeights)
            {
                if (kvp.Value <= 0) continue;
                
                switch (matchMode)
                {
                    case ETagMatchMode.IncludeChildren:
                        if (kvp.Key.MatchesOrIsChildOf(tag)) return true;
                        break;
                        
                    case ETagMatchMode.IncludeParents:
                        if (kvp.Key.MatchesOrIsParentOf(tag)) return true;
                        break;
                        
                    case ETagMatchMode.SameRoot:
                        if (kvp.Key.SharesAncestorWith(tag)) return true;
                        break;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Returns true if ANY of the specified tags are present.
        /// </summary>
        public bool HasAnyTag(IEnumerable<Tag> tags, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (HasTag(tag, matchMode)) return true;
            }
            return false;
        }
        
        /// <summary>
        /// Returns true if ALL of the specified tags are present.
        /// </summary>
        public bool HasAllTags(IEnumerable<Tag> tags, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (!HasTag(tag, matchMode)) return false;
            }
            return true;
        }
        
        /// <summary>
        /// Returns true if NONE of the specified tags are present.
        /// </summary>
        public bool HasNoneOfTags(IEnumerable<Tag> tags, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            foreach (var tag in tags)
            {
                if (HasTag(tag, matchMode)) return false;
            }
            return true;
        }
        
        /// <summary>
        /// Gets the total weight of tags matching the query (hierarchical).
        /// </summary>
        public int GetMatchingWeight(Tag tag, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (matchMode == ETagMatchMode.Exact)
            {
                return GetWeight(tag);
            }
            
            int total = 0;
            foreach (var kvp in _tagWeights)
            {
                if (kvp.Value <= 0) continue;
                
                bool matches = matchMode switch
                {
                    ETagMatchMode.IncludeChildren => kvp.Key.MatchesOrIsChildOf(tag),
                    ETagMatchMode.IncludeParents => kvp.Key.MatchesOrIsParentOf(tag),
                    ETagMatchMode.SameRoot => kvp.Key.SharesAncestorWith(tag),
                    _ => kvp.Key.Equals(tag)
                };
                
                if (matches) total += kvp.Value;
            }
            
            return total;
        }
        
        /// <summary>
        /// Gets all tags that match the query according to match mode.
        /// </summary>
        public IEnumerable<Tag> GetMatchingTags(Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            foreach (var kvp in _tagWeights)
            {
                if (kvp.Value <= 0) continue;
                
                bool matches = matchMode switch
                {
                    ETagMatchMode.Exact => kvp.Key.Equals(query),
                    ETagMatchMode.IncludeChildren => kvp.Key.MatchesOrIsChildOf(query),
                    ETagMatchMode.IncludeParents => kvp.Key.MatchesOrIsParentOf(query),
                    ETagMatchMode.SameRoot => kvp.Key.SharesAncestorWith(query),
                    _ => false
                };
                
                if (matches) yield return kvp.Key;
            }
        }
        
        /// <summary>
        /// Tries to get the weight of a tag.
        /// </summary>
        public bool TryGetWeight(Tag tag, out int weight)
        {
            weight = GetWeight(tag);
            return weight >= 0;
        }
        
        /// <summary>
        /// Adds a tag with weight 1, or increments existing weight.
        /// </summary>
        public void AddTag(Tag tag, bool noDuplicates = false)
        {
            if (string.IsNullOrEmpty(tag.Name)) return;
            
            if (_tagWeights.TryGetValue(tag, out int before))
            {
                if (noDuplicates) return;
                _tagWeights[tag] = before + 1;
                Changed(tag, before, before + 1);
            }
            else
            {
                _tagWeights[tag] = 1;
                _appliedTagsDirty = true;
                Changed(tag, 0, 1);
            }
        }
        
        /// <summary>
        /// Adds multiple tags.
        /// </summary>
        public void AddTags(IEnumerable<Tag> tags, bool noDuplicates = false)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                AddTag(tag, noDuplicates);
        }
        
        /// <summary>
        /// Removes one instance of a tag (decrements weight).
        /// </summary>
        public void RemoveTag(Tag tag)
        {
            if (string.IsNullOrEmpty(tag.Name) || !_tagWeights.TryGetValue(tag, out int before)) return;
            
            int after = before - 1;
            if (after <= 0)
            {
                _tagWeights.Remove(tag);
                _appliedTagsDirty = true;
                after = 0;
            }
            else _tagWeights[tag] = after;
            Changed(tag, before, after);
        }
        
        /// <summary>
        /// Removes multiple tags.
        /// </summary>
        public void RemoveTags(IEnumerable<Tag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                RemoveTag(tag);
        }
        
        /// <summary>
        /// Completely removes a tag regardless of weight.
        /// </summary>
        public void RemoveTagCompletely(Tag tag)
        {
            if (!_tagWeights.TryGetValue(tag, out int before)) return;
            _tagWeights.Remove(tag);
            _appliedTagsDirty = true;
            Changed(tag, before, 0);
        }
        
        /// <summary>
        /// Removes all tags matching the query.
        /// </summary>
        public int RemoveMatchingTags(Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            var toRemove = GetMatchingTags(query, matchMode).ToList();
            foreach (var tag in toRemove)
            {
                int before = _tagWeights[tag];
                _tagWeights.Remove(tag);
                _appliedTagsDirty = true;
                Changed(tag, before, 0);
            }
            return toRemove.Count;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG WORKER MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void ProvideWorker(AbstractTagWorker worker)
        {
            if (worker == null || _tagWorkers.Contains(worker)) return;
            _tagWorkers.Add(worker);
            _workersDirty = true;
            CountEveryFrameWorkers();
        }
        
        public void RemoveWorker(AbstractTagWorker worker)
        {
            if (worker == null) return;
            
            if (_tagWorkers.Remove(worker))
            {
                _workersDirty = true;
                CountEveryFrameWorkers();
            }
            
            // If it was active, resolve it
            if (_activeWorkers.Contains(worker))
            {
                _activeWorkers.Remove(worker);
                _tickCounters.Remove(worker);
                worker.Resolve(_system);
            }
        }
        
        public bool IsWorkerActive(AbstractTagWorker worker) => _activeWorkers.Contains(worker);

        public int ActiveWorkerCount => _activeWorkers.Count;
        public int RegisteredWorkerCount => _tagWorkers.Count;

        /// <summary>Read-only view of all registered tag workers (for debuggers/inspectors).</summary>
        public IReadOnlyList<AbstractTagWorker> RegisteredWorkers => _tagWorkers;

        /// <summary>Read-only view of currently-active tag workers.</summary>
        public IReadOnlyCollection<AbstractTagWorker> ActiveWorkers => _activeWorkers;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME PROCESSING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Evaluate tag worker states and queue activate/resolve actions.
        /// Called during end-of-frame processing.
        /// </summary>
        /// <remarks>
        /// It evaluates them only when the tags changed, a worker came or went, or the last evaluation queued an action,
        /// which it then sees land: in any other frame, every worker would come out as it was. The workers were evaluated
        /// at the end of every frame, a poll for the tags they name (FEAT-3). A worker whose validity reads more than the
        /// tags asks for every frame with <see cref="AbstractTagWorker.EvaluateEveryFrame"/>.
        /// </remarks>
        public void EvaluateTagWorkers()
        {
            if (!_workersDirty && _everyFrameWorkers == 0) return;

            if (_system == null)
            {
                Debug.LogWarning("[TagCache] System not set - cannot evaluate tag workers");
                return;
            }
            
            bool queued = false;

            // Check workers that should resolve (no longer valid). Resolving is queued, not done here, so
            // the set isn't changed while it's walked and needs no copy: copying it allocated every frame
            // for every GAS, workers or not (PF-007)
            foreach (var worker in _activeWorkers)
            {
                if (worker.ValidateWorkFor(_handler)) continue;
                
                // Worker conditions no longer met - queue resolve
                _actionQueue?.Enqueue(new TagWorkerResolveAction(worker, _system, _frameSummary));
                queued = true;
            }
            
            // Check workers that should activate (now valid)
            foreach (var worker in _tagWorkers)
            {
                if (!worker.ValidateWorkFor(_handler)) continue;
                if (_activeWorkers.Contains(worker)) continue;
                
                // Worker conditions now met - queue activate
                _actionQueue?.Enqueue(new TagWorkerActivateAction(worker, _system, _frameSummary));
                queued = true;
            }

            _workersDirty = queued;
        }
        
        /// <summary>
        /// Tick all active tag workers.
        /// Called after tag worker activation/resolution actions are processed.
        /// </summary>
        public void TickTagWorkers()
        {
            if (_system == null) return;
            
            foreach (var worker in _activeWorkers)
            {
                // Skip if ticking is disabled: a pause below 0, read at the entity's level (FN-19)
                int pause = worker.TickPause?.Evaluate(NumberContext.Of(_system)) ?? -1;
                if (pause < 0) continue;
                
                // Check tick counter
                if (!_tickCounters.TryGetValue(worker, out int counter))
                    counter = 0;
                
                if (counter <= 0)
                {
                    // Time to tick
                    worker.Tick(_system);
                    _tickCounters[worker] = pause;
                }
                else
                {
                    // Decrement counter
                    _tickCounters[worker] = counter - 1;
                }
            }
        }
        
        /// <summary>
        /// Mark a worker as active. Called by TagWorkerActivateAction.
        /// </summary>
        internal void MarkWorkerActive(AbstractTagWorker worker)
        {
            _activeWorkers.Add(worker);
            _tickCounters[worker] = 0;
        }
        
        /// <summary>
        /// Mark a worker as inactive. Called by TagWorkerResolveAction.
        /// </summary>
        internal void MarkWorkerInactive(AbstractTagWorker worker)
        {
            _activeWorkers.Remove(worker);
            _tickCounters.Remove(worker);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // RESET
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Reset all tag state and resolve all active workers.
        /// </summary>
        public void ResetWeights()
        {
            ClearAndTell();
        }

        public void ResetCache()
        {
            // Resolve all active workers
            foreach (var worker in _activeWorkers.ToList())
            {
                worker.Resolve(_system);
            }
            
            _activeWorkers.Clear();
            _tickCounters.Clear();
            ClearAndTell();
            _workersDirty = true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG-CHANGE EVENTS (FEAT-3)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Listens to a tag and the tags under it, as a listener on <c>Status</c> hears <c>Status.Burning</c>: as each
        /// comes or goes, or, with <see cref="ETagChangeKind.AnyCount"/>, as its weight changes at all. A change is told
        /// as it happens, and a rebuild's, as a compile is, as it ends, with only what it changed. Dispose the listener to
        /// stop it (FEAT-3, Decision 30).
        /// </summary>
        public TagListener Listen(Tag tag, Action<TagChange> onChange, ETagChangeKind kind = ETagChangeKind.AddedOrRemoved)
        {
            if (onChange == null) throw new ArgumentNullException(nameof(onChange));
            var listener = new TagListener(this, tag, onChange, kind);
            if (!_listeners.TryGetValue(tag, out var list))
            {
                _listeners[tag] = list = new List<TagListener>();
                if (!_listenedByPath.TryGetValue(tag.Path, out var listened)) _listenedByPath[tag.Path] = listened = new List<Tag>();
                listened.Add(tag);
            }
            list.Add(listener);
            _listenerCount++;
            return listener;
        }

        internal void StopListening(TagListener listener)
        {
            if (_dispatching > 0) _stopped.Add(listener);
            else TakeOff(listener);
        }

        private void TakeOff(TagListener listener)
        {
            if (!_listeners.TryGetValue(listener.Tag, out var list) || !list.Remove(listener)) return;
            _listenerCount--;
            if (list.Count > 0) return;
            _listeners.Remove(listener.Tag);
            if (_listenedByPath.TryGetValue(listener.Tag.Path, out var listened))
            {
                listened.Remove(listener.Tag);
                if (listened.Count == 0) _listenedByPath.Remove(listener.Tag.Path);
            }
        }

        /// <summary>
        /// Starts a rebuild of the weights from their sources, as a compile is: the weights empty, and the changes wait
        /// for <see cref="EndRebuild"/>, which tells what the rebuild changed. Each of its steps told, every tag would go
        /// and come back (FEAT-3).
        /// </summary>
        public void BeginRebuild()
        {
            if (_rebuilding) return;
            (_rebuildBefore, _tagWeights) = (_tagWeights, _rebuildBefore);
            _tagWeights.Clear();
            _appliedTagsDirty = true;
            _rebuilding = true;
        }

        /// <summary>Ends a rebuild, and tells each tag whose weight it changed.</summary>
        public void EndRebuild()
        {
            if (!_rebuilding) return;
            _rebuilding = false;
            int start = _rebuildChanges.Count;
            foreach (var pair in _tagWeights)
            {
                _rebuildBefore.TryGetValue(pair.Key, out int before);
                if (before != pair.Value) _rebuildChanges.Add(new TagChange(pair.Key, before, pair.Value));
            }
            foreach (var pair in _rebuildBefore)
                if (!_tagWeights.ContainsKey(pair.Key)) _rebuildChanges.Add(new TagChange(pair.Key, pair.Value, 0));
            _rebuildBefore.Clear();

            int end = _rebuildChanges.Count;
            if (end == start) return;
            _changedSinceChecked = true;
            _workersDirty = true;
            try
            {
                for (int i = start; i < end; i++) Tell(_rebuildChanges[i]);
            }
            finally
            {
                _rebuildChanges.RemoveRange(start, _rebuildChanges.Count - start);
            }
        }

        /// <summary>
        /// Whether a rebuild changed a tag since <see cref="MarkTagsChecked"/>, as an effect's requirements ask: a tag an
        /// ability, an item or a loose grant gives or takes back comes through a rebuild, and ended no effect until another
        /// effect came or went (GAS-56). An effect that comes or goes checks the requirements itself, and a tag added
        /// straight to the cache, which the next rebuild drops, asks for nothing.
        /// </summary>
        internal bool TagsChangedSinceChecked => _changedSinceChecked;

        internal void MarkTagsChecked() => _changedSinceChecked = false;

        // A weight that changed, told now, or at a rebuild's end with the rest of what it changed
        private void Changed(Tag tag, int before, int after)
        {
            if (_rebuilding) return;
            _workersDirty = true;
            Tell(new TagChange(tag, before, after));
        }

        private void Tell(TagChange change)
        {
            TagChanged?.Invoke(change);
            if (_listenerCount == 0) return;

            _dispatching++;
            try
            {
                TellListenersOf(change.Tag, change);
                // The listeners on each tag whose path is above the tag's, plain or unique (TAG-1)
                var above = AncestorsOf(change.Tag);
                for (int i = 0; i < above.Length; i++)
                {
                    if (!_listenedByPath.TryGetValue(above[i], out var listened)) continue;
                    int count = listened.Count;
                    for (int j = 0; j < count && j < listened.Count; j++) TellListenersOf(listened[j], change);
                }
            }
            finally
            {
                if (--_dispatching == 0 && _stopped.Count > 0)
                {
                    for (int i = 0; i < _stopped.Count; i++) TakeOff(_stopped[i]);
                    _stopped.Clear();
                }
            }
        }

        // By index over the listeners there were as the telling began; one added meanwhile hears the next change
        private void TellListenersOf(Tag tag, TagChange change)
        {
            if (!_listeners.TryGetValue(tag, out var list)) return;
            int count = list.Count;
            for (int i = 0; i < count && i < list.Count; i++)
            {
                var listener = list[i];
                if (!listener.Listening || !listener.Hears(change)) continue;
                try
                {
                    listener.OnChange(change);
                }
                catch (Exception ex)
                {
                    // One listener's throw stops neither the change nor the others
                    Debug.LogException(ex);
                }
            }
        }

        // Empties the weights, then tells each tag's going, so a listener reads the cache as it is now
        private void ClearAndTell()
        {
            if (_rebuilding || _tagWeights.Count == 0)
            {
                _tagWeights.Clear();
                _appliedTagsDirty = true;
                return;
            }
            int start = _rebuildChanges.Count;
            foreach (var pair in _tagWeights) _rebuildChanges.Add(new TagChange(pair.Key, pair.Value, 0));
            _tagWeights.Clear();
            _appliedTagsDirty = true;
            _changedSinceChecked = true;
            _workersDirty = true;
            int end = _rebuildChanges.Count;
            try
            {
                for (int i = start; i < end; i++) Tell(_rebuildChanges[i]);
            }
            finally
            {
                _rebuildChanges.RemoveRange(start, _rebuildChanges.Count - start);
            }
        }

        private static string[] AncestorsOf(Tag tag)
        {
            if (ancestors.TryGetValue(tag, out var found)) return found;
            var list = new List<string>();
            foreach (var each in tag.GetAncestorPaths()) list.Add(each);
            found = list.ToArray();
            ancestors[tag] = found;
            return found;
        }

        private void CountEveryFrameWorkers()
        {
            _everyFrameWorkers = 0;
            for (int i = 0; i < _tagWorkers.Count; i++)
                if (_tagWorkers[i] != null && _tagWorkers[i].EvaluateEveryFrame) _everyFrameWorkers++;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void LogWeights()
        {
            Debug.Log("[ TAG WEIGHTS ]");
            foreach (var kvp in _tagWeights)
            {
                Debug.Log($"\t{kvp.Key} => {kvp.Value}");
            }
        }
        
        public void LogActiveWorkers()
        {
            Debug.Log($"[ ACTIVE TAG WORKERS ({_activeWorkers.Count}) ]");
            foreach (var worker in _activeWorkers)
            {
                Debug.Log($"\t{worker.GetType().Name}");
            }
        }
        
        /// <summary>
        /// Returns a formatted string of all tags grouped by hierarchy.
        /// </summary>
        public string GetHierarchyDebugString()
        {
            var roots = new Dictionary<string, List<string>>();
            
            foreach (var tag in _tagWeights.Keys)
            {
                var root = tag.GetRoot().Name;
                if (!roots.ContainsKey(root))
                    roots[root] = new List<string>();
                roots[root].Add($"{tag} ({_tagWeights[tag]})");
            }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[ TAG HIERARCHY ]");
            foreach (var kvp in roots.OrderBy(k => k.Key))
            {
                sb.AppendLine($"  {kvp.Key}:");
                foreach (var tag in kvp.Value.OrderBy(t => t))
                {
                    sb.AppendLine($"    - {tag}");
                }
            }
            
            return sb.ToString();
        }
    }
}