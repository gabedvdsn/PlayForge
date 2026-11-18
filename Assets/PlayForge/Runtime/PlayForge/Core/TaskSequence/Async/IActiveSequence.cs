using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A sequence as code runs it, a <c>TaskSequence</c> or a <c>TaskSequenceChain</c>: <c>Run</c> runs it with a data
    /// packet, <c>TryRun</c> also says whether it finished, and <c>IsCriticalSection</c> whether its run is in a
    /// critical stage now. A <c>TaskSequenceReference</c>'s <c>GetActiveSequence</c> gives one.
    /// </summary>
    public interface IActiveSequence
    {
        public UniTask Run(ProcessDataPacket data, CancellationToken externalToken = default);
        public UniTask<bool> TryRun(ProcessDataPacket data, CancellationToken token = default);
        public bool IsCriticalSection { get; }
    }
}
