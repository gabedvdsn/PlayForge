using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractReader : LazyMonoProcess, ISystemReader
    {
        [Header("Reader")]
        
        public EReaderPolicy Policy;
        protected IGameplayAbilitySystem Source;

        public void Assign(IGameplayAbilitySystem gas)
        {
            Source = gas;

            if (Source is null || Policy != EReaderPolicy.OnChange) return;
            
            SubscribeIfOnChangePolicy();
        }

        protected abstract void SubscribeIfOnChangePolicy();
    }
}
