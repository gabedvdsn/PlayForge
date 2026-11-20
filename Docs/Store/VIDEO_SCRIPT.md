# The trailer: voice-over script

A 1:31 trailer for the Asset Store page, filmed in Unity frame by frame at 1920 × 1080 and 30 fps, with no sound. The
voice-over below is timed to its cuts. Every line fits its shot at about 125 words a minute, with a breath before the
next cut. Each claim comes from the package's `CHANGELOG.md`, the checklist or the store shots' captions.

## Recording it

- Read at an easy pace, about two words a second. A line may run up to its next time, never past it.
- Start each line just after its cut, not on it.
- Record in one take if you can, to keep the voice even, then cut it to the times below.
- Lay the voice-over at 0:00 in any editor (Clipchamp comes with Windows 11), add music low under it if you like, and
  export at 1080p.
- The Asset Store takes a video as a YouTube or Vimeo link: upload it there and paste the link in the listing.

## The script

| Time | On screen | Voice-over |
|---|---|---|
| 0:00 – 0:04 | The title card: PlayForge, a gameplay ability system for Unity 6 | PlayForge: a gameplay ability system for Unity 6. |
| 0:04 – 0:20 | Survivors: hundreds of enemies, abilities casting themselves, the hero dashing | Abilities, effects, attributes, tags and items are assets you build in the inspector, and PlayForge runs them, hundreds at once. Here, the Survivors sample: abilities that cast themselves, passives, and a dash through the horde. |
| 0:20 – 0:34 | The Demo: the Pyromancer against an AI Vanguard | The Demo is a complete arena: five heroes, fourteen items and an AI opponent. Every fireball, slow and stun is an asset you can open, read and change. |
| 0:34 – 0:39 | The Forge on the Demo's project | The Forge holds every asset in your project, in one window. |
| 0:39 – 0:44 | Fireball's stages in the inspector | Abilities are staged tasks, built in the inspector. |
| 0:44 – 0:49 | Burning, its duration and stacking | Effects stack, tick and play their cues, with no code. |
| 0:49 – 0:54 | The Physical Damage value pipeline | Damage runs through pipelines you can try in the editor. |
| 0:54 – 0:59 | The Debugger's Entities view in play | The Debugger shows every entity, live: attributes, effects and tags. |
| 0:59 – 1:03 | The Debugger's Timeline | Its Timeline shows what each ability and effect did. |
| 1:03 – 1:07 | The Problems view | Problems catches mistakes before you press Play. |
| 1:07 – 1:11 | The Creator Lab | The Creator Lab tries it all on units and bots. |
| 1:11 – 1:16 | Diagram: how PlayForge fits together | Under the hood: one hub per entity, and every ability a staged workflow. |
| 1:16 – 1:20.5 | Diagram: an ability, from cast to end | Every step of a cast is data you set. |
| 1:20.5 – 1:25 | Diagram: an effect, as it lands | And every effect, from its gates to its numbers. |
| 1:25 – 1:31 | The end card: full C# source, Unity 6000.0 or later, six samples, the docs' address | PlayForge. Full C# source, six samples, and guides online. For Unity 6. |

About 190 words in all.

## Remaking it

The film comes from a scratch capture in a private copy of Proving Grounds, which isn't committed. The capture plays
Survivors and the Demo with Unity's game time stepped a frame at a time, lays each HUD over its camera, and encodes
with Unity's own `MediaEncoder`. Its stills are the gallery's images in `gallery/`, so a new screenshot there is a new
shot in the film.
