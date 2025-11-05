using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractCompositeBehaviour
    {
        protected Dictionary<ICompositeBehaviourUser, EActionStatus> status = new();
        
        public abstract UniTask Run(CancellationToken token);
        public abstract EActionStatus End();
        public virtual EActionStatus Status(ICompositeBehaviourUser user)
        {
            return status.TryGetValue(user, out var s) ? s : EActionStatus.NoData;
        }
        public virtual void SetStatus(ICompositeBehaviourUser user, EActionStatus s)
        {
            status[user] = s;
        }
        public abstract AbstractCompositeBehaviour CreateInstance();
    }
}
