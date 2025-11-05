using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractReader : LazyMonoProcess, ISystemReader
    {
        [Header("Reader")]
        
        public EReaderPolicy Policy;
        protected GASComponent Source;

        public void Assign(GASComponent gas)
        {
            Source = gas;

            if (!Source || Policy != EReaderPolicy.OnChange) return;
            
            SubscribeIfOnChangePolicy();
        }

        protected abstract void SubscribeIfOnChangePolicy();
    }
}
