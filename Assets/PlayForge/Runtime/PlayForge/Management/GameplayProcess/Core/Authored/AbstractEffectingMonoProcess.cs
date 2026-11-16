using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A gameplay mono process that applies effects, as a projectile does as it lands. Its effects come from its origin,
    /// and take the set-by-caller magnitudes of the data that spawned it, as an ability's other effects do.
    /// </summary>
    public abstract class AbstractEffectingMonoProcess : AbstractGameplayMonoProcess
    {
        public List<GameplayEffect> Effects;

        protected void ApplyEffects(ITarget target)
        {
            if (Effects == null || target == null) return;
            foreach (var effect in Effects)
            {
                if (effect == null) continue;
                var spec = target.GenerateEffectSpec(Origin, effect);
                // The magnitudes what spawned it set, as an ability's other effects take them, and its own
                // attributes by role
                GiveMagnitudes(spec);
                target.ApplyCarrier(spec);
            }
        }
    }
}
