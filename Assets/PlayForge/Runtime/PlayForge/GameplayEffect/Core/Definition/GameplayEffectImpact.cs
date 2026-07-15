using System;
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
        // IMPACT ENTRIES — DERIVED, NEVER SERIALIZED (IMPACT_MODEL_REFACTOR.md §3.1)
        //
        // The serialized authoring surface is still the single quad below (the drawer
        // edits those fields). Entries are re-derived from the quad + duration flags on
        // EVERY Generate(), so inspector edits — magnitude, attribute target, duration
        // policy, tick flags — always take effect immediately. Serializing entries was
        // a bug: OnValidate froze half-authored state into the asset (null targets,
        // stale magnitudes, deliveries minted while the policy was still Instant) and
        // never re-synced.
        //
        // When a multi-entry drawer ships, an explicitly-authored serialized entry list
        // can take precedence over this derivation — flip at that point, not before.
        // ═══════════════════════════════════════════════════════════════════════════

        [NonSerialized] private List<AttributeImpactEntry> derivedEntries;

        public IReadOnlyList<AttributeImpactEntry> Entries =>
            derivedEntries ?? (IReadOnlyList<AttributeImpactEntry>)Array.Empty<AttributeImpactEntry>();

        public bool HasEntries => derivedEntries is { Count: > 0 };

        /// <summary>
        /// Re-derive the entry list from the authored quad + duration flags. Called by
        /// <see cref="GameplayEffect.Generate"/> before every spec creation. Delivery
        /// mapping (plan §5):
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

            if (AttributeTarget == null && (MagnitudeOperation == null || MagnitudeOperation.Scaler == null && MagnitudeOperation.Magnitude == 0f))
            {
                // Nothing authored — pure tag-grant / worker-only effect.
                return;
            }

            derivedEntries.Add(new AttributeImpactEntry
            {
                AttributeTarget = AttributeTarget,
                Delivery = ResolveDelivery(duration, out _),
                TargetSlot = TargetImpact,
                Operation = ImpactOperation,
                MultiplyBehaviour = MultiplyBehaviour,
                SortOrder = 0,
                Magnitude = MagnitudeOperation, // live reference — inspector edits apply instantly
                RetentionGroupOverride = default, // falls back to Definition.RetentionGroup
                ImpactTypes = ImpactTypes,
            });
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

        public EAffiliationPolicy AffiliationPolicy = EAffiliationPolicy.AlwaysAllow;
        public EAnyAllPolicy AffiliationComparison = EAnyAllPolicy.Any;
        [ForgeTagContext(ForgeContext.Affiliation)] public List<Tag> Affiliations = new List<Tag>();

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
        public ScalerMagnitudeOperation MagnitudeOperation;

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
            AffiliationPolicy = o.AffiliationPolicy;
            ImpactTypes = o.ImpactTypes;
            ReverseImpactOnRemoval = o.ReverseImpactOnRemoval;
            MagnitudeOperation = o.MagnitudeOperation;
            
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

        public IEnumerable<GameplayEffect> GetContainedEffects(EApplyTickRemove policy)
        {
            return Packets.Where(packet => packet.Policy == policy).Select(p => p.ContainedEffect);
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
