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
        
        // What the worker keeps for its changes as it runs: a carrier for each source it works for, the actions its
        // deferred half queues, back here once the queue has run them, and the list it hands one back in. Each change
        // made a new carrier, and the deferred half an iterator and an action as well (GAS-P9)
        [NonSerialized] private SourceCarriers carriers;
        [NonSerialized] private Stack<ModifyAttributeAction> spareActions;
        [NonSerialized] private IRootAction[] oneAction;

        /// <summary>
        /// For deferred execution. The list it returns is the worker's and holds its next action too, so the queue
        /// takes the action out of it as it's returned.
        /// </summary>
        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            if (!TryWork(ctx.ImpactData, out var sourcedModifier)) return Array.Empty<IRootAction>();

            oneAction ??= new IRootAction[1];
            oneAction[0] = ModifyAttributeAction.Rent(spareActions ??= new Stack<ModifyAttributeAction>(),
                ctx.ImpactData.SourcedModifier.Source, WorkAttribute, sourcedModifier, true);
            return oneAction;
        }

        private void PerformWork(ImpactData impactData)
        {
            if (!TryWork(impactData, out var sourcedModifier)) return;

            impactData.SourcedModifier.Source.TryModifyAttribute(WorkAttribute, sourcedModifier);
        }

        // The change this worker makes for an impact, carried for the impact's source by the carrier kept for it
        private bool TryWork(ImpactData impactData, out SourcedModifiedAttributeValue sourcedModifier)
        {
            sourcedModifier = default;
            if (!GetWrtTarget(impactData, out var relValue)) return false;

            var attributeValue = Operation switch
            {
                ECalculationOperation.Add => relValue + impactData.RealImpact,
                ECalculationOperation.Multiply => relValue * impactData.RealImpact,
                ECalculationOperation.Override => relValue,
                ECalculationOperation.FlatBonus => relValue + impactData.RealImpact,
                _ => throw new ArgumentOutOfRangeException()
            };

            attributeValue = ForgeHelper.AlignToSign(attributeValue, WorkSignPolicy);

            var impacting = impactData.SourcedModifier.Carrier;
            var carrier = (carriers ??= new SourceCarriers()).For(
                impacting.GetOrigin().GetSource(), impacting.GetAttribute(), Tags.IgnoreRetention, WorkImpactType);
            sourcedModifier = new SourcedModifiedAttributeValue(
                carrier,
                attributeValue.CurrentValue,
                attributeValue.BaseValue,
                WorkerImpactWorkable
            );
            return true;
        }
        
        // The value the work is relative to: the Relative Attribute's, on the side With Respect To names. It read the
        // impacted attribute's even with one set, so lifesteal by a Lifesteal share healed the attacker by its own health
        // times the blow (GAS-57). A worker with none set reads the impacted attribute still, as it did
        private bool GetWrtTarget(ImpactData impactData, out AttributeValue value)
        {
            value = default;
            IAttribute relative = RelativeAttribute != null ? RelativeAttribute : impactData.Attribute;
            return WithRespectTo switch
            {
                ESourceTarget.Source => impactData.SourcedModifier.Source.TryGetAttributeValue(relative, out value),
                ESourceTarget.Target => impactData.Target.TryGetAttributeValue(relative, out value),
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