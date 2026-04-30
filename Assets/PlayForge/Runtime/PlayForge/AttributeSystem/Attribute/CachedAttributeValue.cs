using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class CachedAttributeValue
    {
        /// <summary>
        /// IEffectOrigin.AssetTag : RCV
        /// </summary>
        private Dictionary<Tag, RetainedCachedValue> derivations = new();
        public AttributeValue ActiveValue { get; private set; }
        public AttributeBlueprint Blueprint { get; }

        private IAttributeImpactDerivation root;
        public IAttributeImpactDerivation Root => root;
        
        public IReadOnlyDictionary<Tag, RetainedCachedValue> RetainedValues => derivations;
        public AttributeValue RootValue => Blueprint.RootValue;

        private AttributeValue heldValue;
        private IAttributeImpactDerivation heldDerivation;

        public static CachedAttributeValue GenerateNull()
        {
            return new CachedAttributeValue(null);
        }

        public static CachedAttributeValue GenerateGeneric(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            var cav = GenerateNull();
            cav.Initialize(attribute, source, cache, retentionGroup, value);

            return cav;
        }
        
        public CachedAttributeValue(AttributeBlueprint blueprint)
        {
            Blueprint = blueprint;
        }

        public bool Initialize(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Blueprint is not null
                   && Initialize(
                       attribute, source, cache,
                       Blueprint.SetElement.RetentionGroup,
                       Blueprint.GetInitialValue(cache)
                   );
        }

        private bool Initialize(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            root = IAttributeImpactDerivation.GenerateSourceDerivation(
                source, attribute, 
                retentionGroup,
                new List<Tag>() { Tags.DisallowImpact }
            );
            
            ApplyModification(root, value, true);
            
            return true;
        }

        public AttributeValue RefreshActiveValue(ISource source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            var key = source.GetAssetTag();
            if (!derivations.TryGetValue(key, out var rcv)) return default;
    
            var oldValue = rcv.Value;
            var newDefaultValue = Blueprint.GetActiveValue(cache);
    
            rcv.Set(root, newDefaultValue);
    
            // Update total value
            var delta = rcv.Value - oldValue;
            ActiveValue += delta;
    
            return delta;
        }

        public void NullifyDerivation(IAttributeImpactDerivation derivation)
        {
            if (!derivations.TryGetValue(derivation.GetEffectDerivation().GetAssetTag(), out var rcv)) return;
            rcv.NullifyDerivation(derivation);
        }
        
        public void ApplyModification(IAttributeImpactDerivation derivation, AttributeValue attributeValue, bool retain)
        {
            ActiveValue += attributeValue;

            if (!retain) return;
            
            var key = derivation.GetEffectDerivation().GetAssetTag();
            if (derivations.ContainsKey(key)) derivations[key].Add(derivation, attributeValue);
            else derivations[key] = new RetainedCachedValue(derivation, attributeValue);

            heldValue = attributeValue;
            heldDerivation = derivation;
        }
        
        public void Remove(IAttributeImpactDerivation derivation, bool nullify, bool retainCurrent)
        {
            if (nullify)
            {
                NullifyDerivation(derivation);
                return;
            }
            
            var key = derivation.GetEffectDerivation().GetAssetTag();
            var delta = derivations[key].Value;
            if (retainCurrent) delta.CurrentValue = 0f;
            ActiveValue -= delta;
            if (derivations.ContainsKey(key)) derivations.Remove(key);
        }

        public void UpdateHeld(AttributeValue realImpact)
        {
            derivations[heldDerivation.GetEffectDerivation().GetAssetTag()].Set(heldDerivation, realImpact);
        }

        /// <summary>
        /// Clamp <see cref="ActiveValue"/> to the element's overflow bounds (single source of
        /// truth lives on <see cref="AttributeOverflowData.Clamp"/> so the drawer's live input
        /// clamp and this runtime clamp can never disagree).
        /// </summary>
        public void ApplyBounds()
        {
            if (!(Blueprint?.SetElement.Constraints.AutoClamp ?? false)) return;

            ActiveValue = Blueprint.SetElement.Overflow.Clamp(ActiveValue);
        }

        public void EnforceScaling(WorkerContext ctx)
        {
            if (Blueprint is null) return;
            
            if (!Blueprint.SetElement.Constraints.AutoScaleWithBase || ctx.Change.Value.BaseValue == 0) return;
            float oldBase = ActiveValue.BaseValue - ctx.Change.Value.BaseValue;
            if (Mathf.Approximately(oldBase, 0f)) return;
            float proportion = ctx.Change.Value.BaseValue / oldBase;  // change / oldBase
            
            float delta = proportion * ActiveValue.CurrentValue;

            var derivation = IAttributeImpactDerivation.GenerateSourceDerivation(
                ctx.Change.Value, Tags.IgnoreRetention, new List<Tag>() { Tags.AllowImpact });
            var scaleAmount = new SourcedModifiedAttributeValue(derivation, delta, 0f, false);
            ctx.ActionQueue.Enqueue(new ModifyAttributeAction(
                ctx.System, ctx.Change.Value.Derivation.GetAttribute(), scaleAmount, false)
            );
        }

        public void ApplyRounding()
        { 
            if (Blueprint is null) return;
            
            var newValue = Blueprint.SetElement.Constraints.RoundingMode switch
            {
                EAttributeRoundingPolicy.None => ActiveValue,
                EAttributeRoundingPolicy.ToFloor => new AttributeValue(
                    Mathf.Floor(ActiveValue.CurrentValue),
                    Mathf.Floor(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.ToCeil => new AttributeValue(
                    Mathf.Ceil(ActiveValue.CurrentValue),
                    Mathf.Ceil(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.Round => new AttributeValue(
                    Mathf.Round(ActiveValue.CurrentValue),
                    Mathf.Round(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.SnapTo => Mathf.Approximately(Blueprint.SetElement.Constraints.SnapInterval, 0f)
                    ? ActiveValue
                    : new AttributeValue(
                        Mathf.Round(ActiveValue.CurrentValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval,
                        Mathf.Round(ActiveValue.BaseValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval),
                _ => throw new ArgumentOutOfRangeException()
            };

            ActiveValue = newValue;
        }
    }
    
    public class RetainedCachedValue
    {
        public AttributeValue Value { get; private set; }

        public IAttributeImpactDerivation RootDerivation;
        public Dictionary<IAttributeImpactDerivation, AttributeValue> Derivations;
        public CachedValueInfo Info { get; }
        
        public bool HasDerivations => Derivations.Count > 0 && Derivations.Keys.Any(key => key.DerivationAlive());

        public bool CanClean => !HasDerivations && Value == default;
            
        public RetainedCachedValue(IAttributeImpactDerivation firstDerivation, AttributeValue value)
        {
            RootDerivation = firstDerivation;
            
            Derivations = new Dictionary<IAttributeImpactDerivation, AttributeValue>();
            Info = new CachedValueInfo(firstDerivation);
            
            Add(RootDerivation, value);
        }

        public void Add(IAttributeImpactDerivation derivation, AttributeValue value)
        {
            if (Derivations.TryGetValue(derivation, out _)) Derivations[derivation] += value;
            else Derivations[derivation] = value;
            
            Value += value;
        }

        public void Set(IAttributeImpactDerivation derivation, AttributeValue newValue)
        {
            Debug.Log($"Setting {derivation.GetCacheKey()} ({derivation.GetEffectDerivation().GetAssetTag()}");
            
            if (!Derivations.TryGetValue(derivation, out var oldValue)) return;
    
            var delta = newValue - oldValue;
            Derivations[derivation] = newValue;
            Value += delta;
        }
        
        public void Remove(IAttributeImpactDerivation derivation)
        {
            Debug.Log($"Removing {derivation.GetCacheKey()} ({derivation.GetSource().GetAssetTag()}");
            
            if (!Derivations.ContainsKey(derivation)) return;

            Value -= Derivations[derivation];
            Derivations.Remove(derivation);
        }

        public void Zero(IAttributeImpactDerivation derivation)
        {
            if (Derivations.TryGetValue(derivation, out _)) Derivations[derivation] = default;
        }

        public void NullifyDerivation(IAttributeImpactDerivation derivation)
        {
            if (!Derivations.TryGetValue(derivation, out var value)) return;

            Debug.Log($"Nullifying {derivation.GetCacheKey()} ({derivation.GetSource().GetAssetTag()}");
            
            Remove(derivation);
            Add(IAttributeImpactDerivation.GenerateNullifiedDerivation(derivation), value);
        }

        public bool TryGetNullified(Tag rootKey, out NullifiedImpactDerivation nullDerivation)
        {
            nullDerivation = null;
            foreach (var derivation in Derivations.Keys)
            {
                if (derivation.DerivationAlive() || derivation is not NullifiedImpactDerivation nid || nid.RootKey != rootKey) continue;
                
                nullDerivation = nid;
                return true;
            }

            return false;
        }

        public void CleanNullified()
        {
            var keys = Derivations.Keys.ToArray();
            foreach (var key in keys)
            {
                if (!key.DerivationAlive()) Derivations.Remove(key);
            }
        }
    }

    public class CachedValueInfo
    {
        public readonly Tag DerivationAssetTag = Tags.NONE;
        public readonly IEffectOrigin Origin;

        public bool HasOrigin => Origin is not null;
        
        public CachedValueInfo(IAttributeImpactDerivation derivation)
        {
            if (derivation is null) return;
            
            Origin = derivation.GetEffectDerivation();
            DerivationAssetTag = Origin.GetAssetTag();
        }
    }
}
