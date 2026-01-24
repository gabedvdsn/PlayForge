using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages hierarchical relationships between MonoProcesses.
    /// Parent actions cascade to children, but not vice versa.
    /// 
    /// Hierarchy Rules:
    /// - Processes on the SAME GameObject are "siblings" (treated as a group)
    /// - Processes on CHILD GameObjects are "children" 
    /// - Only the FIRST ancestor with processes counts as the "parent"
    /// </summary>
    public class ProcessHierarchy
    {
        /// <summary>
        /// Represents a node in the process hierarchy.
        /// A node corresponds to a GameObject that has one or more processes.
        /// </summary>
        public class HierarchyNode
        {
            public GameObject GameObject { get; }
            public HierarchyNode Parent { get; set; }
            
            // All process cache indices on this GameObject
            private readonly List<int> _localProcessIds = new();
            public IReadOnlyList<int> LocalProcessIds => _localProcessIds;
            
            // Direct children (first-level descendants with processes)
            private readonly List<HierarchyNode> _children = new();
            public IReadOnlyList<HierarchyNode> Children => _children;

            public HierarchyNode(GameObject gameObject)
            {
                GameObject = gameObject;
            }

            public void AddLocalProcess(int cacheIndex)
            {
                if (!_localProcessIds.Contains(cacheIndex))
                    _localProcessIds.Add(cacheIndex);
            }

            public void RemoveLocalProcess(int cacheIndex)
            {
                _localProcessIds.Remove(cacheIndex);
            }

            public void AddChild(HierarchyNode child)
            {
                if (!_children.Contains(child))
                {
                    _children.Add(child);
                    child.Parent = this;
                }
            }

            public void RemoveChild(HierarchyNode child)
            {
                _children.Remove(child);
                if (child.Parent == this)
                    child.Parent = null;
            }

            /// <summary>
            /// Gets all descendant process IDs (children, grandchildren, etc.)
            /// Does NOT include local processes on this node.
            /// </summary>
            public void GetDescendantProcessIds(List<int> results)
            {
                foreach (var child in _children)
                {
                    results.AddRange(child._localProcessIds);
                    child.GetDescendantProcessIds(results);
                }
            }

            /// <summary>
            /// Gets all process IDs in this subtree (local + all descendants)
            /// </summary>
            public void GetAllProcessIds(List<int> results, bool includeLocal = true)
            {
                if (includeLocal)
                    results.AddRange(_localProcessIds);
                GetDescendantProcessIds(results);
            }
            
            public bool HasProcesses => _localProcessIds.Count > 0;
            public bool HasChildren => _children.Count > 0;
        }

        // Maps GameObject to its hierarchy node
        private readonly Dictionary<GameObject, HierarchyNode> _nodesByGameObject = new();
        
        // Maps process cache index to its node (for quick lookup)
        private readonly Dictionary<int, HierarchyNode> _nodesByProcessId = new();
        
        // Root nodes (processes with no parent process)
        private readonly List<HierarchyNode> _roots = new();
        
        // Reusable list for collecting process IDs (avoids allocation)
        private readonly List<int> _tempIdList = new();

        /// <summary>
        /// Registers a MonoProcess and establishes its hierarchy relationships.
        /// </summary>
        public void Register(AbstractMonoProcess process, int cacheIndex)
        {
            if (process == null) return;
            
            var go = process.gameObject;
            
            // Get or create node for this GameObject
            if (!_nodesByGameObject.TryGetValue(go, out var node))
            {
                node = new HierarchyNode(go);
                _nodesByGameObject[go] = node;
                
                // Find parent node (first ancestor with processes)
                var parentNode = FindParentNode(go.transform.parent);
                if (parentNode != null)
                {
                    parentNode.AddChild(node);
                }
                else
                {
                    _roots.Add(node);
                }
                
                // Check if any existing roots should become children of this node
                ReorganizeRootsUnder(node);
            }
            
            // Add this process to the node
            node.AddLocalProcess(cacheIndex);
            _nodesByProcessId[cacheIndex] = node;
        }

        /// <summary>
        /// Unregisters a process from the hierarchy.
        /// </summary>
        public void Unregister(int cacheIndex)
        {
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return;

            node.RemoveLocalProcess(cacheIndex);
            _nodesByProcessId.Remove(cacheIndex);
            
            // If node has no more processes, remove it from hierarchy
            if (!node.HasProcesses)
            {
                CleanupEmptyNode(node);
            }
        }

        /// <summary>
        /// Gets all child process IDs that should be affected when operating on the given process.
        /// This includes sibling processes on the same GameObject and all descendant processes.
        /// </summary>
        /// <param name="cacheIndex">The source process cache index</param>
        /// <param name="includeSiblings">Whether to include other processes on the same GameObject</param>
        /// <param name="includeSelf">Whether to include the source process itself</param>
        /// <param name="reverseList">Whether to reverse the output list</param>
        public List<int> GetCascadeTargets(int cacheIndex, bool includeSiblings = true, bool includeSelf = false, bool reverseList = true)
        {
            _tempIdList.Clear();
            
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return new List<int>(_tempIdList);

            // Add local processes if requested
            if (includeSiblings || includeSelf)
            {
                foreach (var localId in node.LocalProcessIds)
                {
                    if (localId == cacheIndex && !includeSelf)
                        continue;
                    if (localId != cacheIndex && !includeSiblings)
                        continue;
                    _tempIdList.Add(localId);
                }
            }
            
            // Add all descendant processes
            node.GetDescendantProcessIds(_tempIdList);

            var ids = new List<int>(_tempIdList);
            if (reverseList) ids.Reverse();

            return ids;
        }

        /// <summary>
        /// Gets only the direct children process IDs (not grandchildren)
        /// </summary>
        public List<int> GetDirectChildProcessIds(int cacheIndex)
        {
            _tempIdList.Clear();
            
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return new List<int>(_tempIdList);

            foreach (var child in node.Children)
            {
                _tempIdList.AddRange(child.LocalProcessIds);
            }
            
            return new List<int>(_tempIdList);
        }

        /// <summary>
        /// Checks if the given process has any child processes
        /// </summary>
        public bool HasChildren(int cacheIndex)
        {
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return false;
            return node.HasChildren;
        }

        /// <summary>
        /// Gets the parent process ID(s) for the given process
        /// </summary>
        public List<int> GetParentProcessIds(int cacheIndex)
        {
            _tempIdList.Clear();
            
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return new List<int>(_tempIdList);

            if (node.Parent != null)
            {
                _tempIdList.AddRange(node.Parent.LocalProcessIds);
            }
            
            return new List<int>(_tempIdList);
        }

        /// <summary>
        /// Gets sibling process IDs (other processes on the same GameObject)
        /// </summary>
        public List<int> GetSiblingProcessIds(int cacheIndex)
        {
            _tempIdList.Clear();
            
            if (!_nodesByProcessId.TryGetValue(cacheIndex, out var node))
                return new List<int>(_tempIdList);

            foreach (var localId in node.LocalProcessIds)
            {
                if (localId != cacheIndex)
                    _tempIdList.Add(localId);
            }
            
            return new List<int>(_tempIdList);
        }

        /// <summary>
        /// Checks if processA is an ancestor of processB
        /// </summary>
        public bool IsAncestorOf(int ancestorId, int descendantId)
        {
            if (!_nodesByProcessId.TryGetValue(ancestorId, out var ancestorNode))
                return false;
            if (!_nodesByProcessId.TryGetValue(descendantId, out var descendantNode))
                return false;

            var current = descendantNode.Parent;
            while (current != null)
            {
                if (current == ancestorNode)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Clears all hierarchy data
        /// </summary>
        public void Clear()
        {
            _nodesByGameObject.Clear();
            _nodesByProcessId.Clear();
            _roots.Clear();
            _tempIdList.Clear();
        }

        #region Private Helpers

        private HierarchyNode FindParentNode(Transform parentTransform)
        {
            var current = parentTransform;
            while (current != null)
            {
                if (_nodesByGameObject.TryGetValue(current.gameObject, out var node))
                    return node;
                current = current.parent;
            }
            return null;
        }

        private void ReorganizeRootsUnder(HierarchyNode potentialParent)
        {
            // Check if any current roots should become children of this new node
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                var root = _roots[i];
                if (root == potentialParent) continue;
                
                if (IsTransformDescendantOf(root.GameObject.transform, potentialParent.GameObject.transform))
                {
                    _roots.RemoveAt(i);
                    potentialParent.AddChild(root);
                }
            }
        }

        private bool IsTransformDescendantOf(Transform potential, Transform ancestor)
        {
            var current = potential.parent;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private void CleanupEmptyNode(HierarchyNode node)
        {
            // Move all children up to parent or make them roots
            foreach (var child in node.Children)
            {
                if (node.Parent != null)
                {
                    node.Parent.AddChild(child);
                }
                else
                {
                    child.Parent = null;
                    if (!_roots.Contains(child))
                        _roots.Add(child);
                }
            }
            
            // Remove from parent
            node.Parent?.RemoveChild(node);
            
            // Remove from roots if it was there
            _roots.Remove(node);
            
            // Remove from lookup
            _nodesByGameObject.Remove(node.GameObject);
        }

        #endregion

        #region Debug

        public int NodeCount => _nodesByGameObject.Count;
        public int RootCount => _roots.Count;
        public int ProcessCount => _nodesByProcessId.Count;

        public string GetDebugInfo()
        {
            return $"ProcessHierarchy: {ProcessCount} processes across {NodeCount} nodes ({RootCount} roots)";
        }

        #endregion
    }
}
