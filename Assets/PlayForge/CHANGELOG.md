# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html). The repository's `Docs/REFACTOR_PLAN.md`
tracks each fix below by its ID.

## [Unreleased]

### Added
- The Debugger's Timeline view. It records, as the game plays, each run of an ability, stage by stage, each refused
  activation, and each effect's time on the shelf, and draws them over the last seconds, a track for each entity's
  ability or effect. A track selected shows an ability's stages and their tasks, with when the selected run reached
  each, beside its runs, or an effect's applications (FEAT-12).
- A world of processes of its own. `ProcessWorld` holds what ProcessControl's bookkeeping held, and ProcessControl
  wraps one (`ProcessControl.World`) and steps it from Unity's loop, with its API as it was. `new ProcessWorld()` makes
  one with no scene, `ProcessControl.Register(process).In(world)` registers into it, and `world.Step()` steps it, the
  headless split's second step (FEAT-10).
- A world of its own keeps its own time. `world.Step(deltaTime)` steps it by a time its owner gives: its clock reads
  that time, and its own tick goes on, so its sequences' waits go on by its steps, and a loop of steps runs it
  through its time within one frame. `ForgeTickSource` is a tick; `ForgeTick.Scene` is the scene's, and
  `ForgeTick.Current` the one in force (FEAT-10).
- Abilities in a world of their own. An ability's processes register in their caster's world, which
  `ProcessWorld.Of(process)` finds, so a battle of runtime entities in a world of its own plays out as its owner
  steps it (FEAT-10).
- Waits that ask once a tick. `ForgeTick.WaitWhile` and `WaitUntil` ask once a tick where the tick in force ticks,
  and are UniTask's own waits where it doesn't. A sequence's conditional waits, the builder's Delay Until, Wait Until
  and Wait While among them, go through them, so a world of its own sees what they wait for change between its
  steps (FEAT-10).
- Worlds side by side, and with no scene. Several worlds of their own run at once, each with its own processes and
  time, and one runs outside play mode with no scene at all, as a balance tool in the editor might step it
  (FEAT-10).
- Targeting input. The click-targeting tasks read the game's input, `ForgeInput.Current`, in place of the Input
  Manager's mouse and `Camera.main`. By default it's the Input System's pointer, a mouse, a pen or a touch, where
  that package is in and on, or else the Input Manager's mouse, each aimed through `Camera.main` or a camera you
  give it. Put an `IForgeInput` of your own in with `ForgeInput.Use`, for a gamepad's reticle or a controller's ray
  (FEAT-11).
- Gameplay cues. An effect's new Cues section lists what it shows and sounds, as its workers are listed: a camera
  shake through the shaker the game gives `ForgeCues.Shaker`, a sound, or a prefab, as a burst or for as long as
  the effect lasts. Derive from `AbstractCueHandler` for a cue of your own. A save's load ends and begins lasting
  cues again, and plays no burst (FEAT-1).
- Ability triggers. An ability's Triggers set it off on its own: a gameplay event sent to its entity with
  `SendGameplayEvent(tag, data)`, or a tag its entity gains, under the trigger's tag or one under it. The ability
  inspector edits them, and the callbacks' `OnGameplayEvent` hears each event (FEAT-4).
- SetByCaller magnitudes. `SetByCaller(tag, magnitude)` on an effect's spec, or on an ability activation's data,
  sets an amount the code knows as it applies the effect, and the new Set By Caller scaler reads it. Each effect an
  ability applies takes its data's magnitudes as it's made, its cost, cooldown and contained effects among them, and
  a save keeps them (FEAT-2).
- Run Sequence runs a sequence made in code: its `Definition`, which `TaskSequenceBuilder` makes, a fresh run each
  activation, so an ability built as the game runs can run one (FEAT-21).
- Copying an entity. `ForgeSave.Copy(from, onto)` copies what lasts on one entity onto another, as a mirror
  clone takes its original's state, with no catalog and no IDs. A save now keeps the entity's own level as its
  own, so it loads onto an entity of another kind as that one's level (FEAT-20).
- Activation by asset or tag, with a reason. `TryActivateAbility(ability)` activates an ability by its asset, and
  `TryActivateAbility(tag)` the first whose asset or context tag is the tag or under it. Each form can give an
  `ActivationRefusal` that says why it was refused, and the ability system's `OnAbilityRefused` tells each refusal
  (FEAT-17).
- Profiler markers. PlayForge's work shows in Unity's Profiler under its own names: a process's step, a GAS's step
  and its end of frame, an effect's application and an ability's activation (`ForgeProfiler`, FEAT-18).
- More Profiler markers: the step loop at each timing, a tag compile, an attribute's recompute and a stage's start;
  and a process's step under a marker of its type's too, with the process's ID as its metadata (FEAT-18).
- PlayForge depends on `com.unity.profiling.core` 1.0.2, which the editor ships, for its Profiler markers with
  metadata and its counters (FEAT-18).
