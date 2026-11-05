using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A gameplay cue: what an effect shows and sounds, apart from what it changes, listed on the effect as its workers
    /// are. Bursts play as the effect lands, as its ticks execute and as it comes off. A lasting cue begins as the effect
    /// lands and ends as it goes, and a restore ends it and begins it again as the effect leaves and comes back, playing no
    /// burst. Each hook is a no-op until a handler overrides it. An effect changed attributes and tags and nothing else,
    /// so what it showed was each game's own code (FEAT-1).
    /// </summary>
    [Serializable]
    public abstract class AbstractCueHandler
    {
        /// <summary>As the effect lands, a burst: a new application, or one an effect on already takes in.</summary>
        public virtual void OnApplied(CueContext cue) { }

        /// <summary>As the effect's ticks execute, a burst, with how many executed.</summary>
        public virtual void OnTicked(CueContext cue) { }

        /// <summary>As the effect comes off, a burst.</summary>
        public virtual void OnRemoved(CueContext cue) { }

        /// <summary>The effect is on, from its landing or its coming back with a restore: a lasting cue begins.</summary>
        public virtual void Begin(CueContext cue) { }

        /// <summary>The effect is off, as it goes or as a restore lets go of it: a lasting cue ends.</summary>
        public virtual void End(CueContext cue) { }
    }

    /// <summary>What a cue plays for: the entity the effect is on, the effect's spec, and for a tick, how many executed (FEAT-1).</summary>
    public readonly struct CueContext
    {
        public readonly IGameplayAbilitySystem Target;
        public readonly GameplayEffectSpec Spec;
        public readonly int Ticks;

        public CueContext(IGameplayAbilitySystem target, GameplayEffectSpec spec, int ticks = 0)
        {
            Target = target;
            Spec = spec;
            Ticks = ticks;
        }

        /// <summary>Where the target is: a MonoBehaviour hub's transform, or null for a runtime GAS, which has no place.</summary>
        public Transform Transform => Target is Component component && component != null ? component.transform : null;

        /// <summary>The target's position, or the world's origin where it has none.</summary>
        public Vector3 Position => Transform != null ? Transform.position : Vector3.zero;
    }
}
