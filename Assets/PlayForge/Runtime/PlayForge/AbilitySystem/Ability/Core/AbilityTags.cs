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
        [ForgeTagContext(ForgeContext.AssetTag)]
        public Tag AssetTag;
        [ForgeTagContext(ForgeContext.ContextTag)]
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

        /// <summary>
        /// Whether an ability is tagged with one of the tags, or a child of one, by its asset tag or a context tag, as
        /// a stage's Cancelled By and Blocks name abilities (FEAT-5).
        /// </summary>
        public static bool Names(List<Tag> tags, Ability ability)
        {
            if (tags == null || tags.Count == 0 || ability == null || ability.Tags == null) return false;
            var own = ability.Tags.ContextTags;
            for (int i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (!tag.IsValid) continue;
                if (ability.Tags.AssetTag.MatchesOrIsChildOf(tag)) return true;
                if (own == null) continue;
                for (int j = 0; j < own.Count; j++)
                    if (own[j].MatchesOrIsChildOf(tag)) return true;
            }
            return false;
        }
    }

}