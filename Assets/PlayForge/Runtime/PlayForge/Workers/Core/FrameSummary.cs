using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Captures all modifications and events that occurred during a frame.
    /// Used by observer systems to monitor gameplay without attaching workers to every entity.
    /// </summary>
    public class FrameSummary
    {
        /// <summary>
        /// All attribute impacts that occurred this frame, in order of occurrence.
        /// </summary>
        public List<ImpactData> Impacts { get; } = new();
        
        /// <summary>
        /// Impacts indexed by attribute for quick lookups.
        /// </summary>
        public Dictionary<Attribute, List<ImpactData>> ImpactsByAttribute { get; } = new();
        
        /// <summary>
        /// Impacts indexed by source system for tracking damage dealt.
        /// </summary>
        public Dictionary<IGameplayAbilitySystem, List<ImpactData>> ImpactsBySource { get; } = new();
        
        /// <summary>
        /// Impacts indexed by target for tracking damage received.
        /// </summary>
        public Dictionary<ITarget, List<ImpactData>> ImpactsByTarget { get; } = new();
        
        /// <summary>
        /// Actions that were queued but invalidated before execution.
        /// Useful for debugging and understanding what was prevented.
        /// </summary>
        public List<IRootAction> InvalidatedActions { get; } = new();
        
        /// <summary>
        /// Tag workers that activated this frame.
        /// </summary>
        public List<AbstractTagWorker> ActivatedTagWorkers { get; } = new();
        
        /// <summary>
        /// Tag workers that resolved (deactivated) this frame.
        /// </summary>
        public List<AbstractTagWorker> ResolvedTagWorkers { get; } = new();
        
        /// <summary>
        /// Effects that were applied this frame.
        /// </summary>
        public List<GameplayEffectSpec> AppliedEffects { get; } = new();
        
        /// <summary>
        /// Effects that were removed this frame.
        /// </summary>
        public List<GameplayEffect> RemovedEffects { get; } = new();
        
        /// <summary>
        /// GAS deaths this frame.
        /// </summary>
        public List<IGameplayAbilitySystem> Deaths { get; } = new();
        
        // ═══════════════════════════════════════════════════════════════
        // RECORDING METHODS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Record an attribute impact.
        /// </summary>
        public void RecordImpact(ImpactData impact)
        {
            Impacts.Add(impact);
            
            // Index by attribute
            if (!ImpactsByAttribute.ContainsKey(impact.Attribute))
                ImpactsByAttribute[impact.Attribute] = new List<ImpactData>();
            ImpactsByAttribute[impact.Attribute].Add(impact);
            
            // Index by source
            var source = impact.SourcedModifier.BaseDerivation?.GetSource() as IGameplayAbilitySystem;
            if (source != null)
            {
                if (!ImpactsBySource.ContainsKey(source))
                    ImpactsBySource[source] = new List<ImpactData>();
                ImpactsBySource[source].Add(impact);
            }
            
            // Index by target
            if (impact.Target != null)
            {
                if (!ImpactsByTarget.ContainsKey(impact.Target))
                    ImpactsByTarget[impact.Target] = new List<ImpactData>();
                ImpactsByTarget[impact.Target].Add(impact);
            }
        }
        
        /// <summary>
        /// Record an invalidated action.
        /// </summary>
        public void RecordInvalidatedAction(IRootAction action)
        {
            InvalidatedActions.Add(action);
        }
        
        /// <summary>
        /// Record a tag worker activation.
        /// </summary>
        public void RecordTagWorkerActivated(AbstractTagWorker worker)
        {
            ActivatedTagWorkers.Add(worker);
        }
        
        /// <summary>
        /// Record a tag worker resolution.
        /// </summary>
        public void RecordTagWorkerResolved(AbstractTagWorker worker)
        {
            ResolvedTagWorkers.Add(worker);
        }
        
        /// <summary>
        /// Record an effect application.
        /// </summary>
        public void RecordEffectApplied(GameplayEffectSpec spec)
        {
            AppliedEffects.Add(spec);
        }
        
        /// <summary>
        /// Record an effect removal.
        /// </summary>
        public void RecordEffectRemoved(GameplayEffect effect)
        {
            RemovedEffects.Add(effect);
        }
        
        public void RecordDeath(IGameplayAbilitySystem system)
        {
            Deaths.Add(system);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // QUERY HELPERS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Get total negative impact (damage) to an attribute this frame.
        /// </summary>
        public float GetTotalDamage(Attribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts))
                return 0f;
            
            return impacts
                .Where(i => i.RealImpact.CurrentValue < 0)
                .Sum(i => -i.RealImpact.CurrentValue); // Return as positive value
        }
        
        /// <summary>
        /// Get total positive impact (healing) to an attribute this frame.
        /// </summary>
        public float GetTotalHealing(Attribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts))
                return 0f;
            
            return impacts
                .Where(i => i.RealImpact.CurrentValue > 0)
                .Sum(i => i.RealImpact.CurrentValue);
        }
        
        /// <summary>
        /// Get net impact to an attribute this frame.
        /// </summary>
        public float GetNetImpact(Attribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts))
                return 0f;
            
            return impacts.Sum(i => i.RealImpact.CurrentValue);
        }
        
        /// <summary>
        /// Get all impacts from a specific source to a specific target.
        /// </summary>
        public IEnumerable<ImpactData> GetImpactsBetween(IGameplayAbilitySystem source, ITarget target)
        {
            if (!ImpactsBySource.TryGetValue(source, out var impacts))
                return Enumerable.Empty<ImpactData>();
            
            return impacts.Where(i => i.Target != null && i.Target.Equals(target));
        }
        
        /// <summary>
        /// Get total damage dealt by a source this frame.
        /// </summary>
        public float GetTotalDamageDealt(IGameplayAbilitySystem source, Attribute attribute = null)
        {
            if (!ImpactsBySource.TryGetValue(source, out var impacts))
                return 0f;
            
            var filtered = impacts.Where(i => i.RealImpact.CurrentValue < 0);
            if (attribute != null)
                filtered = filtered.Where(i => i.Attribute.Equals(attribute));
            
            return filtered.Sum(i => -i.RealImpact.CurrentValue);
        }
        
        /// <summary>
        /// Get total damage received by a target this frame.
        /// </summary>
        public float GetTotalDamageReceived(ITarget target, Attribute attribute = null)
        {
            if (!ImpactsByTarget.TryGetValue(target, out var impacts))
                return 0f;
            
            var filtered = impacts.Where(i => i.RealImpact.CurrentValue < 0);
            if (attribute != null)
                filtered = filtered.Where(i => i.Attribute.Equals(attribute));
            
            return filtered.Sum(i => -i.RealImpact.CurrentValue);
        }
        
        /// <summary>
        /// Check if an entity took any damage this frame.
        /// </summary>
        public bool TookDamage(ITarget target, Attribute attribute = null)
        {
            if (!ImpactsByTarget.TryGetValue(target, out var impacts))
                return false;
            
            return impacts.Any(i => 
                i.RealImpact.CurrentValue < 0 && 
                (attribute == null || i.Attribute.Equals(attribute)));
        }
        
        /// <summary>
        /// Get all unique sources that damaged a target this frame.
        /// </summary>
        public IEnumerable<IGameplayAbilitySystem> GetDamageSources(ITarget target)
        {
            if (!ImpactsByTarget.TryGetValue(target, out var impacts))
                return Enumerable.Empty<IGameplayAbilitySystem>();
            
            return impacts
                .Where(i => i.RealImpact.CurrentValue < 0)
                .Select(i => i.SourcedModifier.BaseDerivation?.GetSource() as IGameplayAbilitySystem)
                .Where(s => s != null)
                .Distinct();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Clear all recorded data for the next frame.
        /// </summary>
        public void Clear()
        {
            Impacts.Clear();
            ImpactsByAttribute.Clear();
            ImpactsBySource.Clear();
            ImpactsByTarget.Clear();
            InvalidatedActions.Clear();
            ActivatedTagWorkers.Clear();
            ResolvedTagWorkers.Clear();
            AppliedEffects.Clear();
            RemovedEffects.Clear();
            Deaths.Clear();
        }
        
        /// <summary>
        /// Create a snapshot copy of the current state.
        /// </summary>
        public FrameSummarySnapshot CreateSnapshot(int executed)
        {
            return new FrameSummarySnapshot(this, executed);
        }
    }
    
    /// <summary>
    /// Immutable snapshot of a FrameSummary for passing to callbacks.
    /// </summary>
    public class FrameSummarySnapshot
    {
        public IReadOnlyList<ImpactData> Impacts { get; }
        public IReadOnlyList<IRootAction> InvalidatedActions { get; }
        public IReadOnlyList<AbstractTagWorker> ActivatedTagWorkers { get; }
        public IReadOnlyList<AbstractTagWorker> ResolvedTagWorkers { get; }
        
        public int Executed { get; private set; }
        public int Invalidated { get; private set; }
        
        public FrameSummarySnapshot(FrameSummary summary, int executed)
        {
            Impacts = summary.Impacts.ToList();
            InvalidatedActions = summary.InvalidatedActions.ToList();
            ActivatedTagWorkers = summary.ActivatedTagWorkers.ToList();
            ResolvedTagWorkers = summary.ResolvedTagWorkers.ToList();

            Executed = executed;
            Invalidated = summary.InvalidatedActions.Count;
        }
    }
}