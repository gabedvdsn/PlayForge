using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;

namespace FarEmerald.PlayForge
{
    public interface IActiveSequence
    {
        public UniTask Run(ProcessDataPacket data, CancellationToken externalToken = default);
        public UniTask<bool> TryRun(ProcessDataPacket data, CancellationToken token = default);
        public bool IsCriticalSection { get; }
    }
}
