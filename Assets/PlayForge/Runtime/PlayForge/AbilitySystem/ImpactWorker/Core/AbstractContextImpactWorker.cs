using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractContextImpactWorker : AbstractImpactWorker
    {
        [Header("Impact Context")]
        
        [Tooltip("The attribute must be the target attribute in the impact")]
        public Attribute ImpactedAttribute;

        public bool AnyImpactType = true;
        [ForgeCategory(Forge.Categories.ImpactType)]
        public List<Tag> ImpactType;
        public EEffectImpactTargetExpanded ImpactTarget;
        [Tooltip("Validate that exclusively the modify target is modified, as opposed to itself AND the alternative (e.g. target is Current when Current AND Base are modified would NOT pass validation.")]
        public bool ImpactTargetExclusive;
        public ESignPolicy ImpactSign;
        public bool AllowSelfImpact;
        
        [Space(5)]
        
        public bool AnyContextTag;
        [Tooltip("The sourced ability context tags (all of them) must exist in this list")]
        public List<Tag> ImpactAbilityContextTags;
        
        [Header("Work Context")] 
        
        [Tooltip("The attribute to apply work on")]
        public Attribute WorkAttribute;
        
        [ForgeCategory(Forge.Categories.ImpactType)]
        public Tag WorkImpactType;
        public ESignPolicy WorkSignPolicy;

        public override bool ValidateWorkFor(AbilityImpactData impactData)
        {
            return ForgeHelper.ValidateContextTags(AnyContextTag, ImpactAbilityContextTags,
                    impactData.SourcedModifier.Derivation.GetContextTags())
                   && ForgeHelper.ValidateSelfModification(AllowSelfImpact,
                       impactData.SourcedModifier.Derivation.GetSource(), impactData.Target.AsGAS())
                   && ForgeHelper.ValidateImpactTypes(AnyImpactType, impactData.SourcedModifier.Derivation.GetImpactTypes(), ImpactType)
                   && ForgeHelper.ValidateImpactTargets(ImpactTarget, impactData.RealImpact, ImpactTargetExclusive)
                   && ForgeHelper.ValidateSignPolicy(ImpactSign, ImpactTarget, impactData.RealImpact);

        }

        public override bool PreValidateWorkFor(AbilityImpactData impactData)
        {
            return impactData.Attribute.Equals(ImpactedAttribute);
        }

        public override void SubscribeToCache(ImpactWorkerCache cache)
        {
            cache.AddWorker(ImpactedAttribute, this);
        }

        public override void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            cache.RemoveWorker(ImpactedAttribute, this);
        }
    }
    
}
