using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Attribute worker focused on a specific target attribute with standard validation options.
    /// Most attribute workers should inherit from this class.
    /// </summary>
    [Serializable]
    public abstract class AbstractFocusedAttributeWorker : AbstractAttributeWorker
    {
        [Header("Timing")]
        [Tooltip("When this worker executes in the modification pipeline")]
        public EChangeEventTiming TimingConfig = EChangeEventTiming.PostChange;
        
        public override EChangeEventTiming Timing => TimingConfig;
        
        [Header("Target Attribute")]
        [Tooltip("The attribute to monitor for changes")]
        public Attribute TargetAttribute;
        
        [Header("Change Validation")]
        [Tooltip("Include changes where the source and target are the same entity")]
        public bool AllowSelfModification;
        
        [Tooltip("The type of modification to monitor (Current, Base, or both)")]
        public EEffectImpactTargetExpanded TargetModification = EEffectImpactTargetExpanded.CurrentOrBase;
        
        [Tooltip("Exclude changes that don't exactly match the modification type")]
        public bool ExclusivelyTargetModification;
        
        [Space(5)]
        [Tooltip("The sign of the change to monitor")]
        public ESignPolicyExtended SignPolicy = ESignPolicyExtended.Any;
        
        [Space(5)]
        [Tooltip("Accept changes from any impact type")]
        public bool AnyImpactType = true;
        
        [Tooltip("The impact types to accept (if not any)")]
        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes;
        
        [Header("Context Validation")]
        [Tooltip("Accept changes from any context")]
        public bool AnyContextTag = true;
        
        [Tooltip("The context tags to accept (if not any)")]
        [ForgeTagContext(ForgeContext.ContextIdentifier)]
        public List<Tag> ValidContextTags;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VALIDATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool PreValidateWorkFor(ChangeValue change)
        {
            // Fast check: is this the right attribute?
            return change.Value.BaseDerivation.GetAttribute().Equals(TargetAttribute);
        }
        
        public override bool ValidateWorkFor(WorkerContext ctx)
        {
            var change = ctx.Change;
            var system = ctx.System;
            
            // Attribute check
            if (!change.Value.BaseDerivation.GetAttribute().Equals(TargetAttribute))
                return false;
            
            // Context tags check
            if (!ForgeHelper.ValidateContextTags(AnyContextTag, ValidContextTags,
                change.Value.BaseDerivation.GetContextTags()))
                return false;
            
            // Self-modification check
            if (!ForgeHelper.ValidateSelfModification(AllowSelfModification,
                change.Value.BaseDerivation.GetSource(), system))
                return false;
            
            // Impact target check (Current, Base, Both)
            if (!ForgeHelper.ValidateImpactTargets(TargetModification,
                change.Value.ToAttributeValue(), ExclusivelyTargetModification))
                return false;
            
            // Sign policy check
            if (!ForgeHelper.ValidateSignPolicy(SignPolicy, TargetModification, change.Value.ToAttributeValue())) return false;
            
            // Impact type check
            if (!ForgeHelper.ValidateImpactTypes(AnyImpactType,
                change.Value.BaseDerivation.GetImpactTypes(), ImpactTypes))
                return false;
            
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool RegisterWithHandler(
            AttributeChangeMomentHandler preChange, 
            AttributeChangeMomentHandler postChange)
        {
            return Timing switch
            {
                EChangeEventTiming.PreChange => preChange.AddWorker(TargetAttribute, this),
                EChangeEventTiming.PostChange => postChange.AddWorker(TargetAttribute, this),
                EChangeEventTiming.Both => preChange.AddWorker(TargetAttribute, this) | 
                                          postChange.AddWorker(TargetAttribute, this),
                _ => false
            };
        }
        
        public override bool DeRegisterFromHandler(
            AttributeChangeMomentHandler preChange, 
            AttributeChangeMomentHandler postChange)
        {
            return Timing switch
            {
                EChangeEventTiming.PreChange => preChange.RemoveWorker(TargetAttribute, this),
                EChangeEventTiming.PostChange => postChange.RemoveWorker(TargetAttribute, this),
                EChangeEventTiming.Both => preChange.RemoveWorker(TargetAttribute, this) | 
                                          postChange.RemoveWorker(TargetAttribute, this),
                _ => false
            };
        }
    }
}