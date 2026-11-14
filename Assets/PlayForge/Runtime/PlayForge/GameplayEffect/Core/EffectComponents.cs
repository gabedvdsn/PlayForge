using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Area: as its effect lands on its target, it lands on each the area takes near that target too, as Storm Hammer stuns
    /// everyone near the one it hits (TW-10, FN-14). Without one, an effect lands on its target alone. An effect holds one
    /// at most.
    /// </summary>
    [Serializable]
    public sealed class AreaEffectComponent : AbstractEffectComponent
    {
        [Tooltip("Its area: as its effect lands on its target, it lands on each the area takes near that target too (TW-10)")]
        public EffectArea Area = new();

        public override bool AllowsSeveral => false;
    }

    /// <summary>
    /// Transfer: moves an amount from an attribute of its effect's caster or target to one of either, as the effect lands,
    /// each tick, or while it lasts, giving back as it ends; only what the giver has moves (TW-20, FN-14). An effect holds
    /// as many as it moves, each its own.
    /// </summary>
    [Serializable]
    public sealed class TransferEffectComponent : AbstractEffectComponent
    {
        [Tooltip("What it moves: when, who gives and of what, who gets and in what, which of the values, and how much (TW-20)")]
        public AttributeTransfer Transfer = new();
    }
}
