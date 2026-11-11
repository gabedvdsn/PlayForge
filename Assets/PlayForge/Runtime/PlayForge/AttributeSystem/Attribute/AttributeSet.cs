using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute Set", fileName = "AttributeSet_")]
    public class AttributeSet : BaseForgeLevelProvider, IWorkerGroupSource
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures = new();
        
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        
        [SerializeField]
        public List<AttributeSetElement> Attributes = new();
        
        [Space]

        public List<AttributeSet> SubSets;
        public EValueCollisionPolicy CollisionResolutionPolicy = EValueCollisionPolicy.UseMaximum;

        public StandardWorkerGroup WorkerGroup;

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Provider
        //
        // AttributeSet is a BaseForgeLevelProvider so that cached scalers authored on its
        // elements can derive their level bounds from the set itself. The set may be
        // standalone (uses its own StartingLevel/MaxLevel) or linked to another provider
        // (Item, Ability, Entity, etc.) in which case those values are forwarded.
        // ═══════════════════════════════════════════════════════════════════════════

        [Min(0)] public int StartingLevel = 1;
        [Min(1)] public int MaxLevel = 4;

        public EAttributeSetLinkMode LinkMode;

        [Tooltip("Optional link to a level provider (Entity, Ability, Item, …) to derive level bounds from")]
        [SerializeField]
        [LinkedSource]
        private BaseForgeLevelProvider _linkedSource;
        
        public void Initialize(AttributeSystemComponent system)
        {
            var meta = new AttributeSetMeta(this, system.Self);
            meta.InitializeAttributeSystem(system, this);

            // The GAS gives out the set's workers, its subsets' with them, through InitWorkers. Given here too, each
            // ran twice for whatever it saw (GAS-18)
        }

        /// <summary>
        /// This set and every set below it, each once: this set, then each sub-set's in turn, depth first, the order
        /// collisions resolve in. A set that included itself, directly or through another, sent every walk over the
        /// sub-sets round forever, and one reached by two paths counted twice (GAS-44).
        /// </summary>
        public List<AttributeSet> SelfAndSubSets()
        {
            var order = new List<AttributeSet>();
            CollectSelfAndSubSets(order, new HashSet<AttributeSet>());
            return order;
        }

        /// <summary>
        /// <see cref="SelfAndSubSets"/> into the caller's buffers, passing over any set already in
        /// <paramref name="seen"/>. It allocates nothing once the buffers have grown, for walks that run often, such as
        /// tag compiling (GAS-P2).
        /// </summary>
        public void CollectSelfAndSubSets(List<AttributeSet> into, HashSet<AttributeSet> seen)
        {
            if (!seen.Add(this)) return;
            into.Add(this);
            if (SubSets == null) return;
            for (int i = 0; i < SubSets.Count; i++)
            {
                if (SubSets[i]) SubSets[i].CollectSelfAndSubSets(into, seen);
            }
        }

        public HashSet<IAttribute> GetUnique()
        {
            var attributes = new HashSet<IAttribute>();
            foreach (var set in SelfAndSubSets())
            {
                if (set.Attributes == null) continue;
                // An element with no attribute gives none, so it isn't counted as one (GAS-54)
                foreach (var attr in set.Attributes) if (attr?.Attribute != null) attributes.Add(attr.Attribute);
            }
            return attributes;
        }
        public override string GetName()
        {
            return Name;
        }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            foreach (var set in SelfAndSubSets()) yield return set.AssetTag;
        }
        public override BaseForgeLevelProvider LinkedProvider
        {
            get => _linkedSource;
            set => _linkedSource = value;
        }

        public override bool IsLinked => LinkMode == EAttributeSetLinkMode.LinkedToProvider && LinkedProvider != null;

        public override bool LinkToProvider(BaseForgeLevelProvider provider)
        {
            if (provider == null)
            {
                Unlink();
                return true;
            }

            LinkedProvider = provider;
            LinkMode = EAttributeSetLinkMode.LinkedToProvider;
            return true;
        }

        public void Unlink()
        {
            LinkedProvider = null;
            LinkMode = EAttributeSetLinkMode.Standalone;
        }

        public override bool IsLinkedTo(ScriptableObject provider) => IsLinked && LinkedProvider == provider;

        /// <summary>Max level after following the link chain. Falls back to local <see cref="MaxLevel"/> when not linked.</summary>
        public int GetLinkedMaxLevel(bool useOwnIfNotLinked = true)
            => IsLinked ? LinkedProvider.GetMaxLevel() : (useOwnIfNotLinked ? MaxLevel : 1);

        /// <summary>Starting level after following the link chain. Falls back to local <see cref="StartingLevel"/> when not linked.</summary>
        public int GetLinkedStartingLevel(bool useOwnIfNotLinked = true)
            => IsLinked ? LinkedProvider.GetStartingLevel() : (useOwnIfNotLinked ? StartingLevel : 0);

        // ─── ILevelProvider overrides ────────────────────────────────────────────────
        public override int GetMaxLevel()      => IsLinked ? GetLinkedMaxLevel()      : MaxLevel;
        public override int GetStartingLevel() => IsLinked ? GetLinkedStartingLevel() : StartingLevel;
        public override string GetProviderName() => GetName();
        public override Tag GetAssetTag() => AssetTag;

        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }

        public void InitWorkers(ISource system)
        {
            foreach (var set in SelfAndSubSets()) set.WorkerGroup?.ProvideWorkersTo(system);
        }
        public void RemoveWorkers(ISource system)
        {
            foreach (var set in SelfAndSubSets()) set.WorkerGroup?.RemoveWorkersFrom(system);
        }
    }

    /// <summary>
    /// Defines how an AttributeSet's level range is sourced.
    /// </summary>
    public enum EAttributeSetLinkMode
    {
        /// <summary>The set defines its own StartingLevel/MaxLevel — used as-is by cached scalers.</summary>
        Standalone,
        /// <summary>Level range is forwarded from a linked provider (Item, Ability, Entity, …).</summary>
        LinkedToProvider
    }

    /// <summary>
    /// A magnitude + cached-scaler + real-magnitude triplet used for ONE side of an attribute's
    /// root value (either Current or Base). Held by <see cref="AttributeSetElement"/> twice — once
    /// per side. The scaler returns an <see cref="AttributeValue"/>; the blueprint projects to the
    /// slot this spec represents (Current → CurrentValue, Base → BaseValue).
    /// </summary>
    [Serializable]
    public class AttributeMagnitudeSpec
    {
        public float Magnitude;

        [ScalerRootAssignment(typeof(AbstractCachedScaler))]
        [SerializeReference] public AbstractCachedScaler Scaling;

        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.UseMagnitude;

        // A scaling that is no kind, as a Group with no members, reads as none: the number (ED-80)
        public bool BypassScaling => AbstractScaler.IsNone(Scaling) || RealMagnitude == EMagnitudeOperation.UseMagnitude;

        public void CopyFrom(AttributeMagnitudeSpec other)
        {
            if (other is null) return;
            Magnitude = other.Magnitude;
            Scaling = other.Scaling;
            RealMagnitude = other.RealMagnitude;
        }
    }

    [Serializable]
    public class AttributeSetElement
    {
        public Attribute Attribute;

        // Current and Base each get their own magnitude / scaler / real-magnitude config.
        // The Target field has been removed — author each side independently.
        public AttributeMagnitudeSpec Current = new();
        public AttributeMagnitudeSpec Base = new();

        /// <summary>
        /// When true, Current is treated as a live mirror of Base — both runtime resolution
        /// and the editor display ignore Current's own values and use Base's instead. Useful
        /// for stat attributes where Current should always equal Base by definition.
        /// </summary>
        public bool LinkCurrentToBase;

        /// <summary>The spec actually used for the Current side (Base when linked, Current otherwise).</summary>
        public AttributeMagnitudeSpec EffectiveCurrent => LinkCurrentToBase ? Base : Current;

        public AttributeOverflowData Overflow;

        [ForgeTagContext(ForgeContext.RetentionGroup)]
        public Tag RetentionGroup = Tags.DEFAULT;

        public EAttributeElementCollisionPolicy CollisionPolicy;

        public AttributeConstraints Constraints = new();

        public AttributeValue ValueFromMagnitude => new(EffectiveCurrent.Magnitude, Base.Magnitude);
    }

    public enum EAttributeElementCollisionPolicy
    {
        UseSetCollisionSetting,
        UseThis,
        UseExisting,
        Combine
    }
    
    /// <summary>
    /// Bounds policy for an attribute's Current and Base slots. The same instance is used both
    /// at runtime (via <see cref="CachedAttributeValue.ApplyBounds"/>) and authoring time (the
    /// AttributeSetElement drawer clamps user input live) — all clamping flows through
    /// <see cref="Clamp"/> / the per-slot min/max helpers so policy semantics are defined once.
    ///
    /// Per-policy semantics:
    ///   • Unlimited    — no bounds.
    ///   • ZeroToBase   — Current ∈ [0, Base];                Base unbounded.
    ///   • FloorToBase  — Current ∈ [Floor.Current, Base];    Base ∈ [Floor.Base, +∞).
    ///   • ZeroToCeil   — Current ∈ [0, Ceil.Current];        Base ∈ [0, Ceil.Base].
    ///   • FloorToCeil  — Current ∈ [Floor.Current, Ceil.Current]; Base ∈ [Floor.Base, Ceil.Base].
    /// </summary>
    [Serializable]
    public struct AttributeOverflowData
    {
        public EAttributeOverflowPolicy Policy;
        public AttributeValue Floor;
        public AttributeValue Ceil;

        /// <summary>Clamp an entire <see cref="AttributeValue"/> to the policy's bounds. Base is clamped
        /// first so Current can be clamped against the post-clamp Base for Base-relative policies.</summary>
        public AttributeValue Clamp(AttributeValue value)
        {
            float clampedBase = Mathf.Clamp(value.BaseValue, BaseMin, BaseMax);
            float clampedCurrent = Mathf.Clamp(value.CurrentValue, CurrentMin(clampedBase), CurrentMax(clampedBase));
            return new AttributeValue(clampedCurrent, clampedBase);
        }

        /// <summary>Clamp a single Base value.</summary>
        public float ClampBase(float baseValue) => Mathf.Clamp(baseValue, BaseMin, BaseMax);

        /// <summary>Clamp a single Current value, given the Base value it sits beside (for Base-relative policies).</summary>
        public float ClampCurrent(float currentValue, float baseValue)
            => Mathf.Clamp(currentValue, CurrentMin(baseValue), CurrentMax(baseValue));

        // ─── Per-slot effective bounds ───────────────────────────────────────
        public float CurrentMin(float baseValue) => Policy switch
        {
            EAttributeOverflowPolicy.Unlimited   => Mathf.NegativeInfinity,
            EAttributeOverflowPolicy.ZeroToBase  => 0f,
            EAttributeOverflowPolicy.FloorToBase => Floor.CurrentValue,
            EAttributeOverflowPolicy.ZeroToCeil  => 0f,
            EAttributeOverflowPolicy.FloorToCeil => Floor.CurrentValue,
            _ => Mathf.NegativeInfinity
        };

        public float CurrentMax(float baseValue) => Policy switch
        {
            EAttributeOverflowPolicy.Unlimited   => Mathf.Infinity,
            EAttributeOverflowPolicy.ZeroToBase  => baseValue,
            EAttributeOverflowPolicy.FloorToBase => baseValue,
            EAttributeOverflowPolicy.ZeroToCeil  => Ceil.CurrentValue,
            EAttributeOverflowPolicy.FloorToCeil => Ceil.CurrentValue,
            _ => Mathf.Infinity
        };

        public float BaseMin => Policy switch
        {
            EAttributeOverflowPolicy.Unlimited   => Mathf.NegativeInfinity,
            EAttributeOverflowPolicy.ZeroToBase  => Mathf.NegativeInfinity,
            EAttributeOverflowPolicy.FloorToBase => Floor.BaseValue,
            EAttributeOverflowPolicy.ZeroToCeil  => 0f,
            EAttributeOverflowPolicy.FloorToCeil => Floor.BaseValue,
            _ => Mathf.NegativeInfinity
        };

        public float BaseMax => Policy switch
        {
            EAttributeOverflowPolicy.Unlimited   => Mathf.Infinity,
            EAttributeOverflowPolicy.ZeroToBase  => Mathf.Infinity,
            EAttributeOverflowPolicy.FloorToBase => Mathf.Infinity,
            EAttributeOverflowPolicy.ZeroToCeil  => Ceil.BaseValue,
            EAttributeOverflowPolicy.FloorToCeil => Ceil.BaseValue,
            _ => Mathf.Infinity
        };
    }
    
    [Serializable]
    public class AttributeConstraints
    {
        [Tooltip("Auto clamp attribute values that fall outside of bounds")]
        public bool AutoClamp = true;  // Use Overflow data for clamping
    
        [Tooltip("Auto scale current value to changes in base value")]
        public bool AutoScaleWithBase;
    
        [Tooltip("Round attribute values after changes are applied. Will artificially increase/decrease attribute impact.")]
        public EAttributeRoundingPolicy RoundingMode;
        public float SnapInterval;  // For Snap mode
    }

    public enum EAttributeOverflowPolicy
    {
        ZeroToBase,
        FloorToBase,
        ZeroToCeil,
        FloorToCeil,
        
        Unlimited
    }

    public enum EAttributeRoundingPolicy
    {
        None,
        ToFloor,
        ToCeil,
        Round,
        SnapTo
    }

    public class AttributeSetMeta
    {
        private Dictionary<IAttribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>> matrix; 

        public AttributeSetMeta(AttributeSet attributeSet, ISource owner)
        {
            matrix = new Dictionary<IAttribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>>();
            if (!attributeSet) return;
            foreach (var set in attributeSet.SelfAndSubSets()) HandleAttributeSet(set, owner);
        }

        // One set's elements; the constructor walks the sets, each once (GAS-44)
        private void HandleAttributeSet(AttributeSet attributeSet, ISource owner)
        {
            foreach (AttributeSetElement element in attributeSet.Attributes)
            {
                // An element with no attribute, as a list's "+" leaves one, gives nothing. As a dictionary key it threw,
                // and the entity got none of its attributes (GAS-54)
                if (element?.Attribute == null) continue;
                if (!matrix.TryGetValue(element.Attribute, out var table))
                {
                    table = matrix[element.Attribute] = new();
                }

                if (!table.ContainsKey(element.CollisionPolicy))
                {
                    matrix[element.Attribute][element.CollisionPolicy] = new List<AttributeBlueprint>() { new AttributeBlueprint(element, owner) };
                }
                else matrix[element.Attribute][element.CollisionPolicy].Add(new AttributeBlueprint(element, owner));
            }
        }

        public void InitializeAttributeSystem(AttributeSystemComponent system, AttributeSet attributeSet)
        {
            foreach (IAttribute attribute in matrix.Keys)
            {
                if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseSetCollisionSetting, out var defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseThis, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.Combine, out defaults))
                {
                    AttributeBlueprint blueprint = null;
                    if (defaults.Count > 0)
                    {
                        // The first takes in the others, once each. It took itself, and each of the others twice (GAS-27)
                        blueprint = defaults[0];
                        for (int i = 1; i < defaults.Count; i++)
                        {
                            blueprint.Combine(defaults[i]);
                        }
                    }

                    system.ProvideAttribute(attribute, blueprint);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseExisting, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
            }
        }

        private void InitializeAggregatePolicy(AttributeSystemComponent system, IAttribute attribute, List<AttributeBlueprint> defaults, EValueCollisionPolicy resolution)
        {
            switch (resolution)
            {
                case EValueCollisionPolicy.UseAverage:
                {
                    // The elements' resolved values, averaged, with bounds and rounding the first's. It took the first
                    // alone (GAS-40)
                    var blueprint = defaults[0];
                    for (int i = 1; i < defaults.Count; i++) blueprint.Average(defaults[i]);
                    system.ProvideAttribute(attribute, blueprint);
                    break;
                }
                case EValueCollisionPolicy.UseMaximum:
                {
                    int idx = defaults.IndexMax(mav => mav.RootValue.BaseValue);
                    system.ProvideAttribute(attribute, defaults[idx]);
                    break;
                }
                case EValueCollisionPolicy.UseMinimum:
                {
                    int idx = defaults.IndexMin(mav => mav.RootValue.BaseValue);
                    system.ProvideAttribute(attribute, defaults[idx]);
                    break;
                }
                case EValueCollisionPolicy.UseFirst:
                {
                    var bp = defaults.First();
                    system.ProvideAttribute(attribute, bp);
                    break;
                }
                case EValueCollisionPolicy.UseLast:
                {
                    var bp = defaults.Last();
                    system.ProvideAttribute(attribute, bp);
                    break;
                }
                // The values that would override the first are passed by, as a scaler group passes its overrides by, so
                // the first stands. It fell through to the default and threw (GAS-40)
                case EValueCollisionPolicy.Ignore:
                {
                    system.ProvideAttribute(attribute, defaults.First());
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }
    }
}
