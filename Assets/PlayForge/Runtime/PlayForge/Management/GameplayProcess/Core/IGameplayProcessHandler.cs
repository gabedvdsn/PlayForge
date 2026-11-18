using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a process is registered under, its handler: what the process belongs to, as a GAS hub, ProcessControl, a
    /// data packet or another process. It keeps the relays of the processes it handles (<c>GetRelays</c>), which
    /// ProcessControl subscribes as each registers and voids as each is removed, and gives the instantiator that makes
    /// the object of a mono process that uses its handler's (<c>GetInstantiator</c>). A process, a hub among them, ends
    /// what it handles as it ends.
    /// </summary>
    public interface IGameplayProcessHandler : IHasReadableDefinition
    {
        public ProcessRelay[] GetRelays();
        
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler);

        public bool HandlerProcessIsSubscribed(ProcessRelay relay);

        public void HandlerSubscribeProcess(ProcessRelay relay);

        public bool HandlerVoidProcess(ProcessRelay relay);

        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono);
    }

    /// <summary>
    /// Extension methods for a process handler: <c>MassDataAction</c> runs an action on the data packet of each process
    /// it handles, and returns false where it handles none.
    /// </summary>
    public static class ProcessHandlerUtil
    {
        public static bool MassDataAction(this IGameplayProcessHandler handler, Action<ProcessDataPacket> action)
        {
            if (handler is null) return false;
            var relays = handler.GetRelays();
            if (relays.Length == 0) return false;

            foreach (var relay in relays) action?.Invoke(relay.Wrapper.Data);

            return true;
        }
    }
}
