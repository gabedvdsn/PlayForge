using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A sweep in front of its caster, the way it's aimed: each enemy within its radius and its arc takes its effects, at
    /// once, and the sweep shows a moment. Its radius grows with its caster's Area.
    /// </summary>
    public class SlashArc : SurvivorsProcess
    {
        [Tooltip("How far it reaches")]
        [Min(0.1f)] public float Radius = 2.8f;

        [Tooltip("How wide it sweeps, in degrees")]
        [Range(10f, 360f)] public float Arc = 160f;

        [Tooltip("Seconds it shows")]
        [Min(0f)] public float Show = 0.2f;

        [Tooltip("Its look")]
        public SurvivorsLine Sweep;

        public override async UniTask RunProcess(CancellationToken token)
        {
            var aim = AimOf();
            var centre = transform.position;
            transform.rotation = Quaternion.LookRotation(aim);
            float reach = Grown(Radius);
            if (Sweep)
            {
                Sweep.Arc = Arc;
                Sweep.Draw(reach);
            }
            FindAround(centre, reach);
            float half = Arc / 2f;
            for (int i = 0; i < Near.Count; i++)
            {
                var toward = SurvivorsTeams.PlaceOf(Near[i]) - centre;
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.01f || Vector3.Angle(aim, toward) <= half) ApplyEffects(Near[i]);
            }
            await Wait(Show, token);
        }
    }
}