- Profiler counters. `ForgeCounters`, in the Profiler's PlayForge category, count ProcessControl's processes, a
  frame's registrations, terminations, steps, state changes and step time, the entities by hub, the effects active,
  a frame's effects applied and removed, impacts, activations, refusals, tag compiles and recomputes, the sequences
  running and a frame's stages started. They count in the editor and development builds (FEAT-18).
- Two Profiler modules. PlayForge Processes charts ProcessControl's and TaskSequence's counters, and its details list
  the selected frame's costliest processes, which PlayForge puts into the capture while the Profiler records
  (`ForgeFrameCosts`). PlayForge GAS charts the GAS's counters (FEAT-18).
- An Effect Stacks scaler, which reads the stack count of the effect's own container, 1 for one that doesn't stack.
  With Multiply With Scaler, a sunder of −5 armor takes 5 a stack, and a Continuous change follows the count as it
  changes (FEAT-19).
- A warning in the Ability inspector, under Cooldown, while the ability has a cost or a cooldown and no stage applies
  usage effects, so neither is ever applied. The Problems view lists it too (ED-41).
- Save and load. `ForgeSave.Capture` takes an entity at rest into a `ForgeSnapshot`, plain serializable types a game
  writes where it likes: each attribute's permanent commits, its levels, the abilities it knows, its items with their
  stacks, worn or not, and its effects with their time left, with its loose tags and time scale. `ForgeSave.Restore`
  puts it back on the same entity or a fresh one of its kind. Running abilities end, nothing is told as it goes, and
  `OnRestored` fires once. A new Asset Catalog names the assets a save refers to; Add Every PlayForge Asset, in its
  menu, names each by its GUID. See `Documentation/SAVING.md` (FEAT-9).
- Item stacking. A give fills an item's stack first, up to its Max Stack Size, which the Item inspector now draws;
  a full stack opens another only with Allow Duplicate Items. `GiveItem(item, count)` takes what fits and says how
  many, and `RemoveItem(item, count)` takes stacks off. A stack applies its effects once, and the new Item Stacks
  scaler reads its count (FEAT-14, GAS-51).
- Stages that branch. An ability's stage has a Condition: a tag in the activation's data, on the caster or on the
  target, and whether it must be there or missing; a stage whose condition fails is skipped. Apply Effects' Record
  Landed As tags the data as an effect lands, so a move can recover one way on a hit and another on a miss (TS-7).
- The input buffer. Under the Queue policy, a request that can't start yet, as while another ability holds the claim
  or a running stage blocks it, waits for up to its ability's Buffer Window (default 0.3 s, in the caster's time),
  then lets go. Each ability holds one slot, which a new press refreshes; the highest Buffer Priority fires first,
  then the newest. A request can name a window of its own (FEAT-6).
- Cancel and block abilities by tag. An ability's Cancelled By names the abilities that cancel it as they activate,
  and its Blocks those that can't activate while it runs; each stage can name its own, and one that names none takes
  the ability's. A critical stage can't be cancelled (FEAT-5).
- A While active row in an ability's tag requirements: the caster's tags for as long as the ability runs. When they
  fail, the ability is interrupted, so a stun ends a channel (FEAT-5).
- Abilities granted by effects. An effect's Granted Abilities are given to its target as it lands, at the level of
  what applied it, and taken back, interrupted if running, as it comes off; only what it gave is taken back
  (FEAT-8).
- Chance to Apply. An effect lands by its chance, from 0 to 1 and scalable by level, drawn once each application
  from `ForgeRandom`; a miss is refused. The default, 1, always lands and draws nothing. The Effect inspector sets
  it in Requirements, above the tables, since it gates the whole application (FEAT-8).
- Overflow at max stacks. A Stack Bounds Config's Overflow Effects land, from the application's origin, as a stack
  would take the effect past its Max; Deny Refresh On Overflow keeps that application from refreshing or extending
  the effect; Clear Stacks On Overflow takes its stacks off. A stack past the max did nothing (FEAT-8).
- A fixed-rate tick: with ProcessControl's Fixed Tick Rate set, the game steps in ticks of a fixed step of its time,
  as many a frame as the frame's time holds, up to Max Ticks Per Frame. Every process steps once a tick, its end of
  frame with it, and each sequence's wait goes on by the tick (`ForgeTick.Next`), so a run comes out the same at any
  frame rate. 0, the default, steps once a frame, as before (FEAT-7).
- A clock for each entity, `IGameplayAbilitySystem.GetClock()`: the game's time scaled by its `TimeScale`. The
  entity's effects, its cooldowns among them, and its abilities' waits, sequences and projectiles run by it, so one
  entity can be slowed, hasted or held still while the rest run on (FEAT-7).
- `ForgeTime` and `ForgeRandom`, which the runtime reads instead of Unity's `Time` and `Random`: `Use` puts in a clock
  of your own (`IForgeClock`) or a random source (`IForgeRandom`, such as `SeededRandom`), for a replay, a server or a
  test. By default they read Unity's, so `Random.InitState` still seeds a roll. `GameplayEffectSpec.ApplicationId`
  numbers each effect application on its target (FEAT-13).
