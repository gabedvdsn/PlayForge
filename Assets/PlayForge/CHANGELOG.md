# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html). The repository's `Docs/REFACTOR_PLAN.md`
tracks each fix below by its ID.

## [Unreleased]

### Added
- Conditions in an effect's requirements, beside its tags: Apply conditions to land, Ongoing ones to go on, Removal ones
  to come off, a phase passing only when its tags and conditions all say so, the lasting ones checked at each frame's
  end (TW-19).
- Changing sides as the game runs: `SetAffiliation` on both hubs, and an effect's Side Change, which gives the one it's
  on its caster's sides or those it names while it lasts, the highest priority holding, and interrupts what it runs
  where it says so (TW-18).
- A game's settings on its Forge Project, read through its GameRoot (or set in code): its attributes' roles, each a role
  tag, PlayForge reading its own (Health, Luck, Movement Speed) and a game's code its own through `ForgeRoles`, and its
  default crowd control; an ability's Attribute Costs, each spending the attribute or role it names beside its Cost
  effect (TW-29).
- Charges: an ability's Charges, several uses with a cooldown each, and its Recharge: each spent charge on a cooldown of
  its own, all at once as one cooldown ends once all are spent, or a recharge of your own (TW-17).
- Crowd control, project-wide: Crowd Control Rules, each a tag and the abilities it stops, named by an entity or else the
  project's; an ability they stop is refused with Crowd Control and one running is interrupted; an ability's Ignores
  Crowd Control opts it out of the rules it names (TW-16).
- The common rules, built in: an attribute's Gains Scaled By, as Healing Received; lifesteal on an effect and on items,
  abilities and entities; an Execute stage; a Can't Fall Below worker; and a Return Damage worker whose returned damage
  isn't returned again (TW-15).
- Effects that land on the caster: each contained effect, a stack bound's overflow effects and an effect a modifier adds
  lands on the target, as it did, or on the caster, as it says (TW-14).
- An effect's own worker group: attribute, impact, tag and analysis workers it gives the one it's on while it lasts,
  once however many of it last (TW-13).
- Absorption and block: the Barrier attribute role soaks the losses to the attribute it absorbs for, decaying at a rate
  or fading with the effect that gave it; Block and Evade pipeline stages stop an effect as it lands, the one hit hearing
  Blocked or Evaded and the dealer Hit Blocked or Hit Evaded (TW-12).
- Thresholds that act: the Threshold analysis worker reads an attribute's share of its base, or what's missing, fires
  as it crosses, falling, rising or either, once or each time, and applies effects, from the entity or the one whose hit
  crossed it, and sends a gameplay event (TW-11).
- Areas without physics: an area search finds each entity on either hub by its position, flat by default, around the
  caster, the activation's target or its point, with physics only where it asks; an effect's own Area lands it on those
  near its target; and auras on items, abilities, effects and entities hold their effects on those near their holders
  (TW-10).
- Number math: a Stacking Curve kind (linear, hyperbolic, reciprocal, inverse, exponential, then a number added), a
  Group's Divide, and an attribute read as what's missing of it (base − current) or its share of its base
  (current ÷ base), in × Attribute, the cached attribute scaler and the Attribute condition (TW-9).
- Chance, built in: a Chance condition that rolls once for a hit; pseudo-random chances, each caster's streak its own;
  luck, the project's Luck or a chance's own; an ability's Chance to Activate; and a hit's Proc Coefficient, which scales
  its on-hit effects' chance and duration (TW-8).
- The project's attributes, on Project Settings → PlayForge: Health, what an entity dies of at 0 where nothing of its
  own watches for its death, and what On Hit counts; Mana, what an ability's new Mana Cost spends; Luck; and Movement
  Speed, the Creator Lab's units'. Each is optional, and an asset's own setting wins (TW-28).
- Effect modifiers: items, abilities, effects and entities change other effects as they're made, their holder's own or
  those landing on it, picked by tag, with a contained effect added, an impact's number changed by a Value Pipeline
  stage, an overflow added, a duration, stacks, a chance or granted tags changed; inline, or a shared Effect Modifier
  asset (TW-7).
- The Creator Lab's battle: teams of bots, a quick battle, play and reset; `LabBot`, a bot's mind, which the Demo's AI
  now is (LAB-9, LAB-10).
- The Creator Lab, Window → PlayForge → Creator Lab: a space for trying a project's assets, its units, test dummies,
  library, panels and its own time (FEAT-35, LAB-1 to LAB-8). See `Documentation/CREATOR_LAB.md`.
- `IForgeInput` reads a key, a held pointer button, the pointer's place and the scroll wheel, each with a default, so a
  project's own input for click targeting still compiles (LAB-2).
- On Hit on items, abilities and effects: the effects the holder's hits apply, or a kill's, only of some impact types,
  to the one hit or the holder, under a condition; an item's while it's equipped, an ability's while it's known, an
  effect's while it lasts (TW-6).
