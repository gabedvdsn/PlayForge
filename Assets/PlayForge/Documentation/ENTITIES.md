# PlayForge — Entities

An **Entity** asset (`EntityIdentity`) describes one kind of entity, such as the hero or a goblin. An entity is anything
that runs a gameplay ability system (GAS).

The asset is the entity's starting setup: its attributes, abilities, items, tags, level and workers. Many entities can
share one asset.

At runtime, a GAS hub reads the asset when it initializes. The hub is either the `GameplayAbilitySystem` component on a
GameObject, or a `RuntimeGameplayAbilitySystem` made from code. As it initializes, the hub:

1. Subscribes the entity's workers.
2. Registers its level.
3. Gives its Starting Abilities.
4. Builds its attributes from its Attribute Set.
5. Gives its Starting Items.
6. Applies its tags.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Hero gives `Entity.Hero`. The entity carries this tag and keeps its own level under it.

Each section below matches the inspector section of the same name. A collapsed section shows one line about what it
holds, such as the level range or how many starting items the entity equips.

The inspector's header shows the entity's one-line summary, as The Forge's Summary column does. The line gives the
first three attributes of the entity's attribute set as it starts, at its starting level, then its starting abilities:
"Health 500, Mana 300, Armor 5; Fireball, Blink".

## Identity

The Identity section names the entity, shows its asset tag and holds its icons.

| Property | Description |
|---|---|
| **Name** | The entity's display name, which also gives the asset tag. Renaming the entity changes its tag, so update anything that names the old tag. |
| **Description** | Free text for your UI. |
| **Asset Tag** | Read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the entity's icon; without one, the first texture is used. |

The asset tag is stored when you edit the Name, and the inspector header shows it after the entity's type. If the
stored tag differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new
one.

## Leveling

The Leveling section sets the entity's level range. An entity's level is kept under its asset tag. A level provider is
an asset whose level range another asset follows.

| Property | Description |
|---|---|
| **Link Mode** | **Standalone** (default): keeps the entity's own range.<br>**Linked To Provider**: shows a provider's range instead. |
| **Starting Level** | The entity's level when its GAS initializes, 1 by default. It's also the entity's lowest level. |
| **Max Level** | Its highest level, 99 by default. The level always stays within this range. |
| **Level Provider** | Shown while linked: the asset whose range the section shows, an Entity, Ability, Effect, Item or Attribute Set. |

- Right-click the **Level Provider** field to pick from one type.
- A link that would make a loop is refused, with an error in the Console.
- While no provider is set, a warning says the entity uses its own range.

Two buttons link the entity's Starting Items and Starting Abilities to it, or unlink them. Each asks first, and one Undo
takes it back.

- **Link Items & Abilities** links this entity's Starting Items and Starting Abilities to it, so their per-level values
  follow its range.
- **Unlink Items & Abilities** removes the links that point at this entity.

The entity's level feeds per-level values:

- the effects it applies as an origin;
- its Max Abilities and item limits;
- the cached scalers in its attribute set.

Code changes the level through the GAS, with `SetLevel` or `ModifyLevel` and the entity's asset tag.

> [!NOTE]
> At runtime, the entity's level range comes from its own Starting Level and Max Level, even while it is linked.

## Tags

The Tags section sets the entity's sides and the tags it carries.

| Property | Description |
|---|---|
| **Affiliation** | The entity's team or faction tags. An effect's Affiliation check compares its origin's affiliation with its target's. |
| **Context Tags** | Tags that describe the entity, such as `Undead`. Changes the entity makes as an origin itself carry them as context, and workers can filter by them. |
| **Granted Tags** | Tags that are always applied to the entity. |

### Affiliation at runtime

An entity's affiliation can change as the game runs, from code or from an effect.

- Code that starts a GAS can pass affiliation tags in its registration data. Those replace the asset's list.
- As the game runs, `SetAffiliation` on either hub sets them, for a side switch or a scripted betrayal.
- The GAS also applies these tags to the entity, on either hub.
- An effect's Side Change goes over them while it lasts ([EFFECTS.md](EFFECTS.md)). `GetSides` says which side changes
  the entity is under.

### Hearing tag changes

Code hears an entity's tags change through its tag cache.

