# Asset Store images

Screenshots for PlayForge's Asset Store page. Each is 1920 × 1080, the store's screenshot size, but `cover-base.png`, which
is 1950 × 1300, the cover's.

They come from Proving Grounds' **Window → PlayForge → Docs → Capture Screenshots**, which writes them to its `store/`
folder beside the docs' own. Keep the editor in front for the whole run: play mode stops in the background.

| File | Shows | Caption from the shot list |
|---|---|---|
| `demo.png` | The Demo mid-fight: the Vanguard against an AI Ranger, damage numbers, the HUD | A complete arena demo: 5 heroes, 14 items, an AI opponent |
| `forge.png` | The Forge on the Demo Project: every asset, and Fireball's summary and inspector | Every ability, effect and attribute is an asset, browsed and validated in The Forge |
| `ability.png` | Fireball in The Forge: its summary, its sections closed to their summaries, its Stages open | Abilities as staged tasks, built in the inspector |
| `effect.png` | Burning in The Forge: its summary, and its Duration open, with its ticks and stacking | Stacking, ticks, overflow and cues, with no code |
| `pipeline.png` | The Physical Damage value pipeline: its stages, and its Try | Damage pipelines and level scaling as data |
| `debugger.png` | The Debugger's Entities view in play: the Ranger's attributes, effects, abilities and tags | Every entity's attributes, effects, abilities and tags, live |
| `timeline.png` | The Debugger's Timeline: each track's runs, and an effect's applications and journal | See what every ability and effect did, over time |
| `problems.png` | The Problems view, by rule: a broken effect's two warnings, each with its quick fix | Validation catches mistakes before you press Play |
| `lab.png` | The Creator Lab: three of the Demo's heroes a side in battle, a unit selected | Try your heroes and abilities in the Creator Lab |
| `cover-base.png` | The Demo in play | The cover's base, to lay the wordmark and the pillars over |

## The page's set, in order (EP-81)

`gallery/` is the set to upload, in this order. Each image is 1920 × 1080 with its caption on it. The first two are
frames of the trailer, which casts abilities as the capture above can't; the rest are the shots above under a title
band, and three diagrams.

| # | File | Shows |
|---|---|---|
| 1 | `01-survivors.png` | Survivors: 281 enemies at once, frozen, burning and slammed |
| 2 | `02-demo.png` | The Demo: the Pyromancer stunned by a Shield Bash, its Flame Wall burning |
| 3 | `03-forge.png` | The Forge |
| 4 | `04-ability.png` | Fireball's stages |
| 5 | `05-effect.png` | Burning, its duration and stacking |
| 6 | `06-pipeline.png` | The Physical Damage value pipeline |
| 7 | `07-debugger.png` | The Debugger's Entities view |
| 8 | `08-timeline.png` | The Debugger's Timeline, cropped to its rows |
| 9 | `09-problems.png` | The Problems view, cropped to its rows |
| 10 | `10-lab.png` | The Creator Lab |
| 11 | `11-architecture.png` | Diagram: how PlayForge fits together |
| 12 | `12-ability-flow.png` | Diagram: an ability, from cast to end |
| 13 | `13-effect-flow.png` | Diagram: an effect, as it lands |

The diagrams' sources are in `diagrams/`. They're Mermaid, drawn with the docs site's own Mermaid in headless Edge, and
simplified from the docs' diagrams of the same flows. Each runs in rows or columns, never one long line, so it stays
legible at the store's small size.

## The trailer

A 1:31 film of Survivors, the Demo, the tools and the diagrams, with no sound, for a voice-over. `VIDEO_SCRIPT.md` is
its script, timed to its cuts. The film itself, `video/PlayForge-trailer.mp4`, stays out of git; upload it to YouTube or
Vimeo for the listing's video.

## Still to make by hand

- **The key images.** The icon (160 × 160), the card (420 × 280) and the cover (1950 × 1300), each with the wordmark.
  Unity's own logo stays out. `01-survivors.png`'s frame may make a livelier cover base than `cover-base.png`.
- **The voice-over**, to `VIDEO_SCRIPT.md`.
