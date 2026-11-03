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
        [Tooltip("Inline executes immediately, Deferred queues for end-of-frame. InlineAndDeferred runs deferred, as " +
                 "both halves do the same work")]
        public EWorkerExecution ExecutionMode = EWorkerExecution.Deferred;
        
        [Header("Danger! [ KEEP FALSE ]")]
        [Tooltip("Only set TRUE if you know what you are doing. FALSE breaks infinite cycles.")]
        public bool WorkerImpactWorkable = false;
        
        // Both halves do the same work, so InlineAndDeferred runs deferred, the safe default. It did the work twice, and
        // lifesteal healed double (GAS-41)
        public override EWorkerExecution Execution =>
            ExecutionMode == EWorkerExecution.InlineAndDeferred ? EWorkerExecution.Deferred : ExecutionMode;
        
        public override void ActivateInline(ImpactData impactData)
        {
            // For inline execution
            PerformWork(impactData);
        }
        
        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            // For deferred execution
            var impactData = ctx.ImpactData;
            
            if (!GetWrtTarget(ctx.ImpactData, out var relValue)) yield break;
            
            var attributeValue = Operation switch
            {
                ECalculationOperation.Add => relValue + impactData.RealImpact,
                ECalculationOperation.Multiply => relValue * impactData.RealImpact,
                ECalculationOperation.Override => relValue,
                ECalculationOperation.FlatBonus => relValue + impactData.RealImpact,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            attributeValue = ForgeHelper.AlignToSign(attributeValue, WorkSignPolicy);
            
            var sourcedModifier = new SourcedModifiedAttributeValue(
                IAttributeImpactCarrier.GenerateSourceDerivation(
                    impactData.SourcedModifier, 
                    Tags.IgnoreRetention, WorkImpactType),
                attributeValue.CurrentValue, 
                attributeValue.BaseValue,
                WorkerImpactWorkable
            );
            
            yield return new ModifyAttributeAction(ctx.ImpactData.SourcedModifier.Source, WorkAttribute, sourcedModifier);
        }
        
        private void PerformWork(ImpactData impactData)
        {
            if (!GetWrtTarget(impactData, out var relValue)) return;
            
            var attributeValue = Operation switch
            {
                ECalculationOperation.Add => relValue + impactData.RealImpact,
                ECalculationOperation.Multiply => relValue * impactData.RealImpact,
                ECalculationOperation.Override => relValue,
                ECalculationOperation.FlatBonus => relValue + impactData.RealImpact,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            attributeValue = ForgeHelper.AlignToSign(attributeValue, WorkSignPolicy);
            
            var sourcedModifier = new SourcedModifiedAttributeValue(
                IAttributeImpactCarrier.GenerateSourceDerivation(
                    impactData.SourcedModifier, 
                    Tags.IgnoreRetention, WorkImpactType),
                attributeValue.CurrentValue, 
                attributeValue.BaseValue,
                WorkerImpactWorkable
            );
            
            impactData.SourcedModifier.Source.TryModifyAttribute(WorkAttribute, sourcedModifier);
        }
        
        private bool GetWrtTarget(ImpactData impactData, out AttributeValue value)
        {
            value = default;
            return WithRespectTo switch
            {
                ESourceTarget.Source => impactData.SourcedModifier.Source.TryGetAttributeValue(impactData.Attribute, out value),
                ESourceTarget.Target => impactData.Target.TryGetAttributeValue(impactData.Attribute, out value),
                _ => false
            };
        }
        
        public override bool ValidateWorkFor(ImpactData impactData)
        {
            if (!base.ValidateWorkFor(impactData)) return false;
            
            return WithRespectTo switch
            {
                ESourceTarget.Target => impactData.Target.FindAttributeSystem(out var attr) && 
                                        attr.DefinesAttribute(RelativeAttribute),
                ESourceTarget.Source => impactData.SourcedModifier.Source.FindAttributeSystem(out var attr) && 
                                        attr.DefinesAttribute(RelativeAttribute),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}