- Tag-change events. `TagCache.Listen(tag, onChange)` hears a tag, and every tag under it, come and go as it
  happens, or every change to its weight with `ETagChangeKind.AnyCount`; `TagCache.TagChanged` hears every change.
  A rebuild of the tags from their sources tells only what it changed (`BeginRebuild`, `EndRebuild`). Nothing told
  anyone a tag came or went: a tag worker polled for it every frame (FEAT-3).
- `ProcessControl.Register(process)` starts a registration, which `HandledBy`, `With` and `In` shape and `Run` makes:
  it registers the process and starts it, and says why when ProcessControl refuses it (`ProcessRegistrationResult`,
  `EProcessRegistrationRefusal`). Left out, the handler follows the packet's run-time type, so an ability's packet
  hands its process to its source wherever it's passed as a plainer packet. `ProcessRelay.Run` starts a process that
  waits, as one that requires control does (FEAT-15).
- Cleanse and immunity. An effect's Removes Effects Tagged takes off, as it lands, every effect on its target tagged
  with one of those tags or a child of one, by its asset tag, a context tag or a tag it grants; its Blocks Effects
  Tagged refuses such an effect while it's on. A cleanse or an immunity took a requirement on each effect it meant to
  clear or block (FEAT-8).
- What the Debugger reads of a process and an ability: `ProcessControlBlock.MeasureSteps`, which has each process
  count its steps and time them (`Steps`, `StepSeconds`, `PeakStepSeconds`), `RunIsHeld`, whether a paused
  process holds its run, and `AbilitySpecContainer.ActiveStageIndex`, the stage an ability's run is in (ED-4, ED-5).
- Loose tags: `AddLooseTag`, `RemoveLooseTag` and `GetLooseTags` on each hub grant a tag with no effect, ability or
  item behind it, as Unreal's loose gameplay tags do. Each grant counts until it's taken back. A tag added straight to
  the tag cache went at its next compile (FEAT-16).
- One validator and a Problems view (`Window → PlayForge → Problems`). The validator checks each PlayForge asset for
  what its inspector warns about in place: a link with no provider, an impact that never applies, a field that reads a
  scaler it hasn't got, a Shared scaler with no asset, two fields that hold one scaler, a set element with no attribute
  or a magnitude outside its bounds, a stale asset tag, a missing name, and data whose class is missing. The view lists
  each problem with its severity and asset, and a click selects the asset; an inspector's header counts its asset's
  problems. Both follow the project, with no Refresh. `ForgeValidator.LogAll` writes the problems to the Console in
  batch mode, and a `ForgeValidationRule` of your own adds a check (ED-15).
- Scaler assets: a scaler that many fields share. A field of the Shared kind reads its asset's scaler, so an edit to
  the asset reaches every field that uses it, and the field's own number adjusts what it reads, as in Fire Duration
  × 1.5. The Scaler Template asset is the Scaler asset now, without the sync settings nothing read. An attribute set's
  elements share one through the cached Shared kind. `SCALERS.md` describes scalers (ED-33).
- An effect can make more than one impact. Its Additional Impacts each change an attribute with an operation, magnitude
  and delivery of their own, so one effect can lower Armor while it lasts and burn Health on each tick. Add Impact adds
  one the duration can make, and the Cost rule checks each attribute a cost spends (ED-3).
- Reference pages for the six asset types in `Documentation`. Each inspector section's help button opens its type's
  page, through one table of links in the editor.

### Changed
- A world of its own reaches for nothing of the scene's. What's registered there with no handler has none, where
  GameRoot took it and kept it for as long as the scene lasted, and with no packet it takes the default, parented
  nowhere, where a MonoBehaviour process's and a sequence's took the scene root's (FEAT-10).
- The Queue policy's requests wait out a window, 0.3 s by default, where they waited for good, and fire by priority
  and then the newest, where they fired in the order asked. A press again replaces the first, where it queued a second
  (FEAT-6).
- `RunProcessTask` runs on scaled time by default, and `ProcessRelay.RemainingRuntime` reads scaled time unless asked
  for unscaled; both read unscaled time by default, apart from the game's time scale (TS-6). A `RunProcessTask` saved
  with Use Unscaled Delta Time on keeps it.
- An ability activation's `HandleId` counts from 0 per ability system, where one count numbered every entity's
  activations in turn (FEAT-13).
- A tag worker is evaluated at the end of a frame in which the tags changed, a worker came or went, or the last
  evaluation queued an action, where every worker was evaluated every frame. A worker that reads more than the tags
  overrides `EvaluateEveryFrame` (FEAT-3).
- ProcessControl's 28 static `Register` overloads that take an `out` relay are obsolete wrappers of the builder, and go
  before the first Asset Store submission. Through them too, an ability's packet passed as a `SequenceDataPacket` or
  a `ProcessDataPacket` hands its process to its source; it went to GameRoot, so the process didn't end with its
  entity. `TaskSequence.RegisterAndRun` did this with every ability's packet (FEAT-15). With no ProcessControl, a
  registration is refused, where the overloads threw.
