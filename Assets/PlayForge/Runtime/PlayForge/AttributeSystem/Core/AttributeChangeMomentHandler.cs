using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttributeChangeMomentHandler
    {
        public Dictionary<Attribute, List<AbstractAttributeWorker>> ChangeEvents = new();

        public bool AddEvent(Attribute attribute, AbstractAttributeWorker worker)
        {
            if (ChangeEvents.ContainsKey(attribute))
            {
                if (ChangeEvents[attribute].Contains(worker)) return false;
                ChangeEvents[attribute].Add(worker);
            }
            else ChangeEvents[attribute] = new List<AbstractAttributeWorker>() { worker };
                
            return true;
        }
            
        public bool RemoveEvent(Attribute attribute, AbstractAttributeWorker worker)
        {
            if (!ChangeEvents.ContainsKey(attribute)) return false;
                
            ChangeEvents[attribute].Remove(worker);
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
                if (!fEvent.PreValidateWorkFor(change)) continue;
                if (!fEvent.ValidateWorkFor(system, attributeCache, change)) continue;
                fEvent.Activate(system, attributeCache, change);
            }
        }
    }
}
