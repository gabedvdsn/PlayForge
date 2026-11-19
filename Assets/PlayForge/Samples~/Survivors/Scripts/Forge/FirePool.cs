using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A pool of fire on the ground: for its lifetime it burns each enemy within its radius, each interval. Its radius
    /// grows with its caster's Area and its lifetime with its Duration.
    /// </summary>
    public class FirePool : SurvivorsProcess
    {
        [Tooltip("How far from its middle it burns")]
        [Min(0.1f)] public float Radius = 2f;

        [Tooltip("Seconds it burns")]
        [Min(0.1f)] public float Lifetime = 3f;

        [Tooltip("Seconds between its burns")]
        [Min(0.05f)] public float Interval = 0.5f;

        [Tooltip("A child it scales to its radius, as the fire's look")]
        public Transform Look;

        private float radius;

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            var at = transform.position;
            transform.SetPositionAndRotation(new Vector3(at.x, 0.03f, at.z), Quaternion.identity);
            radius = Grown(Radius);
            if (Look) Look.localScale = new Vector3(radius * 2f, Look.localScale.y, radius * 2f);
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            float life = Lasting(Lifetime), elapsed = 0f, next = 0f;
            while (elapsed < life)
            {
                token.ThrowIfCancellationRequested();
                if (CasterGone) return;
                if (elapsed >= next)
                {
                    FindAround(transform.position, radius);
                    for (int i = 0; i < Near.Count; i++) ApplyEffects(Near[i]);
                    next += Interval;
                }
                elapsed += Delta;
                await ForgeTick.NextFrame(token);
            }
        }
    }
}
