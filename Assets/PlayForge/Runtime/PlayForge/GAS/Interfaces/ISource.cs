using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface ISource : ITarget, IGameplayProcessHandler
    {
        public List<Tag> GetContextTags();
        public TagCache GetTagCache();
        public ActionQueue GetActionQueue();
        public FrameSummary GetFrameSummary();
        public Tag GetAssetTag();
        public int GetLevel();
        public int GetMaxLevel();
        public void SetLevel(int level);
        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag);
        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags);
    }

    public struct SystemComponentData
    {
        public readonly AbilitySystemComponent AbilitySystem;
        public readonly AttributeSystemComponent AttributeSystem;

        public SystemComponentData(ITarget source)
        {
            source.FindAbilitySystem(out AbilitySystem);
            source.FindAttributeSystem(out AttributeSystem);
        }
    }
}
