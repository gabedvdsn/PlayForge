using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A ring that widens from its caster, hitting each enemy as its edge reaches it, once: a frost nova, or a boss's
    /// slam. Its widest grows with its caster's Area.
    /// </summary>
    public class NovaRing : SurvivorsProcess
    {
        [Tooltip("How wide it grows")]
        [Min(0.1f)] public float Radius = 4f;

        [Tooltip("Seconds it takes to grow")]
        [Min(0.01f)] public float Grow = 0.35f;

        [Tooltip("Seconds its edge lingers once grown")]
        [Min(0f)] public float Linger = 0.2f;

        [Tooltip("Its edge's look")]
        public SurvivorsLine Edge;

        private readonly HashSet<ITarget> struck = new();

        public override async UniTask RunProcess(CancellationToken token)
        {
            struck.Clear();
            float widest = Grown(Radius), time = 0f;
            var centre = transform.position;
            while (time < Grow)
            {
                token.ThrowIfCancellationRequested();
                if (CasterGone) return;
                time += Delta;
                float now = widest * Mathf.Clamp01(time / Grow);
                if (Edge) Edge.Draw(now);
                FindAround(centre, now);
                for (int i = 0; i < Near.Count; i++)
                    if (struck.Add(Near[i])) ApplyEffects(Near[i]);
                await ForgeTick.NextFrame(token);
            }
            await Wait(Linger, token);
        }
    }
}
