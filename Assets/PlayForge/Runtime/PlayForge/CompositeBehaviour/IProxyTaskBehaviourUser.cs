using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a composite behaviour, an <c>AbstractProxyTaskBehaviour</c>, acts on: something with a process's data
    /// (<c>Data</c>), which the behaviour reads, as a disjoint reads whether a shot aimed at its caster is marked
    /// disjointable. Every <c>IProxyTaskBehaviourCaller</c>, which applies a behaviour to others, is one: the processes
    /// made from <c>AbstractMonoProcess</c> and <c>AbstractRuntimeProcess</c>, and each GAS hub.
    /// </summary>
    public interface IProxyTaskBehaviourUser
    {
        public ProcessDataPacket Data { get; }
    }
}
