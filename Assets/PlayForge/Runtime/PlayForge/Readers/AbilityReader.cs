using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A reader of an ability's cooldown: <c>Assign(Tag)</c> names the tag, and each Update step it finds the GAS's
    /// longest effect duration under it. It shows nothing of what it finds.
    /// </summary>
    public class AbilityReader : AbstractReader
    {
        [Header("Ability Reader")] 
        
        private Tag target;
        
        public override void WhenUpdate()
        {
            if (target == Tags.NONE) return;

            var cooldown = Source.GetLongestDurationFor(target);
            if (!cooldown.FoundDuration) return;
            
            // ...
        }

        public void Assign(Tag _target)
        {
            target = _target;
        }

        protected override void SubscribeIfOnChangePolicy()
        {
            
        }
    }
}
