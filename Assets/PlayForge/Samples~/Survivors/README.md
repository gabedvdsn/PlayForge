# PlayForge Survivors

A survival shooter in the style of the horde games, made to show what PlayForge carries. The hero walks, and every
ability it holds casts itself as its cooldown ends. The waves send more enemies and harder ones each time, until
hundreds stand at once, each a full entity on PlayForge's GAS: its own attributes, effects, abilities and tags.

Every ability, passive, enemy, effect and attribute is an asset, so you can open any of them in the inspector, change
it, and see the change in play. The assets are a Forge Project of their own, **Survivors Project**: choose it in The
Forge's project menu to see them there.

## Running it

1. Import the sample from the Package Manager (PlayForge → Samples → Survivors).
2. Open `Survivors.unity` and press Play.

It needs nothing beyond PlayForge. It reads input through PlayForge's `ForgeInput`, the Input System or the Input
Manager, whichever the project has. Its hero and its enemies are the Creator Lab's units, which the roster gives its
rules. It renders in the Built-in pipeline and in URP: every shape is made as the scene plays, with the pipeline's own
default material.

## Controls

- **WASD** or the **arrow keys** walk the hero. Its abilities cast themselves.
- **1 to 5**, or a click, take a card while the cards are up. Time stands until you take one.
- **R** starts a new run once the hero falls.

## How a run goes

- **Waves.** A wave lasts 15 seconds and sends its enemies all through it. Each wave sends more than the one before,
  and new kinds join as the waves go on. An enemy's level rises every two waves, and its Health and Power with it.
- **Boss waves.** Every second wave is a boss wave. It also sends a pack of hard enemies at once: a Brute Pack, the Ogre
  Warlord, the Lich or the Hive Queen, in turn. Felling them all offers five passive cards.
- **Experience.** Each enemy drops a crystal where it falls. The hero draws crystals in from its Pickup Radius, and each
  level takes more experience than the last.
- **Level-ups.** Each level offers three ability cards: a new ability while the hero holds fewer than five, or a level
  more of one it holds. An ability goes to level 8.
- **Passives.** The hero holds up to five, each to level 20, raising an attribute every level.

## The HUD

- **Across the top:** the experience toward the next level, and the level.
- **Left:** the wave and whether it's a boss wave, the seconds to the next one, the time survived and the kills.
- **Right:** what the run costs: the frame rate and frame time, how many enemies stand, how many processes
  ProcessControl runs, and how many crystals lie.
- **At the bottom:** the hero's Health, its abilities with their levels and cooldowns, and its passives with their
  levels.

## The abilities

| Ability | What it does | What it shows |
|---|---|---|
| Arcane Bolt | Homing bolts at the nearest enemies. | Auto Target, a Run Sequence task, a projectile process. |
| Cleave | Sweeping arcs around the hero. | A mono process of its own, `SlashArc`, aimed by the sequence. |
| Holy Aura | A pulse that burns every enemy near. | The Area Effect task, a radius that reads the caster's Area. |
| Orbiting Blades | Blades that circle the hero a while. | `OrbitingBlade`, phased around the hero by the sequence. |
| Lightning Strike | Bolts from the sky on random enemies. | Auto Target at random, a pooled Prefab cue. |
| Fire Field | Pools of fire under enemies. | `FirePool`, burning what stands in it each half second. |
| Hurled Axe | Axes thrown high in a fan, piercing several. | A projectile on an arc, its pierce raised by the caster's. |
| Frost Nova | A ring of frost that freezes what it hits. | The Create Mono Process task, the roster's stop tag. |

Every throw, drop and orbit runs one task sequence, **Survivors: Volley**, in `SurvivorsSequences.cs`. Each ability's
Local Data says what it spawns, how many by level, and in which pattern, so a designer changes a volley in the ability.

## The passives

Might, Haste, Swiftness, Evasion, Multishot, Piercing, Precision, Ferocity, Vitality, Regeneration, Expanse, Magnet,
Armor Plating and Persistence. Each is an item whose effect is linked to it, so the effect grows with the item's level.
Taking a passive's next level gives it again at that level.

## The horde

| Enemy | Comes from wave | What it does |
|---|---|---|
| Shambler | 1 | Walks at the hero and claws it. |
| Bat | 2 | Comes in flocks of five, fast and frail. |
| Archer | 3 | Shoots arrows from afar. |
| Brute | 4 | Big, armored and hard-hitting. |
| Bomber | 5 | Bursts beside the hero, and falls with the blast. |
| Shaman | 6 | Casts homing fire bolts, and mends the horde around it. |
| Charger | 7 | Braces, rushes the hero and stomps where it stops. |
| Splitter | 8 | Breaks into three Splitlings as it falls. |
| Wraith | 9 | Phases out a while: the hero's blows and targeting pass it by. |
| Guardian | 10 | Gives the horde around it +20 Armor, through an Aura entity component. |

The bosses: the **Ogre Warlord** slams the ground where its red ring shows. The **Lich** shoots death bolts, looses
rings of them and raises Shamblers. The **Hive Queen** spits fans of acid and broods swarms of Bats. Both summons run
the **Survivors: Summon** sequence.

## The damage model

Two Value Pipelines, in `Effects/Pipelines`:

- **Survivor Damage**, for every blow of the hero's: × (1 + Damage Amplification), then a chance stage on the hero's
  Crit Chance, times its Crit Damage, then the target's Armor, 100 ÷ (100 + Armor) of the blow landing.
- **Horde Damage**, for every blow of the horde's: a chance stage on the hero's Dodge Chance that lets none of it
  through, then the hero's Armor.

An enemy's blow is its **Power**, which grows with its level, times what its effect says.

## Where things are

| Folder | What it holds |
|---|---|
| `Abilities/Hero`, `Abilities/Horde` | The abilities, their effects in `Effects` and their cooldowns in `Usage`. |
| `Attributes`, `Attribute Sets` | The attributes, the hero's set and each enemy's. |
| `Effects` | The pipelines, and the effects several share: Frozen, Phased, Guarded, and the bosses' tells. |
| `Entities` | The hero, every enemy and the bosses. |
| `Items` | The passives, their effects in `Effects`. |
| `Prefabs` | The projectiles, the processes and the cues' looks. |
| `Scripts/Forge` | The processes, the sequences and the sample's tags. |
| `Scripts/Game` | The game, the horde, the hero's controls, the crystals, the cards, the roster and the camera. |
| `Scripts/UI`, `UI` | The HUD, its style sheet and its panel settings. |
| `Survivors Roster` | The game's rules: the hero and its pools, the waves, the enemies, the boss waves, and the attributes and tags it reads. |