- One Debugger (`Window → PlayForge → Debugger`) replaces the GAS Debugger, Process Control and the Tag Debugger.
  Its toolbar switches between three views, searches the one shown, and filters it by hub or by state. **Entities**
  lists every hub ProcessControl registers, runtime hubs among them, beside the one selected: its attributes with
  what changed them, its effects with the time each has left, their stacks and where they came from, its abilities
  with their state, such as Stage 2/3, and cooldown, and its tags with what grants them. Each section's foot row acts
  on that entity: it sets the selected attribute's base and current values, applies an effect picked there or takes
  the selected one off, grants an ability picked there at a level and activates or interrupts the selected one, and
  grants a tag typed or picked from the project's until it's removed. The status bar says what each did. Follow
  Selection shows the entity whose GameObject is selected. **Processes** lists every process with its lifecycle,
  timing, state, lifetime and the time its steps took over the last second, sorted by any column, and pauses,
  resumes and terminates the one selected, with the processes it handles; it says whether a paused run is held or
  was cancelled. **Tags** shows every tag the entities hold as a tree, beside who holds the one selected and what
  grants it. The views follow ProcessControl as processes join and leave and change their values in place, at the
  rate the window menu sets, and a status bar counts the processes and entities. Outside Play mode it offers Play
  Mode. The GAS Debugger scanned the scene for MonoBehaviour hubs and rebuilt its details every 0.25 s, and Process
  Control's usage was the time between a process's steps, not what they cost (ED-4, ED-5, ED-40).
- Templates work as prefabs do. A template reaches its children when it's pushed, with its asset menu's Push to Its
  Children, as one Undo step, and each child, and each of theirs, keeps the fields it overrides. An edit to a child's
  field makes it the child's own, which its inspector marks as Unity marks a prefab instance's override, and one Undo
  takes back both. The field's right-click menu has Revert to Template. Saving a template no longer opens a dialog in
  the middle of the save, loads every asset of its type, or changes the children with no Undo (ED-13).
- The Forge is a list beside a detail pane. Its toolbar holds Create, a filter for each type with its type icon, Tags,
  the search and the problem count, and its status bar the counts. The list's columns, the name with its type icon, the
  type, the asset tag, the levels and a problem glyph, sort in natural order, and it scrolls through thousands of
  assets. A click selects a row and shows the asset's inspector in the detail pane, under a summary of its sections; a
  double click shows it in the Project window, and its right-click menu shows, opens, duplicates or trashes it. Tags
  lists the project's tags and what uses each. The Forge follows the project with no Refresh, keeps its filter, search
  and selection across a domain reload, and Ctrl+Alt+Shift+F opens it with the search focused. Inside it, an
  inspector's Open in The Forge reads Show in Project (ED-27, ED-9).
- The Forge's Create makes an asset through the one factory a field's New uses: a copy of the type's New Asset
  default, named, with the tag its name gives, where Project Settings puts its type. A duplicate is named as a copy,
  with its own tag; it kept the original's name and tag.
- PlayForge's settings belong to the project.**Project Settings → PlayForge** holds where each type's new assets go
  and how their files are named, each type's defaults (the asset a new one starts as a copy of, and the assets Import
  lists first) and the Cost and Cooldown effects, in `ProjectSettings/PlayForgeSettings.asset`, under version control.
  **Preferences → PlayForge** holds each user's view toggles and the log level. They lived in EditorPrefs, which every
  project on the machine shares; a project takes them from there on its first use (ED-21).
- A magnitude field reads as the canvas's scaler board draws it:its name and kind (Constant, By Level, Shared or
  another), the range it gives, as "6–8 s", and a field menu with Save as Scaler Asset, Use a Scaler Asset, Import
  Values, Quick Fill and Make Constant. A By Level field's values sit in a table, ten levels to a row, with a read-only
  curve beside them, and Combine says how the field joins its number with its scaler. A Scaler asset's inspector lists
  the fields that share it (ED-33).
- Import Values compares a Scaler asset's or another field's values with a field's, level by level, before it copies
  them. It replaces the scaler import window and the curve editor (ED-32).
- A scaler you make maps levels exactly: level 4 reads its 4th value, and a level past the end reads the last. Its new
  Stretch Over Levels spreads the values over a level range, as every scaler did, and the scalers saved before it keep
  it on, so they read as they did (Decision 28).
- One Import window serves every inspector section, a whole asset, a worker group and a requirement group: choose a
  source, then the fields that differ, each checked, and import them as one Undo step. A list can take the source's
  items after its own instead of being replaced. Clear resets a section at once, and Undo takes it back (ED-32).
- The Effect inspector's sections are Identity, Leveling, Tags, Duration, Impact, Requirements, Workers, Local Data and
  Used By, as the other asset inspectors' follow the style doc. Duration shows the fields its Policy uses, by the
  Effect mockup's names; Re-application's choices are Add Another, Replace Existing, Stack Existing and Do Nothing.
  Requirements is a table of what each group requires and must not have, for the target and then the source, and a
  cell opens its list to edit. The Ability inspector's tag requirements use the same table (ED-37).
