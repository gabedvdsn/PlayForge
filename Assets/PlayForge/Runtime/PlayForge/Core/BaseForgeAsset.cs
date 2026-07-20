using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeAsset : ScriptableObject, ILocalDataSource, IHasReadableDefinition, ITagSource
    {
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TEMPLATE SYSTEM
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// The template asset this asset derives from. Only same-type assets can be templates.
        /// Changes to the template propagate to this asset for non-overridden fields.
        /// </summary>
        [SerializeField] private BaseForgeAsset _template;
        
        /// <summary>
        /// Property paths that have been manually changed on this asset and should not
        /// be overwritten during template propagation. Automatically maintained by comparing
        /// field values against the template on save.
        /// </summary>
        [SerializeField] private List<string> _templateOverrides = new();
        
        /// <summary>
        /// Snapshots of each list property's state at last template sync.
        /// Used to detect additions/removals in the template for list merge logic.
        /// </summary>
        [SerializeField] private List<TemplateListSnapshot> _templateListSnapshots = new();
        
        /// <summary>The template asset this asset derives from (null if standalone).</summary>
        public BaseForgeAsset Template => _template;
        
        /// <summary>True if this asset has a template assigned.</summary>
        public bool HasTemplate => _template != null;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TEMPLATE ASSIGNMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Sets the template for this asset. Only assets of the same concrete type are accepted.
        /// Clears existing overrides and snapshots.
        /// </summary>
        public bool SetTemplate(BaseForgeAsset template)
        {
            if (template == this) return false;
            if (template != null && GetType() != template.GetType()) return false;
            
            _template = template;
            _templateOverrides.Clear();
            _templateListSnapshots.Clear();
            return true;
        }
        
        /// <summary>
        /// Removes the template assignment and clears all override/snapshot data.
        /// </summary>
        public void ClearTemplate()
        {
            _template = null;
            _templateOverrides.Clear();
            _templateListSnapshots.Clear();
        }
        
        /// <summary>
        /// Returns the template cast to a specific type.
        /// </summary>
        public T TemplateAs<T>() where T : BaseForgeAsset
        {
            return _template as T;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // OVERRIDE TRACKING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if the given property path has been marked as overridden,
        /// meaning it should not be overwritten during template propagation.
        /// </summary>
        public bool IsFieldOverridden(string propertyPath)
        {
            return _templateOverrides.Contains(propertyPath);
        }
        
        /// <summary>
        /// Marks a property path as overridden (will not be affected by template propagation).
        /// </summary>
        public void MarkFieldOverridden(string propertyPath)
        {
            if (!_templateOverrides.Contains(propertyPath))
                _templateOverrides.Add(propertyPath);
        }
        
        /// <summary>
        /// Removes an override marking, allowing template propagation for this path again.
        /// </summary>
        public void ClearFieldOverride(string propertyPath)
        {
            _templateOverrides.Remove(propertyPath);
        }
        
        /// <summary>Returns all currently overridden property paths.</summary>
        public IReadOnlyList<string> GetOverriddenPaths() => _templateOverrides;
        
        /// <summary>Clears all field overrides.</summary>
        public void ClearAllOverrides() => _templateOverrides.Clear();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // LIST SNAPSHOTS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gets the stored snapshot of a list property's element identifiers from last sync.
        /// Returns null if no snapshot exists for the given path.
        /// </summary>
        public TemplateListSnapshot GetListSnapshot(string propertyPath)
        {
            return _templateListSnapshots.FirstOrDefault(s => s.PropertyPath == propertyPath);
        }
        
        /// <summary>
        /// Stores or updates a snapshot of a list property's element identifiers.
        /// </summary>
        public void SetListSnapshot(string propertyPath, List<string> identifiers)
        {
            var existing = _templateListSnapshots.FirstOrDefault(s => s.PropertyPath == propertyPath);
            if (existing != null)
            {
                existing.ElementIdentifiers = identifiers;
            }
            else
            {
                _templateListSnapshots.Add(new TemplateListSnapshot(propertyPath, identifiers));
            }
        }
        
        /// <summary>Removes the snapshot for a specific list path.</summary>
        public void ClearListSnapshot(string propertyPath)
        {
            _templateListSnapshots.RemoveAll(s => s.PropertyPath == propertyPath);
        }
        
        /// <summary>Clears all list snapshots.</summary>
        public void ClearAllListSnapshots() => _templateListSnapshots.Clear();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // LOCAL DATA
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            data = LocalData.FirstOrDefault(ld => ld.Key == key);
            return data != null;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ABSTRACT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public abstract IEnumerable<Tag> GetGrantedTags();
        public abstract string GetName();
        public abstract string GetDescription();
        public abstract Texture2D GetDefaultIcon();
        public List<DataWrapper> GetLocalData()
        {
            return LocalData;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TEMPLATE LIST SNAPSHOT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Stores the element identifiers of a list property at the time of last template sync.
    /// Used to detect additions/removals in the template for list merge logic.
    /// </summary>
    [Serializable]
    public class TemplateListSnapshot
    {
        public string PropertyPath;
        public List<string> ElementIdentifiers;
        
        public TemplateListSnapshot() { }
        
        public TemplateListSnapshot(string path, List<string> identifiers)
        {
            PropertyPath = path;
            ElementIdentifiers = new List<string>(identifiers);
        }
    }

    public interface ILocalDataUser
    {
        public void InitLocalData(ILocalDataSource source);
        public void SetLocalData(Tag key, DataWrapper data);
        public bool TryGetLocalData(Tag key, out DataWrapper data);
        public LocalDataStructure GetLocalDataStructure();
    }

    public interface ILocalDataSource
    {
        public List<DataWrapper> GetLocalData();
    }

    public class LocalDataStructure
    {
        private Dictionary<Tag, DataWrapper> data;

        public LocalDataStructure()
        {
            data = new Dictionary<Tag, DataWrapper>();
        }

        public LocalDataStructure(ILocalDataSource source)
        {
            data = new Dictionary<Tag, DataWrapper>();
            Init(source);
        }

        public void Init(ILocalDataSource source)
        {
            if (source is null) return;
            
            foreach (var wrapper in source.GetLocalData())
            {
                data[wrapper.Key] = wrapper;
            }
        }

        public void Set(Tag key, DataWrapper wrapper) => data[key] = wrapper;
        public DataWrapper GetOrInit(Tag key, DataWrapper fallback = null)
        {
            if (data.TryGetValue(key, out var wrapper)) return wrapper;
            data[key] = fallback ?? new DataWrapper();
            return data[key];
        }
        public bool TryGet(Tag key, out DataWrapper wrapper)
        {
            return data.TryGetValue(key, out wrapper);
        }
    }
}