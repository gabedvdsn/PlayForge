using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface ISource : ITarget, IEffectOrigin, IGameplayProcessHandler
    {
        public TagCache GetTagCache();
        public ActionQueue GetActionQueue();
        public FrameSummary GetFrameSummary();
        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag);
        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags);
        public LevelCallbackStatus SetLevel(Tag key, IntValuePair level);
        public LevelCallbackStatus ModifyLevel(Tag key, IntValuePair delta);
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
