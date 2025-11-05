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
            if (target == Tags.NULL) return;

            var cooldown = Source.GetLongestDurationFor(target);
            if (!cooldown.Valid) return;
            
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
