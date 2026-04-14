namespace FarEmerald.PlayForge
{
    public class TrackedImpact
    {
        private class Node
        {
            public AttributeValue Impact;
            public Node Next;

            public Node(AttributeValue impact)
            {
                Impact = impact;
            }
        }
        
        public AttributeValue Total { get; private set; }
        public AttributeValue Last => end?.Impact ?? default;
        
        public int Count { get; private set; }
        
        private Node root;
        private Node end;

        public void Add(AttributeValue value)
        {
            if (root is null)
            {
                root = new Node(value);
                end = root;
                
                Total = value;
                Count = 1;
                return;
            }
            
            end.Next = new Node(value);
            end = end.Next;
            
            Total += value;
            Count += 1;
        }
    }
}
