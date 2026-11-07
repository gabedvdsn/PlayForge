# PlayForge Demo

A small hero arena, in the style of a MOBA. Pick a hero and an opponent, give items, change levels, and fight. Every
hero, ability, item, effect and attribute is a ScriptableObject asset, so you can open any of them in the inspector,
change it, and see the change in play.

## Running it

1. Import the sample from the Package Manager (PlayForge → Samples → Demo).
2. Open `Demo.unity` and press Play.

It needs nothing beyond PlayForge. It reads input through the Input System or the Input Manager, whichever the project
has. It renders in the Built-in pipeline and in URP: every shape is made as the scene plays, with the pipeline's own
default material.

## Controls

- **Right-click** the ground to move. Right-click an enemy to attack it: the hero walks into range, then attacks.
- **Q, W, E** cast the hero's abilities. An ability that needs a target waits for a **left-click** on an enemy, or on
  the ground.
- **1 to 6** use the items' abilities.
- **S** stops. **Esc** cancels a cast that waits for its click.

## The panel and the HUD

- **You:** your hero, its level with − and +, each ability's level, and your items, each with its level and Remove.
- **Opponent:** an AI hero or the training dummy. For an AI hero: its hero, levels and difficulty. For the dummy: the
  damage it takes each second and its last blow.
- **Items:** each item, with a button to give it to you or to your opponent.
- **Fight:** Reset puts both back where they started, at full health.
- **The HUD**, at the bottom: health and mana, the ability bar with cooldowns, your attributes with the part items add
  shown apart, as "STR 32 (+8)", and the effects on you that are meant to be seen.

## The heroes

| Hero | Attack | Q | W | E |
|---|---|---|---|---|
| Vanguard | Strike | Shield Bash | Rallying Cry | Iron Skin |
| Ranger | Arrow | Hunter's Mark | Volley | Tumble |
| Pyromancer | Ember | Fireball | Flame Wall | Combustion |
| Shade | Cut | Shadow Step | Eviscerate | Smoke Veil |
| Oracle | Spark | Drain Life | Hex | Renewal |

Every hero also has Second Wind, a passive that gives it Regeneration as it comes in.

## Where each feature shows

| Feature | Where to look |
|---|---|
| Attribute sets and templates | `Attribute Sets/Hero/Hero` is the template each hero's set takes; its sub-sets hold the vitals, primaries, offense, defense and utility. Health, Mana and Armor follow Strength, Intelligence and Agility through cached scalers. |
| The damage model | The Hero set's workers: Mitigation (Armor, Magic Resistance), Amplification, and Lifesteal, PlayForge's own impact worker. `Scripts/Forge/DemoWorkers.cs`. |
| Effect templates | `Effects/Templates`: Physical, Magic and Pure Strike, which every blow takes, and Stat Bonus, which every item's bonus takes. |
| Scalers | Constant, Simple, Attribute Backed (every blow), Clamped (Eviscerate), Conditional (Hunter's Mark's bonus on every physical blow), Randomized (Arrow), Group (every cooldown), Threshold (Sage's Crown), Effect Stacks (Burning), Item Stacks (Soul Shard), Set By Caller (the AI's difficulty), Shared (`Scalers/Attack Growth`). |
| Stacking | Burning stacks to five; a sixth bursts as Flashpoint, its overflow effect. |
| Cleanse and immunity | Iron Skin blocks and removes stuns; Renewal removes every debuff. |
| Tag logic | Stuns and silences block abilities by their requirements; Smoke Veil ends as the Shade attacks; Combustion's second stage runs only on a burning target; Cut picks its damage type by a stage condition. |
| Targeting | Click targeting that an AI hands its target in data; Check Range (Shield Bash); Auto Target by Affinity (Thunderclap). |
| Processes with their own attributes | The projectiles carry their speed; Flame Wall's fire carries its radius, lifetime and pace, its radius by the ability's level. |
| Task sequences and asset loading | Volley and Renewal run sequences in `Scripts/Forge/DemoSequences.cs`; Renewal loads its heal and cleanse with Set Effects. |
| Workers of your own | Ember Brand's impact worker, Drain Life's heal, the Shade's tag worker, Eviscerate's effect worker. |
| Cues | Prefab cues for hits, auras and bursts; a camera shake for Thunderclap and Flashpoint; floating words, the demo's own cue. |
| Items | Stat items from a template, granted effects in the Bonus retention group, active abilities, stacks (Healing Salve, Soul Shard) and levels. |

## Placeholder icons

The icons are drawn in code. **Window → PlayForge → Demo → Make Placeholder Icons** draws them again, into `Textures`. To use
your own art, replace a PNG there: the assets point at the texture, so nothing else changes.

## Tests

`Scripts/Tests` plays the scene: every hero's abilities cast, the damage formulas hold, items and levels change what
they should, Burning bursts at six, and the AI fights back. Run them from the Test Runner's PlayMode tab.
