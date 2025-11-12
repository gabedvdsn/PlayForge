using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class AttributeChangeEventGroup : AbstractAttributeChangeEvent
    {
        public List<AbstractAttributeChangeEvent> ChangeEvents;
        
        public override void AttributeChangeEvent(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            foreach (AbstractAttributeChangeEvent fEvent in ChangeEvents)
            {
                if (!fEvent.ValidateWorkFor(system, attributeCache, change)) continue;
                fEvent.AttributeChangeEvent(system, attributeCache, change);
            }
        }

        public override bool ValidateWorkFor(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            foreach (var changeEvent in ChangeEvents)
            {
                // If any of the events are valid return true
                if (changeEvent.ValidateWorkFor(system, attributeCache, change)) return true;
            }

            return false;
        }

        public override bool RegisterWithHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return ChangeEvents.Any(changeEvent => changeEvent.RegisterWithHandler(preChange, postChange));
        }
        public override bool DeRegisterFromHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return ChangeEvents.Any(changeEvent => changeEvent.DeRegisterFromHandler(preChange, postChange));
        }
    }
}
