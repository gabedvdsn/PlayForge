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
        private readonly Dictionary<Tag, int> _tagWeights;
        
        // Active worker tracking (no instances needed)
        private readonly HashSet<AbstractTagWorker> _activeWorkers;
        private readonly Dictionary<AbstractTagWorker, int> _tickCounters;
        
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
        public List<Tag> GetAppliedTags() => _tagWeights.Keys.ToList();
        
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
            
            if (_tagWeights.ContainsKey(tag))
            {
                if (!noDuplicates) _tagWeights[tag]++;
            }
            else
            {
                _tagWeights[tag] = 1;
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
            if (string.IsNullOrEmpty(tag.Name) || !_tagWeights.ContainsKey(tag)) return;
            
            _tagWeights[tag]--;
            if (_tagWeights[tag] <= 0)
                _tagWeights.Remove(tag);
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
            _tagWeights.Remove(tag);
        }
        
        /// <summary>
        /// Removes all tags matching the query.
        /// </summary>
        public int RemoveMatchingTags(Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            var toRemove = GetMatchingTags(query, matchMode).ToList();
            foreach (var tag in toRemove)
            {
                _tagWeights.Remove(tag);
            }
            return toRemove.Count;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TAG WORKER MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void ProvideWorker(AbstractTagWorker worker)
        {
            if (worker != null && !_tagWorkers.Contains(worker))
                _tagWorkers.Add(worker);
        }
        
        public void RemoveWorker(AbstractTagWorker worker)
        {
            if (worker == null) return;
            
            _tagWorkers.Remove(worker);
            
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
        public void EvaluateTagWorkers()
        {
            if (_system == null)
            {
                Debug.LogWarning("[TagCache] System not set - cannot evaluate tag workers");
                return;
            }
            
            // Check workers that should resolve (no longer valid)
            foreach (var worker in _activeWorkers.ToList())
            {
                if (worker.ValidateWorkFor(_handler)) continue;
                
                // Worker conditions no longer met - queue resolve
                _actionQueue?.Enqueue(new TagWorkerResolveAction(worker, _system, _frameSummary));
            }
            
            // Check workers that should activate (now valid)
            foreach (var worker in _tagWorkers)
            {
                if (!worker.ValidateWorkFor(_handler)) continue;
                if (_activeWorkers.Contains(worker)) continue;
                
                // Worker conditions now met - queue activate
                _actionQueue?.Enqueue(new TagWorkerActivateAction(worker, _system, _frameSummary));
            }
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
                // Skip if ticking is disabled
                if (worker.TickPause < 0) continue;
                
                // Check tick counter
                if (!_tickCounters.TryGetValue(worker, out int counter))
                    counter = 0;
                
                if (counter <= 0)
                {
                    // Time to tick
                    worker.Tick(_system);
                    _tickCounters[worker] = worker.TickPause;
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
            _tagWeights.Clear();
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
            _tagWeights.Clear();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void LogWeights()
        {
            Debug.Log("[ TAG WEIGHTS ]");
            foreach (var kvp in _tagWeights)
            {
                Debug.Log($"\t{kvp.Key.Name} => {kvp.Value}");
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
                roots[root].Add($"{tag.Name} ({_tagWeights[tag]})");
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