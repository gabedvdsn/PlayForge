using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AbilityTags
    {
        [Header("Base")] 
        
        [ForgeCategory(Forge.Categories.Identifier)]
        public Tag AssetTag;
        public Tag[] ContextTags;
        
        [Header("Tags")]
        
        [Tooltip("Tags that are granted as long as this ability is learned")]
        public Tag[] PassivelyGrantedTags;
        [Tooltip("Tags that are granted while this ability is active")]
        public Tag[] ActiveGrantedTags;

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
