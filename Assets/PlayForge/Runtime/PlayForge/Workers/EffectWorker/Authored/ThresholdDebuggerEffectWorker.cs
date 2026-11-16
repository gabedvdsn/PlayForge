using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [ForgeMenu(Description = "Logs as its target's attribute crosses a threshold, for debugging.")]
    public class ThresholdDebuggerEffectWorker : TargetAttributeThresholdEffectWorker
    {
        protected override IEnumerable<IRootAction> OnThresholdMet(EffectWorkerContext ctx)
        {
            return new[]
            {
                new LambdaAction(_ =>
                {
                    ForgeLog.Info(ForgeLog.ESystem.Workers, $"Threshold is met: {ctx.Carrier}");
                })
            };
        }
    }
}
