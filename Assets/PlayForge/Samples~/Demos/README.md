# PlayForge Demos

Two scenes built on PlayForge, with the abilities, effects, attributes and entities they share. They import together,
since each uses the other's assets.

## What you need

- The **Input System** package, with *Project Settings → Player → Active Input Handling* set to **Input System Package**
  or **Both**. Without it, this sample's scripts don't compile and its scenes do nothing.
- The **Built-in Render Pipeline**. The materials use the Standard shader, which renders magenta under URP or HDRP.
- UniTask 2.5.11, which PlayForge itself needs.

## The scenes

Open a scene and press Play.

| Scene | What it shows |
| --- | --- |
| `Demo/Scenes/DemoScene.unity` | A showcase run from its own UI. Pick a scenario or a task sequence from the panels to watch abilities such as Fireball, Ice Lance, Chain Lightning and Meteor Strike play out. Targeting uses the mouse, and Escape cancels it. |
| `Swarm Defender/Sample_SwarmDefender.unity` | A small survival game whose whole loop runs as task sequences. Enemies swarm the hero, and each level-up offers a choice of abilities and buffs. The manager only builds the UI and passes your choices to the sequences. |

## The folders

| Folder | What's in it |
| --- | --- |
| `Data` | The assets both scenes share: Swarm Defender's abilities, Meteor Blast, and the attributes, attribute sets, effects and entities they use |
| `Demo` | The showcase: its scene, scripts, materials, prefabs and its own abilities, attributes, effects and entities |
| `Swarm Defender` | The game: its scene, scripts, materials and prefabs |
| `UI Toolkit` | The panel settings and style sheets for the scenes' UI |

The scripts compile into their own assembly, `FarEmerald.PlayForge.Samples`.
