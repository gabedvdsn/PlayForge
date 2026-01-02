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
        public EScalerCondition Condition = EScalerCondition.SourceHasTag;
        
        [Header("Condition Parameters")]
        [Tooltip("Tag to check for tag-based conditions")]
        public Tag RequiredTag;
        [FormerlySerializedAs("TagWeight")] public int RequiredWeight;
        
        [Tooltip("Attribute to check for attribute-based conditions")]
        public Attribute CheckAttribute;
        
        [Tooltip("Threshold value for comparison conditions")]
        public float ThresholdValue = 50f;
        
        [Tooltip("Comparison operator for threshold conditions")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterThan;
        
        [Header("Result Scalers")]
        [Tooltip("Scaler to use when condition is TRUE")]
        [SerializeReference]
        public AbstractScaler TrueScaler;
        
        [Tooltip("Scaler to use when condition is FALSE")]
        [SerializeReference]
        public AbstractScaler FalseScaler;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            TrueScaler?.Initialize(spec);
            FalseScaler?.Initialize(spec);
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            bool conditionMet = EvaluateCondition(spec);
            
            if (conditionMet)
            {
                return TrueScaler?.Evaluate(spec) ?? EvaluateFromSpec(spec);
            }
            else
            {
                return FalseScaler?.Evaluate(spec) ?? 0f;
            }
        }
        
        private bool EvaluateCondition(IAttributeImpactDerivation spec)
        {
            return Condition switch
            {
                EScalerCondition.SourceHasTag => spec.GetSource().GetWeight(RequiredTag) > RequiredWeight,
                EScalerCondition.TargetHasTag => spec.GetTarget().GetWeight(RequiredTag) > 1,
                EScalerCondition.SourceMissingTag => spec.GetSource().GetWeight(RequiredTag) <= 1,
                EScalerCondition.TargetMissingTag => spec.GetTarget().GetWeight(RequiredTag) <= 1,
                EScalerCondition.SourceAttributeThreshold => CheckAttributeThreshold(spec),
                EScalerCondition.TargetAttributeThreshold => CheckAttributeThreshold(spec),
                EScalerCondition.LevelAbove => spec.GetEffectDerivation().GetLevel() > ThresholdValue,
                EScalerCondition.LevelBelow => spec.GetEffectDerivation().GetLevel() < ThresholdValue,
                EScalerCondition.RelativeLevelAbove => spec.GetEffectDerivation().GetRelativeLevel() > ThresholdValue,
                EScalerCondition.Always => true,
                EScalerCondition.Never => false,
                _ => false
            };
        }
        
        private bool CheckAttributeThreshold(IAttributeImpactDerivation spec)
        {
            if (CheckAttribute is null) return false;
            
            if (!spec.GetSource().FindAttributeSystem(out var attrSystem) ||
                !attrSystem.TryGetAttributeValue(CheckAttribute, out AttributeValue attrValue))
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
        SourceHasTag,
        
        [Tooltip("Target entity has the specified tag")]
        TargetHasTag,
        
        [Tooltip("Source entity does NOT have the specified tag")]
        SourceMissingTag,
        
        [Tooltip("Target entity does NOT have the specified tag")]
        TargetMissingTag,
        
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