using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Which effects a modifier changes: those its holder applies, those landing on it, or both (TW-7).</summary>
    public enum EModifierDirection
    {
        /// <summary>The holder's own effects, as it applies them, on another or itself.</summary>
        Outgoing,

        /// <summary>The effects landing on the holder, whoever applies them.</summary>
        Incoming,

        /// <summary>Both.</summary>
        Both,
    }

    /// <summary>
    /// What a modifier changes, and of which effects (TW-7, Decision 47): the effects it picks, by tag, its direction and
    /// a condition, and its changes to each effect as it's made, which last as long as that application does.
    /// </summary>
    [Serializable]
    public class EffectModifierRule
    {
        [Tooltip("The holder's own effects as it applies them, those landing on it, or both")]
        public EModifierDirection Direction = EModifierDirection.Outgoing;

        [Tooltip("The effects it changes: one with any of these among its asset tag and context tags, or a child of one. None, every effect")]
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag)]
        public List<Tag> EffectTags = new();

        [Tooltip("Only while this holds, read as the effect is made: its caster the caster, its target the target")]
        [SerializeReference] public GameplayCondition When;

        [Tooltip("What it changes of each effect it picks")]
        [SerializeReference] public List<AbstractEffectChange> Changes = new();

        /// <summary>Whether it picks an effect: by its tags, children included; an empty list picks every effect.</summary>
        public bool Picks(GameplayEffect effect)
        {
            if (effect == null) return false;
            if (EffectTags == null || EffectTags.Count == 0) return true;
            var tags = effect.Tags;
            foreach (var wanted in EffectTags)
            {
                if (!wanted.IsValid) continue;
                if (tags.AssetTag.IsValid && tags.AssetTag.MatchesOrIsChildOf(wanted)) return true;
                if (tags.ContextTags == null) continue;
                foreach (var tag in tags.ContextTags)
                    if (tag.IsValid && tag.MatchesOrIsChildOf(wanted)) return true;
            }
            return false;
        }

        /// <summary>Whether it goes in a direction: an outgoing one for the effect's source, incoming for its target.</summary>
        public bool Goes(bool outgoing) => Direction == EModifierDirection.Both || (Direction == EModifierDirection.Outgoing) == outgoing;
    }

    /// <summary>
    /// A modifier an item, an ability, an effect or an entity holds (TW-7): its own rule, or a shared Effect Modifier's,
    /// which it names in place of its own.
    /// </summary>
    [Serializable]
    public class EffectModifierEntry
    {
        [Tooltip("A shared Effect Modifier, used in place of this entry's own rule")]
        public EffectModifier Shared;

        public EffectModifierRule Rule = new();

        /// <summary>The rule in force: the shared one's where one is named, or its own.</summary>
        public EffectModifierRule Resolved => Shared ? Shared.Rule : Rule;
    }

    /// <summary>
    /// One change a modifier makes to an effect as it's made (TW-7). Each writes into the application's
    /// <see cref="EffectChanges"/>, which the effect's own reads take in; a project adds its own by deriving one.
    /// </summary>
    [Serializable]
    public abstract class AbstractEffectChange
    {
        /// <summary>Writes its change into the application's changes, reading its numbers for the application.</summary>
        public virtual void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule) { }

        /// <summary>The change in a line, as an inspector shows it.</summary>
        public virtual string Describe() => GetType().Name;
    }

    /// <summary>Adds a contained effect to it, landing on its target as it lands, each tick, or as it ends.</summary>
    [Serializable]
    public class AddContainedEffectChange : AbstractEffectChange
    {
        [Tooltip("The effect it adds")]
        public GameplayEffect Effect;

        [Tooltip("When it lands: as the effect lands, each tick, or as it ends")]
        public EApplyTickRemove When = EApplyTickRemove.OnApply;

        [Tooltip("Whom it lands on: the effect's target, or its caster (TW-14)")]
        public EEffectRecipient LandsOn = EEffectRecipient.Target;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            if (Effect) changes.Packets.Add(new EffectChanges.Packet(When, Effect, rule, LandsOn));
        }

        public override string Describe() => Effect
            ? $"Adds {Effect.GetName()} {EffectChanges.WhenText(When)}{(LandsOn == EEffectRecipient.Caster ? ", on its caster" : "")}"
            : "Adds an effect";
    }

    /// <summary>
    /// Changes the numbers of its impacts on an attribute, of some impact types where it names any, by a Value Pipeline
    /// stage: one that commits, after the effect's own pipeline, kept in the hit's breakdown; a lasting one, on its
    /// magnitude.
    /// </summary>
    [Serializable]
    public class ChangeImpactChange : AbstractEffectChange
    {
        [Tooltip("Only its impacts on this attribute. None, every impact")]
        public Attribute Attribute;

        [Tooltip("Only an effect of one of these impact types. None, any")]
        [ForgeTagContext(ForgeContext.ImpactType)]
        public List<Tag> ImpactTypes = new();

        [Tooltip("What its number goes through")]
        [SerializeReference] public AbstractValueStage Stage;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            if (Stage == null || !Stage.Enabled) return;
            if (ImpactTypes is { Count: > 0 } && !ForgeHelper.ValidateImpactTypes(false, spec.GetImpactTypes() ?? new List<Tag>(), ImpactTypes)) return;
            changes.Stages.Add(new EffectChanges.ImpactStage(Attribute, Stage));
        }

        public override string Describe() => Stage != null ? $"{Stage.Name()}{(Attribute ? $" on {Attribute.GetName()}" : "")}" : "Changes its numbers";
    }

    /// <summary>
    /// Adds an overflow effect to it, landing as an application would take it past its most stacks; it needs a stack
    /// bound of the effect's own to overflow. Clear Stacks takes its stacks off as it overflows, as its bound can.
    /// </summary>
    [Serializable]
    public class AddOverflowChange : AbstractEffectChange
    {
        [Tooltip("The effect that lands as it overflows")]
        public GameplayEffect Effect;

        [Tooltip("Its stacks come off as it overflows, so it starts again from none")]
        public bool ClearStacks;

        [Tooltip("Whom the overflow's effect lands on: the effect's target, or its caster (TW-14)")]
        public EEffectRecipient LandsOn = EEffectRecipient.Target;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            if (Effect) changes.Overflows.Add(new EffectChanges.Packet(EApplyTickRemove.OnApply, Effect, rule, LandsOn));
            if (ClearStacks) changes.ClearStacksOnOverflow = true;
        }

        public override string Describe() => Effect ? $"Overflows into {Effect.GetName()}{(ClearStacks ? ", its stacks cleared" : "")}" : "Adds an overflow";
    }

    /// <summary>Changes how long it lasts: its time multiplied, then more added. Its ticks spread over the time it has.</summary>
    [Serializable]
    public class DurationChange : AbstractEffectChange
    {
        [Tooltip("What its time is multiplied by")]
        public ScalableFloat Multiply = 1f;

        [Tooltip("The seconds added after, or taken where it's below 0")]
        public ScalableFloat AddSeconds = 0f;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            changes.DurationScale *= Multiply?.Evaluate(spec) ?? 1f;
            changes.DurationAdd += AddSeconds?.Evaluate(spec) ?? 0f;
        }

        public override string Describe() => "Changes how long it lasts";
    }

    /// <summary>Changes its stacks: more it can hold at most, and more a landing gives.</summary>
    [Serializable]
    public class StacksChange : AbstractEffectChange
    {
        [Tooltip("More stacks it can hold at most, where a stack bound of its own sets a most")]
        public int MostStacks;

        [Tooltip("More stacks a landing gives")]
        public int StacksPerLanding;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            changes.MostStacks += MostStacks;
            changes.StacksPerLanding += StacksPerLanding;
        }

        public override string Describe() => "Changes its stacks";
    }

    /// <summary>Changes its chance to apply: multiplied, then added to.</summary>
    [Serializable]
    public class ChanceChange : AbstractEffectChange
    {
        [Tooltip("What its chance is multiplied by")]
        public ScalableFloat Multiply = 1f;

        [Tooltip("What's added to its chance after, from 0 to 1")]
        public ScalableFloat Add = 0f;

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            changes.ChanceScale *= Multiply?.Evaluate(spec) ?? 1f;
            changes.ChanceAdd += Add?.Evaluate(spec) ?? 0f;
        }

        public override string Describe() => "Changes its chance to apply";
    }

    /// <summary>Adds tags it grants while it's on, as its own granted tags are.</summary>
    [Serializable]
    public class GrantTagsChange : AbstractEffectChange
    {
        [Tooltip("The tags it grants besides its own")]
        [ForgeTagContext(ForgeContext.Granted)]
        public List<Tag> Tags = new();

        public override void Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)
        {
            if (Tags == null) return;
            foreach (var tag in Tags)
                if (tag.IsValid) changes.GrantedTags.Add(tag);
        }

        public override string Describe() => "Grants tags";
    }

    /// <summary>
    /// What modifiers changed of one application (TW-7): the effect's own reads take these in, its contained effects, its
    /// impacts' numbers, its overflow, its time, its stacks, its chance and its granted tags. Null on a spec no modifier
    /// changed, so an effect nothing modifies costs nothing more.
    /// </summary>
    public sealed class EffectChanges
    {
        public readonly struct Packet
        {
            public readonly EApplyTickRemove When;
            public readonly GameplayEffect Effect;
            public readonly EffectModifierRule AddedBy;

            /// <summary>Whom it lands on: the effect's target, or its caster (TW-14).</summary>
            public readonly EEffectRecipient LandsOn;

            public Packet(EApplyTickRemove when, GameplayEffect effect, EffectModifierRule addedBy, EEffectRecipient landsOn = EEffectRecipient.Target)
            {
                When = when;
                Effect = effect;
                AddedBy = addedBy;
                LandsOn = landsOn;
            }
        }

        public readonly struct ImpactStage
        {
            public readonly Attribute Attribute;
            public readonly AbstractValueStage Stage;

            public ImpactStage(Attribute attribute, AbstractValueStage stage)
            {
                Attribute = attribute;
                Stage = stage;
            }
        }

        public readonly List<Packet> Packets = new();
        public readonly List<ImpactStage> Stages = new();
        public readonly List<Packet> Overflows = new();
        public readonly List<Tag> GrantedTags = new();
        public bool ClearStacksOnOverflow;
        public float DurationScale = 1f;
        public float DurationAdd;
        public int MostStacks;
        public int StacksPerLanding;
        public float ChanceScale = 1f;
        public float ChanceAdd;

        /// <summary>The rules this application came from, as an effect one of them added, which don't change it again.</summary>
        public readonly List<EffectModifierRule> AddedBy = new();

        /// <summary>A time changed as the modifiers say: multiplied, then added to, never below 0.</summary>
        public float Duration(float total) => Mathf.Max(0f, total * DurationScale + DurationAdd);

        /// <summary>A chance changed as the modifiers say: multiplied, then added to.</summary>
        public float Chance(float odds) => odds * ChanceScale + ChanceAdd;

        /// <summary>The stages for an impact on an attribute, in the order the modifiers gave them, or null.</summary>
        public List<AbstractValueStage> StagesFor(IAttribute attribute)
        {
            List<AbstractValueStage> found = null;
            foreach (var each in Stages)
                if (each.Attribute == null || (attribute != null && attribute.Equals(each.Attribute)))
                    (found ??= new List<AbstractValueStage>()).Add(each.Stage);
            return found;
        }

        internal static string WhenText(EApplyTickRemove when) => when switch
        {
            EApplyTickRemove.OnTick => "each tick",
            EApplyTickRemove.OnRemove => "as it ends",
            _ => "as it lands",
        };
    }

    /// <summary>
    /// The modifiers an entity holds now (TW-7): its own, its items' while they're equipped, its abilities' while it
    /// knows them, and its effects' while they last. Each holds as often as it's given, as two of an item modify twice.
    /// </summary>
    public sealed class EffectModifierSet
    {
        private readonly List<EffectModifierEntry> entries = new();

        public IReadOnlyList<EffectModifierEntry> Entries => entries;

        public void Provide(IReadOnlyList<EffectModifierEntry> given)
        {
            if (given == null) return;
            for (int i = 0; i < given.Count; i++)
                if (given[i] != null) entries.Add(given[i]);
        }

        public void Remove(IReadOnlyList<EffectModifierEntry> given)
        {
            if (given == null) return;
            for (int i = 0; i < given.Count; i++)
                if (given[i] != null) entries.Remove(given[i]);
        }

        public void Clear() => entries.Clear();
    }

    /// <summary>Gives a holder the modifiers it has, takes them back, and applies them to each effect as it's made (TW-7).</summary>
    public static class EffectModifiers
    {
        // The rules whose added effects are being made now, which don't change them: an effect one adds that it would
        // pick again isn't changed by it again, and the effects that one contains, neither
        [ThreadStatic] private static List<EffectModifierRule> making;

        /// <summary>Gives <paramref name="holder"/> the entries, which change its effects, or those landing on it, from now.</summary>
        public static void Provide(IReadOnlyList<EffectModifierEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0) return;
            (holder as IGameplayAbilitySystem)?.GetEffectModifiers()?.Provide(entries);
        }

        /// <summary>Takes back what <see cref="Provide"/> gave.</summary>
        public static void Remove(IReadOnlyList<EffectModifierEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0) return;
            (holder as IGameplayAbilitySystem)?.GetEffectModifiers()?.Remove(entries);
        }

        /// <summary>
        /// Applies the modifiers to an effect as it's made: its source's outgoing ones, then its target's incoming ones,
        /// each that picks the effect and whose condition holds, but those whose added effect it is. Called from
        /// <see cref="GameplayEffect.Generate"/>.
        /// </summary>
        public static void Apply(GameplayEffectSpec spec)
        {
            if (spec?.Base == null) return;
            var source = spec.Source as IGameplayAbilitySystem;
            var target = spec.Target as IGameplayAbilitySystem;
            var outgoing = source?.GetEffectModifiers();
            var incoming = target?.GetEffectModifiers();
            bool any = (outgoing != null && outgoing.Entries.Count > 0) || (incoming != null && incoming.Entries.Count > 0);
            if (!any && (making == null || making.Count == 0)) return;

            EffectChanges changes = null;
            if (making is { Count: > 0 })
            {
                changes = new EffectChanges();
                changes.AddedBy.AddRange(making);
            }
            // An effect an entity applies to itself is both its own and one landing on it: each of its rules counts once
            if (outgoing != null && ReferenceEquals(outgoing, incoming)) Take(outgoing, spec, null, ref changes);
            else
            {
                if (outgoing != null) Take(outgoing, spec, true, ref changes);
                if (incoming != null) Take(incoming, spec, false, ref changes);
            }
            spec.Changes = changes;
        }

        // The rules of a set that go the way given, or either way where none is
        private static void Take(EffectModifierSet set, GameplayEffectSpec spec, bool? outgoing, ref EffectChanges changes)
        {
            var entries = set.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var rule = entries[i]?.Resolved;
                if (rule?.Changes == null || rule.Changes.Count == 0 || !rule.Picks(spec.Base)) continue;
                if (outgoing.HasValue && !rule.Goes(outgoing.Value)) continue;
                if (changes != null && changes.AddedBy.Contains(rule)) continue;
                if (rule.When != null && !rule.When.Holds(NumberContext.Of(spec))) continue;
                changes ??= new EffectChanges();
                foreach (var change in rule.Changes) change?.Apply(changes, spec, rule);
            }
        }

        /// <summary>
        /// Makes an effect a modifier's change added, or one a changed effect contains, with the rules it came from kept
        /// from changing it again. <paramref name="addedBy"/> is the rule that added it, or null for one the effect contains.
        /// </summary>
        public static GameplayEffectSpec Make(ITarget target, IEffectOrigin origin, GameplayEffect effect, GameplayEffectSpec from, EffectModifierRule addedBy)
        {
            making ??= new List<EffectModifierRule>();
            int mark = making.Count;
            if (from?.Changes != null) making.AddRange(from.Changes.AddedBy);
            if (addedBy != null) making.Add(addedBy);
            try
            {
                return target.GenerateEffectSpec(origin, effect);
            }
            finally
            {
                making.RemoveRange(mark, making.Count - mark);
            }
        }
    }
}
