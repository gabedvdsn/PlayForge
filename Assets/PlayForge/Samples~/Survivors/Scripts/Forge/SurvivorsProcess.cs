using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// What the demo's area processes share: their caster, whom they reach, and their numbers grown by their caster's
    /// attributes, read by role: a radius by its Area, a lasting process's time by its Duration. A role reads the process's
    /// own attribute, or else the caster's that its Attribute Pairs name, so the hero's passives grow every area at once.
    /// </summary>
    public abstract class SurvivorsProcess : AbstractEffectingMonoProcess
    {
        // Its life is its run, so it starts Self Terminating, with no step timing of its own
        protected SurvivorsProcess()
        {
            ProcessLifecycle = EProcessLifecycle.SelfTerminating;
            ProcessTiming = EProcessStepTiming.None;
        }

        [Tooltip("Whom it reaches, by how each stands to its caster")]
        public EAffiliationFilter Hits = EAffiliationFilter.Enemies;

        /// <summary>Who it found at its last search.</summary>
        protected readonly List<ITarget> Near = new();

        /// <summary>The entity that cast it.</summary>
        protected ISource Caster => Origin?.GetSource();

        /// <summary>Whether its caster is gone from the scene, as a fallen enemy or the last run's hero, which ends it.</summary>
        protected bool CasterGone => !SurvivorsTeams.Exists(Caster);

        /// <summary>Where its caster stands now.</summary>
        protected Vector3 CasterPlace => SurvivorsTeams.PlaceOf(Caster as ITarget);

        /// <summary>The seconds of its handler's time since the last frame.</summary>
        protected float Delta => ForgeTime.DeltaTimeFor(ProcessRelay?.Handler);

        /// <summary>A radius, grown by its caster's Area.</summary>
        protected float Grown(float radius) => radius * (TryGetRole(SurvivorsTags.Area, out var area) && area > 0f ? area : 1f);

        /// <summary>A time, lengthened by its caster's Duration.</summary>
        protected float Lasting(float seconds) => seconds * (TryGetRole(SurvivorsTags.Duration, out var length) && length > 0f ? length : 1f);

        /// <summary>Finds whom it reaches within a radius of a point, into <see cref="Near"/>.</summary>
        protected void FindAround(Vector3 centre, float radius)
        {
            Near.Clear();
            SurvivorsTeams.Around(Caster, centre, radius, Hits, Near);
        }

        /// <summary>The direction it's aimed, its registration's Aim, or its forward.</summary>
        protected Vector3 AimOf()
        {
            var aim = regData.TryGet(SurvivorsTags.Aim, EDataTarget.Primary, out Vector3 given) ? given : transform.forward;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;
        }

        /// <summary>A wait on its handler's clock.</summary>
        protected async UniTask Wait(float seconds, CancellationToken token)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                token.ThrowIfCancellationRequested();
                waited += Delta;
                await ForgeTick.NextFrame(token);
            }
        }
    }
}
