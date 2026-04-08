
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointProxyTaskBehaviour : AbstractProxyTaskBehaviour
    {
        public static Tag IS_DISJOINTABLE => Tag.GenerateAsUnique("CB_IS_DISJOINTABLE");
        public static Tag COMMAND => Tag.GenerateAsUnique("CB_DISJOINT");

        public override Tag Command => COMMAND;

        /// <summary>
        /// </summary>
        /// <param name="caller">The target (e.g. a GAS system)</param>
        /// <param name="user">The targeting entity (e.g. homing projectile)</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override UniTask RunAsync(IProxyTaskBehaviourCaller caller, IProxyTaskBehaviourUser user, CancellationToken token)
        {
            if (user is not IHasIntentToTarget userWithIntent)
            {
                SetStatus(user, EActionStatus.Failure);
                return UniTask.CompletedTask;
            }
            
            var data = user.Data;
            if (data.TryGet(IS_DISJOINTABLE, out DataValue<bool> results))
            {
                if (results.ToArray().All(b => !b))
                {
                    SetStatus(user, EActionStatus.Failure);
                    return UniTask.CompletedTask;
                }
            }
            // If user does not indicate disjointable status, noop
            else
            {
                SetStatus(user, EActionStatus.NoOp);
                return UniTask.CompletedTask;
            }
            
            // We expect caller to be a targetable entity (e.g. GAS)
            if (caller is not ITarget _target)
            {
                SetStatus(user, EActionStatus.Error);
                return UniTask.CompletedTask;
            }

            userWithIntent.SetTarget(_target, _target.AsTransform());
            SetStatus(user, EActionStatus.Success);
            
            return UniTask.CompletedTask;
        }
        
        public override EActionStatus End()
        {
            return EActionStatus.NoOp;
        }
        
        public override AbstractProxyTaskBehaviour CreateInstance()
        {
            return new DisjointProxyTaskBehaviour();
        }
        
    }
}
