# The Creator Lab: the design

One lab for trying a project's own assets of every sort, the way a designer works: put units down, give them abilities,
effects and items, cast, watch the numbers move, change an asset, and see the change in play.

**Status:** decided 2026-11-11 (Decision 44): a scene, in the package, showing all six ways inside, with a driver for
the author's unit; Forge Projects are folders. Phase 1, FEAT-32, is built (LVL-1 to LVL-4). FEAT-34 (Forge Projects)
feeds it. Phase 2, the shell, is built (LAB-1 to LAB-8): `Assets/PlayForge/Documentation/CREATOR_LAB.md`.

## The problem

A project's assets can be edited in The Forge, and a playing game can be watched in the Debugger, but nothing lets an
author take their own abilities, effects and items and try them. Each way to try one today is someone else's:

- **The Demo** is a game, with its own five heroes, its own arena and its own panel. Its arena is close to what a lab
  needs: units on a plane, a camera from above, a training dummy that counts damage, an AI that fights back, and a panel
  that gives items and levels. But it plays the Demo's assets, and its code is the sample's.
- **The Battle sample** plays a battle of eight a side by itself, again with its own fighters.
- **Proving Grounds' labs** are code: a battle, a roguelite and the Dota effects, each built and played by tests.
- **The Debugger** shows a game's entities, effects, tags and a timeline of what abilities and effects did, but only of
  a game someone else has built.

So a designer's question, "what does this effect do to a target with 300 armor at level 4?", needs a scene of their
own and code to set it up.

## The idea

The lab is a scene PlayForge brings, opened from **Window → PlayForge → Creator Lab**. It plays the active Forge
Project's assets (FEAT-34), or every asset where there are no projects yet. It has three parts:

1. **The space:** an overhead 3D arena, where units stand, move and fight. Its camera pulls back, tilts and resets.
2. **The modes:** what the space holds. Test dummies, a battle of teams, or nothing; in any of them, a unit the
   author controls.
3. **The panels:** the assets on the left, the one selected in the middle, the mode's controls on the right, each
   collapsible, over the space, in UI Toolkit, as the Demo's HUD is.

And one thing across all three, the reason it's a lab and not a game: **it shows its working.** Any number can be asked
how it came out, any effect what it did and why, any attribute who changed it and how. It can stop as something
happens and go a frame at a time (Open decision 27).

## The space

- **Units** are a body and a GAS: a shape on the ground, a `GameplayAbilitySystem` on it, a bar over it, and a ring in
  its affiliation's color. They walk in straight lines on the plane, as the Demo's do. Movement, navigation and
  physics are a game's, not PlayForge's, so the lab gives units a body of its own; a project can give its own
  (Open decision 30).
- **The camera** looks down at an angle, follows nothing unless asked, and keeps its distance and its tilt as the
  author sets them: scroll to pull back, a drag with the middle button to pan, and the right panel's sliders for both.
  Reset puts it back.
- **Time** runs in the lab's own world (`ProcessWorld`, with `world.Step(deltaTime)`), so the lab pauses, steps a
  frame, and runs slower or faster, without touching Unity's time.

## The modes

### Test dummies

- The author adds as many dummies as they want, each from an entity (an Entity asset or an attribute set), and drags
  them about, or resets each to where it was put.
- Each takes an affiliation, so an ability that hits enemies only can be tried on the dummy that is one.
- A dummy shows what it takes: damage a second, the last blow by its type, and a short history (from the combat log,
  below). It doesn't die unless asked, and it mends after a few seconds alone, as the Demo's does.

### Battle

- The author makes teams, adds bots to each, and gives each bot an entity, abilities, items and a level.
- **Quick battle:** two teams of N, from what's selected or at random from the project.
- **Play, pause and reset:** reset puts every bot back where it started, at full.
- Bots fight with the Battle sample's AI, generalized: each casts its abilities as it can, at the enemy it picks,
  handing the target in the activation's data, which click targeting takes with no click (as the Demo's AI does).

### None

An empty space, where abilities still cast and effects still land: for an ability that summons, a projectile, or an
area with no one in it.

### The author's unit

