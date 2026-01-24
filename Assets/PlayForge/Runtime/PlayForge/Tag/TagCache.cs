using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages tag state and tag workers for an entity.
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
        
        // ═══════════════════════════════════════════════════════════════
        // TAG MANAGEMENT
        // ═══════════════════════════════════════════════════════════════
        
        public List<Tag> GetAppliedTags() => _tagWeights.Keys.ToList();
        
        public int GetWeight(Tag tag) => _tagWeights.GetValueOrDefault(tag, -1);
        
        public bool HasTag(Tag tag) => _tagWeights.ContainsKey(tag) && _tagWeights[tag] > 0;
        
        public bool TryGetWeight(Tag tag, out int weight)
        {
            weight = GetWeight(tag);
            return weight >= 0;
        }
        
        public void AddTag(Tag tag, bool noDuplicates = false)
        {
            if (tag == null) return;
            
            if (_tagWeights.ContainsKey(tag))
            {
                if (!noDuplicates) _tagWeights[tag]++;
            }
            else
            {
                _tagWeights[tag] = 1;
            }
        }
        
        public void AddTags(IEnumerable<Tag> tags, bool noDuplicates = false)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                AddTag(tag, noDuplicates);
        }
        
        public void RemoveTag(Tag tag)
        {
            if (tag == null || !_tagWeights.ContainsKey(tag)) return;
            
            _tagWeights[tag]--;
            if (_tagWeights[tag] <= 0)
                _tagWeights.Remove(tag);
        }
        
        public void RemoveTags(IEnumerable<Tag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                RemoveTag(tag);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // TAG WORKER MANAGEMENT
        // ═══════════════════════════════════════════════════════════════
        
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
        
        // ═══════════════════════════════════════════════════════════════
        // END-OF-FRAME PROCESSING
        // ═══════════════════════════════════════════════════════════════
        
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
        
        // ═══════════════════════════════════════════════════════════════
        // RESET
        // ═══════════════════════════════════════════════════════════════
        
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
        
        // ═══════════════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════════════
        
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
    }
}