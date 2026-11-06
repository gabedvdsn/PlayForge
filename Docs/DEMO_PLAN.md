# The Demo: design for Phase 7

The new demo replaces Demos and Swarm Defender (Decision 37). It is a small hero arena, in the style of a MOBA:
pick a hero, pick an opponent, give items, and fight. Its job is to show PlayForge as a whole way to build a
GAS-based game. Every hero, ability, item and effect is a ScriptableObject asset, and between them they use every
asset feature. This plan is for the user to review before anything is built.

The user's answers that frame it (Decision 38):
- **Controls:** click the ground to move, click an enemy to attack, and keys for abilities.
- **Opponents:** an AI hero that fights back, and a training dummy that stands and takes damage.
- **Textures:** placeholders made in code, saved as texture assets, to swap for art later.
- **Packaging:** one sample, Demo, in place of Demos. Battle stays.

## The scene

- **The arena:** a flat, round floor with a few pillars, seen from an angled camera that follows the player's hero.
  Heroes are primitive shapes in their hero's color, as Battle's are, with a placeholder icon over each.
- **Controls:**
  - Right-click the ground to move. Right-click an enemy to attack it: the hero walks into range, then attacks.
  - Q, W and E cast the hero's abilities. An ability that needs a target waits for a left-click on one, or on the
    ground. 1 to 6 use the items' abilities.
  - S stops.
- **The panel**, on the left, built with UI Toolkit:
  - **You:** a hero picker, the hero's level with − and +, and its items, each with Remove.
  - **Opponent:** an AI hero or the training dummy. For an AI hero, a hero picker and its level, as for yours, and
    its difficulty: Easy, Normal or Hard.
  - **Items:** the item list, each with Give to You and Give to Opponent.
  - **Fight:** Reset puts both back at full health, in their places.
- **The HUD**, at the bottom:
  - Health and mana bars, and the ability bar with each ability's cooldown and mana cost.
  - The attributes, each as its value with the part items add after it, as "Strength 24 (+8)". The split comes from
    retention groups.
  - The buffs and debuffs on the hero, with their icons, stacks and time left. Effects marked Not Visible stay out
    of it.
  - Damage numbers over a target, in a color for each damage type, as cues.
- **The training dummy** has health it regains at once, so it never dies. It shows the damage per second it takes,
  and the last hit's numbers before and after mitigation.

## Attributes

The twelve of the user's spec. Every attribute asset has a placeholder icon, a name and an abbreviation.

| Attribute | Abbr. | What it does |
|---|---|---|
| Health | HP | Base plus 10 × Strength. The hero dies at 0. |
| Mana | MP | Base plus 12 × Intelligence. Abilities cost it. |
| Armor | ARM | Base plus 8 × Agility. Reduces physical damage taken: × 100 / (100 + Armor). |
| Magic Resistance | MR | A fraction, at most 75%. Reduces magic damage taken: × (1 − MR). |
| Movement Speed | MS | Units per second. |
| Strength | STR | Grows each level. Adds Health. |
| Agility | AGI | Grows each level. Adds Armor. |
| Intelligence | INT | Grows each level. Adds Mana. |
| Attack Amplification | AA | Amplifies physical damage dealt: × (1 + AA). |
| Magic Amplification | MA | Amplifies magic damage dealt: × (1 + MA). |
| Lifesteal | LS | Heals by this fraction of the physical damage dealt. |
| Cooldown Reduction | CDR | Shortens cooldowns: × (1 − CDR). Every hero starts at 0%. |

Pure damage ignores Armor and Magic Resistance, and no amplification touches it.

## Attribute sets

- **Hero** is the template every hero's set takes. Its sub-sets:
  - **Vitals:** Health, Mana and Armor. Each element's base is a Cached Attribute Backed scaler: a per-level table,
    plus the primary attribute times its constant. Health takes 10 × Strength, Mana 12 × Intelligence, and Armor
    8 × Agility. Health and Mana stay between 0 and their base, by Zero To Base.
  - **Primaries:** Strength, Agility and Intelligence, each a Cached Simple scaler by level.
  - **Offense:** Attack Amplification, Magic Amplification and Lifesteal, at 0.
  - **Defense:** Magic Resistance, kept between 0% and 75% by its bounds.
  - **Utility:** Movement Speed, and Cooldown Reduction at 0.
