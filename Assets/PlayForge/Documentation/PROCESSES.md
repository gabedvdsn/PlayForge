# Processes

A **process** is anything that runs over time under **ProcessControl**: an entity's GAS, a projectile, an aura's pulse,
a task sequence ([TASK_SEQUENCES.md](TASK_SEQUENCES.md)), a regeneration. ProcessControl steps each from Unity's
player loop, Update, FixedUpdate or LateUpdate as it asks, moves it between its states, and ends it. A Bootstrapper in
the scene brings ProcessControl up.

## Lifecycles

A process's lifecycle says how it starts and what happens when its run is done:

| Lifecycle | Starts | When its run is done | Runs async | For |
| --- | --- | --- | --- | --- |
| **SelfTerminating** | Running | Terminated | Yes | One-shot work: a projectile, a hit, a spawn |
| **RunThenWait** | Running | Waiting | Yes | Work run again and again: an ability, a dialogue |
| **RequiresControl** | Waiting | Waiting | Yes | Work started from outside: an ability on a key, a gated event |
| **Synchronous** | Running | Waiting | No | Work every frame for good: a health bar, steering, a buff's tick |

An async process's `RunProcess` is awaited; a Synchronous one is stepped each frame at its timing instead, through
`WhenUpdate`, `WhenFixedUpdate` or `WhenLateUpdate`.

## Kinds

- **Mono processes** (`LazyMonoProcess`, and `AbstractMonoProcess` under it) are MonoBehaviours: prefabs or scene
  objects, with Unity's components. ProcessControl makes and destroys them.
- **Runtime processes** (`LazyRuntimeProcess`, and `AbstractRuntimeProcess` under it) are plain C# classes, with no
  GameObject, for logic alone.

A mono process's lifecycle and timing are fields in its inspector; a runtime process gives them to its base constructor.

## Registering one

```csharp
ProcessControl.Register(process).HandledBy(gas).With(data).Run(out ProcessRelay relay);

relay.Pause();       // Waiting at once, out of the steps
relay.Unpause();     // Running again
relay.Terminate();   // ended, and cleaned up
await relay.WhenTerminated();
```

The **relay** is the handle on a registered process: its state, how long it has lived, and what's done to it. A mono
process's relay reaches its children too: processes on child GameObjects pause and end with it unless told not to.

## Pausing

A pause holds a process in place, at once. A process that can hold its run says so by returning true from
`TryHandlePause`: its run stays alive, watches `relay.IsWaiting`, and carries on after `TryHandleResume`. Any other
process has its run cancelled, and a new run starts on `Unpause`.

ProcessControl set to `Waiting` or `ClosedWaiting` pauses everything running the same way, and leaving those states
sends back exactly what they stopped. A process paused on its own stays paused, and `Unpause` is refused until
ProcessControl is back. A `Terminate` asked for outranks a pause.

A task sequence holds its run: no stage starts or ends, its time limits stop counting, its conditions aren't checked,
and the task library's waits hold, given the token their task was handed.

## Groups

A **group** is a process that holds other processes, found by a tag. Each member still runs as a process of its own;
the group adds what they have in common:

- **A cap**, `MaxMembers`, and what happens to one member more: `EGroupOverflowPolicy.Reject` refuses it, so
  `RegisterWithGroup` returns false, and `EvictOldest` ends the oldest member to make room.
- **A data packet they share.** A member joins with the group's packet unless it asks for its own (`useSharedData:
  false`) or brings one, so what one member writes, the others read.
- **What's done to all of them at once**: `PauseAll`, `UnpauseAll`, `TerminateAllMembers`; `ForEach` visits each member
  in the order they joined, and `ForEach<T>` those of a kind; `GetOldest`, `GetNewest`, `TryGetFirst<T>`, `HasMember`,
  `MemberCount`, `IsFull`. `DetachMember` takes one out without ending it.
- **How long the group lasts**: until its last member leaves, while `TerminateWhenEmpty` is on, as it is by default; and
  ending the group ends its members, while `TerminateMembersOnGroupTerminate` is on, as it is by default. Its events,
  `OnMemberJoined`, `OnMemberLeft` and `OnGroupEmpty`, hear its members come and go.

```csharp
// Made first, to configure it; or made by its first member, with no cap
var buffs = new GroupProcess("Buffs", 3, EGroupOverflowPolicy.EvictOldest) { TerminateWhenEmpty = false };
ProcessControl.RegisterGroup(buffsTag, buffs, out _);
ProcessControl.RegisterWithGroup(buffsTag, new TimedBuff("Haste", 6f), out var relay);

if (ProcessControl.TryGetGroup(buffsTag, out var group)) group.PauseAll();
```

Only runtime processes join a group. The **Process Group Examples** sample runs each of these from a key in a scene.

## Logging

ProcessControl's own logs are its inspector's toggles. What processes say through `ForgeLog` is of the **Processes**
system, and a sequence's of **Sequences** ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#logging)).
