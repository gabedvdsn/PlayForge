# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html). The repository's `Docs/REFACTOR_PLAN.md`
tracks each fix below by its ID.

## [Unreleased]

### Added
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
- One Runtime Debugger (`Window → PlayForge → Runtime Debugger`) replaces the GAS Debugger, Process Control and the
  Tag Debugger. Its Entities view lists every hub ProcessControl registers, runtime hubs among them, beside the one
  selected: its attributes, tags, effects with the time each has left, abilities with their cooldowns, and the last
  impact it dealt and took, and it acts on that entity: its attribute fields take a value, a tag typed or picked from
  the project's is granted until its Remove, an effect picked lands, and an ability picked is granted, activated or
  removed. Its Processes view lists every process with its state and the time its steps take, and
  pauses, resumes and terminates the one selected, with the processes it handles. The views follow ProcessControl as
  processes join and leave, change their values in place at the rate the toolbar sets, and in edit mode offer Play
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
  the Runtime Debugger take the editor's theme (ED-20).
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
- The GAS Debugger, Process Control and Tag Debugger windows, which the Runtime Debugger replaces, and Process
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
