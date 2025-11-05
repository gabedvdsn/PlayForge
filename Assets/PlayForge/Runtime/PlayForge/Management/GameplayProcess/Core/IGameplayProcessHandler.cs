using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface IGameplayProcessHandler
    {
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler);

        public bool HandlerProcessIsSubscribed(ProcessRelay relay);

        public void HandlerSubscribeProcess(ProcessRelay relay);

        public bool HandlerVoidProcess(int processIndex);
    }
}
