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
            
            if (!regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target)) Debug.Log($"Whelp!");
            targeting = target.GetTargetingPacket();

            var to = Quaternion.LookRotation(targeting.position - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, to, 360);
        }
        
        public override async UniTask RunProcess(CancellationToken token)
        {
            target.CommunicateTargetedIntent(this);
            
            await RunTargetedProcess(token);
            
            ApplyEffects(target);
        }

        protected abstract UniTask RunTargetedProcess(CancellationToken token);

        public void SetTarget(ITarget _target, AbstractTargetingPacket _targeting)
        {
            target = _target;
            targeting = _targeting;

            target.CommunicateTargetedIntent(this);
        }
    }
}
