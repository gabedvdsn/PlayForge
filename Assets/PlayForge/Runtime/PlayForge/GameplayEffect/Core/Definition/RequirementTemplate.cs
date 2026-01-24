using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A reusable tag requirement configuration that can be shared across multiple assets.
    /// 
    /// Usage:
    /// 1. Create a RequirementTemplate asset (Create > PlayForge > Templates > Requirement Template)
    /// 2. Configure the required/avoided tags in the template
    /// 3. Reference this template from any requirement field using the "Link Template" button
    /// 4. Changes to the template will propagate to all linked requirements when they sync
    /// 
    /// Note: Linked requirements can "break" the link to make local modifications.
    /// </summary>
    [CreateAssetMenu(fileName = "New Requirement Template", menuName = "PlayForge/Templates/Requirement Template", order = 101)]
    public class RequirementTemplate : ScriptableObject
    {
        [Tooltip("Description of what this requirement template is used for")]
        [TextArea(2, 4)]
        public string Description;
        
        [Tooltip("Category for organizing templates in the import window")]
        public string Category = "General";
        
        [Tooltip("The requirement configuration stored in this template")]
        public AvoidRequireTagGroup Requirements;
        
        /// <summary>
        /// Gets a display name for this template.
        /// </summary>
        public string DisplayName => Requirements?.HasName == true ? Requirements.Name : name;
        
        /// <summary>
        /// Copies values from this template to a target requirement group.
        /// </summary>
        public void CopyTo(AvoidRequireTagGroup target)
        {
            if (Requirements == null || target == null) return;
            
            target.Name = Requirements.Name;
            
            // Deep copy RequireTags
            if (Requirements.RequireTags != null)
            {
                target.RequireTags = new List<AvoidRequireContainer>();
                foreach (var req in Requirements.RequireTags)
                {
                    target.RequireTags.Add(new AvoidRequireContainer
                    {
                        Tag = req.Tag,
                        Operator = req.Operator,
                        Magnitude = req.Magnitude
                    });
                }
            }
            else
            {
                target.RequireTags = new List<AvoidRequireContainer>();
            }
            
            // Deep copy AvoidTags
            if (Requirements.AvoidTags != null)
            {
                target.AvoidTags = new List<AvoidRequireContainer>();
                foreach (var avoid in Requirements.AvoidTags)
                {
                    target.AvoidTags.Add(new AvoidRequireContainer
                    {
                        Tag = avoid.Tag,
                        Operator = avoid.Operator,
                        Magnitude = avoid.Magnitude
                    });
                }
            }
            else
            {
                target.AvoidTags = new List<AvoidRequireContainer>();
            }
        }
        
        /// <summary>
        /// Checks if a requirement group's values match this template.
        /// </summary>
        public bool MatchesTemplate(AvoidRequireTagGroup other)
        {
            if (Requirements == null || other == null) return false;
            
            // Check RequireTags
            if (!ListsMatch(Requirements.RequireTags, other.RequireTags)) return false;
            
            // Check AvoidTags
            if (!ListsMatch(Requirements.AvoidTags, other.AvoidTags)) return false;
            
            return true;
        }
        
        private bool ListsMatch(List<AvoidRequireContainer> a, List<AvoidRequireContainer> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Tag != b[i].Tag) return false;
                if (a[i].Operator != b[i].Operator) return false;
                if (a[i].Magnitude != b[i].Magnitude) return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets summary statistics for this template.
        /// </summary>
        public (int requiredCount, int avoidCount) GetCounts()
        {
            if (Requirements == null) return (0, 0);
            return (Requirements.RequireTags?.Count ?? 0, Requirements.AvoidTags?.Count ?? 0);
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Initialize lists if null
            if (Requirements == null)
            {
                Requirements = new AvoidRequireTagGroup
                {
                    RequireTags = new List<AvoidRequireContainer>(),
                    AvoidTags = new List<AvoidRequireContainer>()
                };
            }
            
            Requirements.RequireTags ??= new List<AvoidRequireContainer>();
            Requirements.AvoidTags ??= new List<AvoidRequireContainer>();
        }
#endif
    }
}