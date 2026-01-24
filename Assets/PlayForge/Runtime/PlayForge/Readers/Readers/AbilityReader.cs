using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilityReader : AbstractReader, ITagReader
    {
        [Header("Ability Reader")] 
        
        private Tag target;
        
        public override void WhenUpdate(ProcessRelay relay)
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
