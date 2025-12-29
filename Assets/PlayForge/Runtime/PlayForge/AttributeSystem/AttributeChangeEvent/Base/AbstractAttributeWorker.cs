using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractAttributeWorker : Taggable
    {
        public abstract void Activate(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change);

        public abstract bool PreValidateWorkFor(ChangeValue change);
        
        public abstract bool ValidateWorkFor(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change);

        public abstract bool RegisterWithHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange);

        public abstract bool DeRegisterFromHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange);

        public abstract HashSet<Tag> GetAllTags();
        
        public enum EChangeEventTiming
        {
            PreChange,
            PostChange,
            Both
        }
    }
}