- An effect's impact says how it is delivered, which the duration gives it, and how often a periodic one lands, as in
  "7 over 6 s, level 1". Operation sets the operation and the value it changes in one choice, such as Add to Current.
  Reverse On Removal, which changes nothing, is no longer shown (ED-2).
- Every reference to a PlayForge asset is Unity's object field, with Open, which shows the asset in a window of its own,
  or New, which makes one where The Forge puts its type and uses it. An effect field says under it how the effect takes
  its levels, with Link, and setting it no longer asks in a dialog; its template, a pick of unlinked effects and New
  Linked Effect are in its right-click menu (ED-38).
- The Ability inspector's sections are Identity, Leveling, Tags, Activation, Stages, Requirements, Workers, Local Data
  and Used By, as the other asset inspectors' follow the style doc. Activation holds When Busy (Activation Policy),
  Activate At Start (Activate Immediately), Cost, Cooldown and the targeting; Stages holds the asset loaders and the
  stages; Requirements holds the tag requirements and the Caster and Target Checks (Source and Target Rules). When Busy
  shows its choices by what they do, in the Ability and Entity inspectors: Entity's Policy, Activate Anyway, Refuse and
  Queue (ED-37).
- The Attribute Set inspector's sections are Identity, Leveling, Attributes, Sub-Sets, Collision Settings, All
  Attributes, Workers, Local Data and Used By, as the Item and Entity inspectors' follow the style doc. All Attributes
  is a table of each attribute the set gives, its sub-sets' included, with the value it starts at and its range, settled
  as the runtime settles them; it replaces a dialog that listed their names. The Entity inspector shows the same table
  under its Attribute Set (ED-37).
- The Entity inspector's sections are Identity, Leveling, Tags, Attributes, Abilities, Items, Workers, Local Data and
  Used By, as the Item inspector's follow the style doc. The Attribute Set has an Attributes section of its own. A
  starting item takes one row with its Equip checkbox, and Equipped Too (Allow Duplicate Equipped Items) waits for
  Allow Duplicates, which it needs (ED-37).
- The Item inspector's sections are Identity, Leveling, Tags, Effects, Abilities, Workers, Local Data and Used By, in
  the style doc's order. Identity holds the asset tag. Leveling holds the level link that Level Source did, with the
  provider in Unity's object field, whose right-click menu picks one type; a help box says when none is set. Each
  collapsed section shows one line, such as "Levels 1–20 · from Hero", every section's Import and Clear work, and Used
  By lists the assets that use the item (ED-37).
- The Attribute inspector's sections are Identity, Local Data and Used By, which lists the assets that use the
  attribute; a row selects its asset. It replaces Find All References, a dialog that told you how to search. A
  collapsed section shows a one-line summary, and every inspector's text fields line up with its other fields' labels
  (ED-15).
- Every asset inspector's header and sections follow PlayForge's one style sheet, in the editor's Dark or Light
  theme. The header shows the type icon, the asset's name, its type and asset tag, then Open in The Forge, Help and a
  menu that holds import and the template. It no longer shows the description or the asset's own icon. The Forge and
  the Debugger take the editor's theme (ED-20).
- The editor shows no emoji. Icons are vector glyphs in the theme's colors, The Forge shows each asset type's icon, and
  a button that showed an emoji alone says what it does, such as Copy, Preview or Trash (ED-20).
- PlayForge is licensed under the Unity Asset Store EULA.
- The samples moved to `Samples~`, so Package Manager imports them on request: **Demos** and **Code Examples**. The
  TaskSequence and group process examples no longer compile into the runtime.
- UniTask is pinned to 2.5.11.
- The menu moved under **Window → PlayForge**, and each window's title matches its menu item. The main window is
  **The Forge** everywhere. Run Golden Tests left the menu: the Test Runner runs the golden scenarios.
- Deleting an asset from The Forge names the assets that use it, asks, and moves it to the trash.
- Showing an asset never changes it. A rename stores the name and its asset tag in one undoable step, and a stale
  tag offers Update.
- The Demos render in URP as well as the Built-in pipeline.
- Pausing a process holds it in place: it stops stepping at once, and on resume it picks up its run if it can hold
  one.
- The process hierarchy follows transforms: a process spawned at `SceneRoot` is GameRoot's child.
- A commit lands on the current value as measured: after Add, Multiply and FlatBonus, and before Override.
- A raw delta, one handed to the GAS without an effect, reaches attribute and impact workers.
- A disjoint stops homing shots only; skill shots land as usual.
- A random scaler keeps one roll per effect spec, unless set to roll on every evaluation.
- Attribute elements set to Combine add up, and a set's `UseAverage` averages.
- A derived attribute's refresh runs the post-change pipeline and reaches the frame summary.
- `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` forward to one core, so the two hubs behave alike. A
  hub's `AttributeSystem`, `AbilitySystem`, `ItemSystem`, `EffectSystem` and `LevelSystem` are read-only properties,
  public on both. Each hub has what only the other had: `ApplyGameplayEffect(origin, effect)` and
  `GetRelativeLevel()`.
