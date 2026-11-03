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
    public abstract class AbstractTargetedMonoProcess : AbstractEffectingMonoProcess, IHasIntentToTarget
    {
        protected ITarget target;
        protected AbstractTargetingPacket targeting;

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            
            if (!regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target))
            {
                ForgeLog.Warning($"{GetType().Name}: no {nameof(Tags.TARGET_REAL)} target in registration data — process cannot orient.", this);
            }
            targeting = target.GetTargetingPacket();

            var to = Quaternion.LookRotation(targeting.position - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, to, 360);
        }
        
        public override async UniTask RunProcess(CancellationToken token)
        {
            target.CommunicateTargetedIntent(this);
            
            await RunTargetedProcess(token);

            ApplyEffects(target);

            // Landed, it isn't aimed there any more. Nothing ever took an intent back, so a target's list only grew
            // (GAS-26)
            target.WithdrawTargetedIntent(this);
        }

        // Ended before it lands, it isn't aimed anywhere. A pause doesn't end it, and keeps its intent
        public override void WhenTerminate()
        {
            base.WhenTerminate();
            target?.WithdrawTargetedIntent(this);
        }

        protected abstract UniTask RunTargetedProcess(CancellationToken token);

        public void SetTarget(ITarget _target, AbstractTargetingPacket _targeting)
        {
            // Off its old target's list first, or that target's next disjoint took the shot back (GAS-26)
            target?.WithdrawTargetedIntent(this);

            target = _target;
            targeting = _targeting;

            target.CommunicateTargetedIntent(this);
        }
    }
}
