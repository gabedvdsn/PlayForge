using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity as the source of what it does: a target that is also an effect origin of its own and a handler of the
    /// processes it runs, with the tag cache, action queue and frame summary its workers use. Every GAS hub is one, and
    /// an effect's source is the entity behind its origin. <c>SetLevel</c> and <c>ModifyLevel</c> change a level it
    /// keeps under a tag, its own under its asset tag, and <c>GetLongestDurationFor</c> reads how long its longest
    /// effect under a tag has left, as a cooldown.
    /// </summary>
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

    /// <summary>
    /// A target's ability system and attribute system, each found once, as its <c>ToGASComponentData</c> gives them; a
    /// gameplay mono process keeps its origin's source's so. Either is null where the target has none.
    /// </summary>
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
