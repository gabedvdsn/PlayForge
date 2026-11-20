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

- **ProcessControl: one scheduler for everything.** Entities, projectiles and sequences run on Unity's player loop with
  explicit lifecycles and fixed ticks. Timing stays predictable, and every process can be inspected, paused or ended.
- **Task sequences: abilities as workflows.** Staged async tasks on UniTask, with interrupts, timeouts and chains, keep
  complex abilities readable. The same API drives projectiles, cutscenes and boss phases.
- **Save/Load and rollback, built in.** Snapshot an entity and restore it exactly, effect timers included. Save games,
  checkpoints and rollback need no hand-written state capture.
- **Validation as you edit.** Every asset is checked the moment it changes, with quick fixes. Broken data shows up in
  the editor, not in a playtest.
- **The Forge: every asset in one window.** Build, template and refactor in place, with numbers that show what they
  read at every level. Designers tune balance without code.
- **See why, not just what.** A live Debugger and a hit-by-hit Timeline trace a wrong value to its cause, and Profiler
  modules show what each frame costs.
- **Production-ready.** Full C# source, MonoBehaviour or pure C# entities, domain reloading off, a test kit and complete
  documentation.

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
