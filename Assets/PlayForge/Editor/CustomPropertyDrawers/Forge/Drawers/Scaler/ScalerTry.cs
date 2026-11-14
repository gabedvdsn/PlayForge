using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A scaler tried in the editor (LVL-5, Decision 82): read at each level of a range against stand-ins for its caster
    /// and its target, which hold the values typed for them or filled from an entity's attribute set, and with the values
    /// a caller sets. A read finds what the scaler asks for, each attribute on its side and each caller's key, and adds a
    /// row for each the first time, filled from its side's entity where it has one. A row it lacks reads as an entity
    /// without the attribute does, and a caller's key not set reads as a caller that didn't set it.
    /// <para>
    /// A stand-in holds attributes alone: a number that reads an effect on a side, an item, a tag, a retention group's
    /// part or Local Data reads as it would on an entity with none of them. A Scaler asset's try, from <see cref="For"/>,
    /// is kept for the session with <see cref="Keep"/>, and never written into the asset.
    /// </para>
    /// </summary>
    public sealed class ScalerTry
    {
        /// <summary>The most levels a read takes at once, as an effect's Levels shows.</summary>
        public const int MostLevels = EffectLevelsView.MostLevels;

        /// <summary>An attribute a side holds for a read: whether it has it, and its current and base values.</summary>
        [Serializable]
        public sealed class AttributeRow
        {
            public Attribute Attribute;
            public bool Has = true;
            public float Current;
            public float Base;
        }

        /// <summary>A key a caller sets for a read, with its value, or not set.</summary>
        [Serializable]
        public sealed class CallerRow
        {
            public Tag Key;
            public bool Set;
            public float Value;
        }

        /// <summary>The caster or the target: the attributes the read asked of it, and the entity they were filled from.</summary>
        public sealed class Side
        {
            private readonly List<AttributeRow> rows = new();
            private EntityIdentity entity;
            private AttributeStartValues.Result starts;

            internal Side(NumberSide which) => Which = which;

            /// <summary>Which side it is.</summary>
            public NumberSide Which { get; }

            /// <summary>The entity its values were filled from, or none.</summary>
            public EntityIdentity Entity => entity;

            /// <summary>Each attribute a read asked of it, in the order asked.</summary>
            public IReadOnlyList<AttributeRow> Attributes => rows;

            /// <summary>Its row for an attribute, or null where no read has asked for it.</summary>
            public AttributeRow Of(Attribute attribute) => attribute == null ? null : rows.Find(row => row.Attribute == attribute);

            /// <summary>Gives it an attribute's value, its base the same.</summary>
            public void Set(Attribute attribute, float current) => Set(attribute, current, current);

            /// <summary>Gives it an attribute's current and base values.</summary>
            public void Set(Attribute attribute, float current, float baseValue)
            {
                var row = Ask(attribute);
                if (row == null) return;
                row.Has = true;
                row.Current = current;
                row.Base = baseValue;
            }

            /// <summary>Takes an attribute from it, so it reads as an entity without the attribute.</summary>
            public void Lack(Attribute attribute)
            {
                var row = Ask(attribute);
                if (row != null) row.Has = false;
            }

            /// <summary>
            /// Fills its attributes from an entity: what the entity's attribute set starts each at, at the entity's starting
            /// level, and a row for an attribute it doesn't give lacking it. Those a later read asks for fill from it too.
            /// None keeps the values as they are and fills nothing more.
            /// </summary>
            public void FillFrom(EntityIdentity from)
            {
                entity = from;
                starts = null;
                if (from == null) return;
                foreach (var row in rows) Fill(row);
            }

            // A read's ask: its row, made the first time and filled from the entity where there is one
            internal AttributeRow Ask(Attribute attribute)
            {
                if (attribute == null) return null;
                var row = Of(attribute);
                if (row != null) return row;
                row = new AttributeRow { Attribute = attribute };
                if (entity != null) Fill(row);
                rows.Add(row);
                return row;
            }

            private void Fill(AttributeRow row)
            {
                starts ??= entity.AttributeSet != null ? AttributeStartValues.Of(entity.AttributeSet) : null;
                var start = starts is { Problem: null } ? starts.Of(row.Attribute) : null;
                if (start == null)
                {
                    row.Has = false;
                    row.Current = 0f;
                    row.Base = 0f;
                    return;
                }
                var value = start.At(entity.GetLinkedStartingLevel());
                row.Has = true;
                row.Current = value.CurrentValue;
                row.Base = value.BaseValue;
            }

            internal void Restore(EntityIdentity from, IEnumerable<AttributeRow> kept)
            {
                entity = from;
                starts = null;
                rows.Clear();
                if (kept != null) rows.AddRange(kept.Where(row => row != null && row.Attribute != null));
            }
        }

        /// <summary>What a read gave: its levels, the value at each, and the parts behind each.</summary>
        public sealed class Reading
        {
            /// <summary>The first level it read.</summary>
            public int From { get; internal set; }

            /// <summary>The last level it read.</summary>
            public int To { get; internal set; }

            /// <summary>The scaler's value at each level, from the first.</summary>
            public float[] Values { get; internal set; }

            /// <summary>The parts behind each level's value; null where its read failed.</summary>
            public NumberTrace[] Parts { get; internal set; }

            /// <summary>What a level's read threw, where one did, so the panel says so rather than breaking.</summary>
            public string Error { get; internal set; }
        }

        /// <summary>The first level a read takes.</summary>
        public int From = 1;

        /// <summary>The last level a read takes, at most <see cref="MostLevels"/> after the first.</summary>
        public int To = 5;

        /// <summary>The caster's stand-in.</summary>
        public Side Caster { get; } = new(NumberSide.Caster);

        /// <summary>The target's stand-in.</summary>
        public Side Target { get; } = new(NumberSide.Target);

        /// <summary>Each key a read asked the caller for, in the order asked.</summary>
        public IReadOnlyList<CallerRow> Caller => caller;

        private readonly List<CallerRow> caller = new();

        // The session key it's kept under, a Scaler asset's; none for a try made apart
        private string key;

        /// <summary>Sets a caller's value for a key, so the read takes it.</summary>
        public void SetCaller(Tag key, float value)
        {
            var row = AskCaller(key);
            if (row == null) return;
            row.Set = true;
            row.Value = value;
        }

        /// <summary>Takes a caller's value off, so the read takes its default.</summary>
        public void UnsetCaller(Tag key)
        {
            var row = caller.Find(each => each.Key == key);
            if (row != null) row.Set = false;
        }

        internal CallerRow AskCaller(Tag key)
        {
            if (!key.IsValid) return null;
            var row = caller.Find(each => each.Key == key);
            if (row != null) return row;
            row = new CallerRow { Key = key };
            caller.Add(row);
            return row;
        }

        /// <summary>
        /// Reads a scaler at each level from <see cref="From"/> to <see cref="To"/>, at most <see cref="MostLevels"/>, the
        /// sides and the caller as they stand, adding a row for each attribute and key it asks for that it hadn't.
        /// </summary>
        public Reading Read(AbstractScaler scaler)
        {
            int from = Mathf.Max(1, From);
            int to = Mathf.Clamp(To, from, from + MostLevels - 1);
            var reading = new Reading { From = from, To = to, Values = new float[to - from + 1], Parts = new NumberTrace[to - from + 1] };
            if (scaler == null) return reading;

            var caster = new NumberStandIn(Caster, "The caster");
            var target = new NumberStandIn(Target, "The target");
            var values = new CallerValues(this);
            for (int level = from; level <= to; level++)
            {
                try
                {
                    var context = NumberContext.For(caster, target, level, to).WithMagnitudes(values);
                    var trace = NumberTrace.Of(scaler, in context);
                    reading.Parts[level - from] = trace;
                    reading.Values[level - from] = trace?.Value ?? 0f;
                }
                catch (Exception error)
                {
                    // A kind that breaks on a stand-in breaks no panel: the read says so, and goes on
                    reading.Values[level - from] = float.NaN;
                    reading.Error ??= $"Level {level}: {error.GetType().Name}: {error.Message}";
                }
            }
            return reading;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Kept for the session
        // ═══════════════════════════════════════════════════════════════════════════

        // A try as the session keeps it: its assets by their instance IDs, which hold for the session, saved or not
        [Serializable]
        private sealed class Kept
        {
            public int From = 1;
            public int To = 5;
            public int CasterEntity;
            public int TargetEntity;
            public List<KeptRow> CasterRows = new();
            public List<KeptRow> TargetRows = new();
            public List<CallerRow> Caller = new();
        }

        [Serializable]
        private sealed class KeptRow
        {
            public int Attribute;
            public bool Has;
            public float Current;
            public float Base;
        }

        /// <summary>A Scaler asset's try, as it was left this session, or a new one.</summary>
        public static ScalerTry For(ScalerAsset asset)
        {
            var tried = new ScalerTry { key = KeyOf(asset) };
            var json = tried.key == null ? null : SessionState.GetString(tried.key, null);
            if (string.IsNullOrEmpty(json)) return tried;

            var kept = JsonUtility.FromJson<Kept>(json);
            if (kept == null) return tried;
            tried.From = kept.From;
            tried.To = kept.To;
            tried.Caster.Restore(ObjectOf<EntityIdentity>(kept.CasterEntity), RowsOf(kept.CasterRows));
            tried.Target.Restore(ObjectOf<EntityIdentity>(kept.TargetEntity), RowsOf(kept.TargetRows));
            tried.caller.AddRange(kept.Caller.Where(row => row != null && row.Key.IsValid));
            return tried;
        }

        /// <summary>Keeps it for the session, for <see cref="For"/> to give again; never in the asset.</summary>
        public void Keep()
        {
            if (key == null) return;
            var kept = new Kept
            {
                From = From,
                To = To,
                CasterEntity = IdOf(Caster.Entity),
                TargetEntity = IdOf(Target.Entity),
                CasterRows = KeptOf(Caster.Attributes),
                TargetRows = KeptOf(Target.Attributes),
                Caller = caller.ToList(),
            };
            SessionState.SetString(key, JsonUtility.ToJson(kept));
        }

        private static int IdOf(Object asset) => asset != null ? asset.GetInstanceID() : 0;

        private static T ObjectOf<T>(int id) where T : Object => id == 0 ? null : EditorUtility.InstanceIDToObject(id) as T;

        private static List<KeptRow> KeptOf(IEnumerable<AttributeRow> rows) => rows
            .Where(row => row.Attribute != null)
            .Select(row => new KeptRow { Attribute = IdOf(row.Attribute), Has = row.Has, Current = row.Current, Base = row.Base })
            .ToList();

        private static IEnumerable<AttributeRow> RowsOf(List<KeptRow> rows) => (rows ?? new List<KeptRow>())
            .Select(row => new AttributeRow { Attribute = ObjectOf<Attribute>(row.Attribute), Has = row.Has, Current = row.Current, Base = row.Base });

        /// <summary>Forgets a Scaler asset's try, so the next starts anew.</summary>
        public static void Forget(ScalerAsset asset)
        {
            var key = KeyOf(asset);
            if (key != null) SessionState.EraseString(key);
        }

        // By the asset's GUID, or its instance for one not saved
        private static string KeyOf(ScalerAsset asset)
        {
            if (asset == null) return null;
            string id = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _) && !string.IsNullOrEmpty(guid)
                ? guid
                : asset.GetInstanceID().ToString();
            return $"PlayForge.ScalerTry.{id}";
        }
    }

    /// <summary>
    /// A caster or a target for a read in the editor (LVL-5): an entity with no body, effects, items, tags or Local Data,
    /// holding the attributes its side of a try gives, and asking the try for each one a read asks it for.
    /// </summary>
    internal sealed class NumberStandIn : ISource
    {
        private readonly ScalerTry.Side side;
        private readonly string name;
        private TagCache tags;

        public NumberStandIn(ScalerTry.Side side, string name)
        {
            this.side = side;
            this.name = name;
        }

        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value)
        {
            value = default;
            var row = attribute is Attribute asset ? side.Ask(asset) : null;
            if (row == null || !row.Has) return false;
            value = new AttributeValue(row.Current, row.Base);
            return true;
        }

        // ─── An entity with nothing else on it ─────────────────────────────

        public List<Tag> GetAffiliation() => new();
        public List<Tag> GetAppliedTags() => new();
        public int GetTagWeight(Tag tag) => -1;
        public bool QueryTags(TagQuery query) => query != null && query.Validate(new List<Tag>());
        public void CompileGrantedTags() { }

        public string GetName() => name;
        public string GetDescription() => "A stand-in for a read in the editor";
        public Texture2D GetDefaultIcon() => null;

        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity) { }
        public bool ApplyGameplayEffect(GameplayEffectSpec spec) => false;
        public bool RemoveGameplayEffect(GameplayEffect effect) => false;
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect) => null;
        public IntValuePairClamped GetLevel(Tag key) => default;

        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem)
        {
            lvlSystem = null;
            return false;
        }

        public bool FindItemSystem(out ItemSystemComponent itemSystem)
        {
            itemSystem = null;
            return false;
        }

        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            attrSystem = null;
            return false;
        }

        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            abilSystem = null;
            return false;
        }

        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true) => false;
        public AbstractTargetingPacket GetTargetingPacket() => null;
        public void MarkDead() { }
        public bool IsDead => false;

        public ISource GetSource() => this;
        public IHasReadableDefinition GetReadableDefinition() => this;
        public List<Tag> GetContextTags() => new();
        public Tag GetAssetTag() => default;
        public IntValuePairClamped GetLevel() => default;
        public bool IsActive() => true;

        public ProcessRelay[] GetRelays() => Array.Empty<ProcessRelay>();
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler) => false;
        public bool HandlerProcessIsSubscribed(ProcessRelay relay) => false;
        public void HandlerSubscribeProcess(ProcessRelay relay) { }
        public bool HandlerVoidProcess(ProcessRelay relay) => false;
        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono) => null;

        public TagCache GetTagCache() => tags ??= new TagCache(this);
        public ActionQueue GetActionQueue() => null;
        public FrameSummary GetFrameSummary() => null;
        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag) => default;
        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags) => default;
        public LevelCallbackStatus SetLevel(Tag key, IntValuePair level) => default;
        public LevelCallbackStatus ModifyLevel(Tag key, IntValuePair delta) => default;
    }

    /// <summary>A try's caller values, which a read asks by key, the try adding a row for each key the first time (LVL-5).</summary>
    internal sealed class CallerValues : IReadOnlyDictionary<Tag, float>
    {
        private readonly ScalerTry tried;

        public CallerValues(ScalerTry tried) => this.tried = tried;

        public bool TryGetValue(Tag key, out float value)
        {
            var row = tried.AskCaller(key);
            value = row is { Set: true } ? row.Value : 0f;
            return row is { Set: true };
        }

        public bool ContainsKey(Tag key) => TryGetValue(key, out _);

        public float this[Tag key] => TryGetValue(key, out float value) ? value : throw new KeyNotFoundException(key.ToString());

        public IEnumerable<Tag> Keys => Set().Select(row => row.Key);
        public IEnumerable<float> Values => Set().Select(row => row.Value);
        public int Count => Set().Count();

        public IEnumerator<KeyValuePair<Tag, float>> GetEnumerator() =>
            Set().Select(row => new KeyValuePair<Tag, float>(row.Key, row.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<ScalerTry.CallerRow> Set() => tried.Caller.Where(row => row.Set);
    }
}
