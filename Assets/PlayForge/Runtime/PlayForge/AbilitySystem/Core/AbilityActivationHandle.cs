using System.Threading;

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

        private static int _nextId;

        public AbilityActivationHandle(AbilitySpecContainer container, AbilityDataPacket data, string abilityName = null)
        {
            Container = container;
            HandleId = _nextId++;
            Data = data;
            Cts = new CancellationTokenSource();
            TargetingCts = new CancellationTokenSource();
            Proxy = container.Spec.Base.Behaviour.GenerateProxy(abilityName);
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
