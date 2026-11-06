using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Selects between different scalers based on a condition.
    /// Useful for abilities that behave differently under certain circumstances.
    /// </summary>
    public class ConditionalScaler : AbstractScaler
    {
        [Tooltip("The condition to evaluate")]
        public EScalerCondition Condition = EScalerCondition.SourceTagCondition;

        [Tooltip("Tag to check for tag-based conditions")]
        [ForgeShowWhen(nameof(Condition), EScalerCondition.SourceTagCondition, EScalerCondition.TargetTagCondition)]
        public TagQuery TagCondition;
        
        [Tooltip("Attribute to check for attribute-based conditions")]
        [ForgeShowWhen(nameof(Condition), EScalerCondition.SourceAttributeThreshold, EScalerCondition.TargetAttributeThreshold)]
        public Attribute CheckAttribute;
        
        [Tooltip("Threshold value for comparison conditions")]
        [ForgeShowWhen(nameof(Condition), EScalerCondition.SourceAttributeThreshold, EScalerCondition.TargetAttributeThreshold,
            EScalerCondition.LevelAbove, EScalerCondition.LevelBelow, EScalerCondition.RelativeLevelAbove)]
        public float ThresholdValue = 50f;
        
        [Tooltip("Comparison operator for threshold conditions")]
        [ForgeShowWhen(nameof(Condition), EScalerCondition.SourceAttributeThreshold, EScalerCondition.TargetAttributeThreshold)]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;
        
        [Tooltip("Scaler to use when condition is TRUE")]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference] 
        public AbstractScaler TrueScaler;
        
        [Tooltip("Scaler to use when condition is FALSE")]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference] 
        public AbstractScaler FalseScaler;
        
        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            TrueScaler?.Initialize(deriv);
            FalseScaler?.Initialize(deriv);
        }
        
        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            bool conditionMet = EvaluateCondition(deriv);
            
            if (conditionMet)
            {
                return TrueScaler?.Evaluate(deriv) ?? EvaluateFromSpec(deriv);
            }
            else
            {
                return FalseScaler?.Evaluate(deriv) ?? 0f;
            }
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        // With no True scaler, a met condition reads its own values per level (ED-51)
        public override EScalerParts Parts => TrueScaler == null ? EScalerParts.All : EScalerParts.None;

        private bool EvaluateCondition(IAttributeImpactCarrier spec)
        {
            return Condition switch
            {
                EScalerCondition.SourceTagCondition => TagCondition.Validate(spec.GetOrigin().GetSource().GetTagCache()),
                EScalerCondition.TargetTagCondition => TagCondition.Validate(spec.GetTarget().GetAppliedTags()),
                EScalerCondition.SourceAttributeThreshold => CheckAttributeThreshold(spec),
                EScalerCondition.TargetAttributeThreshold => CheckAttributeThreshold(spec),
                EScalerCondition.LevelAbove => spec.GetOrigin().GetLevel().CurrentValue > ThresholdValue,
                EScalerCondition.LevelBelow => spec.GetOrigin().GetLevel().CurrentValue < ThresholdValue,
                EScalerCondition.RelativeLevelAbove => spec.GetOrigin().GetLevel().Ratio > ThresholdValue,
                EScalerCondition.Always => true,
                EScalerCondition.Never => false,
                _ => false
            };
        }
        
        private bool CheckAttributeThreshold(IAttributeImpactCarrier spec)
        {
            if (CheckAttribute is null) return false;
            
            if (!spec.GetOrigin().GetSource().TryGetAttributeValue(CheckAttribute, out var attrValue))
            {
                return false;
            }
            
            float value = attrValue.CurrentValue;
            
            return Comparison switch
            {
                EComparisonOperator.LessThan => value < ThresholdValue,
                EComparisonOperator.LessOrEqual => value <= ThresholdValue,
                EComparisonOperator.Equal => Mathf.Approximately(value, ThresholdValue),
                EComparisonOperator.NotEqual => !Mathf.Approximately(value, ThresholdValue),
                EComparisonOperator.GreaterOrEqual => value >= ThresholdValue,
                EComparisonOperator.GreaterThan => value > ThresholdValue,
                _ => false
            };
        }
    }
    
    public enum EScalerCondition
    {
        [Tooltip("Source entity has the specified tag")]
        SourceTagCondition,
        
        [Tooltip("Target entity has the specified tag")]
        TargetTagCondition,
        
        [Tooltip("Source's attribute value meets threshold")]
        SourceAttributeThreshold,
        
        [Tooltip("Target's attribute value meets threshold")]
        TargetAttributeThreshold,
        
        [Tooltip("Effect level is above threshold")]
        LevelAbove,
        
        [Tooltip("Effect level is below threshold")]
        LevelBelow,
        
        [Tooltip("Relative level (0-1) is above threshold")]
        RelativeLevelAbove,
        
        [Tooltip("Always true")]
        Always,
        
        [Tooltip("Always false")]
        Never
    }
}