In any mode, the author can put down a unit they control. It moves and casts by the game's own controls, through a
driver the project provides (Open decision 30), or by the lab's own, which are the Demo's: right-click to move,
Q, W, E and R to cast, 1 to 6 for items, left-click a target.

## What the author can do anywhere

- **Select anything:** a unit, or an asset in the library. A unit shows its attributes, tags, effects with their
  stacks and time, abilities with their cooldowns, and items; an asset shows its breakdown, level by level
  (below).
- **Apply an effect:** drag it from the library onto a unit. It lands from the lab, a neutral source, or from the
  selected unit when one is selected, so "the caster's Strength" means something.
- **Cast an ability:** drag it onto a unit or the ground, and the lab's caster, or the selected unit, casts it there.
  A targeting task gets that target in the activation's data; one that needs more waits for a click, and Esc cancels it.
- **Give and take:** abilities, items and levels, on any unit, from the middle dock.
- **Edit and see:** an asset changed in the inspector or The Forge as the lab plays is read on its next application,
  as every asset is (an effect derives its impacts on each `Generate`). An entity's attribute set takes a unit's reset.

## The panels

The UI is UI Toolkit in the Game view, over the space, so dragging from the library onto a unit is one gesture. Each
panel collapses to a tab at its edge.

- **Left: the library.** The Forge Project the lab plays (and a switch between projects), then its assets by type:
  abilities, effects, items, entities, attribute sets, scalers. A search box filters them; a type's color shows only in
  its icon (`Docs/EDITOR_STYLE.md`).
- **Middle: the selected.** Hidden until something is selected. For an asset, its summary and **its numbers level by
  level**, as "80/120/160/200", from FEAT-32's query: each impact's attribute, how it lands, its number's parts and its
  result at each level, and where a part can't be worked out without a target (a share of the target's Health), what
  it reads instead. For a unit, what it is and holds now. In the editor, a button opens the asset in The Forge.
- **Right: the mode.** Test dummies: add, reset, affiliations. Battle: teams, bots, quick battle, play, pause, reset.
  The author's unit: put down, pick its entity, take control. The camera's distance and tilt, and reset. The lab's time:
  pause, step, speed.
- **Bottom: the log.** Collapsed by default: the journals below, filtered to the selected unit.

## Showing its working

The user asked what to propose (Open decision 27). Each of these records only while the lab, or the Debugger, listens,
so a game pays nothing; and each is the editor's and a development build's alone.

1. **Explain a number.** Every number field, scaler and condition can trace a read: a tree of each part, what it read
   and what it gave. Monkey King's charged hit reads "−180 = −1 × (100, Caster's Attack Damage, + 80, as Caster's Jingu
   Charges 4 > 0)". It is FEAT-32's engine too, so the middle dock's numbers and the log's agree. A `NumberTrace`,
   beside `NumberText`, read through the same `NumberContext`.
2. **An effect's journal.** Each application, refusal (by requirement, immunity or chance), tick, impact and removal,
   with its numbers' traces, the workers that ran (which hook, on which side) and the actions they queued and ran. The
   GAS raises most of this already (`GameplayAbilitySystemCallbacks`: effect applied, ticked and removed, impact dealt and
   taken, actions queued, run and dropped); the workers' hooks and the refusals need events of their own.
3. **A combat log.** Each change to an attribute as it was made, each attribute worker that changed it and how (before
   and after, inline or queued), and what landed, by dealer and by effect: "Target −180 Health from Monkey King Attack
   (Monkey King): 180 as made, Mitigation −0, landed −180; Monkey King +54 Health, Lifesteal". Attribute workers' changes
   need an event.
4. **Stop on an event, and step.** Stop the lab's world as an effect lands or is refused, a worker runs, an attribute
   crosses a value, a tag comes or goes, or an ability is refused, on a unit or any; then step a frame, or a tick, at a
   time. The lab's own world makes this a pause of one world, not of Unity.
5. **Watches.** Pin a unit's attribute, a tag's count, an effect's stacks or time, or a number field read on it, and see
   them as lines over time, the last minute of the lab's time.
