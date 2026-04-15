using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A named group of workers that can be attached to entities, abilities, items, or attribute sets.
    /// Workers are disbursed to the appropriate subsystems when the owning asset is applied.
    /// </summary>
    [Serializable]
    public class StandardWorkerGroup
    {
        [Tooltip("Display name for this worker group")]
        public string Name = "Workers";
        
        [Space(5)]
        [SerializeReference] public List<AbstractAttributeWorker> AttributeWorkers = new();
        [SerializeReference] public List<AbstractImpactWorker> ImpactWorkers = new();
        [SerializeReference] public List<AbstractTagWorker> TagWorkers = new();
        [SerializeReference] public List<AbstractAnalysisWorker> AnalysisWorkers = new();

        /// <summary>
        /// Disburse all workers to the appropriate subsystems.
        /// </summary>
        public void ProvideWorkersTo(ISource system)
        {
            if (system == null) return;
            
            if (!system.FindAttributeSystem(out var attrSystem) || attrSystem != null)
            {
                foreach (var worker in AttributeWorkers)
                {
                    if (worker != null) attrSystem.ProvideWorker(worker);
                }
            }

            var tags = system.GetTagCache();
            if (tags != null)
            {
                foreach (var worker in TagWorkers)
                {
                    if (worker != null) tags.ProvideWorker(worker);
                }
            }

            if (!system.FindAbilitySystem(out var abilSystem) || abilSystem != null)
            {
                foreach (var worker in ImpactWorkers)
                {
                    if (worker != null) abilSystem.ProvideWorker(worker);
                }
            }

            var analysis = system.ToGAS()?.GetAnalysisCache();
            if (analysis != null)
            {
                foreach (var worker in AnalysisWorkers)
                {
                    if (worker != null) analysis.ProvideWorker(worker);
                }
            }
        }
        
        /// <summary>
        /// Remove all workers from the appropriate subsystems.
        /// </summary>
        public void RemoveWorkersFrom(ISource system)
        {
            if (system == null) return;
            
            if (!system.FindAttributeSystem(out var attrSystem) || attrSystem != null)
            {
                foreach (var worker in AttributeWorkers)
                {
                    if (worker != null) attrSystem.RemoveWorker(worker);
                }
            }

            var tags = system.GetTagCache();
            if (tags != null)
            {
                foreach (var worker in TagWorkers)
                {
                    if (worker != null) tags.RemoveWorker(worker);
                }
            }

            if (!system.FindAbilitySystem(out var abilSystem) || abilSystem != null)
            {
                foreach (var worker in ImpactWorkers)
                {
                    if (worker != null) abilSystem.RemoveWorker(worker);
                }
            }

            var analysis = system.ToGAS()?.GetAnalysisCache();
            if (analysis != null)
            {
                foreach (var worker in AnalysisWorkers)
                {
                    if (worker != null) analysis.RemoveWorker(worker);
                }
            }
        }
        
        /// <summary>
        /// Total count of all workers in this group.
        /// </summary>
        public int TotalWorkerCount => 
            AttributeWorkers.Count + 
            ImpactWorkers.Count + 
            TagWorkers.Count + 
            AnalysisWorkers.Count;
        
        /// <summary>
        /// Check if this group has any workers.
        /// </summary>
        public bool HasWorkers => TotalWorkerCount > 0;
    }
}