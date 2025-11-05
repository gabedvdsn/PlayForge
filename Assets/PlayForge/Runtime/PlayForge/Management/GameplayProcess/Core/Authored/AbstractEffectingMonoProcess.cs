using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractEffectingMonoProcess : AbstractGameplayMonoProcess
    {
        public List<GameplayEffect> Effects;

        protected void ApplyEffects(ITarget target)
        {
            foreach (var effect in Effects) target.ApplyGameplayEffect(target.GenerateEffectSpec(Origin, effect));
        }
    }
}
