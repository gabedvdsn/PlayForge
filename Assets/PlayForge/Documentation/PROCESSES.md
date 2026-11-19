# Processes

A **process** is anything that runs over time under **ProcessControl**. An entity's GAS, a projectile, an aura's
pulse, a task sequence ([TASK_SEQUENCES.md](TASK_SEQUENCES.md)) and a regeneration are all processes.

ProcessControl does three things with each process:

- It steps the process from Unity's player loop, in Update, FixedUpdate or LateUpdate, as the process asks.
- It moves the process between its states.
- It ends the process.

A Bootstrapper in the scene brings ProcessControl up.

## Lifecycles

A process's lifecycle says how it starts and what happens when its run is done.

| Lifecycle | Starts | When its run is done | Runs async | For |
| --- | --- | --- | --- | --- |
| **SelfTerminating** | Running | Terminated | Yes | One-shot work: a projectile, a hit, a spawn |
| **RunThenWait** | Running | Waiting | Yes | Work run again and again: an ability, a dialogue |
| **RequiresControl** | Waiting | Waiting | Yes | Work started from outside: an ability on a key, a gated event |
| **Synchronous** | Running | Waiting | No | Work every frame for good: a health bar, steering, a buff's tick |

An async process's `RunProcess` is awaited. A Synchronous process isn't: ProcessControl steps it each frame at its
timing instead, through `WhenUpdate`, `WhenFixedUpdate` or `WhenLateUpdate`.

## Kinds

A process is one of two kinds: a mono process or a runtime process.

| Kind | Built on | What it is | Its lifecycle and timing |
| --- | --- | --- | --- |
| **Mono processes** | `LazyMonoProcess`, and `AbstractMonoProcess` under it | MonoBehaviours: prefabs or scene objects, with Unity's components. ProcessControl makes and destroys them. | Fields in its inspector |
| **Runtime processes** | `LazyRuntimeProcess`, and `AbstractRuntimeProcess` under it | Plain C# classes, with no GameObject, for logic alone. | Given to its base constructor |

## Registering one

You register a process with ProcessControl, which gives you a relay to control it with.

```csharp
ProcessControl.Register(process).HandledBy(gas).With(data).Run(out ProcessRelay relay);

relay.Pause();       // Waiting at once, out of the steps
relay.Unpause();     // Running again
relay.Terminate();   // ended, and cleaned up
await relay.WhenTerminated();
```

The **relay** is the handle on a registered process. It holds the process's state and how long it has lived, and it's
how you act on the process.

A mono process's relay reaches its children too. Processes on child GameObjects pause and end with it, unless told not
to.

### Instantiators

A mono process's object comes from an instantiator, an `AbstractMonoProcessInstantiator`. An instantiator works as a
pool does: it hands out an idle object, and takes it back as the process ends.

- A registration can give an instantiator of its own with `.InstantiatedBy(pool)`. It goes ahead of the handler's
  instantiator and the object's own.
- With no instantiator, the object is cloned, and destroyed as its process ends.

An object whose process has ended is no longer `IsInitialized`, so a pool's object can run again.

## Pausing

A pause holds a process in place, at once. What happens to the process's run depends on whether the process can hold
it:

| Process | On a pause | On `Unpause` |
| --- | --- | --- |
| One that can hold its run, and says so by returning true from `TryHandlePause` | Its run stays alive, and watches `relay.IsWaiting`. | Its run carries on after `TryHandleResume`. |
| Any other process | Its run is cancelled. | A new run starts. |

ProcessControl can pause every process at once. Setting it to `Waiting` or `ClosedWaiting` pauses everything running,
in the same way. Leaving those states sends back exactly what they stopped.

- A process paused on its own stays paused.
- `Unpause` is refused until ProcessControl is back.
- A `Terminate` asked for outranks a pause.

A paused task sequence holds its run:

- No stage starts or ends.
- Its time limits stop counting.
- Its conditions aren't checked.
- The task library's waits hold, given the token their task was handed.

## Groups

A **group** is a process that holds other processes, found by a tag. Each member still runs as a process of its own.
The group adds what the members have in common:

- **A cap** on its members.
- **A data packet they share.**
- **What's done to all of them at once**.
- **How long the group lasts**, and events as its members come and go.

Only runtime processes join a group. The **Process Group Examples** sample runs each of these from a key in a scene.

### Making and joining a group

A group is made first, to configure it, or else made by its first member, with no cap.

```csharp
// Made first, to configure it; or made by its first member, with no cap
var buffs = new GroupProcess("Buffs", 3, EGroupOverflowPolicy.EvictOldest) { TerminateWhenEmpty = false };
ProcessControl.RegisterGroup(buffsTag, buffs, out _);
ProcessControl.RegisterWithGroup(buffsTag, new TimedBuff("Haste", 6f), out var relay);

if (ProcessControl.TryGetGroup(buffsTag, out var group)) group.PauseAll();
```

A member joins with the group's data packet, so what one member writes, the others read. A member uses a packet of its
own instead when it asks for its own (`useSharedData: false`) or brings one.

### Capping a group

A group's cap, `MaxMembers`, limits how many members it holds. Its overflow policy says what happens to one member more:

| Overflow policy | What it does |
| --- | --- |
| `EGroupOverflowPolicy.Reject` | Refuses the new member, so `RegisterWithGroup` returns false. |
| `EvictOldest` | Ends the oldest member to make room. |

### Acting on every member

A group acts on all its members at once, and finds them for you.

| Member | What it does |
| --- | --- |
| `PauseAll` | Pauses every member. |
| `UnpauseAll` | Unpauses every member. |
| `TerminateAllMembers` | Ends every member. |
| `ForEach` | Visits each member, in the order they joined. |
| `ForEach<T>` | Visits the members of a kind. |
| `GetOldest`, `GetNewest`, `TryGetFirst<T>`, `HasMember`, `MemberCount`, `IsFull` | Find, check and count its members. |
| `DetachMember` | Takes one member out without ending it. |

### Group lifetime and events

A group lasts until its last member leaves, and ending it ends its members. Two properties set this, and both are on by
default:

| Property | While it's on |
| --- | --- |
| `TerminateWhenEmpty` | The group lasts until its last member leaves. |
| `TerminateMembersOnGroupTerminate` | Ending the group ends its members. |

Its events, `OnMemberJoined`, `OnMemberLeft` and `OnGroupEmpty`, hear its members come and go.

## Logging

What processes log is set in two places.

- ProcessControl's own logs are its inspector's toggles.
- What processes say through `ForgeLog` is of the **Processes** system, and what a sequence says is of **Sequences**
  ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#logging)).
