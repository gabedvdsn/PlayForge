# The Asset Store listing: a draft

A draft of the listing's text, for the user's word before it goes into the publisher portal. Each claim comes from
the package's own `CHANGELOG.md`, `PUBLISHING.md`, its guides or the store shots' captions in `README.md`. What sets
it apart names no other asset, as the store's guidelines keep other products' names out of a listing. The price, the
category and the publisher's name are the user's to set, and the portal's own length limits come first.

## Name

PlayForge: Gameplay Ability System

## Summary

A data-driven gameplay ability system for Unity 6: abilities, effects, attributes, tags and items as assets, with an
editor, a live debugger and a lab to try them in.

## Description

PlayForge is a gameplay framework for Unity 6, modelled on Unreal's Gameplay Ability System. Abilities, effects,
attributes, tags and items are assets you build in the inspector. PlayForge runs them, and its editor tools show you
what they did.

**What sets PlayForge apart**

- **ProcessControl, one scheduler for everything that runs.** Entities, projectiles, auras and sequences are all
  processes, stepped from Unity's player loop in Update, FixedUpdate or LateUpdate. Each takes a lifecycle
  (SelfTerminating, RunThenWait, RequiresControl or Synchronous) and hands back a relay to pause, resume, terminate or
  await it. Processes join capped groups that share a data packet, and a fixed tick rate makes a run come out the same
  at any frame rate.
- **Task sequences, the engine under every ability.** An ability runs as a staged task sequence on UniTask: stages of
  concurrent tasks with completion policies, injections that interrupt, skip or jump a run, timeouts, repeats, chains,
  input waits, and per-run clocks, scaled, unscaled or your own. A synced runner steps the same sequence frame by
  frame with no async, and two task libraries ship movement, rotation, scale and tween tasks. Projectiles, channels,
  cutscenes and boss phases use the same API.
- **Save/Load and rollback, built in.** ForgeSave captures what lasts on an entity (attribute commits, levels,
  abilities, items, tags, and effects with their exact timers) into a plain, versioned ForgeSnapshot, and restores it
  onto the same entity or a fresh one. With a fixed tick and a seeded random source, a rollback replays to the last
  digit.
- **Continuous asset validation.** The validator checks each asset again as it's edited, imported, moved or deleted,
  with no polling, against built-in rules that include cross-asset lint: stacks read twice, filters that never match,
  events nothing sends. The Problems view lists findings by asset or by rule, with one-click quick fixes, and takes
  rules of your own.
- **An advanced editor suite.** The Forge browses, creates and refactors every asset in one window: presets,
  templates that push to their children and mark overrides as prefab instances do, and a delete that first names the
  assets, prefabs and scenes still referring to its target. Every number field picks a kind (constant, table, by
  level, attribute-scaled, set by caller, group and more), states what it reads, and takes values from a Scaler
  asset, a quick fill or a spreadsheet. Numbers and item stats go to and from spreadsheets.
- **Runtime introspection.** The Debugger shows every entity's attributes, effects, abilities and tags live, every
  process, and a Timeline of each ability and effect, down to an effect journal with each number's trace and each
  hit's breakdown. Two Profiler modules chart PlayForge's counters and each frame's costliest processes. The Creator
  Lab pits your heroes against units, dummies and bots, with stops, stepping, watches and logs.
- **Production-ready.** Full C# source, entity hubs as MonoBehaviours or plain C# objects, domain reloading off, a
  render-pipeline-agnostic runtime, a combat log for each entity, a test kit and golden tests, and online guides and an
  API reference behind every inspector's (?) button.

**Build gameplay as data**

- Abilities as staged tasks, with targeting, costs, cooldowns, triggers, a buffer, and cancelling and blocking by tag.
- Effects: instant, lasting or periodic, with stacking, ticks, requirements, immunities, purges and cues, with no code.
- Attributes with bounds and overflow, and workers that change or answer each change.
- Damage pipelines and level scaling as data: every number a constant, a value by level or a scaler, explained part
  by part.
- Hierarchical, weighted tags, and items with stacks, levels and what they grant.

**Samples**

- **Demo**: a complete arena, with 5 heroes, 14 items, an AI opponent and a training dummy.
- **Survivors**: a horde shooter with hundreds of enemies at once, abilities that cast themselves, passives and a dash.
- **Battle**, a battle that plays itself; **Additional Content**, recipes for common rules, hit feedback and a sparring
  scene; and **Task Sequence** and **Process Group** examples.

**Documentation**: guides and an API reference at https://gabedvdsn.github.io/PlayForge-Docs/. Each (?) button in
PlayForge's inspectors and The Forge opens its page.

## PlayForge and PlayForge Process Sequencer

PlayForge Process Sequencer, sold on its own, is ProcessControl and task sequences without the ability system
(`LISTING_SEQUENCER.md`). Both listings carry this table (rule 3.1.c).

| | PlayForge | PlayForge Process Sequencer |
| --- | --- | --- |
| ProcessControl: processes, lifecycles, timing, priorities, groups, fixed ticks | Yes | Yes |
| Task sequences, both task libraries and clocks | Yes | Yes |
| Abilities, effects, attributes and items, as assets | Yes | No |
| Tags as assets, weighted and by hierarchy | Yes | No: tags in code, for groups |
| The Debugger | Entities, Processes, Tags and Timeline | Processes |
| The Profiler's modules | Processes and GAS | Processes |
| The Forge, Problems and the Creator Lab | Yes | No |
| Saving, the combat log and spreadsheets | Yes | No |
| Test kit and golden tests | Yes | No |
| Samples | Demo, Survivors, Battle, Additional Content, Task Sequence Examples, Process Group Examples | Task Sequence Examples, Process Group Examples |

## Technical details

- Unity 6000.0 or later.
- Render pipelines: the runtime is render-pipeline-agnostic. The samples render in the Built-in Render Pipeline and
  URP.
- Includes UniTask 2.5.11 (MIT, Cysharp) in `PlayForge/ThirdParty/UniTask`. Delete that folder where your project
  already has UniTask from Package Manager.
- Installs uGUI (`com.unity.ugui`) and Profiling Core (`com.unity.profiling.core`) with the package.
- The Input System is optional: the samples read it where the project has it, and the Input Manager otherwise.
- Works with domain reloading off.
- Additional Content's floating numbers and status bars draw with TextMeshPro: import its Essential Resources when
  Unity asks.
- The package goes in `Assets/Plugins/PlayForge`. The Forge Projects you make go in `Assets/PlayForge`, a folder
  Project Settings can change.

## Keywords

gameplay ability system, GAS, abilities, gameplay effects, attributes, buffs, debuffs, status effects, RPG, MOBA,
action, combat, data-driven, framework, editor tools
