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
    }
    
    /// <summary>
    /// Built-in analysis worker that watches an attribute and deactivates the entity when it reaches zero.
    /// Common use case: death when health reaches 0.
    /// </summary>
    [Serializable]
    public class AttributeWatcherAnalysisWorker : AbstractAnalysisWorker
    {
        public Attribute Target;
        public bool DeactivateOnZero = true;
        public bool DeactivateOnNegative = true;
        
        public override IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            if (Target == null) yield break;
            if (!system.GetAttributeSystem().TryGetAttributeValue(Target, out AttributeValue value)) yield break;
            
            bool shouldDeactivate = false;
            
            if (DeactivateOnZero && value.CurrentValue == 0)
                shouldDeactivate = true;
            else if (DeactivateOnNegative && value.CurrentValue < 0)
                shouldDeactivate = true;
            
            if (shouldDeactivate)
            {
                // Queue deactivation action
                yield return new LambdaAction(
                    sys => sys.ToGASObject()?.gameObject.SetActive(false),
                    $"Deactivate({Target.Name} <= 0)",
                    ActionPriority.Analysis
                );
            }
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
        
        private bool _hasTriggered = false;
        
        public override IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            if (Target == null) yield break;
            if (TriggerOnce && _hasTriggered) yield break;
            if (!system.GetAttributeSystem().TryGetAttributeValue(Target, out AttributeValue value)) yield break;
            
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
                _hasTriggered = true;
                
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
        
        public void ResetTrigger()
        {
            _hasTriggered = false;
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