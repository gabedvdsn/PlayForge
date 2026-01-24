using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Attribute impact derivations are sources of attribute impact (impact carriers)
    /// </summary>
    public interface IAttributeImpactDerivation
    {
        public Attribute GetAttribute();
        public IEffectOrigin GetEffectDerivation();
        public ISource GetSource();
        public ITarget GetTarget();
        public List<Tag> GetImpactTypes();
        public Tag AttributeRetention();
        public void TrackImpact(ImpactData impactData);
        public TrackedImpact GetTrackedImpact();
        public AttributeValue GetLastTrackedImpact();
        public List<Tag> GetContextTags();
        public void RunWorkerApplication(EffectWorkerContext ctx);
        public void RunWorkerTick(EffectWorkerContext ctx);
        public void RunWorkerRemoval(EffectWorkerContext ctx);
        public void RunWorkerImpact(EffectWorkerContext ctx);
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes();
        public IAttributeImpactDerivation GetImpactDerivation();
        
        public static SourceAttributeImpact GenerateSourceDerivation(ISource source, Attribute attribute, Tag retention, Tag impactType)
        {
            return new SourceAttributeImpact(source, attribute, impactType, retention);
        }

        public static SourceAttributeImpact GenerateSourceDerivation(SourcedModifiedAttributeValue sourceModifier, Tag retention, Tag impactType)
        {
            return GenerateSourceDerivation(sourceModifier.Derivation.GetSource(), sourceModifier.Derivation.GetAttribute(), retention, impactType);
        }
    }

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

    public class SourceAttributeImpact : IAttributeImpactDerivation
    {
        private ISource Source;
        public Attribute Attribute;
        private Tag ImpactType;
        private Tag Retention;

        public SourceAttributeImpact(ISource source, Attribute attribute, Tag impactType, Tag retention)
        {
            Source = source;
            Attribute = attribute;
            ImpactType = impactType;
            Retention = retention;
        }

        public Attribute GetAttribute()
        {
            return Attribute;
        }
        public IEffectOrigin GetEffectDerivation()
        {
            return IEffectOrigin.GenerateSourceDerivation(Source);
        }
        public ISource GetSource()
        {
            return Source;
        }
        public ITarget GetTarget()
        {
            return Source;
        }
        public List<Tag> GetImpactTypes()
        {
            return new List<Tag>(){ ImpactType };
        }

        public Tag AttributeRetention()
        {
            return Retention;
        }
        
        public void TrackImpact(ImpactData impactData)
        {
            // Source derivations do not track their impact
        }
        
        public TrackedImpact GetTrackedImpact()
        {
            return new TrackedImpact();
        }
        public AttributeValue GetLastTrackedImpact()
        {
            return default;
        }
        public List<Tag> GetContextTags()
        {
            return Source.GetContextTags();
        }
        public void RunWorkerApplication(EffectWorkerContext ctx)
        {
            
        }
        public void RunWorkerTick(EffectWorkerContext ctx)
        {
            
        }
        public void RunWorkerRemoval(EffectWorkerContext ctx)
        {
            
        }
        public void RunWorkerImpact(EffectWorkerContext ctx)
        {
            
        }
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return new();
        }
        public IAttributeImpactDerivation GetImpactDerivation()
        {
            return null;
        }
    }
}
