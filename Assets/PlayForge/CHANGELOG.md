# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - Unreleased

The first release.

### Added
- **The Gameplay Ability System**: one hub for each entity, a MonoBehaviour (`GameplayAbilitySystem`) or a plain object
  (`RuntimeGameplayAbilitySystem`), built from an Entity asset, owning its attributes, effects, abilities, tags and
  items.
- **Attributes and attribute sets**: a base and a current value each, bounds and overflow, retention groups, and
  attribute workers that change or answer each change.
- **Gameplay effects**: instant, lasting or periodic, with stacking, requirements, immunities, a chance to apply,
  contained effects, purges, cues, components and workers, each impact run through its value pipeline.
- **Abilities**: staged task workflows with targeting, which gives way as another ability is cast, costs and
  cooldowns, triggers, a buffer, and cancelling and blocking by tag; tasks to apply effects, wait, send events
  and move.
- **Tags**, hierarchical and weighted, with tag workers; and **items**, with stacks, levels and what they grant.
- **Cues**: what effects, abilities, entities and items show and sound, played through `ForgeCues` at their
  moments. A Cue Tag plays what the project's Cue Library routes its tag to, or an entity's Cue Receiver its own
  way, and prefab cues pool.
- **Number fields**: every authored number a constant, a value by level or a scaler, read against its caster and its
  target and explained part by part.
- **Saving and loading** an entity's state, a **combat log** for each entity, and numbers and item stats to and from
  **spreadsheets**.
- **ProcessControl**, which runs every process from Unity's player loop, in worlds of their own.
- **The Forge**, an editor for every asset of a Forge Project; **the Debugger**, for a game's live entities, with each
  effect's journal and each hit's steps; and **the Creator Lab**, a scene to try assets on units, test dummies and bots,
  with stops, stepping, watches and logs.
- **Samples**: Battle, Demo, Additional Content, Task Sequence Examples and Process Group Examples; and a **test kit** for a game's own tests.
- **Online documentation** at [gabedvdsn.github.io/PlayForge-Docs](https://gabedvdsn.github.io/PlayForge-Docs/): the guides,
  each section a page of its own, and an API reference with a page for every public type, linked both
  ways. Each help (?) button in PlayForge's inspectors opens its section. The same guides ship in
  `Documentation`.

Requires Unity 6000.0 or later, and UniTask 2.5.11 (MIT, Cysharp), which the Asset Store package includes; see
`Third-Party Notices.txt`.
