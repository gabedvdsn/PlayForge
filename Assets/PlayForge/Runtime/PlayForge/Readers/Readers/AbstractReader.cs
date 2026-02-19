using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractReader : LazyMonoProcess
    {
        [Header("Reader")]
        
        protected IGameplayAbilitySystem Source;

        public void Assign(IGameplayAbilitySystem gas)
        {
            Source = gas;

            //if (Source is null || Policy != EReaderPolicy.OnChange) return;
            
            SubscribeIfOnChangePolicy();
        }

        protected abstract void SubscribeIfOnChangePolicy();
    }
}
