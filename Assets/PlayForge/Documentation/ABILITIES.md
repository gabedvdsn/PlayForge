# PlayForge — Abilities

An **Ability** asset describes one thing an entity can do, such as a fireball, a dash or a heal. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy.

An entity learns an ability in one of three ways, and keeps it until the ability is removed:

- from its Entity asset's Starting Abilities;
- from an equipped Item;
- from code, with `AbilitySystemComponent.GiveAbility`.

Each time the ability activates, the GAS checks that it may run, lets a targeting task pick a target, then runs the
ability's stages of tasks. The ability's cost and cooldown are gameplay effects, assets that change an entity, which it
applies to its caster. Abilities run as processes under `ProcessControl`, so the scene needs one.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fire Ball gives `Ability.FireBall`. The entity carries this tag while it has the ability, and keeps
the ability's level under it.

Each section below matches the inspector section of the same name.

The inspector's header shows the ability's one line, as The Forge's Summary column does. The line gives each effect the
ability applies, by its first impact across its levels, then its costs and its cooldown, as
"-450/-600/-750 Health; 280/420/680 Mana; 70/60/50 s". A number that reads what the editor can't give it, such as the
target, is marked `*`.

## Identity

The Identity section names the ability, describes it and sets its icon.

| Property | Description |
| --- | --- |
| **Name** | The ability's display name. It also gives the asset tag, so renaming the ability changes its tag. |
| **Description** | Free text for your UI. The inspector header shows it. |
| **Asset Tag** | Read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The tag is stored when you edit the Name. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the ability's icon; without one, the first texture is used. |

If the stored tag differs from the one the Name gives, the Asset Tag shows in warning text with an **Update** button
that stores the new one.

> [!NOTE]
> Renaming an ability changes its asset tag. Update anything that names the old tag.

## Leveling

An ability has its own level on each entity that learns it, kept under the ability's asset tag. Effects the ability
applies take this level, so their scalers can pick per-level values.

A scaler is a value source, such as a table with one value per level. A level provider is an asset whose level range
another asset follows.

| Property | Description |
| --- | --- |
| **Link Mode** | Standalone (default) keeps the ability's own range.<br>Linked To Provider takes the range from a level provider instead. |
| **Starting Level** | The bottom of the ability's own range (default 1). An entity's Starting Abilities begin at the Starting Level, and the ability's level never goes below it. |
| **Max Level** | The top of the ability's own range (default 4). |
| **Level Provider** | Shown while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set. Right-click the field to pick from one type. |
| **Ignore When Level Zero** | On by default: the ability can't activate while its level is 0. With a Starting Level of 0, the entity must raise the ability's level before it can use it. |
| **Link Cost & Cooldown** | Links this ability's cost and cooldown effects to it, so their per-level values follow its range. |
| **Unlink Cost & Cooldown** | Removes the links that point at this ability. |

Under Linked To Provider:

- A link that would make a loop is refused, with an error in the Console.
- While no provider is set, a warning says the ability uses its own range.
- While linked, Starting Level and Max Level show the range in use, read-only.

**Link Cost & Cooldown** and **Unlink Cost & Cooldown** each ask first, and one Undo takes it back.

Code changes an ability's level through the entity's GAS, with `SetLevel` or `ModifyLevel` and the ability's asset tag.
An ability that an Item grants starts at the item's level, kept within this range.

> [!NOTE]
> The link serves the inspector and the tooltips that `AbilityInspector` builds. At runtime, the ability's level stays
> within its own Starting Level and Max Level, even while it is linked.

## Tags

An ability's tags describe it, and some of them are applied to its entity while it has the ability or while it runs.

| Property | Description |
| --- | --- |
| **Context Tags** | Describe the ability, such as `Spell` or `Melee`, and are not applied to the entity. The changes made by effects this ability applies carry them as context, and workers can filter by them. |
| **Passive Tags** | Applied to the entity while it has the ability. |
| **Active Tags** | Applied to the entity while an activation of the ability is running. |

## Activation

The Activation section sets how the ability starts: when it may start, what sets it off, what it costs and how it picks
its target.

