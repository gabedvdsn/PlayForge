using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class RunSequenceTask : AbstractAbilityTask
    {
        public TaskSequenceReference SequenceReference;

        public override bool IsCriticalSection => SequenceReference.GetActiveSequence().IsCriticalSection;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (SequenceReference.GetActiveSequence() is null) return;
            
            var seqData = new SequenceDataPacket(data);
            await SequenceReference.GetActiveSequence().Run(seqData, token);
        }
    }
    
    public class RunSequenceExampleTask : AbstractAbilityTask
    {
        public TaskSequenceReference SequenceReference;

        public override bool IsCriticalSection => SequenceReference.GetActiveSequence().IsCriticalSection;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (SequenceReference.GetActiveSequence() is null) return;
            
            var seqData = new SequenceDataPacket(data);
            await SequenceReference.GetActiveSequence().Run(seqData, token);
        }
    }
}
