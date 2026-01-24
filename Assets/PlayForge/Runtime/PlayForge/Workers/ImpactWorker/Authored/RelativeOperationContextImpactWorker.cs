using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Impact worker that applies effects relative to the impact (e.g., lifesteal).
    /// Can be configured as INLINE (immediate) or DEFERRED (end-of-frame).
    /// </summary>
    public class RelativeOperationContextImpactWorker : AbstractContextImpactWorker
    {
        [Header("Relative & Operation")]
        
        [Tooltip("The attribute to use relative to the impact data")]
        public Attribute RelativeAttribute;
        public ECalculationOperation Operation;
        public ESourceTarget WithRespectTo;
        
        [Header("Execution Mode")]
        [Tooltip("Inline executes immediately, Deferred queues for end-of-frame")]
        public EWorkerExecution ExecutionMode = EWorkerExecution.Deferred;
        
        [Header("Danger! [ KEEP FALSE ]")]
        [Tooltip("Only set TRUE if you know what you are doing. FALSE breaks infinite cycles.")]
        public bool WorkerImpactWorkable = false;
        
        public override EWorkerExecution Execution => ExecutionMode;
        
        public override void Activate(ImpactData impactData)
        {
            // For inline execution
            PerformWork(impactData);
        }
        
        public override IEnumerable<IRootAction> CreateActions(ImpactWorkerContext ctx)
        {
            // For deferred execution
            var impactData = ctx.ImpactData;
            
            // Calculate the work to do
            if (!impactData.SourcedModifier.Derivation.GetSource().FindAttributeSystem(out var attr))
                yield break;
            
            if (!attr.TryGetAttributeValue(RelativeAttribute, out AttributeValue relValue))
                yield break;
            
            AttributeValue attributeValue = Operation switch
            {
                ECalculationOperation.Add => relValue + impactData.RealImpact,
                ECalculationOperation.Multiply => relValue * impactData.RealImpact,
                ECalculationOperation.Override => relValue,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            attributeValue = ForgeHelper.AlignToSign(attributeValue, WorkSignPolicy);
            
            var sourcedModifier = new SourcedModifiedAttributeValue(
                IAttributeImpactDerivation.GenerateSourceDerivation(impactData.SourcedModifier, Tags.SYSTEM, WorkImpactType),
                attributeValue.CurrentValue, 
                attributeValue.BaseValue,
                WorkerImpactWorkable
            );
            
            // Get the target for the modification
            var target = GetWorkTarget(impactData);
            if (target == null) yield break;
            
            yield return new ModifyAttributeAction(target, WorkAttribute, sourcedModifier);
        }
        
        private void PerformWork(ImpactData impactData)
        {
            if (!impactData.SourcedModifier.Derivation.GetSource().FindAttributeSystem(out var attr))
                return;
            
            if (!attr.TryGetAttributeValue(RelativeAttribute, out AttributeValue relValue))
                return;
            
            AttributeValue attributeValue = Operation switch
            {
                ECalculationOperation.Add => relValue + impactData.RealImpact,
                ECalculationOperation.Multiply => relValue * impactData.RealImpact,
                ECalculationOperation.Override => relValue,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            attributeValue = ForgeHelper.AlignToSign(attributeValue, WorkSignPolicy);
            
            var sourcedModifier = new SourcedModifiedAttributeValue(
                IAttributeImpactDerivation.GenerateSourceDerivation(impactData.SourcedModifier, Tags.SYSTEM, WorkImpactType),
                attributeValue.CurrentValue, 
                attributeValue.BaseValue,
                WorkerImpactWorkable
            );
            
            attr.ModifyAttribute(WorkAttribute, sourcedModifier);
        }
        
        private ITarget GetWorkTarget(ImpactData impactData)
        {
            return WithRespectTo switch
            {
                ESourceTarget.Source => impactData.SourcedModifier.Derivation.GetSource(),
                ESourceTarget.Target => impactData.Target,
                _ => null
            };
        }
        
        public override bool ValidateWorkFor(ImpactData impactData)
        {
            if (!base.ValidateWorkFor(impactData)) return false;
            
            return WithRespectTo switch
            {
                ESourceTarget.Target => impactData.Target.FindAttributeSystem(out var attr) && 
                                        attr.DefinesAttribute(RelativeAttribute),
                ESourceTarget.Source => impactData.SourcedModifier.BaseDerivation.GetSource().FindAttributeSystem(out var attr) && 
                                        attr.DefinesAttribute(RelativeAttribute),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}