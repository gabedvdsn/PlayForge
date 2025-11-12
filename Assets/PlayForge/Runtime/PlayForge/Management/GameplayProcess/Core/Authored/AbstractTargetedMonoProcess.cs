using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base class for following/tracking projectiles. 
    /// </summary>
    public abstract class AbstractTargetedMonoProcess : AbstractEffectingMonoProcess
    {
        protected ITarget target;
        protected AbstractTransformPacket targetTransform;

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            
            if (!regData.TryGet(Tags.PAYLOAD_TARGET, EProxyDataValueTarget.Primary, out target)) Debug.Log($"Whelp!");
            targetTransform = target.AsTransform();

            var to = Quaternion.LookRotation(targetTransform.position - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, to, 360);
        }
        
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            target.CommunicateTargetedIntent(this);
            
            await RunTargetedProcess(relay, token);
            
            ApplyEffects(target);
        }

        protected abstract UniTask RunTargetedProcess(ProcessRelay relay, CancellationToken token);

        /// <summary>
        /// Implement composite behaviour responses here!
        /// 
        /// This user accepts:
        ///     Disjoint
        /// </summary>
        /// <param name="command"></param>
        /// <param name="cb"></param>
        /// <param name="caller"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            if (command == DisjointProxyTaskBehaviour.Command)
            {
                if (regData.TryGet(DisjointProxyTaskBehaviour.IS_DISJOINTABLE, EProxyDataValueTarget.Primary, out bool result))
                {
                    if (!result)
                    {
                        cb.SetStatus(this, EActionStatus.Failure);
                        return UniTask.CompletedTask;
                    }
                }
                else
                {
                    cb.SetStatus(this, EActionStatus.Failure);
                    return UniTask.CompletedTask;
                }

                // We expect caller to be a targetable entity (e.g. GASComponent)
                if (caller is not ITarget _target)
                {
                    cb.SetStatus(this, EActionStatus.Error);
                    return UniTask.CompletedTask;
                }

                var disjoint = DisjointTarget.Generate(_target);
                
                target = disjoint;
                targetTransform = disjoint.AsTransform();

                target.CommunicateTargetedIntent(this);

                cb.SetStatus(this, EActionStatus.Success);
            }
            else cb.SetStatus(this, EActionStatus.NoOp);
            
            return UniTask.CompletedTask;
        }
    }
}
