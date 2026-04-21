using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What happens when a new member tries to join a full group.
    /// </summary>
    public enum EGroupOverflowPolicy
    {
        /// <summary>Reject the new member. Registration returns false.</summary>
        Reject,

        /// <summary>Terminate the oldest member (FIFO) to make room for the new one.</summary>
        EvictOldest
    }

    /// <summary>
    /// A container process that other processes can register into.
    ///
    /// Members are still their own processes in ProcessControl — they move through
    /// Running/Waiting/Terminated as normal. The group adds coordinated lifecycle
    /// management, shared data, member-cap enforcement, and group-level operations.
    ///
    /// Usage:
    ///   // Registration auto-creates the group if it doesn't exist:
    ///   ProcessControl.RegisterWithGroup(myGroupTag, someProcess, out relay);
    ///
    ///   // Or create the group manually for configuration:
    ///   var group = new GroupProcess("Buffs", maxMembers: 5, overflowPolicy: EGroupOverflowPolicy.EvictOldest);
    ///   ProcessControl.RegisterGroup(myGroupTag, group, out groupRelay);
    ///   ProcessControl.RegisterWithGroup(myGroupTag, someProcess, out relay);
    /// </summary>
    public class GroupProcess : LazyRuntimeProcess
    {
        /// <summary>
        /// Ordered list of member cache indices. Insertion order = age (index 0 is oldest).
        /// </summary>
        private readonly List<int> _members = new();

        /// <summary>
        /// Map from member cache index to their ProcessRelay for quick access.
        /// </summary>
        private readonly Dictionary<int, ProcessRelay> _memberRelays = new();

        /// <summary>
        /// Maximum number of members allowed. 0 = unlimited.
        /// </summary>
        public int MaxMembers { get; set; }

        /// <summary>
        /// What happens when the group is full and a new member tries to join.
        /// </summary>
        public EGroupOverflowPolicy OverflowPolicy { get; set; }

        /// <summary>
        /// When true, the group auto-terminates when its last member is removed.
        /// </summary>
        public bool TerminateWhenEmpty { get; set; } = true;

        /// <summary>
        /// When true (default), member processes that haven't explicitly opted out
        /// will be terminated when the group is terminated.
        /// </summary>
        public bool TerminateMembersOnGroupTerminate { get; set; } = true;

        // Callbacks
        public event Action<ProcessRelay> OnMemberJoined;
        public event Action<ProcessRelay> OnMemberLeft;
        public event Action OnGroupEmpty;

        /// <summary>Number of active members in this group.</summary>
        public int MemberCount => _members.Count;

        /// <summary>True when the group has no members.</summary>
        public bool IsEmpty => _members.Count == 0;

        /// <summary>True when the group has reached its member cap (and MaxMembers > 0).</summary>
        public bool IsFull => MaxMembers > 0 && _members.Count >= MaxMembers;

        #region Construction

        public GroupProcess(string name, int maxMembers = 0, EGroupOverflowPolicy overflowPolicy = EGroupOverflowPolicy.Reject)
            : base(name, EProcessStepPriorityMethod.First, 0, EProcessStepTiming.None, EProcessLifecycle.Synchronous)
        {
            MaxMembers = maxMembers;
            OverflowPolicy = overflowPolicy;
        }

        #endregion

        #region Member Management

        /// <summary>
        /// Adds a process as a member of this group.
        /// Called internally by ProcessControl.RegisterWithGroup.
        /// </summary>
        /// <returns>True if the member was added successfully.</returns>
        internal bool AddMember(ProcessRelay relay)
        {
            if (relay == null) return false;
            if (_memberRelays.ContainsKey(relay.CacheIndex)) return false;

            // Check capacity
            if (IsFull)
            {
                switch (OverflowPolicy)
                {
                    case EGroupOverflowPolicy.Reject:
                        return false;
                    case EGroupOverflowPolicy.EvictOldest:
                        EvictOldest();
                        break;
                }
            }

            _members.Add(relay.CacheIndex);
            _memberRelays[relay.CacheIndex] = relay;

            // Watch the member so we can clean up when it terminates
            ProcessControl.Instance.WatchProcess(relay, this,
                onUnregister: _ => RemoveMember(relay.CacheIndex));

            OnMemberJoined?.Invoke(relay);

            return true;
        }

        /// <summary>
        /// Removes a member by cache index. Called automatically when a member terminates.
        /// </summary>
        private void RemoveMember(int cacheIndex)
        {
            if (!_memberRelays.TryGetValue(cacheIndex, out var relay)) return;

            _members.Remove(cacheIndex);
            _memberRelays.Remove(cacheIndex);

            OnMemberLeft?.Invoke(relay);

            if (IsEmpty)
            {
                OnGroupEmpty?.Invoke();

                if (TerminateWhenEmpty && !_terminating && Relay != null && Relay.ProcessActive)
                {
                    Relay.Terminate();
                }
            }
        }

        /// <summary>
        /// Terminates the oldest member to make room for a new one.
        /// Uses TerminateImmediate to ensure the slot is freed synchronously,
        /// which is required for the subsequent AddMember call to succeed.
        /// </summary>
        private void EvictOldest()
        {
            if (_members.Count == 0) return;

            int oldest = _members[0];
            if (_memberRelays.TryGetValue(oldest, out var relay))
            {
                relay.TerminateImmediate();
            }
        }

        /// <summary>
        /// Manually removes a member from the group without terminating it.
        /// The process continues to run independently in ProcessControl.
        /// </summary>
        public bool DetachMember(ProcessRelay relay)
        {
            if (relay == null || !_memberRelays.ContainsKey(relay.CacheIndex)) return false;

            ProcessControl.Instance.StopWatchingProcess(relay);
            RemoveMember(relay.CacheIndex);
            return true;
        }

        /// <summary>
        /// Checks whether a process is a member of this group.
        /// </summary>
        public bool HasMember(ProcessRelay relay) => relay != null && _memberRelays.ContainsKey(relay.CacheIndex);

        /// <summary>
        /// Checks whether a process with the given cache index is a member.
        /// </summary>
        public bool HasMember(int cacheIndex) => _memberRelays.ContainsKey(cacheIndex);

        #endregion

        #region Group Operations

        /// <summary>
        /// Pauses all members in the group.
        /// </summary>
        public void PauseAll(bool cascade = false)
        {
            foreach (int idx in _members)
            {
                if (_memberRelays.TryGetValue(idx, out var relay) && relay.IsRunning)
                    relay.Pause(cascade);
            }
        }

        /// <summary>
        /// Resumes all members in the group.
        /// </summary>
        public void UnpauseAll(bool cascade = false)
        {
            foreach (int idx in _members)
            {
                if (_memberRelays.TryGetValue(idx, out var relay) && relay.IsWaiting)
                    relay.Unpause(cascade);
            }
        }

        /// <summary>
        /// Terminates all members in the group.
        /// The group itself may also terminate if TerminateWhenEmpty is true.
        /// </summary>
        public void TerminateAllMembers(bool cascade = false)
        {
            // Copy to avoid modification during iteration
            var indices = new List<int>(_members);
            foreach (int idx in indices)
            {
                if (_memberRelays.TryGetValue(idx, out var relay))
                    relay.Terminate(cascade);
            }
        }

        /// <summary>
        /// Iterates all member relays. Safe for read-only operations.
        /// </summary>
        public void ForEach(Action<ProcessRelay> action)
        {
            foreach (int idx in _members)
            {
                if (_memberRelays.TryGetValue(idx, out var relay))
                    action(relay);
            }
        }

        /// <summary>
        /// Iterates all members, attempting to extract process of type T.
        /// Only invokes the action for members that match the type.
        /// </summary>
        public void ForEach<T>(Action<T, ProcessRelay> action)
        {
            foreach (int idx in _members)
            {
                if (_memberRelays.TryGetValue(idx, out var relay) && relay.TryGetProcess<T>(out var process))
                    action(process, relay);
            }
        }

        /// <summary>
        /// Returns the first member matching type T, or false if none found.
        /// </summary>
        public bool TryGetFirst<T>(out T process, out ProcessRelay relay)
        {
            foreach (int idx in _members)
            {
                if (_memberRelays.TryGetValue(idx, out relay) && relay.TryGetProcess(out process))
                    return true;
            }

            process = default;
            relay = null;
            return false;
        }

        /// <summary>
        /// Returns all member relays as a read-only list.
        /// </summary>
        public IReadOnlyList<ProcessRelay> GetMembers()
        {
            return _memberRelays.Values.ToList();
        }

        /// <summary>
        /// Returns the relay of the oldest member, or null if empty.
        /// </summary>
        public ProcessRelay GetOldest()
        {
            if (_members.Count == 0) return null;
            return _memberRelays.TryGetValue(_members[0], out var relay) ? relay : null;
        }

        /// <summary>
        /// Returns the relay of the newest member, or null if empty.
        /// </summary>
        public ProcessRelay GetNewest()
        {
            if (_members.Count == 0) return null;
            return _memberRelays.TryGetValue(_members[^1], out var relay) ? relay : null;
        }

        #endregion

        #region Lifecycle Overrides

        private bool _terminating;

        public override void WhenTerminate()
        {
            _terminating = true;

            if (TerminateMembersOnGroupTerminate)
            {
                TerminateAllMembers();
            }

            base.WhenTerminate();
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            // Synchronous lifecycle - RunProcess is never called.
            // The group stays alive via ProcessControl stepping until terminated.
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }

        #endregion

        #region Readable Definition

        public override string GetName() => $"Group: {ProcessName}";
        public override string GetDescription() => $"Group process '{ProcessName}' with {MemberCount} member(s).";

        #endregion
    }
}