- `gas.GetTagCache().Listen(tag, onChange)` hears the tag, and every tag under it, come and go, as it happens.
- Pass `ETagChangeKind.AnyCount` to hear every change to a tag's weight.
- Each change says the tag, and its weight before and after.
- `TagChanged` hears every change to any tag.

When the entity rebuilds its tags from their sources, it tells only what the rebuild changed. It rebuilds them as an
ability starts or ends, as an item changes, or as a loose tag is granted.

The listener below drives an animator's Stunned flag. Dispose the listener to stop it.

```csharp
var listener = gas.GetTagCache().Listen(stunned, change => animator.SetBool("Stunned", change.Added));
listener.Dispose();
```

A tag worker is evaluated at the end of a frame in which the entity's tags changed. One whose `ValidateWorkFor` reads
more than the tags overrides `EvaluateEveryFrame` to be evaluated every frame.

## Attributes

The Attributes section names the entity's attribute set and the attribute its time runs at.

| Property | Description |
|---|---|
| **Attribute Set** | The set the entity's attributes come from. [ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md) describes the set itself. |
| **Time Scale** | The attribute the entity's time runs at, 1 as the game's. |

Under the Attribute Set field, a table lists each attribute the set gives, its sub-sets' included, with the value it
starts at and its range.

When the GAS initializes, it:

- creates every attribute in the set and its sub-sets, each at its starting Current and Base values;
- applies the set's asset tags to the entity;
- subscribes the set's workers.

Gameplay effects then change the values. An entity with no Attribute Set has no attributes. An effect that targets an
attribute the entity lacks leaves it unchanged.

### Time Scale

**Time Scale** names the attribute the entity's time runs at, 1 as the game's. The entity's effects, cooldowns,
sequences and waits run by that time.

- An effect that lowers the attribute slows the entity.
- One that raises it hastes the entity.
- As each comes off, the entity's time is the game's again.

Give the attribute a Current of 1 in the set.

Code sets a scale of its own with `GetClock().TimeScale`, such as a hit-stop's 0. The clock reads the two multiplied,
so a hit-stop holds under a slow. `OwnScale` is code's part, and `AttributeScale` the attribute's.

## Abilities

The Abilities section sets the abilities the entity starts with, how many it can hold, and what its abilities do while
another is busy.

| Property | Description |
|---|---|
| **When Busy** | What one of the entity's abilities set to Entity's Policy does when it's asked to activate while another is busy:<br>**Activate Anyway** starts it at once.<br>**Refuse** starts it only when no ability under that choice is busy, and refuses it otherwise.<br>**Queue** (default) makes it wait in a queue while one is busy. |
| **Targeting Yields** | On by default: an ability whose While Targeting is the entity's rule gives way, while it waits for its target, as another of the entity's abilities activates. |
| **Starting Abilities** | Abilities the entity learns when its GAS initializes, each at its own Starting Level. Those set to Activate At Start then try to activate. |
| **Max Abilities** | The most abilities the entity can hold, 99 by default. Its kind can make it a scaler, which changes the limit with the entity's level ([SCALERS.md](SCALERS.md)). |
| **Allow Duplicates** | Lets the entity hold the same ability more than once. Off by default. |
| **Crowd Control** | The crowd control rules its abilities follow, such as a stun stopping every ability and Silenced stopping spells. Where it names none, the project's apply ([ABILITIES.md](ABILITIES.md#crowd-control)). |

[ABILITIES.md](ABILITIES.md) explains each When Busy choice, and Targeting Yields. A runtime entity's identity names
its crowd control rules too.

## Items

The Items section sets the items the entity starts with and the limits on its inventory.

| Property | Description |
|---|---|
| **Starting Items** | Items the entity is given when its GAS initializes, one row each. A row's **Equip** equips the item as it is given. |
| **Max Items** | The most items the inventory can hold, 99 by default. |
| **Max Equipped Items** | The most items equipped at once, 99 by default. |
| **Allow Duplicates** | Lets the inventory hold the same item more than once. Off by default. |
| **Equipped Too** | Lets more than one copy of the same item be equipped at once. Off by default; it needs Allow Duplicates, since an inventory that holds one of each item has no second copy to equip. |