- **Each hero's set**, such as Vanguard Attributes, is a child of Hero. It overrides only the per-level tables of
  its primaries and the bases of its vitals. A push from Hero reaches all five.
- **The Hero set's workers** apply the damage model to every hero (below).
- **Training Dummy Attributes** has Health and Armor of its own and no sub-sets.

## The damage model

- **Damage types are tags:** Damage.Physical, Damage.Magic and Damage.Pure. A damage effect carries one as its
  context tag. Damage.Attack marks a default attack, and Damage.Ability an ability.
- **Damage effects take a template:** Physical Strike, Magic Strike and Pure Strike. Each is an Instant effect on
  Health with its context tag, a hit cue and a damage number cue. An ability's damage effect is a child of one, and
  sets only its magnitude.
- **Mitigation:** a worker on the Hero set, Mitigation, scales an incoming negative change to Health by the target's
  Armor or Magic Resistance, as the change's damage tag says. It is the demo's own attribute worker, authored in
  the sample.
- **Amplification:** an attribute worker scales the change by the source's Attack or Magic Amplification.
- **Lifesteal:** a Relative Operation Context impact worker on the Hero set heals the source by its Lifesteal times
  the physical damage dealt.
- **Cooldowns:** every cooldown effect's duration is a Scaler Group: its per-level table, multiplied by an Attribute
  Backed scaler of 1 − the caster's Cooldown Reduction.
- **Death:** a Death Watcher analysis worker on each hero. The dummy has none. A dead hero respawns at its start
  after five seconds, at the level it had.

## Heroes

Five heroes, each an EntityIdentity child of the **Hero** entity template. The template holds what every hero shares:
the Hero worker group, the default attack's range in Local Data, the death watcher and the starting level. Each hero
is a level provider, levels 1 to 10, that its abilities and attribute set take their levels from.

Each hero has a default attack and three abilities, Q, W and E. Every ability costs mana, has a cooldown, and is a
level provider, levels 1 to 4. Its effects are linked to it.

### Vanguard: Strength, melee, physical

A front-line fighter that stuns and shields.

| Key | Ability | What it does | What it shows |
|---|---|---|---|
| Attack | Strike | Melee, physical, Strength-scaled. | An Attribute Backed scaler; a Shared scaler for the attack curve all heroes use. |
| Q | Shield Bash | Physical damage and a 1.5 s stun. | Click targeting in range; Check Range; a stun's State.Stunned tag, which blocks moving and casting. |
| W | Rallying Cry | Armor for itself and allies near it, for 6 s. | Auto targeting by affinity; a Bonus retention group; a visible buff; a Prefab cue while it lasts. |
| E | Iron Skin | 4 s of immunity to stuns, and a cleanse. | An effect's Blocks Effects Tagged and Removes Effects Tagged. |

### Ranger: Agility, ranged, physical

A ranged attacker that marks and kites.

| Key | Ability | What it does | What it shows |
|---|---|---|---|
| Attack | Arrow | A projectile, physical, with a small random spread to its damage. | Create Mono Process; a projectile; a Randomized scaler. |
| Q | Hunter's Mark | Marks a target for 8 s: it takes 20% more physical damage. | A Conditional scaler on every physical damage effect: more against Status.Marked. |
| W | Volley | Arrows at every enemy in an area; each has a 50% chance to slow. | Position targeting, then Auto Target at the point; Chance To Apply. |
| E | Tumble | A short dash, untargetable for 0.5 s. | A demo task that moves the hero; State.Untargetable, which the targeting tasks' requirements avoid. |

### Pyromancer: Intelligence, ranged, magic

A burst caster built on stacking burns.

| Key | Ability | What it does | What it shows |
|---|---|---|---|
| Attack | Ember | A projectile, magic. | A projectile's effects on arrival. |
| Q | Fireball | A projectile, magic damage, and a stack of Burning. | Burning: periodic ticks; Stack Existing, at most 5; an Effect Stacks scaler for damage per stack. At 5 stacks, an overflow effect, Flashpoint, bursts. |
| W | Flame Wall | Fire on the ground for 4 s that burns whoever stands in it. | Position targeting; Create Mono Process for the area, which applies Burning each second to those in it; a Prefab cue. |
| E | Combustion | Consumes the target's Burning for pure damage. | Removes Effects Tagged; a Conditional scaler by the weight of the target's Status.Burning tag; a stage condition. |