- Gameplay events the core sends where an ability listens: Impact Dealt, Impact Taken, Kill, Died and Revived, with the
  instigator as Source and the one it was done to as Subject. An ability trigger takes only some impacts (attribute,
  sign, impact types), rolls a chance once for each event, and can target the instigator or the subject (TW-5).
- An entity that stays when it dies: an identity's Death says End, as before, or Stay, with the effects its death ends
  by tag, the effects it takes as it dies and revives, and its Body, a MonoBehaviour process that goes as it dies and
  comes back as it revives; `Revive(at)` on either hub, `ReviveOnRemovalEffectWorker` for a respawn timer, `GetBody()`,
  and `OnEntityRevived` (TW-4).
- Value Pipelines: an asset of ordered stages (Multiply, Add, Chance, Clamp, Absorb, or a project's own) that an
  effect's impact names as its calculation, run once for each hit as it lands, with a breakdown of each stage kept on the
  spec (TW-2). `Documentation/VALUE_PIPELINES.md`.
- An attribute's Role says how bonuses to it add up and how a number reads it: a Bonus Share's bonuses add and read as
  1 + x; a Resistance's stack with diminishing returns and read as 1 − x. An effect's Add Behaviour and × Attribute's
  Read As can choose their own way (TW-1).
- Attribute reads choose their retention groups: × Attribute, the Attribute condition, a relative attribute worker's
  Relative To and the Threshold worker read all of them, only these, or all but these (TW-3).
- `ForgeEntities`: every live entity on both hubs, which area searches go through (GAS-75).
- An asset with a texture shows it in its header in place of its type's icon: the texture under its Primary tag, else
  the first that has one (ED-69).
- An asset header's three dots offer what a right-click on its row in The Forge does: Show in Project, Open in a
  Window, Duplicate, Move or Copy to Project and Move to Trash (ED-71).
- The Forge's asset menu moves or copies its selection into another Forge Project, each asset into its type's folder
  there; its list takes a selection (FEAT-34).
- The Demo's assets make a Forge Project of their own, Demo Project, at the sample's root (FEAT-34).
- Forge Projects (FEAT-34): **Create → PlayForge → Forge Project** makes a project of the assets in its folder, an Asset
  Catalog of its own, whose **Add Its Assets** names them for saves. The Forge shows the active project's assets alone,
  and its toolbar's project menu switches between projects and makes new ones. A new asset goes into the active
  project; the first made makes the default project, at the project's root, which holds every asset no other does.
- The Forge goes back and forward over the assets it has shown, as a browser does: **Back** and **Forward** in its
  toolbar, Alt+Left and Alt+Right, and the mouse's back and forward buttons (ED-76).
- A tag's contexts can be declared: in The Forge's tag detail, **Declared For**, and in New Tag, **Contexts**, which
  start as the field's. The project's Tag Library keeps them, and a field of a declared context offers the tag (ED-83).
- The Forge's summary of a Scaler asset and a Requirement Template: its description, as text, and a line for each of
  its parts, where it said nothing (ED-74, ED-78).
- A Scaler asset's inspector has a **Name**, which renames it, as The Forge and the field menus list it (ED-74).
- A scaler field of its own, as a Group member's, has the number field's menu: Save as Scaler Asset, Scaler Assets,
  Import Values, Quick Fill and Make Constant, which makes it a Fixed Value of what it read at level 1 (ED-77).
- Quick Fill on a scaler spread over levels previews what each level reads between its points (ED-82).
- `AbstractScaler.IsEmpty`, whether a scaler is no kind at all, as a Group with no members, and
  `AbstractScaler.IsNone(scaler)` for a slot: a field that holds one reads its number, as a field with no scaler does
  (ED-80).
- A Group whose members multiply, with none that adds, says they multiply 0, under its members and in the Problems
  view (ED-80).
- An effect level by level, with what makes each of its numbers (FEAT-32). `NumberTrace.Of(number, context)` explains a
  number's read as a tree of parts, each with what it read and what it gave, and says what a part needs where its
  context lacks it, as "the target"; a kind or a condition of your own adds its parts by overriding `Explain`.
  `AbilityInspector.BuildEffectSeries(effect, min, max, caster, target)` reads an effect against a caster and a target,
  either of which may be null: each impact with its line, its parts at each level and what it needs, and the effect
  with its chance, tick interval and contained effects; `Describe(level)` says what it does at a level, an impact to a
  line (LVL-1 to LVL-3).
- The effect inspector's Levels section, which The Forge shows: the effect's duration, ticks, chance and impacts at
  each level, its contained effects' among them, and under them the parts behind the cell selected (LVL-4).
- Number fields' foundation (FEAT-29, phase 1): `ScalableFloat` and `ScalableInt`, one number type for every authored
  gameplay number, a constant by default and a value by level, a multiple of an attribute, a caller's value or any
  scaler otherwise. A float converts to one, a read is `Evaluate(context)`, and `IsConstant` and `Constant` serve
  previews and setup code (NUM-1).
- `NumberContext`, what a number is read against: the caster, the target, the level, the carrier, the caller's
  magnitudes, Local Data, the activation's data and the owner, with `Has(NumberReads)`. Build one `Of` an effect spec,
  an activation's data, a worker's context, an entity, a cue or a process, or `For` a test. A read allocates nothing:
  a scaler read where there's no carrier evaluates against a reusable one the context fills (NUM-2).
- Kinds of number of your own: a `NumberKind` is one class with one method, `Evaluate(in NumberContext)`, and
  `Describe()` for its field's summary; `[NumberKind(name, Category, Reads)]` names it, files it in the kind menu and
  says what it reads, so the inspector offers it only where it can be worked out. A kind is a scaler, so it works in
  an effect's magnitude, a Group or a Conditional too. `ScalableFloat.From(function)` gives an asset built in code a
  number worked out by a function (NUM-3).
- The number field's inspector files the kind menu by category and offers only the kinds a field's context resolves,
  which `[NumberContext]` declares; another kind shows a one-line summary, as "0.3 × Target's Health (base)"; and a
  field whose unit is "%" shows and takes 0.3 as 30% (NUM-5).
- A Problems rule for a number whose kind its field's context can't resolve, as one that reads a target its field
  hasn't got (NUM-6).
- Window/PlayForge/Upgrade Assets re-saves every asset that holds PlayForge data, so each is written in this version's
  form, and `LegacyNumber.Move` moves a float saved before its field became a number into the number as it loads
  (NUM-7).
- Number kinds that read the game (FEAT-29, phase 2), each with a summary that says what it reads:
  - **Local Data** reads a number under a tag in its own, the caster's or the target's Local Data, else its Default
    (NUM-8).
  - **Relative to a Field** reads another number field of the same object, as a radius that is half a range. It is
    offered where the field's context has an owner, and fields that read one another in a loop stop eight deep,
    where the field reads its own number and logs once (NUM-9).
  - **Tracked Impact** reads what an effect has dealt: its last impact, its total or how many, of this effect or of
    one found on the caster or the target by its asset or by a tag (NUM-10).
  - **Group Impact** reads an attribute's change held under a retention group, as a caster's Strength bonus from
    items (NUM-11).
  - **Asset Part** finds an effect, an ability or an item on the caster or the target by a tag, its asset tag, a
    context tag or a tag it grants, and reads a part of it: an effect's stacks, level, tracked impact, time left,
    time to its next tick or a worker's value; an ability's level, whether it is active or on cooldown and the
    cooldown left; an item's stacks, level and whether it is equipped; or how many match. Where several match it
    takes their sum, the first, the highest or the lowest (NUM-13).
- A number can ask a worker. A worker that implements `INumberWorker` answers `TryGetNumber(key, context, out value)`,
  and a number of the **Worker** kind asks under a key: this effect's workers, or the workers an entity provides, the
  caster's or the target's. An entity's worker groups provide their workers as they are added, `NumberWorkers.Provide`
  provides one in code, and `NumberWorkers.TryAsk` asks from code, so anything can ask what a number can (NUM-12).
- `GameplayEffectSpec.GetWorkerState<T>(worker)` and `EffectWorkerContext.GetState<T>(worker)`: what one worker keeps
  for one application of its effect. A worker is shared by every application, so its own fields are not the place
  (NUM-12).
- The **Attribute Change** effect worker answers how much of an attribute an entity has lost, gained or changed by
  since the effect began, or what it was as the effect began, and can answer only every so many ticks. With a Worker
  number it makes a curse that deals damage each second and more every fourth by the health lost so far, in one
  effect (NUM-12).
- `[NumberMin]`, `[NumberMax]` and `[NumberRange]` limit the constant a number field takes in the inspector, a range
  as a slider, and a number field shows its `[Tooltip]`, a clipped label's too. They limit what is typed, not what a kind reads: a Clamped
  scaler limits that. Unity's own `[Min]` and `[Range]` take a number field's drawing from it, so the console names
  each number field that has one as scripts load (NUM-15).
- Conditions as classes (FEAT-29, phase 3, Decision 43): `GameplayCondition`, read against a number's context with
  `Holds(context)`, and `[GameplayCondition(name, Category, Reads)]`, so a project adds a condition with one class.
  Built in: Tag, Attribute, Level, Compare Numbers, Always, Never, and under Logic All Of, Any Of and Not, each with
  its own fields and its own line, as "Target's Health < 0.3 × Target's Health (base)". Where the context lacks what a
  condition reads, it doesn't hold and logs once. A condition's field picks its kind from a menu (NUM-17, NUM-24).
- A scaler's own parameters are numbers (FEAT-29, phase 3): a Conditional's True and False, a Clamped's Min, Max and
  what it clamps, a Randomized's Variance and multipliers, Set By Caller's Default, and a Threshold's tier values and
  Default. Each scaler reads its own numbers with itself as their owner, so a Clamped's Max can be relative to its
  Min, and an asset saved before loads as it read (NUM-18 to NUM-23).
- In the inspector, a number inside a scaler offers the kinds its outer field's context resolves, and Relative to a
  Field there lists that scaler's own numbers; the Problems rule checks such a field against the scaler (NUM-24).
- `ScalableFloat.Initialize(carrier)` and `ScalableInt.Initialize(carrier)` ready a number's scaler as an effect's spec
  readies its own, and `LegacyNumber.TakeTable(scaler)` moves a scaler's own values per level into a number of the By
  Level kind, for a scaler that reads a number in their place (NUM-17, NUM-23).
- `GameplayEffectTags.Names(tag, effect)`: whether a tag names an effect, as its asset tag, one of its context tags
  or a tag it grants, or a child of one (NUM-13).
- `ERoundingOperation.Round`: a whole number can round to the nearest, a half away from zero (NUM-1).
- The Demo sample: a hero arena of five heroes, fourteen items, an AI hero and a training dummy, with a panel to
  pick heroes, give items and change levels, and a HUD. Every hero, ability, item, effect and attribute is an
  asset, and between them they use every asset feature; its README maps each to where it shows. It needs nothing
  beyond PlayForge, reads the Input System or the Input Manager, renders in the Built-in pipeline or URP, and its
  PlayMode tests play it (SP-25).
- What a gameplay mono process carries reaches the effects it applies: `GiveMagnitudes(spec)` gives a spec the
  set-by-caller magnitudes of the data that spawned it, then each of the process's own attributes under its role,
  so an effect's Set By Caller scaler reads what the process carries, as a fire's burn its heat. A projectile's
  effects take them as they land (FEAT-28).
- `AttributeValidation.ShareOfBase` compares an attribute's share of its base, from 0 to 1, in place of its value, as
  "a target below half its health" (FEAT-26).
- A Clamped scaler clamps a value worked out elsewhere, as damage worked out from a target's missing health is
  capped: its `Value`, a number of any kind (FEAT-27, FEAT-29).
- A gameplay mono process, as a projectile, carries attributes of its own (`Attributes`, `ProcessAttribute`): each
  read by a role, as a projectile's speed by ProjectileSpeed, and starting as its own value, a number or a scaler at
  the spawning ability's level, its origin's attribute, or the origin's where it has one. The data that spawns it
  can set one, and its code reads and sets them as it runs. A projectile's speed is the one it carries, else its
  caster's (FEAT-24).
- The Problems window's By Rule view: problems grouped under the rule each breaks, with a Quick Fix where one step
  fixes a problem, as storing a stale asset tag or clamping a magnitude to its bounds, and a Fix All for each rule,
  each one Undo step. A rule of your own names its group with `ForgeValidationRule.Name`, and offers a fix with
  `ForgeProblem.Fix` (FEAT-25).
- `AttributeStartValues`, an editor API that works out what each attribute an entity built from an attribute set
  starts at, at each level, as the runtime does, and which element each value comes from (ED-49).
- `AbstractScaler.Parts` and `[ForgeShowWhen]`, which say what a scaler of your own shows in its inspector: the parts
  of every scaler it reads, its values per level, their level range and its behaviours, and when a field of its own
  applies (ED-51).
- The Battle sample: a battle that plays itself, Blue and Red, eight fighters a side, built from PlayForge's
  attributes, abilities, effects, cues, projectiles and summons, made in code, with a health bar and effect boxes
  over each fighter. It needs nothing beyond PlayForge, and renders in the Built-in pipeline or URP.
- `ProcessDataPacket.TryGetAt` reads the entry at an index under a key, building no list, and
  `SetTargetingPackets` files a targeting task's targets in order, the first as the primary (GAS-62).
- Moving an effect to another entity. `EffectSystemComponent.MoveTo(effect, onto)` moves each of an effect's
  containers with its stacks, its time left and what applied it; its lasting changes, granted tags, lent abilities
  and lasting cues go with it, and nothing of a removal or an application runs (FEAT-23).
- An entity's time scale from an attribute. An entity's Time Scale names the attribute its time runs at, so an
  effect that lowers it slows the entity, its effects, cooldowns, sequences and waits with it, and one that raises
  it hastes it (FEAT-22).
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
- The authoring settings live on the Forge Project, each game's its own: where each type's new assets go and how their
  files are named, each type's defaults, and the Cost and Cooldown templates. Project Settings → PlayForge shows the
  active project's settings, and the first project to need them takes what ProjectSettings/PlayForgeSettings.asset held,
  once (TW-30).
- An ability's cost, Mana Cost and cooldown apply as it starts, after its targeting, where no stage marked Apply Usage
  Effects takes them; they applied only from such a stage, so most abilities paid nothing. The warning that said so,
  and its Problems rule, are gone (TW-17, ED-41).
- Auto Target and Auto Target By Affinity find entities by position, flat by default, with no collider needed; **Use
  Physics** searches with physics on their layers as they did (TW-10).
- A Group member's Operation is an `EGroupOperation`: an impact's four in their order, so a member saved before keeps
  its meaning, and Divide. `ScalerGroup.AddMember` takes either (TW-9).
- The Demo's units, camera, dummy and controls are the Creator Lab's, its roster the lab's rules; `DemoInput` is gone,
  the Demo reading `ForgeInput` (LAB-7).
- Renaming an asset in its inspector renames its file, with its type's prefix and suffix; a name another file has takes
  a number, and an Undo of the rename brings the file back (ED-70).
- The Forge opens its list as wide as it was left (ED-72).
- A Local Data row takes its look from the style sheet, with no separator of its own (ED-73).
- Every asset picker offers the active Forge Project's assets alone: a PlayForge object field's picker, the Scaler
  Assets menu, the template picker, the Import window, Import Values, the unlinked-effect picker, and the tag picker,
  which offers the tags the project's assets use (FEAT-34).
- The Create menu makes a Forge Project in the Asset Catalog's place; a catalog made before goes on as it was
  (FEAT-34).
- A row of an asset's Used By, a Scaler asset's among them, opens that asset in The Forge, where it selected it in the
  Project window (ED-76).
- A Requirement Template's **Category** is a tag of the Requirement Category context, where it was a string; a
  template saved with one loads it as the tag of its name (ED-79).
- A scaler keeps its name as its kind changes (ED-74).
- The field menu files the Scaler assets under a **Scaler Assets** submenu, the one the field shares checked; it
  listed each at its top (ED-77).
- A scaler field's kind menu files its kinds by category, as a number field's does (ED-81).
- A Group with no members, or none that holds a kind, is no kind at all: its field reads its number, where it read
  values per level the inspector hid. A Group shows no values per level (ED-80, Decision 44).
- **`ConditionalScaler`** is a number kind. Its condition is `When`, a `GameplayCondition`, in place of `Condition`
  (an `EScalerCondition`), `TagCondition`, `CheckAttribute`, `ThresholdValue` and `Comparison`, and its branches are
  `TrueValue` and `FalseValue`, numbers, in place of `TrueScaler` and `FalseScaler`. With no condition it reads
  False, and it no longer reads its own values per level. `EScalerCondition` is gone. Saved assets load as they read
  (NUM-17, NUM-18, NUM-23).
- A Conditional's tag condition on the target counts a tag by its weight, as on the caster, so a tag granted twice
  counts twice: on the target it counted each tag once, and a query with a count read differently on the two sides
  (NUM-17).
- **`ClampedScaler`** is a number kind. `MinValue` and `MaxValue` are numbers, and what it clamps is `Value`, a number,
  in place of `Inner`; one made with nothing set clamps a value by level, 1, and it no longer reads its own values
  per level (NUM-19).
- `RandomizedScaler`'s `Variance`, `MinMultiplier` and `MaxMultiplier` are numbers; `Variance` takes `[NumberMin(0)]`
  in place of `[Range(0, 1)]`, so a flat variance can pass 1 (NUM-20).
- `SetByCallerScaler.Default` is a number (NUM-21).
- **`ThresholdScaler`** is a number kind, and reads its context's level. `LevelThreshold.Value` and `DefaultValue` are
  numbers, and a tier's value draws as a number field under its level and name. A `LevelThreshold` saved outside a
  Threshold scaler loads with no value, which reads 0 (NUM-22).
- A kind inside a scaler that isn't a kind, as one of your own holds, reads the context its number was read in, with
  its owner and its data; it read one made again from the effect's carrier, which has neither (NUM-16).
- **Group** is a kind of number, at the kind menu's top, and a member can be any kind, as a Local Data read or a
  Tracked Impact. Its Flat Bonus members join the sum, after the Multiplies: they were left out, so a group of
  5 and a Flat Bonus of 3 read 5. With the Ignore policy its Override members are set aside and the rest join, and
  joining allocates nothing (NUM-14).
- A Set By Caller number says what it checks, in its summary, as "the caller's Damage, else 0", and in its tooltips: a
  value set under exactly its key, on the effect's spec, an activation's data or the data that spawned a process,
  and its Default where none is. Its behaviour is unchanged (NUM-4).
- `Tag.GetName()` returns the tag's display name, as `ToString()` does. It returned `Name`, which a unique tag pads
  to tell it apart; code that wants that reads `Name` (ED-67).
- `ScalerMagnitudeOperation` is `ScalableFloat` and `ScalerIntegerMagnitudeOperation` is `ScalableInt`, with the same
  saved fields, so every saved magnitude loads as it was. The old names stay this version as obsolete subclasses, so
  code that names them compiles with a warning; they go in the next (NUM-1).
- `SetByCallerScaler` is a number kind, and reads the caller's magnitudes from any context that holds them: an
  activation's data as a task reads it, the data that spawned a process, an effect's cue and workers. It read only an
  effect spec's, so anything else read its default (NUM-4).
- A gameplay mono process's own attributes read through the process's context, so a Set By Caller reads its caller's
  magnitude, and a read allocates nothing; the carrier they were read through was made on every read and reported
  the process's source as its target (NUM-4).
- The kind menu names Attribute Backed "× Attribute", which the menu starts as the number times the attribute, and
  files the scalers of every kind under Scaler, Effect Stacks under Effect and Item Stacks under Item. A kind with no
  values per level shows its summary under its row in place of its name beside it (NUM-5).
- The synced sequence examples are in Code Examples, with the rest; they shipped with the Demos (SP-26).
- A Mono Process's inspector is a Process section of Unity's fields; each choice says what it does in its tooltip,
  and Priority Method shows only while the process steps, Priority only for Manual. It drew a tinted box and gray
  hints (ED-61).
- Set Template... is Unity's searchable dropdown of the other assets of the type, leaving out those that take the
  asset as their template. It was the Asset Browser window, which is gone (ED-61).
- New Linked Effect... names the effect in The Forge's New Asset window and links it. The Create Linked Effect
  dialog is gone, as are the unused Type Picker window, `TypePickerField` and `TypeRef` (ED-61).
- A requirement group of its own, as a targeting task's, is the requirements table of one row, whose cells open its
  list with its name and template under it. It was a tinted box with colored badges (ED-66).
- A tag query is its tag, its match, and its count's comparison and number on one row; a threshold is its level, value
  and tier on one row; a scaler group's member is its operation, then its scaler. Each was drawn with colors of its
  own (ED-66).
- A level source is Unity's object field of level providers, with a line of the levels it gives. Its browser window
  is gone: Unity's picker lists the providers (ED-66).
- An ability's behaviour, drawn whole in a component of your own, is a foldout of the fields the ability inspector
  shows (ED-66).
- A Workers section's import lists every asset with a worker group, whatever its type, as the group's own import
  did before ED-57 took it (ED-66).
- A worker group is its name and its four lists of workers, Unity's reorderable lists, where a new worker starts
  empty. It was a box of its own with colored badges and an Import of its own, inside a section that already
  imports it (ED-57).
- A sequence reference is Unity's popup of the project's sequences, None first, with a method that's gone marked
  missing. It was an IMGUI popup (ED-57).
- An ability's stage folds as an attribute set element does, to a line of what it holds, with Critical on its row
  while a task commits the ability to it. Its tasks are Unity's reorderable list, where a new task starts empty, and
  its condition shows where it looks for its tag only once it names one. The stages' list removes a stage, and each
  stage keeps its fold as one is removed or dragged. It drew colored badges, arrows as text, and a list of its own
  whose new task shared the last one's object (ED-64).
- A stale asset tag whose path is right says it has no display name, or which one it has, where it named one path
  twice (FEAT-25).
- A field that holds an object of some kind, as a task, a cue, a worker or a scaler behaviour, is Unity's field with
  the kind's name in its box, which opens Unity's searchable dropdown of the kinds; the kind's fields indent under
  it, and a kind that folds shows a line of its values. It was a button that opened a list in tinted boxes, with an
  orange border and colored badges, and a list's elements could show each other's fields (ED-56).
- In The Forge, an asset header's actions share one row, with the file name in a row of its own under them (ED-65).
- A spread scaler's points are numbered, and each real level shows what it reads, as the runtime reads it. Its table
  named each point by a level such as 1.8, and never said what a real level reads (ED-62).
- Quick Fill is a dialog in the style, with a preview as the scaler's table shows it. It adds Ease In and Out, a
  Custom Curve, a strength for its bending patterns, rounding to whole numbers or a step, and a count for a scaler
  with levels or points of its own. Apply is one Undo step (ED-63).
- An attribute set element shows what its attribute starts at, at each level, worked out as the runtime works it
  out, with a note when another element's value is used and a warning when a scaler reads an attribute the set
  doesn't give. Base and Current are magnitude fields, as an effect's are, both shown, and Bounds, Constraints and
  a value's Current and Base are Unity's fields. The Attribute Set inspector's Preview In works a set's values out
  as part of a set that holds it, and All Attributes shows the same values. An element showed a formula, Current
  and Base one at a time on a switch, and the table read Scaled (ED-49).
- A tag field is Unity's field: its label in the label column, the tag's parents in secondary text and its leaf in
  strong text. It opens Unity's searchable dropdown of the tags its context offers, with None and New Tag…, and its
  right-click menu clears it, makes a new tag, makes its tag a child of another, or copies its path. It was a button
  with a colored border, text that changed as the pointer passed, and a picker drawn under it (ED-55).
- A scaler's Stretch Over Levels is its Values: Per Level or Spread Over Levels, side by side. Spread, each value says
  the level it sits at, and the curve beside the table is the curve the scaler reads (ED-54).
- The Forge's list shows each asset's name alone, and the selected asset's header shows its file name under Show in
  Project. The Show File Names preference is gone (ED-58).
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
- A stage cut short as its run is interrupted no longer says it succeeded, so a stage that applies usage effects as it
  ends doesn't pay them for a broken channel (TS-AE).
- Updating an asset's tag, from the Problems window or Identity, carries the new tag to every asset that stored the old
  one, as an Asset Part that finds the asset by it; it changed the asset's tag alone, and what named it broke (ED-84).
- An impact worker's context names its holder, the hit's dealer, as its System; it named the one hit (GAS-72).
- An ability's effects with no delay between them land in one frame; a delay of 0 waited a frame (GAS-74).
- Auto Target and Auto Target by Affinity find runtime entities, with no range and within one; they found MonoBehaviour
  entities alone (GAS-75).
- Two analysis workers of one type on an entity each keep their own "has fired"; they shared one (GAS-76).
- An asset's icon is the texture under its Primary tag, else the first item that has a texture; it was the first item's,
  with no texture as it might be (ED-69).
- An OnApply impact on a durational effect lands as the effect does, where the effect doesn't tick on application; it
  never landed, and only the effect's ticks dealt (GAS-71).
- A Group member's kind menu offers a number's kinds alone; it offered an attribute set's cached kinds beside them,
  By Level and Shared each twice. A number inside a scaler, as a Conditional's, takes its own kinds too (ED-81).
- A cached Group, an attribute set element's, set to ignore its overriding members, no longer throws as its element is
  worked out; it sets them aside, and the others join as they would (ED-80).
- An effect's Stack Behaviours can be added and edited in the inspector: each picks its kind, Stack Bounds, Scaled
  Stack Amount or Starting Stacks, and shows its fields, with tooltips. They had no drawer, so a new behaviour had no
  kind to pick (ED-75).
- A Scaled Stack Amount or Starting Stacks behaviour that rounds to the nearest no longer throws as its effect stacks;
  it rounds a half away from zero (ED-75).
- A tag is shown by its display name wherever it is read: an effect's and an item's summaries in the Forge, the
  import window's tag column, a tag query's and a tag event's text, the tag cache's log and a process's role. Each
  showed the tag's stored name, which is padded for a unique tag (ED-67).
- A Local Data row's key can be pressed. The key was drawn under a label as wide as an inspector's label column, in a
  column narrower than that, which left the tag's box 8 pixels wide. The key, and a value drawn by its own drawer, as
  a tag's, have no label (ED-68).
- An effect's Stack Behaviours save. Unity dropped the list whole as the asset saved, so a stack bound, its overflow
  effects or a stack amount rule set in the inspector was gone, and the inspector's field read nothing (GAS-70).
- A Conditional scaler's Target Attribute Threshold reads the attribute on the effect's target. It read the
  source's, as Source Attribute Threshold does (GAS-69).
- The effects a spawned process applies, as a projectile's as it lands, take the set-by-caller magnitudes of the data
  that spawned it, as an ability's other effects do. They took none (GAS-68).
- Create Mono Process's Spawn At places what it spawns at the caster or at the ability's target. It appeared where
  its prefab stood unless the ability's data held a Position (GAS-67).
- Click targeting takes the target or point its activation's data holds, as an AI's or a replay's does, with no
  click, so the ability an AI casts can be the one a player clicks; Take Target From Data turns it off. A handed
  target out of range fails at once. They waited for a click whatever the data held (GAS-66).
- A process Create Mono Process spawns takes the ability as its origin, so its effects are the caster's at the
  ability's level. It took GameRoot. And the task spawns its own processes where the data holds none; it spawned
  them only with Internal Is Override on (GAS-65).
- A scaler spread over its provider's levels up to a level read its last value at every level above the first: it
  ended its range at the level being read. It ends at the lower of its Max Level and the provider's last level
  (GAS-64).
- Changing how many values a scaler spreads over its levels, after a change to it was undone, could crash the
  editor: its field wrote through a property it kept from before the Undo (ED-49).
- A reference field inside a list's element, as the attribute a scaler reads in an attribute set element, had no
  label. A magnitude field whose kind reads no values of its own, as one that reads an attribute, showed a range
  from values it never reads (ED-49).
- An effect's additional impact named no delivery on its row and didn't say what it commits, and its Retention Group
  drew as a bare bar with no label. Its row names its delivery, a periodic one says what it commits, and its Retention
  Group has its label (ED-50).
- A scaler showed parts its kind never reads and hid parts it does: a Fixed Value showed a table of values per level,
  and a Randomized scaler hid the values its base value comes from. A scaler shows what its kind reads, its settings
  on a shaded block under its field, and a kind's field that applies to some of its settings shows only for them. A
  scaler of your own draws every field it adds, its own abstract bases' among them, and none marked Hide in
  Inspector. A range below 0 reads "-25 to -10" (ED-51).
- The menu beside a scaler field opened as narrow as its button, so none of its items could be read. It opens as wide
  as its items (ED-52).
- A scaler's level count ignored its asset's level provider. Per Level, a scaler follows the provider's levels, with a
  value for each, unless it takes levels of its own, and Quick Fill fills the levels it shows (ED-53).
- Tags showed their internal names. A tag the tag picker makes is unique, and pads its name with a generated
  suffix; The Forge, the Debugger and the inspectors showed that name. Each shows a tag by its display name now, and
  the Debugger's Tags view keeps a unique tag under its parents (ED-60).
- The Forge's tag view couldn't sort. Its Tag, For and Uses columns sort, and each list's header shows its sort again
  after a domain reload (ED-59).
- The Timeline added each new record at the bottom. The newest track is at the top, and a track's runs and
  applications list the latest first (ED-46).
- Selecting an effect in the Timeline hid part of its strip: the application its detail shows was drawn in the
  selection's color, on a row the selection colors. It's taller than the other spans now, in strong text (ED-47).
- The Debugger's Entities view lost its list after another view: its column collapsed against the window's edge
  until its splitter was dragged. The Tags view's tree never showed at all. Each view keeps its splits' sizes
  through the other views, and through Play mode's end and start (ED-45).
- A table's column widths weren't remembered: a switch of view in the Debugger or The Forge, entering Play mode or
  closing the window put them back. A width a column is dragged to stays, in the Debugger, The Forge and the
  Problems view, and Preferences → PlayForge → Use Defaults forgets it (ED-48).
- An ability made in code had no stage list until it was saved and loaded, so adding a stage to one threw. It starts
  with an empty one (GAS-63).
- A template's push searched the project again for each child's own children, so a push to 2,000 children took 28 s.
  It finds the family in one search, and a push costs the same a child however big the family (ED-44).
- Ending a process searched its step group from the front, so ending the newest of many cost as much as every
  process stepped with it, and ending 10,000 cost the square of that. A step group keeps each process's position,
  so an ending costs the same however many there are (PC-34).
- An area ability struck one entity. A targeting task kept only the last target it selected, the farthest of every
  match, and Target Real and the apply tasks took only the first. Auto targeting keeps every target it selects, in
  order, and Apply Effects and Apply/Remove Effects reach each of them (GAS-62).
- A restore gave each ability back as a new one, so another entity's effect that one of them applied lost track of
  it, and its next save named no slot for it. A restore keeps each ability in the slot its save gives it (GAS-60).
- A save kept an effect's timers as floats, so a restored effect ran on a hair apart from one never restored. It keeps
  them in full; a save written with floats still loads (GAS-61).
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
- `ProjectAttributes`, `ForgeAttributes` and an ability's Mana Cost (TW-28): their fixed Health, Mana, Luck and Movement
  Speed slots became the Forge Project's roles, and Mana Cost became attribute costs (TW-29).
- `IValidationReady`, which nothing took; `IAttribute`, `ITarget`, `BasicEffectOrigin` and `ProcessDataPacket` no
  longer implement it (FEAT-36).
- The generic `IValidationRule<T>`; `IAbilityValidationRule` declares its own `Validate` (FEAT-36).
- `IAttributeHandler`, which nothing took (FEAT-36).
- `IGameplayConfigUser`, which nothing took (FEAT-36).
- `ILockedTagProvider`, which nothing read; the locked-tag attributes stay (FEAT-36).
- `Utils/RuntimeProxy.cs`, whose code was commented out (FEAT-36).
- The Demos sample, the Demo scene and Swarm Defender, which the Demo sample replaces (SP-26, Decision 37).
- `AbstractRefDrawer`, the old type picker, which only `AbstractTypeRefDrawer` used. A drawer of your own that
  derives from `AbstractTypeRefDrawer` or `AbstractGenericDrawer` keeps `GetStringValue`, `FilterType`,
  `CleanTypeName`, `GetEntries`, `AcceptClear`, `SetValue` and `OnTypeChanged`; `AcceptOpen` and `AcceptAdd` are
  gone (ED-56).
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
