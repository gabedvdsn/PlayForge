using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Analysis workers run at end-of-frame after all other work is complete.
    /// Used for death checks, state evaluation, and other post-frame analysis.
    /// 
    /// Analysis workers can either:
    /// 1. Return actions to be queued (preferred for deferred execution)
    /// 2. Perform direct actions via the legacy Activate() method
    /// </summary>
    [Serializable]
    public abstract class AbstractAnalysisWorker
    {
        /// <summary>
        /// Analyze the system state and return any actions to queue.
        /// Called after all queued actions and tag workers have processed.
        /// </summary>
        /// <param name="system">The gameplay ability system to analyze</param>
        /// <param name="frameSummary">Summary of all frame activity for context</param>
        /// <returns>Actions to queue, or empty if none</returns>
        public virtual IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            return Enumerable.Empty<IRootAction>();
        }

        protected const string MY_KEY_PREFIX = "Analysis_Worker_";

        // This worker's own, made as it's first asked. It was its type's name, so two workers of a type on one entity,
        // as thresholds at a half and a quarter, shared it, and the first to fire marked the other (GAS-76)
        [NonSerialized] private Tag myKey;
        [NonSerialized] private bool keyMade;
        private static int keys;

        protected Tag MyKey
        {
            get
            {
                if (keyMade) return myKey;
                myKey = Tag.GenerateAsUnique($"{GetType().Name}_{++keys}", MY_KEY_PREFIX);
                keyMade = true;
                return myKey;
            }
        }
        
        protected virtual bool HasTriggered(ILocalDataUser system)
        {
            return system.TryGetLocalData(MyKey, out var _hasTriggered) && _hasTriggered.boolValue;
        }

        protected virtual void SetTriggered(ILocalDataUser system)
        {
            system.SetLocalData(MyKey, new DataWrapper(){ boolValue = true });
        }

        protected virtual void ResetTriggered(ILocalDataUser system)
        {
            if (!system.TryGetLocalData(MyKey, out var data)) return;
            data.boolValue = false;
        }

        /// <summary>
        /// Ready to fire again, as an entity that revives readies its death watcher for its next death (TW-4). Each worker
        /// that fires once is re-armed so; one that keeps more overrides it.
        /// </summary>
        public virtual void Rearm(ILocalDataUser system) => ResetTriggered(system);
    }

    [Serializable]
    public class DeathWatcherAnalysisWorker : ThresholdAnalysisWorker
    {
        protected override IEnumerable<IRootAction> OnThresholdReached(IGameplayAbilitySystem system, AttributeValue value, FrameSummary frameSummary)
        {
            // Its effects and its event, where it has them, as it dies (TW-11)
            if (Effects is { Count: > 0 } || Event.IsValid)
                foreach (var action in base.OnThresholdReached(system, value, frameSummary)) yield return action;
            yield return new DeathAction(system);
        }
    }
    
    /// <summary>
    /// Analysis worker that triggers callbacks based on attribute thresholds.
    /// </summary>
    [Serializable]
    public class ThresholdAnalysisWorker : AbstractAnalysisWorker
    {
        [Tooltip("The attribute it watches")]
        public Attribute Target;

        [Tooltip("Which retention groups' parts it reads: all of them, the whole value, unless it says (TW-3)")]
        public RetentionGroups Groups = new();

        [Tooltip("What of it it reads: its current, its base, what's missing of it, or its share of its base, as half its " +
                 "Health at a Threshold of 0.5 (TW-11)")]
        public EAttributeTargetBinary What = EAttributeTargetBinary.Current;

        [Tooltip("The value it watches for")]
        public float Threshold;

        [Tooltip("As it fires: while its comparison holds, as a threshold always did; or as what it reads crosses its " +
                 "threshold, falling to it or below, rising above it, or either way (TW-11)")]
        public EThresholdCrossing Crossing = EThresholdCrossing.WhileItHolds;

        [Tooltip("How what it reads compares with its threshold, where it fires while that holds")]
        public EThresholdComparison Comparison = EThresholdComparison.LessThanOrEqual;

        [Tooltip("Fires once; off, each time: each crossing, or each frame its comparison holds")]
        public bool TriggerOnce = true;

        [Tooltip("The effects it applies to the entity as it fires, as a rage at half Health (TW-11)")]
        public List<GameplayEffect> Effects = new();

        [Tooltip("Whom its effects come from: the entity itself, as a rage; or the one whose hit crossed it, so a shatter " +
                 "is its caster's, kill and all (TW-11)")]
        public EThresholdSource AppliedBy = EThresholdSource.Itself;

        [Tooltip("A gameplay event it sends the entity as it fires, for its abilities' triggers: its source the one whose " +
                 "change crossed it, its subject the entity. None, none (TW-11)")]
        [ForgeTagContext(ForgeContext.Event)]
        public Tag Event;
        
        public override IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            if (Target is null) yield break;
            if (TriggerOnce && HasTriggered(system)) yield break;
            if (!RetentionGroups.TryRead(Groups, system, Target, out AttributeValue value)) yield break;
            
            // What it reads of the attribute: its current, as it always read, its base, what's missing or its share (TW-11)
            float read = AttributeParts.Read(value, What);

            bool fires;
            if (Crossing == EThresholdCrossing.WhileItHolds)
            {
                fires = Comparison switch
                {
                    EThresholdComparison.LessThan => read < Threshold,
                    EThresholdComparison.LessThanOrEqual => read <= Threshold,
                    EThresholdComparison.GreaterThan => read > Threshold,
                    EThresholdComparison.GreaterThanOrEqual => read >= Threshold,
                    EThresholdComparison.Equal => Math.Abs(read - Threshold) < 0.001f,
                    _ => false
                };
            }
            else
            {
                // A crossing is a change of side since its last check; its first check only notes its side (TW-11)
                bool above = read > Threshold;
                bool known = TryGetSide(system, out bool wasAbove);
                SetSide(system, above);
                fires = known && wasAbove != above && Crossing switch
                {
                    EThresholdCrossing.Falling => !above,
                    EThresholdCrossing.Rising => above,
                    _ => true,
                };
            }

            if (!fires) yield break;
            if (TriggerOnce && HasTriggered(system)) yield break;
            if (TriggerOnce) SetTriggered(system);

            // Return actions from derived implementation
            foreach (var action in OnThresholdReached(system, value, frameSummary))
            {
                yield return action;
            }
        }

        /// <summary>
        /// What it does as it fires (TW-11): its effects on the entity, from the entity or from the one whose change
        /// crossed it, as it says, and its event to the entity, its source the one who crossed it; with neither, it logs,
        /// as it did. Override it for more.
        /// </summary>
        protected virtual IEnumerable<IRootAction> OnThresholdReached(
            IGameplayAbilitySystem system,
            AttributeValue value,
            FrameSummary frameSummary)
        {
            bool acts = false;
            var crosser = Crosser(system, frameSummary);
            if (Effects is { Count: > 0 } && system is ITarget onto)
            {
                ISource from = AppliedBy == EThresholdSource.Crosser && crosser != null ? crosser : system;
                var origin = IEffectOrigin.GenerateSourceDerivation(from);
                foreach (var effect in Effects)
                {
                    if (effect == null) continue;
                    acts = true;
                    yield return new ApplyEffectAction(onto, effect, origin);
                }
            }

            if (Event.IsValid)
            {
                acts = true;
                var abilities = system.GetAbilitySystem();
                var tag = Event;
                yield return new LambdaAction(_ =>
                {
                    if (abilities == null || !abilities.ListensFor(tag)) return;
                    var data = ProcessDataPacket.Default();
                    if (crosser is ITarget source) data.SetPrimary(Tags.SOURCE, source);
                    if (system is ITarget subject) data.SetPrimary(Tags.EVENT_SUBJECT, subject);
                    abilities.SendGameplayEvent(tag, data);
                }, $"Send {tag}");
            }

            if (!acts) yield return new LogAction($"Threshold reached! {Target}-{Threshold}");
        }

        // Who crossed it: the source of the last workable change from another to its attribute this frame, a loss where it
        // fires falling, a gain where it fires rising (TW-11)
        private ISource Crosser(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            var received = frameSummary?.ImpactsReceived;
            if (received == null || Target == null) return null;
            for (int i = received.Count - 1; i >= 0; i--)
            {
                var impact = received[i];
                var source = impact.SourcedModifier.Source;
                if (!impact.SourcedModifier.ImpactIsWorkable || source == null || ReferenceEquals(source, system)) continue;
                if (!Target.Equals(impact.Attribute)) continue;
                float change = impact.RealImpact.CurrentValue != 0f ? impact.RealImpact.CurrentValue : impact.RealImpact.BaseValue;
                if (Crossing == EThresholdCrossing.Falling && change >= 0f) continue;
                if (Crossing == EThresholdCrossing.Rising && change <= 0f) continue;
                return source;
            }
            return null;
        }

        // Its side of its threshold at its last check, for a crossing, kept by the entity: none till its first check (TW-11)
        [NonSerialized] private Tag sideKey;
        [NonSerialized] private bool sideKeyMade;
        private static int sideKeys;

        private Tag SideKey
        {
            get
            {
                if (sideKeyMade) return sideKey;
                sideKey = Tag.GenerateAsUnique($"Threshold_Side_{++sideKeys}", MY_KEY_PREFIX);
                sideKeyMade = true;
                return sideKey;
            }
        }

        private bool TryGetSide(ILocalDataUser system, out bool above)
        {
            above = false;
            if (!system.TryGetLocalData(SideKey, out var data) || data.intValue == 0) return false;
            above = data.intValue == 2;
            return true;
        }

        private void SetSide(ILocalDataUser system, bool above)
        {
            if (system.TryGetLocalData(SideKey, out var data)) data.intValue = above ? 2 : 1;
            else system.SetLocalData(SideKey, new DataWrapper { intValue = above ? 2 : 1 });
        }

        /// <summary>Ready to fire again, its side forgotten, so a revive's restoring isn't a crossing (TW-11).</summary>
        public override void Rearm(ILocalDataUser system)
        {
            base.Rearm(system);
            if (system.TryGetLocalData(SideKey, out var data)) data.intValue = 0;
        }
    }
    
    public enum EThresholdComparison
    {
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal
    }

    /// <summary>As a threshold fires (TW-11).</summary>
    public enum EThresholdCrossing
    {
        /// <summary>While its comparison holds, as a threshold always did.</summary>
        WhileItHolds,

        /// <summary>As what it reads falls to the threshold or below, from above it.</summary>
        Falling,

        /// <summary>As what it reads rises above the threshold, from it or below.</summary>
        Rising,

        /// <summary>As what it reads crosses the threshold either way.</summary>
        Either,
    }

    /// <summary>Whom a threshold's effects come from (TW-11).</summary>
    public enum EThresholdSource
    {
        /// <summary>The entity itself.</summary>
        Itself,

        /// <summary>The one whose change crossed it, this frame, from another; else the entity itself.</summary>
        Crosser,
    }
}