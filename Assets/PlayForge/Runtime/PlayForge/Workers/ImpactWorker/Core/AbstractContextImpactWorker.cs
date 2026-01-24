using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Impact worker with standard context validation options.
    /// Most impact workers should inherit from this class.
    /// </summary>
    [Serializable]
    public abstract class AbstractContextImpactWorker : AbstractImpactWorker
    {
        [Header("Impact Context")]
        [Tooltip("The attribute that must be impacted")]
        public Attribute ImpactedAttribute;
        
        [Tooltip("Accept impacts from any impact type")]
        public bool AnyImpactType = true;
        
        [Tooltip("The impact types to accept (if not any)")]
        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactType;
        
        [Tooltip("Which part of the attribute was impacted")]
        public EEffectImpactTargetExpanded ImpactTarget;
        
        [Tooltip("Require exact match for impact target (e.g., Current only, not CurrentAndBase)")]
        public bool ImpactTargetExclusive;
        
        [Tooltip("The sign of the impact")]
        public ESignPolicy ImpactSign;
        
        [Tooltip("Allow impacts where source and target are the same")]
        public bool AllowSelfImpact;
        
        [Space(5)]
        
        [Tooltip("Accept impacts from any context")]
        public bool AnyContextTag;
        
        [Tooltip("The context tags the impact source must have (if not any)")]
        public List<Tag> ImpactAbilityContextTags;
        
        [Header("Work Context")]
        [Tooltip("The attribute to apply work on")]
        public Attribute WorkAttribute;
        
        [Tooltip("The impact type for the work output")]
        [ForgeTagContext(ForgeContext.Impact)]
        public Tag WorkImpactType;
        
        [Tooltip("The sign policy for the work output")]
        public ESignPolicy WorkSignPolicy;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VALIDATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool PreValidateWorkFor(ImpactData impactData)
        {
            return impactData.Attribute.Equals(ImpactedAttribute);
        }
        
        public override bool ValidateWorkFor(ImpactData impactData)
        {
            return ForgeHelper.ValidateContextTags(AnyContextTag, ImpactAbilityContextTags,
                       impactData.SourcedModifier.Derivation.GetContextTags())
                   && ForgeHelper.ValidateSelfModification(AllowSelfImpact,
                       impactData.SourcedModifier.Derivation.GetSource(), impactData.Target.AsGAS())
                   && ForgeHelper.ValidateImpactTypes(AnyImpactType, 
                       impactData.SourcedModifier.Derivation.GetImpactTypes(), ImpactType)
                   && ForgeHelper.ValidateImpactTargets(ImpactTarget, impactData.RealImpact, ImpactTargetExclusive)
                   && ForgeHelper.ValidateSignPolicy(ImpactSign, ImpactTarget, impactData.RealImpact);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CACHE REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override void SubscribeToCache(ImpactWorkerCache cache)
        {
            cache.ProvideWorker(ImpactedAttribute, this);
        }
        
        public override void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            cache.RemoveWorker(ImpactedAttribute, this);
        }
    }
}