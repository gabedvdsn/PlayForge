using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of a mono process that shows a GAS's state: <c>Assign</c> gives it the GAS and calls
    /// <c>SubscribeIfOnChangePolicy</c>, which a reader overrides to hear the changes it shows.
    /// </summary>
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
