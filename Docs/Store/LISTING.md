# The Asset Store listing: a draft

A draft of the listing's text, for the user's word before it goes into the publisher portal. Each claim comes from
the package's own `CHANGELOG.md`, `PUBLISHING.md` or the store shots' captions in `README.md`. The price, the category
and the publisher's name are the user's to set, and the portal's own length limits come first.

## Name

PlayForge: Gameplay Ability System

## Summary

A data-driven gameplay ability system for Unity 6: abilities, effects, attributes, tags and items as assets, with an
editor, a live debugger and a lab to try them in.

## Description

PlayForge is a gameplay framework for Unity 6, modelled on Unreal's Gameplay Ability System. Abilities, effects,
attributes, tags and items are assets you build in the inspector. PlayForge runs them, and its editor tools show you
what they did.

**Build gameplay as data**

- Abilities as staged tasks, with targeting, costs, cooldowns, triggers, a buffer, and cancelling and blocking by tag.
- Effects: instant, lasting or periodic, with stacking, ticks, requirements, immunities, purges and cues, with no code.
- Attributes with bounds and overflow, and workers that change or answer each change.
- Damage pipelines and level scaling as data: every number a constant, a value by level or a scaler, explained part
  by part.
- Hierarchical, weighted tags, and items with stacks, levels and what they grant.

**See what happened**

- **The Forge**: every ability, effect and attribute of a project is an asset, browsed, edited and validated in one
  window.
- **The Debugger**: every entity's attributes, effects, abilities and tags, live, and a timeline of what each ability
  and effect did, with each effect's journal and each hit's steps.
- **Problems**: validation catches mistakes before you press Play, each with its quick fix.
- **The Creator Lab**: try your heroes and abilities on units, dummies and bots, with stops, stepping, watches and
  logs.

**Samples**

- **Demo**: a complete arena, with 5 heroes, 14 items, an AI opponent and a training dummy.
- **Survivors**: a horde shooter with hundreds of enemies at once, abilities that cast themselves, passives and a dash.
- **Battle**, a battle that plays itself; **Additional Content**, recipes for common rules, hit feedback and a sparring
  scene; and **Task Sequence** and **Process Group** examples.

**Built to last**

- ProcessControl runs every process from Unity's player loop, and abilities run as task sequences on UniTask.
- Saving and loading an entity's state, a combat log for each entity, and numbers and item stats to and from
  spreadsheets.
- A test kit for your game's own tests.
- Full C# source.

**Documentation**: guides and an API reference at https://gabedvdsn.github.io/PlayForge-Docs/. Each (?) button in
PlayForge's inspectors and The Forge opens its page.

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
