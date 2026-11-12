# PlayForge — The Creator Lab

A space for trying a project's own assets: put units down, give them abilities, effects and items, cast, and watch the
numbers move. Open it from **Window → PlayForge → Creator Lab**: it opens in a new scene and plays. The scenes open
before are saved first, if you want them saved.

The lab plays every PlayForge asset in the project's own folders. Its design is `Docs/CREATOR_LAB_DESIGN.md` in the
repository; what's built so far is the space, its units and dummies, its panels and its time (phase 2, LAB-1 to LAB-8),
and battle (phase 3, LAB-9 and LAB-10).

## The space

- **The ground** is a plane in the middle of the scene, lit from above.
- **The camera** looks down at an angle. Scroll to pull it back or bring it in; drag with the middle button to pan.
  The right panel's sliders set its distance and tilt, and **Reset view** puts it back.
- **Units** are a body and a `GameplayAbilitySystem`: a capsule with a nose to show where it faces, a ring at its feet
  in its side's color, and a bar over it with its name and health. A unit walks in a straight line where it's sent and
  attacks what it's set on. PlayForge has no movement of its own, so the lab's is its own: a project's game moves its
  units its own way.
- **The lab's caster** is a unit of the lab's own, on no side, with no body. It's the source of what you drop while no
  unit is selected.

## The panels

Each panel collapses to a tab at its edge with its − button.

- **Library** (left): the project's assets by kind, abilities, effects, items, entities, attribute sets, attributes
  and scalers, each kind by name. The search box filters them; **Refresh** lists them again after you make or delete
  one. A kind's color shows in its icon alone.
- **Selected** (middle): hidden until something is selected. Click an asset in the library to see its numbers level by
  level, as "−80/−120/−160 Health"; **Open in The Forge** opens it there. Click a unit to see its attributes, tags,
  effects with their stacks and time left, abilities with their levels and cooldowns, and items. There, − and + change
  an ability's level, × takes an ability or an item, and **Remove** takes the unit out.
- **Lab** (right): the mode, the test dummies, the camera and the lab's time.
- **Log** (bottom): collapsed; its journals come with phase 5.

## Dropping assets

Drag an asset from the library and drop it on the space:

| Dropped | On | What happens |
|---|---|---|
| An effect | A unit | It lands on the unit, from the selected unit, or from the lab's caster |
| An ability | A unit, or the ground | The selected unit, or the lab's caster, casts it there, its target in the activation's data, as click targeting takes it with no click. A unit that doesn't know it learns it first. An ability that needs more than the point waits for a click, which **Esc** cancels |
| An item | A unit | It's given to the unit, equipped |
| An entity or an attribute set | The ground | A test dummy of it is put down, on the side the Lab panel's picker names |

## Test dummies

The **Test dummies** section puts down a dummy of any entity or attribute set, on a side: Blue, Red, Green, Gold or
Neutral. A dummy counts what it takes: its damage a second, the last blow by its impact types and the effect it came
from, and a short history, which the Selected panel shows. It doesn't die unless asked: falling, it's revived where it
stands, at full. Left alone for three seconds, it mends to full. **Reset** puts each dummy back where it was put, at
full; **Clear** takes every unit out.

An effect or an entity changed in the inspector or The Forge as the lab plays is read on its next application, as
every asset is. What a unit was made from, its attribute set and its starting abilities, shows on a new unit.

## Battle

The **Battle** mode fights teams of bots. Pick an entity and a side above, a level in **Level**, and **Add bot**; each
side stands in a line of its own, Blue to the south, Red to the north, Green to the west and Gold to the east. **Quick
battle** makes Blue against Red, **A side** of each, of the entity selected in the library, or picked at random from the
project's. **Play** starts the fight and the lab's time; the line under the buttons says who stands, and who won, or
that no one stands. **Reset** makes every bot anew where it started, at full, with the items and abilities you gave it
since and their levels.

A bot takes the nearest foe it can target, casts what's ready, and otherwise attacks with its attack. Its order and its
moments are its rules':

- **Cast Order Key**: the Local Data key an entity's order is under, as "W Q E", the letters its abilities other than its
  attack, in its entity's order. With none, each in turn.
- **Cast When Key**: the key for when it casts an ability: **InRange** (the default), **Hurt** (below 60% health),
  **Low** (below 35%, away from its foe, as an escape), **Stunned** (while something stops it) or **FoeLow** (its foe
  below half).
- **Reach Key**: the key for an ability's reach, 0 for anywhere.

Low on health, a bot backs off a moment once in a while. The Demo's AI hero is a bot, its roster giving the keys.

## Time

The lab's units live in a world of the lab's own (`ProcessWorld`), which the lab steps by its own time, so its time
stands still or runs faster without touching Unity's. **Pause** stops it, **Step** goes one frame (a sixtieth of a
second) while it's paused, and **Speed** runs it from a quarter to four times as fast. The camera and the panels go on
while it's paused.

## The lab's rules

The lab needs to know a few things about a project: which attribute is a unit's health, which it walks at, which tags
stop, hide or shield it, and which ability is its attack. With no rules given, it takes the attributes playing the
project's Health and Movement Speed roles ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), or else finds the attributes named Health and
Movement Speed (or Move Speed), and walks a unit without a speed at 4 a second. To say more, make a **Lab Rules** asset
(**Create → PlayForge → Lab Rules**) and give it to the lab's `CreatorLab` component:

- **Health** and **Movement Speed**, or **Walk Speed** where a unit has none. Left empty, each is the project's.
- **Stops**: tags that stop a unit moving, as Stunned. **Hides**: tags under which the other side can't see or target
  it, as Invisible. **Shields**: tags under which no one can target it.
- **Attack Range Key**: the Local Data key a unit's attack range is under, or **Attack Range** for every unit.
- **Attack Slot**: which of an entity's starting abilities is its attack, from 0, or −1 for none.

The Demo's roster is the Demo's rules: its units, camera and dummy are the lab's, so the two can't drift.

## In code

`CreatorLab.Create(library, rules, panels)` makes a lab, as the tests do. Its calls are the panels': `AddUnit` and
`AddDummy`, `ApplyEffect`, `CastAt`, `GiveItem`, `Drop` (at a point of the screen) and `DropAt` (on a unit or a point),
`Select`, `SetMode`, `ResetDummies`, `Clear`, and `Step`, `Paused`, `Speed` and `StepFrame` for its time. A library is an
`ILabLibrary`: in the editor every asset of the project (`EditorLabLibrary`), in code a `ListLibrary` of the assets
given.

A build can leave the lab out by defining `PLAYFORGE_NO_LAB`. The Demo uses the lab's units, so it goes too.
