using System;
using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Analysis workers run at end-of-frame after all other work is complete.
    /// Used for death checks, state evaluation, and other post-frame analysis.
    /// 
    /// Analysis workers can either:
    /// 1. Return actions to be queued (preferred for deferred execution)
    /// 2. Perform direct actions via the legacy Activate() method
    /// </summary>
    [Serializable]
    public abstract class AbstractAnalysisWorker
    {
        /// <summary>
        /// Analyze the system state and return any actions to queue.
        /// Called after all queued actions and tag workers have processed.
        /// </summary>
        /// <param name="system">The gameplay ability system to analyze</param>
        /// <param name="frameSummary">Summary of all frame activity for context</param>
        /// <returns>Actions to queue, or empty if none</returns>
        public virtual IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            return Enumerable.Empty<IRootAction>();
        }

        protected const string MY_KEY_PREFIX = "Analysis_Worker_";

        // This worker's own, made as it's first asked. It was its type's name, so two workers of a type on one entity,
        // as thresholds at a half and a quarter, shared it, and the first to fire marked the other (GAS-76)
        [NonSerialized] private Tag myKey;
        [NonSerialized] private bool keyMade;
        private static int keys;

        protected Tag MyKey
        {
            get
            {
                if (keyMade) return myKey;
                myKey = Tag.GenerateAsUnique($"{GetType().Name}_{++keys}", MY_KEY_PREFIX);
                keyMade = true;
                return myKey;
            }
        }
        
        protected virtual bool HasTriggered(ILocalDataUser system)
        {
            return system.TryGetLocalData(MyKey, out var _hasTriggered) && _hasTriggered.boolValue;
        }

        protected virtual void SetTriggered(ILocalDataUser system)
        {
            system.SetLocalData(MyKey, new DataWrapper(){ boolValue = true });
        }

        protected virtual void ResetTriggered(ILocalDataUser system)
        {
            if (!system.TryGetLocalData(MyKey, out var data)) return;
            data.boolValue = false;
        }
    }

    [Serializable]
    public class DeathWatcherAnalysisWorker : ThresholdAnalysisWorker
    {
        protected override IEnumerable<IRootAction> OnThresholdReached(IGameplayAbilitySystem system, AttributeValue value, FrameSummary frameSummary)
        {
            yield return new DeathAction(system);
        }
    }
    
    /// <summary>
    /// Analysis worker that triggers callbacks based on attribute thresholds.
    /// </summary>
    [Serializable]
    public class ThresholdAnalysisWorker : AbstractAnalysisWorker
    {
        public Attribute Target;
        public float Threshold;
        public EThresholdComparison Comparison = EThresholdComparison.LessThanOrEqual;
        public bool TriggerOnce = true;
        
        public override IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            if (Target is null) yield break;
            if (TriggerOnce && HasTriggered(system)) yield break;
            if (!system.TryGetAttributeValue(Target, out AttributeValue value)) yield break;
            
            bool conditionMet = Comparison switch
            {
                EThresholdComparison.LessThan => value.CurrentValue < Threshold,
                EThresholdComparison.LessThanOrEqual => value.CurrentValue <= Threshold,
                EThresholdComparison.GreaterThan => value.CurrentValue > Threshold,
                EThresholdComparison.GreaterThanOrEqual => value.CurrentValue >= Threshold,
                EThresholdComparison.Equal => Math.Abs(value.CurrentValue - Threshold) < 0.001f,
                _ => false
            };
            
            if (conditionMet)
            {
                if (TriggerOnce && HasTriggered(system))
                {
                    yield break;
                }
                if (TriggerOnce) SetTriggered(system);
                
                // Return actions from derived implementation
                foreach (var action in OnThresholdReached(system, value, frameSummary))
                {
                    yield return action;
                }
            }
        }
        
        /// <summary>
        /// Override to return actions when threshold is reached.
        /// </summary>
        protected virtual IEnumerable<IRootAction> OnThresholdReached(
            IGameplayAbilitySystem system, 
            AttributeValue value, 
            FrameSummary frameSummary)
        {
            yield return new LogAction($"Threshold reached! {Target}-{Threshold}");
        }
    }
    
    public enum EThresholdComparison
    {
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal
    }
}