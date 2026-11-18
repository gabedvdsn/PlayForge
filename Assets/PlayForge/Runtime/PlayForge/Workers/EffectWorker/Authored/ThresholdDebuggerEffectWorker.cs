using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An effect worker for debugging: on each hit of its effect where its threshold is met, as
    /// <c>TargetAttributeThresholdEffectWorker</c> checks it, it queues an action that logs so, with the effect's spec,
    /// through ForgeLog at Info.
    /// </summary>
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
