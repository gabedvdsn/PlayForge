# PlayForge Core's Asset Store listing: a draft

A draft of the core edition's listing (EP-84), for the user's word before it goes into the publisher portal, as
`LISTING.md` is the full package's. PlayForge Core is the full package's own files with the ability system left out:
Proving Grounds' `Tests/Editor/CoreEdition.txt` names them, and its `CoreEditionTests` compile them with nothing else.
Each claim here comes from those files, the two guides it ships and its samples' descriptions. The price, the category
and the publisher's name are the user's to set, and the portal's own length limits come first.

The Asset Store's rules shape it. A lite edition is allowed where what it includes works as the full one's does (rule
1.4.c), which it does, being the same files. Packages that are largely the same are refused (rule 2.1.f), so both
listings say plainly what each has, and each carries the comparison below (rule 3.1.c).

## Name

PlayForge Core: Process Control and Task Sequences

## Summary

PlayForge's process scheduler and task sequences for Unity 6, without its ability system: processes run from the
player loop, and staged workflows built in code, with a live debugger.

## Description

PlayForge Core is the part of PlayForge that everything else runs on. ProcessControl schedules every process from
Unity's player loop, and task sequences run staged workflows, built in code on UniTask. It has no ability system: no
abilities, effects, attributes or items. It is the same code as PlayForge's, so PlayForge imports over it, and code
written for PlayForge Core runs unchanged.

**Processes**

- MonoBehaviour and plain C# processes, registered with one call and stepped in Update, LateUpdate or FixedUpdate,
  in the order their priorities set.
- Four lifecycles: self-terminating, run then wait, requires control, and synchronous.
- Pause, end and list processes, and hear each one register, change state and go.
- Each owner's processes, listed and ended together.
- Process groups: a cap and what happens to one more, a data packet the members share, and every member paused or
  ended at once.
- A fixed tick rate, so a run comes out the same at any frame rate.

**Task sequences**

- Staged workflows from a builder: tasks, waits, conditions, timers, repeats, chains, input waits, time limits and
  what happens when a task throws.
- Two task libraries: `SequenceTaskLibrary`, awaited tasks for moving and turning, sprites, sound, animation,
  physics, UI and timing, and `SyncSequenceTaskLibrary`, tasks stepped a frame at a time, for sequences that run
  synchronously.
- Clocks: the game's, an unscaled one for menus, or a scaled one, for a whole sequence or any packet.

**See what happens**

- The Debugger's Processes view: every process, what it's doing and how long its steps take, while the game plays.
- The Profiler's PlayForge Processes module: ProcessControl's and the sequences' counters, and each frame's costliest
  processes.
- Logs by level, set in Preferences.

**Samples**

- **Task Sequence Examples**: each feature a step at a time, mechanics built from them, every task of both
  libraries, synced sequences as scene components, and a component that runs any example.
- **Process Group Examples**: each use of a group, from a key in a scene.

Full C# source.

**Documentation**: the Processes and Task Sequences guides, in the package and at
https://gabedvdsn.github.io/PlayForge-Docs/.

## PlayForge Core and PlayForge

Both listings carry this table.

| | PlayForge Core | PlayForge |
| --- | --- | --- |
| ProcessControl: processes, lifecycles, timing, priorities, groups, fixed ticks | Yes | Yes |
| Task sequences, both task libraries and clocks | Yes | Yes |
| Abilities, effects, attributes and items, as assets | No | Yes |
| Tags as assets, weighted and by hierarchy | No: tags in code, for groups | Yes |
| The Debugger | Processes | Entities, Processes, Tags and Timeline |
| The Profiler's modules | Processes | Processes and GAS |
| The Forge, Problems and the Creator Lab | No | Yes |
| Saving, the combat log and spreadsheets | No | Yes |
| Test kit and golden tests | No | Yes |
| Samples | Task Sequence Examples, Process Group Examples | These, with Demo, Survivors, Battle and Additional Content |

## Technical details

- Unity 6000.0 or later.
- Render pipelines: render-pipeline-agnostic.
- Includes UniTask 2.5.11 (MIT, Cysharp) in `PlayForge/ThirdParty/UniTask`. Delete that folder where your project
  already has UniTask from Package Manager.
- Installs uGUI (`com.unity.ugui`) and Profiling Core (`com.unity.profiling.core`) with the package.
- The Input System is optional: input waits read it where the project has it, and the Input Manager otherwise.
- Works with domain reloading off.
- The package goes in `Assets/Plugins/PlayForge`. A **Process Bootstrapper** in a scene brings ProcessControl up.
- Moving up to PlayForge: import it over PlayForge Core. Every file keeps its GUID and its assembly, so nothing in
  your project needs changing.

## Keywords

task sequence, process control, coroutine, async, UniTask, scheduler, workflow, state machine, tween, timer, update
loop, framework
