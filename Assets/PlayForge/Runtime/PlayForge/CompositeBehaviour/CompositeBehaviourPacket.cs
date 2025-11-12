namespace FarEmerald.PlayForge
{
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