| Property | Description |
| --- | --- |
| **When Busy** | What happens when the ability is asked to activate while another ability is busy. See [When Busy](#when-busy). |
| **While Targeting** | Whether the ability gives way, while its targeting waits for the player, as another of the entity's abilities activates. See [While Targeting](#while-targeting). |
| **Buffer Window** and **Buffer Priority** | The input buffer's window and priority, under Queue. See [The input buffer](#the-input-buffer). |
| **Activate At Start** | When the ability is one of the entity's Starting Abilities, it tries to activate once, as the entity's GAS starts. An ability that an item or code grants later doesn't try it. |
| **Triggers** | What sets the ability off on its own. See [Triggers](#triggers). |
| **Chance to Activate** | How likely an activation is to start, from 0 to 1, rolled once for each, a player's or a trigger's, after its other checks. A miss is refused with Chance. |
| **Activation Roll** | Makes Chance to Activate pseudo-random or lucky. It shows for a chance that can miss. |
| **Proc Coefficient** | What its effects' hits carry for on-hit effects, where an effect sets none of its own. |
| **Ignores Crowd Control** | The crowd control rules the ability ignores, by their tags, as Black King Bar's Avatar ignores Silenced; a tag ignores the rules under it too. The other rules still stop it, and still interrupt it as it runs (see [Crowd Control](#crowd-control)). |
| **Cost** and **Cooldown** | Gameplay effects applied to the caster as the ability starts, or by the stage that takes them. See [Cost and Cooldown](#cost-and-cooldown). |
| **Attribute Costs** | What the ability spends of attributes as it's used, beside its Cost effect. See [Attribute Costs](#attribute-costs). |
| **Targeting** | The task that picks what the ability acts on. See [Targeting](#targeting). |
| **Use Implicit Targeting** | Records the caster as the activation's target before targeting. See [Implicit targeting](#implicit-targeting). |

### How an ability starts

One activation of an ability goes through these steps:

1. The caster's tag requirements and Caster Checks are checked (see [Requirements](#requirements)), and so is the
   ability's level (see [Leveling](#leveling)).
2. The Targeting task runs, if there is one.
3. If targeting found a target, every check and requirement is checked again, the target's included.
4. The activation winds up. It waits for what its components and its entity's wait for before it starts, such as a turn
   to face its target (see [Turn To Face](#turn-to-face)).
5. The Asset Loaders run (see [Stages](#stages)).
6. The Stages run, one after another.

An interrupt during the wind-up cancels the activation. Its cost and cooldown, paid after the wind-up, aren't paid.

These steps start inside the call that asks for the ability, `AbilitySystemComponent.TryActivateAbility`, and run until
a task waits. So an ability whose tasks all finish at once has ended, and its callbacks have fired, by the time the call
returns.

An ability asked for again while that call still runs starts in the next frame instead. That holds whether it's asked
for from its own callbacks or from those of another ability that it started.

When the entity ends, its running abilities end with it, critical or not, and it takes no more activations.

### Requesting an activation

Code asks for an ability in one of three ways, and each returns an `ActivationResult`.

| Request | How |
| --- | --- |
| By slot | With a request from `CreateActivationRequest`. |
| By asset | `TryActivateAbility(ability)`. |
| By tag | `TryActivateAbility(tag)` activates the first ability, by slot, whose asset tag or one of whose context tags is that tag or under it. |

The result's `Status` says what came of the request:

| Status | Meaning |
| --- | --- |
| **Started** | It started, inside the call. |
| **Buffered** | It waits in the input buffer. |
| **NextFrame** | It was asked for while its own activation call still ran, so it starts in the next frame. |
| **Refused** | It was refused. See [Refusals](#refusals). |

The result reads as true where the request wasn't refused, so `if (abilities.TryActivateAbility(ability))` asks whether
it was taken.

#### Passing magnitudes

The request's data can carry magnitudes for the effects the ability applies, set with
`data.SetByCaller(tag, magnitude)`. A Set By Caller scaler on each effect reads them (see [EFFECTS.md](EFFECTS.md)). So
a caller that knows the damage as it casts passes it in, and one effect serves every strength.

### Refusals

Each form of request can say why it was refused, through its result's `Refusal` or an `out ActivationRefusal`. The
refusal holds:

| Field | Description |
| --- | --- |
| `Reason` | Why it was refused, such as Cooldown, Cost, Requirements, Blocked or Busy. |
| `Rule` | For another caster check, that check's name. For Crowd Control, the rule's tag, such as Stunned. |
| `Detail` | Where a check or a component refused it, why, in the check's own words, such as "Its caster's Mana, 10, can't pay 50". |

The ability system's `OnAbilityRefused` callback tells each refusal as it happens. A request that waits in the input
buffer isn't refused, and isn't told again as the buffer retries it.

#### Refusals after targeting

A check on the target comes after targeting, and ends the activation instead, with nothing of it paid. The request said
nothing was refused, as the activation had started. So the callback tells it, as **Target**, with `Rule` naming the
check and `Detail` saying why.

A component that refuses the target its targeting picked is told the same way, as a Cast Range refuses one beyond it.
It's told as the kind its `RefusedAs` says, **Range** for a Cast Range, with its reason in `Rule` and `Detail`.

### When Busy

**When Busy** sets what happens when the ability is asked to activate while another ability is busy.

An activation is busy, or holds its *claim*, while its targeting and its critical tasks run. A critical task is one that
must not overlap another ability's critical work, such as an attack animation. The inspector marks critical tasks and
stages Critical.

Which claims an ability waits on depends on its choice and its tasks:

- An ability waits on the claims of abilities that use the same choice.
- An ability with critical tasks also waits on any ability with critical tasks that holds its claim, whatever its
  choice.

| Option | What it does |
| --- | --- |
| **Entity's Policy** (default) | Uses the entity's When Busy, set in the Entity asset's Abilities section. |
| **Activate Anyway** | Starts at once. It is refused, as **Critical**, only when it has critical tasks and another ability with critical tasks holds its claim. |
| **Refuse** | Starts only when no Refuse ability holds its claim; otherwise the request is refused, as **Busy**. With critical tasks, it is also refused, as **Critical**, while another ability with critical tasks holds its claim. |
| **Queue** | Waits in the input buffer while it can't start, for up to **Buffer Window** seconds of the caster's time, then lets go. See [The input buffer](#the-input-buffer). |

Whatever the choice, an ability can't start again while its own activation holds its claim.

#### The input buffer

Under **Queue**, a request waits in the input buffer when any of these holds:

- A Queue ability holds its claim.
- The ability has critical tasks, and any ability with critical tasks holds its claim.
- A running stage blocks this ability (see [Stages](#stages)).

The request waits for up to **Buffer Window** seconds of the caster's time, then lets go.

- The buffer holds one request for each ability. A new press replaces it, with its window afresh.
- As a claim is released, and each frame, the request with the highest **Buffer Priority** that can start does. Of
  equals, the newest starts.
- While the entity is paused or its time is stopped, as in a hit-stop, the windows wait too.
- A request can name a window of its own (`CreateActivationRequest`'s `bufferWindow`).

| Property | Description |
| --- | --- |
| **Buffer Window** | How long a request waits in the input buffer, in seconds of the caster's time (default 0.3 s). A window of 0 doesn't wait, so the request is refused. |
| **Buffer Priority** | Which waiting request starts first: the highest (default 0). |

Each is a number field read at the ability's level. They show under Queue and under the entity's policy, which may be
Queue, and are hidden under the others.

### While Targeting

**While Targeting** sets whether the ability, while its targeting waits for the player, gives way as another of the
entity's abilities activates. A player who starts one ability's targeting and presses another's key means the other.

| Option | What it does |
| --- | --- |
| **Entity's Rule** (default) | Follows the entity's **Targeting Yields**, set in its Abilities section, on by default. |
| **Yields** | Its targeting is cancelled, nothing of it paid, and its claim lets go at once. So the other starts, ahead of anything waiting in the input buffer. |
| **Holds** | It keeps its claim until its target is chosen or it's cancelled. The other does as its When Busy says. |

An ability that yields gives way only to an activation that passes its own checks. So a press of an ability on cooldown
leaves it be.

An activation that a trigger or Activate At Start makes doesn't cut a choice short, as the player didn't ask for it.
Code that activates for the game rather than the player says the same of its request: `CancelsTargeting = false`.

### Triggers

**Triggers** are what set the ability off on its own. Each trigger tries the ability as a request would, so its checks
and its When Busy still hold.

| Trigger | What sets it off |
| --- | --- |
| **Gameplay Event** | Code sends the entity an event under the trigger's tag, or a tag under it, with `SendGameplayEvent(tag, data)`. The ability gets the event's data, its target among it. |
| **Tag Added** | The entity gains the trigger's tag, or one under it, that it didn't have. More of a tag it holds sets off nothing. |

#### Core events

**The core's own events** are sent only where an ability listens for them.

| Event | Sent to |
| --- | --- |
| **Impact Dealt** | The dealer, as an impact lands between two entities. |
| **Impact Taken** | The one hit, as an impact lands between two entities. |
| **Kill** | The killer: the source of the last blow the one who died took. |
| **Died** | The one who died. |
| **Revived** | A staying entity, as it revives. |
| **Blocked** | One that stops an effect by a Block stage. |
| **Evaded** | One that stops an effect by an Evade stage. |
| **Hit Blocked** | The dealer of an effect that was blocked. |
| **Hit Evaded** | The dealer of an effect that was evaded. |

Their data hold the instigator as Source, the entity it was done to as Subject, and an impact as Impact.

#### Trigger properties

Each trigger can narrow what sets it off, and say what the ability targets.

| Property | Description |
| --- | --- |
| **Attribute** | For an impact event, the trigger takes only impacts on this attribute. |
| **Sign** | For an impact event, it takes only a Loss or only a Gain. |
| **Impact Types** | For an impact event, it takes only impacts of these types. |
| **Chance** | Rolls once for each event, as Counter Helix's 20%. |
| **Chance Roll** | Makes the Chance pseudo-random or lucky ([EFFECTS.md](EFFECTS.md#requirements)). |
| **Target** | What the ability targets: the ability's own targeting, as an area spin finds the enemies round it; the event's **Instigator**, as a counterattack; or its **Subject**, as an on-hit ability targets the one hit. |

An ability set off by Died runs on a dead entity only if its rules don't ask that its caster be alive.

#### Problems view

The Problems view checks an ability's triggers:

- It notes a Gameplay Event trigger on an event that neither the core nor an asset in the project sends, as only code
  can send it. An asset sends one as a threshold's Event or a stop stage's does.
- It warns of an Impact Dealt or Impact Taken trigger's impact type that no effect deals, which never matches.

### Cost and Cooldown

**Cost** and **Cooldown** are gameplay effects ([EFFECTS.md](EFFECTS.md)) that the ability applies to its caster. They
apply as the ability starts, after its targeting, so a cancelled targeting pays nothing. A stage that takes them applies
them instead (see [Stages](#stages)).

| Property | Description |
| --- | --- |
| **Cost** | Usually an Instant effect that lowers a resource such as Mana. |
| **Cooldown** | Usually a Durational effect. While it is active, the caster carries its asset tag, and the Cooldown check refuses the ability. |
| **Attribute Costs** | Shown after Cost: what the ability spends of attributes as it's used, beside its Cost effect. See [Attribute Costs](#attribute-costs). |

Each field has tools of its own:

- A line under each field says how its effect takes its levels. **Link** makes it take this ability's.
- **New**, beside an empty field, makes an effect linked to this ability.
- If Project Settings → PlayForge names a Cost or Cooldown effect, the field's right-click menu copies it or uses it.

#### Attribute Costs

**Attribute Costs** are what the ability spends of attributes as it's used. They're paid as well as its Cost effect.

| Property | Description |
| --- | --- |
| **Attribute** | The attribute it spends. |
| **Role** | Where it names no attribute, a role whose attribute in the project it spends ([ATTRIBUTES.md](ATTRIBUTES.md#roles)). |
| **Amount** | How much it spends, a number that can scale by level. |

- The ability is refused with Cost while its caster has less than they come to, lacks the attribute, or no attribute
  plays the role.
- They're paid where a Cost is: as the ability starts, or as its stage applies usage.
- The section's line says them, as "30 Mana".

### Targeting

**Targeting** is the task that picks what the ability acts on, such as an entity the player clicks, or the caster
itself.

- Targeting holds the activation's claim.
- If the task fails while its **Break Runtime On Invalid** is on, or it doesn't finish, the activation ends.
- Leave Targeting empty to skip this step.

PlayForge's targeting tasks come in two kinds:

| Kind | Tasks |
| --- | --- |
| **Click targeting** | The Select tasks: Position, Direction, GAS Target, GAS Or Position and GAS Target In Range. See [Click targeting](#click-targeting). |
| **Auto targeting** | Auto Target and Auto Target By Affinity. See [Auto targeting](#auto-targeting). |

#### Numbers in targeting tasks

A targeting task's numbers can scale by level or read an attribute. They're read as its activation starts, at its
ability's level.

They include its **Range**, its **Max Targets**, a pointer task's **Max Distance** and an attribute condition's value. A
pointer's **Mouse Button** picks one of three, so it stays a choice.

#### Implicit targeting

Implicit targeting records the caster as the activation's target before targeting runs.

| Property | Description |
| --- | --- |
| **Use Implicit Targeting** | Off by default. Before targeting, it records the caster as the activation's target, under **Implicit Tag**. |
| **Implicit Tag** | The tag the caster is recorded under (default `Target`). It shows while Use Implicit Targeting is on. |

- A targeting task that sets a target replaces it.
- The Passive preset turns it on, as a passive gives its holder its effects.

### Click targeting

**Click targeting** waits for a press and aims along the game's input, `ForgeInput.Current`. Its tasks are the Select
tasks: Position, Direction, GAS Target, GAS Or Position and GAS Target In Range.

#### Input and cameras

By default, the game's input is the Input System's pointer, a mouse, a pen or a touch, where its package is in and on.
Otherwise it's the Input Manager's mouse. Each is aimed through `Camera.main`, and with no camera, a press aims nowhere.

To aim another way:

- For a camera of your own, give the default one: `new LegacyPointerInput { Camera = camera }`, or
  `InputSystemPointerInput`.
- For a gamepad's reticle or a controller's ray, write an `IForgeInput`.
- Put either in with `ForgeInput.Use(input)`.

#### Targets handed in the data

An activation that already holds its target, under TargetReal, or its point, under Position, takes it with no click, as
an AI's does. This holds unless the task's **Take Target From Data** is off.

A handed target the task can't take, out of range or failing its requirements, fails the targeting at once.

#### Prompts

While a click targeting task waits, its slot's `IsTargeting` is true. The task's `Prompt` gives the words to ask with,
as "Click a target", for a game's own screen. The Creator Lab and the Demo show them this way
([CREATOR_LAB.md](CREATOR_LAB.md)).

### Auto targeting

**Auto targeting** takes the entities within a range. Its tasks are Auto Target and Auto Target By Affinity.

Each task picks its targets in three steps:

1. It takes the entities within its **Range**, or within its ability's Cast Range when all of these hold:
   - the ability has a Cast Range;
   - the range is measured from the caster;
   - **Use Cast Range** is on.
2. It sorts them by its **Mode**.
3. It keeps up to **Max Targets**, or every one at 0.

Each is a target of the activation. The first, the nearest for Nearest, is its primary.

- A Target row and the Target Checks check the primary.
- Apply Effects and Apply/Remove Effects reach each target. They do so in the project's resolve order unless their
  **Use Resolve Order** is off ([EFFECTS.md](EFFECTS.md#resolve-order)). So an area ability is Auto Target and then
  Apply Effects.
- **Auto Target From Data** with **Primary Only** off takes each target its key holds.

#### Finding entities

Each entity is found by its position, on either hub, with no collider needed.

| Property | Description |
| --- | --- |
| **Around** | Where the range is measured from: the caster, the activation's target (as an AI hands it, or a task before it in a Composite picks it), or its point. |
| **Flat** | On by default: measures along the ground. Off, it measures a sphere. |
| **Use Physics** | Finds entities with physics instead, by collider on its **Detection Layers**, as it did. A runtime entity is still found by its position. |
| **Detection Layers** | The layers its physics search finds colliders on. They show under Use Physics alone. |
| **Takes** | Whom each takes, by how each stands to its caster. |

**Takes** offers its **Enemies**, its **Allies** (itself among them), its **Other Allies**, **Self**, **All But Self**
or **Everyone**. Auto Target takes All But Self to start, and Auto Target By Affinity its Enemies.

## Stages

The Stages section sets what the ability does once it has started.

| Property | Description |
| --- | --- |
| **Asset Loaders** | Run once per activation, after targeting. Each loader adds assets to the activation's data under a tag. |
| **Cancelled By** | Abilities that cancel this one as they activate. See [Cancelling and blocking](#cancelling-and-blocking). |
| **Blocks** | Abilities that can't activate while this one runs. See [Cancelling and blocking](#cancelling-and-blocking). |
| **Stages** | The ability's stages, which run one after another. See [Stage properties](#stage-properties). |

A Set Effects loader, for example, adds effects under `Effects`. Code-driven tasks read them from there.

### Stage properties

Each stage starts all its tasks together, and the next stage starts when it ends.

| Property | Description |
| --- | --- |
| **Completion** | All ends the stage when every task has finished.<br>Any ends it when the first task finishes, and cancels the rest.<br>A stage with no policy set behaves as All. |
| **Apply Usage Effects** | Applies the ability's cost and cooldown in this stage. They apply once per activation, in the first stage that does this. |
| **At Stage Start** | Under Apply Usage Effects. On, it applies them as the stage starts; off, as it ends, and only if it succeeded. |
| **Tasks** | The stage's work, such as Apply Effects, Delay or Run Sequence. See [Tasks](#tasks). |
| **Condition** | When the stage runs, by a tag. See [Stage conditions](#stage-conditions). |
| **Cancelled By** and **Blocks** | The stage's own lists, which hold while it runs. See [Cancelling and blocking](#cancelling-and-blocking). |

If no stage applies usage effects, the cost and cooldown apply as the ability starts, after its targeting.

In the stage list:

- A stage folds to one line of what it holds, as "2 tasks · All · Applies usage effects".
- It shows Critical on its row while a task commits the ability to it.
- The list's **−** removes the selected stage.
- A stage keeps its fold as others are removed or dragged.

### Tasks

A stage's **Tasks** are its work, such as Apply Effects, Delay or Run Sequence.

To add a task:

1. Click the list's **+**. It adds an empty task.
2. Pick a kind for it from the searchable picker, in which each task sits under its category (see
   [Built-in tasks](#built-in-tasks)).

A task's numbers, as Delay's milliseconds and Channel's duration, are numbers that can scale by level. They're read as
the task runs, at its ability's level. A stage that holds a critical task is critical.

#### Your own tasks in the picker

A task of your own names its submenu with `[ForgeMenu("Effects", Description = "...")]`.

- A slash nests one submenu in another.
- A class's path holds for those made from it.
- A task that names no submenu sits after the submenus.
- A task's Description is what the picker's Context says while it's highlighted.

#### Run Sequence

Run Sequence runs the sequence a static method makes. You pick it in **Sequence** by name, from a searchable dropdown of
the project's `[TaskSequenceMethod]` methods; where more than one class holds them, each class's sit under its name.

For an ability built in code, it runs a fresh run of the sequence its `Definition` describes, which
`TaskSequenceBuilder` makes.

Its **Clock** picks the clock that sequence runs by. The ability's other tasks keep the caster's.

| Option | Clock |
| --- | --- |
| None | The sequence's own, or the caster's. |
| Caster's | The caster's. |
| Game's | The game's. |
| Unscaled | Runs while the game is paused. |
| Scaled | A pace of its own. |

### Built-in tasks

PlayForge's own tasks sit under these categories in the task picker.

| Category | Tasks |
| --- | --- |
| **Effects** | Apply Effects, Apply/Remove Effects, **Area Effect** and **Apply To Caster**. |
| **Asset Loaders** | The Set tasks. |
| **Process** | Create Mono Process, Run Process and Disjoint. |
| **Sequence** | Run Sequence and Inject. |
| **Waits** | Delay, Channel, **Wait For Event**, **Wait For Tag** (gained or lost) and **Wait For Attribute** (crossing a value). |
| **Events and Tags** | **Send Event**, **Grant Tags** and **Cancel Abilities**. |
| **Movement** | **Dash** and **Blink**. |
| **Items** | **Consume Item**. |
| **Cues** | **Play Cue**. |
| **Utility** | Unity Event. |

What some of them do:

- **Area Effect** applies its effects to each it takes within its radius of its caster, its target or its point.
  - Where the activation has neither a target nor a point, it measures from its caster.
  - It finds them by position on either hub, or with physics.
- **Wait For Event**, **Wait For Tag** and **Wait For Attribute** wait on the caster or the target. Each has a
  **Timeout**, and a **Record As** for a later stage's Condition.
- While a Wait For Event waits for one of the core's own events, the core sends it.
- **Send Event** sends an event to the caster or the targets.
- **Grant Tags** grants loose tags while its stage runs.
- **Cancel Abilities** cancels the caster's running abilities by tag, except one in a critical stage.
- **Dash** and **Blink** tell a mover of the game's own: an `IForgeMover` on the caster's object, as the Creator Lab's
  units have.
- **Consume Item** is heard by its item system's Item Consumed.
- **Play Cue** plays its cues at the stage's moment, on the caster, on the target or at the point.
- Apply Effects' **Record Landed As** tags the activation's data as an effect lands on a target, for a later stage's
  Condition.

Dash, Blink, Area Effect, Apply To Caster and Consume Item were the Demo's own.

### Stage conditions

A stage's **Condition** sets when the stage runs, by a tag.

- With no tag, the stage always runs.
- Otherwise it runs only while the tag is there, or, with **When Missing**, only while it isn't.
- A stage whose condition fails is skipped.

| Property | Description |
| --- | --- |
| **Looked For On** | Where the tag is looked for.<br>The Activation: the activation's data, as Record Landed As fills it.<br>Caster or Target: that entity's tags. |
| **When Missing** | Runs the stage only while the tag is missing. |

Both show once the condition names a tag.

Two stages that name one tag, one wanting it and one wanting it missing, branch: a jab's hit recovery and its whiff.

> [!TIP]
> A critical stage that's skipped lets the claim go as the next stage starts. So put a committed whiff before the hit's
> recovery, and the claim goes as that recovery starts.

### Cancelling and blocking

An ability's **Cancelled By** and **Blocks** lists name other abilities, by asset tag or a context tag, or a child of
one.

| List | What it does |
| --- | --- |
| **Cancelled By** | The abilities it names cancel this one as they activate. The ability is interrupted, then the other starts. |
| **Blocks** | The abilities it names can't activate while this one runs. |

- Each stage that names none of its own takes the ability's lists, and targeting takes its Cancelled By too.
- A critical stage is committed, so nothing cancels it.
- A blocked activation is refused, unless When Busy is Queue: then it waits in the input buffer for the block to lift.

A stage's own **Cancelled By** and **Blocks** hold while it runs. A heavy attack's recovery can name the specials that
cut it short, and its windup none.

### Critical stages

A stage that holds a critical task is critical: it commits the ability to the stage.

- The stage list shows Critical on the stage's row.
- A critical stage is committed, so nothing cancels it.
- The activation keeps its claim until its last critical stage ends. With no critical stage, it lets the claim go as the
  stages start.
- A critical stage that's skipped lets the claim go as the next stage starts (see
  [Stage conditions](#stage-conditions)).

## Requirements

Requirements are what an activation must pass. If one fails, the ability doesn't run.

| Requirement | What it checks |
| --- | --- |
| Tag requirements | The tags of the caster, of the caster while the ability runs, and of the target. See [Tag requirements](#tag-requirements). |
| **Caster Checks** | The caster, before targeting and again after it. See [Caster Checks and Target Checks](#caster-checks-and-target-checks). |
| **Target Checks** | The target, after targeting. See [Caster Checks and Target Checks](#caster-checks-and-target-checks). |
| Crowd control | Rules that hold for every ability at once. See [Crowd Control](#crowd-control). |

### Tag requirements

The **tag requirements** table holds tag checks, a row for each side.

| Row | What it checks |
| --- | --- |
| **Caster** | The caster, with the Caster Checks. |
| **While active** | The caster, for as long as the ability runs. When it fails, the ability is interrupted, and while it fails, the ability can't activate. |
| **Target** | The target, the same way, after targeting. |

For example, a channel whose **While active** row must not have `State.Stunned` ends as a stun lands.

Each row has two cells:

| Cell | Description |
| --- | --- |
| **Requires** | The tags the entity must have. |
| **Must not have** | The tags it must not have, such as `State.Stunned`, or None. |

- A row passes when the entity has every tag it requires and none it must not have. So an empty row always passes.
- Click a cell to edit its list under the table, with the row's **Template** and **Sync With Template** (see
  [EFFECTS.md](EFFECTS.md)).
- Each entry is a tag query: a **Tag**, a **Match** mode such as Include Children, and a comparison with a number.
  Greater Than 0 means "the tag is present".

> [!NOTE]
> The section's Clear button empties the tag requirements and leaves the checks, since an ability without them would
> skip its cost and cooldown.

### Caster Checks and Target Checks

An ability's checks are rules it runs on its caster and on its target. You pick each check's type from a list.

| List | When it runs | Defaults | Offers |
| --- | --- | --- | --- |
| **Caster Checks** | When the ability is asked to activate, before targeting, and again after targeting. | Cooldown, Cost and Is Alive. | The caster's types, and those that check either side. |
| **Target Checks** | After targeting. If targeting found no target, they are skipped. | Is Alive. | The target's types, and those that check either side. |

A check a component stands behind shows under its list, locked, as Check Range does under the Target Checks while the
ability holds a Cast Range.

- No list holds it: it comes and goes with its component, which enforces it.
- The section's line counts it among the checks.

### Check types

PlayForge's check types are grouped below by the list that offers them.

#### Caster check types

| Check | What it does |
| --- | --- |
| **Cooldown** | Passes when the ability isn't cooling down, or, with its charges counted (see [Charges](#charges)), has one in hand. |
| **Cost** | Passes when the ability has no cost, or the cost would leave each attribute it spends at 0 or more, and its Attribute Costs can be paid. The caster must have each attribute. |
| **Ability Level** | Compares the ability's level with **Level**, using **Comparison**, as a form it unlocks at a rank. It starts at 2 or more. |

#### Check types for either list

These check the caster among the Caster Checks, and the target among the Target Checks.

| Check | What it does |
| --- | --- |
| **Is Alive** | Passes when the checked entity isn't dead. |
| **Attribute** | Compares the checked entity's Current value of **Attribute** with **Value**, using **Comparison**. With **Share Of Base**, it compares its share of its base, from 0 to 1, as "below half its health". |
| **Tag Weight** | Compares the checked entity's weight of **Tag** with **Weight**, using **Comparison**, as combo points or stacks. It starts at 1 or more; an entity without the tag holds it at 0. |
| **Has Effect** | Passes when the checked entity has **Effect** on, or any effect tagged **Tagged** or under it, by its asset tag, a context tag or a tag it grants. With **Without**, it passes when it has none of it; naming neither, it passes. |
| **Holds Item** | Passes when the checked entity holds **Item**, or, with **Equipped**, wears it. Naming none, it passes. |
| **Condition** | Passes while its condition, **When**, holds: a tag, an attribute, a level, a comparison of numbers, or a condition of your own (see [SCALERS.md](SCALERS.md)). With none, it passes. |

A Condition check reads its condition as an effect's requirements and an On Hit read theirs. It's read against the
activation, with the checked entity as its target, so among the Caster Checks its target is the caster.

#### Target check types

| Check | What it does |
| --- | --- |
| **Affiliation** | Passes when the target stands to its caster as **Target** says, as a search's Takes does. |
| **Line of Sight** | Passes when nothing on the **Blocking** layers blocks a line from the caster to its target. The line runs **Height** above each, so the ground under them doesn't block it. |
| **Facing** | Passes when the target lies within **Angle** in front of its caster. With **Behind Target**, it passes when the caster stands within the angle behind its target, as a backstab wants. |
| **Check Range** | The Cast Range component's check, which no list offers. It shows while the ability holds a Cast Range, saying its range, and passes, as the component enforces the range (see [Cast Range](#cast-range)). |

Affiliation's **Target** offers these choices:

| Option | Passes for |
| --- | --- |
| **Enemies** | A target that shares none of the caster's sides. |
| **Allies** | A target that shares one, the caster itself among them. |
| **Other Allies** | An ally, leaving the caster out. |
| **Self** | The caster alone. |
| **All But Self** | Anyone else. |
| **Everyone** | Anyone. |

A caster with no sides takes anyone as either.

- **Line of Sight**: 3D physics colliders block it. Triggers and the two ends' own colliders don't.
- **Facing**: the caster and the target each face as their targeting packet does, along the ground. An **Angle** of 90
  takes 45° to either side, and 360 anywhere.

### Failed checks

A failed check refuses or ends the activation, and says why.

| Check | What a failure does |
| --- | --- |
| Caster Check | Refuses the activation as the kind its `RefusedAs` says: Cooldown, Cost or Dead for the Cooldown, Cost and Is Alive checks, which name none, and Rule, naming the check, for any other. |
| Target Check | Ends the activation after targeting, told as Target, naming the check (see [Refusals](#refusals)). |

Each says why in `Validate`'s `error`, which the refusal carries as its `Detail`, as
"Its caster's Mana, 20, can't pay 30".

### Writing your own check

A check of your own implements `IAbilityValidationRule`.

| Member | What it does |
| --- | --- |
| `Validate` | Says whether the activation passes, and why not. |
| `GetName` | Names the check. |
| `RefusedAs` | The kind its refusal of the caster is told as, Rule unless it says otherwise. So an ammunition check can say Cooldown, as the built-in does. |
| `[ForgeMenu]` | Names and describes it in the list. |
| `[ValidationSide]` | Says which list offers it, Caster or Target. Without it, both do. |

A component stands behind a check by overriding its `TargetCheck` or `CasterCheck` with an
`AbstractComponentValidationRule<T>` of its own kind. The check shows under the list while the ability holds the
component, and the component enforces it, in `CanStart`.

### Crowd Control

**Crowd control** holds for every ability at once, so no ability has to list Stunned among the tags it must not have.

A **Crowd Control Rules** asset (Create → PlayForge → Crowd Control Rules) holds its rules. Each rule has these
properties:

| Property | Description |
| --- | --- |
| **Tag** | The tag the caster carries, as Stunned. |
| **Stops** | What the rule stops: **Every Ability**, as a stun, or **Abilities Tagged**. |
| **Ability Tags** | Under Abilities Tagged, the rule stops the abilities whose asset tag or a context tag is one of these or under it. So Silenced stops spells and Disarmed attacks. |

An entity's abilities follow its own **Crowd Control** ([ENTITIES.md](ENTITIES.md#abilities)), or, where it names none,
its Forge Project's ([ATTRIBUTES.md](ATTRIBUTES.md#roles)).

While the caster carries a rule's tag, or one under it:

- An ability the rule stops is refused with **Crowd Control**, the refusal's `Rule` naming the tag.
- One that runs is interrupted as the tag lands, as a stun breaks a channel.

An ability's **Ignores Crowd Control** (see [Activation](#activation)) opts it out of the rules it names. Code asks with
`rules.Stopping(ability, tags)`, which gives the first rule that stops the ability, or null.

## Components

An ability's components are features it carries, each a class with hooks into its life, added with the list's **+**. A
kind that makes no sense twice is offered only while the ability holds none of it.

| Component | What it does |
| --- | --- |
| [Cast Range](#cast-range) | The one range the ability has. |
| [Charges](#charges) | How many uses the ability holds. |
| [Toggle](#toggle) | An effect the ability turns on and off on its caster. |
| [Turn To Face](#turn-to-face) | How the ability turns its caster to face its target or its point before its first stage. |
| [Auras](#auras) | An effect on those near its holder, while the entity knows the ability. |
| [Cues](#cues) | What the ability shows and sounds at its moments. |

To write a component of your own, see [Component hooks](#component-hooks), [Writing a component](#writing-a-component)
and [Components in code](#components-in-code).

### Cast Range

A **Cast Range** component is the one range the ability has: how far from its caster its target or its point may be.

| Property | Description |
| --- | --- |
| **Range** | How far from its caster the target or point may be, a number that can scale by level. 0 reaches anywhere. |
| **Flat** | The range is measured along the ground unless Flat is off. |

How the range is enforced:

- An activation handed a target or a point beyond it, under TargetReal or Position, as an AI's is, is refused as
  **Range**. So nothing of it is paid, and an AI can walk closer and ask again.
- Targeting that picks a target beyond it, whichever task picked it, ends the activation before its stages, told as
  **Range** too.

Where the range is used:

- The auto-targeting tasks measured from the caster, and Select GAS Target In Range, search within it where their
  **Use Cast Range** is on, as it is by default. Their own range is set aside.
- The Creator Lab's units attack from their attack's Cast Range. Its bots cast an ability once their foe is within its
  Cast Range, where it has one ([CREATOR_LAB.md](CREATOR_LAB.md)).

It's the ability's reach, an `IAbilityReach`. Code reads it through `ForgeReach`, as
`ForgeReach.TryRead(caster, ability, out range)`, and a reach of your own takes its place in each of these.

While the ability holds it, its Target Checks show its **Check Range**, locked, saying the range (see
[Requirements](#requirements)). An ability holds one at most.

### Charges

A **Charges** component says how many uses the ability holds, each use spending a charge.

| Property | Description |
| --- | --- |
| **Charges** | How many uses the ability holds, a number that can scale by level. With 1 it is an ordinary cooldown. |
| **Recharge** | How spent charges come back: **Each Charge** (the default), **All At Once**, or a recharge of your own. |

With more than one charge, the Cooldown check refuses the ability only with none in hand.

| Recharge | What it does |
| --- | --- |
| **Each Charge** (default) | Starts the Cooldown for each charge spent, a container of its own whatever the effect does when applied again. Each brings its charge back as it ends, so three spent a second apart come back a second apart. |
| **All At Once** | Starts the Cooldown only as the last charge is spent, and its end brings them all back. |

#### A recharge of your own

Write a recharge of your own by extending `AbstractChargeRecharge`:

- Override `InHand`: how many are in hand.
- Override `Spend`: what a use does.
- Use `AbilityCharges`' `Max`, `Spent`, `Cooldowns` and `StartCooldown`.

One of your own counts even a single charge, unless its `CountsASingleCharge` says not.

#### Charges in code

Charges are the ability's cooldown model (see [Components in code](#components-in-code)). So code reads what's in hand
of any ability as `spec.CooldownModel.InHand(spec)`.

The component itself, `spec.Find<ChargesAbilityComponent>()`, holds its **Charges** and **Recharge**. Its
`ChargesOf(spec)` gives the ability's charges on its caster, an `AbilityCharges`.

#### Charges in the editor

- The section's line counts them, as "3 charges".
- A warning under the Components list, as in the Problems view, says where built-in charges have no Cooldown to bring
  them back, so they never run out.

An ability holds one at most.

### Toggle

A **Toggle** component holds an **Effect** the ability turns on and off on its caster, as a mana shield.

| Property | Description |
| --- | --- |
| **Effect** | The effect the ability turns on and off on its caster. |

| Use | What happens |
| --- | --- |
| Used while off | It puts the effect on where its usage would be paid, paying its Cost and Attribute Costs and starting no cooldown. |
| Used again, while on | It takes the effect off, paying nothing and asking no cost, and starts its Cooldown, or spends a charge. |

- Neither its cost nor its cooldown stops it turning off.
- It counts only the effect it put on. One another put there leaves it off.
- A purge counts its effect a Toggle's, as a death that keeps toggles keeps it.
- As its holder forgets the ability, the effect comes off; it stayed on.
- The Problems view warns where it toggles an Instant effect, which is never on.

Code asks the component, `spec.Find<ToggleAbilityComponent>()`. Its `IsOn(spec)` says whether its effect is on, and its
**Effect** is the effect.

It's written on the components' hooks alone: it applies the usage in its ability's place, waives the cost and the
cooldown while it's on, and claims its effect. An ability holds one at most.

### Turn To Face

A **Turn To Face** component says how this ability turns its caster to face its target or its point before its first
stage. It does so in place of its entity's Turns To Face ([ENTITIES.md](ENTITIES.md#turns-to-face)).

| Property | Description |
| --- | --- |
| **Turns** | Whether the ability turns its caster. Off, it doesn't turn at all, as a cast behind its caster doesn't. |
| **Turn Speed** | How fast it turns, in degrees a second of its own time. 0 faces at once. |
| **Start Angle** | How close to facing its target, in degrees, it turns before the first stage starts. |
| **Facing Angle** | How close to facing its target it goes on turning once the stage has started. |
| **If Too Long** | What happens when reaching the Start Angle takes longer than Max Wait: No Limit, Start Anyway or Cancel. |
| **Max Wait** | How long, in seconds of its own time, it waits to reach the Start Angle, where If Too Long sets a limit. |

The Target Checks come before the turn. So a Facing check that wants its target in front of its caster reads the
caster's facing before it turns. An ability holds one at most.

### Auras

An **Aura** component holds an effect on those near its holder, while the entity knows the ability, as a banner that
inspires the allies round its bearer.

| Property | Description |
| --- | --- |
| **Effect** | The effect it holds on each it takes while they're near. |
| **Radius** | How far it reaches from its holder. |
| **Flat** | Measures the radius along the ground; off, a sphere. |
| **Takes** | Whom it takes, by how each stands to its holder, its holder too or not. |
| **Interval** | How often it checks who's near. |

For how auras work, see [EFFECTS.md](EFFECTS.md#auras).

### Cues

A **Cues** component plays what the ability shows and sounds at its moments, on its caster.

| Property | Description |
| --- | --- |
| **On Activate** | Plays as an activation starts. |
| **On Commit** | Plays as it pays its cost and starts its cooldown. |
| **While Active** | Lasts from its start to its end. A Prefab cue here lasts until the activation ends. |
| **On End** | Plays as it ends, however it ended. |
| **On Refused** | Plays as one is refused, nothing of it paid. |

Each list holds cues as an effect's does ([EFFECTS.md](EFFECTS.md#cues)), a Cue Tag among them. A stage's exact moment,
as a sword's arc, plays through a **Play Cue** task. An ability holds one at most.

### Component hooks

A component hears its ability's life through hooks, each a no-op unless the component overrides it.

| Hook | What it does |
| --- | --- |
| **Granted** | Hears the ability granted to an entity. |
| **Removed** | Hears the ability removed from it. |
| **CanStart** | Is asked whether an activation may start. A reason it gives refuses the activation, named by it, as the kind its **RefusedAs** says, Rule unless it says otherwise. |
| **CanTakeTarget** | Is asked whether the target its targeting picked may be taken. A reason it gives ends the activation there, before anything of it is paid, told as the kind its **RefusedAs** says. |
| **WindUp** | Is waited on as an activation winds up, after its target checks and before its cost, as a turn to face its target is. |
| **Start** | Hears an activation start. |
| **Commit** | Hears the activation commit its cost and cooldown. |
| **End** | Hears the activation end, by how it ended: done, cancelled or failed. |
| **ApplyUsage** | As the activation's usage applies, it may apply it in the ability's place, as a Toggle does. It composes its own from the spec's `PayCosts` and `SpendCooldown`. |
| **Waives** | May waive the Cost and Cooldown checks while it says so. |
| **CategoryOf** | Claims the categories of the effects its ability puts on for a purge, as a Toggle claims its effect a toggle's. |

### Writing a component

A component of your own derives from `AbstractAbilityComponent` and overrides the hooks it needs, each a no-op
otherwise.

- It's the ability's, shared by everything that holds it. So what it keeps for one entity's ability it keeps through its
  context: `ctx.GetState<T>(this)`.
- **Describe** says what it does in one line with its numbers, as a Cast Range's "Range 600". The Components section's
  line, The Forge's summary and the ability's breakdown say it beside its name.
- **DescribeUsage** says what it adds to the ability's use, as Charges' "3 charges". The Activation section's line says
  it after its cost and cooldown.

#### Editor rules

A component's rules in the editor derive from `ForgeComponentRule<T>`, in the editor's assembly, with a public
constructor that takes nothing.

- The Problems view runs each on every asset that holds a component of its kind.
- The ability's inspector says what it finds under its Components list, as it says the Charges'.

### Components in code

Code finds a component by what it does, not by its class. `Find<T>()`, on an ability, its spec, an effect, an item and
an entity, gives the first of its components that is a `T`, a kind or an interface it implements.

| Capability | What it is | Implemented by |
| --- | --- | --- |
| `IAbilityReach` | An ability's reach. | Cast Range |
| `ICooldownModel` | How an ability's uses come back. | Charges |

#### Reach

The targeting tasks, the Creator Lab's bots and your own code read an ability's reach through `ForgeReach`. So a
component of your own that implements `IAbilityReach` is asked as a Cast Range is.

#### Cooldown model

An `ICooldownModel` says how many uses the ability holds, how many are in hand, what a use spends, and why the Cooldown
check refuses it with none.

- An ability with none has `SingleCooldown`: one use, back as its Cooldown ends.
- The Cooldown check asks it, the ability spends through it, and a HUD reads it as `spec.CooldownModel`.

#### Component state

A component's state that needs its holder, as a Charges component's charges need their ability, is kept with
`spec.ComponentStates.TryGet` and `Keep`.

#### No shortcuts

The built-in components are written on this API alone, and PlayForge's core names none of them. So a component of your
own reaches as far as they do.

None has a shortcut: code reads one through `Find<T>()` or the component itself. For example,
`spec.Find<ToggleAbilityComponent>()?.IsOn(spec)` says whether a Toggle is on.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
to the entity when it learns the ability, and removed when the ability is removed.

**Name** labels the group. It holds four lists:

| List | What its workers do |
| --- | --- |
| **Attribute** | Run before or after an attribute changes, for example to reduce incoming damage. |
| **Impact** | Inspect the changes this entity deals. |
| **Tag** | React when the entity's tags change. |
| **Analysis** | Examine the frame at its end, for example to detect death. |

- Each list's **+** adds an empty worker to pick a kind for.
- The section's import button copies a worker group from another asset.

### On Hit

**On Hit**, above the workers, holds the effects the entity's hits apply while it knows the ability, as a passive's.

Its entries are an item's ([ITEMS.md](ITEMS.md#on-hit)). Each holds:

- an attribute;
- the effects, to the one hit or the holder;
- whether it applies only on a kill;
- impact types;
- a condition.

### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, while the entity knows the ability. They change
either:

- its holder's own, as a boon that adds a burst to its holder's every Chill;
- or those landing on it, as a cloak that halves the stuns its wearer takes.

Each entry is a rule of its own or names a shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).

### Lifesteal

An ability gives its holder lifesteal by an effect that raises the holder's **Lifesteal**, as a passive applies it to
its caster.

- **Lifesteal** is the attribute the project's Lifesteal worker names as its Share.
- The Lifesteal worker heals by it ([WORKERS.md](WORKERS.md#lifesteal)).

For example, a vampiric mask's Infinite effect of +0.15 Lifesteal heals its wearer 15% of each blow it deals.

The ability's Lifesteal list is gone.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag) and a value of a kind.

The kinds are:

- PlayForge's: Float, Int, String, Bool, Tag, Attribute, Effect, Ability, Entity, Object, MonoBehaviour or
  ScriptableObject.
- A kind of your own: a `[Serializable]` class made from `LocalDataValue<T>`, as a point or a colour. The row's kinds
  offer it beside PlayForge's.

A row saves its own value alone. Object fields take project assets, not scene objects.

Code reads a value with `ability.TryGetLocalData(key, out var data)`. The first row with that key wins.

Code then reads the value by its type, `data.TryGet(out float range)` or `data.TryGet(out MyPoint point)`. A number, a
Float or an Int, which can scale, reads with `data.Value.TryReadNumber(context, out float number)`.

A tag check of your own, extending `AbstractTagValidationRule`, reads its tag's value with `GetValue`. The value is the
tag's weight on the entity it checks or, where that entity holds none of it, the ability's Local Data under the tag.

## Used By

The Used By section lists the PlayForge assets that refer to this ability, each with its type icon. They include an
entity that starts with it, an item that grants it, or a cost linked to it.

Click a row to select the asset. The list follows the project as it changes.
