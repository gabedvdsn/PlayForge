# The tooling wishlist: what PlayForge should provide

Decision 45 (2026-11-11, the user's request): recreate abilities, effects, entities and items from Dota 2, Risk of Rain
2 and Hades as assets, not code, and list what PlayForge should provide to make the whole workflow approachable:
authoring, testing, debugging, iterating and managing a project's assets, and the limits of the system as it stands.
Each item is sorted:

- **Framework need:** in the package's runtime or editor, there in a blank project. The rule leans this way: anything
  a common workflow meets, whatever the genre.
- **Additional Content:** an importable sample, *Additional Content*, beside Demo and Data: cues, presets and rules
  past PlayForge's basic scope, which a project imports when it wants them and can do without.

The labs are in Proving Grounds, each played by a bot: `Tests/Labs/DotaLabTests.cs` (the first ten effects, FEAT-31),
`DotaAbilitiesLabTests.cs`, `RiskOfRainLabTests.cs` and `HadesLabTests.cs`. What they needed that PlayForge doesn't
have is in the Lab Kit, `Assets/ProvingGrounds/LabKit/`: seven small classes, each standing in for an item here, so
the labs' assets use them as an author would a built-in. As each item lands, its class goes and the labs use the
built-in (AC-6): TW-5's and TW-6's have. The rows these items became are under *The tooling wishlist
(FEAT-38)* in `Docs/REFACTOR_PLAN.md`. The user approved the sort on 2026-11-11 and settled the shapes left open
(Decision 46): an attribute's role, which an effect can override (TW-1); a generic *Value Pipeline* asset, which an
effect names as its shared calculation (TW-2); and a body that is a MonoBehaviour process (TW-4).

## The sort at a glance

| ID | Item | Sort |
|---|---|---|
| TW-1 | Attributes say how their bonuses accumulate and how a number reads them | Framework |
| TW-2 | A damage pipeline, defined once, with a breakdown of each hit | Framework |
| TW-3 | Attribute reads choose their retention groups | Framework |
| TW-4 | Death that doesn't end the entity, and its body as a process | Framework |
| TW-5 | Gameplay events, built in, with the instigator apart from the targets | Framework |
| TW-6 | On-hit and on-kill effects as fields of items, effects and abilities | Framework |
| TW-7 | Effect modifiers: change other effects as they're made | Framework |
| TW-8 | Chance, built in: rolled once, pseudo-random, luck, proc coefficient | Framework |
| TW-9 | Number math: stacking curves, division, missing and share of base | Framework |
| TW-10 | Areas without physics, for both hubs, and auras | Framework |
| TW-11 | Thresholds that act | Framework |
| TW-12 | Absorption and block | Framework |
| TW-13 | Rules that come with an effect | Framework |
| TW-14 | Effects that land on the caster | Framework |
| TW-15 | The common rules, built in | Framework |
| TW-16 | Crowd control, project-wide | Framework |
| TW-17 | An ability's cost and cooldown apply unless told otherwise; charges | Framework |
| TW-18 | Changing sides as the game runs | Framework |
| TW-19 | Conditions in requirements | Framework |
| TW-20 | A transfer impact, and toggles | Framework |
| TW-21 | One line for each asset | Framework |
| TW-22 | A combat log | Framework |
| TW-23 | Recipes, a worker map, and what each context holds | Framework |
| TW-24 | Asset templates | Framework |
| TW-25 | A content linter | Framework |
| TW-26 | A test kit in the package | Framework |
| TW-27 | Tables by level, and from spreadsheets | Framework |
| AC-1 | The Additional Content sample | Additional Content |
| AC-2 | Cue handlers | Additional Content |
| AC-3 | Formula presets | Additional Content |
| AC-4 | Genre rules too narrow for the core | Additional Content |
| AC-5 | A starter set of generic assets | Additional Content |
| AC-6 | Retiring the Lab Kit | Additional Content |

Already tracked, and fed by the labs: FEAT-33 (stacks for each source, stacks that fall off, charges, a continuous
change that counts each stack), FEAT-35 (the Creator Lab, where these were wanted by hand), FEAT-37 (a builder for
assets made in code; the labs' generators are about 900 lines of the same helpers). Bugs found on the way are GAS-72 to
GAS-76, at the end.

## Framework needs

### The user's three

**TW-1. Attributes say how their bonuses accumulate, and how a number reads them.** The user's concern, 2026-11-11:
several effects give Damage Amplification, and a scaler that works out attack damage reads it; how does it add up in a
way that makes sense? Today the accumulation is each effect's choice, not the attribute's: an impact entry's Multiply
Behaviour pools Additive Bonus multipliers into one `×(1 + Σ)` and compounds Multiplicative ones (the pipeline's
Multiply stage), and a flat Add sums. Nothing on the attribute says which its bonuses are, so two authors can give one
attribute bonuses of both kinds; and each number that reads it writes `1 + x` or `1 − x` by hand. Laguna Blade's
"less magic resistance" is four scalers, a Group of a Group of `MR × −1` and a constant 1.

The labs show what works and what's wanted. God's Strength's +100% and another +50% on Attack Damage pool to 250, as
Dota's do, because both effects chose Additive Bonus; Hades' Attack boons pool on Strike Damage the same way; Weak's
−30% on a foe's Damage Dealt is a Multiplicative of its own, so Megaera's rage and Weak make 1 × 1.5 × 0.7. Wanted, on
the attribute:

- a **role**: a plain value; a bonus share, whose bonuses sum and which numbers read as `1 + x`; a multiplier, whose
  bonuses multiply; a resistance or a chance, whose bonuses stack with diminishing returns (`1 − Π(1 − x)`, so two
  30% reductions make 51%, as Hades' do) and which numbers read as `1 − x`;
- its **buckets**, by retention group or tag: bonuses within a bucket add, buckets multiply. Hades adds its boons'
  bonuses and multiplies its reductions and its crits; Risk of Rain adds an item's stacks and multiplies across items;
- and in a number, **× Attribute** offering *as a multiplier* and *as a resistance* beside its value, so `1 − MR` is one
  member.

**TW-2. A damage pipeline, defined once, with a breakdown.** Every hit in the labs that a bonus changes had to write the
bonus into itself. The Risk of Rain bullet carries Crowbar's term (75% a stack while the target is above 90%) and the
critical roll; Hades' Strike carries Pressure Points' crit, so Doom and Hangover, which don't, can never be critical,
where Hades makes any damage critical; each foe's attack reads its own Damage Dealt for Weak to matter; Laguna Blade and
Storm Hammer each carry `1 − MR`. A new item that changes hits means editing every attack. Unreal's answer is a meta
attribute, Damage, filled by an execution calculation that captures the source's attack and the target's armor, then
applied to health and cleared ([damage pipeline](https://gas-reference.pages.dev/patterns/damage-pipeline/),
[an execution calculation for damage](https://gas-reference.pages.dev/examples/exec-calc-damage/)). Wanted: a *Damage
Pipeline* asset, one per project or per damage type (physical, magical, pure), whose ordered stages each hit of that
type goes through:

1. the hit's own amount, as its effect has it;
2. the source's outgoing bonuses, by TW-1's buckets;
3. conditional bonuses that read the other side, as Crowbar's "target above 90%";
4. a critical roll, once for the hit (TW-8);
5. the target's incoming reductions: armor through a curve (TW-9), magic resistance, God Mode's;
6. absorption: a barrier or a shield first (TW-12);
7. what's left, to health.

Each stage keeps what it did, so a hit's breakdown can be read in a test, in the combat log (TW-22) and in the
Debugger: *12 base × 1.75 Crowbar × 2 critical, less 0 armor, 8 to barrier, 34 to health*. God Mode shows the target
half already works without code: a Math attribute worker on Zagreus's health takes his Damage Resistance off every
blow.

**TW-3. Attribute reads choose their retention groups.** The user's note, 2026-11-11: wherever an attribute's value is
read (the × Attribute number, an Attribute condition, the Math worker's Relative To, Asset Part, a threshold), it should
read one group, several by tag, or ignore them and read the whole. Today × Attribute reads the whole, and one group's
part is a kind of its own, `GroupImpactKind`, one group only. Wanted: one *Groups* field on every attribute read: All,
Only These, All But These, by tag and the tags under it, with GroupImpactKind folded in. Dota's God's Strength is a
bonus of Sven's *base* damage, not his items'; with the field it reads Attack Damage's own group, where the lab's
multiplies the whole.

**TW-4. Death that doesn't end the entity, and its body as a process.** The user's request, 2026-11-11: in a MOBA, or
any game where a character dies and comes back, the entity has to stay while only its body goes: its items, levels,
gold, cooldowns and lasting effects remain, its body is gone until it respawns. Today death is final: `GASCore.MarkDead`
marks the entity dead, and its frame's end hands it to its hub, which ends it, a MonoBehaviour hub at once, a runtime
one queued. Unreal keeps the ability system on the player state, the *owner*, and the character is its *avatar*, so the
avatar can die and spawn again while the attributes and effects stay
([ASC on the PlayerState](https://gascompanion.github.io/asc-on-player-state),
[the ability system component](https://gas-reference.pages.dev/core-concepts/ability-system-component/)). Wanted:

- a **death policy** on the entity: *End*, as today, or *Stay*: dead, still registered, abilities refused (Is Alive
  already refuses them), effects kept or ended by tag (*Persists Through Death*);
- **revival**: an action and an API to bring it back at a point, with health as its revival says, and an effect whose
  end revives, as a respawn timer, which buyback can shorten;
- a **body instantiator**: the entity's model is a process it spawns from a template at a spawn point, despawns as it
  dies, and spawns again as it revives; the entity itself, a runtime GAS, never leaves;
- **events**: Died, Revived (TW-5).

Decided (Decision 46): the body is a MonoBehaviour process spawned from a prefab, and the entity, a runtime GAS,
follows it for its position (targeting, areas) while it's there.

### Events, on-hit and modifiers

**TW-5. Gameplay events, built in.** Ability triggers listen for gameplay events (FEAT-4), but PlayForge sends none of
its own, so "when attacked", "on kill" or "when healed" takes code. Counter Helix needed a Lab Kit worker that sends
Axe an event as he's hit. Wanted, sent by the core: hit dealt and hit taken (with the dealer, the amount and the impact
types), kill and death, heal, block and evade, critical hit, effect applied and removed, ability activated. The event's
data needs the **instigator in a place of its own**: the lab first made the dealer the event's target, and Counter
Helix, an area ability, spun at the dealer alone, as the target took the place of what its search found.

**TW-6. On-hit and on-kill effects as fields.** Seven of the labs' assets act as their holder hits: Desolator's
Corruption, Tri-Tip Dagger's bleed, Topaz Brooch's barrier on a kill, and four Hades boons' Weak, Chill, Doom and
Hangover. Each needed the Lab Kit's `OnHitApplyEffectsImpactWorker`. Wanted, on an item, an effect and an ability:
*On Hit* and *On Kill*, each a list of effects, with impact types to take (so a bleed's ticks, typed apart, set off
nothing, its proc coefficient 0), to whom (the one hit or the holder), and a condition. Building it found GAS-72.

**TW-7. Effect modifiers: change other effects as they're made.** Old item 5, and the labs' biggest repeated cost. An
item or boon that changes its holder's hits (Crowbar, Lens-Maker's Glasses, Pressure Points, Coup de Grace, Fury
Swipes, Jingu Mastery) must be written into each hit; one that changes another boon's status (Arctic Blast bursting
Chill, Dire Misfortune growing Doom) can't be its own asset at all: the lab folded Arctic Blast into Chill. Wanted: a
modifier list on an item, an effect or an entity: *for each outgoing (or incoming) effect tagged X: add this packet,
scale this impact, add this overflow, extend this duration*. With TW-2, damage modifiers go in the pipeline; this is
for the rest.

**TW-8. Chance, built in.** The labs needed a chance in a number (crits: the Lab Kit's `ChanceCondition`), a chance to
block (Tougher Times), a chance for an ability to go (Counter Helix), and a chance on hit (Tri-Tip, through the bleed's
Chance to Apply, which works). Wanted:

- a **Chance condition** in numbers, its roll drawn once for each hit and kept with it, so a hit read twice (its
  preview, its tracking) reads the same;
- a **chance to activate** on an ability, rolled once for each activation: activation rules run twice (GAS-73), so the
  lab rolled Counter Helix's chance in its event worker;
- **pseudo-random distribution** for a caster, which evens out streaks, as Dota's (old item 10);
- **luck**: roll *n* times, keep the best, or the worst (Risk of Rain's 57 Leaf Clover, Purity);
- **proc coefficient**: what a hit carries for its on-hit effects, scaling their chance and duration (Risk of Rain:
  Railgun 3.0, Double Tap 1.0, Ukulele 0.2; 0 sets off nothing). A hit carries no data its on-hit effects can read.

**TW-9. Number math.** Risk of Rain stacks items linearly (Soldier's Syringe, 15% a stack), hyperbolically (Tougher
Times, `1 − 1/(1 + 0.15n)`, 13% with one, 60% with ten), reciprocally and exponentially; Dota's armor leaves
`1/(1 + 0.06 × armor)` of a physical hit; an attack speed shortens a wind-up as `a / speed`. No number divides, so each
needed the Lab Kit's `StackingCurveKind` (linear, hyperbolic, reciprocal, inverse, exponential, plus a constant).
Wanted: those curves as a number kind, a Divide in a Group, and old item 6's *Missing* (base − current) and *Share*
(current ÷ base), which would make Mana Void and the Crowbar's "above 90%" one member each.

**TW-10. Areas without physics, for both hubs, and auras.** Area targeting searches with physics, so its targets need
colliders; with no range it searches the scene for MonoBehaviour entities, so a runtime entity is never found
(GAS-75); and it always leaves the caster out. So Storm Hammer hits its target alone where Dota's stuns an area around
it, Mekansm doesn't heal its holder, and Counter Helix's and Mekansm's tests run on the MonoBehaviour hub only. Wanted:
a spatial registry of every entity's position on both hubs, searched around the caster, a target or a point, by
affiliation, with *include the caster*; an effect that lands on an area around its target (old item 16); and an aura,
an effect that holds on whoever is near its holder while it lasts.

**TW-11. Thresholds that act.** Megaera rages as her health falls past half. The Threshold analysis worker compares an
absolute value, only logs, and shares its "has fired" key with every worker of its type on the entity (GAS-76), so the
lab used its own `ThresholdCrossingAttributeWorker`. Wanted: a threshold by share of base or by value, falling or
rising, once or each time, that applies effects and sends an event, without code. Ice Blast's shatter (execute below
13%) is the same shape.

**TW-12. Absorption and block.** Topaz Brooch's barrier takes a hit before health and fades over 30 seconds; Tougher
Times blocks a hit outright, so nothing of it lands and no on-hit goes off. Both needed Lab Kit workers
(`BarrierAttributeWorker`, `BlockChanceAttributeWorker`), and the fade an effect of its own on the item. Wanted: an
attribute role (TW-1) *absorbs for* another, with a decay; and block and evade as pipeline stages (TW-2) that a hit can
fail, with the events TW-5 sends.

### Rules from the Dota lab (FEAT-31), carried over

**TW-13. Rules that come with an effect.** Old item 2: an effect grants tags and abilities but not workers, so a debuff
that changes how its target takes damage (Ice Blast's no-healing and shatter, Shallow Grave, Blade Mail) gives its
target workers as it lands and takes them back. Items and abilities have worker groups (an ability's are given as it's
granted, which Counter Helix's worker used); effects should too.

**TW-14. Effects that land on the caster.** Old item 1: contained effects, overflow effects and packets land on the
target alone. A Target or Caster choice on each.

**TW-15. The common rules, built in.** Old item 3: a healing-received multiplier (which 0 blocks), execute below a
share of maximum, can't fall below a value, damage return with a flag so returned damage isn't returned again, and
lifesteal on an effect with a condition.

**TW-16. Crowd control, project-wide.** A stunned hero mustn't cast, and the lab's Storm Hammer test shows it works
only because each ability lists Stunned among the tags it avoids. Wanted: project-wide activation rules by tag
(Stunned stops every ability, Silenced spells, Disarmed attacks), which also cancel what's running, as a stun breaks a
channel; an ability opts out (Black King Bar's Avatar can be used while silenced in Dota, not while stunned).

**TW-17. An ability's cost and cooldown apply unless told otherwise; charges.** Laguna Blade had a cost and a cooldown
and paid neither: they apply only from a stage marked *Apply Usage Effects*, which is off. An author who sets a cost
expects it paid. Wanted: cost and cooldown apply as the ability starts unless a stage takes them; and charges, several
uses with a cooldown each, as Hades' Cast and many abilities have.

**TW-18. Changing sides as the game runs.** `GASCore.SetAffiliation` exists but neither hub has it, so an entity's team
is its identity's for good; the labs set teams through each test entity's identity. Wanted: the hubs' setter, and an
effect that changes its target's side while it lasts (mind control, conversion).

**TW-19. Conditions in requirements.** Old item 8: an effect's requirements take tags only; a condition there would
stop Attribute Shift at its floor once and end an effect as a condition stops holding.

**TW-20. A transfer impact, and toggles.** Old items 7 and 9: one entry that moves an amount from one attribute to
another (Attribute Shift, Essence Shift); and an ability that toggles an effect, paying its cost as it turns on.

### The workflow

**TW-21. One line for each asset.** Old item 12, done for effects at a level (LVL-3): the same for abilities ("a bolt a
quarter second after the cast: 450 / 600 / 750 magical; 280 / 420 / 680 mana; 70 / 60 / 50 s"), items and entities, in
the inspector's header and The Forge's list.

**TW-22. A combat log.** Old item 13: a log of hits for each entity, by effect and by dealer, with TW-2's breakdown,
readable in a test and in the Debugger. The lab tests read health before and after.

**TW-23. Recipes, a worker map, and what each context holds.** Old items 14 and 15: recipes (a curse that bursts, a
counter, a crit, an execute, lifesteal, damage return, can't die, on-hit, on-kill, a phase), which worker hook runs
where and for whom, and *Unworkable* documented. The labs found two traps a map would have named: an impact worker
runs for the dealer but its context's System is the one hit (GAS-72); and a stacking effect's periodic tick runs once
for each stack while its lasting change counts once, so Hangover, which read its stacks, took 100 a tick where 20 was
meant (FEAT-33 has the fix).

**TW-24. Asset templates.** Each lab asset started from the same few shapes. The Forge's *New* menu: Damage Effect, Damage
over Time, Buff, Debuff, Stun, Aura, On-Hit Item, Active Item, Passive (Triggered) Ability, Hero; each made with its
impact types, stacking, cost stage and tags set, ready to fill in.

**TW-25. A content linter.** In the Problems window, with quick fixes (FEAT-25): an ability with a cost or cooldown no
stage applies (TW-17); a periodic magnitude that reads its own stacks; a number reading an item's stacks for an item no
asset grants; an on-hit filter of an impact type no effect deals; an ability trigger whose event nothing sends; an
ability a stun can't stop.

**TW-26. A test kit in the package.** Each lab's tests rest on the same helpers, now `Tests/Labs/LabTest.cs`: a clock
whose frames each take an exact step, rolls the test writes with a count of what's drawn, an entity made from an
identity asset on either hub, and Cast, Use, Hold and Hit. Proving Grounds has them; a project testing its own content
doesn't. Wanted: those, and the harness's world, as a package test assembly, so "play it in a test" is a workflow and
not a project of its own. (A trap for its docs: `Tag` converts to a string, so NUnit's `Does.Contain(tag)` compares
strings and fails on a list of tags.)

**TW-27. Tables by level, and from spreadsheets.** Laguna Blade's 450 / 600 / 750 by level is a By Level number set to
read its table at the ability's level (*Stretch Over Levels* off), and it reads exactly. An ability's or an item's
numbers by level are a table far more often than a curve; a *Table* mode, named so in the inspector, would say so
plainly. And games balance in spreadsheets: import numbers by level and item stats from CSV.

## Additional Content

**AC-1. The Additional Content sample.** `Samples~/Additional Content`, imported from the Package Manager as Demo and
Data are, with an assembly of its own that references the runtime and that nothing in the runtime references, so a
blank project builds without it.

**AC-2. Cue handlers.** The cue system is the framework's (FEAT-1); handlers that make a project's look are content:
floating damage numbers (reading TW-2's breakdown, crits marked), a hit flash, a status icon bar, screen shake, a sound
on hit.

**AC-3. Formula presets.** Named presets of TW-9's curves and TW-1's roles for a genre's formulas: armor as Dota has it
(0.06, reciprocal) or as `100 / (100 + armor)`, flat armor; Risk of Rain's item curves; Dota's pseudo-random constants
for TW-8.

**AC-4. Genre rules too narrow for the core.** Attribute Shift's moving of points between attributes over time, if the
transfer impact (TW-20) doesn't cover it, and such rules as the labs meet that only one genre uses. The labs' other
workers move to the framework (AC-6).

**AC-5. A starter set of generic assets.** A melee attack, a bolt, a stun, a damage over time, a slow, a shield, an
on-hit item and an on-kill item, a boss with a second phase, built from framework features alone and named for what
they are. The labs' recreations stay in Proving Grounds: they bear other games' names.

**AC-6. Retiring the Lab Kit.** As each framework need lands, its Lab Kit class goes and the labs' assets switch over:
`OnHitApplyEffectsImpactWorker` (TW-6), `HitTakenEventAttributeWorker` (TW-5), `ThresholdCrossingAttributeWorker`
(TW-11), `BarrierAttributeWorker` and `BlockChanceAttributeWorker` (TW-12), `ChanceCondition` (TW-8),
`StackingCurveKind` (TW-9); and the Dota lab's seven workers to TW-13 to TW-15. TW-5's and TW-6's went on 2026-11-11:
Counter Helix's trigger takes Impact Taken, Health's losses to attacks, on its 20%; the seven on-hit assets have On Hit
entries.

## Found on the way

| ID | What | Where |
|---|---|---|
| GAS-72 | An impact worker runs for the dealer, but its context's System is the one hit, so a worker that took System for its holder applied its holder's effects to the target, and read the target's items for its holder's | `ImpactWorkerCache.RunImpactData` (`ImpactWorkerContext` made with `impactData.Target`) |
| GAS-73 | An ability's source activation rules run twice for each activation, at the request and as its handle starts, so a rule with a side effect, as a roll, runs twice | `AbilitySystemComponent.cs` (`SourceRefusal`), `AbilitySpecContainer.cs` (`ValidateAllActivationRequirements`) |
| GAS-74 | A delay of 0 between an ability's effects waits a frame: Apply Effects awaits `SequenceTaskLibrary.Delay(0)`, which ticks once, so an ability's second effect lands a frame after its first (Mekansm's lockout after its heal) | `ApplyEffectsAbilityTask.cs`, `SequenceTaskLibrary.Delay` |
| GAS-75 | Area targeting with no range finds MonoBehaviour entities alone (`FindObjectsByType<GameplayAbilitySystem>`), never a runtime one; with a range it needs colliders | `AutoTargetByAffinityTask.cs` (`GatherCandidates`) |
| GAS-76 | An analysis worker's "has fired" key is its type's name, so two Threshold workers on one entity share it: the first to fire marks the other | `AbstractAnalysisWorker.cs` (`MyKey`) |

## The labs, and what each asset took

### Dota 2, second lab: abilities, items and an entity

| Asset | Built from | Code it needed |
|---|---|---|
| **Laguna Blade** (ability) | A delay of 0.25 s, then a magical bolt of 450 / 600 / 750 by level less magic resistance; cost 280 / 420 / 680 mana, cooldown 70 / 60 / 50 s, all tables by level | None; the cost and cooldown needed a stage marked (TW-17) |
| **God's Strength** (ability) | Self-targeted; +100 / 150 / 200% Attack Damage for 25 s, Additive Bonus, so it pools with other bonuses | None |
| **Storm Hammer** (ability) | Magical damage by level and a 2 s stun granting Stunned, which each ability avoids | None; one target where Dota's is an area (TW-10) |
| **Counter Helix** (ability) | Triggered by an event, an area spin of pure damage by level at each enemy | Lab Kit: an event as Axe is hit, on a 20% chance (TW-5, TW-8) |
| **Black King Bar** (item) | +24 Attack Damage while held; its ability's Avatar blocks effects tagged magical for 9 s | None |
| **Desolator** (item) | +50 Attack Damage; each attack Corrupts, −6 armor for 7 s, refreshed; attacks deal `1/(1 + 0.06 × armor)` | Lab Kit: on hit (TW-6), the armor curve (TW-9) |
| **Mekansm** (item) | Its ability heals each ally 275, unless healed by one in 21 s, a lockout the heal's requirement avoids | None; doesn't heal its holder (TW-10) |
| **Sven** (entity) | The Strength hero's set, the Radiant, Storm Hammer and God's Strength | None |

### Risk of Rain 2

| Asset | Built from | Code it needed |
|---|---|---|
| **Soldier's Syringe** (item) | +15% attack speed a stack, Additive Bonus, from Item Stacks | Lab Kit: the curve, `1 + 0.15n` (TW-9) |
| **Tougher Times** (item) | Blocks a hit on `1 − 1/(1 + 0.15n)` | Lab Kit: block, hyperbolic curve (TW-12, TW-9) |
| **Crowbar** (item) | Nothing of its own: every attack reads its stacks, ×(1 + 0.75n) above 90% health | None; written into the bullet (TW-2, TW-7) |
| **Tri-Tip Dagger** (item) | On hit, a bleed of 240% base damage over 3 s, 10% a stack, typed apart so its ticks set off nothing | Lab Kit: on hit (TW-6), the curve (TW-9) |
| **Topaz Brooch** (item) | On a kill, 15 barrier a stack, fading by an effect it grants | Lab Kit: on kill (TW-6), barrier (TW-12) |
| **Lens-Maker's Glasses** (item) | +10% crit chance a stack; the bullet doubles on a roll under it | Lab Kit: chance (TW-8) |
| **Double Tap** (ability) | Two bullets, 0.1 s apart | None |
| **Commando** (entity) | The Survivor set, its barrier soaking hits first, and Double Tap | Lab Kit: barrier (TW-12) |

### Hades

| Asset | Built from | Code it needed |
|---|---|---|
| **Strike** (ability) | Strike Damage, triple on a critical roll | Lab Kit: chance (TW-8) |
| **Heartbreak Strike** (boon, an item) | +50% Strike Damage, pooled; Weak on each Attack hit: the foe's Damage Dealt ×0.7 for 4 s | Lab Kit: on hit (TW-6) |
| **Frost Strike** (boon) | +40% Strike Damage, pooled; Chill on each hit, −4% speed a stack; the tenth stack bursts as Arctic Blast, 80, and clears it | Lab Kit: on hit, the curve; Arctic Blast folded in (TW-7) |
| **Curse of Agony** (boon) | Doom on each hit: 50 after 1.1 s, from an effect it holds for its removal; hits while Doomed add nothing | Lab Kit: on hit |
| **Drunken Strike** (boon) | Hangover on each hit: 4 a stack every 0.5 s for 4 s, up to 5 | Lab Kit: on hit; the stacks counted once (TW-23) |
| **Pressure Points** (boon) | +2% crit chance; only hits that read it can be critical | None of its own (TW-2) |
| **God Mode** (effect) | +20% Damage Resistance, which a Math worker on Zagreus's health takes off every blow | None |
| **Zagreus** and **Megaera** (entities) | Their sets and Strike; Megaera rages past half her health, +50% Damage Dealt, once | Lab Kit: a threshold that acts (TW-11) |

Of the 25 assets, 11 needed no code of their own; the other 14 used seven Lab Kit classes, each a framework need above.
Counter Helix and the seven on-hit assets have since moved to the built-ins, TW-5's events and TW-6's On Hit (AC-6), and
the Lab Kit is five classes.

## Research

- Unreal's Gameplay Ability System, from the community references: effect stacking (by source or target, limits,
  refresh, overflow), immunity, the modifier order with additive and compound multipliers, magnitudes, execution
  calculations and meta attributes for damage ([gas-reference](https://gas-reference.pages.dev/),
  [damage pipeline](https://gas-reference.pages.dev/patterns/damage-pipeline/),
  [execution calculation](https://gas-reference.pages.dev/examples/exec-calc-damage/)); the owner and the avatar, and
  the ability system on the player state so it outlives a respawn
  ([GAS Companion](https://gascompanion.github.io/asc-on-player-state)).
- Risk of Rain 2 ([wiki](https://riskofrain2.wiki.gg/)): item stacking (linear, hyperbolic, reciprocal, exponential),
  proc coefficients, luck, and the items above.
- Hades: status effects ([wiki](https://hades.wiki.fextralife.com/Status+Effects)); percentage damage bonuses added
  together on the base, reductions and crits multiplied
  ([Hades wiki, gameplay mechanics](https://hades.fandom.com/wiki/Gameplay_mechanics/Hades_II),
  [a guide to damage](https://gameplay.tips/guides/hades-guide-to-damage.html)).
- Dota 2's mechanics as the first lab used them, armor's `0.06 × armor / (1 + 0.06 × |armor|)` and pseudo-random
  chance among them.