Each Max field is a number or, by its kind, a scaler that changes the limit with the entity's level, with its rounding
([SCALERS.md](SCALERS.md)). A give or equip that breaks a limit fails.

## Components

An entity's components are features it carries, each a class with hooks into the entity's life. Add one with the
list's **+**. A kind that makes no sense twice is offered only while the entity holds none of it.

A component hears these moments:

| Hook | When it runs |
|---|---|
| **Register** | As the entity registers, its systems set up. |
| **Death** | As the entity dies, whether it stays or ends. Its context's `Killer` is the one whose loss killed it, as the entity's Died event names. |
| **Revive** | As an entity that stays revives. |
| **End** | As the entity ends. |
| **WindUp** | As an activation of one of the entity's abilities winds up, before the ability's cost and first stage. The activation waits on it, as on a turn to face its target. |

### Auras

An **Aura** component holds an effect on those near the entity for as long as the entity lives, such as a captain's
command aura.

| Property | Description |
|---|---|
| **Effect** | The effect it holds on each one it takes while they're near. |
| **Radius** | How far it reaches from the entity, read at each check. |
| **Flat** | Measures along the ground, leaving height out. Off, it measures a sphere. |
| **Takes** | Whom it takes, by how each stands to the entity: **Allies** (the default, itself among them), **Other Allies**, **Enemies**, **Self**, **All But Self** or **Everyone**. |
| **Interval** | How often it checks who's near, by the entity's own time: 0.25 s by default. |