- Applying an effect costs the same however many effects the entity carries. It no longer re-checks every effect's
  requirements or rebuilds the entity's tags: the requirements are checked once, at the entity's next update, and a
  durational effect's tags count from the moment it lands, so its own application workers see them.
- Ending an effect costs the same however many end with it, so a cleanse or a mass expiry costs as many as it ends,
  not their square. Effects ending in the same update leave the entity together before any of their removals run, so
  a removal worker no longer finds another effect that is ending with its own.
- A tick allocates nothing on its way to its commit, and a recompute, a modifier refresh and a same-root tag query
  allocate nothing. The built-in `Tags` are made once, not on every read. `GameplayEffectSpec.GetImpactedAttributes`
  returns a `ReadOnlyListView` and `GameplayEffectImpact.GetContainedEffects` a `ContainedEffects`, which `foreach`
  walks without allocating and which still enumerate as before. `Tag.IsChildOf` compares ordinally.
- A commit allocates nothing, so a frame in which an effect ticks and commits allocates nothing, with or without a
  worker adjusting it as armor does. A `ChangeValue` handed to a callback or a worker is reused once its commit is
  done: read it during the call, or copy its `Value`. A deferred worker's actions may still keep it. `TrackedImpact`
  keeps a total, the last impact and a count, not every impact. `ForgeHelper.SignPolicy` and `SignPolicyExtended`
  take one or two values without allocating.
- A raw delta allocates nothing on its way through the GAS. `GenerateSimple` still makes a new carrier on every call,
  so a raw delta made every frame keeps one, made once with `IAttributeImpactCarrier.GenerateSourceDerivation`. A
  `SourceAttributeImpact` with no root carrier gives the same origin each time it's asked for one.
- A rescale with Base and a lifesteal's heal allocate nothing, and neither does taking an action off an entity's queue.
  The system uses its own `ModifyAttributeAction`s again once the queue has run them, so a listener of
  `OnActionQueued` or `OnActionExecuted` reads an action during the call. `ActionQueue.EnqueueRange` reads a list or
  an array by index, and `RelativeOperationContextImpactWorker.CreateDeferredActions` returns a list the worker
  reuses for its next action, so what it returns is queued at once.
- Tags compare and hash by an ID each is given at run time, as it's made or loaded, so a lookup costs the same however
  long a tag's name is. Nothing new is serialized. Two tags are still equal exactly when their names are, and a tag
  whose `Name` is changed in place takes its new name's ID. `Tag.GetHashCode` no longer returns the name's hash, and
  can differ from one run to the next.
- A stage under WhenAll or RepeatUntilSkipped hands its policy the stage's own token source and makes no second one,
  so a three-stage ability makes about 10 token sources, not 14. A stage under WhenAny, WhenN or a policy of your own
  still makes one, as its policy may cancel what it's handed.
- Sequences and abilities end in the frame their last task does. A task made with `Task(Action)` runs its action at
  once, as `Do` does, not a frame later. A run waits a frame for its maintained stages only when it has some, and
  waiting on a process no longer polls: `ProcessRelay.WhenTerminated` completes in the frame the process ends, and
  the ability proxy and `RunSequenceTask` wait on it. An instant ability ends in the frame it starts, and a nested
  sequence adds no frame; so an instant sequence can end inside `ProcessControl.Register`, and an instant ability
  inside `TryActivateAbility`. A repeating sequence that runs instantly still takes a frame per repeat.
- A frame whose summary has a listener allocates nothing. The `FrameSummarySnapshot` that `OnFrameComplete` hands a
  listener is its entity's own and is refilled each frame, so read it during the call, or copy what you need.
  `FrameSummary.CreateSnapshot` still makes one to keep.
- A task waiting out a time in its sequence, as `DelayTask` and `ChannelAbilityTask` do, allocates nothing a frame.
  Asking whether its sequence was paused took 24 bytes a frame.

### Fixed
- The Debugger kept its wide layout docked in a narrow column, squeezing its lists and details side by side. Below
  720 px each view's list sits above its detail, the toolbar's views take a row of their own, the frame count moves
  to the status bar, and the tables hide their lowest-priority columns (ED-43).
- A second world of its own refused its first process where GameRoot handled it, as it did by default: each world
  numbered its processes from 0, and a handler keeps what it handles by that number. Every world draws its
  processes' cache indices from one counter, so each is a process's own among all worlds' (FEAT-10).
- A MonoBehaviour process ended what it handled through the scene's ProcessControl, by cache index, so one in a
  world of its own ran on, and the scene's process under that index ended in its place. It ends each through its
  relay (FEAT-10).
- A sequence registered in a world of its own with no packet threw where there was no GameRoot, as outside play
  mode (FEAT-10).
- A runtime entity that fell ended whatever process ProcessControl held under its cache index, and not its own, when
  another world held it. It ends through its own relay (FEAT-10).
- A process's relay, its state changes, a watch on it and what a runtime process handles reached
  `ProcessControl.Instance`, whichever control held the process, so once another control was Instance they reached
  the wrong one. Each goes to the control that holds the process, which `ProcessRelay.Control` and
  `ProcessControlBlock.Control` name (FEAT-10).