### Shade: Agility, melee, pure

An assassin that strikes from stealth.

| Key | Ability | What it does | What it shows |
|---|---|---|---|
| Attack | Cut | Melee, physical. Deals pure damage while Empowered. | A Conditional scaler and a damage type chosen by tag. |
| Q | Shadow Step | Blinks behind a target, and Empowers the next attack. | A demo task that moves the hero; Inject, to skip the second stage when the target is lost. |
| W | Eviscerate | Pure damage that grows with the target's missing health, to at most 400. Usable on a target below 50% health. | An Attribute Validation rule on the target; an Attribute Backed scaler of the target's health, in a Clamped scaler. |
| E | Smoke Veil | Invisible for 5 s, or until it deals damage. | State.Invisible, which the AI's targeting requirements avoid; the demo's own tag worker, which ends the veil on attacking. |

### Oracle: Intelligence, ranged, magic, support

A support caster that drains, silences and heals.

| Key | Ability | What it does | What it shows |
|---|---|---|---|
| Attack | Spark | A projectile, magic. | |
| Q | Drain Life | A 3 s channel: magic damage each second, and heals by half of it. | Channel; an ability's worker group with an impact worker for the drain's heal; the ability's Active Granted Tags. |
| W | Hex | Silences the target for 2 s, and lowers its Magic Resistance for 6 s. | State.Silenced, which blocks abilities with Ability.Spell; Additional Impacts; Contained effects. |
| E | Renewal | Heals over 5 s, and cleanses debuffs. | Asset loading: Set Effects loads Renewal's heal and cleanse, and a task sequence applies them, by level. |

## Items

Fourteen items. Each is a level provider. Stat items are children of the **Stat Item** template. An item's attribute
bonuses are effects it grants while equipped. Those effects use the Bonus retention group and are Not Visible, so
they show in the attributes' "(+8)", not as buffs.

| Item | Gives | What it shows |
|---|---|---|
| Iron Gauntlets | +8 Strength | A stat item from the template. |
| Duelist's Edge | +8 Agility, +10% Attack Amplification | Two granted effects. |
| Arcane Tome | +8 Intelligence, +10% Magic Amplification | |
| Warden Plate | +12 Armor | |
| Mystic Veil | +20% Magic Resistance | Magic Resistance's bounds, which cap it at 75%. |
| Swift Boots | +40 Movement Speed | |
| Hourglass | +15% Cooldown Reduction | Cooldown effects reading CDR. |
| Vampiric Fang | +15% Lifesteal | The lifesteal worker. |
| Ember Brand | Default attacks apply a stack of Burning. | The demo's own impact worker, on the item's worker group. |
| Blink Charm | Blink: teleport a short way. | An item's active ability. |
| Healing Salve | Salve: heal over 6 s. Stacks to 3, and each use takes one. | Max Stack Size; Remove Item by count. |
| Soul Shard | +3 Intelligence per shard. Stacks to 10. | An Item Stacks scaler. |
| Thunder Hammer | +6 Strength, and Thunderclap: a stun around the hero. | Stats and an ability on one item; a Camera Shake cue. |
| Sage's Crown | +Intelligence by tier: Bronze, Silver, Gold at levels 1, 2 and 3. | A Threshold scaler; an item's levels. |

## Tags

- **Damage:** Damage.Physical, Damage.Magic, Damage.Pure, Damage.Attack, Damage.Ability.
- **State:** State.Stunned (no moving, no abilities), State.Silenced (no spells), State.Invisible,
  State.Untargetable, State.Channeling, State.Empowered.
- **Status:** Status.Burning, Status.Marked, Status.Hexed, Status.Slowed.
- **Kinds:** Buff, Debuff, which a cleanse removes; Ability.Spell, which silence blocks; Ability.Attack.
- Stunned blocks every ability through its requirements, and Silenced blocks Ability.Spell. A projectile's targeting
  and the AI's skip State.Invisible and State.Untargetable.

## The AI hero

- It picks the player's hero, unless that hero is invisible or untargetable.
- It walks into its attack range, from its Local Data, and attacks.
- It casts an ability when it's ready, by a priority list in its Local Data: for example, Shield Bash when in range,
  and Iron Skin when stunned.
