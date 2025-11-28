using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Editor tags:
    /// - no_edit: bool => Cannot be edited
    /// - built: bool => Data object is built into SO
    /// - requires_rebuild: bool => Data object need has changes and needs to be rebuilt
    /// - missing_refs: bool => Referenced materials are missing
    /// - search_open_in_home: bool
    /// - search_open_in_creator: bool
    /// - search_open_in_developer: bool
    /// </summary>

    public enum EDataType
    {
        Ability = 2, 
        Effect = 3, 
        Entity = 1, 
        Attribute = 4, 
        Tag = 5, 
        AttributeSet = 6, 
        None = 7
    }
    
    [Serializable]
    public class FrameworkProject
    {
        public string Version = AboutPlayForge.PlayForgeVersion;
        public string MetaName;
        public string MetaAuthor = "";

        public List<AbilityData> Abilities = new();
        public List<AttributeData> Attributes = new();
        public List<TagData> Tags = new();
        public List<EffectData> Effects = new();
        public List<EntityData> Entities = new();
        public List<AttributeSetData> AttributeSets = new();
        
        public int DataCount => Attributes.Count + AttributeSets.Count + Tags.Count + Entities.Count + Abilities.Count + Effects.Count;

        public static FrameworkProject EmptyDefault()
        {
            return new FrameworkProject
            {
                Version = AboutPlayForge.PlayForgeVersion,
                MetaName = "EmptyFramework",
                MetaAuthor = "PlayForge"
            };
        }

        public IList GetAllOf(EDataType kind)
        {
            return kind switch
            {
                EDataType.Ability => Abilities,
                EDataType.Effect => Effects,
                EDataType.Entity => Entities,
                EDataType.Attribute => Attributes,
                EDataType.Tag => Tags,
                EDataType.AttributeSet => AttributeSets,
                EDataType.None => new List<ForgeDataNode>(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }
        
        public Dictionary<EDataType, List<(int, string)>> GetCompleteDescriptions()
        {
            var items = new Dictionary<EDataType, List<(int, string)>>();
            
            AddItems(EDataType.Ability, Abilities);
            AddItems(EDataType.Effect, Effects);
            AddItems(EDataType.Entity, Entities);
            AddItems(EDataType.Attribute, Attributes);
            AddItems(EDataType.Tag, Tags);
            AddItems(EDataType.AttributeSet, AttributeSets);

            return items;

            void AddItems<T>(EDataType type, List<T> nodes) where T : ForgeDataNode
            {
                items[type] = new List<(int, string)>();
                foreach (var node in nodes)
                {
                    // Only show created (referential) data
                    if (!ForgeTags.IsValidForEditing(node)) continue;
                    
                    items[type].Add((node.Id, node.Name));
                }
            }
        }

        public Dictionary<EDataType, List<ForgeDataNode>> GetCompleteNodes(params Func<ForgeDataNode, bool>[] validations)
        {
            var items = new Dictionary<EDataType, List<ForgeDataNode>>();
            
            AddItems(EDataType.Ability, Abilities);
            AddItems(EDataType.Effect, Effects);
            AddItems(EDataType.Entity, Entities);
            AddItems(EDataType.Attribute, Attributes);
            AddItems(EDataType.Tag, Tags);
            AddItems(EDataType.AttributeSet, AttributeSets);

            return items;

            void AddItems<T>(EDataType type, List<T> nodes) where T : ForgeDataNode
            {
                items[type] = new List<ForgeDataNode>();
                foreach (var node in nodes.Where(node => validations.All(v => v == null || v(node))))
                {
                    items[type].Add(node);
                }
            }
        }

        public bool TryGet(int id, EDataType kind, out ForgeDataNode node)
        {
            return TryGet(id, "", kind, out node);
        }
        
        public bool TryGet(int id, string name, EDataType kind, out ForgeDataNode node)
        {
            bool status = true;
            node = null;
            
            switch (kind)
            {
                case EDataType.Ability:
                    var AbilityData = Abilities.Where(d => d.Id == id).ToArray();
                    if (AbilityData.Length != 0) node = AbilityData[0];
                    else
                    {
                        node = new AbilityData
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.Effect:
                    var EffectData = Effects.Where(d => d.Id == id).ToArray();
                    if (EffectData.Length != 0) node = EffectData[0];
                    else
                    {
                        node = new EffectData()
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.Entity:
                    var EntityData = Entities.Where(d => d.Id == id).ToArray();
                    if (EntityData.Length != 0) node = EntityData[0];
                    else
                    {
                        node = new EntityData()
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.Attribute:
                    var AttributeData = Attributes.Where(d => d.Id == id).ToArray();
                    if (AttributeData.Length != 0) node = AttributeData[0];
                    else
                    {
                        node = new AttributeData()
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.Tag:
                    var TagData = Tags.Where(d => d.Id == id).ToArray();
                    if (TagData.Length != 0) node = TagData[0];
                    else
                    {
                        node = new TagData()
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.AttributeSet:
                    var AttributeSetData = AttributeSets.Where(d => d.Id == id).ToArray();
                    if (AttributeSetData.Length != 0) node = AttributeSetData[0];
                    else
                    {
                        node = new AttributeSetData()
                        {
                            Id = id,
                            Name = name
                        };
                        status = false;
                    }
                    break;
                case EDataType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            return status;
        }

        public bool DataWithNameExists(string name, EDataType kind, int ignoreId = -1)
        {
            return kind switch
            {

                EDataType.Ability =>
                    Abilities.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.Effect =>
                    Effects.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.Entity =>
                    Entities.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.Attribute =>
                    Attributes.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.Tag =>
                    Tags.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.AttributeSet =>
                    AttributeSets.Any(d => d.Id != ignoreId && string.Equals(d.Name, name, StringComparison.InvariantCultureIgnoreCase)),
                EDataType.None => false, 
                _ => false
            };
        }
        
        public ForgeDataNode BuildNode(int id, string name, EDataType kind)
        {
            if (kind == EDataType.None) return null;
            
            ForgeDataNode node = null;
            switch (kind)
            {
                case EDataType.Ability:
                    node = new AbilityData
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.Effect:
                    node = new EffectData()
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.Entity:
                    node = new EntityData()
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.Attribute:
                    node = new AttributeData()
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.Tag:
                    node = new TagData()
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.AttributeSet:
                    node = new AttributeSetData()
                    {
                        Id = id,
                        Name = name
                    };
                    break;
                case EDataType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            ForgeTags.ForNewData(node);
            return node;
        }

        /// <summary>
        /// Saves an item into the Project. Marked with the SAVED_NOT_BUILT
        /// </summary>
        public void Save(ForgeDataNode node, EDataType kind)
        {
            if (node.TagStatus(ForgeTags.IS_SAVED_COPY))
            {
                return;
            }

            ForgeTags.OnSave(node);
            
            int copyId = node.TagStatus(ForgeTags.COPY_ID, -1);
            if (!TryGet(copyId, kind, out var copy)) AddCloneToProject(node, kind, out _);  // Create saved copy
            else UpdateSavedCopy(node, copy);
        }

        public bool Revert(ForgeDataNode node, EDataType kind)
        {
            var copy = node.TagStatus<int>(ForgeTags.COPY_ID);
            if (copy <= 0) return false;
            if (!TryGet(copy, node.Name, kind, out var copyNode)) return false;

            switch (kind)      
            {
                case EDataType.Ability:
                    var a = copyNode as AbilityData;
                    return Replace(Abilities, node.Id, a);
                case EDataType.Effect:
                    var e = copyNode as EffectData;
                    return Replace(Effects, node.Id, e);
                case EDataType.Entity:
                    var ent = copyNode as EntityData;
                    return Replace(Entities, node.Id, ent);
                case EDataType.Attribute:
                    var attr = copyNode as AttributeData;
                    return Replace(Attributes, node.Id, attr);
                case EDataType.Tag:
                    var t = copyNode as TagData;
                    return Replace(Tags, node.Id, t);
                case EDataType.AttributeSet:
                    var attrs = copyNode as AttributeSetData;
                    return Replace(AttributeSets, node.Id, attrs);
                case EDataType.None:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Replace ID node with node in source
        /// </summary>
        /// <param name="source"></param>
        /// <param name="id"></param>
        /// <param name="node"></param>
        /// <typeparam name="T"></typeparam>
        private bool Replace<T>(List<T> source, int id, T node) where T : ForgeDataNode
        {
            int idx = -1;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Id == id)
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0) return false;

            source[idx] = node;
            return true;
        }
        
        public void Create(ForgeDataNode node, EDataType kind)
        {
            ForgeTags.OnCreate(node);
            
            AddToProject(kind, node);
            Save(node, kind);
        }
        
        public void CreateTemplate(ForgeDataNode node, EDataType kind)
        {
            ForgeTags.OnCreateTemplate(node);
            AddToProject(kind, node);
        }

        private void AddToProject(EDataType kind, ForgeDataNode node)
        {
            Debug.Log($"Adding to project {kind} {node.Name}");
            switch (kind)
            {
                case EDataType.Ability:
                    for (int i = 0; i < Abilities.Count; i++)
                    {
                        if (Abilities[i].Id == node.Id)
                        {
                            Abilities[i] = node as AbilityData;
                            break;
                        }
                    }
                    Abilities.Add(node as AbilityData);
                    break;
                case EDataType.Effect:
                    for (int i = 0; i < Effects.Count; i++)
                    {
                        if (Effects[i].Id == node.Id)
                        {
                            Effects[i] = node as EffectData;
                            break;
                        }
                    }
                    Effects.Add(node as EffectData);
                    break;
                case EDataType.Entity:
                    for (int i = 0; i < Entities.Count; i++)
                    {
                        if (Entities[i].Id == node.Id)
                        {
                            Entities[i] = node as EntityData;
                            break;
                        }
                    }
                    Entities.Add(node as EntityData);
                    break;
                case EDataType.Attribute:
                    for (int i = 0; i < Attributes.Count; i++)
                    {
                        if (Attributes[i].Id == node.Id)
                        {
                            Attributes[i] = node as AttributeData;
                            break;
                        }
                    }
                    Attributes.Add(node as AttributeData);
                    break;
                case EDataType.Tag:
                    for (int i = 0; i < Tags.Count; i++)
                    {
                        if (Tags[i].Id == node.Id)
                        {
                            Debug.Log($"replaced attribute");
                            Tags[i] = node as TagData;
                            break;
                        }
                    }
                    Tags.Add(node as TagData);
                    break;
                case EDataType.AttributeSet:
                    for (int i = 0; i < AttributeSets.Count; i++)
                    {
                        if (AttributeSets[i].Id == node.Id)
                        {
                            AttributeSets[i] = node as AttributeSetData;
                            break;
                        }
                    }
                    AttributeSets.Add(node as AttributeSetData);
                    break;
                case EDataType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void AddCloneToProject(ForgeDataNode node, EDataType kind, out int _id)
        {
            switch (kind)
            {
                case EDataType.Ability:
                    AddToProject(kind, ForgeDataNode.Clone(node as AbilityData, out _id));
                    break;
                case EDataType.Effect:
                    AddToProject(kind, ForgeDataNode.Clone(node as EffectData, out _id));
                    break;
                case EDataType.Entity:
                    AddToProject(kind, ForgeDataNode.Clone(node as EntityData, out _id));
                    break;
                case EDataType.Attribute:
                    AddToProject(kind, ForgeDataNode.Clone(node as AttributeData, out _id));
                    break;
                case EDataType.Tag:
                    AddToProject(kind, ForgeDataNode.Clone(node as TagData, out _id));
                    break;
                case EDataType.AttributeSet:
                    AddToProject(kind, ForgeDataNode.Clone(node as AttributeSetData, out _id));
                    break;
                case EDataType.None:
                    _id = -1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ForgeDataNode BuildClone(EDataType kind, ForgeDataNode source, out int _id)
        {
            _id = -1;
            return kind switch
            {
                EDataType.Ability => ForgeDataNode.Clone(source as AbilityData, out _id, "", false),
                EDataType.Effect => ForgeDataNode.Clone(source as EffectData, out _id, "", false),
                EDataType.Entity => ForgeDataNode.Clone(source as EntityData, out _id, "", false),
                EDataType.Attribute => ForgeDataNode.Clone(source as AttributeData, out _id, "", false),
                EDataType.Tag => ForgeDataNode.Clone(source as TagData, out _id, "", false),
                EDataType.AttributeSet => ForgeDataNode.Clone(source as AttributeSetData, out _id, "", false),
                EDataType.None => null,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private void UpdateSavedCopy(ForgeDataNode node, ForgeDataNode copy)
        {
            Debug.Log($"updating saved copy");
        }

        public void Remove(ForgeDataNode node, EDataType kind)
        {
            Debug.Log($"Removing {node}");
        }

        public bool HasNewChanges(ForgeDataNode node, EDataType kind)
        {
            if (!node.TagStatus(ForgeTags.IS_CREATED)) return false;

            int copyId = node.TagStatus(ForgeTags.COPY_ID, -1);
            if (!TryGet(copyId, kind, out var copy))
            {
                throw new Exception($"Could not find saved copy of {node.Name}.");
            }

            return HasNewChangesInternal(node, copy, node.GetType());

            bool HasNewChangesInternal(object a, object b, Type t)
            {
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var aVal = fi.GetValue(a);
                    var bVal = fi.GetValue(b);

                    if (aVal is null || bVal is null)
                    {
                        if (aVal != bVal) return true;
                        continue;
                    }

                    if (fi.FieldType.IsPrimitive
                        || fi.FieldType.IsEnum
                        || fi.FieldType == typeof(string)
                        || fi.FieldType.IsValueType)
                    {
                        if (!Equals(aVal, bVal)) return true;
                        continue;
                    }

                    if (typeof(IEnumerable).IsAssignableFrom(fi.FieldType))
                    {
                        var aList = ((IEnumerable)aVal).Cast<object>().ToArray();
                        var bList = ((IEnumerable)bVal).Cast<object>().ToArray();

                        if (aList.Length != bList.Length) return true;

                        for (int i = 0; i < aList.Length; i++)
                        {
                            if (HasNewChangesInternal(aList[i], bList[i], aList[i].GetType())) return true;
                        }

                        continue;
                    }

                    if (HasNewChangesInternal(aVal, bVal, fi.FieldType)) return true;
                }

                return false;
            }
        }
        
    }
}
