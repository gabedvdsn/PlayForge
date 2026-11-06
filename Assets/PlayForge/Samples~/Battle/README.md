# PlayForge Battle

One scene: Blue and Red, eight fighters a side, fight until one side stands, and then a new battle begins. Each fighter
thinks for itself: it takes the nearest foe, closes to its range and casts what's ready. Open `Battle.unity` and press
Play.

## What you need

- UniTask 2.5.11, which PlayForge itself needs. Nothing else: the battle plays itself, and it brings PlayForge up when
  the scene has no ProcessControl.
- The Built-in Render Pipeline or URP. The materials take the active pipeline's own default shader.

## What it shows

It's built from PlayForge's own parts, made in code rather than saved as assets, so each part is in one place to read.

| Part | Where |
| --- | --- |
| Attributes in a set: health, mana, armor, move speed and shot speed, with a death watcher on health | `Battle.SetFor` |
| Armor, as an attribute worker that takes its share of each physical or magical blow | `ResistWorker` |
| Abilities with mana costs and cooldowns, which a stun keeps from being cast | `Battle.Kit` |
| Effects that burn, poison, chill, fortify and regenerate, which stack or refresh as they land again | `Battle.Ticking`, `Battle.Lasting`, `Battle.Status` |
| Knockback, as a gameplay cue on a blow | `KnockbackCue` |
| Arrows, fireballs and bolts as the framework's projectile process, drawn from a pool | `BattleShot`, `ShotPool` |
| Blows over an area: a fireball's burst and a frost nova | `Battle.Burst`, `Battle.NovaAround` |
| Summoned imps, each an entity with a lifetime | `Battle.Call` |
| Each fighter's mind | `BattleFighter.Think` |
| Health bars and effect boxes, read from each fighter's attributes and effects as they're drawn | `BattleView` |

## The files

| File | What's in it |
| --- | --- |
| `Battle.unity` | The scene: a camera, a light, and the Battle object that plays the battles |
| `Scripts/BattleSample.cs` | Plays battles one after another, and brings PlayForge up when the scene has none. Its Seed makes each battle the same |
| `Scripts/Battle.cs` | The battle: its sides, its fighters' kits and minds, its shots and its tasks |
| `Scripts/BattleKit.cs` | The assets the battle builds in code, and where its entities and shots live |
| `Scripts/BattleView.cs` | What it draws over the fighters, and the rings where things happen |
