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
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            await UniTask.Delay(5000, cancellationToken: token);
        }

        private void OnTriggerEnter(Collider other)
        {
            /*if (!other.TryGetComponent(out GameplayAbilitySystem gas)) return;
            
            ApplyEffects(gas);*/
        }
    }
}
