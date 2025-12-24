using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractFocusedAttributeWorker : AbstractAttributeWorker
    {
        [Header("Change Event")]
        
        public EChangeEventTiming Timing;

        [Header("Change Attribute Validation")] 
        
        [Tooltip("The attribute to screen for")]
        public Attribute TargetAttribute;
        [Tooltip("Include attribute changes where the source and target are the same")]
        public bool AllowSelfModification;
        [Tooltip("The change in attribute type to screen for")]
        public EEffectImpactTargetExpanded TargetModification;
        [Tooltip("Exclude changes that do not exactly meet the target modification type\nE.g. Current when CurrentAndBase")]
        public bool ExclusivelyTargetModification;
        
        
        [Space(5)]
        
        [Tooltip("The sign of the change to screen for")]
        public ESignPolicyExtended SignPolicy;
        
        [Tooltip("The impact type of the change to screen for")]
        [ForgeCategory(Forge.Categories.ImpactType)]
        public List<Tag> ImpactType;
        
        [Header("Change Tag Validation")]
        
        [Tooltip("Allow changes deriving from any context")]
        public bool AnyContextTag = true;
        [Tooltip("The modification source context tag(s) (all of them) must exist in this list")]
        public List<Tag> ValidContextTags;

        public override bool PreValidateWorkFor(ChangeValue change)
        {
            return change.Value.BaseDerivation.GetAttribute().Equals(TargetAttribute);
        }

        public override bool ValidateWorkFor(IGameplayAbilitySystem system,
            Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            if (!change.Value.BaseDerivation.GetAttribute().Equals(TargetAttribute))
            {
                return false;
            }
            
            if (!ForgeHelper.ValidateContextTags(AnyContextTag, ValidContextTags,
                    change.Value.BaseDerivation.GetContextTags()))
            {
                return false;
            }

            if (!ForgeHelper.ValidateSelfModification(AllowSelfModification, change.Value.BaseDerivation.GetSource(),
                    system))
            {
                return false;
            }
            if (!ForgeHelper.ValidateImpactTargets(TargetModification, change.Value.ToAttributeValue(), ExclusivelyTargetModification)) 
            {
                return false;
            }
            if (!ForgeHelper.ValidateSignPolicy(SignPolicy, TargetModification, change.Value.ToAttributeValue())) 
            {
                return false;
            }
            if (!ForgeHelper.ValidateImpactTypes(change.Value.BaseDerivation.GetImpactTypes(), ImpactType)) 
            {
                return false;
            }
            return true;
        }

        public override bool RegisterWithHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return Timing switch
            {
                EChangeEventTiming.PreChange => preChange.AddEvent(TargetAttribute, this),
                EChangeEventTiming.PostChange => postChange.AddEvent(TargetAttribute, this),
                EChangeEventTiming.Both => preChange.AddEvent(TargetAttribute, this) || postChange.AddEvent(TargetAttribute, this),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override bool DeRegisterFromHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return Timing switch
            {
                EChangeEventTiming.PreChange => preChange.RemoveEvent(TargetAttribute, this),
                EChangeEventTiming.PostChange => postChange.RemoveEvent(TargetAttribute, this),
                EChangeEventTiming.Both => preChange.RemoveEvent(TargetAttribute, this) || postChange.RemoveEvent(TargetAttribute, this),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
