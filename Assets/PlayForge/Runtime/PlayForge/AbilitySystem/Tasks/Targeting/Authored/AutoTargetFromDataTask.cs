using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Auto-targeting task that reads an ITarget from the data packet
    /// using a configurable tag key. Useful when a previous system
    /// (event, trigger, or another ability) has already determined the target
    /// and stored it in the data packet.
    ///
    /// If no target is found under the specified key, falls back to
    /// targeting invalid behavior.
    /// </summary>
    [Serializable]
    public class AutoTargetFromDataTask : AbstractGasTargetingAbilityTask
    {
        [Tooltip("The data packet key to read the target from")]
        [ForgeTagContext(ForgeContext.Data)]
        public Tag SourceKey;

        [Tooltip("When true, reads the Primary entry. When false, reads all entries and adds them.")]
        public bool PrimaryOnly = true;

        public override string Description => "Read target from data packet";

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var key = SourceKey != null ? SourceKey : Tags.TARGET_REAL;

            if (PrimaryOnly)
            {
                if (data.TryGet(key, EDataTarget.Primary, out ITarget target) && TargetIsValid(target))
                {
                    data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                }
                else
                {
                    WhenTargetingInvalid(data);
                }
            }
            else
            {
                bool found = false;
                if (data.TryGet<ITarget>(key, out var targets))
                {
                    foreach (var target in targets)
                    {
                        if (target == null || !TargetIsValid(target)) continue;
                        data.SetTargetingPacket(Tags.TARGET, target.GetTargetingPacket());
                        found = true;
                    }
                }

                if (!found) WhenTargetingInvalid(data);
            }

            return UniTask.CompletedTask;
        }
        
    }
}
