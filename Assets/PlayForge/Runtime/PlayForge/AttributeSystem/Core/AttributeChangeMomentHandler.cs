using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttributeChangeMomentHandler
    {
        public Dictionary<Attribute, List<AbstractAttributeChangeEvent>> ChangeEvents = new();

        public bool AddEvent(Attribute attribute, AbstractAttributeChangeEvent changeEvent)
        {
            if (ChangeEvents.ContainsKey(attribute))
            {
                if (ChangeEvents[attribute].Contains(changeEvent)) return false;
                ChangeEvents[attribute].Add(changeEvent);
            }
            else ChangeEvents[attribute] = new List<AbstractAttributeChangeEvent>() { changeEvent };
                
            return true;
        }
            
        public bool RemoveEvent(Attribute attribute, AbstractAttributeChangeEvent changeEvent)
        {
            if (!ChangeEvents.ContainsKey(attribute)) return false;
                
            ChangeEvents[attribute].Remove(changeEvent);
            if (ChangeEvents[attribute].Count == 0)
            {
                ChangeEvents.Remove(attribute);
            }
                
            return true;
        }
            
        public void RunEvents(Attribute attribute, GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            if (!ChangeEvents.ContainsKey(attribute)) return;
            foreach (var fEvent in ChangeEvents[attribute])
            {
                if (!fEvent.ValidateWorkFor(system, attributeCache, change)) continue;
                fEvent.AttributeChangeEvent(system, attributeCache, change);
            }
        }
    }
}
