using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A blade that circles its caster for a while, hitting each enemy it passes, again once a moment has gone by. Its
    /// circle grows with its caster's Area and its time with its Duration; a volley spreads its blades around the circle.
    /// </summary>
    public class OrbitingBlade : SurvivorsProcess
    {
        [Tooltip("How far from its caster it circles")]
        [Min(0.1f)] public float Radius = 2.6f;

        [Tooltip("Seconds it circles")]
        [Min(0.1f)] public float Lifetime = 3f;

        [Tooltip("Turns it makes a second")]
        public float Turns = 0.8f;

        [Tooltip("How far around it reaches to hit")]
        [Min(0.05f)] public float HitRadius = 0.8f;

        [Tooltip("Seconds before it can hit the same one again")]
        [Min(0.05f)] public float Rehit = 0.5f;

        private readonly Dictionary<ITarget, float> lastHit = new();
        private float phase;

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            lastHit.Clear();
            phase = regData.TryGet(SurvivorsTags.Phase, EDataTarget.Primary, out float given) ? given : 0f;
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            float radius = Grown(Radius), life = Lasting(Lifetime), time = 0f;
            while (time < life)
            {
                token.ThrowIfCancellationRequested();
                if (CasterGone) return;
                time += Delta;
                float angle = (phase + 360f * Turns * time) * Mathf.Deg2Rad;
                var centre = CasterPlace;
                var at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                transform.SetPositionAndRotation(at + Vector3.up, Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f));
                FindAround(at, HitRadius);
                for (int i = 0; i < Near.Count; i++)
                {
                    var target = Near[i];
                    if (lastHit.TryGetValue(target, out float when) && time - when < Rehit) continue;
                    lastHit[target] = time;
                    ApplyEffects(target);
                }
                await ForgeTick.NextFrame(token);
            }
        }
    }
}
