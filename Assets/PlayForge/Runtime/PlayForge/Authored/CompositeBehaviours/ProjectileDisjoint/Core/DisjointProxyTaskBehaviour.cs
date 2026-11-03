
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A disjoint, as in Dota: the shots already aimed at an entity lose it, fly on to where it stood, and land on
    /// nothing. A shot is found through the intent it registers with its target, which only a homing shot does
    /// (<see cref="AbstractTargetedMonoProcess"/>), and only one whose data marks it <see cref="IS_DISJOINTABLE"/>
    /// is disjointed. A skill shot, one that hits whatever it crosses, registers no intent, and lands as usual (GAS-36).
    /// </summary>
    public class DisjointProxyTaskBehaviour : AbstractProxyTaskBehaviour
    {
        public static Tag IS_DISJOINTABLE => Tag.GenerateAsUnique("CB_IS_DISJOINTABLE");
        public static Tag COMMAND => Tag.GenerateAsUnique("CB_DISJOINT");

        public override Tag Command => COMMAND;

        /// <summary>
        /// Aims one shot at a placeholder where its target stood. A user that isn't a homing shot fails, and one not
        /// marked disjointable is left alone.
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
            if (data.TryGetAll(IS_DISJOINTABLE, out DataValue<bool> results))
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

            // At a placeholder where the target stood, which takes nothing. This aimed the shot back at the target it
            // had already, so it went on and hit (GAS-26)
            var disjoint = DisjointTarget.Generate(_target);
            userWithIntent.SetTarget(disjoint, disjoint.GetTargetingPacket());
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
