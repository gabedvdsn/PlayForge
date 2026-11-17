# PlayForge — Abilities

An **Ability** asset describes one thing an entity can do, such as a fireball, a dash or a heal. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy. An entity learns an ability from its Entity
asset's Starting Abilities, from an equipped Item, or from code (`AbilitySystemComponent.GiveAbility`). It keeps the
ability until the ability is removed.

Each time the ability activates, the GAS checks that it may run, lets a targeting task pick a target, then runs the
ability's stages of tasks. The ability's cost and cooldown are gameplay effects, assets that change an entity, which
it applies to its caster. Abilities run as processes under `ProcessControl`, so the scene needs one.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fire Ball gives `Ability.FireBall`. The entity carries this tag while it has the ability, and keeps
the ability's level under it. Each section below matches the inspector section of the same name.

The inspector's header shows the ability's one line, as The Forge's Summary column does: each effect it applies,
by its first impact across its levels, then its costs and its cooldown, as "-450/-600/-750 Health; 280/420/680 Mana;
70/60/50 s". A number that reads what the editor can't give it, as the target, is marked `*`.

## Identity

- **Name**: the ability's display name. It also gives the asset tag, so renaming the ability changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI. The inspector header shows it.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name. If the stored tag differs from the one the Name gives, it shows in warning
  text with an **Update** button that stores the new one.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the ability's icon. Without one, the
  first texture is used.

## Leveling

An ability has its own level on each entity that learns it, kept under the ability's asset tag. Effects the ability
applies take this level, so their scalers can pick per-level values. A scaler is a value source, such as a table with
one value per level. A level provider is an asset whose level range another asset follows.

- **Link Mode**: Standalone (default) keeps the ability's own range. Linked To Provider takes the range from a level
  provider instead.
- **Starting Level** (default 1) and **Max Level** (default 4): the ability's own range. An entity's Starting Abilities
  begin at the Starting Level, and the ability's level never goes below it.
- **Level Provider**, while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set.
  Right-click the field to pick from one type. A link that would make a loop is refused, with an error in the Console.
  While no provider is set, a warning says the ability uses its own range. While linked, Starting Level and Max Level
  show the range in use, read-only.
- **Ignore When Level Zero** (default on): the ability can't activate while its level is 0. With a Starting Level of 0,
  the entity must raise the ability's level before it can use it.
- **Link Cost & Cooldown** links this ability's cost and cooldown effects to it, so their per-level values follow its
  range. **Unlink Cost & Cooldown** removes the links that point at this ability. Each asks first, and one Undo takes
  it back.

Code changes an ability's level through the entity's GAS, with `SetLevel` or `ModifyLevel` and the ability's asset
tag. An ability that an Item grants starts at the item's level, kept within this range.

The link serves the inspector and the tooltips that `AbilityInspector` builds. At runtime, the ability's level stays
within its own Starting Level and Max Level, even while it is linked.

## Tags

- **Context Tags** describe the ability, such as `Spell` or `Melee`. The changes made by effects this ability applies
  carry them as context, and workers can filter by them. They are not applied to the entity.
- **Passive Tags** are applied to the entity while it has the ability.
- **Active Tags** are applied to the entity while an activation of the ability is running.

## Activation

This section is how the ability starts. One activation goes through these steps:

1. The caster's tag requirements and Caster Checks are checked (see Requirements), and the ability's level (see
   Leveling).
2. The Targeting task runs, if there is one.
3. If targeting found a target, every check and requirement is checked again, the target's included.
4. It winds up: what its components and its entity's wait for before it starts, as a turn to face its target (see
   Turn To Face). An interrupt here cancels it, and its cost and cooldown, paid after, aren't.
5. The Asset Loaders run (see Stages).
6. The Stages run, one after another.

These steps start inside the call that asks for the ability, `AbilitySystemComponent.TryActivateAbility`, and run until
a task waits. So an ability whose tasks all finish at once has ended, and its callbacks have fired, by the time the call
returns. An ability asked for again while that call still runs, from its own callbacks or from another ability's that
it started, starts in the next frame instead. When the entity ends, its running abilities end with it, critical or not,
and it takes no more activations.

