using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SourcedModifiedAttributeCache
    {
        private Dictionary<Attribute, List<SourcedModifiedAttributeValue>> cache = new();
        private List<Attribute> active = new();

        public Dictionary<Attribute, List<SourcedModifiedAttributeValue>>.KeyCollection Attributes => cache.Keys;
        
        #region Core
        
        public void SubscribeModifiableAttribute(Attribute attribute)
        {
            cache[attribute] = new List<SourcedModifiedAttributeValue>();
        }

        public bool AttributeIsActive(Attribute attribute) => cache.ContainsKey(attribute) && cache[attribute].Count > 0;
        public bool DefinesAttribute(Attribute attribute) => cache.ContainsKey(attribute);

        public IEnumerable<Attribute> GetDefined() => cache.Keys.ToList();
        public IEnumerable<Attribute> GetModified() => active;

        public void Clear()
        {
            if (active.Count == 0) return;
            foreach (Attribute attribute in active) cache[attribute].Clear();
            active.Clear();
        }

        private bool TryRegisterAttribute(Attribute attribute)
        {
            if (!cache.ContainsKey(attribute)) return false;
            if (!active.Contains(attribute)) active.Add(attribute);

            return true;
        }
        
        public void Register(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            cache[attribute].Add(sourcedModifiedValue);
        }
        
        public bool TryToModified(Attribute attribute, out ModifiedAttributeValue modifiedAttributeValue)
        {
            if (!active.Contains(attribute) || !TryGetSourcedModifiers(attribute, out var sourcedModifiers))
            {
                modifiedAttributeValue = default;
                return false;
            }
            
            modifiedAttributeValue = sourcedModifiers.Aggregate(new ModifiedAttributeValue(), (current, smav) => current.Combine(smav.ToModified()));
            return true;
        }

        public bool TryGetSourcedModifiers(Attribute attribute, out List<SourcedModifiedAttributeValue> sourcedModifiers)
        {
            return cache.TryGetValue(attribute, out sourcedModifiers);
        }
        
        #endregion
        
        public void Multiply(Attribute attribute, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Multiply(operand);
            }
        }
        
        public void Multiply(Attribute attribute, ESignPolicy signPolicy, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].SignPolicy != signPolicy) continue;
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Multiply(operand);
            }
        }

        public void MultiplyAmplify(Attribute attribute, List<Tag> impactType, ESignPolicy signPolicy, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (!ForgeHelper.ValidateImpactTypes(false, impactType, cache[attribute][i].Derivation.GetImpactTypes())) continue;
                if (cache[attribute][i].SignPolicy != signPolicy) continue;
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Multiply(1 + operand);
            }
        }

        public void MultiplyAttenuate(Attribute attribute, List<Tag> impactType, ESignPolicy signPolicy, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (!ForgeHelper.ValidateImpactTypes(false, impactType, cache[attribute][i].Derivation.GetImpactTypes())) continue;
                if (cache[attribute][i].SignPolicy != signPolicy) continue;
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Multiply(1 - operand);
            }
        }
        
        public void Add(Attribute attribute, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag, bool spread = false)
        {
            if (!TryRegisterAttribute(attribute)) return;

            AttributeValue addValue = spread ? operand : operand / cache[attribute].Count;
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Add(addValue);
            }
        }

        public void Add(Attribute attribute, ESignPolicy signPolicy, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag, bool spread = false)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            AttributeValue addValue = spread ? operand : operand / cache[attribute].Count;
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].SignPolicy != signPolicy) continue;
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Add(addValue);
            }
        }
        
        public void Override(Attribute attribute, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Override(operand);
            }
        }
        
        public void Override(Attribute attribute, ESignPolicy signPolicy, AttributeValue operand, bool allowSelfModify, List<Tag> contextTags, bool anyContextTag)
        {
            if (!TryRegisterAttribute(attribute)) return;
            
            for (int i = 0; i < cache[attribute].Count; i++)
            {
                if (cache[attribute][i].SignPolicy != signPolicy) continue;
                if (cache[attribute][i].BaseDerivation.GetSource() == cache[attribute][i].BaseDerivation.GetTarget() && !allowSelfModify) continue;
                if (!anyContextTag && !cache[attribute][i].BaseDerivation.GetContextTags().ContainsAll(contextTags)) continue;
                cache[attribute][i] = cache[attribute][i].Override(operand);
            }
        }
    }

    public enum EAttributeModificationMethod
    {
        FromLast,
        FromFirst
    }
    
}
