using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class PooledMonoProcessInstantiator : AbstractMonoProcessInstantiator
    {
        protected override AbstractMonoProcess PrepareNew(AbstractMonoProcess process, ProcessDataPacket data)
        {
            // Pooling logic
            return null;
        }
        
        protected override AbstractMonoProcess PrepareExisting(AbstractMonoProcess process, ProcessDataPacket data)
        {
            // Pooling logic
            return null;
        }
        
        public override void CleanProcess(AbstractMonoProcess process)
        {
            // Pooling logic
        }
    }
}
