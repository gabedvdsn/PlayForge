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

        /// <summary>
        /// A projectile's flight is its run, so it starts Self Terminating, ending as it lands, with no step timing, as it
        /// has no update of its own. A mono process starts Synchronous, which has no run, so one made in code never flew
        /// (PC-35). Its inspector, or code, still sets either.
        /// </summary>
        protected AbstractTargetedMonoProcess()
        {
            ProcessLifecycle = EProcessLifecycle.SelfTerminating;
            ProcessTiming = EProcessStepTiming.None;
        }

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            
            // With nothing to fly at, its run ends at once. It went on to use the missing target, and threw (GAS-31)
            if (!regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target))
            {
                ForgeLog.Warning($"{GetType().Name}: no {nameof(Tags.TARGET_REAL)} target in registration data, so it has nothing to fly at and ends.", this);
                return;
            }
            targeting = target.GetTargetingPacket();

            var to = Quaternion.LookRotation(targeting.position - transform.position);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, to, 360);
        }
        
        public override async UniTask RunProcess(CancellationToken token)
        {
            if (target is null) return;

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
