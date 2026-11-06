using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The stack count of the item that applied the effect: 1 for an item that doesn't stack, and 1 for an effect no item
    /// applied. With Multiply With Scaler, an item's effect grows by the stack, as a relic of +10 a stack does. Nothing
    /// read an item's stack count (FEAT-14).
    /// </summary>
    [Serializable]
    public class ItemStacksScaler : AbstractScaler
    {
        // It reads the item's stacks alone (ED-51)
        public override EScalerParts Parts => EScalerParts.None;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
        }

        public override float Evaluate(IAttributeImpactCarrier deriv) =>
            deriv?.GetOrigin() is ItemSpec item ? Mathf.Max(1, item.Stacks) : 1f;
    }
}
