using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class AbstractProxyTaskBehaviour
    {
        protected Dictionary<IProxyTaskBehaviourUser, EActionStatus> status = new();

        public abstract Tag Command { get; }
        public abstract UniTask RunAsync(IProxyTaskBehaviourCaller caller, IProxyTaskBehaviourUser user, CancellationToken token);
        public abstract EActionStatus End();
        public virtual EActionStatus Status(IProxyTaskBehaviourUser user)
        {
            return status.TryGetValue(user, out var s) ? s : EActionStatus.NoData;
        }
        public virtual void SetStatus(IProxyTaskBehaviourUser user, EActionStatus s)
        {
            status[user] = s;
        }
        
        public abstract AbstractProxyTaskBehaviour CreateInstance();
    }
}
