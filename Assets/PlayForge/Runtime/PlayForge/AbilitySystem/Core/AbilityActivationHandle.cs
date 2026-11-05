using System.Threading;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilityActivationHandle
    {
        public readonly AbilitySpecContainer Container;
        public readonly int HandleId;
        public AbilityDataPacket Data;
        public CancellationTokenSource Cts;
        public CancellationTokenSource TargetingCts;
        public AbilityProxy Proxy;
        public bool IsTargeting;
        public bool IsExecuting;
        public bool ClaimReleased;

        // An activation with no ability system to number it, which none has in practice
        private static int _nextId;

        // Each session numbers its activations from the start, as it did with a domain reload (SP-22)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIds() => _nextId = 0;

        public AbilityActivationHandle(AbilitySpecContainer container, AbilityDataPacket data, string abilityName = null)
            : this(container, data, container.Spec.Base.Behaviour.GenerateProxy(abilityName))
        {
        }

        /// <summary>An activation run by <paramref name="proxy"/>, an already compiled behaviour.</summary>
        public AbilityActivationHandle(AbilitySpecContainer container, AbilityDataPacket data, AbilityProxy proxy)
        {
            Container = container;
            // Numbered by its ability system, so an entity's activations count from 0 whatever else runs. One count for
            // the whole game numbered them in the order of every entity's activations, which two machines can't share
            // (FEAT-13)
            HandleId = data?.System != null ? data.System.NextHandleId() : _nextId++;
            Data = data;
            Cts = new CancellationTokenSource();
            TargetingCts = new CancellationTokenSource();
            Proxy = proxy;
        }

        public void ReleaseClaimIfNeeded()
        {
            if (ClaimReleased) return;
            ClaimReleased = true;
            Container.OnHandleClaimReleased(this);
        }

        public void CleanTokens()
        {
            CleanToken(ref Cts);
            CleanToken(ref TargetingCts);
        }

        private static void CleanToken(ref CancellationTokenSource token)
        {
            if (token is null) return;
            if (!token.IsCancellationRequested) token.Cancel();
            token.Dispose();
            token = null;
        }
    }
}
