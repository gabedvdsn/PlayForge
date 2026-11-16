# PlayForge Process Group Examples

A group is a process that holds other processes. Each member still runs as a process of its own, registered with
ProcessControl and stepped, paused and ended as any process is. The group adds what its members have in common:

- **A cap**, and what happens to one member more: it's refused (`EGroupOverflowPolicy.Reject`), or the oldest member is
  ended to make room (`EvictOldest`).
- **A data packet they can share.** A member joins with the group's packet unless it brings its own, so what one
  writes, the others read.
- **What's done to all of them at once:** `PauseAll`, `UnpauseAll` and `TerminateAllMembers`, with `ForEach` to visit
  each member in the order they joined.
- **How long the group lasts:** until its last member leaves (`TerminateWhenEmpty`, on by default), or until the group
  is ended, which ends its members (`TerminateMembersOnGroupTerminate`, on by default).

A group is found by a tag, so any code that has the tag can reach it. Only runtime processes join groups:
`RegisterWithGroup` takes an `AbstractRuntimeProcess`.

## Running it

1. Add a **Bootstrapper** to a scene. It brings PlayForge up, and needs nothing else.
2. Add **Process Group Examples** to any GameObject. For the squad, give it a leader and a few units, any transforms in
   the scene.
3. Enter Play mode and press the keys. The Console says what each one did.

| Key | What it shows |
| --- | --- |
| 1 | A group made before its first member, to configure it: at most three buffs, a fourth ending the oldest. Its events say who joins and who leaves, and it stays when its last buff ends. |
| 2 | A cap that refuses: at most two summons, and `RegisterWithGroup` returns false for a third. Each summon joins with a data packet of its own. |
| 3 | A group made by its first member, with no cap, which ends with its last. Its hazards share the group's data packet, so their pulses add up to one total. |
| 4 | `ForEach` over a squad, giving each member its place in a line as one joins or leaves. Pressed again, the squad is disbanded, its members with it. |
| P | `PauseAll` and `UnpauseAll` across every group: a buff's countdown stops, and a hazard's pulses wait. |
| L | What a group says of its members: `ForEach`, `GetOldest`, `GetNewest`, `TryGetFirst` and `IsFull`. |
| D | `DetachMember`: the oldest summon leaves its group and goes on alone. |
| C | `TerminateAllMembers` on the buffs. The group stays for the next buff. |
| X | Ending the summons' group, which ends its members. A detached summon isn't a member, so it stays. |

## In code

```csharp
// A group configured before its first member, found later by its tag
var group = new GroupProcess("Buffs", 3, EGroupOverflowPolicy.EvictOldest) { TerminateWhenEmpty = false };
ProcessControl.RegisterGroup(buffsTag, group, out _);

// A member: any runtime process, with the group's data packet unless it brings its own
ProcessControl.RegisterWithGroup(buffsTag, new TimedBuff("Haste", 6f), out var relay);

// What's done to all of them, from anywhere that has the tag
if (ProcessControl.TryGetGroup(buffsTag, out var buffs)) buffs.PauseAll();
```

A process registered with a group that doesn't exist yet makes it, with no cap. Make the group first when its settings
matter.

| File | What it holds |
| --- | --- |
| `ProcessGroupExamples.cs` | The component: an example for each key, each explained where it's written. |
| `GroupMembers.cs` | The members: a buff, a summon, a hazard and a squad member, each a `LazyRuntimeProcess` with the Synchronous lifecycle. |
