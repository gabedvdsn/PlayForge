using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.ProcessGroups
{
    /// <summary>
    /// Process groups, each shown from a key. A group is a process that holds other processes: each member still runs as a
    /// process of its own, registered with ProcessControl, and the group adds what they have in common: how many there
    /// may be and what happens to one more, a data packet they can share, what's done to all of them at once, and how long
    /// the group itself lasts. Put this component and a Bootstrapper in a scene, enter Play mode and press the keys; the
    /// Console says what each one did.
    /// </summary>
    public class ProcessGroupExamples : MonoBehaviour
    {
        [Tooltip("Adds a buff to a group of at most Max Buffs: one more pushes the oldest out")]
        public KeyCode AddBuffKey = KeyCode.Alpha1;
        [Min(1)] public int MaxBuffs = 3;
        [Min(0.1f)] public float BuffDuration = 6f;

        [Tooltip("Summons into a group of at most Max Summons: one more is refused")]
        public KeyCode SummonKey = KeyCode.Alpha2;
        [Min(1)] public int MaxSummons = 2;

        [Tooltip("Starts a hazard. Hazards share their group's data packet, so their pulses add up to one total")]
        public KeyCode HazardKey = KeyCode.Alpha3;
        [Min(0.1f)] public float HazardInterval = 1f;
        public float HazardDamage = 5f;
        [Min(1)] public int HazardPulses = 4;

        [Tooltip("Forms the squad, or disbands it: the units follow the leader, and take new places as one joins or leaves")]
        public KeyCode SquadKey = KeyCode.Alpha4;
        public Transform SquadLeader;
        public List<Transform> SquadUnits = new();
        [Min(0.1f)] public float SquadSpacing = 1.5f;
        [Min(0.1f)] public float SquadSpeed = 4f;

        [Tooltip("Pauses every member of every group, or unpauses them")]
        public KeyCode PauseKey = KeyCode.P;
        [Tooltip("Lists each group's members, oldest first")]
        public KeyCode ListKey = KeyCode.L;
        [Tooltip("Takes the oldest summon out of its group. It goes on, a process on its own")]
        public KeyCode DetachKey = KeyCode.D;
        [Tooltip("Ends every buff at once. The buffs' group stays for the next one")]
        public KeyCode ClearBuffsKey = KeyCode.C;
        [Tooltip("Ends the summons' group itself, which ends its members with it")]
        public KeyCode DismissKey = KeyCode.X;

        // Each group is found by its tag, so any code that has the tag reaches the group: ProcessControl.TryGetGroup
        public static readonly Tag BUFFS = Tag.GenerateAsUnique("ProcessGroupExamples.Buffs");
        public static readonly Tag SUMMONS = Tag.GenerateAsUnique("ProcessGroupExamples.Summons");
        public static readonly Tag HAZARDS = Tag.GenerateAsUnique("ProcessGroupExamples.Hazards");
        public static readonly Tag SQUAD = Tag.GenerateAsUnique("ProcessGroupExamples.Squad");
        private static readonly Tag[] Groups = { BUFFS, SUMMONS, HAZARDS, SQUAD };

        private int buffs;
        private int summons;
        private int hazards;
        private bool paused;

        private void Update()
        {
            if (!ProcessControl.Instance) return;
            var input = ForgeInput.Current;
            if (input.KeyPressed(AddBuffKey)) AddBuff();
            if (input.KeyPressed(SummonKey)) CallSummon();
            if (input.KeyPressed(HazardKey)) StartHazard();
            if (input.KeyPressed(SquadKey)) ToggleSquad();
            if (input.KeyPressed(PauseKey)) TogglePause();
            if (input.KeyPressed(ListKey)) List();
            if (input.KeyPressed(DetachKey)) DetachOldestSummon();
            if (input.KeyPressed(ClearBuffsKey)) ClearBuffs();
            if (input.KeyPressed(DismissKey)) DismissSummons();
        }

        // ─── A group made first, to configure it ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// The buffs' group is made before its first member, as a group whose settings matter has to be: at most
        /// Max Buffs, the oldest pushed out to make room for a new one, and kept when its last buff ends, so it's there
        /// for the next. Its events say who joins and who leaves; a buff pushed out leaves as one that ran out does.
        /// </summary>
        private void AddBuff()
        {
            if (!ProcessControl.HasGroup(BUFFS))
            {
                var group = new GroupProcess("Buffs", MaxBuffs, EGroupOverflowPolicy.EvictOldest) { TerminateWhenEmpty = false };
                group.OnMemberJoined += relay => ForgeLog.Info($"Buffs: {NameOf(relay)} joins, {group.MemberCount} of {group.MaxMembers}");
                group.OnMemberLeft += relay => ForgeLog.Info($"Buffs: {NameOf(relay)} leaves, {group.MemberCount} of {group.MaxMembers}");
                group.OnGroupEmpty += () => ForgeLog.Info("Buffs: none left. The group stays, as Terminate When Empty is off");
                ProcessControl.RegisterGroup(BUFFS, group, out _);
            }

            ProcessControl.RegisterWithGroup(BUFFS, new TimedBuff($"Buff {++buffs}", BuffDuration), out _);
        }

        /// <summary>
        /// The summons' group refuses a member past its cap, rather than pushing one out: RegisterWithGroup returns false,
        /// and the summon it was given is ended. Each summon joins with a data packet of its own, which carries the name
        /// it's called by, rather than the group's.
        /// </summary>
        private void CallSummon()
        {
            if (!ProcessControl.HasGroup(SUMMONS))
            {
                // Reject is the default overflow, said here so it's seen
                var group = new GroupProcess("Summons", MaxSummons, EGroupOverflowPolicy.Reject);
                ProcessControl.RegisterGroup(SUMMONS, group, out _);
            }

            var data = ProcessDataPacket.Default();
            data.SetPrimary(Summon.NAME, $"Summon {++summons}");
            if (!ProcessControl.RegisterWithGroup(SUMMONS, new Summon(), data, out _))
                ForgeLog.Info($"Summons: the group is full at {MaxSummons}, so Summon {summons} was refused");
        }

        // ─── A group made by its first member ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// No one makes the hazards' group: the first hazard to register makes it, with no cap, and it ends as its last
        /// hazard does. A member joins with the group's data packet unless it asks for its own, so every hazard reads and
        /// writes the one total.
        /// </summary>
        private void StartHazard()
        {
            bool first = !ProcessControl.HasGroup(HAZARDS);
            ProcessControl.RegisterWithGroup(HAZARDS,
                new HazardPulse($"Hazard {++hazards}", HazardInterval, HazardDamage, HazardPulses), out _);

            if (!first || !ProcessControl.TryGetGroup(HAZARDS, out var group)) return;
            ForgeLog.Info("Hazards: the first hazard made the group, which ends with its last");
            group.OnGroupEmpty += () => ForgeLog.Info($"Hazards: the last pulse is done, {group.Data.GetPrimary(HazardPulse.TOTAL, 0f):0.#} " +
                                                      "dealt in all, and the group ends");
        }

        // ─── What a group does for all its members ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The squad's members each keep a data packet of their own, as following a leader shares nothing. What the group
        /// gives them is each other: as a unit joins or leaves, ForEach goes through the members in the order they joined
        /// and gives each its place, so the line closes up.
        /// </summary>
        private void ToggleSquad()
        {
            if (ProcessControl.TryGetGroup(SQUAD, out var formed))
            {
                // Ending the group ends its members, as Terminate Members On Group Terminate is on by default
                ForgeLog.Info($"Squad: disbanded, {formed.MemberCount} members with it");
                formed.ProcessRelay.Terminate();
                return;
            }

            if (!SquadLeader || SquadUnits.Count == 0)
            {
                ForgeLog.Warning("Squad: give the component a Squad Leader and Squad Units to form a squad", this);
                return;
            }

            var group = new GroupProcess("Squad");
            group.OnMemberJoined += _ => Reform(group);
            group.OnMemberLeft += _ => Reform(group);
            ProcessControl.RegisterGroup(SQUAD, group, out _);

            foreach (var unit in SquadUnits)
                if (unit) ProcessControl.RegisterWithGroup(SQUAD, new SquadMember(unit, SquadLeader, SquadSpeed), out _, useSharedData: false);
            ForgeLog.Info($"Squad: formed, {group.MemberCount} units behind {SquadLeader.name}");
        }

        // Each member's place: a line behind the leader, centred on it, in the order they joined
        private void Reform(GroupProcess squad)
        {
            int count = squad.MemberCount;
            int index = 0;
            squad.ForEach<SquadMember>((member, _) =>
            {
                member.Offset = new Vector3((index - (count - 1) * 0.5f) * SquadSpacing, 0f, -SquadSpacing);
                index++;
            });
        }

        /// <summary>
        /// PauseAll holds every member where it is, and UnpauseAll lets them go on: a buff's countdown stops, a hazard's
        /// pulses wait. The groups themselves keep running, so a group still takes new members while its others are paused.
        /// </summary>
        private void TogglePause()
        {
            paused = !paused;
            foreach (var tag in Groups)
            {
                if (!ProcessControl.TryGetGroup(tag, out var group)) continue;
                if (paused) group.PauseAll();
                else group.UnpauseAll();
            }
            ForgeLog.Info(paused ? "Every group's members are paused" : "Every group's members go on");
        }

        /// <summary>
        /// What a group says of its members: how many, whether it's full, each member by ForEach in the order they joined,
        /// the oldest and the newest, and the first of a kind with TryGetFirst.
        /// </summary>
        private void List()
        {
            var text = new StringBuilder();
            foreach (var tag in Groups)
            {
                if (!ProcessControl.TryGetGroup(tag, out var group))
                {
                    text.AppendLine($"{tag.GetName()}: no group");
                    continue;
                }

                text.Append($"{group.GetName()}: {group.MemberCount} member(s)");
                if (group.MaxMembers > 0) text.Append(group.IsFull ? ", full" : $" of {group.MaxMembers}");
                text.AppendLine();
                group.ForEach(relay => text.AppendLine($"  {NameOf(relay)}, {relay.State}, out {relay.Lifetime:0.0} s"));
                if (!group.IsEmpty)
                    text.AppendLine($"  oldest {NameOf(group.GetOldest())}, newest {NameOf(group.GetNewest())}");
                if (group.TryGetFirst<TimedBuff>(out var buff, out _))
                    text.AppendLine($"  the first buff has {buff.Remaining:0.0} s left");
            }
            ForgeLog.Info(text.ToString().TrimEnd());
        }

        /// <summary>
        /// DetachMember takes a member out without ending it: the summon goes on as a process of its own, no longer
        /// counted against the cap, and no longer ended with the group.
        /// </summary>
        private void DetachOldestSummon()
        {
            if (!ProcessControl.TryGetGroup(SUMMONS, out var group) || group.IsEmpty)
            {
                ForgeLog.Info("Summons: none to detach");
                return;
            }

            var oldest = group.GetOldest();
            group.DetachMember(oldest);
            ForgeLog.Info($"Summons: {NameOf(oldest)} is detached and goes on alone; in the group still: {group.HasMember(oldest)}");
        }

        /// <summary>
        /// TerminateAllMembers ends each member, and the group stays where Terminate When Empty is off, as the buffs' is.
        /// </summary>
        private void ClearBuffs()
        {
            if (!ProcessControl.TryGetGroup(BUFFS, out var group)) return;
            ForgeLog.Info($"Buffs: ending all {group.MemberCount}");
            group.TerminateAllMembers();
        }

        /// <summary>
        /// Ending a group ends its members with it, unless Terminate Members On Group Terminate is off. A summon detached
        /// before is no member, so it stays.
        /// </summary>
        private void DismissSummons()
        {
            if (!ProcessControl.TryGetGroup(SUMMONS, out var group)) return;
            ForgeLog.Info($"Summons: the group ends, and its {group.MemberCount} member(s) with it");
            group.ProcessRelay.Terminate();
        }

        // A member's name, as the process it is gives it
        private static string NameOf(ProcessRelay relay) =>
            relay == null ? "none" : relay.TryGetProcess<Summon>(out var summon) ? summon.Called : relay.Wrapper.ProcessName;
    }
}
