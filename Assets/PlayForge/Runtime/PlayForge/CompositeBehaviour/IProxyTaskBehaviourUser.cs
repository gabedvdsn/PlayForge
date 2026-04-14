using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public interface IProxyTaskBehaviourUser
    {
        public ProcessDataPacket Data { get; }
    }
}
