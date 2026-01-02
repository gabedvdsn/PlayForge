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
        
        [Tooltip("Source requirements to use this ability")]
        public AvoidRequireTagGroup SourceRequirements;
        [Tooltip("Target requirements to use this ability (n/a for non-targeted abilities, e.g. ground cast)")]
        public AvoidRequireTagGroup TargetRequirements;

        public bool ValidateSourceRequirements(ITarget source)
        {
            return SourceRequirements.Validate(source.GetAppliedTags());
        }

        public bool ValidateTargetRequirements(ITarget target)
        {
            return TargetRequirements.Validate(target.GetAppliedTags());
        }
    }
}