- A section's Clear set each field to 0 or its first choice, not its own default, so clearing an effect's Impact
  allowed no target and left no chance to apply: the effect never landed. A cleared field takes a new asset's value,
  and a cleared list still empties (ED-42).
- Click targeting in a scene with no main camera threw at the press. The press now aims nowhere, and a warning says
  so (FEAT-11).
- A level and its max set at once, or raised at once, applied the level first, so a level past the old max was
  held at it (GAS-59).
- A lasting effect whose magnitude reads a level follows the level as it changes. Its magnitude was read as its
  modifier registered, so an item that levelled up kept its first level's bonus (GAS-58).
- A Mono process's object goes back to the instantiator that made it as the process ends. The wrapper asked the
  object's own instantiator, which a clone never has, so a pool's object was destroyed and never reused (PC-33).
- A task sequence's critical section ends as the run starts a stage past its last critical stage, if a branch skipped
  that stage; it ended only as a critical stage ended, so a skipped one held the ability's claim to the end (TS-7).
- A relative-operation impact worker reads its Relative Attribute. It read the impacted attribute's value even with
  one set, so lifesteal by a Lifesteal share healed the attacker by its own health times the blow; one with none set
  reads the impacted attribute still (GAS-57).
- A tag that an ability, an item or a loose grant gives or takes back meets or breaks an effect's requirements at the
  next frame. An effect's requirements were checked only when an effect came or went (GAS-56).
- Importing a whole asset no longer copies its name, asset tag, template or level link (ED-35).
- Opening an attribute set no longer changes it: its element drawer clamped a magnitude outside the bounds into the
  asset as it drew, and again on every change to the set. A help box says the runtime clamps it, and a typed magnitude
  is still kept within the bounds. The element drawer follows the style sheet (ED-39).
- An attribute set element with no Attribute no longer stops an entity from starting; it gives nothing (GAS-54).
- An attribute set whose collisions settle by Use Minimum takes the lowest value; it took the first (GAS-55).
- The Attribute inspector's Name, Abbreviation and Description undo, and the inspector edits several attributes at once.
  Every asset inspector's header follows Undo and Redo (ED-12).
- The MonoProcess inspector edits several selected processes at once (ED-12).
- The Effect inspector's Description and Link Mode undo, and its Level Source section follows an Undo (ED-12).
- The Item inspector's Description and Link Mode undo, and its Level Source and Leveling sections follow an Undo
  (ED-12).
- Every field of the Entity inspector undoes: Description, Starting and Max Level, Activation Policy, the three Allow
  Duplicate toggles and Link Mode (ED-12).
- The Attribute Set inspector's Description and Link Mode undo, and its Level Source and Leveling sections follow an
  Undo (ED-12).
- The Ability inspector's Description, Activation Policy, Activate Immediately and Link Mode undo (ED-12).
- Link, Unlink and template assignment undo in every asset inspector, children included (ED-12).
- Each inspector's Unlink All question counts the children it would unlink; it always said 0 (ED-31).
- A drawer that rebuilds itself, such as the tag requirements or worker group drawer, keeps editing every selected
  asset; after a rebuild it edited only the first (ED-12).
- The Impact and Effect drawers follow the properties they show instead of re-checking on a timer, and the Impact
  drawer's Multiply Behaviour hides again after an Undo (ED-11).
- An asset inspector listens to the editor's update only while an object picker is open; it ran every frame (ED-11).
- An asset inspector measures nothing on a timer. Its header stays at the top of the Inspector by following the
  Inspector's own scroll view; it measured the window every 200 to 300 ms for as long as it was open (ED-11, ED-36).
- An asset inspector's header and sections are legible in the Light theme; they kept hard-coded dark colors. A
  section shows Import, Clear and Help at all times, not only while the pointer is over its header (ED-20).
- A drawer keeps its collapsed parts for each asset until the editor closes, through domain reloads. Collapsing a part
  on one asset collapsed it on every asset, and a reload opened everything again. A worker's state follows it when its
  list is reordered, and deleting a stage leaves the later stages as they were; it opened them all (ED-18).
- The Item inspector edits several selected items at once. Name, the asset tag and linking wait for one item (ED-12).
- The Ability inspector edits several selected abilities at once. Name, the asset tag, linking, targeting, asset
  loaders, stages and activation rules wait for one ability (ED-12).
- The Effect, Entity and Attribute Set inspectors edit several selected assets at once. Name, the asset tag, linking
  and Used By wait for one asset, and so do an effect's additional impacts, workers and stack behaviours, a set's
  attributes, and the attribute tables (ED-12).
- A magnitude field, and a scaler drawn on its own, edit each selected asset's own values. Kinds that differ show as
  mixed, and choosing one gives it to each field from that field's own value; a field of that kind keeps its scaler.
  Levels and Make Constant work on each field's own values, where they gave every field the first one's (ED-12).
- Drawing an empty type field no longer writes its default type into it (ED-12). An empty activation rule or scaler
  behaviour is now skipped; both threw. A new Inject task starts with Interrupt.
