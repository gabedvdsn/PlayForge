using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffectImpact
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // IMPACT ENTRIES — REBUILT ON EVERY Generate() (IMPACT_MODEL_REFACTOR.md §3.1)
        //
        // The first entry is derived from the single quad below and the duration flags,
        // and never serialized, so inspector edits — magnitude, attribute target, duration
        // policy, tick flags — always take effect immediately. Serializing it was a bug:
        // OnValidate froze half-authored state into the asset (null targets, stale
        // magnitudes, deliveries minted while the policy was still Instant) and never
        // re-synced.
        //
        // The entries after it are AdditionalImpacts, authored as entries, each with its
        // own delivery, now that a drawer edits them (ED-3).
        // ═══════════════════════════════════════════════════════════════════════════

        [NonSerialized] private List<AttributeImpactEntry> derivedEntries;

        public IReadOnlyList<AttributeImpactEntry> Entries =>
            derivedEntries ?? (IReadOnlyList<AttributeImpactEntry>)Array.Empty<AttributeImpactEntry>();

        public bool HasEntries => derivedEntries is { Count: > 0 };

        /// <summary>
        /// Rebuild the entry list: the one the authored quad + duration flags give, then each of the
        /// <see cref="AdditionalImpacts"/>. Called by <see cref="GameplayEffect.Generate"/> before every spec
        /// creation. The quad's delivery mapping (plan §5):
        ///   Instant → OnApply;
        ///   durational/infinite without ticks → Continuous;
        ///   with periodic ticks → Periodic;
        ///   the TickOnApplication-only trap → Continuous for Multiply/Override
        ///   (near-certainly an authored buff), OnApply otherwise.
        /// </summary>
        public void RebuildEntries(GameplayEffectDuration duration)
        {
            derivedEntries ??= new List<AttributeImpactEntry>(1);
            derivedEntries.Clear();

            // Nothing authored in the quad: a tag-grant or worker-only effect, or one whose impacts are all additional
            if (AttributeTarget != null || MagnitudeOperation != null && (MagnitudeOperation.Scaler != null || MagnitudeOperation.Magnitude != 0f))
            {
                derivedEntries.Add(new AttributeImpactEntry
                {
                    AttributeTarget = AttributeTarget,
                    Delivery = ResolveDelivery(duration, out _),
                    TargetSlot = TargetImpact,
                    Operation = ImpactOperation,
                    MultiplyBehaviour = MultiplyBehaviour,
                    AddBehaviour = AddBehaviour,
                    SortOrder = 0,
                    Magnitude = MagnitudeOperation, // live reference — inspector edits apply instantly
                    RetentionGroupOverride = default, // falls back to Definition.RetentionGroup
                    ImpactTypes = ImpactTypes,
                });
            }

            // Then each additional impact as authored, live, as the quad's magnitude is (ED-3). One with no attribute, as
            // a new row leaves it, changes nothing, and one whose delivery the duration can't make never happens, as the
            // drawer says in place
            if (AdditionalImpacts == null) return;
            foreach (var impact in AdditionalImpacts)
            {
                if (impact?.AttributeTarget == null || !impact.IsDeliveryValidFor(duration)) continue;
                derivedEntries.Add(impact);
            }
        }

        /// <summary>
        /// Delivery derivation from the authored duration flags. <paramref name="warning"/>
        /// is non-null for authoring shapes worth reviewing (used by the audit tool —
        /// deliberately NOT logged here, this runs on every spec generation).
        /// </summary>
        public EImpactDelivery ResolveDelivery(GameplayEffectDuration duration, out string warning)
        {
            warning = null;
            if (duration == null || duration.DurationPolicy == EEffectDurationPolicy.Instant)
                return EImpactDelivery.OnApply;

            if (duration.EnablePeriodicTicks)
            {
                if (ReverseImpactOnRemoval)
                {
                    warning = "Periodic effect sets ReverseImpactOnRemoval — tick rollback no longer exists " +
                              "(commits are permanent). If this was a transient buff, author it without ticks (Continuous).";
                }
                return EImpactDelivery.Periodic;
            }

            if (duration.TickOnApplication)
            {
                // The accidental-permanent-buff trap: a durational effect that pulsed
                // once on apply. Multiplies/overrides are near-certainly meant as
                // reversing buffs → Continuous. Additive pulses keep commit semantics.
                if (ImpactOperation is ECalculationOperation.Multiply or ECalculationOperation.Override)
                {
                    warning = "Durational effect with TickOnApplication-only and a Multiply/Override op is treated " +
                              "as a Continuous modifier — disable TickOnApplication to silence this.";
                    return EImpactDelivery.Continuous;
                }
                return EImpactDelivery.OnApply;
            }

            return EImpactDelivery.Continuous;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // AUTHORED IMPACT — the serialized single-impact surface (drawer edits these)
        // ═══════════════════════════════════════════════════════════════════════════

        public Attribute AttributeTarget;
        public EAttributeTarget TargetImpact;
        public ECalculationOperation ImpactOperation = ECalculationOperation.Add;

        /// <summary>
        /// Only meaningful when <see cref="ImpactOperation"/> is Multiply. Controls how this
        /// multiply impacter composes with multiply impacters from OTHER carriers acting on
        /// the same attribute slot. See <see cref="EMultiplyBehaviour"/> for semantics.
        /// </summary>
        public EMultiplyBehaviour MultiplyBehaviour = EMultiplyBehaviour.Multiplicative;

        /// <summary>
        /// Only meaningful when <see cref="ImpactOperation"/> is Add: how this add combines with the others on the attribute,
        /// as its role says, a plain sum, or diminishing (TW-1).
        /// </summary>
        public EAddBehaviour AddBehaviour = EAddBehaviour.AttributeRole;

        public EAffiliationPolicy AffiliationPolicy = EAffiliationPolicy.AlwaysAllow;
        public EAnyAllPolicy AffiliationComparison = EAnyAllPolicy.Any;
        [ForgeTagContext(ForgeContext.Affiliation)] public List<Tag> Affiliations = new List<Tag>();

        /// <summary>
        /// How likely the effect is to land, from 0 to 1, drawn from <see cref="ForgeRandom"/> once each time it's
        /// applied; a miss is refused, as an immunity refuses. 1, the default, always lands and draws nothing. An effect
        /// always landed, so a strike couldn't bleed one time in three but through a worker (FEAT-8).
        /// </summary>
        public ScalableFloat ChanceToApply = new() { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude };

        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes;

        /// <summary>
        /// Drives both the retention rule and the per-tick rollback path:
        /// <list type="bullet">
        ///   <item>For Current-only impacters, set true to be retained as a sustained modifier
        ///   (auto-reverses on removal via Recompute).</item>
        ///   <item>For periodic-tick contributions committed past the impacter list, set true
        ///   to enqueue a synthetic rollback carrier on container removal that subtracts
        ///   <see cref="TrackedImpact"/> from the attribute's one-shot history.</item>
        ///   <item>Redundant for Base-touching impacters (Base contributions are always retained).
        ///   The drawer flags this with a warning.</item>
        ///   <item>Meaningless for instant-policy effects (no removal phase). Drawer hides.</item>
        /// </list>
        /// </summary>
        public bool ReverseImpactOnRemoval;

        [ScalerOperationKeyword("Magnitude")]
        public ScalableFloat MagnitudeOperation;

        /// <summary>
        /// The effect's further impacts, after the one above: each with its own attribute, operation, magnitude and
        /// delivery, so one effect can lower armor while it lasts and burn each tick. The one above takes its delivery
        /// from the duration; each of these names its own (ED-3).
        /// </summary>
        public List<AttributeImpactEntry> AdditionalImpacts = new();

        public ContainedEffectPacket[] Packets;

        public GameplayEffectImpact()
        {
        }

        public GameplayEffectImpact(GameplayEffectImpact o)
        {
            // Entries are derived from the authored fields — copying those suffices.
            AttributeTarget = o.AttributeTarget;
            TargetImpact = o.TargetImpact;
            ImpactOperation = o.ImpactOperation;
            MultiplyBehaviour = o.MultiplyBehaviour;
            AddBehaviour = o.AddBehaviour;
            AffiliationPolicy = o.AffiliationPolicy;
            AffiliationComparison = o.AffiliationComparison;
            Affiliations = o.Affiliations is null ? null : new List<Tag>(o.Affiliations);
            ImpactTypes = o.ImpactTypes;
            ReverseImpactOnRemoval = o.ReverseImpactOnRemoval;
            MagnitudeOperation = o.MagnitudeOperation;
            ChanceToApply = o.ChanceToApply;
            // Each its own copy, as the affiliation list is; a copy shares a magnitude, as the quad's does (ED-3)
            AdditionalImpacts = o.AdditionalImpacts is null
                ? new List<AttributeImpactEntry>()
                : o.AdditionalImpacts.Select(impact => impact is null ? null : new AttributeImpactEntry(impact)).ToList();

            // The affiliation above was left behind, and an impact with no packets threw here (ED-14)
            if (o.Packets is null) return;

            Packets = new ContainedEffectPacket[o.Packets.Length];
            for (int i = 0; i < o.Packets.Length; i++)
            {
                Packets[i] = new ContainedEffectPacket()
                {
                    Policy = o.Packets[i].Policy,
                    ContainedEffect = o.Packets[i].ContainedEffect
                };
            }
        }

        public void ApplyImpactSpecifications(GameplayEffectSpec spec)
        {
            MagnitudeOperation?.Scaler?.Initialize(spec);
            ChanceToApply?.Scaler?.Initialize(spec);

            foreach (var entry in Entries)
            {
                entry?.Magnitude?.Scaler?.Initialize(spec);
            }
        }

        public float GetMagnitude(GameplayEffectSpec spec)
        {
            return MagnitudeOperation.Evaluate(spec);
        }
        
        public float GetMagnitude(AbstractStackingEffectContainer container)
        {
            float calculatedMagnitude = MagnitudeOperation.Scaler?.Evaluate(container) ?? 0f;
            
            return MagnitudeOperation.RealMagnitude switch
            {
                EMagnitudeOperation.AddScaler => MagnitudeOperation.Magnitude + calculatedMagnitude,
                EMagnitudeOperation.MultiplyWithScaler => MagnitudeOperation.Magnitude * calculatedMagnitude,
                EMagnitudeOperation.UseMagnitude => MagnitudeOperation.Magnitude,
                EMagnitudeOperation.UseScaler => calculatedMagnitude,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// The effects contained for <paramref name="policy"/>, which <c>foreach</c> walks in place without allocating.
        /// A LINQ chain built them on every call, and a periodic effect asked on every tick (GAS-P4).
        /// </summary>
        public ContainedEffects GetContainedEffects(EApplyTickRemove policy) => new ContainedEffects(Packets, policy);
    }

    /// <summary>The contained effects of one policy, read from the packets as they are walked.</summary>
    public readonly struct ContainedEffects : IEnumerable<GameplayEffect>
    {
        private readonly ContainedEffectPacket[] packets;
        private readonly EApplyTickRemove policy;

        public ContainedEffects(ContainedEffectPacket[] packets, EApplyTickRemove policy)
        {
            this.packets = packets;
            this.policy = policy;
        }

        public Enumerator GetEnumerator() => new Enumerator(packets, policy);

        IEnumerator<GameplayEffect> IEnumerable<GameplayEffect>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<GameplayEffect>
        {
            private readonly ContainedEffectPacket[] packets;
            private readonly EApplyTickRemove policy;
            private int index;

            public Enumerator(ContainedEffectPacket[] packets, EApplyTickRemove policy)
            {
                this.packets = packets;
                this.policy = policy;
                index = -1;
            }

            public GameplayEffect Current => packets[index].ContainedEffect;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (packets == null) return false;
                while (++index < packets.Length)
                {
                    if (packets[index].Policy == policy) return true;
                }
                return false;
            }

            public void Reset() => index = -1;

            public void Dispose() { }
        }
    }
    
    [Serializable]
    public struct ContainedEffectPacket
    {
        public EApplyTickRemove Policy;
        public GameplayEffect ContainedEffect;
    }

    public enum EMagnitudeOperation
    {
        MultiplyWithScaler,
        AddScaler,
        UseMagnitude,
        UseScaler
    }
    
    public enum EAttributeTargetLimited
    {
        CurrentAndBase,
        Base
    }

    public enum EAttributeTargetBinary
    {
        Current,
        Base
    }

    public enum EAttributeTarget
    {
        Current,
        Base,
        CurrentAndBase
    }

    public enum EAttributeTargetExpanded
    {
        Current,
        Base,
        CurrentAndBase,
        CurrentOrBase
    }

    public enum EApplyTickRemove
    {
        OnApply,
        OnTick,
        OnRemove
    }

    public enum EAffiliationPolicy
    {
        UseAffiliationList,
        Unaffiliated,
        Affiliated,
        AlwaysAllow
    }
}
