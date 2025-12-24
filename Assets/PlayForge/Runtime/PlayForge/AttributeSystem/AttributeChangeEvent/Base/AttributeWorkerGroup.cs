using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class AttributeWorkerGroup : AbstractAttributeWorker
    {
        [SerializeReference]
        public List<AbstractAttributeWorker> ChangeEvents;
        
        public override void Activate(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            foreach (AbstractAttributeWorker fEvent in ChangeEvents)
            {
                if (!fEvent.ValidateWorkFor(system, attributeCache, change)) continue;
                fEvent.Activate(system, attributeCache, change);
            }
        }

        public override bool PreValidateWorkFor(ChangeValue change)
        {
            foreach (var changeEvent in ChangeEvents)
            {
                if (changeEvent.PreValidateWorkFor(change)) return true;
            }

            return false;
        }

        public override bool ValidateWorkFor(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            return true;
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
