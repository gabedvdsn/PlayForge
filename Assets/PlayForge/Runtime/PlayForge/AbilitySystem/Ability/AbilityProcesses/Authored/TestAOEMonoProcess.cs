using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class TestAOEMonoProcess : AbstractEffectingMonoProcess
    {
        public int DelayMs = 5000;
        
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await UniTask.Delay(DelayMs, cancellationToken: token);
        }

        private void OnTriggerEnter(Collider other)
        {
            /*if (!other.TryGetComponent(out GameplayAbilitySystem gas)) return;
            
            ApplyEffects(gas);*/
        }
    }
}