- Opening a scaler no longer changes it. On an effect with no level link, Lock mode cut its per-level table to one
  value; other modes resized the table, a Max Level of 0 became 10, and a scaler two list elements shared was cloned.
  A shared scaler now shows a warning with a button that makes a separate copy (ED-34).
- Runtime correctness: 96 fixes across ProcessControl, TaskSequence and the GAS core, among them exception isolation,
  queued state changes, pause, effect stacking and ticking, commit order, worker scheduling and targeting.
- A fresh project imports the package with no warning and no missing type, and the samples import with no missing
  script, reference or type.
- BuildVerification builds an empty scene of its own when the build settings enable none, instead of the first scene
  the project lists.
- PlayForge works with domain reloading off (Enter Play Mode Options): each play session starts clean.
- The Forge: its settings save, reset and refresh as labelled; Analysis hides the groups it can't analyse yet and says
  how much a list leaves out; the tag display settings apply everywhere.
- An entity's affiliation is its own: the sides it registers with no longer change its Entity asset, and with it every
  entity that shares the asset.
- Unequipping an item takes back the ability that item granted, not another copy of it.
- Abilities that activate immediately wait until the entity's attributes, items and tags exist, so an opening ability
  with a cost activates.
- An effect that only ticks on application lands its change.
- An attribute set that includes itself, directly or through another set, no longer recurses forever, and a set reached
  by two paths counts once.
- A runtime GAS applies its entity's affiliation as tags, as a GameObject's does, and one made from an Entity asset
  keeps the asset's Local Data.
- A sequence that repeats no longer grows its packet's path for as long as it runs. Each part a run or a stage adds to
  the path comes off as it ends, and a failed run still logs the path to where it failed. The parts are formatted
  once, and the LINQ left on an ability's activation is gone.
- An ability asked for again from inside its own activation, by its own callbacks or by another ability's that it
  started, starts in the next frame. It started inside the call, so a loop of instant activations nested until the
  stack overflowed. `TryActivateAbility` returns true for such a request. Activation is otherwise as before: the
  ability runs inside the call until one of its tasks waits.
- A runtime entity's abilities end when it does. A runtime process now ends the processes it handles as it ends, as a
  Mono process does, so ending a runtime entity, however it's ended, ends its running abilities, critical or not.
  They ran on for as long as their stages lasted. An entity that has ended takes no more activations:
  `TryActivateAbility` returns false at once, where a destroyed Mono entity threw.
- Interrupting every running ability at once ends each of them, whether through `InjectAll`, `Inject` with a policy,
  or turning `Enabled` off. Once the first had ended, it threw, and the rest ran on.

### Removed
- The GAS Debugger, Process Control and Tag Debugger windows, which the Debugger replaces, and Process
  Control's four public enums, `EProcessDebuggerPolicy`, `EProcessStateFilter`, `EProcessSortMode` and
  `EUsageSortMode`, which nothing else used (ED-4, ED-5).
- The Forge's Create, View and Analysis tabs, and the Visualizer window. The detail pane's summary replaces the
  Visualizer, which was blank for items and sets; the Analysis tab's power scores stay hidden until items and entities
  score too. The Create tab's recently created list and statistics, the View tab's browsers of embedded scalers and of
  requirements (Scaler assets and requirement templates are in the list now), and the preferences they used are gone
  too (ED-27, ED-9).
- Validate All,which flagged every effect with no workers and showed 15 problems in a dialog, and the Audit Effect
  Impact Deliveries menu item, which wrote to the Console. The Problems view has their checks (ED-15).
- The Forge's Settings tab,whose settings are on the two settings pages; template tags, which only labelled templates
  on that tab; and the Create dialog's type settings, of which all but the Requirement's category named fields the types
  don't have, so they did nothing. A type's New Asset default sets such values now (ED-21).
- QuickOutline, replaced by the Demo's own outline.
- The test processes `TestAOEMonoProcess` and `TestMultiProcess`, and the unused `BootstrapperEditor`.
- Two settings nothing read: an entity's **Cap At Max**, since its level always stays between Starting Level and Max
  Level, and an item's **Allow Duplicates**, since the entity's **Allow Duplicate Items** decides. Code that set
  either field no longer compiles; delete the assignment.
- From the GAS hubs, what nothing used: `GameplayAbilitySystem`'s `Relays` field and `HandleDestructionInternally`
  hook, and each hub's protected `localData` field. A subclass reads Local Data through `GetLocalDataStructure()`.
- `PooledMonoProcessInstantiator`, a stub that gave back no process and kept a process's object when it ended, so it
  leaked. Nothing used it. To pool processes, extend `AbstractMonoProcessInstantiator`.
- An unused style sheet and two unused UXML layouts. `SectionResult.Arrow` is now the section's arrow button (ED-20).
- The Forge's Inspector settings, two delays for the asset inspector's sticky header, which is gone (ED-11). With it
  went `BasePlayForgeEditor.contentScrollView` and `ForgeDrawerStyles.CreateScrollView`: an inspector builds into the
  container `BuildInspectorContent` is handed.

## [0.1.0] - 2026-10-30

- First version as a UPM package.
