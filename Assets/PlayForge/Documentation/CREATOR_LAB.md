# PlayForge — The Creator Lab

The Creator Lab is a space for trying a project's own assets. You put units down, give them abilities, effects and
items, cast, and watch the numbers move.

To open it, choose **Window → PlayForge → Creator Lab**. It opens in a new scene and plays. The scenes open before are
saved first, if you want them saved.

The lab plays every PlayForge asset in the project's own folders.

## The space

The space is the scene the lab plays in: its ground, its camera, its units and the lab's caster.

| Part | Description |
| --- | --- |
| **The ground** | A plane in the middle of the scene, lit from above. |
| **The camera** | Looks down at an angle ([The camera](#the-camera)). |
| **Units** | Each a body and a `GameplayAbilitySystem` ([Units](#units)). |
| **The lab's caster** | A unit of the lab's own, on no side, with no body. It's the source of what you drop while no unit is selected. |

### The camera

The lab's camera looks down at the space at an angle.

| Control | What it does |
| --- | --- |
| Scroll | Pulls the camera back, or brings it in. |
| Drag with the middle button | Pans the camera. |
| The right panel's **Distance** and **Tilt** sliders | Set its distance and tilt. |
| **Reset view** | Puts the camera back. |

### Units

Each of the lab's units is a body and a `GameplayAbilitySystem`.

- Its body is a capsule, with a nose to show where it faces.
- A ring at its feet is in its side's color.
- A bar over it shows its name and health.

A unit walks in a straight line where it's sent, and attacks what it's set on. A walk or a stop ends a turn to face
that its abilities ordered, as a game's move would.

> [!NOTE]
> PlayForge has no movement of its own, so the lab's movement is its own. A project's game moves its units its own
> way.

### Modes

The mode sets what the space holds. You pick it with the buttons at the top of the Lab panel.

| Mode | What the space holds |
| --- | --- |
| **Dummies** | Test dummies you put down ([Test dummies](#test-dummies)). |
| **Battle** | Teams of bots that fight ([Battle](#battle)). |
| **None** | An empty space, where abilities still cast and effects still land. |

The lab starts in **Dummies**. Each mode starts with its units cleared.

## The panels

The lab's panels sit at the edges of the screen. The middle of the screen is the space's.

| Panel | Where | What it holds |
| --- | --- | --- |
| **Library** | Left | The active Forge Project's assets, by kind. |
| **Selected** | Right, over the Lab panel | What's selected. It's shown as something is selected. |
| **Lab** | Right, under Selected | The mode, the test dummies, the camera and the lab's time. |
| **Log** | Bottom | A page for each way the lab shows its working. It's collapsed until you open it. |
| **The status** | Top, between the panels | What a cast waits for, what's selected, and the keys for what you can do now. |

Each panel but Selected collapses to a tab at its edge with its − button.

### The Library panel

The Library panel, on the left, lists the active Forge Project's assets by kind, each kind by name.

The kinds are abilities, effects, items, entities, attribute sets, attributes and scalers. While the project has no
Forge Project, the panel lists every asset of the project. A kind's color shows in its icon alone.

| Control | What it does |
| --- | --- |
| **Project** | Switches the active project, as The Forge does. It also follows The Forge's switch. |
| The search box | Filters the assets. |
| **Refresh** | Lists the assets again, after you make or delete one. |
| Click an asset | Shows its numbers in the Selected panel. |
| Drag an asset | Drops it on the space ([Dropping assets](#dropping-assets)). |

### The Selected panel

The Selected panel shows what's selected: an asset from the library, or a unit in the space.

It sits on the right, over the Lab panel, and shows as something is selected. Its **Lab** tab sets it aside, the
selection kept. The Lab panel's **Selected** tab brings it back, as each selection does.

| Selected | What the panel shows |
| --- | --- |
| An asset, clicked in the library | Its numbers level by level, as "−80/−120/−160 Health". **Open in The Forge** opens it there. |
| A unit, clicked in the space | Its attributes, its tags, its effects with their stacks and time left, its abilities with their levels and cooldowns, and its items. |

For a unit, the panel has these controls:

| Control | What it does |
| --- | --- |
| − and + | Change an ability's level. |
| × | Takes an ability or an item. |
| **Remove** | Takes the unit out. |
| **Take control** | Makes the unit yours ([Your unit](#your-unit)). |
| **Release** | Lets it go. |

A test dummy's count of what it took shows here too ([Test dummies](#test-dummies)).

### The Lab panel

The Lab panel, on the right under the Selected panel, holds the mode, the test dummies, the camera and the lab's time.

| Section | What it does |
| --- | --- |
| The mode buttons | Pick the mode ([Modes](#modes)). |
| **Entity** and **Side** | Pick the entity and the side a dummy, a bot or your unit is put down as. |
| **Test dummies** | Puts down dummies, in the Dummies mode ([Test dummies](#test-dummies)). |
| **Your unit** | Puts down a unit for you to control, or takes one ([Your unit](#your-unit)). |
| Battle | Adds bots and runs a fight, in the Battle mode ([Battle](#battle)). |
| Camera | Its **Distance** and **Tilt** sliders, and **Reset view** ([The camera](#the-camera)). |
| Time | **Pause**, **Step** and **Speed** ([Time](#time)). |

### The Log panel

The Log panel, at the bottom, shows the lab's working as it plays.

It's collapsed until you open it. It holds a page for each way the lab shows its working
([Showing its working](#showing-its-working)):

| Page | What it shows |
| --- | --- |
| **Journal** | The lab's effect journal. |
| **Combat** | The selected unit's combat log. |
| **Stops** | What pauses the lab's world. |
| **Watches** | Numbers pinned on the selected unit, over time. |
| **Log** | PlayForge's messages. |

### The status

The status runs along the top of the screen, between the panels, and says what's going on.

| Line | What it says |
| --- | --- |
| What a cast waits for | The cast's wait, in its targeting's words, as "Bolt: Click a target · Esc or a right-click cancels". |
| The lab's state | What's selected, whom you control, and whether the lab is paused. |
| The keys | The keys for what you can do now. |

Clicks go through the status to the space.

## Dropping assets

You give assets to units by dragging them from the library and dropping them on the space.

| Dropped | On | What happens |
|---|---|---|
| An effect | A unit | It lands on the unit, from the selected unit, or from the lab's caster. |
| An ability | A unit, or the ground | The selected unit, or the lab's caster, casts it there. |
| An item | A unit | It's given to the unit, equipped. |
| An entity or an attribute set | The ground | A test dummy of it is put down, on the side the Lab panel's picker names. |

When you drop an ability:

- Its target goes in the activation's data, as click targeting takes it with no click.
- A unit that doesn't know the ability learns it first.
- An ability that needs more than the point waits for a click, which **Esc** cancels.

## Test dummies

A test dummy is a unit that stands and counts what it takes.

The Lab panel's **Test dummies** section puts down a dummy of any entity or attribute set, on a side: Blue, Red,
Green, Gold or Neutral.

| Button | What it does |
| --- | --- |
| **Add** | Puts down a dummy of the entity picked, on the side picked. |
| **Reset** | Puts each dummy back where it was put, at full. |
| **Clear** | Takes every unit out. |

A dummy:

- counts what it takes: its damage a second, the last blow by its impact types and the effect it came from, and a
  short history, which the Selected panel shows;
- doesn't die unless asked: falling, it's revived where it stands, at full;
- mends to full when it's left alone for three seconds.

### Changing assets as the lab plays

An asset you change while the lab plays takes effect the next time it's read.

- An effect or an entity changed in the inspector or The Forge as the lab plays is read on its next application, as
  every asset is.
- What a unit was made from, its attribute set and its starting abilities, shows on a new unit.

## Battle

The **Battle** mode fights teams of bots.

### Setting up a battle

You set up a battle in the Lab panel's Battle section.

1. Pick an entity and a side at the top of the Lab panel.
2. Pick a level in **Level**.
3. Select **Add bot**.

Each side stands in a line of its own:

| Side | Its line |
| --- | --- |
| Blue | To the south |
| Red | To the north |
| Green | To the west |
| Gold | To the east |

The section's other buttons run the battle:

| Button | What it does |
| --- | --- |
| **Quick battle** | Makes Blue against Red, with as many bots on each side as **A side** sets. Each bot is the entity selected in the library, or one picked at random from the project's. |
| **Play** | Starts the fight and the lab's time. |
| **Reset** | Makes every bot anew where it started, at full, with the items and abilities you gave it since, and their levels. |

The line under the buttons says who stands, and who won, or that no one stands.

### How bots fight

A bot takes the nearest foe it can target, casts what's ready, and otherwise attacks with its attack.

Its order and its moments are its rules', read from Local Data under these keys
([The lab's rules](#the-labs-rules)):

| Rule | Description |
| --- | --- |
| **Cast Order Key** | The Local Data key an entity's order is under, as "W Q E": the letters of its abilities other than its attack, in its entity's order. With none, it casts each in turn. |
| **Cast When Key** | The key for when it casts an ability. |
| **Reach Key** | The key for an ability's reach, 0 for anywhere, where it has no reach of its own, as a Cast Range. An ability that has one reaches as far as that does. |

An ability's **Cast When Key** names one of these moments:

| Moment | The bot casts the ability |
| --- | --- |
| **InRange** (the default) | With its foe in reach. |
| **Hurt** | Below 60% health. |
| **Low** | Below 35%, away from its foe, as an escape. |
| **Stunned** | While something stops it. |
| **FoeLow** | With its foe below half. |

Low on health, a bot backs off a moment once in a while.

The Demo's AI hero is a bot, its roster giving the keys.

## Your unit

In either mode you can play a unit of your own, with the Demo's controls.

### Taking control

The Lab panel's **Your unit** section gives you a unit of your own to control.

| Button | What it does |
| --- | --- |
| **Put down** | Puts down a unit of the entity picked above, on the side picked, where the camera looks, and gives you control of it. |
| **Take control** | Takes the unit selected. |
| **Release** | Lets your unit go. |
| **Follow** | Has the camera follow your unit. |

A bot you take over stops fighting until you let it go.

- A new fight passes it over.
- **Reset** keeps you on its seat's new bot.

The line under the buttons says whom you control.

### Controls

You control your unit with the mouse and the keys, as in the Demo.

| Input | What it does |
| --- | --- |
| Right-click the ground | Walks there. |
| Right-click a foe | Attacks it. |
| Right-click an ally | Walks to it. |
| **Q**, **W**, **E** and **R** | Cast its abilities past its attack, in its entity's order. |
| **1** to **6** | Cast its items' active abilities. |
| **S** | Stops it. |
| Left-click | Picks the target of an ability that waits for one. |
| **Esc** | Cancels an ability that waits for its target. |

An ability that needs a target waits for a left-click. While it waits:

- a ring on the ground shows its reach, its Cast Range or its Reach;
- another ability's key casts that one instead;
- a right-click order lets it go;
- **Esc** cancels it.

The click that picks a target selects nothing.

The keys are the **Lab Controls**' own, as the status says them, and the ring's colors, width and material are its
too. They read PlayForge's input, `ForgeInput`, so a game's own input or a test's stands in.

### Drivers

Your unit's orders go through a driver. The lab's driver walks a unit in a straight line, and casts as the lab's units
do.

A project whose units move and cast their own way gives a driver of its own:

1. Make a **Lab Driver** asset (**Create → PlayForge → Lab Driver**) of a class of your own.
2. In that class, override the orders your game gives its own way: `MoveTo`, `Attack`, `Cast` and `Stop`. Each does
   as the lab's does unless overridden.
3. Name the asset on your Lab Rules' **Driver**.

## Time

The lab's units live in a world of the lab's own (`ProcessWorld`), which the lab steps by its own time. So its time
stands still or runs faster without touching Unity's.

| Control | What it does |
| --- | --- |
| **Pause** | Stops the lab's time. |
| **Step** | Goes one frame (a sixtieth of a second) while the lab is paused. |
| **Speed** | Runs the lab's time from a quarter to four times as fast. |

The camera and the panels go on while the lab is paused.

## Showing its working

The bottom panel's pages show what happens as the lab plays. Each records only while the lab runs.

### Journal

The **Journal** page is the lab's effect journal over every unit, newest first.

It records:

- each effect landing, with its number traced;
- each effect refused, and why;
- each effect ticking, hitting and coming off;
- each worker that ran, by its hook and its side, with the actions it queued and whether they ran.

It shows what happened on the selected unit or, with **Selected unit** off, on any. Click an entry to see its parts: a
number's, a hit's breakdown, or a worker's actions.

### Combat

The **Combat** page is the selected unit's combat log, newest first.

Each hit has its steps under it, as "-30 as made → Math -30 → -15 (inline) → landed -15". The project's combat log
writes it (Project Settings → PlayForge → Combat Log).

### Stops

The **Stops** page holds a list of what pauses the lab's world.

A stop fires when:

- an effect lands, is refused or has a worker run;
- an attribute crosses a value;
- a tag comes or goes;
- an ability is refused.

Each stop watches the selected unit or any. It names an effect, an ability, a tag or an attribute where it asks for
one, and **Any** for an effect or an ability takes any.

As one fires, the world pauses, and the page says which stop fired and what fired it.

| Button | What it does |
| --- | --- |
| **Go** | Plays on. |
| **Step Frame** | Goes one frame. |
| **Step Tick** | Goes on until an effect ticks on the selected unit, or on any, up to ten seconds, or until a stop fires. |

### Watches

The **Watches** page pins numbers on the selected unit, and draws each over time.

You can pin:

- an attribute's current or base value;
- a tag's count;
- an effect's stacks or time left;
- a Scaler asset read on the unit.

Each shows as a line over the last minute of the lab's time, in its own color and scaled to its own range, with its
value now.

### Log

The **Log** page shows PlayForge's messages as `ForgeLog` lets them through, by system, newest first.

| Control | What it does |
| --- | --- |
| **Selected unit** | While it's on, the page shows only the messages about the selected unit. |
| **System** and **Level** | Give a system a level of its own, which the Console keeps too. |
| **As all** | Gives the system back the Logging preference's level. |

## The lab's rules

The lab's rules tell it a few things about a project: which attribute is a unit's health, which it walks at, which
tags stop, hide or shield it, and which ability is its attack.

With no rules given, the lab:

- takes the attributes playing the project's Health and Movement Speed roles
  ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), or else finds the attributes named Health and Movement Speed (or Move Speed);
- walks a unit without a speed at 4 a second.

### Lab Rules properties

To say more, you give the lab a **Lab Rules** asset.

1. Make one with **Create → PlayForge → Lab Rules**.
2. Give it to the lab's `CreatorLab` component.

| Property | Description |
| --- | --- |
| **Health** | The attribute that is a unit's health. Left empty, it's the project's. |
| **Movement Speed** | The attribute a unit walks at. Left empty, it's the project's. |
| **Walk Speed** | How fast a unit walks where it has no Movement Speed. |
| **Stops** | Tags that stop a unit moving, as Stunned. |
| **Hides** | Tags under which the other side can't see or target it, as Invisible. |
| **Shields** | Tags under which no one can target it. |
| **Attack Range Key** | The Local Data key a unit's attack range is under. |
| **Attack Range** | An attack range for every unit, in place of the key. |
| **Attack Slot** | Which of an entity's starting abilities is its attack, from 0, or −1 for none. |
| **Driver** | The project's **Lab Driver**, which the unit you control is ordered about through ([Your unit](#your-unit)). |

A unit whose attack has a reach of its own, as a Cast Range, attacks from as far as it reaches.

The rules also hold the bots' keys: **Cast Order Key**, **Cast When Key** and **Reach Key** ([Battle](#battle)).

The Demo's roster is the Demo's rules: its units, camera and dummy are the lab's, so the two can't drift.

## In code

The lab can be made, driven and read from code.

> [!NOTE]
> A build can leave the lab out by defining `PLAYFORGE_NO_LAB`. The Demo uses the lab's units, so it goes too.

### Making and driving a lab

`CreatorLab.Create(library, rules, panels)` makes a lab, as the tests do. Its calls are the panels':

| For | Calls |
| --- | --- |
| Units | `AddUnit` and `AddDummy` |
| Assets | `ApplyEffect`, `CastAt` and `GiveItem` |
| Dropping | `Drop` (at a point of the screen) and `DropAt` (on a unit or a point) |
| The lab | `Select`, `SetMode`, `ResetDummies` and `Clear` |
| Its time | `Step`, `Paused`, `Speed` and `StepFrame` |

### Libraries

A lab's library, the assets it plays, is an `ILabLibrary`.

| Library | Where | What it holds |
| --- | --- | --- |
| `EditorLabLibrary` | The editor | Every asset of the project. |
| `ListLibrary` | Code | The assets given. |
| `ListLibrary.Of(project)` | A game's own build | The assets its Forge Project's catalog names. The lab plays it where no editor gives it a library. |

A library offers projects to switch between through `ILabLibrary.Projects`, `Project` and `UseProject`, which have
defaults.

### Your unit in code

The lab's members for your unit match its **Your unit** section.

- The unit you control is `ControlledUnit`, given by `PutDownOwnUnit`, `TakeControl` and `ReleaseControl`.
- Its camera is set by `Follow`.
- The lab's controls are its `Controls`, a `LabControls`, whose `Driver` comes before its rules'.

A game's own player can be a `LabControls` too, as the Demo's is: its unit set on it, or its `ControlledUnit()`
overridden.

### Showing its working in code

What the lab shows of its working is its `Showing`, a `LabShowing`.

| Member | What it is |
| --- | --- |
| `Journal` | An `EffectJournal` over every unit. |
| `Stops` | Its stops, added with `Add(stop)`. As one fires, `StoppedBy` and `StoppedFor` say which and what fired it. `Resume()` lets it go, and `StepTick()` steps on as **Step Tick** does. |
| `Watches` | Its watches, pinned with `Pin(watch)`. |
| `Log` | ForgeLog's messages. |
