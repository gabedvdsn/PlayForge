using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Revives the entity its effect is on as the effect ends: a respawn timer, as a staying entity takes as it dies, whose
    /// duration is the time it waits (TW-4). It revives where the entity was, or at a point. An entity revived already, as
    /// by a buyback, takes nothing from it.
    /// </summary>
    [Serializable]
    public class ReviveOnRemovalEffectWorker : AbstractEffectWorker
    {
        [Tooltip("Revive at the point below, where on, else where the entity was")]
        public bool AtPoint;

        [Tooltip("Where it revives, with At Point on: a spawn point")]
        public Vector3 Point;

        public override IEnumerable<IRootAction> OnEffectRemoval(EffectWorkerContext ctx)
        {
            var entity = ctx.System;
            if (entity == null) yield break;
            Vector3? at = AtPoint ? Point : null;
            yield return new LambdaAction(_ => entity.Revive(at), "Revive");
        }
    }
}
