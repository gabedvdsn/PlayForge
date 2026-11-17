# PlayForge — Cues

A **cue** is what something shows and sounds, apart from what it changes: a spark as a hit lands, a hum while a shield
holds, a shake as a giant falls. Effects, abilities, entities and items each list their cues, and a game's own code
raises one where it likes. Every cue plays through `ForgeCues`, after Unreal's gameplay cues: a **burst** at a moment,
as a hit's spark, or a **lasting** cue's beginning and its end, as a shield's hum, which share a key.

## Where cues are raised

- **Effects**: an effect's Cues section plays its bursts as it lands, as its ticks execute and as it comes off, and its
  lasting cues while it's on ([EFFECTS.md](EFFECTS.md#cues)).
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

Each cue in a list is a handler, its kind picked from the list:

- **Camera Shake** shakes the camera through the shaker the game gives `ForgeCues.Shaker`, an `ICameraShaker`.
- **Sound** plays its clip at its target or its point.
- **Prefab** places its prefab at its target or its point, for its **Lifetime**, or, with **While Active**, until its
  cue ends ([Pooling](#pooling)).
- **Cue Tag** plays what its tag routes to ([Routes](#routes)).

Write a handler of your own by deriving from `AbstractCueHandler`: override the moments it plays at, or `Burst` to play
every burst one way, reading the moment from `cue.Moment`, and `Begin` and `End` for a lasting one.

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

## Silencing and listening

- `ForgeCues.Enabled = false` silences every cue, as on a server or while a simulation runs ahead.
- `ForgeCues.Mute(entity, true)` silences one entity's, as one off screen. A lasting cue that began still ends.
- `ForgeCues.Played` hears each cue as it plays, for a debugger or a recorder.