Code asks for an ability by its slot, with a request from `CreateActivationRequest`, by its asset
(`TryActivateAbility(ability)`), or by a tag (`TryActivateAbility(tag)`). A tag activates the first ability, by slot,
whose asset tag or one of whose context tags is that tag or under it. Each form can say why it was refused, through an
`out ActivationRefusal`: its `Reason`, such as Cooldown, Cost, Requirements, Blocked or Busy, and for another caster
check, that check's name in `Rule`; for Crowd Control, `Rule` is the rule's tag, as Stunned. Where a check or a
component refused it, its `Detail` says why in the check's own words, as "Its caster's Mana, 10, can't pay 50". The
ability system's `OnAbilityRefused` callback tells each refusal as it happens.
A request that waits in the input buffer isn't refused, and isn't told again as the buffer retries it. A check on the
target comes after targeting and ends the activation instead, nothing of it paid: the request said nothing was refused,
as the activation had started, so the callback tells it, as **Target**, `Rule` naming the check and `Detail` saying why.
A component that refuses the target its targeting picked, as a Cast Range refuses one beyond it, is told the same way, as
the kind its `RefusedAs` says, **Range** for a Cast Range, its reason in `Rule` and `Detail`.

The request's data can carry magnitudes for the effects the ability applies: `data.SetByCaller(tag, magnitude)`, which a
Set By Caller scaler on each effect reads (see [EFFECTS.md](EFFECTS.md)). So a caller that knows the damage as it casts
passes it in, and one effect serves every strength.