An entity that dies takes its auras' effects off. One that stays and revives holds them again.
[EFFECTS.md](EFFECTS.md#auras) describes how an aura holds its effect.

### Bounty

A **Bounty** component gives the entity's killer an effect as the entity dies, such as gold or experience.

| Property | Description |
|---|---|
| **Effect** | The effect the killer takes. It comes from the entity, so the effect reads the entity's level. |
| **Share Radius** | Where it's set, the killer's allies within this distance of the entity take the effect too, as an assist. It's read at the entity's level. |
| **Flat** | Measures the Share Radius along the ground. Off, it measures a sphere. |

- A death no one dealt gives no bounty.
- An entity can hold several Bounty components, one for each bounty it gives.

### Turns To Face

A **Turns To Face** component turns the entity to face the target, or the point, of each of its abilities before the
ability's first stage.

| Property | Description |
|---|---|
| **Turn Speed** | How fast it turns, in degrees a second of the entity's own time. It's a number that can read a turn rate attribute; 0 faces at once. |
| **Start Angle** | The ability's first stage starts once the entity faces its target within this angle. |
| **Facing Angle** | The angle the turn goes on to after the stage starts, even after the ability has ended. The turn goes on until another turn, the entity's death or the game's word ends it. |
| **If Too Long** | What happens where reaching the Start Angle takes longer than **Max Wait**:<br>**No Limit** (default): it waits as long as it takes.<br>**Start Anyway**: it starts the stage, the turn going on.<br>**Cancel**: it cancels the activation. |
| **Max Wait** | How long reaching the Start Angle may take, in seconds of the entity's own time, where If Too Long sets a limit. |

- The ability's cost and cooldown are paid after the turn, so an interrupted turn pays nothing.
- A turn taken over before the stage starts, by another order or the game's word, cancels the activation.
- An ability with nothing to face, or with the entity for its target, turns nothing.
- An ability a trigger sets off turns as any does, so a counterattack turns to its attacker.
- An ability with a Turn To Face of its own turns its own way, or not at all
  ([ABILITIES.md](ABILITIES.md#turn-to-face)).
- An entity holds one at most.

#### Turning from code

Code turns an entity through its hub:

- `gas.Face(target, degreesPerSecond, within)` turns it to face a target, and follows the target as it moves.
- `gas.Face(point, ...)` turns it to face a point.
- `gas.StopTurning()` stops the turn, as when a move of the game's takes over.
- `gas.AngleToFace` says how far it has yet to turn.

A turn sets the entity's facing, flat: its object's rotation, or a runtime entity's. A game that turns its characters
itself, by animation or a controller, puts an `IForgeTurner` on the entity's object. The turner takes each step of a
turn in PlayForge's place.

The Creator Lab's units stop a turn as they walk or stop.

### Respawn

A **Respawn** component revives an entity that stays as it dies, once a delay has passed.

| Property | Description |
|---|---|
| **Delay** | How long after it dies the entity revives, in seconds of its own time, read at its level. |
| **At** | Where it revives:<br>**Home**: where it stood as it registered.<br>**Where It Died**.<br>**Point**: a point in the world.<br>A place of your own, such as a checkpoint. |

A place of your own is a `[Serializable]` class made from `AbstractRespawnPlace`. It says `Where` from the entity's
context and its home: a hero's fountain, for example.

- Its Death's On Revive effects land as it revives, as any revive's do.
- A revive before its time, such as a buyback, ends the wait.
- The wait is the entity's `RespawnTimer`, a service. A HUD reads `gas.FindService<RespawnTimer>()`'s `Waiting` and
  `TimeLeft`.
- An entity that ends as it dies has nothing to revive, which the Problems view says.
- A save keeps neither the entity's death nor its wait.
- An entity holds one at most.

### Cues

A **Cues** component plays what the entity shows and sounds at its moments, on the entity.

| Property | Description |
|---|---|
| **On Spawn** | Plays as the entity comes into the game. |
| **On Death** | Plays as it dies, its killer behind the cue. |
| **On Revive** | Plays as it comes back. |
| **While Tagged** | Lasting cues, each under a tag, that play for as long as the entity holds the tag, such as stars over its head while it's Stunned. |

- Each step, a tag the entity has taken on begins its While Tagged cues, and one it has let go of ends them.
- The While Tagged cues end with the entity.
- Each list holds cues as an effect's does ([EFFECTS.md](EFFECTS.md#cues)), a Cue Tag among them.
- An entity holds one at most.

### Cue Receiver

A **Cue Receiver** component plays cue tags its own way for the entity, before the project's cue library.

| Property | Description |
|---|---|
| **Routes** | Its own routes, which play for its cue tags before the library's. |
| **Pass Through** | Plays the library's route too, after its own, for a tag it plays. |
| **Variant** | Its variant, by its last name. The library's route under that name plays in place of the tag's own, such as Cue.Hit.Golem for Cue.Hit, where the library has one. |

[CUES.md](CUES.md#routes) explains cue routes. An entity holds one Cue Receiver at most.

### Writing your own component

A component of your own derives from `AbstractEntityComponent` and overrides the hooks it needs. Each hook is a no-op
otherwise.

A component is the entity's, shared by everything that holds it, such as every entity made from one asset. So what it
keeps for one entity, it keeps through its context: `ctx.GetState<T>(this)`.

Its context's `Identity` is the entity's settings, its Death among them.

### Services

A service is a per-entity object that an entity's components share. The auras an entity runs are one, `AuraSet`, and
so is the wait a Respawn counts down, `RespawnTimer`.

- `gas.GetService<T>()` returns the entity's service of a kind, made the first time it's asked for.
- `gas.FindService<T>()` finds one without making one.

The entity calls each service's hooks:

| Hook | When it runs |
|---|---|
| `Step` | Each frame, by the entity's own time: after its effects, and before its turn and its barriers. |
| `Revive` | As the entity revives, before anything else of the revive. |
| `End` | As the entity ends. |

A service of your own derives from `AbstractEntityService` and overrides what it needs, such as a heat meter that
cools by the entity's time. A save keeps no services.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
when the entity's GAS initializes, and stays for the entity's lifetime. The attribute set's workers are subscribed at
the same time.

| Property | Description |
|---|---|
| **Name** | Labels the group. |
| **Attribute** | Workers that run before or after an attribute changes, for example to reduce incoming damage. |
| **Impact** | Workers that inspect the changes the entity deals. |
| **Tag** | Workers that react when the entity's tags change. |
| **Analysis** | Workers that examine the frame at its end, for example to detect death. |

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group from
another asset. Above the worker group, the section holds the entity's **Modifiers**.

### Threshold

A **Threshold**, an analysis worker, acts as an attribute crosses a value, without code. Megaera rages as her Health
falls past half, and Ice Blast shatters below 13%.

| Property | Description |
|---|---|
| **Target** | The attribute it watches. |
| **Groups** | Which retention groups' parts it reads: all of them, the whole value, by default; only these; or all but these. |
| **What** | What it reads of the attribute: **Current**, **Base**, **Missing** (what's missing of it) or **Share** (its share of its base). On its share, a **Threshold** of 0.5 is half. |
| **Threshold** | The value it watches for, read at the entity's level. |
| **Crossing** | When it fires:<br>**While It Holds**: while its **Comparison** holds, as it always did.<br>**Falling**: as what it reads falls to the threshold or below.<br>**Rising**: as what it reads rises above it.<br>**Either**: either way. |
| **Comparison** | How what it reads compares with the threshold, where it fires while that holds. |
| **Trigger Once** | Fires at its first crossing alone. Off, it fires each time. |
| **Effects** | The effects it applies to the entity as it fires. |
| **Applied By** | Whom the effects come from: the entity itself, or, set to Crosser, the one whose change crossed it this frame. So a shatter is its caster's, kill and all. |
| **Event** | A gameplay event it sends as it fires, which its abilities' triggers hear. Its source is the one who crossed it, and its subject the entity. |

- It checks at the frame's end, as the death watcher does, so the frame's hits and heals net out. The death watcher is
  a Threshold that kills.
- An entity that revives re-arms it. Its first check after the revive notes its side again.

### Can't Fall Below and Return Damage

Two attribute workers keep the common rules.

| Worker | What it does |
|---|---|
| **Can't Fall Below** | Holds its attribute at its **Floor**: a loss that would take it lower takes it to the floor. |
| **Return Damage** | Sends its **Share** of each workable loss its attribute takes from another back to the one who dealt it, as Blade Mail does. |

- Shallow Grave keeps its target at 1 Health with a Can't Fall Below, in a buff's worker group while it lasts.
- What Return Damage sends back is typed Returned. So a return on the other side doesn't send it back again, and no
  lifesteal heals from it.

### Modifiers

**Modifiers**, above the workers, change other effects as they're made, for as long as the entity lives.

- They change its own effects, such as a Frost Mage's every Chill granting Frozen.
- They change those landing on it.

Each entry is a rule of its own, or names a shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).

### Lifesteal

An entity's own lifesteal is its **Lifesteal**: the attribute its project's Lifesteal worker names as its Share. Its
value is its set's value for that attribute, which the Lifesteal worker heals by ([WORKERS.md](WORKERS.md#lifesteal)).

> [!NOTE]
> The entity's Lifesteal list is gone.

## Death

The Death section sets what the entity's death does.

| Property | Description |
|---|---|
| **Policy** | **End** (default): as it dies, its hub ends it at its frame's end, as an entity always did.<br>**Stay**: keeps it, dead, until it revives, for a MOBA hero or anyone who dies and comes back. |
| **Dies At Zero Health** | On by default: where no death watcher watches, the entity dies as the attribute playing its project's Health role reaches 0. Off, as for a training dummy, that attribute doesn't kill it. |
| **Purges** | On by default: a staying entity's death purges its effects, all but those it keeps. |
| **Keeps As Project** | On, it keeps what its project's Purge Levels keep on death. |
| **Keeps Tiers** | More tiers it keeps. |
| **Keeps Categories** | More categories it keeps, whatever their effects' tiers. |
| **On Death** | The effects it takes as it dies. |
| **On Revive** | The effects it takes as it revives, such as its health back. |
| **Body** | A MonoBehaviour process, a prefab, that it spawns where it is as it starts and as it revives, and ends as it dies. |

### How an entity dies

An entity dies as a death watcher among its analysis workers, or its attribute set's, says it does.

Where none watches, and an attribute plays its project's Health role ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), it dies as
that attribute reaches 0. **Dies At Zero Health**, on by default, says it does. Off, as for a training dummy, that
attribute doesn't kill it.

### Staying dead

An entity whose **Policy** is **Stay** dies at its frame's end and stays registered, with its items, levels and
lasting effects.

- Its abilities are refused while it's dead.
- A dead entity takes no one else's effects, as the dead in the games are untargetable. An effect another applies to it
  while it's dead is refused.
- Its own effects still land, as its On Death respawn timer and its On Revive effects do.

### Purging on death

A staying entity's death purges its effects, all but those it keeps, as the games' deaths do
([EFFECTS.md](EFFECTS.md#purging)). **Purges**, on by default, turns the purge on.

It keeps the **categories** it keeps, whatever their effects' tiers, and the **tiers** it keeps. The categories are:

| Category | What it holds |
|---|---|
| **Cooldown** | An ability's cooldown. |
| **Lent** | What an equipped item, a set bonus, an aura or a passive lends. A passive is an ability that activates as it's granted, and what applied an effect can claim it so too, through `spec.Claimed`. |
| **Toggle** | A Toggle component's effect, or one an ability's component claims as a toggle's. |
| **Own** | The rest it applied to itself. |

Three fields say what it keeps:

- **Keeps As Project**, on, keeps what its project's Purge Levels keep on death: each tier's Kept On Death, and their
  Kept On Death categories.
- **Keeps Tiers** keeps more tiers.
- **Keeps Categories** keeps more categories.

A project starts with cooldowns, lent effects and toggles as its Kept On Death categories, and those are what a death
keeps with no project. An effect at no tier goes, unless a category it's of is kept.

> [!NOTE]
> The purge took the place of Ends Effects Tagged, which ended effects by their tags.

### On Death and On Revive

A staying entity takes the effects these two lists hold as it dies and as it revives.

- **On Death** holds the effects it takes as it dies. A respawn timer is a durational effect with a
  **Revive On Removal** worker: as it ends, the entity revives, where it was or at a point.
- **On Revive** holds the effects it takes as it revives, such as its health back.

### Body

An entity's **Body** is a MonoBehaviour process, a prefab, that it spawns where it is as it starts and as it revives,
and ends as it dies.

- A runtime entity takes its position from its body, so an area search finds it there.
- `GetBody()` returns the body.

`UseBody(process)` gives a running entity a body of its own at runtime: a process running already, or one registered
for it there.

- The entity follows it, and it ends with the entity as the entity dies, as its identity's Body does.
- A revive spawns the identity's Body, where it has one.
- The body the entity had ends first.
- `UseBody(null)` lets one go, ending it.
- A dead entity takes none.

### Reviving

`Revive(at)` on either hub brings a staying entity back, at a point or where it was.

- It re-arms the entity's analysis workers, so its death watcher sees its next death.
- Callbacks hear `OnEntityRevived`, as they hear `OnEntityDied`.

## Combat Log

Each entity keeps a combat log of what happens to it. A test reads it as the game does, through `gas.GetCombatLog()`,
and the Debugger shows it under the entity it has selected.

### What the log writes

What a combat log writes is the project's to say, under **Combat Log** in Project Settings → PlayForge.

| Setting | Description |
|---|---|
| **Records** | What it records:<br>**Hits Taken**: a workable change another's effect makes to its attributes.<br>**Hits Dealt**.<br>**Changes**: its other changes, such as its own, a cost, regeneration or a transfer. |
| **Events** | The gameplay events it's sent, by tag or one under it, such as Died, Kill or your own. They're written though no ability listens for them. |
| **Keep** | How many entries each entity keeps, its last: 100 by default. 0 keeps none, as for a build. |

### Log entries

Each entry in a combat log is a `CombatLogEntry`.

| Property | Description |
|---|---|
| **Kind** | Taken, Dealt, Changed or Event. |
| **Time** and **Frame** | When it was written. |
| **Other** | The other entity: the dealer of a hit taken, the one a hit dealt struck, an event's source, or the one it names. |
| **Effect** | The effect behind it. |
| **Attribute** | The attribute it changed. |
| **Before** and **After** | The attribute's value before and after. |
| **Change** | The change. |
| **Steps** | The hit's steps through its pipeline. |
| **Killed** | Whether it killed. It's marked on the blow that killed, in both logs. |

An entry reads as a line: "Took -20 Health from Lina (Fireball)".

The log's **Entries** run oldest first.

- **Of(kind)**, **By(effect)** and **With(entity)** filter them.
- **Total(kind, attribute)** adds their changes up, such as the damage an entity took.

### How a change landed

A combat log entry also tells how its change came to land.

- **Made** is its change as made, before any step.
- **ChangeSteps** lists each step it took on the way:
  - each attribute worker that heard it, with its **Timing** (before the commit or after), its **Execution** (inline,
    or the actions it **Queued**), and the change **Before** and **After** it;
  - a barrier's soak;
  - a gain's scaling, such as Healing Received's.
- **StepsText** reads them as a line:
  "-30 as made → Math -30 → -15 (inline) → Answering (queued 1, after) → landed -15".

The steps are recorded only where the log keeps entries, or where something hears the entity's
`OnAttributeChangeStep`.

## Effect Journal

An `EffectJournal` keeps the life of each effect on the entities it listens to. The Creator Lab's bottom panel reads
one, and so does the Debugger's Timeline.

### Listening

An effect journal records only while it listens, and an entity no journal listens to pays nothing.

| Method | What it does |
|---|---|
| `Listen(gas)` | Listens to one entity. |
| `Follow(list)` | Listens to these entities and no others. |
| `Forget(gas)` | Stops listening to one entity. |
| `Dispose()` | Stops listening to all of them. What it wrote stays. |

### Reading the journal

An effect journal keeps its entries in order, and counts each one it writes.

- Its **Entries** run oldest first: its last **Keep** of them, 500 unless it's told otherwise.
- **Of(effect)** and **On(entity)** filter them.
- **Written** counts every entry it wrote.
- **Wrote** is raised as each is written.

### Journal entries

Each entry in an effect journal is an `EffectJournalEntry`. It holds its **Kind**, its **Time** and **Frame**, the
**Entity** it happened on, the **Effect**, its **Spec** and its **Source**, and what its kind tells.

| Kind | What it tells |
|---|---|
| **Applied** | The effect landed, with its first impact's number read as it landed, part by part (**Trace**, a `NumberTrace`). |
| **Refused** | It didn't land, and its **Refusal** says why. |
| **Ticked** | Its ticks executed, **Ticks** of them. |
| **Impact** | A change it made, to its **Attribute**, by its **Change**, with its **Breakdown** through its pipeline. |
| **Removed** | It came off. |
| **Worker** | One of its workers heard one of its moments, or one of its dealer's impact workers ran for its hit. |

An entry reads as a line: "Burn landed on Target, from Caster", "Poison refused by Target: immunity", "Burn's Noting
heard it tick on Target, queuing Ticked".

#### Refusals

A refused entry's **Refusal** is one of these:

- **Dead**: the entity is dead.
- **Affiliation**: its affiliation policy doesn't take the entity.
- **Requirements**: its requirements don't hold.
- **Immunity**: an immunity names it.
- **Chance**: its chance missed.
- **Stopped**: a Block or Evade stopped it.
- **Cycle**: it was asked for again inside its own application.

Callbacks hear each refusal as `OnEffectRefused(spec, refusal)`.

#### Worker entries

A worker entry holds:

- the **Worker**, and its **WorkerName**;
- the **Hook**: Application, Tick, Removal or Impact;
- the **Side** it ran on: the **Target**, or the **Dealer** for a hit, as [WORKERS.md](WORKERS.md#the-map) says;
- the **Actions** it queued, each **Queued**, **Ran** or **Dropped**.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag) and a value of a kind.

| Column | Description |
|---|---|
| **Key** | A tag that names the row. |
| **Kind** | The kind of value: Float, Int, String, Bool, Tag, Attribute, Effect, Ability, Entity, Object, MonoBehaviour or ScriptableObject, or a kind of your own. |
| **Value** | The row's value. A row saves its own value alone. |

- A kind of your own is a `[Serializable]` class made from `LocalDataValue<T>`, such as a point or a colour. The
  row's kinds offer it beside PlayForge's.
- Object fields take project assets, not scene objects.
- Code reads a value by its type, with `data.TryGet(out float range)` or `data.TryGet(out MyPoint point)`.
- A number, a Float or an Int, can scale. Code reads it with `data.Value.TryReadNumber(context, out float number)`.

When a `GameplayAbilitySystem` component initializes, it loads the entity's Local Data into a store of its own. A
`RuntimeGameplayAbilitySystem` loads its identity's rows the same way: the entity asset's, where it's made from one.

Code and workers read and write that store through the GAS, with `TryGetLocalData` and `SetLocalData`. Threshold
analysis workers, for example, note there that they have fired.

## Used By

The Used By section lists the PlayForge assets that refer to this entity, each with its type icon, such as the
abilities and items linked to it. Click a row to select the asset. The list follows the project as it changes.
