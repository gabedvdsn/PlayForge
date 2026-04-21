using System;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    public class Character : GameplayAbilitySystem
    {
        public override void WhenInitialize()
        {
            base.WhenInitialize();
            
            StartInternalProcesses();
        }

        protected virtual void StartInternalProcesses()
        {
            
        }
    }
}
