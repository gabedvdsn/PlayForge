# Complex effects: what PlayForge could provide

Ten effects from Dota 2, authored as assets (2026-11-10, the user's request), with a note at each step of what made
them harder than they should be. The effects are in Proving Grounds, `Assets/ProvingGrounds/Dota/`, and its bot,
`Tests/Labs/DotaLabTests.cs`, plays each.

## The ten, and what each took

| Effect | Built from | Code it needed |
|---|---|---|
| **Maledict** (Witch Doctor) | A durational effect, 12 ticks of 10; a second periodic impact of 24% of what the Attribute Change worker answers, the health lost since it landed, every fourth tick | None |
| **Jingu Mastery** (Monkey King) | A counter on the target, stack bound 3, whose overflow lands a trigger; the trigger gives Monkey King 4 charges, an attribute; his attack adds 80 while charged (a Conditional) and adds the counter only while uncharged (the counter's chance to apply, a Conditional) | Lands an effect on the caster (twice: the charges, and spending one); lifesteal while charged |
| **Coup de Grace** (Phantom Assassin) | Her attack carries the crit as a contained effect: 225% more of her Attack Damage, with a 15% chance to apply | None |
| **Ice Blast** (Ancient Apparition) | A durational effect of 10 ticks of 20, holding the blast, 325, as an Instant effect it lands with (an OnApply impact on it doesn't land: GAS-71) | Gives its target two attribute workers while it lasts: no healing, and a shatter below 13% of maximum health |
| **Attribute Shift** (Morphling) | Two infinite effects, four ticks a second, each moving a point to the base and current of one attribute from the other while that one is above 1 (a Conditional on each impact); each removes the other as it lands | None |
| **Fury Swipes** (Ursa) | A stacking debuff that refreshes; Ursa's attack reads its stacks on the target with Asset Part, `20 × stacks`, in a Group with her Attack Damage | None |
| **Essence Shift** (Slark) | A debuff of −1 to three attributes and a buff of +1 Agility, each stack with a duration of its own; each change reads Effect Stacks, as a lasting change counts once otherwise | Lands the buff on the caster |
| **Mana Void** (Anti-Mage) | `0.8 × (Target's Mana (base) − Target's Mana)`: a Group of base mana and a Group of current mana × −1 | None |
| **Shallow Grave** (Dazzle) | A durational effect | Gives its target a worker that keeps health from falling below 1 |
| **Blade Mail** (item) | A durational effect | Gives its wearer a worker that returns 85% of what it takes to whoever dealt it |

Five of the ten needed no code. The other five needed seven small workers, each a general rule, all in
`Assets/ProvingGrounds/Dota/Scripts/`.

## Found on the way (to fix in the engine)

- **A durational effect's OnApply impact never lands** unless Tick On Application is on: `ApplyTickOnApplication`
  is the only durational path that commits OnApply entries (`EffectSystemComponent`), so Ice Blast's 325 on landing
  was dropped and only its ticks dealt. A bug, near GAS-50: GAS-71, open. The lab's Ice Blast lands its blast as a
  contained Instant effect instead.
- **A stacking effect's continuous change counts once**, whatever its stacks, unless its magnitude reads Effect
  Stacks, where its periodic ticks deal once a stack. Essence Shift's three stacks took 1 Strength until each change
  read Effect Stacks. The same rule for both, or a per-stack switch on the impact, would save the surprise.

## The list

Ranked by how many of the ten each would have made simpler.

1. **An effect that lands on the caster.** Contained effects, overflow effects and packets land on the target alone,
   so anything an effect gives its caster, as a charge, a stolen stat or a heal, takes a worker. A choice on each,
   Target or Caster, would have made Jingu Mastery and Essence Shift code-free. *(`ApplyToSourceEffectWorker`)*
2. **Rules that come with an effect.** An effect can grant tags and abilities but not workers, so a debuff whose rules
   change how its target takes damage or healing (Ice Blast, Shallow Grave, Blade Mail) has to give its target
   attribute workers as it lands and take them back as it goes. A *While Active: Attribute Workers* list on the effect
   would carry the rules with it. *(`GrantAttributeWorkersEffectWorker`)*
3. **The common rules, built in.** Each of these is a few lines, and every game writes them:
   - **Heal block**, or better a *healing received* multiplier, which 0 blocks (Ice Blast);
   - **Execute below a share of maximum** (Ice Blast's shatter, Culling Blade);
   - **Can't fall below a value** (Shallow Grave, and any "can't die" or "can't be reduced below");
   - **Damage return** to the dealer, with a flag so returned damage isn't returned again (Blade Mail);
   - **Lifesteal on an effect**, gated by a condition. `RelativeOperationContextImpactWorker` does lifesteal from an
     attribute, but it is an entity's worker with thirteen fields to set.
4. **Charges, or stacks that go down.** A stacking container's stacks only go up (`Stack` ignores an amount of 0 or
   less), so a buff of four charged hits can't be four stacks that each attack spends. Jingu Mastery keeps its charges
   in an attribute. A *Spend a Stack* impact, or an effect that removes N stacks of another, would do it.
5. **On-attack, and modifying outgoing effects.** A passive that changes a hero's attacks (Jingu Mastery, Coup de
   Grace, Fury Swipes, Essence Shift) has to be built into the hero's attack effect, as PlayForge has no "on attack"
   moment a passive can listen to, nor a way for one effect to add to another of a kind as it's made. An effect
   modifier, *for every outgoing effect tagged Attack: add this packet, multiply this impact*, would let each passive
   be its own asset that any hero could learn.
6. **Missing, and share of base.** × Attribute reads current or base. *Missing* (base − current) and *Share* (current ÷
   base) would make Mana Void one number, where it took two nested Groups and a −1, and would make a threshold such as
   "below 13% of maximum health" a number of its own.
7. **Moving an attribute into another.** Attribute Shift and Essence Shift take a point from one attribute and give it
   to another, as two impacts, each carrying the same condition. A *Transfer* impact (from, to, amount, floor) would be
   one entry and could never drift.
8. **Conditions in requirements.** Effect requirements take tags only. A `GameplayCondition` there (FEAT-30) would stop
   Attribute Shift at its floor once, where each of its impacts now carries the same Conditional, and would let an
   effect end itself as a condition stops holding.
9. **Toggles.** Attribute Shift's two directions are two effects, each removing the other as it lands; that works.
   An ability that toggles an effect on and off, with its cost and cooldown on turning on, isn't there.
10. **Pseudo-random chance.** Coup de Grace, Bash and most of Dota's chances use a pseudo-random distribution, which
    evens out streaks. `ChanceToApply` is a plain roll; a *pseudo-random* option, kept for each caster, would match.
    `ForgeRandom.Use` made the test exact, which is good.
11. **Stacks for each source.** Fury Swipes and Jingu Mastery's counter count one attacker's hits; two Ursas on one
    target share one Fury Swipes in PlayForge. A *stack by origin* choice would keep each attacker's apart.
12. **One line for an effect.** A number says itself in a line ("−1 × (Caster's Attack Damage + 80 if Caster's Jingu
    Charges > 0, else 0)"). An effect could too, in its inspector's header and in the Debugger: "Every second for 12
    s: 10 damage; every 4th, 24% of health lost". Authoring ten effects, the asset's own summary was the thing most
    missed.
13. **A combat log.** The tests read health before and after. A log of impacts for each entity, by effect and by
    dealer, would make an effect's numbers checkable at a glance, in a test and in the Debugger.
14. **Recipes and a worker map.** A docs page of recipes (a curse that bursts, a counter that triggers, a crit, an
    execute, lifesteal, damage return, can't die) and a map of which worker hook runs where:
    `OnEffectApplication` on the target's side, `OnEffectImpact` on the caster's, with the impact dealt; an attribute
    worker's `Intercept` before or after the change, and what each may change. Finding that took reading the engine.
15. **Unworkable, documented.** `SourcedModifiedAttributeValue.GenerateUnworkable` marks a change no worker reads,
    which is what keeps two Blade Mails from returning damage forever. It is undocumented, and it also takes the
    returned damage past the dealer's own rules, as a Shallow Grave. A narrower *returned* or *reflected* impact type
    would do the first without the second.
16. **An area from one target.** Maledict and Mana Void hit an area in Dota, Mana Void by its main target's missing
    mana. Targeting tasks find an area, but an effect can't land on an area around its target, nor read its main
    target's values for the others.
