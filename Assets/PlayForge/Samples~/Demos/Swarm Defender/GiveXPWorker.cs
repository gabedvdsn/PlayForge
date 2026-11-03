using System.Collections.Generic;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    public class GiveXPWorker : ThresholdAnalysisWorker
    {
        protected override IEnumerable<IRootAction> OnThresholdReached(IGameplayAbilitySystem system, AttributeValue value, FrameSummary frameSummary)
        {
            return base.OnThresholdReached(system, value, frameSummary);
        }
    }
}
