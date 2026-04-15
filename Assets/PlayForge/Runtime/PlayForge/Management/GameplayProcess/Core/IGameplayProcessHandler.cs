using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface IGameplayProcessHandler : IHasReadableDefinition
    {
        public ProcessRelay[] GetRelays();
        
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler);

        public bool HandlerProcessIsSubscribed(ProcessRelay relay);

        public void HandlerSubscribeProcess(ProcessRelay relay);

        public bool HandlerVoidProcess(ProcessRelay relay);

        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono);
    }

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
