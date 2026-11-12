using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages abilities for an entity.
    /// Integrates with the worker system for impact processing.
    /// </summary>
    public class AbilitySystemComponent : DeferredContextSystem
    {
        // The number of this system's next activation: activations are numbered per entity (FEAT-13)
        private int nextHandleId;

        internal int NextHandleId() => nextHandleId++;

        private EAbilityActivationPolicy activationPolicy;
        public EAbilityActivationPolicy DefaultActivationPolicy => activationPolicy;
        private bool allowDuplicateAbilities;
        private ScalableInt maxAbilitiesOperation;

        public readonly IGameplayAbilitySystem Self;

        private Dictionary<int, AbilitySpecContainer> AbilityCache;
        private Dictionary<EAbilityActivationPolicy, HashSet<int>> ActiveCache;

        protected ImpactWorkerCache ImpactWorkerCache;

        public AbilitySystemCallbacks Callbacks = new();
        
        public AbilitySystemComponent(IGameplayAbilitySystem self)
        {
            Self = self;
        }

        /// <summary>True if any ability is currently executing (in ActiveCache).</summary>
        public bool IsExecuting => ActiveCache.Keys.Any(IsExecutingPolicy);

        /// <summary>True if any ability in this policy is currently executing (in ActiveCache).</summary>
        public bool IsExecutingPolicy(EAbilityActivationPolicy policy)
            => ActiveCache.ContainsKey(policy) && ActiveCache[policy].Count > 0;

        /// <summary>True if any ability is currently claiming (blocking reactivation).</summary>
        public bool IsClaiming => ActiveCache.Keys.Any(IsClaimingPolicy);

        /// <summary>True if any ability in this policy is currently claiming (blocking reactivation).</summary>
        public bool IsClaimingPolicy(EAbilityActivationPolicy policy)
            => ActiveCache.ContainsKey(policy) && ActiveCache[policy].Any(idx => AbilityCache[idx].IsClaiming);

        public bool IsExecutingCritical => IsExecuting && ActiveCache.Keys.Any(IsExecutingPolicyCritical);

        public bool IsExecutingPolicyCritical(EAbilityActivationPolicy policy)
        {
            return IsExecutingPolicy(policy) && ActiveCache[policy].Any(IsCritical);
        }

        /// <summary>True if any claiming ability in this policy is critical.</summary>
        public bool IsClaimingPolicyCritical(EAbilityActivationPolicy policy)
            => IsClaimingPolicy(policy) && ActiveCache[policy].Any(idx => AbilityCache[idx].IsClaiming && IsCritical(idx));

        public bool IsCritical(int index)
            => AbilityCache[index].Spec.Base.Behaviour.Stages.Any(
                stage => stage.Tasks.Any(task => task.IsCriticalSection));

        //public bool CriticalPolicyExecutionCheck()
        
        public int AbilityCount => AbilityCache.Count;
        
        // The input buffer: one slot per ability, each its latest press and how long it may wait yet. The Queue policy was
        // a plain queue: a request carried no time, a press again queued again, and they fired in the order pressed (FEAT-6)
        private readonly List<BufferedRequest> buffer = new();
        private int presses;
        private bool firingBuffer;

        private struct BufferedRequest
        {
            public AbilityActivationRequest Request;
            public float Left;
            public int Priority;
            public int Order;
        }

        // The highest priority first, then the newest
        private sealed class BestFirst : IComparer<BufferedRequest>
        {
            public static readonly BestFirst Instance = new();

            public int Compare(BufferedRequest a, BufferedRequest b) =>
                a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : b.Order.CompareTo(a.Order);
        }

        /// <summary>How many requests wait in the input buffer (FEAT-6).</summary>
        public int BufferedCount => buffer.Count;

        /// <summary>Whether a request for the ability in this slot waits in the input buffer (FEAT-6).</summary>
        public bool IsBuffered(int index)
        {
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Request.Index == index) return true;
            return false;
        }

        /// <param name="bufferWindow">How long the request may wait in the input buffer, in seconds of the caster's time,
        /// or below 0 for its ability's Buffer Window (FEAT-6).</param>
        public AbilityActivationRequest CreateActivationRequest(int index, ProcessDataPacket _data = null, EAbilityActivationPolicyExtended? policy = null,
            float bufferWindow = -1f)
        {
            var _policy = policy?.Translate(this) 
                         ?? (AbilityCache.TryGetValue(index, out var container) 
                             ? container.Spec.Base.Definition.ActivationPolicy.Translate(this) 
                             : DefaultActivationPolicy);
            
            return new AbilityActivationRequest(index, this, _policy, _data) { BufferWindow = bufferWindow };
        }
        
        public struct AbilityActivationRequest
        {
            public int Index;
            public EAbilityActivationPolicy Policy;
            public ProcessDataPacket Data;
            public AbilitySystemComponent System;

            /// <summary>How long the request may wait in the input buffer, or below 0 for its ability's window (FEAT-6).</summary>
            public float BufferWindow;
            
            public AbilityActivationRequest(int index, AbilitySystemComponent asc, EAbilityActivationPolicy policy, ProcessDataPacket data)
            {
                Index = index;
                System = asc;
                Policy = policy;
                Data = data;
                BufferWindow = -1f;
            }
        }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value == _enabled) return;
                if (!value)
                {
                    buffer.Clear();
                    InjectAll(InterruptSequenceInjection.Instance);
                }

                _enabled = value;
            }
        }

        /// <summary>
        /// The entity is ending: no activation from now on, nothing left queued, and the abilities still running end
        /// with the entity's process (GAS-52). Unlike turning <see cref="Enabled"/> off, it sends them no interrupt.
        /// </summary>
        internal void ShutDown()
        {
            buffer.Clear();
            _enabled = false;
        }

        private bool _locked;

        public bool Locked
        {
            get => _locked;
            set => _locked = value;
        }

        #region Initialization
        
        public void Setup(
            EAbilityActivationPolicy activationPolicy, 
            bool allowDuplicates,
            ScalableInt maxAbilitiesOperation)
        {
            this.activationPolicy = activationPolicy;
            this.allowDuplicateAbilities = allowDuplicates;
            this.maxAbilitiesOperation = maxAbilitiesOperation;
            
            ImpactWorkerCache = new ImpactWorkerCache();
        }
        
        /// <summary>
        /// Set the deferred execution context.
        /// Must be called after PreInitialize.
        /// </summary>
        public override void SetDeferredContext(IGameplayAbilitySystem self, ActionQueue actionQueue, FrameSummary frameSummary)
        {
            base.SetDeferredContext(self, actionQueue, frameSummary);
            ImpactWorkerCache?.SetDeferredContext(actionQueue, frameSummary);
        }
        
        public void Initialize(List<Ability> startingAbilities)
        {
            Enabled = true;
            Locked = false;
            
            AbilityCache = new Dictionary<int, AbilitySpecContainer>();
            ActiveCache = new()
            {
                { EAbilityActivationPolicy.AlwaysActivate, new() },
                { EAbilityActivationPolicy.ActivateIfIdle, new() },
                { EAbilityActivationPolicy.QueueActivationIfBusy, new() }
            };
            
            foreach (var ability in startingAbilities ?? new List<Ability>())
            {
                var result = GiveAbility(ability, ability.StartingLevel, out _);
                // Debug.Log($"[{Root.GetName()}] Ability {ability.GetName()} given ({result}) at level {ability.StartingLevel}");
            }

        }

        /// <summary>
        /// Tries each Starting Ability set to Activate Immediately. The hub calls this once its attributes, items and
        /// tags are in place. Tried as the abilities were given, before any attribute existed, the Cost rule refused
        /// every one with a cost (GAS-49).
        /// </summary>
        public void ActivateImmediateAbilities()
        {
            foreach (var idx in AbilityCache.ToList())
            {
                InitializeNewAbility(idx.Key, idx.Value.Spec);
            }
        }
        
        #endregion

        #region Ability Managing
        
        public bool HasAbility(Ability ability)
        {
            return AbilityCache.Values.Any(c => c.Spec.Base == ability);
        }

        public List<AbilitySpecContainer> GetAbilityContainers() => AbilityCache.Values.ToList();

        /// <summary>The granted abilities, without copying them into a list as <see cref="GetAbilityContainers"/> does.</summary>
        public Dictionary<int, AbilitySpecContainer>.ValueCollection Containers => AbilityCache.Values;
        
        public bool TryGetAbilityContainer(int index, out AbilitySpecContainer container)
        {
            return AbilityCache.TryGetValue(index, out container);
        }
        
        public bool TryGetAbilityContainer(Ability ability, out AbilitySpecContainer container)
        {
            foreach (var _container in AbilityCache.Values.Where(_container => _container.Spec.Base == ability))
            {
                container = _container;
                return true;
            }

            container = default;
            return false;
        }
        
        public bool GiveAbility(Ability ability, int level, out int abilityIndex)
        {
            if (!GiveQuietly(ability, level, out abilityIndex)) return false;

            Self.CompileGrantedTags();

            return true;
        }

        /// <summary>
        /// Gives an ability without compiling the tags: into <paramref name="slot"/> where that's free, as a restore puts one
        /// back where it was, or else the first free slot. The restore compiles once as it ends (FEAT-9).
        /// </summary>
        internal bool GiveQuietly(Ability ability, int level, out int abilityIndex, int slot = -1)
        {
            abilityIndex = -1;

            if (!CanGiveAbility(ability)) return false;

            abilityIndex = slot >= 0 && !AbilityCache.ContainsKey(slot) ? slot : GetFirstAvailableCacheIndex();
            if (abilityIndex < 0) return false;

            var container = new AbilitySpecContainer(ability.Generate(Self, level), abilityIndex);
            AbilityCache[abilityIndex] = container;

            ability.InitWorkers(Self);
            // ability.WorkerGroup?.ProvideWorkersTo(Self);

            return true;
        }

        // The slots a restore lets go of, gathered first, as letting go changes the cache
        private readonly List<int> restoreLeaving = new();

        /// <summary>
        /// Ends what's running and lets go of every ability, lent ones too, with nothing left buffered and no tag compile,
        /// but each that <paramref name="keep"/> names in the slot it holds, which stays as it is. A restore gives back
        /// the rest of what the save had, and compiles once as it ends (FEAT-9). It let go of every one and gave each back
        /// as a new one, so another entity's effect that one of them applied kept the old, which no slot held (GAS-60).
        /// </summary>
        internal void ClearForRestore(IReadOnlyDictionary<int, Ability> keep = null)
        {
            if (AbilityCache == null) return;

            buffer.Clear();
            InjectAll(InterruptSequenceInjection.Instance);
            foreach (var active in ActiveCache.Values) active.Clear();

            restoreLeaving.Clear();
            foreach (var pair in AbilityCache)
            {
                if (keep != null && keep.TryGetValue(pair.Key, out var kept) && pair.Value.Spec.Base == kept) continue;
                pair.Value.Spec.Base.RemoveWorkers(Self);
                restoreLeaving.Add(pair.Key);
            }
            foreach (int slot in restoreLeaving) AbilityCache.Remove(slot);
            restoreLeaving.Clear();
        }

        public bool RemoveAbility(Ability ability)
        {
            return TryGetCacheIndexOf(ability, out int index) && RemoveAbilityAt(index);
        }

        /// <summary>
        /// Removes the ability in one slot, if the slot holds <paramref name="expected"/> when one is given. Where an
        /// entity holds the same ability more than once, this takes the copy meant, as an item taking back its grant
        /// does (GAS-48).
        /// </summary>
        public bool RemoveAbilityAt(int index, Ability expected = null)
        {
            if (!AbilityCache.TryGetValue(index, out var container)) return false;
            var ability = container.Spec.Base;
            if (expected != null && ability != expected) return false;

            if (container.IsActive)
                Inject(index, InterruptSequenceInjection.Instance);

            ability.RemoveWorkers(Self);

            // Off the active list too, where the next claim check looked its slot up and threw, and gone before
            // the tags are compiled, which counted it still granted (GAS-20)
            foreach (var active in ActiveCache.Values) active.Remove(index);
            AbilityCache.Remove(index);

            Self.CompileGrantedTags();

            return true;
        }

        public bool CanGiveAbility(Ability ability)
        {
            if (ability == null) return false;
            if (!Enabled) return false;
            if (!allowDuplicateAbilities && HasAbility(ability)) return false;
            if (AbilityCount >= maxAbilitiesOperation.Evaluate(IAttributeImpactCarrier.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

            return true;
        }

        private bool TryGetCacheIndexOf(Ability ability, out int cacheIndex)
        {
            cacheIndex = -1;
            foreach (int index in AbilityCache.Keys.Where(index => AbilityCache[index].Spec.Base == ability))
            {
                cacheIndex = index;
                return true;
            }

            return false;
        }

        private int GetFirstAvailableCacheIndex()
        {
            for (int i = AbilityCache.Count; i >= 0; i--)
            {
                if (!AbilityCache.ContainsKey(i)) return i;
            }

            return -1;
        }

        private void InitializeNewAbility(int abilityIndex, AbilitySpec ability)
        {
            if (!ability.Base.Definition.ActivateImmediately) return;
            
            TryActivateAbility(CreateActivationRequest(abilityIndex));
        }

        #endregion

        #region Ability Handling

        public bool CanActivateAbility(int index)
        {
            return TryGetAbilityContainer(index, out var container)
                   && CanActivateAbility(container, AbilityDataPacket.GenerateFrom(container, this));
            /*return Enabled 
                   && !Locked
                   && AbilityCache.TryGetValue(index, out var container)
                   && (!container.Spec.Base.IgnoreWhenLevelZero || container.Spec.GetLevel() > 0); */
        }
        
        private bool CanActivateAbility(AbilitySpecContainer container, AbilityDataPacket data) =>
            CanActivateUnblocked(container, data) && !IsBlocked(container.Spec.Base);

        // Everything but a running stage's block, which a Queue request waits out in the buffer (FEAT-6)
        private bool CanActivateUnblocked(AbilitySpecContainer container, AbilityDataPacket data) =>
            UnblockedRefusal(container, data, out _) == EActivationRefusal.None;

        // What refuses it, but a running stage's block, as a refusal says it (FEAT-17)
        private EActivationRefusal UnblockedRefusal(AbilitySpecContainer container, AbilityDataPacket data, out string rule)
        {
            rule = null;
            if (!Enabled) return EActivationRefusal.Disabled;
            if (Locked) return EActivationRefusal.Locked;

            // An ability runs as a process, so it can't start while its caster's world refuses new ones (TS-AC). It asked
            // the scene's, wherever the caster was (FEAT-10)
            if (ProcessWorld.Of(data?.EffectOrigin?.GetSource()) is { AcceptsNewProcesses: false }) return EActivationRefusal.NoNewProcesses;

            var source = container.Spec.SourceRefusal(data, out rule);
            if (source != EActivationRefusal.None) return source;
            if (container.Spec.Base.IgnoreWhenLevelZero && container.Spec.GetLevel().CurrentValue <= 0) return EActivationRefusal.LevelZero;

            // Its caster must pass its While active group, as it will for as long as it runs (FEAT-5)
            return PassesWhileActive(container, Self.GetTagCache()) ? EActivationRefusal.None : EActivationRefusal.Requirements;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Cancel and block by tag, and While active (FEAT-5)
        // ═══════════════════════════════════════════════════════════════════════════

        // How many abilities run, over every policy, for a copy of their slots
        private int RunningCount()
        {
            int count = 0;
            foreach (var active in ActiveCache.Values) count += active.Count;
            return count;
        }

        // The running abilities' slots, each once, into a rented array; how many there are
        private int CopyRunning(int[] slots)
        {
            int found = 0;
            foreach (var active in ActiveCache.Values)
                foreach (int index in active)
                    if (Array.IndexOf(slots, index, 0, found) < 0) slots[found++] = index;
            return found;
        }

        // Whether a running ability is in a stage that blocks the one asked for
        private bool IsBlocked(Ability incoming)
        {
            foreach (var active in ActiveCache.Values)
                foreach (int index in active)
                    if (AbilityCache.TryGetValue(index, out var running) && running.IsActive && running.Blocks(incoming)) return true;
            return false;
        }

        // Interrupts each running ability in a stage the activating one cancels, from a copy of their slots, as an
        // interrupted ability can end inside the interrupt, and what its end sets off can change the cache
        private void CancelAbilitiesCancelledBy(Ability incoming)
        {
            int count = RunningCount();
            if (count == 0) return;
            var slots = ArrayPool<int>.Shared.Rent(count);
            try
            {
                int found = CopyRunning(slots);
                for (int i = 0; i < found; i++)
                    if (AbilityCache.TryGetValue(slots[i], out var running) && running.IsActive) running.CancelFor(incoming);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(slots);
            }
        }

        private static bool PassesWhileActive(AbilitySpecContainer container, TagCache tags) =>
            container.Spec.Base.Tags?.TagRequirements?.CheckWhileActiveRequirements(tags) ?? true;

        // Whether the caster's tags changed since the running abilities' While active groups were checked, and whether a
        // check is under way, which a change it sets off waits for
        private bool tagsChangedWhileActive;
        private bool checkingWhileActive;

        /// <summary>
        /// Hears each change to the caster's tags, which a running ability's While active group may fail on, and checks
        /// at once, as tag events are heard. Checked at the next step, a stun let a channel's tick that came due first
        /// land (FEAT-5).
        /// </summary>
        internal void HearTagChange(TagChange change)
        {
            tagsChangedWhileActive = true;
            if (!checkingWhileActive) CheckWhileActive();

            // A tag gained sets off the abilities it triggers (FEAT-4)
            if (change.Added && AbilityCache is { Count: > 0 })
            {
                gainedTags.Add(change.Tag);
                FireTagTriggers();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Triggers (FEAT-4)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends a gameplay event to the entity: each ability it knows whose trigger is a gameplay event under the event's
        /// tag, or a tag above it, activates, in slot order, with the event's data, its target among it. The callbacks
        /// hear the event. How many activated. An ability activated only when code asked for it (FEAT-4).
        /// </summary>
        public int SendGameplayEvent(Tag eventTag, ProcessDataPacket data = null)
        {
            if (!eventTag.IsValid) return 0;
            Callbacks.GameplayEventReceived(eventTag, data);
            return FireTriggers(EAbilityTriggerSource.GameplayEvent, eventTag, data);
        }

        // The tags gained, whose triggers fire one at a time: an ability a tag sets off can grant tags in its turn
        private readonly List<Tag> gainedTags = new();
        private bool firingTagTriggers;

        private void FireTagTriggers()
        {
            if (firingTagTriggers) return;
            firingTagTriggers = true;
            try
            {
                for (int i = 0; i < gainedTags.Count; i++) FireTriggers(EAbilityTriggerSource.TagAdded, gainedTags[i], null);
            }
            finally
            {
                gainedTags.Clear();
                firingTagTriggers = false;
            }
        }

        // Each ability the tag triggers tries to activate, as a request would, with the data
        private int FireTriggers(EAbilityTriggerSource source, Tag tag, ProcessDataPacket data)
        {
            if (AbilityCache == null || AbilityCache.Count == 0 || !Enabled) return 0;
            int fired = 0;
            foreach (int slot in AbilityCache.Keys.OrderBy(index => index).ToList())
            {
                if (!AbilityCache.TryGetValue(slot, out var container) || !IsTriggeredBy(container.Spec.Base, source, tag)) continue;
                // The trigger's impact filters and its chance, rolled once for the event (TW-5)
                var trigger = AcceptingTrigger(container.Spec.Base, source, tag, data);
                if (trigger == null) continue;
                var request = data;
                if (trigger.TargetIn(data) is { } target)
                {
                    // Its own data, so one ability's target isn't another's
                    request = ProcessDataPacket.Default();
                    CopyEvent(data, request);
                    request.SetPrimary(Tags.TARGET_REAL, target);
                }
                if (TryActivateAbility(CreateActivationRequest(slot, request), out _)) fired++;
            }
            return fired;
        }

        // The ability's first trigger that matches and takes the event
        private AbilityTrigger AcceptingTrigger(Ability ability, EAbilityTriggerSource source, Tag tag, ProcessDataPacket data)
        {
            foreach (var trigger in ability.Definition.Triggers)
            {
                if (trigger == null || trigger.Source != source || !trigger.Tag.IsValid || !tag.MatchesOrIsChildOf(trigger.Tag)) continue;
                if (trigger.Accepts(data, Self)) return trigger;
            }
            return null;
        }

        private static void CopyEvent(ProcessDataPacket from, ProcessDataPacket to)
        {
            if (from == null) return;
            if (from.TryGetFirst<ITarget>(Tags.SOURCE, out var source)) to.SetPrimary(Tags.SOURCE, source);
            if (from.TryGetFirst<ITarget>(Tags.SUBJECT, out var subject)) to.SetPrimary(Tags.SUBJECT, subject);
            if (from.TryGetFirst<ImpactData>(Tags.IMPACT, out var impact)) to.SetPrimary(Tags.IMPACT, impact);
        }

        /// <summary>
        /// Whether an ability it knows is set off by a gameplay event under <paramref name="eventTag"/>: the core sends its
        /// own events only where one is, so an entity that listens for none pays nothing (TW-5).
        /// </summary>
        public bool ListensFor(Tag eventTag)
        {
            if (AbilityCache == null || AbilityCache.Count == 0 || !eventTag.IsValid) return false;
            foreach (var container in AbilityCache.Values)
                if (IsTriggeredBy(container.Spec.Base, EAbilityTriggerSource.GameplayEvent, eventTag)) return true;
            return false;
        }

        private static bool IsTriggeredBy(Ability ability, EAbilityTriggerSource source, Tag tag)
        {
            var triggers = ability?.Definition?.Triggers;
            if (triggers == null) return false;
            foreach (var trigger in triggers)
                if (trigger != null && trigger.Source == source && trigger.Tag.IsValid && tag.MatchesOrIsChildOf(trigger.Tag)) return true;
            return false;
        }

        /// <summary>
        /// Interrupts each running ability whose While active group its caster no longer passes, once the tags have
        /// changed: a stun ends a channel. Called as the tags change, and each step as well, after the effects tick.
        /// Requirements weren't checked once an ability ran (FEAT-5).
        /// </summary>
        internal void CheckWhileActive()
        {
            if (checkingWhileActive) return;
            checkingWhileActive = true;
            try
            {
                // Again while an interrupt's own changes ask, each of which ends a running ability, so it stops
                while (tagsChangedWhileActive)
                {
                    tagsChangedWhileActive = false;
                    InterruptFailingWhileActive();
                }
            }
            finally
            {
                checkingWhileActive = false;
            }
        }

        private void InterruptFailingWhileActive()
        {
            int count = RunningCount();
            if (count == 0) return;
            var tags = Self.GetTagCache();
            var slots = ArrayPool<int>.Shared.Rent(count);
            try
            {
                int found = CopyRunning(slots);
                for (int i = 0; i < found; i++)
                {
                    if (!AbilityCache.TryGetValue(slots[i], out var running) || !running.IsActive) continue;
                    if (!PassesWhileActive(running, tags)) running.Inject(InterruptSequenceInjection.Instance);
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(slots);
            }
        }
        
        /// <summary>
        /// Activates the ability the request names, if it may run. The ability runs inside this call until one of its
        /// tasks waits: its claim, its targeting and its first stage. So an instant ability has ended, and its callbacks
        /// have fired, by the time this returns (Decision 25). An ability asked for again while its own activation call
        /// still runs, from its own callbacks or from another ability's that it started, starts next frame instead, so
        /// a loop of instant activations goes round once a frame (TS-8).
        /// </summary>
        /// <returns>True if the ability activated, waits in the input buffer, or was put off to the next frame.</returns>
        public bool TryActivateAbility(AbilityActivationRequest req) => TryActivateAbility(req, out _);

        /// <summary>
        /// Activates the ability the request names, as <see cref="TryActivateAbility(AbilityActivationRequest)"/> does, and
        /// says why where it's refused, as the callbacks' <see cref="AbilitySystemCallbacks.OnAbilityRefused"/> tells it. A
        /// refused activation said only false (FEAT-17).
        /// </summary>
        public bool TryActivateAbility(AbilityActivationRequest req, out ActivationRefusal refusal)
        {
            if (TryActivate(req, fromBuffer: false, out refusal)) return true;
            Callbacks.AbilityRefused(refusal);
            return false;
        }

        /// <summary>
        /// Activates the ability by its asset, in the first slot that holds it, and says why where it's refused. An
        /// ability activated only by its slot (FEAT-17).
        /// </summary>
        public bool TryActivateAbility(Ability ability, out ActivationRefusal refusal, ProcessDataPacket data = null,
            EAbilityActivationPolicyExtended? policy = null)
        {
            if (ability != null && TryGetCacheIndexOf(ability, out int index))
                return TryActivateAbility(CreateActivationRequest(index, data, policy), out refusal);

            refusal = new ActivationRefusal(ability, -1, EActivationRefusal.NotKnown);
            Callbacks.AbilityRefused(refusal);
            return false;
        }

        /// <inheritdoc cref="TryActivateAbility(Ability, out ActivationRefusal, ProcessDataPacket, EAbilityActivationPolicyExtended?)"/>
        public bool TryActivateAbility(Ability ability, ProcessDataPacket data = null, EAbilityActivationPolicyExtended? policy = null) =>
            TryActivateAbility(ability, out _, data, policy);

        /// <summary>
        /// Activates the first ability, by slot, whose asset tag or a context tag is <paramref name="tag"/> or under it, and
        /// says why where none activates: the first one's refusal, or not known where none carries the tag. One call
        /// activates one ability (FEAT-17).
        /// </summary>
        public bool TryActivateAbility(Tag tag, out ActivationRefusal refusal, ProcessDataPacket data = null,
            EAbilityActivationPolicyExtended? policy = null)
        {
            refusal = new ActivationRefusal(null, -1, EActivationRefusal.NotKnown);
            bool found = false;
            if (AbilityCache != null && tag.IsValid)
            {
                foreach (int index in AbilityCache.Keys.OrderBy(slot => slot).ToList())
                {
                    if (!AbilityCache.TryGetValue(index, out var container) || !Carries(container.Spec.Base, tag)) continue;
                    if (TryActivate(CreateActivationRequest(index, data, policy), fromBuffer: false, out var tried))
                    {
                        refusal = tried;
                        return true;
                    }
                    if (!found) refusal = tried;
                    found = true;
                }
            }
            Callbacks.AbilityRefused(refusal);
            return false;
        }

        /// <inheritdoc cref="TryActivateAbility(Tag, out ActivationRefusal, ProcessDataPacket, EAbilityActivationPolicyExtended?)"/>
        public bool TryActivateAbility(Tag tag, ProcessDataPacket data = null, EAbilityActivationPolicyExtended? policy = null) =>
            TryActivateAbility(tag, out _, data, policy);

        // Whether the ability's asset tag, or one of its context tags, is the tag or under it
        private static bool Carries(Ability ability, Tag tag)
        {
            if (ability?.Tags == null) return false;
            if (ability.Tags.AssetTag.MatchesOrIsChildOf(tag)) return true;
            if (ability.Tags.ContextTags == null) return false;
            foreach (var context in ability.Tags.ContextTags)
                if (context.MatchesOrIsChildOf(tag)) return true;
            return false;
        }

        // A request from the buffer doesn't go back in it: it waits on in its own slot, with the time it has left
        private bool TryActivate(AbilityActivationRequest req, bool fromBuffer) => TryActivate(req, fromBuffer, out _);

        private bool TryActivate(AbilityActivationRequest req, bool fromBuffer, out ActivationRefusal refusal)
        {
            // Under its own marker in the Profiler (FEAT-18)
            using var sample = ForgeProfiler.ActivateAbility.Auto();

            // Before the packet, which reads the entity: an ended entity's system is off, and a Mono entity may be
            // destroyed by then (GAS-52)
            if (!Enabled) return Refuse(out refusal, null, req.Index, EActivationRefusal.Disabled);
            if (!AbilityCache.TryGetValue(req.Index, out var container)) return Refuse(out refusal, null, req.Index, EActivationRefusal.NotKnown);
            var ability = container.Spec.Base;
            var data = AbilityDataPacket.GenerateFrom(container.Spec,
                req, ability.Behaviour.UseImplicitTargeting, ability.Behaviour.ImplicitTag);
            var why = UnblockedRefusal(container, data, out string rule);
            if (why != EActivationRefusal.None) return Refuse(out refusal, ability, req.Index, why, rule);

            // Not refused, the request activated, waits in the buffer, or starts next frame
            refusal = new ActivationRefusal(ability, req.Index, EActivationRefusal.None);

            // A running stage's block refuses it, unless, under the Queue policy, it may wait for the block to lift (FEAT-6)
            if (IsBlocked(ability))
            {
                if (!fromBuffer && req.Policy == EAbilityActivationPolicy.QueueActivationIfBusy && BufferActivation(req)) return true;
                return Refuse(out refusal, ability, req.Index, EActivationRefusal.Blocked);
            }

            // Run at once, it nested inside its own activation, and a loop of instant activations nested until the
            // stack overflowed (TS-8)
            if (container.IsActivating)
            {
                ActivateNextFrame(container, req).Forget();
                return true;
            }

            // Busy, a request from the buffer waits on in its slot
            if (fromBuffer && IsClaimingPolicy(req.Policy)) return Refuse(out refusal, ability, req.Index, EActivationRefusal.Busy);

            // Its chance to activate, rolled once for the activation, as it would start (TW-8)
            if (!RollsToActivate(ability, data)) return Refuse(out refusal, ability, req.Index, EActivationRefusal.Chance);

            // Each running ability in a stage this one cancels ends first, so this one starts clear of it (FEAT-5)
            CancelAbilitiesCancelledBy(ability);

            why = ProcessActivationRequest(data);
            if (why != EActivationRefusal.None) return Refuse(out refusal, ability, req.Index, why);
            return true;
        }

        // Whether an ability's chance to activate comes up; one that can't miss draws nothing (TW-8)
        private static bool RollsToActivate(Ability ability, AbilityDataPacket data)
        {
            var chance = ability.Definition?.ChanceToActivate;
            if (chance == null) return true;
            var context = NumberContext.Of(data);
            return ForgeChance.Roll(chance.Evaluate(context), in context, ability.Definition.ActivationRoll, ability);
        }

        private static bool Refuse(out ActivationRefusal refusal, Ability ability, int index, EActivationRefusal why, string rule = null)
        {
            refusal = new ActivationRefusal(ability, index, why, rule);
            return false;
        }

        // Asked for again next frame, as long as its slot still holds it
        private async UniTaskVoid ActivateNextFrame(AbilitySpecContainer container, AbilityActivationRequest req)
        {
            await ForgeTick.NextFrame();

            if (AbilityCache.TryGetValue(req.Index, out var cached) && cached == container) TryActivateAbility(req);
        }
        
        // What the policy refuses, or None where the ability activated or waits in the buffer (FEAT-17)
        private EActivationRefusal ProcessActivationRequest(AbilityDataPacket data)
        {
            var container = AbilityCache[data.Request.Index];
            switch (data.Request.Policy)
            {
                case EAbilityActivationPolicy.AlwaysActivate:
                    if (!AlwaysActivateTargetingValidation(data.Request.Index)) return EActivationRefusal.Critical;
                    return ActivateAbility(container, data) ? EActivationRefusal.None : EActivationRefusal.Busy;

                case EAbilityActivationPolicy.ActivateIfIdle:
                    if (IsClaimingPolicy(EAbilityActivationPolicy.ActivateIfIdle)) return EActivationRefusal.Busy;
                    return ActivateAbility(container, data) ? EActivationRefusal.None : EActivationRefusal.Busy;

                case EAbilityActivationPolicy.QueueActivationIfBusy:
                    if (!IsClaimingPolicy(EAbilityActivationPolicy.QueueActivationIfBusy))
                        return ActivateAbility(container, data) ? EActivationRefusal.None : EActivationRefusal.Busy;
                    return BufferActivation(data.Request) ? EActivationRefusal.None : EActivationRefusal.Busy;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private bool AlwaysActivateTargetingValidation(int abilityIndex)
        {
            if (!IsClaimingPolicyCritical(EAbilityActivationPolicy.AlwaysActivate)) return true;
            return !IsCritical(abilityIndex);
        }

        private bool ActivateAbility(AbilitySpecContainer container, AbilityDataPacket data)
        {
            return container.ActivateAbility(data);
        }

        // Puts a request in its ability's slot in the buffer, which a new press refreshes, with its window. A window of 0
        // doesn't wait, so the request is refused (FEAT-6)
        private bool BufferActivation(AbilityActivationRequest req)
        {
            if (!AbilityCache.TryGetValue(req.Index, out var container)) return false;
            var definition = container.Spec.Base.Definition;
            float window = req.BufferWindow >= 0f ? req.BufferWindow : definition.BufferWindow;
            if (window <= 0f) return false;

            var entry = new BufferedRequest { Request = req, Left = window, Priority = definition.BufferPriority, Order = ++presses };
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].Request.Index != req.Index) continue;
                buffer[i] = entry;
                return true;
            }
            buffer.Add(entry);
            return true;
        }

        /// <summary>
        /// Fires the buffered request that should go first and can start now: the highest priority, then the newest.
        /// One that can't yet, as while a claim or a block holds, waits on in its slot. One fires each time (FEAT-6).
        /// </summary>
        private void FireBuffered()
        {
            if (buffer.Count == 0 || firingBuffer) return;
            firingBuffer = true;
            try
            {
                buffer.Sort(BestFirst.Instance);
                for (int i = 0; i < buffer.Count; i++)
                {
                    // Out of its slot as it fires, so a press it sets off takes the slot afresh
                    var entry = buffer[i];
                    buffer.RemoveAt(i);
                    if (TryActivate(entry.Request, fromBuffer: true)) return;
                    buffer.Insert(i, entry);
                }
            }
            finally
            {
                firingBuffer = false;
            }
        }

        /// <summary>
        /// Tries the buffer, then counts its requests' windows down by the caster's time and lets go of those that ran out.
        /// Each step, after the effects tick, so a press waits out a block's end or a cooldown's in time; paused, the
        /// entity doesn't step, and the windows wait too (FEAT-6).
        /// </summary>
        internal void StepBuffer(float deltaTime)
        {
            if (buffer.Count == 0) return;
            FireBuffered();
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                var entry = buffer[i];
                entry.Left -= deltaTime;
                if (entry.Left <= 0f) buffer.RemoveAt(i);
                else buffer[i] = entry;
            }
        }

        private void ClearAbilityCache()
        {
            if (AbilityCache == null) return;

            foreach (var policy in ActiveCache.Keys)
            {
                // Through Inject, which walks a copy of the set its interrupt empties (GAS-53)
                Inject(policy, InterruptSequenceInjection.Instance);
                ActiveCache[policy].Clear();
            }

            AbilityCache.Clear();
        }

        public void Inject(int index, ISequenceInjection injection)
        {
            if (!AbilityCache.TryGetValue(index, out var container) || !container.IsActive) return;
            container.Inject(injection);
        }

        public void Inject(Ability ability, ISequenceInjection injection)
        {
            if (!TryGetAbilityContainer(ability, out var container) || !container.IsActive) return;
            container.Inject(injection);
        }
        
        public void Inject(EAbilityActivationPolicy policy, ISequenceInjection injection)
        {
            var active = ActiveCache[policy];
            int count = active.Count;
            if (count == 0) return;

            // From a copy: an interrupted ability ends inside the interrupt and takes itself off this set, so walking the
            // set threw once the first had ended, and the rest ran on (GAS-53). Rented, as a stun may interrupt often
            var indices = ArrayPool<int>.Shared.Rent(count);
            try
            {
                active.CopyTo(indices);
                for (int i = 0; i < count; i++)
                {
                    if (AbilityCache.TryGetValue(indices[i], out var container) && container.IsActive) container.Inject(injection);
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(indices);
            }
        }

        public void InjectAll(ISequenceInjection injection)
        {
            foreach (var policy in ActiveCache.Keys)
            {
                Inject(policy, injection);
            }
        }

        public bool ClaimActive(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // The container's own slot, under the policy the activation asked for. The claim went to the first slot
            // holding the same ability (GAS-21), and under the definition's policy, where EndActivation never
            // looked (TS-T)
            if (!AbilityCache.TryGetValue(container.Index, out var cached) || cached != container) return false;

            Callbacks.AbilityClaimed(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            ForgeCounters.Activations.Value++;

            ActiveCache[data.Request.Policy].Add(container.Index);

            return true;
        }

        /// <summary>
        /// Called once the ability has completed all critical sections. Once released, the ability system will re-enable ability activation.
        /// The ability remains in ActiveCache (still executing) — only the reactivation gate opens.
        /// </summary>
        public void ReleaseClaim(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // A removed ability lets go too, or what's queued behind it waits on someone else (GAS-20)
            if (data != null)
            {
                Callbacks.AbilityClaimReleased(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            }

            // The buffer's best request fires now that the claim is released (FEAT-6)
            FireBuffered();
        }

        /// <summary>
        /// Called when the ability has fully completed (all stages done, handle cleaned up).
        /// Removes the ability from ActiveCache.
        /// </summary>
        public void EndActivation(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // A removed ability's slot left the active list with it, and may hold another ability by now (GAS-20)
            if (AbilityCache.TryGetValue(container.Index, out var cached) && cached == container)
            {
                var policy = data?.Request.Policy
                             ?? container.Spec.Base.Definition.ActivationPolicy.Translate(this);

                // Overlapping activations share the slot, which stays while one under this policy still runs (TS-T)
                if (!container.IsActiveUnder(policy)) ActiveCache[policy].Remove(container.Index);
            }

            // After the active list is told, so that a listener finds the ability over (TS-9)
            if (data != null)
            {
                Callbacks.AbilityEnded(AbilityCallbackStatus.GenerateForAbilityEvent(data));
            }
        }

        #endregion

        #region Impact Workers

        /// <summary>
        /// Process impact data dealt by this GAS through source-side workers, record it
        /// to the source's frame summary, and fire source-side GAS callbacks.
        /// Inline workers execute immediately, deferred workers queue actions.
        /// </summary>
        public void ProvideFrameImpactDealt(ImpactData impactData, bool runWorkers = true)
        {
            // Track impact on derivation
            impactData.SourcedModifier.Carrier.TrackImpact(impactData);

            // Run effect-specific workers — only ICarrierApplication carriers have a
            // worker lifecycle. Synthetic delta carriers (SourceAttributeImpact, etc.)
            // skip this entirely.
            if (impactData.SourcedModifier.Carrier is ICarrierApplication appCarrier)
            {
                var effectContext = new EffectWorkerContext(
                    Self, impactData.SourcedModifier.Carrier,
                    Self.GetFrameSummary(), Self.GetActionQueue(),
                    1, impactData);
                appCarrier.RunWorkerImpact(effectContext);
            }

            // An effect's own lifesteal, for the hits it deals (TW-15)
            if (impactData.SourcedModifier.Carrier is GameplayEffectSpec dealt && dealt.Base.Lifesteal is { Steals: true } steal)
                ForgeLifesteal.Steal(Self, impactData, steal);

            // Run global impact workers (source-side)
            if (runWorkers)
            {
                ImpactWorkerCache.RunImpactData(impactData, Self);
            }

            // Record DEALT impact on this (source) GAS — fires source-side callbacks
            Self.RecordFrameImpactDealt(impactData);
        }
        
        /// <summary>
        /// Register an impact worker.
        /// </summary>
        public void ProvideWorker(AbstractImpactWorker worker)
        {
            ImpactWorkerCache?.ProvideWorker(worker);
        }
        
        /// <summary>
        /// Unregister an impact worker.
        /// </summary>
        public void RemoveWorker(AbstractImpactWorker worker)
        {
            ImpactWorkerCache?.RemoveWorker(worker);
        }

        #endregion
    }

    // What an activation does when another holds its claim, which the inspectors show as When Busy. The names the
    // inspectors show say the choice, as the Ability mockup does: Queue, not Queue Activation If Busy (ED-37)
    public enum EAbilityActivationPolicy
    {
        /// <summary>
        /// Do not restrict activation attempts to this ability
        /// </summary>
        [InspectorName("Activate Anyway")] AlwaysActivate,

        /// <summary>
        /// Activate this ability only if ability system is idle
        /// </summary>
        [InspectorName("Refuse")] ActivateIfIdle,

        /// <summary>
        /// If ability system is busy, queue activation attempt
        /// </summary>
        [InspectorName("Queue")] QueueActivationIfBusy
    }

    public enum EAbilityActivationPolicyExtended
    {
        /// <summary>
        /// Use whatever the local source policy is
        /// </summary>
        [InspectorName("Entity's Policy")] UseLocalPolicy,

        /// <summary>
        /// Do not restrict activation attempts to this ability
        /// </summary>
        [InspectorName("Activate Anyway")] AlwaysActivate,

        /// <summary>
        /// Activate this ability only if ability system is idle
        /// </summary>
        [InspectorName("Refuse")] ActivateIfIdle,

        /// <summary>
        /// If ability system is busy, queue activation attempt
        /// </summary>
        [InspectorName("Queue")] QueueActivationIfBusy
    }
}