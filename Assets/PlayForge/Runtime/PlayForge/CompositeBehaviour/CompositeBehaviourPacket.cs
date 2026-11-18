namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One application of a composite behaviour, an <c>AbstractProxyTaskBehaviour</c>, in one value: its <c>Command</c>
    /// tag, the <c>Behaviour</c>, the <c>Caller</c> that applies it, as an entity, and the <c>User</c> it acts on, as a
    /// shot aimed at that entity. <c>Generate</c> makes one.
    /// </summary>
    public struct CompositeBehaviourPacket
    {
        public Tag Command;
        public AbstractProxyTaskBehaviour Behaviour;
        
        public IProxyTaskBehaviourCaller Caller;
        public IProxyTaskBehaviourUser User;

        public static CompositeBehaviourPacket Generate(Tag command, AbstractProxyTaskBehaviour behaviour, IProxyTaskBehaviourCaller caller, IProxyTaskBehaviourUser user)
        {
            return new CompositeBehaviourPacket()
            {
                Command = command,
                Behaviour = behaviour,
                
                Caller = caller,
                User = user
            };
        }
    }

    /// <summary>
    /// How a composite behaviour's run on one user came out, as its <c>Status</c> reads it: <c>Success</c>;
    /// <c>Pending</c>, not yet done; <c>Failure</c>, refused by its checks, as a shot not marked disjointable;
    /// <c>Error</c>, as with a caller that isn't an entity; <c>NoData</c>, nothing recorded for the user; or
    /// <c>NoOp</c>, nothing done, as for a shot whose data says nothing of being disjointable.
    /// </summary>
    public enum EActionStatus
    {
        /// <summary>
        /// CB performed successfully
        /// </summary>
        Success = 0,
        
        /// <summary>
        /// CB status is still pending
        /// </summary>
        Pending = 1,
        
        /// <summary>
        /// CB failed during validation process
        /// </summary>
        Failure = 2,
        
        /// <summary>
        /// An error occurred while executing the CB
        /// </summary>
        Error = 3,
        
        /// <summary>
        /// No status available 
        /// </summary>
        NoData = 4,
        
        /// <summary>
        /// No CB operation occurred
        /// </summary>
        NoOp = -1
    }
}
