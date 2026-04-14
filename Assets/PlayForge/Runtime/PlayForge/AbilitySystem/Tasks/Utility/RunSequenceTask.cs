using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class RunSequenceTask : AbstractAbilityTask
    {
        [FormerlySerializedAs("SequenceReference")] public TaskSequenceReference Sequence;

        public override bool IsCriticalSection => Sequence.GetActiveSequence()?.IsCriticalSection ?? false;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var seq = Sequence.GetActiveSequence();
            if (seq is null) return;

            // Check if the sequence requests synchronous lifecycle via metadata.
            // If so, register it as a TaskSequenceProcess through ProcessControl
            // so it runs via per-frame Step() instead of the async runtime.
            if (seq is TaskSequence taskSeq &&
                taskSeq.Definition.Metadata?.Lifecycle == EProcessLifecycle.Synchronous)
            {
                ProcessControl.Register(taskSeq, data, out var relay);
                await UniTask.WaitWhile(() => relay.ProcessActive, cancellationToken: token);
                return;
            }

            var seqData = new SequenceDataPacket(data);
            await seq.Run(seqData, token);
        }
    }

    public class RunSequenceExampleTask : AbstractAbilityTask
    {
        public TaskSequenceReference SequenceReference;

        public override bool IsCriticalSection => SequenceReference.GetActiveSequence()?.IsCriticalSection ?? false;
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var seq = SequenceReference.GetActiveSequence();
            if (seq is null) return;

            if (seq is TaskSequence taskSeq &&
                taskSeq.Definition.Metadata?.Lifecycle == EProcessLifecycle.Synchronous)
            {
                ProcessControl.Register(taskSeq, data, out var relay);
                await UniTask.WaitWhile(() => relay.ProcessActive, cancellationToken: token);
                return;
            }

            var seqData = new SequenceDataPacket(data);
            await seq.Run(seqData, token);
        }
    }
}
