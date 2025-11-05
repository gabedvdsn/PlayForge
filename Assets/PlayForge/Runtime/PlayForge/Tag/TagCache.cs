using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class TagCache
    {
        private ITagHandler System;

        // List of tag worker datas
        private List<AbstractTagWorker> TagWorkers;

        private Dictionary<Tag, int> TagWeights;
        private Dictionary<AbstractTagWorker, List<AbstractTagWorkerInstance>> ActiveWorkers;

        public Tag[] GetAppliedTags() => TagWeights.Keys.ToArray();

        public TagCache(ITagHandler system)
        {
            System = system;

            TagWorkers = new List<AbstractTagWorker>();
            TagWeights = new Dictionary<Tag, int>();
            ActiveWorkers = new Dictionary<AbstractTagWorker, List<AbstractTagWorkerInstance>>();
        }

        public TagCache(ITagHandler system, List<AbstractTagWorker> workers)
        {
            System = system;

            TagWeights = new Dictionary<Tag, int>();
            TagWorkers = workers;

            ActiveWorkers = new Dictionary<AbstractTagWorker, List<AbstractTagWorkerInstance>>();
        }
        
        public void AddTagWorker(AbstractTagWorker worker)
        {
            if (!TagWorkers.Contains(worker)) TagWorkers.Add(worker);
        }

        public void RemoveTagWorker(AbstractTagWorker worker)
        {
            if (TagWorkers.Contains(worker)) TagWorkers.Remove(worker);
        }

        private void HandleTagWorkers()
        {
            // Handle deactivating active workers if applicable
            IEnumerable<AbstractTagWorker> activeWorkers = ActiveWorkers.Keys;
            foreach (AbstractTagWorker workerData in activeWorkers)
            {
                if (workerData.ValidateWorkFor(System)) continue;
                
                foreach (AbstractTagWorkerInstance worker in ActiveWorkers[workerData]) worker.Resolve();
                ActiveWorkers.Remove(workerData);
            }
            
            // Handle activating new workers if applicable
            foreach (AbstractTagWorker workerData in TagWorkers)
            {
                if (!workerData.ValidateWorkFor(System)) continue;

                if (ActiveWorkers.ContainsKey(workerData) && workerData.AllowMultipleInstances) ActiveWorkers[workerData].Add(workerData.Generate(System));
                else ActiveWorkers[workerData] = new List<AbstractTagWorkerInstance>() { workerData.Generate(System) };
                ActiveWorkers[workerData][^1].Initialize();
            }
        }

        public void TickTagWorkers()
        {
            foreach (AbstractTagWorker workerData in ActiveWorkers.Keys)
            {
                foreach (AbstractTagWorkerInstance worker in ActiveWorkers[workerData]) worker.Tick();
            }
        }

        public void AddTag(Tag tag, bool noDuplicates = false, bool handle = true)
        {
            if (TagWeights.ContainsKey(tag))
            {
                if (!noDuplicates) TagWeights[tag] += 1;
            }
            else TagWeights[tag] = 1;
            
            if (handle) HandleTagWorkers();
        }

        public void AddTags(IEnumerable<Tag> tags, bool noDuplicates = false)
        {
            foreach (var tag in tags)
            {
                AddTag(tag, noDuplicates, false);
            }
            
            HandleTagWorkers();
        }

        public void RemoveTag(Tag tag, bool handle = true)
        {
            if (!TagWeights.ContainsKey(tag)) return;
                
            TagWeights[tag] -= 1;
            if (GetWeight(tag) <= 0) TagWeights.Remove(tag);
            
            if (handle) HandleTagWorkers();
        }

        public void RemoveTags(IEnumerable<Tag> tags)
        {
            foreach (var tag in tags)
            {
                RemoveTag(tag, false);
            }
            
            HandleTagWorkers();
        }
        
        public int GetWeight(Tag tag) => TagWeights.TryGetValue(tag, out int weight) ? weight : 0;

        public bool HasTag(Tag tag) => TagWeights.ContainsKey(tag);

        public void LogWeights()
        {
            Debug.Log($"[ LOG-WEIGHTS ]");
            foreach (Tag tag in TagWeights.Keys)
            {
                Debug.Log($"\t{tag} => {TagWeights[tag]}");
            }
        }
    }
    
    [Serializable]
    public class TagWorkerRequirements
    {
        [Header("Requirements")]
        
        public List<TagWorkerRequirementPacket> TagPackets;
    }
    
    [Serializable]
    public struct TagWorkerRequirementPacket
    {
        public Tag Tag;
        public ERequireAvoidPolicy Policy;
        public int RequiredWeight;

        public TagWorkerRequirementPacket(Tag tag, ERequireAvoidPolicy policy, int requiredWeight)
        {
            Tag = tag;
            Policy = policy;
            RequiredWeight = requiredWeight;
        }
    }

    public enum ERequireAvoidPolicy
    {
        Require,
        Avoid
    }

}
