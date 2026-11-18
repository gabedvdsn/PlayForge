# PlayForge — Cues

A **cue** is what something shows and sounds, apart from what it changes: a spark as a hit lands, a hum while a shield
holds, a shake as a giant falls. Effects, abilities, entities and items each list their cues, and a game's own code
raises one where it likes. Every cue plays through `ForgeCues`, after Unreal's gameplay cues: a **burst** at a moment,
as a hit's spark, or a **lasting** cue's beginning and its end, as a shield's hum, which share a key.

## Where cues are raised

- **Effects**: an effect's **Cues** component plays its cues as it lands, as an application it takes in lands, as each
  tick executes, as an impact it deals lands, as it comes off, while it lasts, as an application is refused and as
  its stacks change ([EFFECTS.md](EFFECTS.md#cues)).
- **Abilities**: a **Cues** component plays an ability's as it activates, as it commits, while it's active, as it ends
  and as one is refused; a **Play Cue** task plays them at a stage's exact moment, on the caster, on the target or at
  the point ([ABILITIES.md](ABILITIES.md#cues)).
- **Entities**: a **Cues** component plays an entity's as it spawns, dies and comes back, and lasting ones while it
  holds a tag, as stars over its head while it's Stunned ([ENTITIES.md](ENTITIES.md#cues)).
- **Items**: a **Cues** component plays an item's as it's equipped, while it's equipped and as it's used
  ([ITEMS.md](ITEMS.md#cues)).
- **Code**: `ForgeCues.Burst(sparks, new CueContext(entity, ECueMoment.Raised, point: hit.point))` plays sparks where a
  ray hit, and `ForgeCues.Begin` and `ForgeCues.End` begin and end a lasting one.

A cue's context, `CueContext`, says what raised it, its `Moment`, as Applied, Activated or Died; its target and its
source; the effect or the ability behind it; where it plays, its `Point` and `Normal`, or else its target's place; a
magnitude; and, for a lasting cue, the key its beginning and its end share.

## Handlers

Each cue in a list is a handler, its kind picked from the list. It plays at each moment whose list holds it, and, in a
list of lasting cues, as a While Active, from its beginning to its end:

- **Camera Shake** shakes the camera through the shaker the game gives `ForgeCues.Shaker`, an `ICameraShaker`.
- **Sound** plays its clip where its Placement says ([Placement](#placement)).
- **Prefab** places its prefab where its Placement says, for its **Lifetime**, or, as a lasting cue, until its cue
  ends ([Pooling](#pooling)).
- **Floating Text** floats a text where its Placement says, as a hit's damage, the gold a kill gave or a status's
  name ([Floating text](#floating-text)).
- **Process** runs a MonoBehaviour process where its Placement says, on a packet loaded for the cue, with its **Data**
  list last ([Data packets](#data-packets)): a burst's runs its own course, and a lasting one's runs until its cue
  ends ([Pooling](#pooling)).
- **Cue Tag** plays what its tag routes to ([Routes](#routes)).

Write a handler of your own by deriving from `AbstractCueHandler`: override the moments it plays at, or `Burst` to play
every burst one way, reading the moment from `cue.Moment`, and `Begin` and `End` for a lasting one.

## Placement

A cue that places something, a Prefab or a Sound, says where with its **Placement**:

- **At**: on whom.
  - **Self**: the one the cue is on: an effect's target, an ability's caster, the entity, or an item's holder. Where
    its raiser gave a point, as a hit's, it's there.
  - **Source**: who's behind it, as an effect's source or a death's killer.
  - **Target**: an ability's first target, or the place it targeted. For another's cue, the one it's on.
  - **Each Target**: one at each of an ability's targets.
  - **Point**: the cue's point, or the place an ability targeted.
- **Socket**: a child of theirs it's placed at, by name, found at any depth, as a hand or a weapon's tip.
- **Offset** moves it, and **Scatter** moves it by a random amount up to that much either way on each axis, both in
  the space of what it's placed at.
- **Attach**: whether it follows them: a lasting cue's only, by default; always; or never. An attached sound plays
  from a source of its own under them.
- **Facing**: as what it's placed at faces, or along the cue's normal at a point; along the cue's normal; away from
  or toward its source; or the world's way.

Scatter draws from `ForgeCues.Random`, the cues' own random numbers, so a cue never moves the game's rolls: a server
that plays no cues and a client that plays them must draw alike.

## Floating text

A **Floating Text** cue floats a text where its Placement says, 2 units above the one the cue is on by default: a
hit's damage, the gold a kill gave, a status's name as it lands.

- **Text**: what it says. `{value}` is its number, `{name}` the name of what raised it, as an effect's, and `{tag}` its
  cue tag: `+{value} gold`, or `{name}` for a status.
- **Value**: its number, **From** the cue's magnitude, as a hit's value from an effect's **On Hit**, or its
  **Number**, read as it plays, as a Tracked Impact's for the gold an instant effect gave. It's written with up to
  **Decimals** decimals; without its sign where **Absolute** says, as damage shown as 35; with a **Plus Sign** above
  0; shortened past a thousand where **Abbreviate** says, as 1.2k; and not at all for 0 where **Hide Zero** says.
- **Look**: its **Color**, or its **Negative Color** below 0 where **By Sign** says; its **Size**, **Font**,
  **Bold** and **Outline**; turned to the camera where **Face Camera** says, `ForgeCues.Camera` or else the main
  camera; and its **Sorting Order**. A **Prefab** holding a TextMeshPro text shows it in place of the cue's own,
  its font, size and outline its own.
- **Motion**: its **Lifetime**; its **Velocity**, and a **Random Velocity** either way on each axis; **Gravity**, for
  an arc, and **Drag**; its opacity over its life, **Fade**, and its scale, **Scale**, as a pop; on the game's clock
  where **Game Time** says, else in real time.
- **Stacking**, on one entity: a number **Merge Within** some seconds of the last adds to it, which shows the sum
  and floats again, as a burn's ticks; each text floats a **Stagger** above the one below it, in the lowest place
  free; and at most **Max Shown** float at once, the oldest going first.

Its texts come from `ForgeCuePool` and go back to it. For damage numbers, put one in an effect's **On Hit**: it plays
as each impact the effect deals lands, on the one it lands on, told the value it changed.

## Routes

A **Cue Tag** cue names a tag, as Cue.Hit.Fire, in place of what it shows, so an effect, an ability and an entity can
each raise Cue.Hit.Fire and one place says how it looks. That place is the project's **Cue Library**, an asset from
Create > PlayForge > Cue Library, which Project Settings > PlayForge names under Forge Project. Each of its routes is a
cue tag and the handlers it plays.

- A tag plays its own route, or else its nearest parent's: with no route for Cue.Hit.Fire, Cue.Hit's plays.
- A route that doesn't **Override** plays its parent's after it, so Cue.Hit.Fire's flames add to Cue.Hit's spark.
- An entity's **Cue Receiver** component plays a tag its own way first, in place of the library's, or before it with
  **Pass Through**. Its **Variant** plays the library's route under its name, as Cue.Hit.Golem for Cue.Hit, where the
  library has one, so a golem's hit sounds of stone.
- Code can give a library of its own, `ForgeCueRoutes.Library`, in place of the project's.

## Raised

The Cue Library's inspector lists under **Raised** each cue tag the project's assets raise through it, by a Cue Tag
cue, in tag order. Each tag shows what plays it, "plays its own route", "plays Cue.Hit's route" or "plays nowhere",
then a row for each asset that raises it, which opens the asset in The Forge. The section's header counts the tags,
and how many of them play nowhere. It follows the project as it changes, and the library's routes as they're edited.

The Problems view notes a Cue Tag cue that plays nowhere:

- one that names no tag;
- one whose project names no Cue Library;
- one whose tag the library routes nowhere, neither its own route nor a parent's.

It's a note, not a warning, as an entity's Cue Receiver may play the tag its own way. An edit to the library, or to
the library a project names, checks every asset again.

## Pooling

A Prefab cue with **Pooled** takes its objects from `ForgeCuePool` and gives them back as its cue ends, a burst's after
its Lifetime, in place of making and destroying one each time; the pool makes **Preallocate** of them ahead as the cue
first plays. An `IPooledCue` on the prefab is told as it's taken and as it's given back, to reset it.

A Process cue with **Pooled** does the same through `CuePoolInstantiator`, the instantiator its process's registration
gives: each object comes from the pool, placed where the cue's packet says, and goes back as its process ends.

## Data packets

A cue that runs a process or a sequence hands it a data packet of its own, loaded for what raised the cue,
`CuePackets.For(cue, data)`. It loads in four steps, each step's keys taking the place of an earlier step's:

1. **An ability's activation:** an ability's cue starts from a copy of its activation's packet, with its targets, its
   targeting data and the numbers its caller set.
2. **The cue's context:**
   - the cue itself under `Tags.CUE`, a `CueContext`;
   - its asset under `Tags.ASSET`, the effect, the ability, the item or the entity's identity, and its spec under
     `Tags.SPEC`, the effect's, the ability's or the item's;
   - its source under `Tags.SOURCE`, and the one it plays on under `Tags.TARGET_REAL` where the activation named no
     target;
   - where it plays under `Tags.POSITION` and `Tags.ROTATION`, and what it follows under `Tags.TRANSFORM`;
   - its owner's clock: the activation's for an ability's cue, else that of the one it plays on.
3. **The asset's Local Data:** each row under its key, its numbers read for the cue, as at its level.
4. **The cue's own Data list,** last, so it has the last word. A row with no value puts nothing.

A process or a sequence reads it as it reads any packet: `data.TryGetFirst(Tags.CUE, out CueContext cue)`.

## Raises and sinks

Each raise of a list of cues names what the list belongs to, its `CueSource`: an effect, an ability, an entity or an
item, by its asset; a cue tag raised from code, as `ForgeCueRoutes.Burst(cue)`; or a game's own code, where it names
nothing. Code can name one: `ForgeCues.Burst(cues, cue, CueSource.Of(effect))`.

Before any cue of a raise plays, its world's sink is asked, once: `ForgeCueSink.WillPlay(event, raise)`, the raise
holding the cue and its source. The default plays everything. A world of its own takes a sink of its own with
`ForgeCues.UseSink(world, sink)`, and `ForgeCues.Sink` is the one every other world asks. It's asked whatever mutes a
cue, so a server that plays none can still send them on, as a network layer's sink does. A cue tag raised from code
asks once; its routes, and a Cue Tag within them, play inside that raise, unasked.

An effect's applier can say where it hit, before applying it: `spec.WithHit(point, normal, magnitude)`. Its cues then
play at the point, facing the normal, with the magnitude, as a sword's spark where the blade struck.

## Silencing and listening

- `ForgeCues.Enabled = false` silences every cue, as on a server or while a simulation runs ahead.
- `ForgeCues.Mute(entity, true)` silences one entity's, as one off screen. A lasting cue that began still ends.
- `ForgeCues.Played` hears each cue as it plays, for a debugger or a recorder.
