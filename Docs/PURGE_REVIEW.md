# Purging effects: research and a shape (FN-16)

FN-16 asks how MOBAs purge effects, then for a shape to decide. This page holds what the research found and the
choices the shape needs. Researched 2026-11-13. Facts marked † came through a search index, as the wiki refused a
direct read.

## What the games do

| Game | Removal strengths | How an effect says what removes it | Beyond any purge | Death |
|---|---|---|---|---|
| Dota 2 | Basic < Strong < Death | Yes, Strong Dispels Only, or No, as flags on each modifier | Death-only modifiers, Undispellable ones, auras | The top strength |
| League of Legends | Cleanse and Quicksilver remove overlapping sets, not tiers | Its crowd-control type | Stasis, airborne movement, crowd control from oneself or an ally | Removes what isn't marked to persist |
| Heroes of the Storm | Unstoppable removes and blocks a curated set | Its crowd-control type | Blinds, until 2019 | Not researched |
| SMITE 2 | Purification Beads: 2 s immune to crowd control | Its crowd-control type | Not documented | Not researched |

- **Tiers.** Only Dota ranks its removals, Basic < Strong < Death < Undispellable, kept as flags, not a number.
  Abilities declare `SpellDispellableType`, Yes / Yes Strong / No, and modifiers answer `IsPurgable` and
  `IsPurgeException` (strong only) ([game files](https://github.com/dotabuff/d2vpkr/blob/master/dota/scripts/npc/heroes/npc_dota_hero_lion.txt),
  [Valve's script help](https://github.com/ModDota/API/blob/master/dump/script_help2.lua)). No game has a numeric
  purge level. LoL's Cleanse and Quicksilver each remove something the other can't
  ([Cleanse](https://wiki.leagueoflegends.com/en-us/Cleanse), [Quicksilver Sash](https://wiki.leagueoflegends.com/en-us/Quicksilver_Sash)).
- **Buffs and debuffs.** Each effect carries a polarity, and each purge says which it strips: Dota's
  `Purge(RemovePositiveBuffs, RemoveDebuffs, ...)`. Dota and WoW go by the purger's relation to the one purged, a
  friendly purge stripping debuffs and a hostile one buffs † ([Liquipedia: Dispel](https://liquipedia.net/dota2/Dispel));
  LoL leaves crowd control from oneself or an ally alone ([Types of Crowd Control](https://wiki.leagueoflegends.com/en-us/Types_of_Crowd_Control)).
- **Never purged.** Both have effects only death removes (Dota's death-only modifiers, LoL's Stasis), and Dota has
  some that outlast death † ([Fandom: Respawn](https://dota2.fandom.com/wiki/Respawn)).
- **Death.** It removes every effect not marked to persist, with named exceptions: objective buffs lost, some passed
  to the killer, stacks partly kept, cooldowns running on
  ([LoL: Death](https://wiki.leagueoflegends.com/en-us/Death)). Dota's modifiers opt out with `RemoveOnDeath`; the
  Unreal GAS sample's `Die()` opts in, removing effects tagged `Effect.RemoveOnDeath`
  ([GDCharacterBase.cpp](https://github.com/tranek/GASDocumentation/blob/master/Source/GASDocumentation/Private/Characters/GDCharacterBase.cpp)).
- **What the system removes.** Scripted removal skips the purge rules, as Dota's `RemoveModifierByName` takes only a
  name, and a round reset clears everything, as LoL's Arena does
  ([Arena](https://wiki.leagueoflegends.com/en-us/Arena)).
- **A dead entity's gains and losses.** No game says it zeroes them. Death strips the effects, a dead source's damage
  over time goes on, and the GAS sample takes damage on the dead with Health held at 0, checking only its hit
  reactions, numbers and bounty against whether it was alive
  ([GDAttributeSetBase.cpp](https://github.com/tranek/GASDocumentation/blob/master/Source/GASDocumentation/Private/Characters/Abilities/AttributeSets/GDAttributeSetBase.cpp)).

## PlayForge today

An effect cleanses by tags (**Removes Effects Tagged**) and grants immunity by tags (**Blocks Effects Tagged**). A death
that keeps its entity ends **Ends Effects Tagged**, which can need every effect tag of a project to clear what was
applied. Effects land on a dead entity as on a live one.

## The shape to decide

1. **How an effect says what purges it.** Dota's tiers as a field on the effect, **Purged By**: Basic, Strong, Death,
   Never; a purge of a strength takes those at or below it, so a Strong purge takes Basic and Strong, death takes all
   but Never. Or a number, a purge level the effect needs at most; or tags alone, as now.
2. **Which a purge takes.** Each effect a **Kind**, Buff or Debuff (or neither, as a cooldown), and each purge its
   kinds; or Dota's rule, by the purger's relation to the one purged, a friend's purge taking debuffs and a foe's
   buffs.
3. **What a death keeps.** Death as the top purge, taking everything but Never, and keeping what the entity's own
   abilities and items lend it, its cooldowns, their passive effects and auras, which their sources put back anyway;
   or only what's marked Never. Either way it takes the place of Ends Effects Tagged.
4. **A dead entity's gains and losses.** Leave them, as the games do; refuse new effects on the dead, but a revive's;
   or zero them by a pipeline-wide stage, as FN-16 first asked.

A purge itself would be a field of an effect, **Purges**, a strength and the kinds it takes, beside its Removes
Effects Tagged, and `ForgePurge.Purge(target, strength, kinds)` for code.
