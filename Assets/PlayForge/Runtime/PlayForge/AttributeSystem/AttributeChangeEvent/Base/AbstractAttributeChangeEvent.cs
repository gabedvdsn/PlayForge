using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractAttributeChangeEvent
    {
        public abstract void AttributeChangeEvent(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change);

        public abstract bool ValidateWorkFor(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change);

        public abstract bool RegisterWithHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange);

        public abstract bool DeRegisterFromHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange);
        
        public enum EChangeEventTiming
        {
            PreChange,
            PostChange,
            Both
        }
    }
}
