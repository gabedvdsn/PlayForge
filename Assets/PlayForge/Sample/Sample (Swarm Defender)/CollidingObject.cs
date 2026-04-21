using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    public class CollidingObject : LazyMonoProcess
    {
        private void OnCollisionEnter(Collision other)
        {
            var gas = other.gameObject.GetComponent<GameplayAbilitySystem>();
            if (!gas) return;

            Data.TryApplyEffects(gas, Tags.EFFECTS);
        }
    }
}