6. **Logs.** ForgeLog's levels by system (effects, abilities, workers, numbers, processes), filtered to the selected
   unit, in the bottom panel.

The Debugger, in the editor, shows the same journals for any game, not just the lab: its Timeline grows the effect
journal, and its Entities view the combat log.

## Where it builds on what's there

| Lab part | Built from |
|---|---|
| Units, their body, the camera, the dummy, the author's controls | The Demo's `DemoUnit`, `DemoCamera`, `DemoDummy`, `DemoPlayer` and `DemoInput`, moved into the lab and made general; the Demo then uses them |
| Bots | The Battle sample's AI, and the Demo's, which hands its target in data |
| Time, pause, step | `ProcessWorld` and `world.Step(deltaTime)` (worlds of their own) |
| Input for targeting | `ForgeInput` and `IForgeInput` |
| Level by level | `AbilityInspector.BuildEffectSeries` and `EvaluateAtLevel`, grown into FEAT-32's query |
| Lines for numbers and effects | `NumberText`, and FEAT-32's effect summary |
| Journals | `GameplayAbilitySystemCallbacks`, `FrameSummary`, the Debugger's `TimelineRecorder` |
| The library and projects | The Forge's asset lists, and FEAT-34's projects |

## Concerns

1. **Movement and controls are the game's.** PlayForge has no movement, and a project's controls may be anything. The
   lab's body and controls are simple and its own; a project that wants its own gives a driver (Open decision 30).
2. **A build.** The lab is an editor tool first. In a build it can list only what a Forge Project names, and its
   journals are a development build's. Whether it ships in builds at all is part of Open decision 29.
3. **Cost.** Recording traces and journals allocates. Nothing records unless a listener asks, and the zero-allocation
   probes run with no listener, as now.
4. **Assets changed as it plays.** Effects read their asset as they're applied, so a change shows on the next one.
   What an entity was built from (its attribute set, its starting abilities) shows on its reset; the middle dock says so.
5. **The Demo.** Its arena code moves into the lab, so the Demo shrinks to its assets, its scene and its panel, and the
   two can't drift. The Demo's tests cover the move.
6. **Size.** This is the largest piece asked for. The phases below each give the author something to use.

## Phasing

Each phase gets tracker rows of its own, red tests first, as FEAT-29's did.

1. **The inside, without the lab:** FEAT-32's query and `NumberTrace`, with The Forge showing an effect level by level
   and a Scaler asset's panel for trying it. Useful at once, and the lab's middle dock is built on it.
2. **The lab's shell:** the scene, the space, the camera, the panels, the library (every asset, until FEAT-34), test
   dummies, and dragging effects and abilities onto units. The Demo's arena code moves in.
3. **Battle:** teams, bots, quick battle, play, pause and reset, and the lab's own world and time.
4. **The author's unit:** the lab's controls, and the driver a project can give.
5. **Showing its working:** the effect journal, the combat log, stopping on an event and stepping, watches and logs,
   in the lab and in the Debugger.
6. **Forge Projects:** the switch in the library, once FEAT-34 is built.

## Decided (Decision 44)

Each as recommended below.


- **Open decision 27** (already open): which of the six ways of showing its working, in what order; and whether the
  lab is a scene in the package or an editor window. **Recommended:** all six, in the order above; a scene played in the
  Game view, with its panels in UI Toolkit over it, so dragging onto a unit is one gesture, and the Debugger showing
  the same journals for any game.
- **Open decision 29** (new): where it lives. **(a)** In the package, as a tool: a lab module of its own, an assembly a
  build can leave out, opened from the Window menu. **(b)** A sample the author imports. **Recommended:** (a); it is a
  tool every project uses, not an example to copy.
- **Open decision 30** (new): what "the user's schemas" for the author's unit are. PlayForge has no movement or
  controls of its own. **Recommended:** a driver interface the project implements, `ILabUnitDriver` (move to a point,
  attack a unit, cast an ability at a target, stop), with the lab's own, the Demo's controls, as the default; and
  `ForgeInput` for targeting, as now. **To answer:** that, or something the project has already that the lab should read.
- **Open decision 26** (already open): Forge Projects as folders or lists, which decides how the library lists assets.
