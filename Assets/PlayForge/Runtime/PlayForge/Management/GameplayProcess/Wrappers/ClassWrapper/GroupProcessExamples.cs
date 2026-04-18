using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Examples
{
    /// <summary>
    /// Demo processes that demonstrate Group Process functionality with Synchronous lifecycle.
    ///
    /// Setup:
    ///   1. Ensure a ProcessControl instance exists in the scene.
    ///   2. Add one of the MonoBehaviour demos to a GameObject, OR
    ///      instantiate the runtime demos from code.
    ///   3. Enter Play mode.
    /// </summary>
    public static class GroupProcessExamples
    {
        public static readonly Tag BUFF_GROUP = Tag.GenerateAsUnique("Demo.BuffGroup");
        public static readonly Tag HAZARD_GROUP = Tag.GenerateAsUnique("Demo.HazardGroup");
        public static readonly Tag SQUAD_GROUP = Tag.GenerateAsUnique("Demo.SquadGroup");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 1 — Buff Stack (EvictOldest overflow, shared data)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simulates a timed buff that registers into a group with a max stack count.
    /// When the group is full, the oldest buff is evicted (FIFO stack replacement).
    /// Each buff runs a Synchronous countdown using a sync sequence.
    ///
    /// Usage:
    ///   // Pre-create the group with a stack cap:
    ///   var group = new GroupProcess("Buffs", maxMembers: 3, overflowPolicy: EGroupOverflowPolicy.EvictOldest);
    ///   ProcessControl.RegisterGroup(GroupProcessExamples.BUFF_GROUP, group, out _);
    ///
    ///   // Then add buffs:
    ///   var buff = new GroupDemo_TimedBuff("Attack+", 5f);
    ///   ProcessControl.RegisterWithGroup(GroupProcessExamples.BUFF_GROUP, buff, out var relay);
    /// </summary>
    public class GroupDemo_TimedBuff : LazyRuntimeProcess
    {
        private readonly float _duration;
        private float _remaining;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        public float Remaining => _remaining;

        public GroupDemo_TimedBuff(string buffName, float duration)
            : base($"Buff:{buffName}", EProcessStepPriorityMethod.First, 0,
                   EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            _duration = duration;
            _remaining = duration;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create($"Buff Timer ({name})")
                .Stage(s => s
                    .WithName("Countdown")
                    .SyncTask((_, dt) =>
                    {
                        _remaining -= dt;
                        return _remaining <= 0f;
                    })
                )
                .BuildSyncRunner();

            Debug.Log($"[GroupDemo_TimedBuff] '{name}' started ({_duration}s)");
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            if (_runner.Step(_data, Time.deltaTime))
            {
                Debug.Log($"[GroupDemo_TimedBuff] '{name}' expired");
                relay.Terminate();
            }
        }

        public override void WhenTerminate(ProcessRelay relay)
        {
            Debug.Log($"[GroupDemo_TimedBuff] '{name}' terminated ({_remaining:F1}s remaining)");
            base.WhenTerminate(relay);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 2 — Hazard Zone Pulse (Shared data, auto-terminate when empty)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A pulsing hazard process that registers into a shared hazard group.
    /// All hazards in the group share the same data packet, so they can
    /// read/write shared state (e.g. a cumulative damage counter).
    ///
    /// Usage:
    ///   var hazard = new GroupDemo_HazardPulse(myTransform, 1f, 10f);
    ///   ProcessControl.RegisterWithGroup(GroupProcessExamples.HAZARD_GROUP, hazard, out var relay);
    /// </summary>
    public class GroupDemo_HazardPulse : LazyRuntimeProcess
    {
        private readonly Transform _target;
        private readonly float _interval;
        private readonly float _damage;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        private static readonly Tag TOTAL_DAMAGE = Tag.GenerateAsUnique("Demo.HazardTotalDamage");

        public GroupDemo_HazardPulse(Transform target, float interval, float damage)
            : base("HazardPulse", EProcessStepPriorityMethod.First, 0,
                   EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            _target = target;
            _interval = interval;
            _damage = damage;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Hazard Pulse")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(_interval))
                )
                .Stage(s => s
                    .WithName("Pulse")
                    .Do(_ =>
                    {
                        // Shared data: all hazards in the group write to the same ProcessDataPacket.
                        // We use regData directly (not the SequenceDataPacket copy) so mutations
                        // are visible to every process sharing this packet.
                        float total = regData.GetPrimary<float>(TOTAL_DAMAGE, 0f);
                        total += _damage;
                        regData.SetPrimary(TOTAL_DAMAGE, total);

                        Debug.Log($"[GroupDemo_HazardPulse] Pulse! Damage: {_damage:F1} " +
                                  $"(Group total: {total:F1})");
                    })
                    .Task(SyncSeq.PunchScale(_target, 0.2f, 0.3f))
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            if (_target == null)
            {
                relay.Terminate();
                return;
            }

            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 3 — Squad Formation (Group iteration, standalone data)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A squad member process that follows a leader at an offset.
    /// Each member uses its own data packet (standalone), but the group
    /// provides coordination via ForEach for formation recalculation.
    ///
    /// Usage:
    ///   // Create leader and members
    ///   var member1 = new GroupDemo_SquadMember(unit1, leader, Vector3.left * 2f, 4f);
    ///   var member2 = new GroupDemo_SquadMember(unit2, leader, Vector3.right * 2f, 4f);
    ///
    ///   ProcessControl.RegisterWithGroup(GroupProcessExamples.SQUAD_GROUP, member1, out _, useSharedData: false);
    ///   ProcessControl.RegisterWithGroup(GroupProcessExamples.SQUAD_GROUP, member2, out _, useSharedData: false);
    ///
    ///   // Later, iterate the group:
    ///   if (ProcessControl.TryGetGroup(GroupProcessExamples.SQUAD_GROUP, out var squad))
    ///   {
    ///       squad.ForEach&lt;GroupDemo_SquadMember&gt;((member, relay) =>
    ///       {
    ///           Debug.Log($"{member.ProcessName} at offset {member.FormationOffset}");
    ///       });
    ///   }
    /// </summary>
    public class GroupDemo_SquadMember : LazyRuntimeProcess
    {
        private readonly Transform _unit;
        private readonly Transform _leader;
        private readonly float _speed;

        /// <summary>Offset from leader position in the formation.</summary>
        public Vector3 FormationOffset { get; set; }

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        public GroupDemo_SquadMember(Transform unit, Transform leader, Vector3 offset, float speed)
            : base($"Squad:{unit.name}", EProcessStepPriorityMethod.First, 0,
                   EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            _unit = unit;
            _leader = leader;
            _speed = speed;
            FormationOffset = offset;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create($"Squad Follow ({_unit.name})")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Follow Leader")
                    .SyncTask((_, dt) =>
                    {
                        if (!_leader || !_unit) return true;

                        var targetPos = _leader.position + _leader.TransformDirection(FormationOffset);
                        _unit.position = Vector3.MoveTowards(_unit.position, targetPos, _speed * dt);

                        // Never completes — always following
                        return false;
                    })
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            if (!_unit || !_leader)
            {
                relay.Terminate();
                return;
            }

            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 4 — MonoBehaviour Group Controller
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A scene-placed MonoBehaviour that creates a buff group with configurable
    /// max stacks and periodically adds new buffs. Demonstrates inspector-driven
    /// group configuration with Synchronous lifecycle.
    ///
    /// Attach to any GameObject. Requires ProcessControl in the scene.
    /// </summary>
    public class GroupDemo_BuffStackController : LazyMonoProcess
    {
        [Header("Group Config")]
        [Tooltip("Maximum number of simultaneous buffs")]
        public int maxStacks = 3;

        [Header("Auto-Spawn")]
        [Tooltip("Seconds between auto-spawned buffs")]
        public float spawnInterval = 2f;
        [Tooltip("Duration of each spawned buff")]
        public float buffDuration = 5f;

        private static readonly Tag BUFF_GROUP_LOCAL = Tag.GenerateAsUnique("Demo.BuffGroupLocal");

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;
        private int _buffCounter;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            _data = new SequenceDataPacket(regData);
            _buffCounter = 0;

            // Create the group with EvictOldest so old buffs get replaced
            var group = new GroupProcess("LocalBuffs",
                maxMembers: maxStacks,
                overflowPolicy: EGroupOverflowPolicy.EvictOldest);

            group.OnMemberJoined += r => Debug.Log($"[BuffController] Buff joined. Stack: {group.MemberCount}/{maxStacks}");
            group.OnMemberLeft += r => Debug.Log($"[BuffController] Buff left. Stack: {group.MemberCount}/{maxStacks}");
            group.OnGroupEmpty += () => Debug.Log("[BuffController] All buffs expired!");

            // TerminateWhenEmpty = false so the group persists even when all buffs expire
            group.TerminateWhenEmpty = false;

            ProcessControl.RegisterGroup(BUFF_GROUP_LOCAL, group, out _);

            // Build a repeating spawn sequence
            _runner = TaskSequenceBuilder.Create("Buff Spawner")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(spawnInterval))
                )
                .Stage(s => s
                    .WithName("Spawn Buff")
                    .Do(_ =>
                    {
                        _buffCounter++;
                        var buff = new GroupDemo_TimedBuff($"Buff#{_buffCounter}", buffDuration);
                        ProcessControl.RegisterWithGroup(BUFF_GROUP_LOCAL, buff, out var _);
                    })
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }
}