- **When Busy**: what happens when the ability is asked to activate while another ability is busy. An activation is
  busy, or holds its *claim*, while its targeting and its critical tasks run. A critical task is one that must not
  overlap another ability's critical work, such as an attack animation. The inspector marks critical tasks and stages
  Critical. An ability only waits on abilities that use the same choice.
  - **Entity's Policy** (default): use the entity's When Busy, set in the Entity asset's Abilities section.
  - **Activate Anyway**: start at once. It is refused only when it has critical tasks and another Activate Anyway
    ability with critical tasks holds its claim.
  - **Refuse**: start only when no Refuse ability holds its claim. Otherwise the request is refused.
  - **Queue**: when a Queue ability holds its claim, or a running stage blocks this ability (see Stages), wait in the
    input buffer for up to **Buffer Window** seconds of the caster's time, then let go. The buffer holds one request
    for each ability, and a new press replaces it, with its window afresh. As a claim is released, and each frame, the
    request with the highest **Buffer Priority** that can start does, and of equals, the newest. While the entity is
    paused or its time is stopped, as in a hit-stop, the windows wait too. A request can name a window of its own
    (`CreateActivationRequest`'s `bufferWindow`).

  Whatever the choice, an ability can't start again while its own activation holds its claim.
- **Buffer Window** (default 0.3 s) and **Buffer Priority** (default 0): the input buffer's, under Queue, as above,
  each a number field read at the ability's level. A window of 0 doesn't wait, so the request is refused. They show under Queue and under the entity's policy, which may be
  Queue, and are hidden under the others.
- **Activate At Start**: when the ability is one of the entity's Starting Abilities, it tries to activate once, as the
  entity's GAS starts. An ability that an item or code grants later doesn't try it.
- **Triggers**: what sets the ability off on its own. A **Gameplay Event** trigger activates it when code sends the
  entity an event under the trigger's tag, or a tag under it, with `SendGameplayEvent(tag, data)`, and the ability gets
  the event's data, its target among it. A **Tag Added** trigger activates it as the entity gains the tag, or one under
  it, that it didn't have; more of a tag it holds sets off nothing. Each trigger tries the ability as a request would,
  so its checks and its When Busy still hold.
  - **The core's own events**, sent only where an ability listens for them: **Impact Dealt**, to the dealer, and
    **Impact Taken**, to the one hit, as an impact lands between two entities; **Kill**, to the killer, and **Died**, to
    the one who died, its killer the source of the last blow it took; **Revived**, to a staying entity as it revives;
    **Blocked** and **Evaded**, to one that stops an effect by a Block or Evade stage, and **Hit Blocked** and **Hit
    Evaded**, to its dealer.
    Their data hold the instigator as Source, the entity it was done to as Subject, and an impact as Impact.
  - For an impact event, a trigger takes only impacts on its **Attribute**, a **Sign** (Loss or Gain), and its
    **Impact Types**. Its **Chance** rolls once for each event: Counter Helix's 20%, its **Chance Roll** pseudo-random
    or lucky ([EFFECTS.md](EFFECTS.md#requirements)). Its **Target** is the ability's
    own targeting, as an area spin finds the enemies round it; the event's **Instigator**, as a counterattack; or its
    **Subject**, as an on-hit ability targets the one hit. An ability set off by Died runs on a dead entity only if its
    rules don't ask that its caster be alive.
  - The Problems view notes a Gameplay Event trigger on an event neither the core nor an asset in the project sends, as
    a threshold's Event or a stop stage's does, as only code can send it; and warns of an Impact Dealt or Impact Taken
    trigger's impact type that no effect deals, which never matches.
- **Chance to Activate**: how likely an activation is to start, from 0 to 1, rolled once for each, a player's
  or a trigger's, after its other checks; a miss is refused with Chance. **Activation Roll** makes it pseudo-random or
  lucky, and shows for a chance that can miss. **Proc Coefficient**: what its effects' hits carry for on-hit effects, where an effect sets none of its own.
- **Ignores Crowd Control**: the crowd control rules the ability ignores, by their tags, as Black King Bar's
  Avatar ignores Silenced: the other rules still stop it, and still interrupt it as it runs (see Requirements). A tag
  ignores the rules under it too.
- **Cost** and **Cooldown**: gameplay effects ([EFFECTS.md](EFFECTS.md)) applied to the caster as the ability starts,
  after its targeting, so a cancelled targeting pays nothing, or by the stage that takes them (see Stages). A
  cost is usually an Instant effect that lowers a resource such as Mana. A cooldown is
  usually a Durational effect: while it is active, the caster carries its asset tag, and the Cooldown check refuses the
  ability. A line under each field says how its effect takes its levels, and **Link** makes it take this ability's.
  **New**, beside an empty field, makes an effect linked to this ability. If Project Settings → PlayForge names a Cost
  or Cooldown effect, the field's right-click menu copies it or uses it.
- **Attribute Costs**, after Cost: what the ability spends of attributes as it's used, beside its Cost effect,
  both paid. Each names the **Attribute** it spends, or, where it names none, a **Role** whose attribute in the project it
  spends ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), and an **Amount** that can scale by level. It's refused with Cost while
  its caster has less than they come to, lacks the attribute, or no attribute plays the role, and they're paid where a
  Cost is, as the ability starts or its stage applies usage. The section's line says them, as "30 Mana".
- **Targeting**: the task that picks what the ability acts on, such as an entity the player clicks, or the caster
  itself. Targeting holds the activation's claim. If the task fails while its **Break Runtime On Invalid** is on, or it
  doesn't finish, the activation ends. Leave Targeting empty to skip this step.
  - **Click targeting** (the Select tasks: Position, Direction, GAS Target, GAS Or Position, GAS Target In Range)
    waits for a press and aims along the game's input, `ForgeInput.Current`. By default that's the Input System's
    pointer, a mouse, a pen or a touch, where its package is in and on, or else the Input Manager's mouse, each aimed
    through `Camera.main`. With no camera, a press aims nowhere. For a camera of your own, give the default one
    (`new LegacyPointerInput { Camera = camera }`, or `InputSystemPointerInput`), and for a gamepad's reticle or a
    controller's ray, write an `IForgeInput`. Put either in with `ForgeInput.Use(input)`. An activation that
    already holds its target, under TargetReal, or its point, under Position, as an AI's does, takes it with no
    click, unless the task's **Take Target From Data** is off; a handed target the task can't take, out of range
    or failing its requirements, fails the targeting at once.
  - **Auto targeting** (Auto Target, Auto Target By Affinity) takes the entities within its **Range**, or its ability's
    Cast Range where it has one, the range measured from the caster and **Use Cast Range** on, sorted by its
    **Mode**, up to **Max Targets**, or every one at 0. Each is a target of the activation, and the first, the nearest
    for Nearest, is its primary. Apply Effects and Apply/Remove Effects reach each target, in the project's resolve
    order unless their **Use Resolve Order** is off ([EFFECTS.md](EFFECTS.md#resolve-order)), so an area ability
    is Auto Target and then Apply Effects. A Target row and the Target Checks check the primary. **Auto Target From Data**
    with **Primary Only** off takes each target its key holds.
    Each entity is found by its position, on either hub, with no collider needed. **Around** says where the
    range is measured from: the caster, the activation's target (as an AI hands it, or a task before it in a
    Composite picks it), or its point; **Flat**, on by default, measures along the ground, and off, a sphere. **Use
    Physics** finds entities with physics instead, by collider on its **Detection Layers**, which show under it alone,
    as it did, and a runtime entity by its position. Auto Target By Affinity's **Include Self** takes its caster where
    it's of the sides it takes.
  - A targeting task's numbers, as its **Range**, its **Max Targets**, a pointer task's **Max Distance** and an
    attribute condition's value, are numbers that can scale by level or read an attribute, read as its activation
    starts, at its ability's level. A pointer's **Mouse Button** picks one of three, so it stays a choice.
- **Use Implicit Targeting** (default on): before targeting, record the caster as the activation's target, under
  **Implicit Tag** (default `Target`), which shows while this is on. A targeting task that sets a target replaces it.

## Stages

This section is what the ability does once it has started.

- **Asset Loaders**: run once per activation, after targeting. Each loader adds assets to the activation's data under
  a tag. A Set Effects loader, for example, adds effects under `Effects`. Code-driven tasks read them from there.
- **Cancelled By**: abilities, by asset tag or a context tag, or a child of one, that cancel this one as they
  activate. The ability is interrupted, then the other starts. Each stage that names none of its own takes this list,
  and so does targeting. A critical stage is committed, so nothing cancels it.
- **Blocks**: abilities, named the same way, that can't activate while this one runs. Each stage that names none
  takes this list. A blocked activation is refused, unless When Busy is Queue: then it waits in the input buffer for
  the block to lift.
- **Stages**: each stage starts all its tasks together. The next stage starts when it ends. A stage folds to one
  line of what it holds, as "2 tasks · All · Applies usage effects", and shows Critical on its row while a task
  commits the ability to it. The list's **−** removes the selected stage, and a stage keeps its fold as others
  are removed or dragged.
  - **Completion**: All ends the stage when every task has finished. Any ends it when the first task finishes,
    and cancels the rest. A stage with no policy set behaves as All.
  - **Apply Usage Effects**: apply the ability's cost and cooldown in this stage. They apply once per activation, in
    the first stage that does this. If no stage does, they apply as the ability starts, after its targeting.
  - **At Stage Start**, under Apply Usage Effects: on, apply them as the stage starts. Off, apply them as it ends,
    and only if it succeeded.
  - **Tasks**: the work, such as Apply Effects, Delay or Run Sequence, in a list whose **+** adds an empty task to
    pick a kind for, from a searchable picker in which each task sits under its category: Effects, Asset Loaders,
    Process, Sequence, Waits, Events and Tags, Movement, Items and Utility. A task of your own names its
    submenu with `[ForgeMenu("Effects", Description = "...")]`, a slash nesting one submenu in another and a
    class's path holding for those made from it; one that names none sits after the submenus, and its Description is
    what the picker's Context says while it's highlighted. A task's numbers, as Delay's milliseconds and Channel's duration, are numbers that can
    scale by level, read as it runs, at its ability's level. Run Sequence runs the sequence a static method
    makes, picked in **Sequence** by name, from a searchable dropdown of the project's `[TaskSequenceMethod]` methods,
    each class's under its name where more than one class holds them, or, for an ability built in code, a fresh run of the sequence its `Definition` describes, which
    `TaskSequenceBuilder` makes. Its **Clock** picks the clock that sequence runs by: none for the sequence's own or the
    caster's, or the Caster's, the Game's, Unscaled, which runs while the game is paused, or Scaled, a pace of its own;
    the ability's other tasks keep the caster's. A stage that holds a critical task is critical.
    PlayForge's own tasks, by category: **Effects**, Apply Effects, Apply/Remove Effects, **Area Effect** (its
    effects on each within its radius of its caster, its target or its point, of the sides it takes, by position on
    either hub or with physics) and **Apply To Caster**; **Asset Loaders**, the Set tasks; **Process**, Create Mono
    Process, Run Process and Disjoint; **Sequence**, Run Sequence and Inject; **Waits**, Delay, Channel, and **Wait For
    Event**, **Wait For Tag** (gained or lost) and **Wait For Attribute** (crossing a value), on its caster or its
    target, each with a **Timeout** and a **Record As** for a later stage's Condition, an event waited for being sent
    by the core while it waits; **Events and Tags**, **Send Event**, to the caster or the targets, **Grant Tags**,
    loose tags while its stage runs, and **Cancel Abilities**, the caster's running ones by tag, but in a critical
    stage; **Movement**, **Dash** and **Blink**, which tell a mover of the game's own, an `IForgeMover` on the caster's
    object, as the Creator Lab's units are; **Items**, **Consume Item**, which its item system's Item Consumed hears;
    **Cues**, **Play Cue**, its cues at the stage's moment, on the caster, on the target or at the point;
    **Utility**, Unity Event. Dash, Blink, Area Effect, Apply To Caster and Consume Item were the Demo's own.
    The activation keeps its claim until its last critical stage ends. With no critical stage, it lets the claim go
    as the stages start. Apply Effects' **Record Landed As** tags the activation's data as an effect lands on a
    target, for a later stage's Condition.
  - **Condition**: when the stage runs, by a tag. With none, always. Otherwise only while the tag is there, or, with
    **When Missing**, only while it isn't: **Looked For On** The Activation looks in the activation's data, as Record
    Landed As fills it, and Caster or Target in that entity's tags. Both show once the condition names a tag. A stage whose condition fails is skipped. Two stages that
    name one tag, one wanting it and one wanting it missing, branch: a jab's hit recovery and its whiff. A critical
    stage that's skipped lets the claim go as the next stage starts, so put a committed whiff before the hit's
    recovery, and the claim goes as that recovery starts.
  - **Cancelled By** and **Blocks**: the stage's own lists, as above, which hold while it runs. A heavy attack's
    recovery can name the specials that cut it short, and its windup none.

## Requirements

Requirements are what an activation must pass. If one fails, the ability doesn't run.

- The **tag requirements** table holds tag checks, a row for each side. **Caster** checks the caster, with the Caster
  Checks. **While active** checks the caster for as long as the ability runs: when it fails, the ability is
  interrupted, so a channel that must not have `State.Stunned` ends as a stun lands, and while it fails, the ability
  can't activate. **Target** checks the target the same way, after targeting. Each row's **Requires** cell lists the tags the
  entity must have, and its **Must not have** cell the tags it must not have, such as `State.Stunned`, or says None. A
  row passes when the entity has every tag it requires and none it must not have, so an empty row always passes. Click
  a cell to edit its list under the table, with the row's **Template** and **Sync With Template** (see
  [EFFECTS.md](EFFECTS.md)). Each entry is a tag query: a **Tag**, a **Match** mode such as Include Children, and a
  comparison with a number. Greater Than 0 means "the tag is present".
- **Caster Checks** check the caster. They run when the ability is asked to activate, before targeting, and again after
  targeting. The defaults are Cooldown, Cost and Is Alive. You pick each check's type from a list, which offers the
  caster's types and those that check either side.
- **Target Checks** check the target, after targeting. The default is Is Alive. If targeting found no target, they are
  skipped. Their list offers the target's types and those that check either side.

A check a component stands behind shows under its list, locked, as Check Range does under the Target Checks while the
ability holds a Cast Range. No list holds it: it comes and goes with its component, which enforces it, and the
section's line counts it among the checks.

The check types, by the list that offers them:

- Caster Checks:
  - **Cooldown**: passes when the ability isn't cooling down, or, with its charges counted (see Charges), has one in
    hand.
  - **Cost**: passes when the ability has no cost, or the cost would leave each attribute it spends at 0 or more, and
    its Attribute Costs can be paid. The caster must have each attribute.
  - **Ability Level**: compares the ability's level with **Level**, using **Comparison**, as a form it unlocks at a
    rank. It starts at 2 or more.
- Either list, checking the caster among the Caster Checks and the target among the Target Checks:
  - **Is Alive**: passes when the checked entity isn't dead.
  - **Attribute**: compares the checked entity's Current value of **Attribute** with **Value**, using **Comparison**;
    with **Share Of Base**, its share of its base, from 0 to 1, as "below half its health".
  - **Tag Weight**: compares the checked entity's weight of **Tag** with **Weight**, using **Comparison**, as combo
    points or stacks. It starts at 1 or more; an entity without the tag holds it at 0.
  - **Has Effect**: passes when the checked entity has **Effect** on, or any effect tagged **Tagged** or under it, by
    its asset tag, a context tag or a tag it grants; with **Without**, when it has none of it. Naming neither, it
    passes.
  - **Holds Item**: passes when the checked entity holds **Item**, or, with **Equipped**, wears it. Naming none, it
    passes.
- Target Checks:
  - **Affiliation**: passes when the target stands to its caster as **Target** says. An **Enemy** shares none of the
    caster's sides and an **Ally** one, the caster itself among them; **Other Ally** leaves the caster out, **Self**
    is the caster alone, and **Not Self** anyone else. A caster with no sides takes anyone as either.
  - **Line of Sight**: passes when nothing on the **Blocking** layers blocks a line from the caster to its target, run
    **Height** above each, so the ground under them doesn't. 3D physics colliders block it; triggers and the two ends'
    own colliders don't.
  - **Facing**: passes when the target lies within **Angle** in front of its caster, or, with **Behind Target**, the
    caster stands within the angle behind its target, as a backstab wants. Each faces as its targeting packet does,
    along the ground: 90 takes 45° to either side, and 360 anywhere.
  - **Check Range**: the Cast Range component's, which no list offers. It shows while the ability holds a Cast Range,
    saying its range, and passes, as the component enforces the range (see Cast Range).

A failed Caster Check refuses the activation as the kind its `RefusedAs` says: Cooldown, Cost or Dead for those three,
which name none, and Rule, naming the check, for any other; a failed Target Check ends it after targeting, told as
Target, naming the check (see Activation). Each says why in `Validate`'s `error`, which the refusal carries as its
`Detail`, as "Its caster's Mana, 20, can't pay 30".

A check of your own implements `IAbilityValidationRule`: `Validate` says whether the activation passes, and why not,
and `GetName` names it; `RefusedAs`, Rule unless it says otherwise, is the kind its refusal of the caster is told as, so
an ammunition check can say Cooldown as the built-in does. `[ForgeMenu]` names and describes it in the list, and `[ValidationSide]` says which list offers
it, Caster or Target; without it, both do. A component stands behind a check by overriding its `TargetCheck` or
`CasterCheck` with an `AbstractComponentValidationRule<T>` of its own kind: the check shows under the list while the
ability holds the component, and the component enforces it, in `CanStart`.

The section's Clear button empties the tag requirements and leaves the checks, since an ability without them would
skip its cost and cooldown.

### Crowd Control

**Crowd control** holds for every ability at once, so no ability has to list Stunned among the tags it must not have. A **Crowd Control Rules** asset (Create → PlayForge → Crowd Control Rules) holds its rules, each a **Tag** the
caster carries, as Stunned, and what it **Stops**: **Every Ability**, as a stun, or **Abilities Tagged**, those whose
asset tag or a context tag is one of its **Ability Tags** or under it, as Silenced stops spells and Disarmed attacks.
An entity's abilities follow its own **Crowd Control** ([ENTITIES.md](ENTITIES.md#abilities)), or, where it names none,
its Forge Project's ([ATTRIBUTES.md](ATTRIBUTES.md#roles)).

While the caster carries a rule's tag, or one under it, an ability the rule stops is refused with **Crowd Control**,
the refusal's `Rule` naming the tag, and one that runs is interrupted as the tag lands, as a stun breaks a channel. An
ability's **Ignores Crowd Control** (see Activation) opts it out of the rules it names. Code asks with
`rules.Stopping(ability, tags)`, the first rule that stops the ability, or null.

## Components

An ability's components are features it carries, each a class with hooks into its life, added with the list's **+**. A component hears the ability **Granted** to an entity and **Removed** from it; is asked whether an activation may start, **CanStart**, where a reason it gives refuses the activation, named by it, as the kind its **RefusedAs** says, Rule unless it says otherwise; is asked whether the target its targeting picked may be taken, **CanTakeTarget**, where a reason it gives ends the activation there, before anything of it is paid, told as the kind its **RefusedAs** says; is waited on as an activation winds up, **WindUp**, after its target checks and before its cost, as a turn to face its target is; and hears an activation **Start**, **Commit** its cost and cooldown, and **End**, by how it ended: done, cancelled or failed. As the activation's usage applies, a component may apply it in the ability's place, **ApplyUsage**, as a Toggle does, composing its own from the spec's `PayCosts` and `SpendCooldown`; may waive the Cost and Cooldown checks while it says so, **Waives**; and claims the categories of the effects its ability puts on for a purge, **CategoryOf**, as a Toggle claims its effect a toggle's. A kind that makes no sense twice is offered only while the ability holds none of it.

A component of your own derives from `AbstractAbilityComponent` and overrides the hooks it needs, each a no-op
otherwise. It's the ability's, shared by everything that holds it, so what it keeps for one entity's ability it keeps through its
context: `ctx.GetState<T>(this)`. **Describe** says what it does in one line with its numbers, as a Cast Range's "Range
600", which the Components section's line, The Forge's summary and the ability's breakdown say beside its name; and
**DescribeUsage** what it adds to the ability's use, as Charges' "3 charges", which the Activation section's line says
after its cost and cooldown. Its rules in the editor derive from `ForgeComponentRule<T>`, in the editor's assembly, with a
public constructor that takes nothing: the Problems view runs each on every asset that holds a component of its kind,
and the ability's inspector says what it finds under its Components list, as it says the Charges'.

Code finds a component by what it does, not by its class: `Find<T>()`, on an ability, its spec, an effect, an item and an
entity, gives the first of its components that is a `T`, a kind or an interface it implements. An ability's reach is an
`IAbilityReach`, which Cast Range implements; the targeting tasks, the Creator Lab's bots and your own code read it
through `ForgeReach`, so a component of your own that implements it is asked as a Cast Range is. How its uses come back
is an `ICooldownModel`, which Charges implements: how many it holds, how many are in hand, what a use spends, and why
the Cooldown check refuses it with none; an ability with none has `SingleCooldown`, one use, back as its Cooldown ends.
The Cooldown check asks it, the ability spends through it, and a HUD reads it as `spec.CooldownModel`. A component's
state that needs its holder, as a Charges component's charges need their ability, is kept with
`spec.ComponentStates.TryGet` and `Keep`. The built-in components are written on this API alone, and PlayForge's core
names none of them, so a component of your own reaches as far as they do. None has a shortcut: code reads one through
`Find<T>()` or the component itself, as `spec.Find<ToggleAbilityComponent>()?.IsOn(spec)` says whether a Toggle is on.

### Cast Range

A **Cast Range** component is the one range the ability has: how far from its caster its target or its point
may be, its **Range** a number that can scale by level, measured along the ground unless **Flat** is off; 0 reaches
anywhere. An activation handed a target or a point beyond it, under TargetReal or Position, as an AI's is, is refused
as **Range**, so nothing of it is paid and an AI can walk closer and ask again; targeting that picks a target beyond it,
whichever task picked it, ends the activation before its stages, told as **Range** too. The auto-targeting tasks measured from the caster, and
Select GAS Target In Range, search within it where their **Use Cast Range** is on, as it is by default, their own range
set aside. The Creator Lab's units attack from their attack's Cast Range, and its bots cast an ability once their foe is
within its Cast Range, where it has one ([CREATOR_LAB.md](CREATOR_LAB.md)). It's the ability's reach, an
`IAbilityReach`: code reads it through `ForgeReach`, as `ForgeReach.TryRead(caster, ability, out range)`, and a reach
of your own takes its place in each of these.
While the ability holds it, its Target Checks show its **Check Range**, locked, saying the range (see Requirements). An
ability holds one at most.

### Charges

A **Charges** component says how many uses the ability holds, a number that can scale by level, each use spending a charge. With 1 it
is an ordinary cooldown. With more, the Cooldown check refuses it only with none in hand, and its **Recharge** says
how spent ones come back: **Each Charge** (the default) starts the Cooldown for each charge spent, a container of its
own whatever the effect does when applied again, and each brings its charge back as it ends, so three spent a second
apart come back a second apart; **All At Once** starts the Cooldown only as the last is spent, and its end brings them
all back. Write a recharge of your own by extending `AbstractChargeRecharge`: override `InHand` (how many are in
hand) and `Spend` (what a use does), using `AbilityCharges`' `Max`, `Spent`, `Cooldowns` and `StartCooldown`; one
of your own counts even a single charge, unless its `CountsASingleCharge` says not. Charges are the ability's cooldown
model (see Components), so code reads what's in hand of any ability as `spec.CooldownModel.InHand(spec)`. The
component itself, `spec.Find<ChargesAbilityComponent>()`, holds its **Charges** and **Recharge**, and its
`ChargesOf(spec)` gives the ability's charges on its caster, an `AbilityCharges`. The section's line counts them, as "3 charges",
and a warning under the Components list, as in the
Problems view, says where built-in charges have no Cooldown to bring them
back, so they never run out. An ability holds one at most.

### Toggle

A **Toggle** component holds an **Effect** the ability turns on and off on its caster, as a mana shield: used, it puts the effect
on where its usage would be paid, paying its Cost and Attribute Costs and starting no cooldown; used again, it takes
the effect off, paying nothing and asking no cost, and starts its Cooldown, or spends a charge. Neither its cost nor
its cooldown stops it turning off. It counts only the effect it put on; one another put there leaves it off. A purge
counts its effect a Toggle's, as a death that keeps toggles keeps it. Code asks the component,
`spec.Find<ToggleAbilityComponent>()`: its `IsOn(spec)` says whether its effect is on, and its **Effect** is the effect. It's written on the components' hooks alone: it applies the usage in its ability's
place, waives the cost and the cooldown while it's on, and claims its effect. The Problems view warns where it toggles an Instant effect, which is never on. As its holder forgets the ability, the effect comes off; it stayed on. An ability
holds one at most.

### Turn To Face

A **Turn To Face** component says how this ability turns its caster to face its target or its point before its first
stage, in place of its entity's Turns To Face ([ENTITIES.md](ENTITIES.md#turns-to-face)): its own **Turn Speed**,
**Start Angle**, **Facing Angle**, **If Too Long** and **Max Wait**. With **Turns** off it doesn't turn at all, as a
cast behind its caster doesn't. The Target Checks come before the turn, so a Facing check that wants its target in front
of its caster reads the caster's facing before it turns. An ability holds one at most.

### Auras

An **Aura** component holds an effect on those near its holder, while the entity knows the ability, as a banner that inspires the allies
round its bearer: each a radius, whom it takes, its holder too or not, and how often it checks
([EFFECTS.md](EFFECTS.md#auras)).

### Cues

A **Cues** component plays what the ability shows and sounds at its moments, on its caster: **On Activate** as an
activation starts, **On Commit** as it pays its cost and starts its cooldown, **While Active** from its start to its
end, **On End** as it ends however it ended, and **On Refused** as one is refused, nothing of it paid. Each list holds
cues as an effect's does ([EFFECTS.md](EFFECTS.md#cues)), a Cue Tag among them; a Prefab cue in While Active lasts
whatever its own While Active says. A stage's exact moment, as a sword's arc, plays through a **Play Cue** task. An
ability holds one at most.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
to the entity when it learns the ability, and removed when the ability is removed. **Name** labels the group. It holds
four lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes this entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group
from another asset.

### On Hit

**On Hit**, above the workers, holds the effects the entity's hits apply while it knows the ability, as a passive's. Its entries are an item's: an attribute, the effects, to the one hit or the holder, only on a kill, impact
types and a condition ([ITEMS.md](ITEMS.md#on-hit)).


### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, while the entity knows the ability: its holder's own, as a boon that adds a
burst to its holder's every Chill, or those landing on it, as a cloak that halves the stuns its wearer takes.
Each entry is a rule of its own or names a shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).

### Lifesteal

An ability gives its holder lifesteal by an effect that raises the holder's **Lifesteal**, the attribute the project's
Lifesteal worker names as its Share, as a passive applies it to its caster, which the Lifesteal worker heals by ([WORKERS.md](WORKERS.md#lifesteal)): a vampiric mask's
Infinite effect of +0.15 Lifesteal heals its wearer 15% of each blow it deals. Its Lifesteal list is gone.
## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

Code reads a value with `ability.TryGetLocalData(key, out var data)`. The first row with that key wins. A tag check of
your own, extending `AbstractTagValidationRule`, reads its tag's value with `GetValue`: the tag's weight on the entity
it checks or, where that entity holds none of it, the ability's Local Data under the tag.

## Used By

The PlayForge assets that refer to this ability, each with its type icon, such as an entity that starts with it, an
item that grants it, or a cost linked to it. Click a row to select the asset. The list follows the project as it
changes.
