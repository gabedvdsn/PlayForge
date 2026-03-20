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
        protected AbstractTransformPacket targetTransform;

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            
            if (!regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target)) Debug.Log($"Whelp!");
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

        public void SetTarget(ITarget _target, AbstractTransformPacket _transform)
        {
            target = _target;
            targetTransform = _transform;

            target.CommunicateTargetedIntent(this);
        }
    }
}
