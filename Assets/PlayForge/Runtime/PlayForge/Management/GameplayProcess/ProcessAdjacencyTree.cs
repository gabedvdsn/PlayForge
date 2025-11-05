using System.Collections.Generic;
using System.Linq;
using FarEmerald;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Each node must be able to describe
    ///     - PARENTS (Every MonoProcess on the first GO with any MonoProcesses upon it)
    ///     - SHARED (Every MonoProcess on the same GO)
    ///     - CHILDREN (Every first MonoProcess descendent under the GO; think nuclear family)
    /// </summary>

    public class ProcessAdjacencyRef
    {
        public int PID;
        public ProcessAdjacencyRef Next;
        public int Count => Next is null ? 1 : Next.Count + 1;

        public ProcessAdjacencyRef(int pid)
        {
            PID = pid;
            Next = null;
        }

        public void Set(int next)
        {
            if (Next is not null) Next.Set(next);
            else Next = new ProcessAdjacencyRef(next);
        }

        public List<int> Accumulate()
        {
            var pids = new List<int>();
            var curr = this;
            while (curr is not null)
            {
                pids.Add(curr.PID);
                curr = curr.Next;
            }
            return pids;
        }

        public List<int> AccumulateSkip()
        {
            var pids = new List<int>();
            var curr = Next;
            while (curr is not null)
            {
                pids.Add(curr.PID);
                curr = curr.Next;
            }
            return pids;
        }
    }

    public class ProcessAdjacencyNode
    {
        public ProcessAdjacencyNode(int PID, GameObject obj)
        {
            Object = obj;
            Local = new ProcessAdjacencyRef(PID);
            
            Parent = null;
            Sibling = null;
            Child = null;
        }

        public GameObject Object;
        public ProcessAdjacencyRef Local;
        
        public ProcessAdjacencyNode Parent;
        public ProcessAdjacencyNode Sibling;
        public ProcessAdjacencyNode Child;
        
        public List<int> GetPIDs(bool skipSelf = true)
        {
            var pids = skipSelf ? Local.AccumulateSkip() : Local.Accumulate();
            var child = Child;
            while (child is not null)
            {
                pids.AddRange(child.Local.Accumulate());
                var sib = child.Sibling;
                while (sib is not null)
                {
                    pids.AddRange(sib.Local.Accumulate());
                    sib = sib.Sibling;
                }
                child = child.Child;
            }

            return pids;
        }
        
        public int Count => Local.Count + (Child?.CountWithin ?? 1);
        public int CountWithin => Local.Count + (Sibling is not null ? Sibling.CountWithin + 1 : 1) + (Child is not null ? Child.CountWithin + 1 : 1);
        
        public int SiblingCount => Sibling is null ? 0 : 1 + Sibling.SiblingCount;
        public int ChildCount => Child.SiblingCount + 1;
            
        public void SetSibling(ProcessAdjacencyNode node)
        {
            if (Sibling is not null) Sibling.SetSibling(node);
            else Sibling = node;
        }
    }

    /// <summary>
    /// P is registered (tracked, relayed, and added to tree)
    /// Parent P is found (if it exists and is registered)
    /// Local non-registered processes are found and registered
    /// Child non-registered processes are found and registered
    /// 
    /// </summary>

    public class ProcessAdjacencyTree
    {
        private ProcessAdjacencyNode Root;
        private Dictionary<AbstractMonoProcess, ProcessAdjacencyNode> Access = new();
        private Dictionary<GameObject, ProcessAdjacencyNode> Activated = new();

        public void Add(AbstractMonoProcess process, ProcessDataPacket data)
        {
            if (Activated.TryGetValue(process.gameObject, out var node))
            {
                node.Local.Set(process.Relay.CacheIndex);
                return;
            }
            
            Activated[process.gameObject] = IncorporateNode(process, data);
            Root ??= Activated[process.gameObject];
        }

        public bool Remove(AbstractMonoProcess process, out ProcessAdjacencyNode node)
        {
            // Make sure it exists and grab the node
            if (!Access.TryGetValue(process, out node)) return false;

            // Node is the root
            if (node == Root)
            {
                
            }
            // Node is primary child
            else if (node == node.Parent.Child) node.Parent.Child = node.Sibling;
            else
            {
                var back = node.Parent.Child;
                var curr = node.Parent.Child.Sibling;
                for (int i = 0; i < node.Parent.ChildCount; i++)
                {
                    if (curr == node)
                    {
                        back.Sibling = curr.Sibling;
                        break;
                    }
                    
                    back = curr;
                    curr = curr.Sibling;
                }    
            }

            Activated.Remove(process.gameObject);
            
            return true;
        }
        
        public bool Contains(AbstractMonoProcess process)
        {
            return Access.ContainsKey(process);
        }

        public bool Contains(GameObject process) => Activated.ContainsKey(process);

        public ProcessAdjacencyNode Get(AbstractMonoProcess process)
        {
            return Access[process];
        }
        
        private ProcessAdjacencyNode IncorporateNode(AbstractMonoProcess process, ProcessDataPacket data)
        {
            var node = new ProcessAdjacencyNode(process.Relay.CacheIndex, process.gameObject);
            Access[process] = node;
            
            // Find the parent node
            var parent = GetParentNode(process.transform.parent);
            if (parent is not null)
            {
                // Parent doesn't have a child, let's create one
                if (parent.Child is null) parent.Child = node;
                // Parent DOES have a child, let's set a sibling
                else parent.SetSibling(node);
            }
            
            // Find local processes
            foreach (var locProcess in process.GetComponents<AbstractMonoProcess>())
            {
                if (process == locProcess) continue;
                
                Access[locProcess] = node;
                ProcessControl.Instance.Register(locProcess, data, out _);

                // node.Local.Set(locProcess.Relay.CacheIndex);
            }
            
            // Find child processes
            foreach (var locProcess in process.GetComponentsInChildren<AbstractMonoProcess>())
            {
                if (process == locProcess) continue;

                var locData = ProcessDataPacket.RootLocal(locProcess, data.Handler);
                //locData.AddPayload(GameRoot.TransformTag, ESourceTargetData.Data, locProcess.transform.parent);
                
                ProcessControl.Instance.Register(locProcess, locData, out _);
            }
            
            return node;
            
            ProcessAdjacencyNode GetParentNode(Transform t)
            {
                while (true)
                {
                    if (t is null) break;

                    var parents = t.GetComponents<AbstractMonoProcess>();
                    if (parents.Length > 0) return Access[parents[0]];
                    t = t.parent;
                }

                return null;
            }
        }

        private void SeparateNode(ProcessAdjacencyNode node)
        {
            
        }
    }
}