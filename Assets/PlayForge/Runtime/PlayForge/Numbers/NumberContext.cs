using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a number field's context provides (FEAT-29): what a site has for its numbers to read, which
    /// <see cref="NumberContext.Has"/> tests, what a kind needs (<see cref="NumberKindAttribute.Reads"/>), and what a field
    /// declares (<see cref="NumberContextAttribute"/>).
    /// </summary>
    [Flags]
    public enum NumberReads
    {
        None = 0,

        /// <summary>The level of what the number belongs to: the ability's, the item's, the entity's.</summary>
        Level = 1 << 0,

        /// <summary>The caster: the entity whose ability, item or effect it is.</summary>
        Caster = 1 << 1,

        /// <summary>The target: the entity the effect lands on, or the activation's target.</summary>
        Target = 1 << 2,

        /// <summary>The magnitudes the caller set, by tag (Set By Caller).</summary>
        Caller = 1 << 3,

        /// <summary>An asset's Local Data, by key: the ability's, the item's or the entity's.</summary>
        LocalData = 1 << 4,

        /// <summary>The impact carrier whose number it is, as an effect's spec, with what it has dealt.</summary>
        Carrier = 1 << 5,

        /// <summary>The activation's data, as an ability task reads it.</summary>
        Data = 1 << 6,

        /// <summary>The object the field belongs to, whose other number fields it can read.</summary>
        Owner = 1 << 7,

        All = Level | Caster | Target | Caller | LocalData | Carrier | Data | Owner,
    }

    /// <summary>Which of a context's two entities a read is about.</summary>
    public enum NumberSide
    {
        Caster,
        Target,
    }

    /// <summary>
    /// What a number is read against (FEAT-29): what its site has. A kind of number reads it; it is the whole of a kind's
    /// API, so adding to it later breaks no kind. It offers the caster, the target and the level; the carrier, where there
    /// is one, and what it has dealt; the caller's magnitudes and Local Data, each by key; an attribute's value or a
    /// retention group's part of it, on either side, and through <see cref="SystemOf"/> the effects there; the activation's
    /// data, where a task reads it; and the owner, the object the field belongs to. <see cref="Has"/> says which of these
    /// this site provides.
    /// <para>
    /// A site builds one with <c>Of</c>, from an effect spec, a container, an activation's data, a worker's context, an
    /// entity, a cue or a process, and reads its numbers through it: <c>Range.Evaluate(NumberContext.Of(data))</c>. A test
    /// builds one with <see cref="For(ISource, ITarget, int, int)"/>. It is a struct, and a number read through it
    /// allocates nothing.
    /// </para>
    /// </summary>
    public readonly struct NumberContext
    {
        /// <summary>The caster: the entity whose ability, item or effect the number is part of.</summary>
        public readonly ISource Caster;

        /// <summary>The target: the entity an effect lands on, or the activation's target; null where there's none yet.</summary>
        public readonly ITarget Target;

        /// <summary>The level the number reads by, with the range it sits in.</summary>
        public readonly IntValuePairClamped Level;

        /// <summary>What applied or holds the number: an ability, an item or an entity, where the site knows it.</summary>
        public readonly IEffectOrigin Origin;

        /// <summary>The impact carrier whose number it is, as an effect's spec; null where there's none.</summary>
        public readonly IAttributeImpactCarrier Carrier;

        /// <summary>The magnitudes the caller set, by tag; null where the site has none to give.</summary>
        public readonly IReadOnlyDictionary<Tag, float> Magnitudes;

        /// <summary>The asset whose Local Data the number reads: the ability's, the item's or the entity's.</summary>
        public readonly ILocalDataSource LocalData;

        /// <summary>The activation's data, as a task reads it; or the data that spawned a process.</summary>
        public readonly ProcessDataPacket Data;

        /// <summary>The object the field belongs to, which a number relative to another field reads.</summary>
        public readonly object Owner;

        private readonly bool hasLevel;

        public NumberContext(ISource caster, ITarget target, IntValuePairClamped level, bool hasLevel = true,
            IEffectOrigin origin = null, IAttributeImpactCarrier carrier = null, IReadOnlyDictionary<Tag, float> magnitudes = null,
            ILocalDataSource localData = null, ProcessDataPacket data = null, object owner = null)
        {
            Caster = caster;
            Target = target;
            Level = level;
            this.hasLevel = hasLevel;
            Origin = origin;
            Carrier = carrier;
            Magnitudes = magnitudes;
            LocalData = localData;
            Data = data;
            Owner = owner;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What it provides
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>What this context provides.</summary>
        public NumberReads Provides
        {
            get
            {
                var provides = NumberReads.None;
                if (hasLevel) provides |= NumberReads.Level;
                if (Caster != null) provides |= NumberReads.Caster;
                if (Target != null) provides |= NumberReads.Target;
                if (Magnitudes != null) provides |= NumberReads.Caller;
                if (LocalData != null || OwningEntity != null) provides |= NumberReads.LocalData;
                if (Carrier != null) provides |= NumberReads.Carrier;
                if (Data != null) provides |= NumberReads.Data;
                if (Owner != null) provides |= NumberReads.Owner;
                return provides;
            }
        }

        /// <summary>Whether this context provides all of what's asked: <c>context.Has(NumberReads.Target)</c>.</summary>
        public bool Has(NumberReads reads) => (Provides & reads) == reads;

        // ═══════════════════════════════════════════════════════════════════════════
        // Building one
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A context for a test or a tool: a caster, a target and a level, with no effect behind them.</summary>
        /// <param name="maxLevel">The level range's top, which a scaler that spreads its values over a range reads; the level, by default.</param>
        public static NumberContext For(ISource caster, ITarget target, int level, int maxLevel = 0) =>
            new(caster, target, new IntValuePairClamped(level, 1, Math.Max(level, maxLevel)), localData: LocalDataOf(caster));

        /// <summary>A context for a test or a tool: a caster at its own level, and a target.</summary>
        public static NumberContext For(ISource caster, ITarget target = null) =>
            caster == null
                ? new NumberContext(null, target, default, hasLevel: false)
                : new NumberContext(caster, target, caster.GetLevel(), origin: caster, localData: LocalDataOf(caster));

        /// <summary>
        /// The context of an impact carrier, as an effect's spec: its origin's caster and level, its target, the caller's
        /// magnitudes, and the origin's Local Data. A worker's or a cue's carrier gives the same.
        /// </summary>
        public static NumberContext Of(IAttributeImpactCarrier carrier)
        {
            if (carrier == null) return default;
            if (carrier is NumberBridge bridge) return bridge.Context;
            // A kind inside a plain scaler reads the context its number was read in, and not one made again from the
            // carrier, which has no owner, no data and none of what the reader set (NUM-16)
            if (readingDepth > 0 && ReferenceEquals(Reading[readingDepth - 1].Carrier, carrier)) return Reading[readingDepth - 1];
            var origin = carrier.GetOrigin();
            var magnitudes = carrier is GameplayEffectSpec spec ? spec.SetByCallerMagnitudes : null;
            return new NumberContext(origin?.GetSource(), carrier.GetTarget(), origin?.GetLevel() ?? default, origin != null,
                origin, carrier, magnitudes, LocalDataOf(origin));
        }

        /// <summary>The context of an effect's container: its spec's, as it stacks.</summary>
        public static NumberContext Of(AbstractStackingEffectContainer container) => container == null ? default : Of(container.Spec);

        /// <summary>
        /// The context of an activation's data, as a task has it as it starts: the ability's caster and level, the
        /// activation's target where it has one, the caller's magnitudes, the ability's Local Data, and the data. Other
        /// data, as a process's, gives what it holds: the origin that spawned it, under Derivation, and its target.
        /// </summary>
        public static NumberContext Of(ProcessDataPacket data)
        {
            if (data == null) return default;
            IEffectOrigin origin = data is AbilityDataPacket ability ? ability.EffectOrigin
                : data.TryGetAt(Tags.DERIVATION, 0, out IEffectOrigin derived) ? derived : null;
            var caster = origin?.GetSource() ?? (data.TryGetAt(Tags.SOURCE, 0, out ISource source) ? source : null);
            var target = data.TryGetAt(Tags.TARGET_REAL, 0, out ITarget aimed) ? aimed : null;
            bool hasLevel = origin != null || caster != null;
            var level = origin?.GetLevel() ?? caster?.GetLevel() ?? default;
            return new NumberContext(caster, target, level, hasLevel, origin ?? caster, null, data.SetByCallerMagnitudes,
                LocalDataOf(origin ?? caster), data);
        }

        /// <summary>The context of an effect worker's call: its carrier's.</summary>
        public static NumberContext Of(in EffectWorkerContext worker) =>
            worker.Carrier != null ? Of(worker.Carrier) : Of(worker.System as ISource);

        /// <summary>
        /// The context of an attribute worker's call: the change's carrier, with the entity being changed as its target.
        /// </summary>
        public static NumberContext Of(in WorkerContext worker)
        {
            var carrier = worker.Change?.Value.Carrier;
            var entity = worker.System as ITarget;
            if (carrier == null) return Of(worker.System as ISource);
            return Of(carrier).WithTarget(entity ?? carrier.GetTarget());
        }

        /// <summary>The context of an impact worker's call: the impact's carrier, with the entity it landed on.</summary>
        public static NumberContext Of(in ImpactWorkerContext worker)
        {
            var carrier = worker.ImpactData.SourcedModifier.Carrier;
            if (carrier == null) return Of(worker.System);
            var context = Of(carrier);
            return worker.ImpactData.Target != null ? context.WithTarget(worker.ImpactData.Target) : context;
        }

        /// <summary>
        /// The context of an entity on its own, as an analysis worker or an attribute set's bounds have it: the entity as
        /// the caster, at its own level, with its Local Data, and no target and no caller.
        /// </summary>
        public static NumberContext Of(ISource entity) =>
            entity == null ? default : new NumberContext(entity, null, entity.GetLevel(), true, entity, localData: LocalDataOf(entity));

        /// <summary>The context of an entity on its own (<see cref="Of(ISource)"/>).</summary>
        public static NumberContext Of(IGameplayAbilitySystem entity) => Of(entity as ISource);

        /// <summary>The context of a cue as it fires: its effect's, landing on the cue's entity.</summary>
        public static NumberContext Of(in CueContext cue)
        {
            if (cue.Spec == null) return Of(cue.Target);
            var context = Of(cue.Spec);
            return cue.Target is ITarget target ? context.WithTarget(target) : context;
        }

        /// <summary>
        /// The context of a gameplay mono process, as a projectile: its origin's caster and level, its target where the
        /// data that spawned it has one, and that data's magnitudes, with the process as the owner of its own numbers.
        /// </summary>
        public static NumberContext Of(AbstractGameplayMonoProcess process)
        {
            if (process == null) return default;
            var data = process.SpawnData;
            var origin = process.SpawnOrigin;
            if (origin == null) return Of(data).WithOwner(process);
            var target = data != null && data.TryGetAt(Tags.TARGET_REAL, 0, out ITarget aimed) ? aimed : null;
            return new NumberContext(origin.GetSource(), target, origin.GetLevel(), true, origin, null,
                data?.SetByCallerMagnitudes, LocalDataOf(origin), data, process);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // A context with one thing changed
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The context with another target.</summary>
        public NumberContext WithTarget(ITarget target) =>
            new(Caster, target, Level, hasLevel, Origin, Carrier, Magnitudes, LocalData, Data, Owner);

        /// <summary>The context at another level, from 1 up to its range's top or the level.</summary>
        public NumberContext WithLevel(int level) =>
            WithLevel(new IntValuePairClamped(level, 1, Math.Max(level, hasLevel ? Level.MaxValue : level)));

        /// <summary>The context at another level. It no longer reads its origin's, so a scaler reads this one.</summary>
        public NumberContext WithLevel(IntValuePairClamped level) =>
            new(Caster, Target, level, true, null, Carrier, Magnitudes, LocalData, Data, Owner);

        /// <summary>The context with the caller's magnitudes, as a site that has its own to give.</summary>
        public NumberContext WithMagnitudes(IReadOnlyDictionary<Tag, float> magnitudes) =>
            new(Caster, Target, Level, hasLevel, Origin, Carrier, magnitudes, LocalData, Data, Owner);

        /// <summary>The context reading another asset's Local Data.</summary>
        public NumberContext WithLocalData(ILocalDataSource localData) =>
            new(Caster, Target, Level, hasLevel, Origin, Carrier, Magnitudes, localData, Data, Owner);

        /// <summary>The context with the activation's data.</summary>
        public NumberContext WithData(ProcessDataPacket data) =>
            new(Caster, Target, Level, hasLevel, Origin, Carrier, Magnitudes ?? data?.SetByCallerMagnitudes, LocalData, data, Owner);

        /// <summary>
        /// The context with the object the field belongs to, which an object's own code gives as it reads its own numbers.
        /// </summary>
        public NumberContext WithOwner(object owner) =>
            new(Caster, Target, Level, hasLevel, Origin, Carrier, Magnitudes, LocalData, Data, owner);

        // ═══════════════════════════════════════════════════════════════════════════
        // What a kind reads
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The entity on one side: the caster or the target.</summary>
        public ITarget EntityOn(NumberSide side) => side == NumberSide.Caster ? Caster : Target;

        /// <summary>The ability system on one side, which holds its effects and their containers; null where there's none.</summary>
        public IGameplayAbilitySystem SystemOf(NumberSide side) => EntityOn(side)?.ToGAS();

        /// <summary>What the carrier has dealt: its last impact, its total and its count. Null with no carrier.</summary>
        public TrackedImpact TrackedImpact => Carrier?.GetTrackedImpact();

        /// <summary>The container holding the carrier, where it's an effect on its target's shelf; null otherwise.</summary>
        public AbstractEffectContainer Container => (Carrier as GameplayEffectSpec)?.RuntimeContainer;

        /// <summary>
        /// The value the caller set under exactly this tag, on what the context was built from: an effect's spec, an
        /// activation's data, or the data that spawned a process. A tag above or below it doesn't match. False where
        /// nothing was set under it, or the context holds no values at all.
        /// </summary>
        public bool TryGetSetByCaller(Tag key, out float magnitude)
        {
            magnitude = 0f;
            return Magnitudes != null && Magnitudes.TryGetValue(key, out magnitude);
        }

        /// <summary>
        /// The Local Data entry under a key, of what the number belongs to: the ability's or the item's asset, or, for an
        /// entity's own number, the entity's Local Data as it holds it at runtime.
        /// </summary>
        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            data = null;
            var entries = LocalData?.GetLocalData();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] == null || entries[i].Key != key) continue;
                    data = entries[i];
                    return true;
                }
                return false;
            }
            return OwningEntity is { } entity && entity.TryGetLocalData(key, out data) && data != null;
        }

        /// <summary>The number in the Local Data entry under a key, of what the number belongs to: a float or a whole number.</summary>
        public bool TryGetLocalData(Tag key, out float value) => NumberOf(TryGetLocalData(key, out DataWrapper data) ? data : null, out value);

        /// <summary>The number in an entity's Local Data under a key, the caster's or the target's, as it holds it at runtime.</summary>
        public bool TryGetLocalData(NumberSide side, Tag key, out float value)
        {
            DataWrapper data = null;
            bool found = EntityOn(side) is ILocalDataUser entity && entity.TryGetLocalData(key, out data);
            return NumberOf(found ? data : null, out value);
        }

        // An entry's number: a float's, or a whole number's; any other entry holds none
        private static bool NumberOf(DataWrapper data, out float value)
        {
            value = 0f;
            if (data == null) return false;
            switch (data.Type)
            {
                case EDataWrapperType.Float: value = data.floatValue; return true;
                case EDataWrapperType.Int: value = data.intValue; return true;
                default: return false;
            }
        }

        // The entity whose own number this is, as an entity on its own or an effect it applied itself: it keeps its Local
        // Data at runtime, not on an asset
        private ILocalDataUser OwningEntity => Origin?.GetReadableDefinition() as ILocalDataUser;

        /// <summary>An attribute's value on one side.</summary>
        public bool TryGetAttribute(NumberSide side, IAttribute attribute, out AttributeValue value)
        {
            value = default;
            var entity = EntityOn(side);
            return entity != null && attribute != null && entity.TryGetAttributeValue(attribute, out value);
        }

        /// <summary>The part of an attribute on one side that <paramref name="groups"/> reads, all of it where none (TW-3).</summary>
        public bool TryGetAttribute(NumberSide side, IAttribute attribute, RetentionGroups groups, out AttributeValue value) =>
            RetentionGroups.TryRead(groups, EntityOn(side), attribute, out value);

        /// <summary>An attribute's current or base value on one side, or 0 where it has none.</summary>
        public float Attribute(NumberSide side, IAttribute attribute, EAttributeTargetBinary which = EAttributeTargetBinary.Current)
        {
            if (!TryGetAttribute(side, attribute, out var value)) return 0f;
            return which == EAttributeTargetBinary.Base ? value.BaseValue : value.CurrentValue;
        }

        /// <summary>
        /// A retention group's part of an attribute on one side, as the Bonus group's part of Strength, which is what items
        /// add.
        /// </summary>
        public bool TryGetGroupValue(NumberSide side, IAttribute attribute, Tag group, out AttributeValue value)
        {
            value = default;
            if (attribute == null || EntityOn(side) is not { } entity || !entity.FindAttributeSystem(out var attributes)) return false;
            if (!attributes.TryGetCachedValue(attribute, out CachedAttributeValue cached)) return false;
            value = cached.GetGroupValue(group);
            return true;
        }

        public override string ToString() => $"NumberContext({Provides})";

        // ═══════════════════════════════════════════════════════════════════════════
        // Reading a scaler through it
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether a number's kind can be worked out here: a kind of a project's own needs what it reads; a scaler is read
        /// as it always was. A kind that can't logs once.
        /// </summary>
        internal static bool CanRead(AbstractScaler scaler, in NumberContext context)
        {
            if (scaler is not NumberKind kind || kind.CanEvaluate(in context)) return true;
            LogUnresolved(kind, in context);
            return false;
        }

        /// <summary>
        /// A scaler's value in a context: a <see cref="NumberKind"/> reads the context, and a Group joins its
        /// members in it; another scaler evaluates against
        /// the context's carrier, or a reusable carrier the context fills, which it is initialized against first, as a
        /// spec's scalers are as it's made. A kind such a scaler holds reads this context, whichever carrier it is given
        /// (NUM-16).
        /// </summary>
        internal static float ReadScaler(AbstractScaler scaler, in NumberContext context)
        {
            // A kind that is none, as a Group with no members, joins nothing (ED-80)
            if (AbstractScaler.IsNone(scaler)) return 0f;
            if (scaler is NumberKind kind) return kind.Read(in context);
            // A group joins its members in the context itself, so each reads all the context gives (NUM-14)
            if (scaler is ScalerGroup { HasMembers: true } group) return group.Read(in context);
            if (context.Carrier != null)
            {
                if (readingDepth >= Reading.Length) return scaler.Evaluate(context.Carrier);
                Reading[readingDepth++] = context;
                try
                {
                    return scaler.Evaluate(context.Carrier);
                }
                finally
                {
                    Reading[--readingDepth] = default;
                }
            }

            var bridge = NumberBridge.Rent(in context);
            if (bridge == null) return 0f;
            try
            {
                scaler.Initialize(bridge);
                return scaler.Evaluate(bridge);
            }
            finally
            {
                NumberBridge.Return(bridge);
            }
        }

        // The contexts plain scalers are being read in against their carriers, the innermost last. A kind inside one
        // reads the context its number was read in, with its owner and its data (NUM-16)
        private static readonly NumberContext[] Reading = new NumberContext[NumberBridge.MostDepth];
        private static int readingDepth;

        // The kinds that have said they can't be worked out where they were read, each said once
        private static readonly HashSet<Type> Unresolved = new();

        internal static void LogUnresolved(NumberKind kind, in NumberContext context)
        {
            if (kind == null || !Unresolved.Add(kind.GetType())) return;
            ForgeLog.Warning($"A {NumberText.KindName(kind.GetType())} number can't be worked out where it was read: {kind.WhyNot(in context)}, so it reads its number (FEAT-29).");
        }

        // The Local Data an origin reads: its asset's, as an ability's or an entity's
        private static ILocalDataSource LocalDataOf(IEffectOrigin origin) => origin?.GetReadableDefinition() as ILocalDataSource;
    }
}