- It falls back when its health is low. A Threshold analysis worker writes the flag it reads.
- It casts with the same assets the player uses. An ability's targeting is a Composite: From Data first, which the AI
  fills with its target, then the click the player makes.

## Placeholder textures

A menu item, PlayForge → Demo → Make Placeholder Icons, draws each icon in code and saves it as a PNG under the
sample's Textures folder: a rounded square in the asset's color, its initials, and a shape for its kind (a circle for
abilities, a diamond for items, a square for attributes, a triangle for effects). The assets hold the textures, so
art can replace a PNG without touching an asset.

## Every feature, and where it shows

| Feature | Where |
|---|---|
| Workers | Attribute: Mitigation, Amplification. Impact: Lifesteal, Drain Life's heal, Ember Brand. Tag: Smoke Veil's end. Analysis: Death Watcher, the AI's low-health flag. Effect worker: Flashpoint at 5 Burning stacks. |
| Cues | Prefab cues for hits, auras and Flame Wall; Camera Shake for Thunderclap; damage numbers. |
| Textures | Every hero, ability, item, attribute and visible effect. |
| Level providers | Heroes (1–10), abilities (1–4), items (Sage's Crown 1–3). Effects linked to their ability or item. |
| Templates | Hero entity, Hero attribute set, the three Strike effects, Stat Item. |
| Scalers, basic | Simple tables, Constant, Attribute Backed, Clamped (Eviscerate), Conditional, Randomized, Group, Threshold, Effect Stacks, Item Stacks (Soul Shard), Set By Caller, Shared. |
| Scalers, cached | Cached Simple for primaries; Cached Attribute Backed for Health, Mana and Armor. |
| Set By Caller | The AI's difficulty: the AI sets it on each activation, and every damage effect multiplies by it, 1 where nothing set it. |
| Local Data | Attack range, the AI's priorities, each hero's projectile prefab. |
| Tag logic | Stun, silence, immunity, cleanse, stage conditions, Marked's bonus damage. |
| Ability tasks | Click targeting, Auto targeting, Composite, Channel, Delay, Inject, Create Mono Process, Run Sequence, Apply and Remove Effects. |
| Asset loading | Renewal's Set Effects, which its sequence reads. |
| Ability requirements | Cost, Cooldown, Is Alive, Check Range, Attribute Validation. |
| Retention groups | Items' bonuses, shown apart in the HUD. |
| Effect visibility | Item effects Not Visible; buffs and debuffs Always Visible. |
| Stacking | Burning's stack bounds and overflow; the Salve's and the Soul Shard's item stacks. |

## Gaps in PlayForge the demo needs closed

- **A spawned projectile's origin.** A projectile that Create Mono Process spawns takes GameRoot as its origin, since
  nothing sets Tags.DERIVATION, so its speed and its effects come from GameRoot, not the caster. Battle works around
  it in its own task. The demo needs the task to pass the caster as the origin: a GAS fix with its own tracker ID
  and test.
- **What a spawned thing knows, Open decision 20 (FEAT-24).** A projectile reads its speed from its origin's
  Projectile Speed attribute, which the spec's twelve attributes don't include. The demo plan assumes a projectile
  carries its own speed, set on its prefab and overridable from the ability's data. This needs the user's answer.
- **No shipped tag worker or useful effect worker.** The demo authors its own, as a game would, in its scripts.

## Building it

1. The two gaps above, each with its test.
2. Tags, attributes, the Hero and hero attribute sets, and the Strike templates.
3. The damage model's workers, with EditMode tests of each formula: armor, magic resistance, amplification,
   lifesteal and cooldown reduction.
4. The heroes and their abilities, one hero at a time, each with a PlayMode test that its abilities land.
5. The items.
6. The scene: bootstrap, controls, camera, the AI, the dummy, the panel and the HUD.
7. The placeholder icons.
8. Package it as Demo. Move the smoke suite's demo assets to it, so its coverage doesn't silently go. Then delete
   Demos, Swarm Defender, `Assets/demo_uss.uss` and the old PanelSettings, and fix the docs that name them.

Each step is a commit on main with its tests, as Phase 6's were.

## Questions for the user

1. **The roster:** do these five heroes, their abilities and the items fit what you want the demo to show?
2. **Projectile speed (Open decision 20):** should a projectile carry its own speed, as above?
3. **Levels in the scene:** the panel's level − and + let you watch scalers change with level. Keep them?
