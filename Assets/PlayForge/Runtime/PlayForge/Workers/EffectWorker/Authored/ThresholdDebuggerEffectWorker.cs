using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ThresholdDebuggerEffectWorker : TargetAttributeThresholdEffectWorker
    {
        protected override IEnumerable<IRootAction> OnThresholdMet(EffectWorkerContext ctx)
        {
            return new[]
            {
                new LambdaAction(_ =>
                {
                    Debug.Log($"Threshold is met: {ctx.Derivation}");
                })
            };
        }
    }
}
