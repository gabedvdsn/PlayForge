using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AbilityTags
    {
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        [ForgeTagContext(ForgeContext.ContextIdentifier)]
        public List<Tag> ContextTags;
        
        [ForgeTagContext(ForgeContext.Granted)]
        [Tooltip("Tags that are granted as long as this ability is learned")]
        public List<Tag> PassiveGrantedTags;
        
        [ForgeTagContext(ForgeContext.Granted)]
        [Tooltip("Tags that are granted while this ability is active")]
        public List<Tag> ActiveGrantedTags;

        [Header("Requirements")] 
        [Tooltip("Tags requirements to activate this ability.")]
        public AbilityTagRequirements TagRequirements;

        public bool ValidateSourceRequirements(ITarget source)
        {
            return TagRequirements.SourceRequirements.Validate(source.GetAppliedTags());
        }

        public bool ValidateTargetRequirements(ITarget target)
        {
            return TagRequirements.TargetRequirements.Validate(target.GetAppliedTags());
        }
    }

}