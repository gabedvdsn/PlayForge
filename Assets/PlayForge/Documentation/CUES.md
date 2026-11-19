# PlayForge — Cues

A **cue** is what something shows and sounds, apart from what it changes: a spark as a hit lands, a hum while a shield
holds, a shake as a giant falls.

Effects, abilities, entities and items each list their cues, and a game's own code raises one where it likes. Every
cue plays through `ForgeCues`, after Unreal's gameplay cues.

A cue plays in one of two ways:

- a **burst** at a moment, as a hit's spark;
- a **lasting** cue's beginning and its end, which share a key, as a shield's hum.

## Where cues are raised

Effects, abilities, entities and items raise their cues through a **Cues** component, and code raises them through
`ForgeCues`.

- **Effects**: an effect's **Cues** component plays its cues ([EFFECTS.md](EFFECTS.md#cues)):
  - as it lands, and as an application it takes in lands;
  - as each tick executes, and as an impact it deals lands;
  - as it comes off, and while it lasts;
  - as an application is refused, and as its stacks change.
- **Abilities**: a **Cues** component plays an ability's as it activates, as it commits, while it's active, as it ends
  and as one is refused ([ABILITIES.md](ABILITIES.md#cues)).
  - A **Play Cue** task plays them at a stage's exact moment, on the caster, on the target or at the point.
- **Entities**: a **Cues** component plays an entity's as it spawns, dies and comes back
  ([ENTITIES.md](ENTITIES.md#cues)).
  - It plays lasting ones while the entity holds a tag, as stars over its head while it's Stunned.
- **Items**: a **Cues** component plays an item's as it's equipped, while it's equipped and as it's used
  ([ITEMS.md](ITEMS.md#cues)).
- **Code**: `ForgeCues.Burst(sparks, new CueContext(entity, ECueMoment.Raised, point: hit.point))` plays sparks where a
  ray hit.
  - `ForgeCues.Begin` and `ForgeCues.End` begin and end a lasting one.

A cue's context, `CueContext`, says what raised it, and where and for whom it plays:

| Member | What it holds |
|---|---|
| `Moment` | What raised it, as Applied, Activated or Died. |
| `Target` and `Source` | Its target, the entity it plays on, and its source, who's behind it. |
| `Spec` and `Origin` | The effect or the ability behind it. |
| `Point` and `Normal` | Where it plays, and which way it faces. Without a point, it plays at its target's place. |
| `Magnitude` | A magnitude its raiser gives it, as a hit's damage. |
| `Key` | For a lasting cue, the key its beginning and its end share. |

## Handlers

Each cue in a list is a handler, its kind picked from the list.

A handler plays at each moment whose list holds it. In a list of lasting cues, as a While Active, it plays from its
beginning to its end.

PlayForge ships the handlers below, and you can write your own.

### Camera Shake cue

A **Camera Shake** cue shakes the camera as its moment comes.

| Property | Description |
|---|---|
| **Strength** | How hard it shakes. |
| **Duration** | How long the shake lasts, in seconds. |

- It shakes through the shaker the game gives `ForgeCues.Shaker`, an `ICameraShaker`. With none, it shakes nothing.
- Its Strength and Duration are read for what raised it.

### Sound cue

A **Sound** cue plays its clip where its Placement says ([Placement](#placement)).

| Property | Description |
|---|---|
| **Clip** | The clip it plays. |
| **Placement** | Where it plays, and on whom. |
| **Volume** | How loud it plays, from 0 to 1. |

An attached sound plays from a source of its own under them, and follows them as it plays.

### Prefab cue

A **Prefab** cue places its prefab where its Placement says.

| Property | Description |
|---|---|
| **Prefab** | The prefab it places. |
| **Placement** | Where it's placed, on whom, and which way it faces ([Placement](#placement)). |
| **Lifetime** | How long a burst's prefab stays, in seconds. 0 keeps it. |
| **Pooled** | Takes its objects from a pool and gives them back, in place of making and destroying one each time. |
| **Preallocate** | How many objects its pool makes ahead, as the cue first plays. |

- A burst's prefab stays for its **Lifetime**.
- As a lasting cue, its prefab stays until its cue ends ([Pooling](#pooling)).

### Floating Text cue

A **Floating Text** cue floats a text where its Placement says, as a hit's damage, the gold a kill gave or a status's
name.

| Property | Description |
|---|---|
| **Text** | What it says. |
| **Value** | Its number, and how it's written. |
| **Placement** | Where it floats: 2 units above the one the cue is on, by default. |
| **Look** | How it looks. |
| **Motion** | How it moves and fades. |
| **Stacking** | How its texts on one entity merge, stagger, and how many show. |

[Floating text](#floating-text) describes each field.

- Every burst floats it, whatever raised it: the list it's in says when.
- A lasting cue floats it as it begins, as a status's name as it lands.

### Process cue

A **Process** cue runs a MonoBehaviour process where its Placement says, on a packet loaded for the cue.

| Property | Description |
|---|---|
| **Process** | The process it runs: a prefab with a MonoBehaviour process on it. |
| **Placement** | Where its process runs ([Placement](#placement)). |
| **Pooled** | Takes its objects from the cue pool, and gives each back as its process ends ([Pooling](#pooling)). |
| **Preallocate** | How many objects its pool makes ahead, as the cue first plays. |
| **Data** | Data its process runs with, loaded last ([Data packets](#data-packets)). |

- A burst's process runs its own course.
- A lasting one's runs until its cue ends ([Pooling](#pooling)).

### Sequence cue

A **Sequence** cue runs a task sequence on a packet loaded for the cue, with its **Data** list last.

| Property | Description |
|---|---|
| **Sequence** | The task sequence it runs, one a `[TaskSequenceMethod]` makes. |
| **Clock** | The clock its sequence runs by (below). |
| **Data** | Data its sequence runs with, loaded last ([Data packets](#data-packets)). |

- A burst's sequence runs its course, and a lasting one's runs until its cue ends.
- It goes by the clock its **Clock** picks, else the sequence's own, else that of the one it plays on.

### Cue Tag cue

A **Cue Tag** cue plays what its tag routes to ([Routes](#routes)).

| Property | Description |
|---|---|
| **Tag** | The cue tag whose route plays. |

Each burst, beginning and end it hears plays the tag's route.

### Writing your own handler

Write a handler of your own by deriving from `AbstractCueHandler`. Each of its hooks does nothing until you override
it.

- Override the moments it plays at: `OnApplied`, `OnTicked` and `OnRemoved`.
- Or override `Burst` to play every burst one way, reading the moment from `cue.Moment`.
- Override `Begin` and `End` for a lasting one.

`Burst` plays `OnTicked` for an effect's tick, `OnRemoved` for its removal, and `OnApplied` for any other moment.

## Placement

A cue that places something, a Prefab or a Sound, says where with its **Placement**.

| Property | Description |
|---|---|
| **At** | On whom it's placed (below). |
| **Socket** | A child of theirs it's placed at, by name, found at any depth, as a hand or a weapon's tip. |
| **Offset** | Moves it, in the space of what it's placed at. |
| **Scatter** | Moves it by a random amount, up to that much either way on each axis, in the same space. |
| **Attach** | Whether it follows them: a lasting cue's only, by default; always; or never. |
| **Facing** | Which way it faces (below). |

**At** has five options:

| At | Where it places it |
|---|---|
| **Self** | On the one the cue is on: an effect's target, an ability's caster, the entity, or an item's holder. Where its raiser gave a point, as a hit's, it's there. |
| **Source** | On who's behind it, as an effect's source or a death's killer. |
| **Target** | On an ability's first target, or the place it targeted. For another's cue, on the one it's on. |
| **Each Target** | One at each of an ability's targets. |
| **Point** | At the cue's point, or the place an ability targeted. |

**Facing** has five options too:

- as what it's placed at faces, or along the cue's normal at a point;
- along the cue's normal;
- away from its source;
- toward its source;
- the world's way.

An attached sound plays from a source of its own under them.

Scatter draws from `ForgeCues.Random`, the cues' own random numbers. So a cue never moves the game's rolls: a server
that plays no cues and a client that plays them must draw alike.

## Floating text

A **Floating Text** cue floats a text where its Placement says, 2 units above the one the cue is on by default. It
shows a hit's damage, the gold a kill gave, or a status's name as it lands.

| Property | Description |
|---|---|
| **Text** | What it says. `{value}` is its number, `{name}` the name of what raised it, as an effect's, and `{tag}` its cue tag. |
| **Value** | Its number, and how it's written. |
| **Look** | How it looks. |
| **Motion** | How it moves and fades. |
| **Stacking** | How its texts on one entity merge and stagger, and how many show. |

Its Text can be `+{value} gold`, for example, or `{name}` for a status.

Its **Value** gives its number and writes it:

| Property | Description |
|---|---|
| **From** | Where its number comes from: the cue's magnitude, as a hit's value from an effect's **On Hit**, or its **Number**. |
| **Number** | Its number, read as it plays, as a Tracked Impact's for the gold an instant effect gave. |
| **Decimals** | The most decimals it's written with. |
| **Absolute** | Writes it without its sign, as damage shown as 35. |
| **Plus Sign** | Writes a plus before a number above 0. |
| **Abbreviate** | Shortens it past a thousand, as 1.2k. |
| **Hide Zero** | Writes nothing for a number of 0. |

Its **Look** sets how it shows:

| Property | Description |
|---|---|
| **Color** | Its colour. |
| **By Sign** | Colours it by its number's sign: its Color for 0 and above, its **Negative Color** below 0. |
| **Negative Color** | Its colour below 0, where By Sign is on. |
| **Size**, **Font** and **Bold** | Its size and font, and whether it's written in bold. |
| **Outline** | Its outline. |
| **Face Camera** | Turns it to the camera: `ForgeCues.Camera`, or else the main camera. |
| **Sorting Order** | The order it draws in among others. |
| **Prefab** | A prefab holding a TextMeshPro text, which shows it in place of the cue's own. Its font, size and outline are the prefab's own. |

Its **Motion** sets how it moves:

| Property | Description |
|---|---|
| **Lifetime** | How long it floats. |
| **Velocity** | How it moves. |
| **Random Velocity** | A random amount of velocity, either way on each axis. |
| **Gravity** and **Drag** | Gravity for an arc, and drag to slow it. |
| **Fade** | Its opacity over its life. |
| **Scale** | Its scale over its life, as a pop. |
| **Game Time** | On, it floats on the game's clock. Off, in real time. |

Its **Stacking** sets how its texts on one entity share the space:

| Property | Description |
|---|---|
| **Merge Within** | A number within this many seconds of the last adds to it, which shows the sum and floats again, as a burn's ticks. |
| **Stagger** | Each text floats this much above the one below it, in the lowest place free. |
| **Max Shown** | At most this many float at once, the oldest going first. |

Its texts come from `ForgeCuePool` and go back to it.

> [!TIP]
> For damage numbers, put one in an effect's **On Hit**. It plays as each impact the effect deals lands, on the one it
> lands on, told the value it changed.

## Routes

A **Cue Tag** cue names a tag, as Cue.Hit.Fire, in place of what it shows. So an effect, an ability and an entity can
each raise Cue.Hit.Fire, and one place says how it looks.

That place is the project's **Cue Library**, an asset from Create > PlayForge > Cue Library. Project Settings >
PlayForge names it under Forge Project.

Each of the library's routes is a cue tag and the handlers it plays:

- A tag plays its own route, or else its nearest parent's. With no route for Cue.Hit.Fire, Cue.Hit's plays.
- A route that doesn't **Override** plays its parent's after it, so Cue.Hit.Fire's flames add to Cue.Hit's spark.
- Code can give a library of its own, `ForgeCueRoutes.Library`, in place of the project's.

An entity's **Cue Receiver** component plays a tag its own way first, in place of the library's.

- With **Pass Through**, it plays its own way before the library's route, not in place of it.
- Its **Variant** plays the library's route under its name, as Cue.Hit.Golem for Cue.Hit, where the library has one.
  So a golem's hit sounds of stone.

## Raised

The Cue Library's inspector lists under **Raised** each cue tag the project's assets raise through it, by a Cue Tag
cue, in tag order.

- Each tag shows what plays it: "plays its own route", "plays Cue.Hit's route" or "plays nowhere".
- Under it, a row for each asset that raises it opens the asset in The Forge.
- The section's header counts the tags, and how many of them play nowhere.
- It follows the project as it changes, and the library's routes as they're edited.

The Problems view notes a Cue Tag cue that plays nowhere:

- one that names no tag;
- one whose project names no Cue Library;
- one whose tag the library routes nowhere, neither its own route nor a parent's.

It's a note, not a warning, as an entity's Cue Receiver may play the tag its own way. An edit to the library, or to the
library a project names, checks every asset again.

## Pooling

A Prefab cue with **Pooled** takes its objects from `ForgeCuePool` and gives them back, in place of making and
destroying one each time.

- It gives an object back as its cue ends, or a burst's after its Lifetime.
- The pool makes **Preallocate** of them ahead, as the cue first plays.
- An `IPooledCue` on the prefab is told as it's taken and as it's given back, to reset it.

A Process cue with **Pooled** does the same through `CuePoolInstantiator`, the instantiator its process's
registration gives. Each object comes from the pool, placed where the cue's packet says, and goes back as its process
ends.

## Data packets

A cue that runs a process or a sequence hands it a data packet of its own, loaded for what raised the cue:
`CuePackets.For(cue, data)`.

The packet loads in four steps. Each step's keys take the place of an earlier step's:

1. **An ability's activation:** an ability's cue starts from a copy of its activation's packet, with its targets, its
   targeting data and the numbers its caller set.
2. **The cue's context:**
   - the cue itself under `Tags.CUE`, a `CueContext`;
   - its asset under `Tags.ASSET`: the effect, the ability, the item, or the entity's identity;
   - its spec under `Tags.SPEC`: the effect's, the ability's or the item's;
   - its source under `Tags.SOURCE`;
   - the one it plays on under `Tags.TARGET_REAL`, where the activation named no target;
   - where it plays under `Tags.POSITION` and `Tags.ROTATION`, and what it follows under `Tags.TRANSFORM`;
   - its owner's clock: the activation's for an ability's cue, else that of the one it plays on.
3. **The asset's Local Data:** each row under its key, its numbers read for the cue, as at its level.
4. **The cue's own Data list,** last, so it has the last word. A row with no value puts nothing.

A process or a sequence reads it as it reads any packet: `data.TryGetFirst(Tags.CUE, out CueContext cue)`.

## Raises and sinks

Each raise of a list of cues names what the list belongs to, its `CueSource`:

- an effect, an ability, an entity or an item, by its asset;
- a cue tag raised from code, as `ForgeCueRoutes.Burst(cue)`;
- or a game's own code, where it names nothing.

Code can name one: `ForgeCues.Burst(cues, cue, CueSource.Of(effect))`.

Before any cue of a raise plays, its world's sink is asked, once: `ForgeCueSink.WillPlay(event, raise)`. The raise
holds the cue and its source.

- The default sink plays everything.
- A world of its own takes a sink of its own with `ForgeCues.UseSink(world, sink)`. `ForgeCues.Sink` is the one every
  other world asks.
- The sink is asked whatever mutes a cue. So a server that plays none can still send them on, as a network layer's
  sink does.
- A cue tag raised from code asks once. Its routes, and a Cue Tag within them, play inside that raise, unasked.

An effect's applier can say where it hit, before applying it: `spec.WithHit(point, normal, magnitude)`. Its cues then
play at the point, facing the normal, with the magnitude, as a sword's spark where the blade struck.

## Silencing and listening

You can silence cues, or listen to each one as it plays.

| Code | What it does |
|---|---|
| `ForgeCues.Enabled = false` | Silences every cue, as on a server or while a simulation runs ahead. |
| `ForgeCues.Mute(entity, true)` | Silences one entity's, as one off screen. A lasting cue that began still ends. |
| `ForgeCues.Played` | Hears each cue as it plays, for a debugger or a recorder. |
