namespace FarEmerald.PlayForge
{
    public struct CompositeBehaviourPacket
    {
        public Tag Command;
        public AbstractCompositeBehaviour Behaviour;
        
        public ICompositeBehaviourCaller Caller;
        public ICompositeBehaviourUser User;

        public static CompositeBehaviourPacket Generate(Tag command, AbstractCompositeBehaviour behaviour, ICompositeBehaviourCaller caller, ICompositeBehaviourUser user)
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
        Success = 0,
        Pending = 1,
        Failure = 2,
        Error = 4,
        Warning = 3,
        NoData = 5,
        NoOp = -1
    }
}
