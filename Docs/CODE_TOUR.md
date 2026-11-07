# PlayForge refactor: code tour

This tour lists the places where the refactor changed code, so you can open each one in your IDE and read it. It
covers `01258b8..e91fe34` on `main`: 387 commits. Phase 0 ported Arcade Fighter's fixes, Phase 1 made the runtime
correct, Phase 2 got the package ready to release, Phase 3 made it cheaper to run and gave the two GAS hubs one core,
Phase 4 rebuilt the editor, and Phase 5 built the missing features. Each entry says what behaves differently and why, names its tracker IDs and Decisions, and
points at the two or three places to read. `Docs/REFACTOR_PLAN.md` has every tracker row, the numbered Decisions,
and a Log of each batch's test results.

## How to use this

- Paths are relative to the repo root.
- Line numbers are as of commit `e91fe34`, the head of `main` on 2026-11-05. A later commit may move them, so each
  entry also names the class and method to search for. `git show e91fe34:<path>` shows a file as it was then.
- In Rider, Go to File accepts `path:line`. In Visual Studio, open the file, then Go To Line (Ctrl+G).
  A bare `:123` after a path means another line in the same file.
- `git grep "<ID>"` finds the comment at each fix, and `git log --grep "<ID>"` finds its commit. Three exceptions:
  - Phase 0's ports cite Arcade Fighter's log ID (`PF-005` to `PF-013`) in their comments, not the tracker ID. The
    plan's Phase 0 checklist maps one to the other.
  - These fixes have no ID comment at the fix: PC-6, PC-10, PC-11, PC-14, PC-16, PC-18, PC-19, PC-21, TS-1, TS-D,
    TS-AB, GAS-2, GAS-4, GAS-5, GAS-6, GAS-9, GAS-10, GAS-11, ED-17, ED-28, and most SP rows. `git log --grep` still
    finds their commits.
  - Seven commit subjects pad the number (`PC-09`, `GAS-04` to `GAS-09`), and a bare `GAS-4` also matches GAS-40 and
    up. To match one ID exactly: `git grep -E "GAS-4([^0-9]|$)"` and `git log -E --grep "GAS-0?4([^0-9]|$)"`.
- "Decision n" is a numbered Decision in the plan. "New" means added since `01258b8`.
- "Still open" names a later tracker row on the same code, and "Fixed in Phase 3" points to
  [Phase 3](#phase-3). The plan's tracker has each row's current status.
- Most fixed code carries a comment that says what it used to do, so the old behaviour is readable in place.

## Phase 5

Phase 5 builds the missing features (FEAT-n): the foundations first, then one lab at a time (Decision 30). Each lab
is a small game, built in code and played by a bot in Proving Grounds, and it brings the features it finds missing.
The plan's Phase 5 section has the order. The phase closed on 2026-11-05, with every lab green and every stretch
scenario in.

### Cleanse and immunity (FEAT-8)

The first of the Status-effect lab's needs. An effect's `Tags.RemovesEffectsTagged` makes it a cleanse: as it lands,
every other effect on the target tagged with one of those tags, or a child of one, by its asset tag or a context tag,
leaves at the next tick, before the cleanse's own contained effects land. `Tags.BlocksEffectsTagged` makes it an
immunity: while it's on and its ongoing requirements hold, such an effect can't land, and applying it returns false.
Either took a requirement on each effect it meant to clear or block. The shelf keeps its immunities in a list of their
own: the first version walked the shelf on every application, and GAS-P3's cost test failed.

- **IDs:** FEAT-8, commit `04d0742`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:187` `IsImmuneTo`, over `:183`
    `immunities`, and `:200` `RemoveEffectsNamedBy`, which `ApplyGameplayEffect` calls (`:144`).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:262` and `:268`, the two lists, and
    `:275` `GameplayEffectTags.Names`, what a list names.
- **Watch for:** `GameplayEffectTags` is a struct, so a test that sets a list through reflection boxes it, sets the
  field, then assigns it back.
- **Tests:** `FEAT_8_A_cleanse_takes_off_the_effects_it_names` and `FEAT_8_An_immunity_refuses_the_effects_it_names`
  on each hub in PlayMode's `CleanseAndImmunityTests`, and
  `FEAT_8_The_Effect_inspector_edits_what_an_effect_removes_and_blocks` in `InspectorUndoTests`.

### Cleanse and immunity name granted tags (FEAT-8)

Decision 30's default: a cleanse and an immunity name an effect by a tag it grants too, as Unreal's queries match, so
`State.Burning` takes off whatever grants it. They named an effect by its asset tag or a context tag only, so
"whatever makes you Burning" needed a context tag on every effect that burns.

- **IDs:** FEAT-8, commit `520c433`; Decision 30.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:275`
  `GameplayEffectTags.Names`, which walks the lists by index, so a check allocates nothing.
- **Tests:** `FEAT_8_A_cleanse_and_an_immunity_name_an_effect_by_a_tag_it_grants` on each hub in PlayMode's
  `CleanseAndImmunityTests`.

### The Register builder (FEAT-15)

The first of the foundations (Decision 30). `ProcessControl.Register(process)` starts a registration, which
`HandledBy`, `With` and `In` shape and `Run` makes: it registers the process and starts it, and returns the relay or
why ProcessControl refused it. Left out, the handler follows the packet's run-time type, so an ability's packet hands
its process to its source however it's typed; through the 28 overloads, an ability's packet passed as a plainer one
put its process under GameRoot, so it didn't end with its entity, and `TaskSequence.RegisterAndRun` did that with
every ability's packet. The overloads are `[Obsolete]` wrappers of the builder until the first Asset Store submission.
`ProcessRelay.Run` starts a process that waits.

- **IDs:** FEAT-15, commit `6586536`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRegistration.cs:112`
    `ProcessRegistration.Run`, and `:157` `DefaultHandler`, by the packet's run-time type; `:4`, the refusals.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:277`, the entry points,
    `:294`, the overloads' one message, and `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:661` `RegisterMono` and `:682` `RegisterRuntime`, which say why.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:97` `ProcessRelay.Run`.
- **Watch for:** nothing compiles `Samples~`, so the samples' calls were built against the package's source in a
  throwaway project. Proving Grounds' tests still call the overloads, so the gates cover the wrappers.
- **Tests:** the six `FEAT_15_*` tests in PlayMode's `RegisterBuilderTests`, among them
  `FEAT_15_The_handler_follows_the_packets_run_time_type` on each hub.

### Tag-change events (FEAT-3, GAS-56)

The second foundation (Decision 30). `TagCache.Listen(tag, onChange, kind)` hears the tag and every tag under it, as a
listener on `Status` hears `Status.Burning`: as each comes or goes, at once, or at every weight change with
`ETagChangeKind.AnyCount`. `TagChanged` hears every change. A compile clears the tags and adds them back from their
sources, so it's a rebuild now: the old weights are kept aside by a swap, and its end tells only what changed. Tag
workers are evaluated when the tags change, where every worker was evaluated every frame, unless one asks for every
frame. GAS-56 rides along: a rebuild that changes a tag marks the cache, and the effect system's next tick runs its
requirement pass when an effect is on the shelf, so a loose tag, an ability's or an item's tag can end an effect.

- **IDs:** FEAT-3 and GAS-56, commit `3dc3129`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagCache.cs:520` `Listen`, `:548` `BeginRebuild` and `:558`
    `EndRebuild`, `:597` `Changed`, and `:627` `TellListenersOf`, which walks the listeners by index, so one added
    or stopped while it hears changes nothing under it.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagCache.cs:397` `EvaluateTagWorkers`, and
    `Assets/PlayForge/Runtime/PlayForge/Workers/TagWorker/Core/AbstractTagWorker.cs:37` `EvaluateEveryFrame`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:305` `CompileGrantedTags`, a rebuild, and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:71` `Tick`, GAS-56's check.
- **Watch for:** a tag added straight to the cache asks for no requirement pass, whose compile would drop it, as
  `GAS_P7_A_tag_lookup_costs_the_same_however_long_its_name` showed the first version doing.
- **Tests:** the seven `FEAT_3_*` tests in PlayMode's new `TagEventTests`, and
  `GAS_56_A_loose_tag_ends_an_effect_that_names_it` and `GAS_56_A_tag_an_ability_grants_ends_an_effect_that_names_it`
  in `EffectRequirementTests`.

### One clock, one random source, and IDs per entity (FEAT-13)

FEAT-13's two cheap changes, the third foundation (Decision 30), which keep networking possible without building it.
The runtime reads time through `ForgeTime` and draws random numbers through `ForgeRandom`, each Unity's unless `Use`
puts another in, as a `SeededRandom` whose rolls repeat from their seed; it read Unity's `Time` and `Random` in sixteen
files. An EditMode test scans the runtime's source to keep it so. An activation's `HandleId` counts per ability system,
where one static count numbered every entity's activations in turn, and each effect application has an
`ApplicationId` on its target.

- **IDs:** FEAT-13, commit `c500d5e`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeTime.cs:48` `ForgeTime`, `:56` `Use`, `:151` `ForgeRandom`, and
    `:112` `SeededRandom`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilityActivationHandle.cs:37`, the ID from its ability system,
    and `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:124`, an application's.
- **Watch for:** Unity's random stays the default, so a test that seeds it with `Random.InitState` still repeats its roll.
- **Tests:** `FEAT_13_The_runtime_reads_time_and_random_numbers_only_through_ForgeTime` in EditMode's new
  `RuntimeSourceTests`, and the four `FEAT_13_*` tests in PlayMode's new `ForgeTimeTests`.

### A clock for each entity (FEAT-7, TS-6)

The first part of the time source, the last foundation (Decision 30). `GetClock()` gives an entity its own time, the
game's as `ForgeTime` reads it times its `TimeScale`, so one entity can be slowed, hasted or held still while the rest
run on; every entity ran on Unity's one clock. An effect runs by its target's clock, so a cooldown, which is on its
caster, by the caster's; a sequence by its packet's owner; a task's waits through `SequenceTaskLibrary.DeltaTime`,
which finds its run by the token; a projectile by its handler's. TS-6 rides along: `RunProcessTask` and
`ProcessRelay.RemainingRuntime` default to scaled time. The fixed-rate tick, the time source's second part, follows.

- **IDs:** FEAT-7 and TS-6, commit `48c2c8e`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EntityClock.cs:12` `EntityClock`, which
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:148` steps once a frame.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeTime.cs:69` `DeltaTimeFor`, by an owner, and `:73` by a packet.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:181` `GetDeltaTime`,
    the target's clock, and `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:163`
    `DataOfRun`.
- **Watch for:** process bookkeeping stays on the game's clock, and a physics wait on the fixed step.
- **Tests:** the three `FEAT_7_*` and two `TS_6_*` tests in PlayMode's new `EntityClockTests`.

### The fixed-rate tick (FEAT-7)

The time source's second part, which closes the foundations (Decision 30). With ProcessControl's Fixed Tick Rate set,
each frame adds its time to an accumulator and runs as many ticks as it holds, up to Max Ticks Per Frame: in each,
what waits for a tick goes on, every process steps, and every process steps its end of frame, all by a tick clock that
reads the fixed step. The runtime's 28 frame waits await `ForgeTick.Next`, which is Unity's next frame with ticks off,
so nothing changes by default, and the next tick with them on. A run comes out the same at any frame rate.

- **IDs:** FEAT-7, commit `4058098`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ForgeTick.cs:110` `Next`, through pooled
    completion sources, and `:160` `Advance`, which lets go the waits made before the tick.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:128` `FixedTickRate`, and
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:405` `RunTicks`, which
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:166` `Update` runs, and `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:389` `TickClock`.
    The ticks moved to the plain world with FEAT-10's step 2.
- **Watch for:** a tick steps the end of frame itself, so `LateUpdate` steps nothing while ticks are on; a physics
  wait stays on Unity's fixed step.
- **Tests:** the four `FEAT_7_*` tests in PlayMode's new `FixedTickTests`.

### The Status-effect lab, and overflow at max stacks (FEAT-8)

The first lab (Decision 30). In Proving Grounds' `Tests/Labs`, a caster puts poison, burn and bleed on a training
dummy under each stacking policy, with a cleanse, an antidote, a barrier, a strike and a potion; a bot plays eight
rounds on each hub, and it passed at its first run. The one feature the lab found missing was overflow. A stack bound
held an effect at its max, and nothing more happened. Now, as a stack would take an effect past its bound's Max, the
bound's Overflow Effects land from the application's origin. Deny Refresh On Overflow keeps that application from
refreshing the duration, and Clear Stacks On Overflow takes the stacks off.

- **IDs:** FEAT-8, commit `13eac5b`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/Settings/AbstractEffectConfig.cs:262`
    `OverflowEffects`, the bound's first of three fields, and `:271` `Overflows`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:274`
    `GetStackAmount`, which records the first bound an amount would take past its max.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:508`, where a stack finds it overflows,
    `:528`, where Deny Refresh skips the interaction, and `:570` `Overflow`.
- **Watch for:** the lab's shield worker reads `ForgeTime.FrameCount`, PlayForge's frame, so two blows in one tick
  share what the shield has left.
- **Tests:** `FEAT_8_An_effect_at_its_max_stacks_overflows` and
  `FEAT_8_Deny_Refresh_keeps_an_overflowing_application_from_refreshing` on each hub in PlayMode's
  `CleanseAndImmunityTests`, and the lab's 16 in `StatusEffectLabTests` (category Lab).

### Abilities granted by effects, and the chance to apply (FEAT-8)

FEAT-8's last two parts, which finish it. An effect's Granted Abilities are given to its target as a lasting effect
lands, at the level of what applied it, as an item gives its ability at the item's level. The shelf notes the slot
each took and takes it back as the effect comes off, interrupting it if it runs. An ability the target can't take
isn't lent, so the target keeps its own (GAS-48). An effect's Chance to Apply, a scaler magnitude on its impact and 1
by default, is drawn once each application from `ForgeRandom`, after the requirements and immunities pass. A miss is
refused, and a chance of 1 or of 0 draws nothing, so effects without one leave the random numbers as they were. The
lab gains a jagged strike that lacerates on a seeded roll and a blessing that lends the caster Smite.

- **IDs:** FEAT-8, commit `60683cd`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:171` `RollsToApply`, which
    `ApplyGameplayEffect` asks (`:108`); `:357` `LendAbilities`, after `Shelve` (`:266`), and `:378`
    `TakeBackAbilities`, as the cleaning pass ends a container (`:737`) and as a replace swaps effects (`:489`).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:22` `GrantedAbilities`, and
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:141` `ChanceToApply`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:302`, the Tags section's
    Granted Abilities, and `:661` `ChanceSummary`. The Requirements section draws the chance, at `:414`,
    above its tables; it sat in the impact drawer until ED-42's commit.
- **Watch for:** a new field's initializer gives an existing asset its default, so every effect saved before this has
  a chance of 1. An instant effect lends nothing, as it's never on its target.
- **Tests:** the five `FEAT_8_*` tests on each hub in PlayMode's new `GrantedAbilityAndChanceTests`,
  `FEAT_8_The_Effect_inspector_edits_the_chance_to_apply_and_granted_abilities` in `InspectorUndoTests`, and the
  lab's `Lab_A_jagged_strike_lacerates_on_a_roll` and `Lab_The_blessing_lends_the_caster_an_ability`.

### Cancel and block abilities by tag, and While active (FEAT-5)

The MOBA hero kit's feature, ahead of its lab (Decision 30). An ability's Cancelled By names the abilities, by asset or
context tag, that cancel it as they activate, and its Blocks those that can't activate while it runs. Each stage can
name its own, and a stage that names none takes the ability's, as targeting does. As an ability activates, each
running ability in a stage it cancels is interrupted first, but a critical stage is committed and goes on. A blocked
activation is refused under every policy; retrying it waits for FEAT-6's input buffer. The While active row of an
ability's tag requirements holds the caster's tags while it runs: the ability system hears every tag change and checks
the running abilities each step, after the effects tick, so a stun an effect grants ends a channel in the step it lands.
Nothing cancelled an ability but an interrupt by slot, blocking was the claim alone, and requirements weren't checked
once an ability ran.

- **IDs:** FEAT-5, commit `cfab32a`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:407`, where `CanActivateAbility`
    asks `IsBlocked` (`:456`) and, through `CanActivateUnblocked`, the While active group (`:430`); `:735`, where
    `TryActivate` calls `CancelAbilitiesCancelledBy` (`:466`); and `:571` `CheckWhileActive`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:226` `Blocks` and `:243`
    `CancelFor`, which read each run's stage.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityBehaviour.cs:36`, the ability's
    lists, `:78` a stage's, `:87` `CancelledByOr` and `:93` `IsCritical`; `AbilityTags.Names` is
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/Core/AbilityTags.cs:44`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:59`, which wires the tag changes to the ability system, and
    `:152`, where each step checks.
- **Watch for:** an interrupt from While active ends a critical stage too, as an interrupt by slot does. A stage reads
  as critical when any of its tasks is a critical section.
- **Tests:** the five `FEAT_5_*` tests on each hub in PlayMode's new `CancelAndBlockTests`,
  `FEAT_5_The_Ability_inspector_edits_cancel_block_and_While_active`, and `ED_37_The_requirements_table_opens_a_cell_to_edit_it`,
  which now expects the While active row.

### The MOBA hero kit lab, GAS-57, and While active heard at once (FEAT-5)

The second lab (Decision 30). In Proving Grounds' `Tests/Labs/MobaLab.cs`, four heroes of four abilities play on a
clock of half a second a frame: mana costs, cooldowns that a hero's Cooldown Reduction shortens, armor and magic
resist, lifesteal, a silence, a stun, a cleanse, and a channel that a stun ends, a blink cancels and that blocks the
caster's other spells. Its bot found two bugs. The framework's lifesteal, the relative-operation impact worker, read
the impacted attribute's value, never its Relative Attribute, so it healed the attacker by its own health times the
blow (GAS-57). And a While active group checked at the stunned entity's next step let a channel's tick that came due
earlier in the frame land: the ability system now checks as it hears the tag change.

- **IDs:** GAS-57, FEAT-5, commit `26aa233`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Authored/RelativeOperationContextImpactWorker.cs:100`
    `GetWrtTarget`, which reads the Relative Attribute (`:103`), or the impacted attribute when none is set.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:496` `HearTagChange`, which
    checks at once, `:571` `CheckWhileActive`, guarded against a change its own interrupt sets off, and `:590`
    `InterruptFailingWhileActive`.
- **Watch for:** GAS-41's test leaves Relative Attribute unset and expects the impacted attribute's value, so a worker
  with none set keeps the old reading. The lab's armor and lifesteal are a game's code, written the way a game would.
- **Tests:** `GAS_57_A_relative_worker_reads_its_relative_attribute` in `WorkerTests`,
  `FEAT_5_A_stun_ends_a_channel_before_its_next_tick` in `CancelAndBlockTests`, and the lab's nine rounds in
  `MobaLabTests` (category Lab).

### The input buffer, stages that branch, and the Duel lab (FEAT-6, TS-7)

The Duel lab's features (Decision 30). The Queue policy is the input buffer: a press that can't start, as while another
Queue ability holds the claim or a running stage blocks it, takes its ability's one slot, replacing an earlier press,
and waits out its Buffer Window in the caster's time. As a claim goes, and each step, the highest Buffer Priority that
can start fires, then the newest. A Queue request waited for good and fired in the order asked. An ability's stage can
have a Condition, a tag in the activation's data or on the caster or the target, there or missing, which the compiler
emits as a one-case branch; Apply Effects records a landing in the data, so a jab recovers one way on a hit and another
on a miss. A critical section now ends as the run starts a stage past its last critical one, so a critical stage a
branch skipped lets the claim go. The Duel lab, two fighters with frame data, hit-stop, the buffer and cancel windows,
found that ending it at the skip itself was a moment too soon: a buffered special fired then didn't see the hit
recovery it cancels. TS-7's shape is a design the plan's Log records for the user to review.

- **IDs:** FEAT-6, TS-7, commit `b0d6492`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:698` `TryActivate`, where a block
    or a busy claim sends a Queue request to `BufferActivation` (`:793`); `:815` `FireBuffered` and `:842`
    `StepBuffer`, which `GASCore.Tick` calls (`Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:155`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityBehaviour.cs:111`
    `AbilityStageCondition` and its `Holds` (`:126`); the compiler's branch is
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilitySequenceCompiler.cs:52`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:431`, where each stage's start
    calls `ExitCriticalSectionIfPassed` (`:843`).
- **Watch for:** claims are per policy, so a Refuse request doesn't wait on a Queue claim. Put a committed whiff before
  the hit's recovery: the claim goes as the stage after a skipped critical one starts. The MOBA lab casts under Refuse,
  as under its heroes' Queue a spell its channel blocked would buffer.
- **Tests:** the five `FEAT_6_*` tests on each hub in PlayMode's new `InputBufferTests`, the three `TS_7_*` in
  `BranchingStageTests`, `FEAT_6_The_Ability_inspector_edits_the_buffer`, `TS_7_A_stage_edits_its_condition`, and the
  Duel lab's seven rounds in `DuelLabTests` (category Lab).

### A pool's object goes back to its pool, and the Bullet heaven lab (PC-33)

The fourth lab (Decision 30). A wizard fires the framework's `ProjectileMonoProcess` at a horde of 500 runtime enemies,
drawn from a pool the lab writes as a game would (Decision 23); its aura burns what's in reach, and a death blasts the
neighbours, which cascades. Its first round found PC-33: the Mono wrapper handed a finished object back through the
object's own `Instantiator`, which a clone never has, as the field isn't serialized, so a pool's object was destroyed as
its process ended. The wrapper now keeps the instantiator that made the object, a template's or the handler's, and
hands it back to that one. The 10,000-bolt round takes about half a second of batch time, so the review's O(n)
unregister doesn't show at this scale.

- **IDs:** PC-33, commit `f66e04d`; Decision 23, Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/MonoWrapperProcess.cs:22` `madeBy`, set as `ConfigureWrapper` asks an instantiator (`:42`), and read in `WhenTerminate` (`:108`).
- **Watch for:** Replace Existing leaves an effect's time alone; an aura that refreshes needs Refresh Duration, as
  EFFECTS.md says. The lab's entities are unobserved, as 500 frame-summary listeners would cost more than the lab.
- **Tests:** `PC_33_A_pooled_object_goes_back_to_its_pool` in PlayMode's new `PoolingTests`, and the lab's four rounds
  in `BulletHeavenLabTests` (category Lab).

### Item stacking, level-up rescaling, and the Roguelite lab (FEAT-14, GAS-58)

The fifth lab (Decision 30). A give fills an item's stack first, up to its Max Stack Size, and takes no new slot; a full
stack opens another only with Allow Duplicate Items. `GiveItem(item, count)` gives one at a time through the old path,
which now stacks first, and says how many it took; `RemoveItem(item, count)` takes stacks off. A stack applies its
effects once, and `ItemStacksScaler` reads its count, which an equipped item's effects read again as it changes. The lab,
a hero's build of a stacking whetstone, a three-piece set whose bonuses follow its tag (FEAT-3), tomes that teach
twice under an aura and a crown that levels, found GAS-58: a lasting effect's magnitude was read as its modifier
registered, so the crown kept its first level's bonus. The GAS core hears its level system and re-reads the effects
whose origin's level changed.

- **IDs:** FEAT-14, GAS-58, GAS-51, commit `643f5c0`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:82`, where a give tries `TryStack` (`:102`) first; `:120`
    `GiveItem(Item, int)` and `:132` `RemoveItem(Item, int)`.
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:35` `ChangeStacks`, and
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/ItemStacksScaler.cs:12` `ItemStacksScaler`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:325` `RefreshEffectsFrom` and `:339` `RefreshEffectsLevelledBy`, which
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:133` `LevelChanged` calls (wired at `:62`).
- **Watch for:** `RemoveItem(item)` still takes the whole slot. An effect an entity's item put on another entity isn't
  on its shelf, so it waits for its next read as the item levels.
- **Tests:** the five `FEAT_14_*` tests in PlayMode's new `ItemStackingTests`, `FEAT_14_The_Item_inspector_edits_its_stack_size`,
  `GAS_58_An_items_effect_rescales_as_the_item_levels` in `LevelRescalingTests`, and the lab's five rounds in
  `RogueliteLabTests` (category Lab).

### Save and load (FEAT-9)

For the Tactics lab (Decision 30). `ForgeSave.Capture` takes an entity at rest into a `ForgeSnapshot` of plain
serializable types, and `ForgeSave.Restore` puts it back, on the same entity or a fresh one of its kind. A save holds
each attribute's permanent commits by retention group, the levels, the abilities the entity knows of its own, its
items, worn or not, and its effects with their origin, stacks and clock; modifiers come back with their effects. A
restore interrupts what's running and lets go of what the entity has, all quietly, then puts back the save's. Nothing
of an effect's application runs again, and nothing is told until one tag compile and one `OnRestored`. A
`ForgeAssetCatalog` names the assets by IDs that last, and an `IForgeEntityIds` names other entities.

- **IDs:** FEAT-9, commit `eee91a6`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:69` `Capture`, and `:264` `Restore`, whose order is the design; `:190` `SaveOrigin` and
    `:410` `OriginOf`, which name what applied an effect and find it again.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:801` `ClearForRestore` and `:828` `Restore`, the shelf's quiet paths, and
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:91` `CaptureClock` and `:109`
    `RestoreClock`, which `IndependentDurationsContainer` overrides for its stacks' own times.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:367` `GetCommits` and `:376` `RestoreCommits`.
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:220` `RestoreEquipped`, and
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:277` `GiveQuietly`, which `GiveAbility` now wraps.
  - `Assets/PlayForge/Runtime/PlayForge/Save/ForgeAssetCatalog.cs:103` `AddProjectAssets`, and `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:139` `Stepping`, which
    refuses a load inside the entity's step.
- **Watch for:** a restore lets go of everything before it puts anything back, so an effect whose source is another
  entity finds that entity's abilities only once that entity is restored; restore the sources first. The effect's
  origin otherwise falls back to the entity it stood for.
- **Tests:** the nine `FEAT_9_*` tests in PlayMode's new `SaveLoadTests` and EditMode's
  `FEAT_9_Add_Every_PlayForge_Asset_names_each_by_its_GUID`. The guide is `Assets/PlayForge/Documentation/SAVING.md`.

### A warning for usage effects no stage applies, and the Tactics lab (ED-41)

The Tactics lab, the seventh (Decision 30), plays a battle a turn at a time on a clock that moves only as a turn ends,
and saves and loads it between turns (FEAT-9). It found ED-41: an ability's cost and cooldown apply only from a stage
set to Apply Usage Effects, and nothing said so when none was, so its Slash had a cooldown that never came on. The
Ability inspector warns under Cooldown, and the Problems view lists it, from one check.

- **IDs:** ED-41, commit `6129b13`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Editor/Problems/ForgeRules.cs:217` `UsageEffectsRule`, and `:227` `Unapplied`, which the inspector shares.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:392`, the note under Cooldown, and `:423`
    `ShowUnappliedUsage`, which `OnAssetChanged` calls as a stage changes.
- **Tests:** `ED_41_The_Ability_inspector_warns_of_usage_no_stage_applies` in EditMode's `InspectorUndoTests`,
  `ED_41_A_cost_or_cooldown_no_stage_applies_is_a_problem` in `ProblemsTests`, and the lab's six rounds in
  `TacticsLabTests` (category Lab).

### An Effect Stacks scaler, and the Combat lab (FEAT-19)

The Combat lab, the eighth and last (Decision 30), puts every delivery, re-application and stacking policy on one
training dummy, with a health bar read from its Health breakdown by retention group; every claim it tests from
EFFECTS.md held. It found FEAT-19 missing: nothing built in read an effect's stack count, so a change by the stack took
a scaler written for it. `EffectStacksScaler` reads it, as `ItemStacksScaler` reads an item's (FEAT-14).

- **IDs:** FEAT-19, commit `2944804`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/EffectStacksScaler.cs:11` `EffectStacksScaler`: `:20` reads a
    stacking container, and `:17` a spec, through its container, or 1.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/ScalerImpacter.cs:49`, where an impact on a stacking effect evaluates against its
    container, which is how the scaler sees the count.
- **Tests:** `FEAT_19_The_Effect_Stacks_scaler_reads_the_effects_stack_count` in PlayMode's `EffectStackingTests`, and the
  lab's five rounds in `CombatLabTests` (category Lab).

### Profiler markers, and activation by asset or tag with a reason (FEAT-18, FEAT-17)

The first of the stretch work (Decision 30). PlayForge had no Profiler marker, so its cost showed only as its callers';
five markers now name a process's step, a GAS's step and end of frame, an effect's application and an ability's
activation. An ability activated only by its slot, and a refused activation said only false; it activates by its asset
or a tag it carries, and a refusal says why, as a callback tells it.

- **IDs:** FEAT-18, FEAT-17, commit `c7f7ef1`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeProfiler.cs:14` `ForgeProfiler`, whose `ProcessStep` wraps a process's step at
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:441`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/ActivationRefusal.cs:7` `EActivationRefusal` and `:56` `ActivationRefusal`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:626`, `:637` and `:657`, the request's, the asset's and the tag's
    `TryActivateAbility`; `:698` `TryActivate`, which names each refusal, and `:415` `UnblockedRefusal`, with
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpec.cs:42` `SourceRefusal` for the caster's.
- **Watch for:** a request the input buffer retries isn't told again as it waits, and a check on the target comes after
  targeting, so it ends the activation instead of refusing it.
- **Tests:** `FEAT_18_PlayForge_shows_in_the_Profiler` in PlayMode's new `ProfilerMarkerTests`, and the two `FEAT_17_*`
  tests in its new `ActivationRefusalTests`.

### Copying an entity, and a level's max before its level (FEAT-20, GAS-59)

The review's mirror clones, a stretch scenario (Decision 30), copy a GAS's state onto a new runtime GAS. A copy took
FEAT-9's capture and restore, which need a catalog naming every asset and IDs for every entity; `ForgeSave.Copy` names
both for the session. A save keeps the entity's own level under no key, so it loads onto another kind's own. The
scenario found GAS-59 too: a level and its max set at once applied the level first, so a raise past the old max was
held at it.

- **IDs:** FEAT-20, GAS-59, commit `7e6dae3`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:469` `Copy`, with `:488` `SessionNames` and `:518` `SessionIds`; `:85` `Take` and `:297`
    `Put`, which Capture, Restore and Copy share, through `:24` `IAssetNames`, which the catalog implements too.
  - `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:100`, the entity's own level under no key.
  - `Assets/PlayForge/Runtime/PlayForge/LevelComponent/SystemLevelsComponent.cs:57` and `:82`, the max before the level.
- **Tests:** `Stretch_A_mirror_clone_takes_its_originals_state` in Proving Grounds' new `Tests/Stretch` (category
  Stretch), with the chain reactions beside it, and `GAS_59_A_level_raised_past_its_old_max_at_once_takes_it` in
  `LevelRescalingTests`.

### SetByCaller magnitudes, and a sequence made in code (FEAT-2, FEAT-21)

The review's ability crafting, a stretch scenario (Decision 30), builds spells from runes as the game runs and casts them
through one spell effect whose damage the caster sets. An effect's amount came only from its asset and its scalers
(PF-C01); a magnitude set by the caller under a tag now reaches every effect made from the cast's data. The scenario
found FEAT-21 too: Run Sequence ran only a sequence a static method made, by name, so a cast built in code couldn't run.

- **IDs:** FEAT-2, FEAT-21, commit `c24fa00`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffectSpec.cs:51` `SetByCaller` and `:64` `TakeSetByCaller`, and
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/ProcessDataPacket.cs:116` and `:126`, the data's own.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/SetByCallerScaler.cs:13` `SetByCallerScaler`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Gameplay/ApplyEffectsAbilityTask.cs:35`, where an ability's effect takes its data's;
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpec.cs:21` `ApplyUsageEffects`, the cost's and cooldown's; and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:214` `ApplyMadeFrom`, a contained or overflow effect's.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/RunSequenceTask.cs:18` `Definition`, a sequence made in code.
- **Tests:** the three `FEAT_2_*` tests in PlayMode's new `SetByCallerTests`, `FEAT_21_Run_Sequence_runs_a_sequence_made_in_code`
  in `NestedSequenceTests`, and the scenario's two rounds in `Tests/Stretch/AbilityCraftingTests` (category Stretch).

### Ability triggers (FEAT-4)

An ability activated only when code asked for it (Decision 30). Its triggers set it off on its own: a gameplay event
sent to its entity, or a tag its entity gains, under the trigger's tag or one under it. The review's beyond combat
scenario, crops that bloom as they ripen and a dialogue gate a greeting opens, drove it and held at its first run.

- **IDs:** FEAT-4, commit `b4e0cbd`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/Core/AbilityTrigger.cs:7` `EAbilityTriggerSource` and `:21` `AbilityTrigger`, which
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/Core/AbilityDefinition.cs:37` `Triggers` lists.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:518` `SendGameplayEvent`; `:496` `HearTagChange`, where a tag
    gained queues its triggers, and `:529` `FireTagTriggers`, which fires them one at a time; `:545` `FireTriggers`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:384`, the Triggers field.
- **Watch for:** a trigger tries the ability as a request would, so a busy or refused activation is refused, and told
  (FEAT-17). Tags gained at a GAS's start fire their triggers too.
- **Tests:** the two `FEAT_4_*` tests in PlayMode's new `AbilityTriggerTests`, EditMode's
  `FEAT_4_The_Ability_inspector_edits_its_triggers`, and `Tests/Stretch/BeyondCombatTests` (category Stretch).

### Gameplay cues (FEAT-1)

An effect changed attributes and tags and nothing else, so what it showed and sounded was each game's own code
(Decision 30). An effect now lists cue handlers, as it lists workers: a burst as it lands, ticks or comes off, and a
lasting cue for as long as it's on. The review's sequence-driven boss, a fight in phases from one TaskSequence whose
blows shake the camera, drove it and held at its first run.

- **IDs:** FEAT-1, commit `2b623ed`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Cues/AbstractCueHandler.cs:14` `AbstractCueHandler`, whose virtual hooks are the bursts and a
    lasting cue's Begin and End, and `:33` `CueContext`, which `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:35` `Cues`
    lists.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:765` `Cue`, which plays an effect's cues for a moment. It's called from
    `:221` `ApplyInstantGameplayEffect`, `:254` `ApplyDurationalGameplayEffect`, `:652` `TickEffectShelf` and `:719`
    `ClearFinishedEffects`, and from `:801` `ClearForRestore` and `:828` `Restore`, which end and begin the lasting
    cues and play no burst.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Cues/ForgeCues.cs:4` `ICameraShaker` and `:14` `Shaker`, which
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Cues/CameraShakeCue.cs:11` `CameraShakeCue` goes through. Beside it are
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Cues/PrefabCue.cs:13` `PrefabCue` and `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Cues/SoundCue.cs:8` `SoundCue`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:448` `BuildCuesSection`, and `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Other/CueDrawers.cs:7`
    `CueHandlerDrawer`.
- **Watch for:** one handler serves every entity its effect is on, so a lasting cue keeps what it placed by spec, as
  `PrefabCue` does. A cue that throws is reported, and its effect goes on. An effect absorbed into one already on plays
  its burst and doesn't begin again.
- **Tests:** the three `FEAT_1_*` tests in PlayMode's new `GameplayCueTests`, EditMode's
  `FEAT_1_The_Effect_inspector_edits_its_cues`, and `Tests/Stretch/SequenceBossTests` (category Stretch).

### Targeting input (FEAT-11)

The five click-targeting tasks read the Input Manager's mouse and `Camera.main` themselves (Decision 30). A game on the
Input System alone, with a camera or a reticle of its own, or a test couldn't stand in, and a scene with no main camera
threw at the press. They read the game's input now, with the project's own as the default.

- **IDs:** FEAT-11, commit `ed60c62`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeInput.cs:13` `IForgeInput`, a press and the ray the pointer aims along, and `:31` `ForgeInput`,
    whose `:39` `Use` puts the game's in and `:50` `Default` picks the project's own. `:62` `TryAimThrough` aims through
    a camera given or `Camera.main`.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeInput.cs:93` `LegacyPointerInput` and `:109` `InputSystemPointerInput`, the defaults, each behind
    the defines that say its input is there.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/AbstractTargetingAbilityTask.cs:110` `WaitForPointerPress`, which each task
    awaits, as `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/Authored/SelectPositionTargetTask.cs:39` does.
  - `Assets/PlayForge/Runtime/FarEmerald.PlayForge.asmdef:18`, the optional `Unity.InputSystem` reference's version
    define.
- **Watch for:** `PLAYFORGE_INPUT_SYSTEM` comes from the asmdef's version define, so a csproj Unity hasn't regenerated
  lacks it. The Input System default is compile-checked only, as Proving Grounds has no Input System. A press with no
  camera aims nowhere: a task that retries on a miss waits for the next.
- **Tests:** EditMode's `FEAT_11_The_runtime_reads_input_only_through_ForgeInput`, and PlayMode's new
  `TargetingInputTests`.

### Clear gives a new asset's values (ED-42), and Chance to Apply in Requirements (FEAT-8)

A section's Clear set each field to its type's default, 0 or the first choice. Clearing an effect's Impact set its
affiliation to Use Affiliation List with none listed and its Chance to Apply to 0, so the effect never landed. It came
to light as the chance moved to Requirements, where the design puts it, since it gates the whole application.

- **IDs:** ED-42 and FEAT-8, commit `4d110da`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/SectionClearUtility.cs:22` `Clear`, which makes `:99` `FreshOf`, a new asset of the target's
    kind that no one sees, and `:154` `ResetPropertyToDefault`, where `:109` `TakeFresh` gives each field the new
    asset's value. A list still empties.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:375` `ImpactPaths`, the impact's own fields but `:372` `ChancePath`, so
    Impact's Import and Clear leave the chance. `:396` `BuildRequirementsSection` draws the chance above the tables
    (`:414`), and `:638` `RequirementsSummary` says a chance short of 1.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:556` `TagsSectionSummary`, which counts the abilities granted, as in "Grants
    Status.Burning, 1 ability".
- **Watch for:** `FreshOf` makes a ScriptableObject of the target's type, so its `OnEnable` runs; a target that isn't
  an asset keeps the old type defaults. A path a new asset doesn't have, as inside a list, does too.
- **Tests:** EditMode's `ED_42_Clearing_a_section_gives_its_fields_a_new_assets_values` and
  `FEAT_8_The_Effect_inspector_edits_the_chance_to_apply_and_granted_abilities`, in `InspectorUndoTests`.

### A process reaches its own control (FEAT-10, step 1)

The headless split's first step (Decision 30). A process's relay, its block's state changes, a watch on it and what a
runtime process handles reached `ProcessControl.Instance`, whichever control held the process, so once another control
was Instance, as a new scene's is while the old one goes, they reached the wrong one. Each goes to the control that
holds the process, which the coming `ProcessWorld` will replace.

- **IDs:** FEAT-10, commit `40f257e`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:145` `Control`, set at `:180` `Generate`, which the block's state changes and its
    unregistering go through.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:21` `Control`, and `:15` `ProcessActive`; the relay's Pause, Unpause, Run and Terminate
    go there too.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:332` `WatchProcess`, which kept a watch in Instance's table.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/AbstractRuntimeProcess.cs:119` `WhenTerminate`, which ends what it handles through
    each one's relay, and `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/GroupProcess.cs:125`, a group's watch on a member.
- **Watch for:** registration's default is still `ProcessControl.Instance`, and an ability's sequences still register
  there; step 4 moves those to the caster's world.
- **Tests:** the three `FEAT_10_*` tests in PlayMode's new `ProcessOwnerTests`.

### The plain world (FEAT-10, step 2)

ProcessControl's bookkeeping was the MonoBehaviour itself, so no process ran without a ProcessControl in a scene. It's a
plain class now, `ProcessWorld`, which a scene's ProcessControl wraps and steps from Unity's loop, and which code can
own and step itself. Its code moved nearly as it was, the Mono branches with it, since they run only for a Mono process.

- **IDs:** FEAT-10, commit `1eb436a`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:14` `ProcessWorld`: `:17` `Control`, its ProcessControl or null; `:20` its public
    constructor, a world of its own, ready; `:462` `Step`, a frame's Update and LateUpdate; `:581` `StepProcesses` and
    `:405` `RunTicks`; `:682` `RegisterRuntime` and `:661` `RegisterMono`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:48` `World`, which the MonoBehaviour owns and its `:166` `Update` steps, and `:188`
    `StepProcesses`, kept by name for the golden tests.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRegistration.cs:109` `In`, a registration into a world; `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:142` `World`
    and `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:24` `World`, where a block's state changes and a relay's calls go.
- **Watch for:** the world's settings read its ProcessControl's inspector when it has one, so a change there holds at
  once. Ticks are still global: only the Instance's world drives `ForgeTick`, until step 3.
- **Tests:** PlayMode's new `ProcessWorldTests`.

### A world's own time (FEAT-10, step 3)

A world of its own stepped in a loop got nowhere: its sequences waited on one global tick, which only the scene's
ProcessControl advanced, or on Unity's next frame. Each world of its own has a tick and a clock of its own now, in force
while it steps or moves a process, so a loop of its steps runs it through its time within one frame.

- **IDs:** FEAT-10, commit `4a603ae`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ForgeTick.cs:96` `ForgeTickSource`, a tick, and its `:160` `Advance`; `ForgeTick`'s `:29` `Scene` and `:32`
    `Current`, which its statics read, and `:83` `Enter`, which puts a tick in force.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:436` `ownTick`, `:440` `Tick`, `:446` `WorldClock` and `:481` `Step`, by a time its owner
    gives; `:1209` `ProcessWillChangeState`, which puts the world's tick in force as a process moves.
- **Watch for:** a wait at FixedUpdate's timing is still Unity's. An ability activated between steps starts its
  sequence outside them; step 4 takes that up.
- **Tests:** the two `FEAT_10_*` tests in PlayMode's `ProcessWorldTests`.

### Abilities in their caster's world (FEAT-10, step 4)

An ability's sequences registered with the scene's ProcessControl wherever its caster was, and a runtime entity that fell
ended whatever the scene's control held under its cache index. Each of an ability's processes registers in its caster's
world now, a fallen entity ends through its own relay, and the keep-alives wait tick by tick, so a duel in a world of
its own plays out within one frame of its owner's steps.

- **IDs:** FEAT-10, commit `c5e9cb8`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:33` `Of`, the world that holds a process.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:201`, the targeting, and `:306`, the ability's sequence,
    each `.In` its caster's world; `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:423`, the world's new processes.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem_EndOfFrame.cs:57`, a fallen entity's end, and
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/LazyRuntimeProcess.cs:24`, a keep-alive.
- **Watch for:** a blow lands at its target's end of frame, so two lethal blows in one step both land. A process no world
  holds takes the scene's.
- **Tests:** `FEAT_10_A_duel_in_a_world_of_its_own_ends_within_one_frame` in PlayMode's `ProcessWorldTests`.

### Waits that ask once a tick (FEAT-10, step 5)

A sequence's conditional waits asked Unity's loop once a frame, so a world of its own stepped in a loop never saw what
they waited for change. They ask once a tick where the tick in force ticks, and are UniTask's own waits where it
doesn't, so a scene without ticks waits as it did.

- **IDs:** FEAT-10, commit `d88583a`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ForgeTick.cs:61` `WaitWhile`, the facade's, and the tick's own at `:141` `WaitWhile`, with `:149`
    `TicksAt` and `:154` `Asked`, which asks once a tick on the tick in force as the wait began.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:193`, the pause hold, and
    `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceBuilder.cs:332` `DelayUntil`, among the waits that go through it.
- **Watch for:** the physics step's timings and the animator wait stay on Unity's loop.
- **Tests:** the two step-5 `FEAT_10_*` tests in PlayMode's `ProcessWorldTests`.

### Worlds side by side, and with no scene (FEAT-10, step 6)

Each world numbered its processes from 0, and a handler keeps what it handles by that number, so GameRoot, which took
what was registered with no handler, refused a second world's first process. Every world draws from one counter now.
A world of its own reaches for nothing of the scene's, so it runs with no scene at all.

- **IDs:** FEAT-10, commit `b9ea337`; Decision 30. The six steps are in the plan's Log.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:171` `NextCacheIndex`, which draws from the one counter, `s_cacheCounter`, and counts
    the world's own in `Created`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRegistration.cs:121`, in `Run`, where a world of its own takes no handler and the default packet,
    and `:157` `DefaultHandler`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:117` `WhenTerminate`, which ends what it handles through each
    relay.
- **Watch for:** a world of its own's owner ends what it holds; nothing of the scene's does.
- **Tests:** the step-6 `FEAT_10_*` tests in PlayMode's `ProcessWorldTests` and EditMode's
  `ProcessControlEditModeTests`, and Proving Grounds' `HeadlessSimulationTests`.

### The step loop's, tags', recomputes', stages' and each type's markers (FEAT-18)

The first of FEAT-18's three open parts (Decision 32): the step loop at each timing, a tag compile, an attribute's
recompute and a stage's start show in the Profiler under their own names, and each process steps under a marker of its
type's, with its process ID as metadata. That marker is a `ProfilerMarker<int>` from `com.unity.profiling.core`, a
dependency now.

- **IDs:** FEAT-18, commit `d2de8ec`; Decision 32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeProfiler.cs:65` `StepOf`, a type's step marker, made the first time the type steps, and `:50`
    `StepLoop`, the step loop's at a timing.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:432`, in `Step`, where a block finds its type's marker at its first step and keeps
    it, and `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:597`, in `StepProcesses`, the step loop's.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:757`, in `ExecuteStageInternal`, the stage's marker.
- **Watch for:** a marker ends where it began, so a stage's holds only its start, its tasks up to the first that waits.
- **Tests:** `FEAT_18_The_step_loop_tags_recomputes_stages_and_each_type_show_in_the_Profiler` in PlayMode's
  `ProfilerMarkerTests`.

### Profiler counters (FEAT-18)

The second of FEAT-18's three open parts (Decision 32). The markers time PlayForge's work; the counters say how much of
it there is: ProcessControl's processes and a frame's registrations, terminations, steps, state changes and step time,
the GAS's entities, effects, impacts, activations, refusals, tag compiles and recomputes, and TaskSequence's sequences
and stages.

- **IDs:** FEAT-18, commit `67bc9d0`; Decision 32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeCounters.cs:13` `ForgeCounters`, a frame's counts and the levels, and `:99` `ResetLevels`, which
    starts the levels that follow what starts and ends from 0 each session.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:613`, in `StepProcesses`, where the scene's ProcessControl sets the process levels as
    its LateUpdate loop ends.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:244` `CountEffects`, which brings Effects Active up to date as a GAS steps.
- **Watch for:** a world dropped with its processes still in it leaves its entities, effects and sequences counted.
- **Tests:** `FEAT_18_PlayForges_counters_count_its_work` in PlayMode's `ProfilerCounterTests`.

### The Profiler modules, and a frame's costliest processes (FEAT-18)

The last of FEAT-18's three open parts (Decision 32), which closes it. Two Profiler modules chart the counters, and the
Processes module's details list the selected frame's costliest processes, which PlayForge puts into the capture as
frame metadata while the Profiler records.

- **IDs:** FEAT-18, commit `f2341a2`; Decision 32.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/ForgeProfilerModules.cs:18` `PlayForgeProcessesProfilerModule` and `:46` `PlayForgeGASProfilerModule`,
    each with its `Counters`; `:71` `CostliestProcessesView`, whose `:139` `Show` reads a frame's metadata.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeFrameCosts.cs:30` `ForgeFrameCosts`: `:58` `Stepped` adds a step's time to its block's frame, and
    `:70` `EndFrame` puts the ten costliest into the capture, which `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:179` calls as the
    scene's frame ends.
- **Watch for:** the costs go under one tag and each one's type's name under the tag after it, in order; a reader
  takes both.
- **Tests:** EditMode's `FEAT_18_The_Profiler_modules_chart_PlayForges_counters` and PlayMode's
  `FEAT_18_A_frames_costliest_processes_go_into_the_Profilers_capture`.

### The Debugger in a narrow column (ED-43)

The Debugger kept its wide layout at any width, so docked in a column it squeezed its lists and details side by side.
It lays itself out by its width now, as EDITOR_STYLE's Debugger section and the canvas's narrow boards have it.

- **IDs:** ED-43, commit `73c4208`.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:535` `Fit`, which the window's geometry calls: below 720 px it turns each view's
    split, hides the low-priority columns and moves the frame count; `:555` `Lay`, a split's orientation and its fixed
    pane.
  - `Assets/PlayForge/Editor/UI/PlayForge.uss`, the `pf-debugger--narrow` rules: the toolbar's views on a row of their own.
- **Watch for:** a view added to the Debugger lays its split out in `Fit` too, and a column that gives way hides there.
- **Tests:** `ED_43_The_Debugger_fits_a_narrow_column` in EditMode's `RuntimeDebuggerTests`.

### The Debugger's Timeline: the ability graph and the effect recorder (FEAT-12)

The Debugger showed what an entity held now, never what it did over time. Its fourth view, Timeline, draws what a
recorder heard as the game played: each ability's runs, stage by stage, its refusals, and each effect's time on the
shelf; and a track selected shows an ability's graph, read-only, beside its runs.

- **IDs:** FEAT-12, commit `514fb70`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/TimelineRecorder.cs:57` `TimelineRecorder`: `:90` `Follow` hears the entities the Debugger lists,
    `:186` `Activated` opens a run keyed by its activation's data packet, and `:256` `Removed` ends an effect's span
    once no container of it is left on the shelf.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Timeline.cs:39` `BuildTimelineView`, `:154` `SampleTimeline`, which follows the
    entities whatever the view, `:192` `ShowTrack` and `:268` `StageBlock`, a stage of the graph; and `:404`
    `TimelineStrip`, a track's spans in its row.
- **Watch for:** the recorder measures by `ForgeTime.Time`, so a game that changes its clock moves the spans with it.
- **Tests:** `FEAT_12_The_Timeline_records_abilities_and_effects_as_they_run` in EditMode's `RuntimeDebuggerTests`.

### An entity's time scale from an attribute, and time magic (FEAT-22)

The time magic scenario's slow field and haste: an entity's time could be set only by its game's code, so no effect
could slow one. Its identity names the attribute its time runs at now, so a slow or a haste is an effect on that
attribute, and stacks, ends and comes off as effects do.

- **IDs:** FEAT-22, commit `0cbdb9a`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:175` `FollowTimeScale`, which `Tick` calls as each step begins, at `:147`, and
    `Initialize` at `:96`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Identity/EntityIdentity.cs:52` `TimeScaleAttribute`, which the runtime identity copies, at
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:315`.
- **Watch for:** an entity with a time-scale attribute follows it every step, so code that sets its clock's scale is
  overwritten; set one or the other.
- **Tests:** PlayMode's `TimeMagicTests` in Proving Grounds' `Tests/Stretch`.

### Moving an effect between entities, and possession (FEAT-23)

The possession scenario moves a body's abilities and effects to its possessor mid-cast. An effect could only be taken
off one entity and applied to another, which started its time over and ran its application twice; it moves now with
its stacks and time left, as a restore puts one back.

- **IDs:** FEAT-23, commit `21da7cb`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:860` `MoveTo`: each container's clock taken as a save takes it, its quiet
    leaving, and `Restore` on the other entity, by what applied it.
- **Watch for:** a move is neither a removal nor an application, so nothing listening for either hears it.
- **Tests:** PlayMode's `PossessionTests` in Proving Grounds' `Tests/Stretch`.

### Rollback: abilities kept through a load, and timers saved in full (GAS-60, GAS-61)

The rollback scenario saves a duel each tick, loads eight back and runs again, which must land where it did, to the
byte. It found two things a load lost: the ability another entity's effect came from, as a restore gave each ability
back as a new one, and the last digits of an effect's timers, which a save kept as floats.

- **IDs:** GAS-60, GAS-61, commit `2c8dff3`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:304`, in `Put`, the abilities a load keeps in their slots, and
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:304` `ClearForRestore`, which lets go of the rest.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:91` `CaptureClock` and `:99`
    `ExactDurationRemaining`, the timers in full, which `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSnapshot.cs:118` keeps as doubles.
- **Watch for:** JsonUtility can move a double's last digit as it reads one back, so a rollback keeps its saves in
  memory.
- **Tests:** the `GAS_60_*` and `GAS_61_*` tests in PlayMode's `SaveLoadTests`, and `RollbackTests` in Proving Grounds'
  `Tests/Stretch`.

### An area ability strikes every target (GAS-62)

The stress suite's area run casts an ability, every match within 10 m and then Apply Effects, among 200 entities. It
struck one. A targeting task set each target it selected as the primary, so each replaced the last, and every match
kept the farthest; then Target Real and the apply tasks took only the first target.

- **IDs:** GAS-62, commit `fff120f`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ProcessTaskUtil.cs:54` `SetTargetingPackets`, which files a task's targets in order, as
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/Authored/AutoTargetTask.cs:67` does.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:137` `ResolveTarget`, which files each one's
    entity under Target Real.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Gameplay/ApplyEffectsAbilityTask.cs:21` `Activate`, which reaches each target through
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/ProcessDataPacket.cs:288` `TryGetAt`, which builds no list.
- **Watch for:** a Target row and the Target Checks still check the primary alone.
- **Tests:** the `GAS_62_*` tests in PlayMode's `AbilityActivationTests`, and `ScaleTests` in Proving Grounds'
  `Tests/Stress`, the stress suite's scale half, whose allocation probe now ends the editor's Build Report REST
  service's retries, the bursts it couldn't explain.

### Ending a process costs the same however many there are (PC-34)

The stress suite's churn registers 10,000 processes and ends them. Ending the newest took six times what it did among
1,000, as a step group searched its list for the process from the front and shifted what came after: the review's
O(n) unregister.

- **IDs:** PC-34, commit `4a226e6`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:213` `StepGroup`, which keeps each process's position, and `:237` `Remove`, which leaves a hole there.
  - `:248` `Sweep`, which takes the holes out in place once they outnumber the processes, and `:581` `StepProcesses`,
    which passes over a hole as over any process no longer active.
- **Watch for:** a group's list can hold up to twice its processes between sweeps.
- **Tests:** `PC_34_Ending_a_process_costs_as_much_among_10000_as_among_1000` in PlayMode's `ProcessRegistrationTests`,
  and `ChurnTests` and `SoakTests` in Proving Grounds' `Tests/Stress`.

### A template's push searches the project once (ED-44)

The editor stress runs push a template to 2,000 children. It took 28 s: for each child, the push searched the project
for that child's own children and loaded every asset of its type again.

- **IDs:** ED-44, commit `024cf09`; Decision 30.
- **Read:**
  - `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePropagator.cs:216` `ChildrenByTemplate`, a type's assets by the template they name, in one search.
  - `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePush.cs:37` `PushToChildren`, which walks that family from `:62` `Push`, and
    `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePropagator.cs:99` `PropagateToAllChildren`, which does the same through `:112` `Cascade`.
- **Watch for:** a push still costs about 2.9 ms a child, the child's own propagation and its Undo record.
- **Tests:** `ED_44_A_push_searches_the_project_once_however_big_the_family` in EditMode's `TemplatePushTests`, and
  Proving Grounds' `Tests/Editor/EditorStressTests`: The Forge over 2,586 assets, the push to 2,000 children, and the
  Debugger over 200 entities.

### An ability made in code has a stage list (GAS-63)

The authoring round trip makes each kind of asset with The Forge's Create, edits every simple field of it through a
SerializedObject, and compares each save, undone and redone, to the byte: 252 fields, all of which came back. Filling an
ability's stage for it found that a new ability had no stage list until it was saved and loaded.

- **IDs:** GAS-63, commit `e91fe34`; Decision 30.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityBehaviour.cs:29` `Stages`, which starts empty, as a
  load leaves it.
- **Watch for:** a behaviour built with its list set to none still has none; the proxy's reads guard against it.
- **Tests:** `GAS_63_An_ability_made_in_code_has_a_stage_list` in PlayMode's `AbilityActivationTests`, and
  `AuthoringRoundTripTests` in Proving Grounds' `Tests/Editor`.

## Phase 4

Phase 4 redesigns the editor one surface at a time, and it starts where the UI interview's Q35 asks: with Undo and
multi-editing, one inspector at a time, then no polling, then the smoke suite that gates the phase, then one style
sheet and a kit of controls, then the redesign, one surface at a time. The plan's Editor and UI section lists the
phase's order. Each fix has a test in
Proving Grounds named after its ID, seen failing first. The phase closed on 2026-11-05: its gate,
the editor smoke suite in both themes, has passed since 2026-11-04.

### The Attribute inspector undoes every edit and edits a multiple selection (ED-12)

The Attribute inspector wrote its Name, Abbreviation and Description straight into the asset when a field lost focus,
and recorded no Undo. Like every PlayForge inspector, it also refused a multiple selection. Its fields now bind to
their properties, so an edit goes through the SerializedObject: Undo takes it back, it reaches every selected
attribute, and a selection that disagrees shows as mixed. The other asset inspectors still write straight into their
asset; ED-12's row lists what is left.

- **IDs:** ED-12, commit `e1d1dd0`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AttributeEditor.cs:139`, the three bound
    fields. The inspector turns on `SupportsMultipleObjects` at `:17` and carries `[CanEditMultipleObjects]` at `:9`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:259` `CreateBoundTextField`,
    which binds a text field and writes when the edit is committed. `SupportsMultipleObjects` (`:222`) lets
    `CreateInspectorGUI` take a multiple selection (`:346`), and `HeaderName` (`:252`) counts it in the header.
  - `:377`, where every inspector's header follows the asset's changes, Undo and Redo among them, through
    `TrackSerializedObjectValue`.
- **Watch for:** with several assets selected, the header leaves out Open in The Forge (`:431`), and its menu leaves
  out the template's items (`:461`), since they act on one asset.
- **Test:** `ED_12_An_edit_in_the_Attribute_inspector_undoes` and
  `ED_12_The_Attribute_inspector_edits_every_selected_attribute` in `InspectorUndoTests`. They build the whole
  inspector in a runtime panel, where its bindings run in batch mode.

### The MonoProcess inspector edits a multiple selection (ED-12)

The MonoProcess inspector bound every field already, but it lacked `[CanEditMultipleObjects]`, so the Inspector never
gave it a multiple selection and showed "Multi-object editing not supported" instead. It is the only MonoBehaviour
inspector, so it is also where a prefab instance's edits show as overrides.

- **IDs:** ED-12, commit `d0941e6`.
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/AbstractMonoProcessEditor.cs:10`.
- **Watch for:** a SerializedObject over several objects flagged DontSave writes only to the first of them, so the
  test uses plain scene objects.
- **Test:** `ED_12_The_MonoProcess_inspector_edits_every_selected_process` in `InspectorUndoTests`.

### Every asset inspector's own fields undo (ED-12)

The Effect, Item, Entity, Attribute Set and Ability inspectors wrote their fields straight into the asset, with no
Undo: Description everywhere, Link Mode everywhere, and the Entity's levels, Activation Policy and Allow Duplicate
toggles, and the Ability's Activation Policy and Activate Immediately. Each Level Source and Leveling section was also
drawn from the link mode once, so after an Undo it showed a mode the asset no longer had. The fields now bind, and a
section drawn from the asset's values redraws in the base's new `OnAssetChanged` hook, which runs after every change,
Undo and Redo among them. Renames already undid in one step, since Phase 2.

- **IDs:** ED-12, commits `2e75b13` (Effect), `2d211d4` (Item), `0cc8e9d` (Entity), `e111fac` (Attribute Set) and
  `62ed48e` (Ability).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:228` `OnAssetChanged`, a
    virtual no-op that the header's tracker at `:377` calls.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:539`, the Effect's override,
    which redraws Leveling when what it's drawn from has changed, and `:234`, the bound Link Mode field. The other four
    inspectors do the same.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:274`, Starting Level
    bound to its property, as the Entity's other fields are.
- **Watch for:** a multiple selection waited for the drawers' one-target copies. ScalerDrawer's, the last, went with
  ED-33's scaler field (`4cc9be9`).
- **Test:** `ED_12_An_edit_in_the_Effect_inspector_undoes` and the matching Item, Entity, Attribute Set and Ability
  tests in `InspectorUndoTests`.

### Link, Unlink and template assignment undo (ED-12)

Each inspector's Link and Unlink buttons, for the asset's own provider and for its children, the provider picker's
link, and the template picker's Assign and Unassign wrote into the assets with no Undo. Each now records every asset
it changes first, so one Undo takes it back.

- **IDs:** ED-12, commit `097384f`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:239` `RecordLinkChange`, and
    `ConfirmLinkChange` (`:232`), the hook the 13 confirmations ask through, which a test can answer.
  - `:906` `LinkTo` and `:314` `AssignTemplate`, the two pickers' steps.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:256`, Link Contained Effects, which the base editor's `CreateChildLinks`
    records with the contained effects before it links them.
- **Still open:** ED-31, the unlink dialogs that count with the editor rather than the asset, so they say 0.
- **Test:** `ED_12_Linking_in_the_Effect_inspector_undoes` in `InspectorUndoTests`.

### Opening a scaler writes nothing (ED-34)

Opening a scaler wrote to it while drawing. On an effect with no level link, Lock mode cut its per-level table to one
value, since the unlinked effect counts as its own level provider, with a Max Level of 1. The other modes resized the
table to Max Level, and a Max Level of 0 or less became 10. Building the drawer also cloned a scaler that another list
element shared. Now drawing writes nothing: a table keeps its length until the user changes it, and a level past its
end reads its last value, as `GetValueAtLevel` already did.

- **IDs:** ED-34, commit `7cc83c0`, Decision 28.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerFields.cs:232` `LevelTable`, which draws
    a table as it is, and `:310` `Resize`, which changes its length only when the user changes Levels. The three
    resizing writes were in the old drawer's `CreateContent`, which ED-33 rewrote.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerDrawer.cs:128` `FindSharedSibling`,
    which only reads, and the warning at `:70`, whose button calls `GiveOwnCopy` (`:164`) as one undoable step.
- **Test:** `ED_34_Opening_a_scaler_leaves_its_table_alone` and `ED_34_Opening_a_shared_scaler_leaves_it_shared` in
  `ScalerDrawerTests`.

### Each unlink question counts the children it would change (ED-31)

The Effect, Ability, Item and Entity inspectors' Unlink All questions counted children linked to `this`, the editor,
which no asset links to, so they always said 0. Each now counts as its asset's `UnlinkAllChildren` decides.

- **IDs:** ED-31, commit `523f57d`.
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:269` `Children`, an
  effect's contained effects, which `CreateChildLinks` counts by their link to the effect's provider, and
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:290` `Children`, an ability's
  cost and cooldown, of which the base editor's `CreateChildLinks` counts those linked to the ability.
- **Test:** `ED_31_The_unlink_question_counts_the_linked_children` in `InspectorUndoTests`, for all four.

### A rebuilt drawer edits every selected asset (ED-12)

Five drawers rebuilt themselves on a new SerializedObject over the first selected asset: the ability behaviour and
stage drawers, the worker group drawer, and the two tag requirement drawers. After a rebuild, such as opening one, an
edit reached that asset alone. The tag drawer did the same for every read and every pick. Each now rebuilds or reads
through its own SerializedObject, refreshed first.

- **IDs:** ED-12, commit `96b4ba6`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagRequirementDrawers.cs:90`, in the group drawer's
    `ScheduleRebuild`; the other four rebuilds read the same.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagDrawer.cs:111`, in `GetFreshProperty`.
- **Watch for:** ScalerDrawer rebuilt on a one-target copy until ED-33's scaler field (`4cc9be9`), which writes
  through the field's own SerializedObject.
- **Test:** `ED_12_A_rebuilt_drawer_edits_every_selected_asset` in `InspectorUndoTests`.

### A type field's drawer writes nothing while drawing (ED-12)

Five type drawers wrote their default type into an empty `[SerializeReference]` field as they drew, so opening an
asset changed it, and a field whose class Unity couldn't find lost the data Unity kept for it. The write is gone, and
so is `GetDefault`. Two runtime reads that threw on an empty slot, which a list's "+" leaves, now skip it, and an
Inject task gets its default from its field initializer.

- **IDs:** ED-12, commit `5134867`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Base/AbstractTypeRefDrawer.cs:67`, in
    `GetCurrentValue`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpec.cs:54`, an empty activation rule, and
    `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/AbstractScaler.cs:418`, an empty scaler
    behaviour.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/InjectAbilityTask.cs:11`, the initializer.
- **Test:** `ED_12_Drawing_an_empty_type_field_leaves_it_empty` and `ED_12_A_new_inject_task_interrupts` in
  `TypeRefDrawerTests`; `ED_12_An_empty_activation_rule_asks_nothing` in `AbilityActivationTests` and
  `ED_12_An_empty_scaler_behaviour_changes_nothing` in `AttributeValueTests`, on both hubs.

### The Item and Ability inspectors edit a multiple selection (ED-12)

Both refused a multiple selection. Now a field the selected assets disagree on shows as mixed and an edit reaches
every one. What acts on one asset steps aside: Name stays read-only, since each asset's tag comes from its own name;
the asset tag row hides; Level Source shows Link Mode alone; and Leveling edits the range only when every asset is
standalone. In the Ability inspector, targeting, the asset loaders, the stages and the activation rules wait for one
ability, since the stage drawers resize lists directly and could cut a longer one.

- **IDs:** ED-12, commits `0054b7d` (Item) and `1a203c1` (Ability).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/ItemEditor.cs:25`, where the Item inspector
    turns on `SupportsMultipleObjects`, and `:257`, Leveling's note for a multiple selection.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:456`, the note that stands
    in for the Ability's own lists.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:251` `IsMultiple`, which the
    inspectors check.
- **Then:** the Effect, Entity and Attribute Set inspectors took one too (`fc92e9d`), once ScalerDrawer's
  rework with ED-33 (`4cc9be9`) freed them.
- **Test:** `ED_12_The_Item_inspector_edits_every_selected_item` and
  `ED_12_The_Ability_inspector_edits_every_selected_ability` in `InspectorUndoTests`.

### The Impact and Effect drawers follow what they show (ED-11)

Phase 4's second step is no polling. The Impact drawer re-checked its operation and its effect's duration every
250 ms, and the Effect drawer rebuilt its link info every 500 ms, for as long as an inspector stayed open. Each now
follows the property it shows, through `TrackPropertyValue`. The Effect drawer also follows the effect its field
names, so a link made elsewhere shows. The timer re-read a stale copy of the operation, so an Undo never hid Multiply
Behaviour again; now it does.

- **IDs:** ED-11, commit `c5d5480`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectImpactDrawer.cs:143`,
    where Multiply Behaviour follows the operation; the drawer follows the asset whole since ED-2, which removed Reverse On Removal.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectDrawer.cs:53`, the
    `ReferencedEffectWatch` element that follows the referenced effect.
- **Test:** `ED_11_No_editor_code_repeats_a_timer` in `EditorSourceTests`, which scans the editor code, and
  `The_impact_drawer_follows_its_operation` in `EditorPollingTests`, a control.

### An inspector listens to the editor only while a picker is open (ED-11)

Every asset inspector added itself to `EditorApplication.update` as it opened, to catch an object picker's choice, and
ran every frame of its life. The two pickers, import and level provider, now add it as they open, and it takes itself
off when the picker closes.

- **IDs:** ED-11, commit `850f8fb`.
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:88` `WaitForPicker`,
  and `:95` `OnEditorUpdate`.
- **Test:** `ED_11_An_idle_inspector_listens_to_no_update` in `EditorPollingTests`.

### The inspector keeps no scroll view of its own (ED-11)

The header stayed put while the content scrolled under it. For that, each inspector hid the Inspector's own scroll
view and gave its content one of its own, sized by measuring the window on two loops that ran every 200 and 300 ms for
as long as the inspector was open. With the UI interview's Q17, the header first scrolled with the content in the
Inspector's own scroll view, so nothing was measured. The Forge's two Inspector delay settings, which those loops read,
are gone. Later the user asked for the header to stay at the top again (Decision 29), and ED-36 does that by the
Inspector's scroll events, still measuring nothing on a timer.

- **IDs:** ED-11, commit `88d0f3f`, Q17.
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:355`, in
  `CreateInspectorGUI`, where the header, a space and a plain content element go into the root.
- **Watch for:** `BasePlayForgeEditor.contentScrollView` and `ForgeDrawerStyles.CreateScrollView` are gone. An
  inspector builds into the container `BuildInspectorContent` is handed.
- **Test:** `ED_11_The_inspector_keeps_no_scroll_view_of_its_own` in `EditorPollingTests`.

### A drawer keeps its collapse state per asset (ED-18)

Eight drawers kept whether a part is collapsed in static dictionaries keyed by property path alone. So opening a part
on one asset opened it on every asset, a domain reload forgot it, and a moved worker took the state of the worker that
had been in its place. Deleting a stage cleared every stage's state, so they all opened. One helper now keeps it per
asset in SessionState, for the editor session and through domain reloads, with each drawer's own default. A
`[SerializeReference]` field keeps it with the object it holds, so it follows the object through its list, and
deleting a stage moves the later stages' states up.

- **IDs:** ED-18, commit `1ae0833`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/DrawerCollapseState.cs:48` `Key`, which names the
    asset, then the held object or the property path.
  - `:35` `RemoveElement`, which the stage drawer's `DeleteStage` calls at
    `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Ability/AbilityTaskBehaviourStageDrawer.cs:637`.
- **Watch for:** the inspector's own sections still share their state across every asset of a type, in EditorPrefs,
  by design (`BasePlayForgeEditor.CreateSection`).
- **Test:** the three tests in `DrawerCollapseTests`, and `ED_18_No_drawer_keeps_collapse_state_in_a_static_dictionary`
  in `EditorSourceTests`.

### The editor smoke suite, Phase 4's gate

Phase 4's third step, from the UI interview's Q36: `EditorSmokeTests` in Proving Grounds runs in each editor theme,
Dark and Light. Every inspector opens without an error, on new assets, on two selected together and on the Demos
sample's 82 data assets, and every window opens as the editor opens it. A sweep changes each value an inspector shows,
one at a time, and one Undo must put the asset back exactly as it was. It found nothing to fix. It changes no PlayForge
code, so the tour has no lines for it; the Proving Grounds README explains it.

- **Commit:** `4fd6a53` (the plan and the tour), with the suite in Proving Grounds.
- **Watch for:** the theme is switched through Unity's own skin setting, which saves it machine-wide, so each fixture
  puts the `UserSkin` key back as it found it.

### One style sheet and a kit of controls (ED-20)

Phase 4's fourth step, in the style `Docs/EDITOR_STYLE.md` records (Decision 20). Every color the style doc lists is
a variable in one style sheet, in a Dark and a Light set, and a kit of controls takes its look from the sheet alone:
the glyphs, the type icons, the icon buttons, the section and the asset header. Every asset inspector's header and
sections use the kit, so all six took the new look at once; their fields keep their own styles until each surface's
redesign. A section shows Import, Clear and Help at all times; they appeared only on hover (Q18). The Forge and the
GAS Debugger take the theme instead of painting their windows dark gray.

- **IDs:** ED-20, commit `f5832df`, Decision 20, Q13, Q18.
- **Read:**
  - `Assets/PlayForge/Editor/UI/PlayForge.uss:8`, the Dark set, and `:48`, the Light set. The kit's rules follow:
    the type icons' colors at `:123`, the asset header at `:160` and the section header at `:239`.
  - `Assets/PlayForge/Editor/UI/ForgeTheme.cs:35` `Apply`, which gives a root the sheet and the theme's class. The
    sheet loads by its GUID (`:15`), so it's found whether the package is in `Packages` or in `Assets`.
  - `Assets/PlayForge/Editor/UI/Kit/ForgeGlyph.cs:86` `Draw`, the mockups' vector glyphs drawn with `Painter2D` in
    the color the sheet gives, and `:351` `ForgeTypeIcon`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:411` `CreateEditorHeader`, and
    `:457` `ShowAssetMenu`, which holds import, the template and Visualize.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDrawerStyles.cs:518` `CreateCollapsibleSection`,
    which builds the kit's `ForgeSection` behind the same signature.
- **Watch for:** a control in the kit sets no style in C#; a test checks the header and section headers.
- **Test:** in `EditorSmokeTests`, run in both themes: `ED_20_An_inspector_takes_each_color_from_the_style_sheet`,
  `ED_20_The_header_shows_the_type_icon_in_its_color`,
  `ED_20_The_header_and_sections_take_their_look_from_the_style_sheet`, `ED_20_A_section_shows_its_actions_at_all_times` and `ED_20_The_Forge_and_the_GAS_Debugger_take_the_theme`; in
  `EditorSourceTests`, `ED_20_The_editor_has_one_style_sheet_and_no_unused_layouts`.

### The editor shows no emoji (ED-20)

The editor showed 102 emoji, as icons and inside its text, which an emoji font draws in color. Each is now a kit glyph
(Link, Warning, Search, Import, and the type icons where The Forge showed a type), a word on a button that was an icon
alone, or nothing where it only decorated a text. Each drawer that shows a glyph takes the theme on its own root, so
the glyph is sized and colored in Unity's own inspector too.

- **IDs:** ED-20, commit `6c0c3fe`.
- **Read:**
  - `Assets/PlayForge/Editor/UI/Kit/ForgeGlyph.cs:143`, the new Link glyph.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectDrawer.cs:90`, a warning
    icon as a glyph, as the other surfaces' are.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerDrawer.cs:28`, a drawer taking the theme.
- **Still open:** text symbols with no emoji form, such as a check mark or an arrow, go with each surface's redesign.
- **Test:** `ED_20_No_editor_text_shows_an_emoji` in `EditorSourceTests`.

### The asset header stays at the top (ED-36)

The user looked at ED-20 in the editor and asked for the asset header to stay at the top of the inspector while the
content scrolls under it (Decision 29), which reverses Q17's answer. The header now comes after the content, so it
draws over it, and a space of its height holds its place at the top. It follows the Inspector's scroll view by its
events and its own layout, and stops where its inspector ends. Before ED-11 it stayed put by hiding the Inspector's
scroll view and measuring the window on timers; nothing polls now.

- **IDs:** ED-36, commit `d851e30`, Decision 29.
- **Read:**
  - `Assets/PlayForge/Editor/UI/Kit/ForgeAssetHeader.cs:100` `StickTo`, and `:132` `Place`, which puts the header at
    the view's top, no higher than its inspector's top and no lower than its end.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:357`, the space, and `:371`,
    where the header goes in after the content.
- **Test:** `ED_36_The_asset_header_stays_at_the_top_as_the_content_scrolls` in `EditorPollingTests`.

### Step 5 begins: the Attribute inspector (ED-15)

The redesign's first surface. Its sections are Identity, Local Data and Used By. Used By lists the PlayForge assets
whose data refers to the attribute, each with its type icon, and a row selects its asset; it follows the project as it
changes (Q30). It replaces a section of sentences about where attributes can go, and a Find All References button that
opened a dialog telling the user how to search. A collapsed section shows one line, and a bound text field's label
takes the Inspector's one label column in every inspector.

- **IDs:** ED-15 (its first part), commit `ed6295a`.
- **Read:**
  - `Assets/PlayForge/Editor/UI/Kit/ForgeUsedBy.cs:38` `UsersOf`, and `:55` `Rebuild`, which draws the rows.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AttributeEditor.cs:82`, the sections in
    order; `:190` `BuildUsedBySection`; and `:217` `UpdateSummaries`, the line each collapsed section shows.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:266`, the label column.
- **Still open:** ED-15's Validate All, with The Forge's redesign.
- **Test:** `ED_15_The_Attribute_inspector_lists_what_uses_it` in `ForgeInspectorTests`.

### The Item inspector follows the style doc (ED-37)

The redesign's second surface. Its sections come in the style doc's order: Identity, Leveling, Tags, Effects,
Abilities, Workers and Local Data, then Used By. Identity holds the asset tag under the name, read-only, in the kit's
new `ForgeAssetTagField`. Leveling holds what Level Source did: Link Mode, then the item's own range, or its provider
in Unity's object field and the range the provider gives. The field's right-click menu picks one type of provider, and
a help box says when none is set. Each collapsed section shows one line, every section's Import and Clear work, and
nothing in the inspector sets a color, border or font in C#: Level Source's tinted, bordered boxes and the 10 px hints
are gone.

- **IDs:** ED-37 (new: how the asset inspectors stray from the style doc), commit `cf70e8f`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/ItemEditor.cs:93`, the sections in order;
    `:239` `RebuildLevelingContent`; and `:450` `UpdateSummaries`, the line each collapsed section shows.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:932`
    `CreateLevelProviderField`, which links through `LinkTo` (`:906`), so a link that would make a loop is refused.
  - `Assets/PlayForge/Editor/UI/Kit/ForgeAssetTagField.cs:35` `Show`, and the style sheet's new rules for value
    fields, rows of buttons, help boxes and notes (`Assets/PlayForge/Editor/UI/PlayForge.uss:303`).
- **Watch for:** the Entity, Attribute Set, Ability and Effect inspectors still draw Level Source with
  `BuildProviderSelector` and its Link to buttons, until their turn.
- **Tests:** `ED_37_The_Item_inspector_keeps_the_style_docs_sections`, `ED_37_The_Item_inspector_sets_no_look_in_code`
  and `ED_37_The_Item_inspector_lists_what_uses_it` in `ForgeInspectorTests`, and
  `ED_37_The_Level_Provider_field_links_the_item` in `InspectorUndoTests`.

### The Entity inspector follows the style doc (ED-37)

The redesign's third surface, after the Item inspector's pattern and the Entity mockup. Its sections are Identity,
Leveling, Tags, Attributes, Abilities, Items, Workers and Local Data, then Used By. The Attribute Set has an Attributes
section of its own; it sat in Definition. In Items, Equipped Too (Allow Duplicate Equipped Items) indents under Allow
Duplicates and waits for it, with a note beside it, since an inventory that holds one of each item has no second copy to
equip. A starting item takes one row: the item, then its Equip checkbox. The Leveling pieces the two inspectors share
moved into the base editor.

- **IDs:** ED-37, commit `a06a4fb`. New row ED-38 tracks the reference fields, which every inspector still draws
  with `AbstractRefDrawer`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:103`, the sections in
    order; `:234` `RebuildLevelingContent`; `:440`, Equipped Too, and `:462` `UpdateEquippedToo`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:645` `AddLinkedLevels` and
    `:687` `CreateChildLinks`, which the Item and Entity inspectors share.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Entity/StartingItemDrawer.cs:14`, the one-row
    starting item.
- **Still open:** the mockup's attribute table, with the Attribute Set inspector, and its "When Busy" label for
  Activation Policy, with the Ability inspector.
- **Tests:** `ED_37_The_Entity_inspector_keeps_the_style_docs_sections`,
  `ED_37_The_Entity_inspector_sets_no_look_in_code` and `ED_37_The_Entity_inspector_lists_what_uses_it` in
  `ForgeInspectorTests`; `ED_37_Equipped_Too_needs_Allow_Duplicates` and `ED_37_A_starting_item_takes_one_row` in
  `InspectorUndoTests`.

### The Attribute Set inspector follows the style doc, with the attribute table (ED-37)

The redesign's fourth surface. Its sections are Identity, Leveling, Attributes, Sub-Sets, Collision Settings, All
Attributes, Workers and Local Data, then Used By. All Attributes replaces a Summary section whose button listed the
attributes in a dialog. It is the Entity mockup's attribute table: each attribute the set gives, its sub-sets' included,
the value its Current starts at, and the range the set keeps it in. Where elements overlap, the table settles the value
by the runtime's rules (`AttributeSetMeta`), through the runtime's own helpers, and a value a scaler sets reads Scaled.
The Entity inspector shows the same table under its Attribute Set.

- **IDs:** ED-37, commit `86ead05`. New row ED-39 tracks the element drawer, which clamps magnitudes into the asset
  when it opens.
- **Read:**
  - `Assets/PlayForge/Editor/UI/Kit/ForgeAttributeTable.cs:43` `Show`; `:176` `RowOf`, the runtime's order of
    collision groups; and `:231` `RangeOf`, a range as in "0–max".
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AttributeSetEditor.cs:101`, the sections in
    order, and `:342` `BuildAllAttributesSection`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:356`, the table under
    the entity's Attribute Set.
- **Fixed since:** Use Minimum took the first element, in the runtime and so in the table, since
  `ForgeUtils.IndexMin` started from `float.MinValue`. GAS-55, the next entry, fixed it.
- **Tests:** `ED_37_The_Attribute_Set_inspector_keeps_the_style_docs_sections`,
  `ED_37_The_attribute_table_shows_what_an_entity_starts_with`, `ED_37_The_Attribute_Set_inspector_sets_no_look_in_code`
  and `ED_37_The_Attribute_Set_inspector_lists_what_uses_it` in `ForgeInspectorTests`.

### An empty set element gives nothing, and Use Minimum takes the lowest (GAS-54, GAS-55)

Two attribute set bugs the attribute table turned up. An element with no Attribute, as a list's "+" leaves one, threw
as the entity started, since the set's elements were filed in a dictionary by their attribute; the entity got none of
its attributes. It is passed by now, and the set's unique count leaves it out. And `ForgeUtils.IndexMin` started its
search from `float.MinValue`, so no value was ever below it, and Use Minimum always took the first element.

- **IDs:** GAS-54 and GAS-55, commit `c59f86a`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:374`, in
    `AttributeSetMeta.HandleAttributeSet`, and `:93`, in `GetUnique`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/ForgeUtils.cs:23` `IndexMin`.
- **Tests:** `GAS_54_An_element_with_no_attribute_gives_nothing` and `GAS_55_Use_Minimum_takes_the_lowest` in
  `AttributeValueTests`, on both hubs.

### Every asset reference is Unity's object field (ED-38)

Every reference to a PlayForge asset, in every inspector and drawer, is Unity's object field, with a plain button beside
it: Open, which shows the asset in a window of its own so the asset that holds the field stays in the Inspector, or New
while the field is empty. New makes the asset where The Forge's settings put its type, named for the holder and the
field, as in "Goblin Attribute Set", with the tag its name gives; an effect it makes takes its levels from the holder.
An effect field asks nothing when it's set. It asked in a dialog whether to link the effect, and again when the effect
was linked elsewhere. A line under it says how the effect takes its levels, with Link, and its template, a pick of the
effects nothing links, and New Linked Effect sit in its right-click menu. The fields were a button that opened a
dropdown list of their own, with hover colors and icon buttons set in C#.

- **IDs:** ED-38, commit `3cfa570`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Base/AbstractForgeObjectDrawer.cs:20`
    `CreatePropertyGUI`, the field and its button; `:68` `Open`; and `:74` `New`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetCreation.cs:27` `Save`, the folder, file name,
    name and tag of a new asset, and `:102` `AssetTagFor`, the tag rule the inspectors now share.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectDrawer.cs:70`
    `ShowLink`, the line under an effect field, and `:148` `AddMenuItems`.
- **Watch for:** `AbstractRefDrawer` still draws the type pickers, which choose a class, not an asset. (ED-56
  retired it: a kind field is `ForgeTypeField`, and its picker `ForgeTypePicker`.)
- **Tests:** `ED_38_An_asset_reference_is_Unity_s_object_field`, `ED_38_New_makes_an_asset_and_uses_it` and
  `ED_38_An_effect_reference_offers_its_link_in_place` in `InspectorUndoTests`.

### Opening an attribute set writes nothing (ED-39)

The attribute set element drawer wrote to the set as it drew: 50 ms after an element drew, and on every change to the
set, it clamped a Current or Base magnitude outside the element's bounds into the asset, so looking at a set marked it
modified. It writes nothing as it draws now. A help box says the magnitude is outside its bounds and what the runtime
clamps it to, and only a value typed into a Magnitude field is kept within the bounds, as part of the same edit.
Binding a field sends it change events of its own as it draws, so the drawer clamps only while the field has the focus.
Its look comes from the style sheet: a foldout row with the attribute and, while folded, one line such as "Current 150
· Base 100 · 0–100".

- **IDs:** ED-39, commit `22824ac`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Attribute/AttributeSetElementDrawer.cs:159`
    `KeepTypedInBounds`, the drawer's one write; `:178` `Show`, the line and the help box; and `:225` `IsTypedInto`.
- **Watch for:** the old drawer's 50 ms clamp doesn't land in batch mode, so the first test fails on it only once the
  set changes elsewhere.
- **Tests:** `ED_39_Opening_an_attribute_set_writes_nothing` and `ED_39_A_typed_magnitude_stays_in_bounds` in
  `InspectorUndoTests`.

### The Ability inspector follows the style doc (ED-37)

The redesign's fifth surface. Its sections are Identity, Leveling, Tags, Activation, Stages, Requirements, Workers and
Local Data, then Used By. Runtime Behaviour, which came before Tags, split in two: Activation holds When Busy, Activate
At Start, Cost, Cooldown and the targeting, and Stages holds the asset loaders and the stages. Requirements holds the
tag requirements, which sat in Tags, and the Caster and Target Checks, which sat in Validation Rules. When Busy is the
mockups' name for Activation Policy, in the Ability and Entity inspectors, and its choices show by what they do, from
`InspectorName`s on the two enums: Entity's Policy, Activate Anyway, Refuse and Queue. The mockup's Activate When
Granted reads Activate At Start, since the runtime tries it only for an entity's Starting Abilities, as its GAS starts.

- **IDs:** ED-37, commit `031c2d4`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:102`, the sections in order;
    `:330` `BuildActivationSection`; `:476` `BuildRequirementsSection`, whose Clear leaves the checks; and `:625`
    `ActivationSummary`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:605` `EnumName`, the name an
    enum field shows.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:1023`, the enums' shown names.
- **Watch for:** the tag requirements and the checks still draw in their drawers' own look. The requirements table the
  Effect inspector shares comes with the Effect step.
- **Tests:** `ED_37_The_Ability_inspector_keeps_the_style_docs_sections`,
  `ED_37_The_Ability_inspector_sets_no_look_in_code` and `ED_37_The_Ability_inspector_lists_what_uses_it` in
  `ForgeInspectorTests`.

### An effect makes more than one impact, each with its own delivery (ED-2, ED-3)

An effect made one change: the attribute, operation and magnitude its impact held, so a drain that took health and mana
took two effects. `GameplayEffectImpact.AdditionalImpacts` holds authored entries, each with its own attribute,
operation, magnitude and delivery, and `RebuildEntries` appends them, live, after the entry it derives from the
impact's fields; one with no attribute, or a delivery the duration can't make, is skipped. The runtime already applied
entries by delivery, so nothing else changed there. The new `AttributeImpactEntryDrawer` edits them, offering only the
deliveries the duration can make, with a help box when the one an entry names can't happen. The impact drawer says how
the first impact is delivered and how often a periodic one commits, from the runtime's own `BuildEffectSeries`, and no
longer draws Reverse On Removal. The Cost check reads each impact a cost makes; it read only the first.

- **IDs:** ED-2 and ED-3, commit `ccb4981`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:46` `RebuildEntries`,
    and `:169` `AdditionalImpacts`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/AttributeImpactEntryDrawer.cs:26`
    `CreatePropertyGUI`, with the Delivery popup at `:82`, and `:162` `Remove`, which lets go of the entries before it
    deletes one.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectImpactDrawer.cs:160`
    `ShowDelivery` and `:209` `AddImpact`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/ImpactFields.cs:28` `OperationField`,
    the mockup's one choice for the operation and the value it changes; `:97` `DeliveriesFor`; `:124` `CommitsText`.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/Validation/CommonValidations.cs:11` `CostValidation.Validate`.
- **Watch for:** the first impact is still derived and never serialized; only the additional ones are, which is the
  flip the impact model's design waited for (`CLAUDE.md`'s gotcha says so).
- **Tests:** `ED_3_An_effect_makes_each_of_its_impacts`, `ED_3_Each_impact_keeps_its_own_delivery` and
  `ED_3_A_cost_is_checked_for_each_attribute_it_spends` in `ImpactEntryTests`, on both hubs;
  `ED_2_The_impact_says_how_it_is_delivered`, `ED_3_An_additional_impact_draws_as_an_entry` and
  `ED_3_An_impact_is_added_and_removed_in_place` in `InspectorUndoTests`.

### The Effect inspector follows the style doc, with the requirements table (ED-37)

The sixth and last asset inspector. Its sections are Identity, Leveling, Tags, Duration, Impact, Requirements, Workers
and Local Data, then Used By. A standalone effect's Leveling says it takes the level of what applies it, and Link and
Unlink Contained Effects show only while it's linked, since with no provider Link would unlink them. The duration
drawer shows the fields its Policy uses, by the mockup's names. Requirements is the mockup's table, in the kit's new
`ForgeRequirementsTable`: a row for each group, with a Requires and a Must Not Have cell that list their tags, and a
cell that opens its list under the table. A two-button switch shows the target's table, then the source's; the Ability
inspector's tag requirements use the same table, with a Caster and a Target row.

- **IDs:** ED-37, commit `6090980`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:94`, the sections in order;
    `:227` `RebuildLevelingContent`; `:396` `BuildRequirementsSection`; `:610` `DurationSummary` and `:630`
    `ImpactSummary`.
  - `Assets/PlayForge/Editor/UI/Kit/ForgeRequirementsTable.cs:48`, the table, and `:142` `Toggle`, which opens a
    cell's list.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagRequirementDrawers.cs:23`, the effect's and
    the ability's tables.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectDurationDrawer.cs:90`
    `ShowPolicy`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:645` `AddLinkedLevels` and
    `:687` `CreateChildLinks`, which take what an asset with no levels of its own needs.
- **Still open:** the mockup's problems in place and the header's problem count, with the Problems view (ED-15); the
  units and "scaled by" beside Duration and Magnitude, with the scaler workflow (ED-33).
- **Tests:** `ED_37_The_Effect_inspector_keeps_the_style_docs_sections`,
  `ED_37_The_Effect_inspector_sets_no_look_in_code` and `ED_37_The_Effect_inspector_lists_what_uses_it` in
  `ForgeInspectorTests`; `ED_37_The_requirements_table_opens_a_cell_to_edit_it` and
  `ED_37_The_duration_shows_what_its_policy_uses` in `InspectorUndoTests`.

### One Import window (ED-32, ED-35)

Decision 28's window, from the canvas's Import board. Choose a source, templates first, then the assets of the type;
the right lists the fields that differ, each checked, beside their values now and the source's, and Import N Changes
imports the checked ones as one Undo step. A field is a value, a list or a reference, down to two levels of a class,
and a magnitude reads as its field ("Duration", "Duration scaler"). A list is replaced unless its row's Add is on. It
serves every section's Import, the asset menu's whole-asset import, and the worker group's and requirement group's
import buttons, in place of five windows. The whole-asset import copied each top-level field whole, so the name, asset
tag, template and level link came along (ED-35); it keeps them now. Clear resets at once, with no dialog.

- **IDs:** ED-35, and ED-32 but for the scaler's import; commit `c300449`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/ForgeImportWindow.cs:60` `ShowFor`, `:73`
    `ShowForAsset` and `:94` `KeepsOwn`, what a whole asset keeps; `:139` `Expand`, the rows a path gives; `:240`
    `Compare` and `:265` `Import`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDrawerStyles.cs:544`, a section's Import.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/SectionClearUtility.cs:22` `Clear`.
- **Watch for:** the windows log "No graphic device" as they open in batch mode, so their tests use the smoke suite's
  guard.
- **Tests:** `ED_32_The_import_window_lists_the_fields_that_differ`,
  `ED_32_A_list_can_take_the_sources_items_after_its_own` and
  `ED_35_Importing_a_whole_asset_leaves_its_identity_and_links` in `ImportWindowTests`.

### Scaler assets, and levels that map exactly (ED-33)

The Scaler Template is the Scaler asset (`ScalerAsset`; the file and its meta moved, so saved assets keep their
script), and its link code, which nothing called, is gone. The new `SharedScaler` kind holds an asset and reads its
scaler, so an edit to the asset reaches every field that uses it, and the field's own magnitude adjusts it. Levels map
exactly: level 4 reads the 4th value, and a level past the end the last. `StretchOverLevels` keeps the old spread over
a level range; it is on for scalers saved before it, since a missing field takes its initializer, and the drawer makes
new scalers with it off.

- **IDs:** ED-33 (part 1), commit `e44d1ed`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/SharedScaler.cs:33` `Evaluate`, with
    its depth guard.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/AbstractScaler.cs:68` `StretchOverLevels`, and
    `:403` `EvaluateFromSpec`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerFields.cs:81` `Make`, a new scaler made
    exact.
- **Still open:** the drawer's field menu, per-level table and preview, and the asset's own inspector, which part 2
  added (below).
- **Tests:** `ED_33_A_shared_scaler_reads_its_asset` and `ED_33_Levels_map_exactly_unless_the_scaler_stretches` in
  `ScalerTests`; `ED_33_A_new_scaler_maps_levels_exactly` in `ScalerDrawerTests`.

### The scaler field reads as the scaler board (ED-32, ED-33)

Decision 28's scaler board, in the inspector. A magnitude field shows its name, its kind (Constant, By Level, Shared or
another), the range it gives, as "6–8 s", and a field menu: Save as Scaler Asset, Use a Scaler Asset, Import Values,
Quick Fill and Make Constant. A Constant shows its number and its unit. A By Level field's values sit in a table, ten
levels to a row, beside a read-only curve, and Combine says how the field joins its number with its scaler. Import
Values compares a Scaler asset's or another field's values with the field's, level by level, before it copies them; it
replaces the scaler import window and the curve editor. A Scaler asset's own inspector lists the fields that share it,
and an attribute set's element shares one through the cached Shared kind. The scaler drawer writes through the field's
own SerializedObject, so it no longer rebuilds on a copy of the first selected asset.

- **IDs:** ED-33 (part 2) and the last of ED-32, commit `4cc9be9`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/MagnitudeField.cs:51` `Build`, the field's row,
    and `:266` `ShowMenu`; `:328` `ScalerActions`, the menu's public side, with `:338` `SetKind` and `:404`
    `SaveAsScalerAsset`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerFields.cs:70` `SetKind`, which gives each
    selected asset its own scaler; `:176` `Content`; and `:127` `KeepCurve`, which writes the curve a stretched scaler
    reads after an edit, never while drawing.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerWindows.cs:102` `ScalerValuesWindow`,
    with `:199` `CopyValues`, and `:379` `ScalerAssetEditor`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/CachedAttribute/Authored/SharedCachedScaler.cs:22`,
    the cached Shared kind's read, with its depth guard.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/AbstractScaler.cs:162` `CurveFor`, and
    `Assets/PlayForge/Runtime/PlayForge/Utils/ScalerOperationKeyword.cs:33` `Unit`.
- **Watch for:** binding sends change events of its own as it draws, so a value field writes the curve only when it
  was typed into (`IsTypedInto`).
- **Tests:** `ED_33_A_magnitude_field_reads_as_the_scaler_board`, `ED_33_A_fields_kind_changes_as_one_undo_step`,
  `ED_33_Save_as_Scaler_Asset_shares_the_fields_values` and `ED_33_Import_Values_copies_a_scalers_values` in
  `ScalerDrawerTests`; `ED_33_An_attribute_set_element_shares_a_scaler` in `ScalerTests`.

### The Effect, Entity and Attribute Set inspectors edit a multiple selection (ED-12)

ED-12's last part. The three inspectors refused a multiple selection; they take one now, as the Item and Ability
inspectors do. A field the assets disagree on shows as mixed, and an edit reaches every one. What acts on one asset
steps aside: Name, the asset tag, linking and Used By, the lists each asset keeps of its own (an effect's additional
impacts, workers and stack behaviours, a set's attributes), and the attribute tables. A property of a multiple
selection reads the first asset, and a write through it gives every asset the same value, so the scaler field and the
scaler drawer now read and write each selected asset through a SerializedObject of its own. A kind change gives each
field the kind from its own first value, and a field of that kind keeps its scaler; Levels and Make Constant keep each
field's own values. A kind, count, range, Combine, operation or delivery the assets disagree on shows as mixed.

- **IDs:** ED-12 (fixed), commit `fc92e9d`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/EachAsset.cs:17` `Of`, each selected asset's own copy of
    a property; `:35` `Differ` and `:42` `Write`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/MagnitudeField.cs:89`, the mixed kind, and
    `:338` `ScalerActions.SetKind`, a kind for each asset from its own first value.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerFields.cs:310` `Resize`, each table from
    its own last value, and `:127` `KeepCurve`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:27`, where the Effect inspector
    turns on `SupportsMultipleObjects`, and `:490`, the note in place of its workers.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectImpactDrawer.cs:160`
    `ShowDelivery`, a mixed delivery, and `:94`, the note in place of the additional impacts.
- **Watch for:** UI Toolkit sends a change event when a field that shows a mixed value is set to the value it holds,
  so a mixed kind takes even the first asset's kind; the kind callbacks skip a same-value change only when the kind
  isn't mixed.
- **Tests:** `ED_12_The_Effect_inspector_edits_every_selected_effect`,
  `ED_12_The_Entity_inspector_edits_every_selected_entity` and `ED_12_The_Attribute_Set_inspector_edits_every_selected_set`
  in `InspectorUndoTests`; `ED_12_A_magnitude_field_edits_every_selected_asset` and
  `ED_12_A_scaler_drawn_on_its_own_edits_every_selected_asset` in `ScalerDrawerTests`.

### Settings belong to the project (ED-21)

The first of The Forge's redesign, from the interview's Q21. The settings the project shares, where each type's new
assets go and how their files are named, each type's defaults and the Cost and Cooldown effects, live in
`ProjectSettings/PlayForgeSettings.asset`, a `ScriptableSingleton` that travels with the project, and Project Settings →
PlayForge edits them, bound, so an edit undoes. Each user's view toggles and the log level stay in EditorPrefs, on
Preferences → PlayForge. Every setting was an EditorPref, shared by every project on the machine; a project takes its
values from there once. The Forge's Settings tab and `TemplateRegistry` are gone.

- **IDs:** ED-21, commit `90a711b`.
- **Read:**
  - `Assets/PlayForge/Editor/Settings/PlayForgeSettings.cs:96` `Ready`, the first use's migration, `:279`
    `TakeMachineSettings`, and `:333` `Serialized`, which makes the singleton editable for the page.
  - `Assets/PlayForge/Editor/Settings/PlayForgeSettingsPages.cs:53` `BuildProjectPage` and `:220`
    `BuildPreferencesPage`.
  - `Assets/PlayForge/Editor/Settings/PlayForgePreferences.cs:68` `UseDefaults`.
- **Watch for:** a `ScriptableSingleton` saves only when told to, so the page saves after each edit and an Undo handler
  saves after each Undo.
- **Tests:** `ED_21_A_shared_setting_belongs_to_the_project`, `ED_21_A_project_takes_the_settings_it_kept_in_EditorPrefs`,
  `ED_21_The_Project_Settings_page_edits_the_shared_settings` and `ED_21_The_Preferences_page_edits_each_users_toggles` in
  `PlayForgeSettingsTests`.

### One validator, the Problems view and the header's count (ED-15)

From Q8: one validator feeds a Problems view and the badges. `ForgeValidator` runs each `ForgeValidationRule` it finds,
PlayForge's and a project's own, on every PlayForge asset, in the inspector's own words, so the view, the header's
count and the inspector agree. It follows the project with no polling, and `LogAll` writes the problems to the Console
in batch mode. Validate All checked four things and showed 15 problems in a dialog; the Audit wrote to the Console.

- **IDs:** ED-15, commit `4dffd79`.
- **Read:**
  - `Assets/PlayForge/Editor/Problems/ForgeValidator.cs:22`, the rules from `TypeCache`, `:41` `Check`, and `:136`
    `OnChanges`, which checks an edited asset again.
  - `Assets/PlayForge/Editor/Problems/ForgeRules.cs:118` `MagnitudeRule` and `:185` `AttributeSetRule`, which shares its
    sentences with the element drawer's help box.
  - `Assets/PlayForge/Editor/UI/Kit/ForgeAssetHeader.cs:59` `ShowProblems`, the header's count.
- **Tests:** `ED_15_The_validator_finds_what_the_inspectors_warn_about`, `ED_15_An_edit_checks_its_asset_again`,
  `ED_15_The_Problems_view_lists_each_problem_and_selects_its_asset` and `ED_15_An_inspectors_header_counts_its_problems`
  in `ProblemsTests`.

### The Forge is a list and a detail (ED-27, ED-9)

From Q5, Q6, Q7, Q24, Q29 and Q30. One window in place of four tabs and the Visualizer: a toolbar with Create, a filter
for each type, Tags, the search and the problem count; a virtualized list of the project's PlayForge assets beside a
detail pane that holds the asset's own inspector under a summary of its sections; and a status bar. A click selects a
row and shows its asset, where a typed table acted only on a double click (ED-27); the summary covers every type, where
the Visualizer was blank for items and sets, and the window keeps its selection across a reload (ED-9). It follows the
project with no Refresh. Create makes an asset through the one factory a field's New uses (Q20).

- **IDs:** ED-27 and ED-9, commit `59f5ae9`.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:184` `CreateGUI`, `:403` `Refill`,
    `:468` `ShowDetail` and `:503` `FillSummary`, which reads the inspector's `Sections`.
  - `:660` `Sorted`, the natural order (ED-7), and `:140` `QueueReload`, which follows the project on the editor's next
    update, as an import (`TheForgeImports`, `:698`), an asset made or destroyed, or `projectChanged` asks.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetCreation.cs:49` `CreateNew`.
- **Watch for:** the power scores stay hidden until items and entities score too (Q7); their code is in git history
  before `59f5ae9`.
- **Tests:** `ED_27_One_click_selects_a_row_and_shows_its_asset`, `ED_27_The_list_follows_the_project` and
  `ED_9_The_summary_shows_each_type_and_the_selection_is_kept` in `TheForgeTests`.

### Templates work as prefabs do (ED-13)

From Q23. A template's changes reach its children when it's pushed, with its asset menu's Push to Its Children, as
one Undo step; each child keeps the fields it overrides. An edit to a child's field records the override as it's made,
and the inspector marks it with Unity's prefab override bar; Revert to Template takes it back. Saving a template opened
a dialog mid-save, loaded every asset of its type and changed the children with no Undo.

- **IDs:** ED-13, commit `6733220`.
- **Read:**
  - `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePush.cs:37` `PushToChildren`, `:121`
    `MarkOverrides` and `:234` `OverridePathOf`.
  - `:197` `OnUndoRedo`, which takes a mark back with the Undo of the step it was made in, and makes it again on Redo.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:181` `ShowOverrides`.
- **Watch for:**
  - Unity hands over an edit's modifications as it flushes undo records, at the end of a frame, so a push flushes first,
    and a revert writes without a modification of its own.
  - The mark can't join the edit's own Undo record. Unity's Undo takes back what a `postprocessModifications` hook adds
    but never makes it again on Redo, and drops a record the hook starts. So the marks are remembered by Undo step, in
    `SessionState`.
- **Tests:** `ED_13_Saving_a_template_changes_no_child`, `ED_13_Push_to_Children_keeps_each_childs_overrides`,
  `ED_13_One_Undo_takes_back_an_edit_and_its_override` and `ED_13_A_childs_inspector_marks_its_overrides` in
  `TemplatePushTests`; and the smoke suite's `Every_edit_in_an_inspector_undoes`, which sweeps the Demo assets that
  are template children.

### One Runtime Debugger (ED-4, ED-5)

From Q4, Q28 and Q30. One window in place of the GAS Debugger, Process Control and the Tag Debugger: an Entities view
and a Processes view, each a list beside a detail. The hubs are ProcessControl's processes whose wrapper holds an
`IGameplayAbilitySystem`, so runtime hubs show too, and the lists follow ProcessControl's join and leave callbacks.
While the game plays, the window samples it at the rate the toolbar sets and changes values in place; in edit mode it
offers Play Mode. The GAS Debugger scanned the scene for MonoBehaviour hubs and rebuilt its details every 0.25 s, and
Process Control's usage was the time between a process's steps. ProcessControl now times its steps while asked.

- **IDs:** ED-4 and ED-5, commit `d220ff4`.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:202` `SampleNow`, `:224` `Follow`, which hooks the
    registry, and `:251` `ReadLists`, which finds the hubs among the processes.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:152` `ShowEntity`, which builds the detail once
    and hooks the shown entity's frame summaries alone (ED-26), and `:726` `CooldownOf`, the cooldown's time left.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Processes.cs:137` `SampleSteps` and `:219` `ShowProcess`, whose
    Pause and Resume take the processes it handles alike.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:52` `MeasureSteps`, and
    `:419` `Step`, which times a step while it's on.
- **Watch for:** `Live` (`RuntimeDebugger.cs:34`) is the tests' way in: they drive the window in edit mode, over a
  ProcessControl bootstrapped there. `.Every(` stays banned; the window samples on the editor's update, and only while
  the game plays.
- **Tests:** `ED_4_The_debugger_lists_each_hub_from_the_registry`, `ED_4_A_sample_changes_the_detail_in_place`,
  `ED_4_A_cooldown_shows_the_time_it_has_left`, `ED_4_In_edit_mode_the_debugger_offers_Play_Mode`,
  `ED_5_A_process_shows_how_long_its_steps_take`, `ED_5_A_running_process_pauses_and_resumes`,
  `ED_26_Closing_the_debugger_removes_its_hooks` and `GAS_S1_The_debugger_names_a_runtime_entity` in
  `RuntimeDebuggerTests`; `ED_5_A_process_times_its_steps_while_asked` in PlayMode's `ProcessUtilityTests`.

### The debugger acts, and loose tags (ED-40, FEAT-16)

From Q27: editing attributes and tags first, then applying effects and granting abilities. An attribute's current and
base fields take a value, which the entity takes as it is. A tag typed or picked from the project's is granted as a
loose tag, which its row removes. An effect picked lands from the entity itself, and an ability picked is granted at a
level, then activated or removed from its row. Loose tags are new in the runtime: a tag added straight to the tag cache
went at its next compile, so a tag could be granted only through an effect.

- **IDs:** ED-40 and FEAT-16, commit `5dd7332`.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:447` `SetAttribute`, a delta no worker sees, `:882`
    `GrantTag`, `:590` `ApplyEffect` and `:743` `ActivateAbility`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:330` the loose tags and `:334` `AddLooseTag`, which
    `CompileGrantedTags` (`:305`) includes, and `Assets/PlayForge/Runtime/PlayForge/GAS/Interfaces/IGameplayAbilitySystem.cs:36`.
- **Watch for:** a removed effect stays on the shelf until the entity's next tick, which clears it, so its row goes a
  sample later. A field being typed in keeps what's typed while samples come.
- **Tests:** `ED_40_An_attribute_takes_the_value_typed_into_the_debugger`,
  `ED_40_A_tag_added_in_the_debugger_lasts_until_its_removed`,
  `ED_40_An_effect_applied_in_the_debugger_lands_and_its_row_removes_it` and
  `ED_40_An_ability_granted_in_the_debugger_activates_and_goes` in `RuntimeDebuggerTests`;
  `FEAT_16_A_loose_tag_lasts_until_its_removed` in PlayMode's `TagTests`.

### The Debugger, as the boards draw it (ED-4, ED-5)

The window rebuilt from the design canvas's debugger boards (version 21), which the user approved: the Entities,
Processes and Tags boards and the board for outside Play mode. It is the Debugger now (`Window → PlayForge →
Debugger`). The toolbar has three views' toggles, the search, the view's filter, Follow Selection or ProcessControl's
state, and the frame; the rate is in the window menu, and a status bar counts the processes and entities. An entity's
attributes, effects, abilities and tags sit two by two under titled columns, with what changed or grants each; an
ability's state reads "Stage 2/3". The Processes list's columns sort, its Step is the time a step over the last second,
and its detail says whether a paused run is held. The Tags view is new: every tag held, as a tree, beside who holds
the one selected. ED-40's edits, which the boards don't draw, follow open decision 19's working assumption: a
double-click sets Base or Current, and a click selects a row whose table's buttons act on it.

- **IDs:** ED-4 and ED-5, commit `c934980`; open decision 19, which Decision 31 settles.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:331` `BuildToolbar` and `:497` `ShowStatus`; `:685`, the
    first-cell class, as USS has no `:first-child`.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:417` `ChangedBy` and `:714` `StateOf`, which
    reads `AbilitySpecContainer.ActiveStageIndex`.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Processes.cs:137` `SampleSteps` and `:190` `StepSecondsOf`, the
    last second's average; `:319` `RunOf`, from `ProcessControlBlock.RunIsHeld`.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Tags.cs:160` `TagRoots`, the tree, and `:339` `SourcesOf`, which
    walks a tag's sources as the tag compile does.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:61` `PeakStepSeconds`
    and `:121` `RunIsHeld`, now public.
- **Watch for:** the tests press rows and cells with `MouseDownEvent`s, and read a tree node through the window's own
  node type, which the tree's controller asks for by name.
- **Tests:** the updated `ED_4_*`, `ED_5_*` and `ED_40_*` tests in `RuntimeDebuggerTests`, and the new
  `ED_4_The_Tags_view_shows_each_tag_and_who_holds_it`, `ED_4_The_status_bar_counts_the_processes_and_entities`,
  `ED_4_Follow_Selection_shows_the_selected_hub` and `ED_5_The_Run_field_says_whether_a_paused_run_is_held`.

### The Debugger's foot rows (ED-40)

Decision 31, drawn on the canvas's Entities board (version 22): each section of the Entities view ends in a foot row
under a thin rule, so every control shows, as the style doc asks; a double-click alone set an attribute. Attributes:
the selected attribute's name, then Base and Current fields that set it on Enter, and a double-click on a value goes to
its field. Effects: Remove, a picker and Apply. Abilities: Activate and Interrupt, then a picker, Level and Grant.
Tags: Remove, for a loose tag only, a tag field with the project's tags under its arrow, and Add Tag. An action that
doesn't apply stays in place, disabled, and what one did is on the status bar's right, so nothing shifts. A section's
selected row takes Unity's unfocused selection grey, so the entity list keeps the blue.

- **IDs:** ED-40, commit `520c433`; Decision 31.
- **Read:**
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:255` `Foot`, `:295` `AttributesSection`, whose
    fields leave alone what's typed in them while samples come, and `:403` `GoesToField`, the double-click.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:753` `InterruptAbility` and `:900` `PickTag`, which
    fills the tag field for Add Tag to grant.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:565` `ShowAction`, the status bar's word on the last action.
- **Watch for:** `Docs/EDITOR_STYLE.md` has the unfocused grey now, as `--pf-selection-unfocused`.
- **Tests:** the four `ED_40_*` tests in `RuntimeDebuggerTests`, among them
  `ED_40_An_ability_granted_in_the_debugger_activates_and_is_interrupted`, whose ability waits in a `DelayTask` so it
  can be interrupted.

## Phase 3

Phase 3 began on `main` with the rows that writing the reference docs turned up, five runtime bugs and two settings
nothing read, then gave the two GAS hubs one core (GAS-S1), turned to cost (GAS-P3, GAS-P5, GAS-P4, GAS-P6, GAS-P8,
GAS-P9, GAS-P7, TS-5, TS-10), removed a stub nothing used (PC-24), made sequences and abilities end in the frame their
last task does (TS-Z), and kept activation synchronous while a loop of activations goes round once a frame (TS-8).
Measuring the phase's exit gate turned up two more: a runtime entity's abilities outlived it (GAS-52), and
interrupting every running ability at once threw, which TS-Z had exposed (GAS-53). The last two steady-state
allocations it found are gone too (GAS-P10, TS-11). Each has a test in Proving Grounds named after its ID, seen
failing first; the runtime tests run on both hubs. The plan's Log has the gate results. The phase closed on
2026-11-04 on its local gates, without the CI its exit gate asked for, since the repo has none (Decision 27, SP-24).

### An entity's affiliation is its own (GAS-47)

The Mono GAS wrote the affiliation in its registration data onto its Entity asset. Every entity that shares the asset
changed side with it, and in the editor the asset itself changed. Each hub now keeps its affiliation in a field of its
own: the registration data's, or else a copy of the asset's.

- **IDs:** GAS-47, commit `fdc1886`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:81`, in `GASCore.Initialize`. The field is at `:21`,
    `GetAffiliation` at `:292` reads it, and the tag compile at `:317` adds it. The fix went into each hub's copy;
    GAS-S1 later moved it into the core both share.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:44`, where the Mono hub reads
    the registration data's sides as it initializes, and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:115`, where a runtime GAS,
    built before it registers, takes them through `GASCore.SetAffiliation`. Two entities can share one runtime
    identity.
- **Test:** `GAS_47_A_registered_affiliation_stays_with_its_entity` in `EntityIdentityTests`.

### Unequipping takes back only the item's grant (GAS-48)

Unequipping removed the item's Active Ability wherever the entity held it. When the grant had failed, because the entity
already held that ability, the entity lost its own copy as the item came off. The item now notes the slot its grant
took, and takes back that slot only if the slot still holds its ability.

- **IDs:** GAS-48, commit `27c2325`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:199`, in
    `ItemSpecContainer.RemoveActiveAbility`. `ApplyActiveAbility` notes the slot at `:183`, in `grantedAbilityIndex`
    (`:18`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:333`
    `AbilitySystemComponent.RemoveAbilityAt`, which removes one slot. `RemoveAbility` (`:323`) finds a slot and calls
    it.
- **New:** `AbilitySystemComponent.RemoveAbilityAt(int, Ability)`.
- **Test:** `GAS_48_Unequipping_removes_only_the_ability_the_item_granted` in `ItemTests`.

### Opening abilities wait for the entity's attributes (GAS-49)

Both hubs tried each Starting Ability set to Activate Immediately as they gave it, before any attribute existed, so the
Cost rule refused every one with a cost. The hubs now give the abilities and build the attributes, items and tags, and
only then try the immediate ones.

- **IDs:** GAS-49, commit `999e779`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:225`
    `AbilitySystemComponent.ActivateImmediateAbilities`, taken out of `Initialize`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:102`, the call in `GASCore.Initialize`, after
    `CompileGrantedTags`. Each hub made it until GAS-S1 gave them one core.
- **New:** `AbilitySystemComponent.ActivateImmediateAbilities`.
- **Test:** `GAS_49_An_opening_ability_with_a_cost_activates` in `AbilityLifecycleTests`.

### Tick On Application alone lands its change (GAS-50)

A Durational or Infinite effect whose only tick is Tick On Application derives OnApply for Add and Flat Bonus. Only the
Instant path committed OnApply entries, so the change never landed. Applying such an effect now commits its OnApply
entries before its ticks run.

- **IDs:** GAS-50, commit `e40b3b6`.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:405`
  `EffectSystemComponent.ApplyTickOnApplication`. The Instant path, `ApplyInstantGameplayEffect`, is at `:221`.
- **Test:** `GAS_50_Tick_On_Application_alone_lands_its_change` in `EffectTickTests`.

### A set reached twice counts once (GAS-44)

An attribute set that included itself, directly or through another set, sent every walk over its sub-sets round
forever, and building the entity's attributes overflowed the stack. A set reached by two paths counted twice: its
combined elements were added twice, and its tag weighed 2. One walk now visits each set once, depth first, and
everything that walked the sub-sets uses it.

- **IDs:** GAS-44, commit `57ea4ff`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:63` `AttributeSet.SelfAndSubSets`,
    the walk, and `CollectSelfAndSubSets` (`:75`), which fills a caller's buffers. `GetUnique`, `GetGrantedTags`,
    `InitWorkers` and `RemoveWorkers` use it.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:364`, the `AttributeSetMeta`
    constructor, which builds the attributes from each set once.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/GrantedTagCompiler.cs:66` `GrantedTagCompiler.AddAttributeSet`. It walks
    into reused buffers (`:59`), so compiling tags still allocates nothing (GAS-P2).
- **New:** `AttributeSet.SelfAndSubSets`, `AttributeSet.CollectSelfAndSubSets`.
- **Tests:** `GAS_44_A_set_that_includes_itself_counts_once` and `GAS_44_A_set_reached_by_two_paths_counts_once` in
  `AttributeValueTests`.

### Two settings nothing read are gone (GAS-45)

An entity's Cap At Max and an item's Allow Duplicates were stored and shown, but nothing read them. The entity's level
always stays between Starting Level and Max Level, and the entity's Allow Duplicate Items decides whether an inventory
holds an item twice. Both fields are gone, with the runtime identity's copy, the two inspector toggles, and the values
the package's entity assets stored. Code that set either no longer compiles.

- **IDs:** GAS-45, commit `5ccaa88`. Removing them, not building them, was the user's call.
- **Read:** what decides instead.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:92`, in `GASCore.Initialize`, where the GAS registers its
    level clamped to its range.
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:58`, in
    `ItemSystemComponent.CanGiveItem`, where the entity's setting refuses a second copy.
- **Removed:** `EntityIdentity.CapAtMaxLevel`, `RuntimeEntityIdentity.CapAtMaxLevel`, `ItemDefinition.AllowDuplicates`.
- **Test:** `GAS_45_Settings_nothing_reads_are_gone` in `ForgeInspectorTests`, in EditMode.

### One core for both hubs (GAS-S1)

`GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` each kept a copy of the same code, about 400 lines:
building the entity, compiling its tags, the end of frame, death, level and Local Data. The copies drifted. A runtime
GAS applied no affiliation tags, and one made from an Entity asset lost the asset's Local Data. The GAS Debugger named
entities through `ToGASObject()`, which a runtime GAS answers with null, so every runtime entity showed as "unknown".
Both hubs now own a `GASCore` and forward to it. A hub keeps only how it is built, where its entity is, and how it goes
away.

- **IDs:** GAS-S1, commit `5e7f077`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:72` `GASCore.Initialize`, the one build order, then
    `EndOfFrame` (`:181`), `MarkDead` (`:264`) and `CompileGrantedTags` (`:305`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:39`
    `GameplayAbilitySystem.WhenInitialize`. The Mono hub builds its core as it registers, from a copy of its asset.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:67`, the constructor, where
    a runtime GAS builds its core before it registers.
  - How each goes away: `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.EndOfFrame.cs:44`
    ends at once, and `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem_EndOfFrame.cs:52`
    queues a Terminate. Both predate the refactor, and each hub's tests pass on its own.
  - `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:587`
    `RuntimeDebugger.NameOf`, which asks the entity's own name, as the GAS Debugger's `ResolveDisplayNameFor` did.
- **New:** `GASCore` (internal). `RuntimeEntityIdentity.LocalData`. Each hub gains what only the other had:
  `ApplyGameplayEffect(IEffectOrigin, GameplayEffect)` and `GetRelativeLevel()`.
- **Removed:** `GameplayAbilitySystem.Relays` and `HandleDestructionInternally`, which nothing used, and each hub's
  protected `localData`.
- **Tests:** `GAS_S1_Both_hubs_apply_the_affiliation_as_tags` and `GAS_S1_Both_hubs_keep_the_entity_s_local_data` in
  `EntityIdentityTests`, and `GAS_S1_The_debugger_names_a_runtime_entity` in `GASDebuggerTests`, in EditMode.

### An application costs the same however much the entity carries (GAS-P3)

Each application re-checked the requirements of every effect already on the entity and rebuilt all its tags, twice over
for an instant effect, and a re-application walked the whole shelf to find its own containers. An instant application
on an entity carrying 1,000 effects took 0.49 ms, 135 times one on an entity carrying 2. Applying no longer walks the
shelf, and takes about 0.001 ms either way.

- **IDs:** GAS-P3, commit `1242251`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:71` `EffectSystemComponent.Tick`. The
    requirements an application leaves wait for one pass here (`:83`), before the shelf ticks; the application only
    marks them due (`:141`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:290` `Shelve`, which counts a container's
    tags as it lands, and `Unshelve` (`:308`), which since GAS-P5 takes a whole cleaning pass off at once. They keep
    the index by asset tag (`:44`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:456`, in
    `TryHandleExistingDurationalGameplayEffect`, where a re-application finds its containers in the index.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:624` `HandleGameplayEffects`, which
    compiles the tags once (`:630`), not twice.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/GrantedTagCompiler.cs:45` `GrantedTagCompiler.AddEffect`, one effect's
    tags, counted the same way by a rebuild and by shelving.
- **Watch for:** a removal or ongoing requirement an application meets now takes effect at the next tick. The container
  leaves at that tick's clean, as it did before. A durational effect's own application workers see its tags.
- **Test:** `GAS_P3_An_application_costs_the_same_however_much_the_entity_carries` in `EffectCostTests`. It writes
  its timings to the results.

### Ending an effect costs the same however many end with it (GAS-P5)

The finished containers were a list. Marking one finished searched it, the shelf's tick and the requirement pass
searched it for every container, and clearing took each off the shelf and out of the index with a linear remove, so a
cleanse or a mass expiry of n effects cost n². Ending 3,000 at once cost 0.0057 ms each, five to nine times as much as
ending 50. Ending one now costs 0.0005 ms either way.

- **IDs:** GAS-P5, commit `7ce68aa`. Found fixing GAS-P3.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:616` `MarkFinished`, which adds to the set
    that mirrors the list (`:36`). The tick and the requirement pass skip a finished container through it (`:663`,
    `:637`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:719` `ClearFinishedEffects`, which takes the
    pass off together (`:732`) before any `OnRemove`, through `Unshelve` (`:308`): one walk of the shelf and one of
    the index.
- **Watch for:** effects ending in the same update leave the shelf together, so one's removal workers no longer find
  another that is ending with it.
- **Test:** `GAS_P5_Ending_an_effect_costs_the_same_however_many_end_with_it` in `EffectCostTests`. It writes its
  timings to the results.

### A tick, a recompute and a tag query allocate nothing (GAS-P4)

A tick's impacter lookups built a new list or set per call, and its contained effects were walked through LINQ: 528
bytes before anything committed. A recompute built a new list for pooled multipliers (136 bytes), a modifier refresh
removed entries through a lambda and built new ones (288), and a same-root tag query cut each root out as a new string
(60). Each is now 0. Every read of a built-in tag such as `Tags.DEFAULT` took two locks and two lookups of a
64-character name; each is now made once.

- **IDs:** GAS-P4, commit `b50a80b`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffectSpec.cs:192` `BuildEntryImpacters`, which
    indexes each delivery's attributes and impacters once (`IndexImpacter`, `:223`). `GetImpactersFor` (`:241`)
    returns the spec's own list, and `GetImpactedAttributes` (`:256`) a view that `foreach` walks without allocating
    (`Assets/PlayForge/Runtime/PlayForge/Utils/ReadOnlyListView.cs:10`).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:244`
    `GetContainedEffects`, now a `ContainedEffects` struct (`:248`) over the packets.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:211` `RemoveCarrier`, one
    pass that keeps order and keeps the entries spare (`:65`) for `RegisterModifier` (`:182`). The recompute clears
    its kept list (`:69`) at `:431`.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:274` `SharesAncestorWith`, which compares roots in place;
    `IsChildOf` (`:223`) compares ordinally, and `IsDirectChildOf` (`:240`) cuts no substring.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tags.cs:17`, the first of the 57 built-in tags, each made once.
- **Watch for:** `GetImpactersFor`'s list is the spec's own: read it, don't change it. Walk these lists by index or with
  a `foreach` over the concrete type, since a `foreach` through an interface boxes.
- **Tests:** `GAS_P4_A_tick_allocates_nothing_up_to_its_commit`, `GAS_P4_A_recompute_allocates_nothing` and
  `GAS_P4_Refreshing_a_modifier_allocates_nothing` in `EffectCostTests`, and
  `GAS_P4_A_same_root_query_allocates_nothing` in `TagTests`.

### A commit allocates nothing (GAS-P6)

A commit made a `ChangeValue`, and a list of frozen impacters with a constant impacter for each. `TrackedImpact` kept a
node for every impact, and the commit's sign check built a params array and summed it through LINQ. A frame in which an
effect ticked and committed allocated 328 bytes. A commit a worker adjusted, as armor does, also wrapped the new delta
in a carrier, and the worker's own sign check allocated. A frame now allocates nothing, with armor or without.

- **IDs:** GAS-P6, commit `e019b15`. Found fixing GAS-P4.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:281`, in
    `CommitWithCarrier`, where a commit rents what it works with, and `:409`, where it gives it back. `RentScratch`
    (`:434`) and `ReturnScratch` (`:441`) keep one set for each commit in progress.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:448` `CommitScratch`.
    `Freeze` (`:462`) evaluates each impacter once into a reused `FrozenImpacter` (`:516`), and `FreezeDelta` (`:482`)
    freezes an adjusted delta's Adds (`:369`). `Release` (`:503`) leaves a kept change to its worker's actions.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeChangeMomentHandler.cs:77`, where a deferred
    worker is handed the change, which is then marked kept.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/TrackedImpact.cs:14` `TrackedImpact.Add`, totals only.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeHelper.cs:53` `SignPolicy` for one value, and `:60` for two;
    `SignPolicyExtended` is at `:72` and `:79`.
- **Watch for:** a `ChangeValue` belongs to its commit. A callback or an inline worker reads it during the call, or
  copies its `Value`, and doesn't keep it. A deferred worker's actions may keep it.
- **Tests:** `GAS_P6_A_frame_in_which_an_effect_commits_allocates_nothing` and
  `GAS_P6_A_frame_in_which_armor_adjusts_a_commit_allocates_nothing` in `EffectCostTests`.

### A raw delta allocates nothing on its way through (GAS-P8)

A raw delta's commit wrapped its delta in a new `SourceAttributeImpact`, with a list and a constant impacter, and the
carrier made a new origin each time it was asked for one, three times a commit. A frame in which a raw delta landed,
its carrier kept, allocated 356 bytes; it now allocates nothing. `GenerateSimple` still makes a carrier on every call.

- **IDs:** GAS-P8, commit `a33a0bd`. Found fixing GAS-P6.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:314`, in
    `CommitWithCarrier`, where a carrier with no impacters has its delta frozen as Adds (`:318`), and a change with
    no carrier commits through the attribute's root (`:319`). Impacters handed to such a carrier still evaluate through
    `WrapDelta`'s wrapper (`:324`).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/IAttributeImpactCarrier.cs:169`
    `SourceAttributeImpact.GetOrigin`, which keeps the origin it makes (`:88`).
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/SourcedModifiedAttributeValue.cs:53`
    `GenerateSimple`, whose summary says how to keep a carrier.
- **Watch for:** a raw delta made every frame keeps its carrier, made once with
  `IAttributeImpactCarrier.GenerateSourceDerivation(source, attribute)`. `GenerateSimple` makes a new one per call.
- **Test:** `GAS_P8_A_frame_in_which_a_raw_delta_lands_allocates_nothing` in `RawDeltaTests`.

### Rescales, lifesteal and the action queue allocate nothing (GAS-P9)

Each rescale that keeps Current in proportion to Base, and each lifesteal heal, made a new carrier for the source, and
a new action when deferred. Taking any action off the queue walked a SortedDictionary, whose enumerator allocated. A
frame in which an attribute rescaled with its Base allocated 500 bytes, one in which lifesteal healed 164 inline and
532 deferred, and taking one action off the queue 104. Each is now 0.

- **IDs:** GAS-P9, commit `6860c24`, Decision 21. Split from GAS-P8.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/IAttributeImpactCarrier.cs:208` `SourceCarriers`, the
    table keyed weakly by source. `For` (`:224`) makes a carrier again only when its impact types or group change.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:621` `EnforceScaling`, which
    takes its carrier from one table for every attribute (`:649`) and its action from the attribute's spares (`:644`).
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Authored/RelativeOperationContextImpactWorker.cs:69`
    `TryWork`, the change both halves make, and `CreateDeferredActions` (`:51`), which hands its action back in a
    list it reuses.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/ActionQueue/BuiltinActions.cs:54` `ModifyAttributeAction.Rent`, and
    `:67` `Release`, which the queue calls once it has run the action
    (`Assets/PlayForge/Runtime/PlayForge/GAS/ActionQueue/ActionQueue.cs:157`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/ActionQueue/ActionQueue.cs:187` `DequeueNext`, which walks the buckets
    (`:16`) by index. `BucketFor` (`:82`) puts a new priority in its place, and `EnqueueRange` reads a list by index
    (`:105`).
- **Watch for:** a listener of `OnActionQueued` or `OnActionExecuted` reads an action during the call, since one the
  system made is used again. The lifesteal worker's `CreateDeferredActions` returns a list it reuses: queue what it
  returns at once.
- **Tests:** `GAS_P9_A_frame_in_which_an_attribute_rescales_with_its_Base_allocates_nothing` and
  `GAS_P9_A_frame_in_which_lifesteal_heals_allocates_nothing` in `EffectCostTests`, and
  `GAS_P9_Taking_an_action_off_the_queue_allocates_nothing` with its control,
  `Actions_run_by_priority_and_in_the_order_queued`, in `ActionQueueTests`.

### A tag lookup costs the same however long its name (GAS-P7)

A tag was hashed and compared by its name on every lookup, and a built-in tag's name is 64 characters. 10,000 lookups
in an entity's tag cache took 0.21 ms for a 3-character name and 7.1 ms for a 2,050-character one, and 58 ms when the
long tag came from an asset, whose name, a string of its own, was compared in full. Each tag now has an ID, given at
run time, and the same lookups take about 0.27 ms either way.

- **IDs:** GAS-P7, commit `3c99621`, Decision 22. Moved from GAS-P4.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:38`, the ID and the name it was given for, neither serialized. A tag
    takes its ID as it's made (`:47`), or as an asset holding it loads, in `OnAfterDeserialize` (`:336`).
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:317` `Id`, which takes the ID for the tag's name if it has none yet
    or its name was changed in place. `Equals` (`:309`) and `GetHashCode` (`:311`) use it.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:356` `TagIds`, one ID for each name, safe across threads. `Of`
    (`:369`) gives one the first time a name is seen.
- **Watch for:** two tags are equal exactly when their names are, as before. `Tag.GetHashCode` is no longer the name's
  hash and can differ between runs, so don't store it. `TagRegistry.Reset` leaves the IDs.
- **Tests:** `GAS_P7_A_tag_lookup_costs_the_same_however_long_its_name` in `TagTests`, for a tag made in code and one
  loaded with an asset, and its control, `A_tag_equals_another_of_its_name`.

### A repeating sequence keeps its path the same (TS-5)

Each part a run or a stage added to its packet's path stayed there, so a sequence that repeated added its stages'
parts every time round: its twentieth run had a 272-character path to its first's 25. Each part now comes off as what
added it ends, and every repeat finds the path as the first did. PF-007 had already taken out the LINQ and the string
joins the review found; the parts are now formatted once, and the LINQ left on an ability's activation is loops.

- **IDs:** TS-5, commit `2096552`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/ProcessDataPacket.cs:74` `TrimPath`, which takes
    the path back to a depth (`:67`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:236`, in `Run`, which takes
    its part off as it ends (`:371`) and starts each time round from it (`:244`). Its failure log reads the path where
    the run failed (`:331`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:541`, in `RunStage`, which
    notes where its iterations start (`:547`) and keeps the path where it failed (`:604`). `RestartStage` (`:691`)
    takes off what the last iteration added, for both kinds of stage repeat.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceStage.cs:176` `SequenceStage.PathPart`, and
    `:398` for a sequence, each formatted once for its name.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:72` `HasCriticalTask`,
    the loop that replaced the activation's LINQ.
- **Watch for:** the path is where a packet is now, not everywhere it has been. A run or a stage that has ended is off
  it, so read it during the run, or from the failure log.
- **Tests:** `TS_5_A_repeating_sequence_keeps_its_path_the_same` in `SequencePathTests`, and its control,
  `A_failed_run_logs_the_path_where_it_failed`.

### A stage that waits for all its tasks makes no second token source (TS-10)

Every stage made a token source for its policy, linked to its own, though only a policy that stops the tasks still
running needs one, as WhenAny and WhenN do. With the stage's own, a three-stage ability made about 14. A stage under
WhenAll or RepeatUntilSkipped now hands its policy the stage's own source, and the ability makes about 10. A frame that
runs one stage of one instant task allocated 1,912 bytes either way; under WhenAll it now allocates 1,464.

- **IDs:** TS-10, commit `a406552`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:703` `ExecuteStageInternal`,
    which hands the policy the stage's own source (`:739`) or makes one of its own (`:742`), and disposes only that
    (`:783`). `OnlyWaits` (`:789`) names the two built-in policies by exact type.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:565`, in `RunStage`, which passes
    its source down through `ExecuteStageWithRepeat` (`:640`).
- **Watch for:** a policy of your own still gets a source of its own, as it may cancel what it's handed. One that only
  waits gets no saving unless it's one of the two built in.
- **Test:** `TS_10_A_stage_that_waits_for_all_its_tasks_makes_no_second_token_source` in `SequenceCostTests`.

### The pooled instantiator stub is gone (PC-24)

`PooledMonoProcessInstantiator` was a stub: it gave back no process, and its `CleanProcess` did nothing, so a process
made through it kept its GameObject when it ended. Nothing used it. It's removed rather than built into a pool.

- **IDs:** PC-24, commit `70c2b77`, Decision 23.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/MonoWrapperProcess.cs:108`,
    in `WhenTerminate`, where a process's wrapper hands its object to an instantiator to clean, or destroys it itself.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Instantiator/AbstractMonoProcessInstantiator.cs:5`
    `AbstractMonoProcessInstantiator`, which a pool extends. Its `CleanProcess` (`:35`) owns the object from then on.
- **Watch for:** code that named the stub no longer compiles. A pool of your own extends the base class.
- **Test:** `PC_24_The_pooled_instantiator_stub_is_gone` in `ProcessControlEditModeTests`, in EditMode.

### Sequences and abilities end the frame their last task does (TS-Z)

Four `Task(Action)` stages took 5 frames, where four `Do` stages took 1: each yielded a frame before running its action.
A run waited a frame at its end for maintained stages it didn't have, and waiting on a process polled its relay once a
frame, so an instant ability ended 2 frames after it started, and a sequence nested by `RunSequenceTask` added 2 more.
Now each ends in the frame its last task does, and an instant ability in the frame it starts.

- **IDs:** TS-Z, commit `04c3db1`, Decision 24.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceCore.cs:119`
    `DelegateSequenceTask.ExecuteSyncActionSafe`, which runs the action at once.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:130` `WhenTerminated`,
    completed in `TryTerminate` (`:405`) once the process is unregistered, and
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:54`
    `ProcessRelay.WhenTerminated`, which the proxy waits on for targeting and execution
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:211`, `:322`) and
    `RunSequenceTask` for its sequence
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/RunSequenceTask.cs:82`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:282`, in `Run`, which waits only
    for maintained stages it has, and `:290`, where a repeating run that took no frame takes one.
- **Watch for:** an instant sequence can end inside `ProcessControl.Register`, and an instant ability inside
  `TryActivateAbility`, so their end callbacks can fire before those calls return. A waiter resumes as the process
  terminates, after it's unregistered.
- **Tests:** `TS_Z_A_task_made_from_an_action_runs_it_in_the_frame_its_stage_starts`,
  `TS_Z_An_instant_sequence_ends_in_the_frame_it_starts` and `TS_Z_An_instant_ability_ends_in_the_frame_it_starts`,
  flat and nested, in `SequenceTimingTests`. `An_instant_sequence_repeats_once_a_frame`, in `SequenceRepeatTests`, still
  holds.

### Activation stays synchronous, and a loop of activations goes round once a frame (TS-8)

`TryActivateAbility` runs an ability until one of its tasks waits: its targeting, then its first stage, so an instant
ability has ended inside the call. That stays, as Unreal's does. An ability asked for again from inside its own
activation nested in the call, so a loop of instant activations, such as an ability that asks for itself as it ends,
nested until the stack overflowed. Such a request now starts next frame.

- **IDs:** TS-8, commit `c45f4f1`, Decision 25.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:619` `TryActivateAbility`,
    which puts the request off (`:725`) while the ability's activation call still runs, and `ActivateNextFrame`
    (`:749`), which tries it again next frame.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:36` `IsActivating`, open from the
    claim to the end of `ActivateAbility` (`:94`).
- **Watch for:** `TryActivateAbility` returns true for a request it puts off. Next frame it's tried in full, and dropped
  if the ability's slot no longer holds it. A queued activation that its own ability's activation call releases waits
  a frame too.
- **Tests:** `TS_8_An_ability_asked_again_inside_its_own_activation_starts_next_frame`, from its end, its claim release
  and through another ability, in `SequenceTimingTests`, with its controls
  `An_ability_runs_inside_the_call_that_activates_it` and
  `An_ability_asked_for_outside_its_own_activation_starts_at_once`.

### A runtime entity's abilities end when it does (GAS-52)

A Mono process ends the processes it handles as it ends, and a runtime process didn't, so an ability running on a
runtime entity ran on after the entity ended, for as long as its stages lasted. An entity of either kind also took
activations once it had ended, and a destroyed Mono entity threw on one. Now a runtime process ends what it handles
too, and an ending entity's ability system shuts before its abilities end.

- **IDs:** GAS-52, commit `3102975`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/AbstractRuntimeProcess.cs:119`
    `WhenTerminate`, which ends what the process handles, as
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:117`
    does.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:232` `WhenTerminate`, which each hub's `WhenTerminate`
    calls before its base's, and `ShutDown` in
    `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:161`, with the check
    `TryActivateAbility` now makes first (`:705`).
- **Watch for:** any runtime process that handles others now ends them as it ends. A process registered with
  ProcessControl as its handler, as a group's members are, isn't one of them. A runtime GAS registered again after it
  ended keeps its ability system shut.
- **Test:** `GAS_52_Ending_an_entity_ends_its_abilities` in `AbilityLifecycleTests`, on both hubs, ended three ways, in
  targeting, a stage and a critical stage.

### Interrupting every running ability ends each (GAS-53)

`Inject` with a policy, `InjectAll` and turning `Enabled` off walked the live set of the abilities running under a
policy. TS-Z made an interrupted ability end inside the interrupt, where it takes itself off that set, so the walk
threw once the first had ended, and the rest ran on. It now walks a copy.

- **IDs:** GAS-53, commit `473ea8e`.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:881` `Inject`, which
  copies the set into a rented array (`:889`) before it interrupts anything. `InjectAll` (`:904`) and the private
  `ClearAbilityCache` (`:855`) go through it.
- **Watch for:** an ability that ends inside the interrupt fires `OnAbilityEnd` before `Inject` returns.
- **Test:** `GAS_53_Interrupting_every_running_ability_ends_each` in `AbilityLifecycleTests`, on both hubs and each
  way of interrupting.

### A frame whose summary has a listener allocates nothing (GAS-P10)

Each frame made a new snapshot for the `OnFrameComplete` listeners, copying the frame's ten lists: 504 bytes a frame for
an entity whose summary anyone listened to, 824 with an effect ticking. The listeners' snapshot is now the entity's
own, refilled each frame, so a listener reads it during the call.

- **IDs:** GAS-P10, commit `11a0f66`, Decision 26.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Workers/Core/FrameSummary.cs:347` `TakeSnapshot`, which refills the summary's
    one snapshot, and `FrameSummarySnapshot` (`:360`), whose lists it keeps from frame to frame (`Fill`, `:392`).
    `CreateSnapshot` (`:335`) still makes a copy to keep.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:218`, in `EndOfFrame`, which hands the listeners the
    refilled snapshot, only when there are some.
- **Watch for:** a listener that keeps the snapshot sees it change next frame. Copy what you need during the call.
- **Test:** `GAS_P10_A_frame_whose_summary_has_a_listener_allocates_nothing` in `EffectCostTests`, idle and with an
  effect ticking. `GAS_39_The_frame_snapshot_carries_effects_injections_and_deaths`, in `WorkerTests`, still holds.

### A task waiting in a delay allocates nothing a frame (TS-11)

A task asks whether its sequence is paused by looking its token up, and the dictionary's default comparer boxed the
token on every lookup under Unity's Mono, where a token isn't IEquatable: 24 bytes a frame for an ability waiting in a
`DelayTask` or a `ChannelAbilityTask`. The dictionary now compares tokens as tokens.

- **IDs:** TS-11, commit `ec008f7`.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:55` `RunsByToken`,
  made with `TokenComparer` (`:59`), which `IsPausedRun` (`:171`) looks tokens up in for
  `SequenceTaskLibrary.IsPaused`.
- **Test:** `TS_11_A_frame_in_which_an_ability_waits_in_a_delay_allocates_nothing` in `SequenceCostTests`, in both
  tasks.

### Left after Phase 3

GAS-51, item stacking that is declared but not built, moved to Phase 5 as FEAT-14, so an Item's `MaxStackSize` does
nothing until then. The phase's exit gate asked for zero steady-state allocation, enforced in CI. Every steady-state
frame its measuring covered allocates nothing, but the repo has no CI, so the phase closed on its local gates
(Decision 27), and CI is SP-24. The plan's Log has the measurement.

## ProcessControl

### Exception isolation

A throw from a process's own code no longer escapes into ProcessControl. It is logged with `Debug.LogException`, and
the process moves on as if the call had returned. A throwing step no longer stops the frame's other processes. A
throwing run still ends and applies its queued state, so nothing waiting on it hangs. A throwing `WhenTerminate` still
unregisters, and a throwing `WhenInitialize` refuses the process, so `Register` returns false.

- **IDs:** PC-1 (its comment says PF-011), PC-2, PC-8, PC-15.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:419`
    `ProcessControlBlock.Step`. A state queued during the step still applies after a throw.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:530`
    `ProcessControlBlock.RunProcessAsync`. The catch at `:552` ends a throwing run as a returning one ends.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:379`
    `ProcessControlBlock.TryTerminate`. `Unregister` runs whatever `WhenTerminate` did.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1044`, in
    `ProcessControl.PrepareCreatedProcess`. A throw in `WhenInitialize` force-terminates the half-made block.
- Watcher callbacks and state-change callbacks are guarded the same way (`ProcessControl.ReportStateChange`,
  `ProcessControl.CheckWatcherOnUnregister`).

### Queued state and a requested Terminate

A requested Terminate now reaches a running process. A queued state applies only when the run ends, and a Lazy run
waits for `WhenTerminate`, so a queued Terminate never completed. `Terminate` and terminate cascades now go through
`QueueTerminate`, which tells the process through the new virtual `WhenTerminateQueued`. Its default clears
`processActive`, so a run with nothing left to do ends, and one with work still finishes. A forced Terminate or Wait
no longer runs `WhenTerminate` or `WhenWait` a second time when the cancelled run unwinds.

- **IDs:** PC-5, PC-4. PC-17: a Synchronous process with no step timing, such as `GroupProcess`, terminates at once,
  since no run or step would ever apply the queued state.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:222`
    `ProcessControlBlock.QueueTerminate`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:574`, the end of
    `RunProcessAsync`. It applies the queued state only while the process is still Running (PC-4).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:249`
    `TaskSequenceProcess.WhenTerminateQueued`, which interrupts the sequence.
- **New:** `IGameplayProcess.WhenTerminateQueued`, with a default on `AbstractMonoProcess` and `AbstractRuntimeProcess`
  that clears `processActive`. `ProcessControlBlock.QueueTerminate`.

### Pause holds in place (Decision 5)

A pause takes effect at once: the process goes to Waiting and stops stepping. A process whose `TryHandlePause` returns
true keeps its run, and `TryHandleResume` picks it up. Otherwise the run is cancelled, and a new one starts on resume.
ProcessControl's own pause (Waiting, ClosedWaiting) holds what was Running and, when it ends, sends back exactly that,
so a process paused for its own reason stays paused.

Before, a control change gave every process its default transition. That parked Synchronous processes, started
RequiresControl ones, re-ran finished ones, and on closing ended GameRoot with everything it handled. A relay pause
waited for the run to end, replaced a pending Terminate, and restarted the run on resume.

- **IDs:** Decision 5. PC-7 (control changes), PC-11 (pause at once, held runs), PC-12 (a token source per run),
  PC-27 (no Unpause during ProcessControl's pause). TS-Y and TS-AB are the sequence side, under TaskSequence.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1074`
    `ProcessControl.SetAllProcessesUponStateChange`. Only a pause, or its end, moves a process.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:254`
    `ProcessControlBlock.ForceIntoState` holds or cancels the run. `HoldForControl` (`:273`) marks what ProcessControl
    stops, and `ReleaseFromControl` (`:300`) sends it back.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:326`
    `ProcessControlBlock.TryRun`. On resume a held run carries on. A run that finished during the pause, or one a
    pending Terminate was waiting for, moves on instead of running again.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1430`
    `ProcessControl.ValidateStateTransition`. A resume follows the transition table (PC-27), and a pending Terminate
    refuses a pause.
- **Rules worth knowing:**
  - A requested Terminate outranks a pause and survives ProcessControl's.
  - A process registered during ProcessControl's pause waits with the rest, unless it is RequiresControl:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:999`
    `ProcessControl.SetProcess`.
  - Pausing a process that ProcessControl already holds makes the pause its own:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:851` `ProcessControl.Wait`.
  - A run's token source is its own, so an old run still unwinding can't cancel the new one:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:534`, in
    `RunProcessAsync`.
- **New:** `ProcessControlBlock.HeldByControl`, `TerminateRequested`, `HoldForControl`, `WaitForControl`,
  `KeepPaused`, `ReleaseFromControl`; `ProcessControl.IsPaused`. `AbstractMonoProcess.TryHandlePause` and
  `TryHandleResume` are virtual now, as the runtime ones were. The contract is documented on
  `IGameplayProcess.TryHandlePause`:
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/IGameplayProcess.cs:48`.
- **Removed:** the private `GetDefaultStateWhenControlChanged`, a stateless guess at where each process should go.

### The hierarchy follows the clone and its transform (Decision 11)

The hierarchy files a Mono process under its live object, which for a prefab is the clone. It used the object handed
in, so every clone of a prefab shared one node, and one GAS's death cascaded to all of them. A process is also filed
again after `WhenInitialize` applies the parent its data asks for, and whenever its transform gets a new parent. So a
process spawned under another is its child for cascades, and one spawned at SceneRoot is GameRoot's child. Scene
processes now register with a default packet, not SceneRoot's, which reparented them, so the scene keeps its layout.

- **IDs:** PC-3, PC-28, PC-20, Decision 11.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1012`
    `ProcessControl.PrepareCreatedProcess`. It files the clone at `:1018` and files it again after initializing at
    `:1059`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:163`
    `AbstractMonoProcess.OnTransformParentChanged`, which calls `ProcessControl.ProcessReparented`
    (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1468`) and then
    `ProcessHierarchy.Reparent` (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/ProcessHierarchy.cs:147`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:204`
    `ProcessControl.CollectSceneProcesses`. The default packet is at `:238` (PC-20).
- **New:** `ProcessControl.ProcessReparented`, `ProcessHierarchy.Reparent`.
- **Watch for:** pausing or ending GameRoot now pauses or ends every process spawned at SceneRoot.

### A process ends with its object

A Mono process now ends however its object goes. `AbstractMonoProcess.OnDestroy` ends it, which also gives an object
with several processes one teardown; an external Destroy or a scene unload used to leave the block stepping nothing.
Unity sends no `OnDestroy` to an object that never woke, and none to the base class when a subclass declares its own,
so ProcessControl watches those blocks each Update and ends any whose object is gone. It warns once per class that
hides `OnDestroy`. Only a real prefab is cloned: a live object outside a scene, such as a `HideAndDontSave` one, is
used as it is. Outside play mode the object is destroyed with `DestroyImmediate`, since Unity refuses `Destroy` there.

- **IDs:** PC-10, PC-30, PC-31, PC-9, PC-32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:153`
    `AbstractMonoProcess.OnDestroy`, which calls `ProcessControl.EndDestroyedProcess`
    (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:925`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:529`
    `ProcessControl.Sweep`, run each Update over `_dormant` (`:151`) and `_unheard` (`:155`). `HidesOnDestroy` (`:553`)
    picks the list.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/MonoWrapperProcess.cs:57`
    `MonoWrapperProcess.IsPrefab` (PC-9), and `:99` `WhenTerminate` (PC-32; the object also goes whatever
    `WhenTerminate` threw, PC-8).
- **New:** `ProcessControl.EndDestroyedProcess`. To act on destruction, override `AbstractMonoProcess.WhenDestroy`
  rather than declaring `OnDestroy`.

### Bootstrapping and singletons

The Bootstrapper now makes ProcessControl in Ready, registers GameRoot, and only then enters the inspector's
StartState, so a StartState of Closed or ClosedWaiting no longer refuses GameRoot. A refused GameRoot is reported
instead of throwing halfway through the Bootstrapper. A duplicate GameRoot no longer takes `Instance` on its way out.
A ProcessControl that nothing bootstrapped steps nothing and says so once; it threw every frame. GameRoot survives
scene loads, behind an inspector toggle like ProcessControl's, since it is every default `Register` overload's handler.

- **IDs:** PC-29, PC-23, PC-26, PC-21.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/Bootstrapper/Bootstrapper.cs:47` `Bootstrapper.Bootstrap`. Ready
    at `:74`, the refusal at `:83`, StartState at `:96`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:107`
    `ProcessControl.Bootstrap(bool deferStartState)`, then `EnterStartState` (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:512`) and `Start` (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:157`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/GameRoot.cs:31` `GameRoot.Bootstrap`, and its
    `DontDestroyOnLoad` field at `:24`.
- **New:** `ProcessControl.Bootstrap(bool)`, `ProcessControl.EnterStartState`, the `GameRoot.DontDestroyOnLoad` field.
  Both singletons clear `Instance` at `SubsystemRegistration`, and the Bootstrapper tests them with Unity's null, so a
  destroyed one is replaced. See `ClearInstance` at
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:16` and
  `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/GameRoot.cs:21`.
- `[SerializeReference]` came off eleven fields that hold Unity objects, the Bootstrapper's `RegisterSets` among
  them. Unity stores those as plain references either way, so no data moves.

### Smaller fixes

- **PC-6.** The runtime `Register` overloads without a packet passed null, then threw after spending a cache index.
  An empty packet stands in, which also unblocked `RunProcessTask`.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRegistration.cs:128`, in
  `ProcessRegistration.Run`, since FEAT-15's builder took the overloads' work.
- **PC-13.** A state change is reported before ProcessControl acts on it, with the real old state, while watchers are
  still attached. `OnProcessUnregistered` fires before the removal.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1209`
  `ProcessControl.ProcessWillChangeState`, and `:779` in `Unregister`.
- **PC-14.** The first watcher gets `onStartWatching`, a second subscription is written back into its struct, and a
  callback can stop watching. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:332`
  `ProcessControl.WatchProcess`.
- **PC-16.** A data packet lets go of a process that ends.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:760`, in `Unregister`.
- **PC-17.** `GroupProcess.DetachMember` removes only the group's own watcher.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/GroupProcess.cs:176`.
- **PC-18.** One runtime process instance can't be registered twice at once, and one that has ended can be registered
  again. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/AbstractRuntimeProcess.cs:29`
  `IsInitialized`, cleared by `MarkEnded` from
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/RuntimeWrapperProcess.cs:54`
  `WhenTerminate`.
- **PC-19.** `ActionProcess` steps at Update, so its action runs. `ActionProcess<T>`'s long constructor now takes a
  `T source`. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/ActionProcess.cs:38`.
- **PC-22.** Processes step in priority order, lowest first, then by arrival. The priority was stored and never used.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:213` `StepGroup`.
- **PC-25.** `ProcessTaskUtil.DoWhile` ends; it measured backwards and looped forever.
  `Assets/PlayForge/Runtime/PlayForge/Utils/ProcessTaskUtil.cs:129`. `ProcessRelay.RemainingRuntime` converts before
  truncating: `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:67`.
- **PC-26.** A block leaves its step groups by the timing it joined under, so changing a running process's timing no
  longer strands it. The hierarchy skips a root whose object is gone.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1146` `RemoveFromStepping`.

## TaskSequence

### Each run has its own runtime

A `TaskSequence` held one runtime slot and kept run state (fired conditions, broken repeats, repeat counts) on the
shared definition. A second run left the first out of reach, and runs broke each other's repeats. Each run now has a
`TaskSequenceRuntime` of its own. A `TaskSequenceProcess` reaches only its own run, and the sequence-level calls
(`Inject`, `Interrupt`, `SkipCurrentStage`, `CheckConditions`) reach every run. Each run also keeps its own clock, so
`ElapsedTime` no longer reads 0 in a sequence without a timer.

- **IDs:** TS-I, TS-W.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequence.cs:24` `TaskSequence._runs`, then
    `CreateRuntime` (`:60`) and `Run(TaskSequenceRuntime, ProcessDataPacket, CancellationToken)` (`:76`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:90`, the per-run record of
    fired conditions, broken repeats and repeat counts, read through `IsRepeatBroken` (`:1150`) and `GetRepeatCount`
    (`:1153`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:1005`
    `TaskSequenceRuntime.RunClock` (TS-W).
- **New:** `TaskSequence.CreateRuntime`, the `Run` overload that takes a runtime, `TaskSequenceRuntime.IsRepeatBroken`
  and `GetRepeatCount`.
- **Removed:** `SequenceStage.RepeatBroken`, `SequenceStage.RepeatCount`, `SequenceStage.ResetConditions`. A chain
  keeps its own condition state, as a chain is a single run.

### One core for every stage

Every stage, whether top-level, a sub-stage or the branch a `BranchStage` chose, now runs through `RunStage`. So a
sub-stage has its callbacks, timers, repeat, error handler, conditions and critical section; it used to run its tasks
and nothing else. A stage's own timer or condition acts on that stage, not on the current top-level one. Maintained
stages are tracked by index, and each cleans its tasks as it ends; moving on used to clean them while they still ran.
A run that ends early waits for them. The active stages are a list, not a stack, because a maintained stage ends under
the stages after it. The stop-maintained requests choose among maintained stages only.

- **IDs:** TS-N, TS-K, TS-J, TS-L.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:523`
    `TaskSequenceRuntime.RunStage`, and `ExecuteSubStage` (`:509`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:1324`
    `RequestSkipCurrentStage`, which acts on `_injectionSource` (`:130`), the stage applying the injection. Then
    `RequestSkipAndMaintain` (`:1330`) and the stop-maintained requests from `:1342`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:395`
    `TaskSequenceRuntime.StopMaintainedStages`.

### Cancellation and cleanup

A cancelled stage now waits for its tasks to stop before `CleanStage` runs. Leaving the moment the token cancelled
cleaned tasks still unwinding. So a task that ignores its token holds its sequence until it ends, though
`TerminateImmediate` still ends the process at once. A WhenAny or WhenN stage that is stopped from outside ends instead
of hanging. A throwing `OnTerminate` or critical-exit listener can't skip the rest of cleanup, the claim release among
it. Stage and sequence token sources are cancelled as well as disposed, so their timers can't fire into the next round
of a repeating sequence.

- **IDs:** TS-AA, TS-A, TS-E, TS-AD.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:403`
    `TaskSequenceRuntime.ExecuteStagesSequentially`. The wait on the stage signal is at `:447` (TS-AA).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceCore.cs:268`
    `WhenAnyStagePolicy.AwaitCompletion` (the fix at `:303`), and `:332` `WhenNStagePolicy.AwaitCompletion` (`:379`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:606`, the `finally` of
    `RunStage` (TS-E, TS-AD), and `:1475` `Cleanup` (TS-AD).

### Repeats

Each iteration of a repeating stage is cleaned and prepared again, and an iteration that took no frame waits one. A
task that resets in `Prepare`, such as `SyncDelay`, finished at once from the second iteration, and a stage that
finished without yielding repeated forever inside one frame.

- **IDs:** TS-M.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:640`
  `TaskSequenceRuntime.ExecuteStageWithRepeat`, and `:688` `RestartStage`.
- **Watch for:** tasks in a repeating stage get `Prepare` and `Clean` on every iteration, not once.

### Pause inside a sequence (Decision 5)

`TaskSequenceProcess.TryHandlePause` returned `processActive`, so a paused sequence said it had paused and kept going.
It now holds its run. No stage starts or ends, and timeouts, max durations and built-in delays stop counting.
Conditions aren't checked either, since a Waiting process isn't stepped. A task's own code can't be stopped from
outside, so it holds only if it keeps time through `SequenceTaskLibrary` with the token it was handed, or watches
`data.IsPaused`. A static map from each stage's token to its run lets the library answer from the token alone, so its
helpers kept their signatures. `ChannelAbilityTask` yielded twice a loop, so a channel lasted twice its duration.

- **IDs:** TS-Y, TS-AB, Decision 5.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:150`
    `TaskSequenceRuntime.Pause`, then `IsPausedRun` (`:171`), `Delay` (`:180`) and `HoldWhilePaused` (`:191`). The
    token map is at `:55`; `ExecuteStageInternal` adds each stage's token at `:747` and holds a finished stage at
    `:774`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:264`
    `TaskSequenceProcess.TryHandlePause`, and `TryHandleResume` (`:274`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceTaskLibrary.cs:847`
    `SequenceTaskLibrary.IsPaused`, then `DeltaTime` (`:856`) and `Delay` (`:865`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/ChannelAbilityTask.cs:23`
    `ChannelAbilityTask.Activate` (TS-AB).
- **New:** `TaskSequenceRuntime.Pause`, `Resume`, `IsPaused`, `IsPausedRun`, `Delay`; `SequenceTaskLibrary.IsPaused`,
  `DeltaTime`, `Delay`; `SequenceDataPacket.IsPaused`; `TaskSequenceChain.Pause` and `Resume`, which hold a chain
  between sequences.
- **Limit:** `SequenceTaskLibrary.ArcForce` can't hold, as physics moves the body whatever the sequence does.

### Nesting and chains

A run nested on the same packet gives the packet back as it found it; it used to clear the packet's runtime, which left
the outer run with none. A chain does the same with `Data.Chain`. Cancelling an ability ends a `RunSequenceTask`'s
inner sequence through its relay, since an interrupt can be refused by a critical section and can't reach a run that
hasn't started. A chain repeats in a loop, not by calling `Run` again from inside itself, and its `IsCriticalSection`
between two sequences is false rather than a throw.

- **IDs:** TS-F, TS-G, TS-O.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:219`
    `TaskSequenceRuntime.Run`. The outer runtime is kept at `:232` and given back at `:370`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/RunSequenceTask.cs:32`
    `RunSequenceTask.Activate`. The relay ends the inner sequence at `:90`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequence.cs:365` `TaskSequenceChain.Run`, and
    `IsCriticalSection` at `:516`.

### Sync sequences and step timings

A sync sequence keeps one packet, made in `WhenInitialize`. It made a new one every step, and swapped a plain packet
for an empty one, so what the packet carried never reached the tasks. It can be interrupted: its runner stops, and its
next step ends the process. Ended part way, it cleans the stage it's in. `BuildSyncRunner` has one implementation now.
A sync stage runs its callbacks, WhenN, RepeatUntilSkipped and skip timeouts, and what a runner can't do (sub-stages,
branches, conditions, sequence timers, async-only tasks) is named in a warning when it is built. Sequences step at
FixedUpdate and LateUpdate as well as Update. OnStep callbacks fire at their timing, and a composite timing moves a sync
sequence on at one timing only.

- **IDs:** TS-P, TS-Q, TS-H.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:151`
    `TaskSequenceProcess.StepAt` (TS-H), with `AdvancingTiming` at `:345`. `WhenInitialize` (`:132`) and `Interrupt`
    (`:322`) are TS-P.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceStage.cs:480`
    `TaskSequenceDefinition.BuildSyncRunner` (TS-Q). `TaskSequenceBuilder.BuildSyncRunner` now calls it.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Synced/SyncSequenceCore.cs:240`
    `SyncedTaskSequence.Interrupt`, which also stops a runner that hasn't stepped yet.
- **New:** `SyncedTaskSequence.BreakStageRepeat`, which `SequenceDataPacket.BreakStageRepeat` falls back to.

### Abilities: activation and targeting outcome

An ability can't activate while ProcessControl takes no new processes. If ProcessControl refuses its targeting or
execution sequence later, the ability ends; it used to fall back to running outside ProcessControl, where no pause or
terminate could reach it. Failed or interrupted targeting stops the ability, unless the targeting task is set not to
break on an invalid target. So does targeting that didn't reach its end for any reason. A non-cancellation throw in
targeting is logged and cleans the activation; it used to leak the handle.

- **IDs:** TS-AC, TS-C, TS-V.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:407`
    `AbilitySystemComponent.CanActivateAbility` (TS-AC at `:423`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:161`
    `AbilityProxy.ActivateTargetingTask` (TS-C at `:179`, TS-V at `:186`), then `ActivateTargetingViaSequence`
    (`:195`) and `ActivateViaSequence` (`:295`) for TS-AC.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:113`
    `AbilitySpecContainer.AwaitAbility`. The catch at `:125` is TS-V.
- **New:** `AbilityDataPacket.TargetingFinished`, set by the targeting adapter
  (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilitySequenceCompiler.cs:124`).
  `ProcessControl.AcceptsNewProcesses`.

### Abilities: stage hooks and ability events

A compiled ability stage fires `AbilityStageActivated` from the stage's `OnInit` and `AbilityStageEnded` from its
`OnTerminate`. It used to add a task that finished at once, which satisfied every Any stage, so such a stage ended as it
began. And `ApplyUsageAtStageStart`, on by default, fired `AbilityStageEnded` at the start and never at the end; now
only the usage effects follow that setting. `OnAbilityEnd` fires when the activation ends. It fired when the claim was
released, which for an ability with no critical stage is as it starts. The release has its own event,
`OnAbilityClaimReleased`. Compiled abilities fire the task callbacks too; only the legacy path did.

- **IDs:** TS-B, TS-D, TS-9.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilitySequenceCompiler.cs:216`
    `AbilitySequenceCompiler.WireStageCallbacks`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemCallbacks.cs:39` `OnAbilityEnd`, and `:50`
    `OnAbilityClaimReleased`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityTaskAdapter.cs:43`
    `AbilityTaskAdapter.Execute`.
- **New:** `AbilitySystemCallbacks.OnAbilityClaimReleased`. **Removed:** the compiler's private `StageCallbackTask`.
- **Watch for:** `AbilityStageEnded` moves to the stage's end, and `OnAbilityEnd` to the activation's end.

### Abilities: per-activation state and reuse

A task object is shared by every activation of its ability, so state for one activation now lives on the packet.
`RunProcessTask` kept its relays on itself, and the next activation replaced them, which left the first one's processes
running for good. A packet copies the request's payload lists instead of sharing them. From Phase 0: a proxy is
compiled once and reused while idle (it was compiled on every activation), a broken ability is logged rather than
swallowed by a bare `catch`, and resuming a sequence no longer sets `Time.timeScale` to 1.

- **IDs:** TS-U, TS-X, TS-3 (PF-012), TS-4 (PF-008), TS-1 (PF-001).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:49`
    `AbilityDataPacket.GetTaskState<T>`, used by `RunProcessTask.Prepare`
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Process/Authored/RunProcessTask.cs:21`). TS-X is in the
    constructor, at `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:72`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:276`
    `AbilitySpecContainer.TakeProxy`, with `AbilityProxy.IsIdle`
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:109`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:274`
    `TaskSequenceProcess.TryHandleResume` (TS-1).
- **New:** `AbilityDataPacket.GetTaskState<T>`, `AbilitySpecContainer.CompiledProxies` (a diagnostic count).

## GAS core

### Effects and the impact pipeline

#### Commit order inside the delivery model (Decision 4)

The OnApply, Continuous and Periodic delivery model predates the refactor; `01258b8` has it. Continuous entries are
modifiers, re-derived on every recompute and reversed by unregistering. OnApply and Periodic entries are commits, kept
in per-group history. What changed is where commits land. A commit was measured against the live value, after the
modifiers, but stored in the baseline, under them, so the modifiers scaled it again: under a ×1.5 aura a 30 hit took
45, and a +10 tome was worth 15. Committed history now comes after Add, Multiply and FlatBonus, and before Override, so
a hit takes what it says and an override still pins the value.

- **IDs:** GAS-15, Decision 4. Golden S6 now expects 85, not 90.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:398`
    `CachedAttributeValue.Recompute`. Committed history is at `:485`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:84`
    `GameplayEffectImpact.ResolveDelivery`, unchanged: how each authored shape maps to a delivery.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:221`
    `EffectSystemComponent.ApplyInstantGameplayEffect` (OnApply commits) and `:432`
    `ApplyDurationGameplayEffectAsInstant` (Periodic commits).
- `Assets/PlayForge/Documentation/EFFECT_PIPELINE.md` section 2.1 has the new order, and `IMPACT_MODEL_REFACTOR.md`
  has a dated amendment.
- **Fixed in Phase 3:** GAS-50. A Durational or Infinite effect with Tick On Application but no periodic ticks
  derives OnApply for Add and Flat Bonus, and only the Instant path committed OnApply. See
  [Phase 3](#phase-3).

#### Pre-change workers and raw deltas (Decision 9)

A commit now evaluates its impacters once, freezes them as constants, and previews the incoming delta. Pre-change
workers and the pre-change callback see that delta and can change what is committed, so armor acts on incoming damage
and the clamp applies after. A raw delta grows Base before Current and shrinks it after, so the clamp no longer eats
the Current part. Under Decision 9, a raw delta (a change handed to the GAS without an effect) reaches attribute and
impact workers with no impact type: a worker that takes any type acts on it, and one that wants a type doesn't. The
system's changes to itself, such as each attribute's startup pulse, stay `DisallowImpact` and unworkable.

- **IDs:** GAS-2, GAS-12 (PF-005), GAS-25, Decision 9. Golden S15 expects 42.5 for its raw −30: the callback halves it,
  then armor halves that.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:274`
    `AttributeSystemComponent.CommitWithCarrier`. Phase 0 (freeze and preview) runs from `:285`, using
    `CachedAttributeValue.PreviewCommit`
    (`Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:307`). It froze through
    `ConstantImpacter.Snapshot`; since GAS-P6 it freezes the same way in impacters it reuses.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/IAttributeImpactCarrier.cs:131`
    `SourceAttributeImpact.FromAdditiveDelta` (GAS-12). Since GAS-P8 a raw delta's commit orders its Adds the same
    way in `CommitScratch.FreezeDelta`
    (`Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:482`).
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/SourcedModifiedAttributeValue.cs:53`
    `GenerateSimple`, and `:60` `GenerateUnworkable`, which the startup pulse uses
    (`Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:111`).
- **New:** `SourcedModifiedAttributeValue.GenerateUnworkable`.

#### Rescaling with AutoScaleWithBase

`EnforceScaling` takes its share from Current before the bounds, where commits land, so the bounds clamp once. Taken
from the clamped Current, an 80/100 attribute under ZeroToBase that lost half its Base ended at 50/50, not 40/50. The
rescale is typed `DisallowImpact`, as the startup pulse is, so a worker that checks impact types passes it by. Typed
`AllowImpact`, it passed every type filter, and a worker that takes unworkable impacts acted on it as if whoever
changed Base had dealt it.

- **IDs:** GAS-37, GAS-35, Decision 9.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:621`
  `CachedAttributeValue.EnforceScaling`, and the `unconstrained` value it reads (`:35`). The
  `AcceptUnworkableImpact` tooltip says what it covers:
  `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Core/AbstractImpactWorker.cs:19`.

#### Stacking

Stacks with independent durations work. The container threw on construction, and its stack packets were structs, so
ticks, expiry and refresh changed copies; they are classes now, and the effect lasts as long as its longest stack.
Re-application adds the configured stack amount, not always one, and an amount left at 0 still means one. A bound
returns how many stacks the container can still take, and configs run by `ConfigPriority`, lowest first, so a bound has
the last word. A stack that runs out, or one that `Extend` adds to independent durations, re-registers the effect's
Continuous entries, so a per-stack magnitude updates at once rather than at the next unrelated recompute.

- **IDs:** GAS-4, GAS-5, GAS-34.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/Stacking/IndependentDurationsContainer.cs:19`
    `Packets`, and the `StackableContainerPacket` class at `:54`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:274`
    `GameplayEffectDuration.GetStackAmount(AbstractStackingEffectContainer, out StackBoundsConfig)`, and
    `ConfigsByPriority` (`:310`). The
    bound is `StackBoundsConfig.GetStacks`
    (`Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/Settings/AbstractEffectConfig.cs:280`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:652` `EffectSystemComponent.TickEffectShelf`
    (a lost stack at `:711`) and `:454` `TryHandleExistingDurationalGameplayEffect` (`Extend` at `:550`).

#### Durations and ticks

Duration, tick and stack-amount scalers are initialized before they are read, so an attribute-backed duration is no
longer 0. An infinite periodic effect reads its tick interval through its scaler. A durational periodic effect always
lands its last tick. Its period and duration timers counted down apart, so at some fixed frame rates the duration ended
first: 5 ticks in 5 s landed 4 at 60, 120 and 144 fps. Timers are doubles behind the float API, and a tick within 0.1%
of a period on the duration's last frame counts.

- **IDs:** GAS-22, GAS-24, GAS-33.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:106`
    `GameplayEffectDuration.ApplyDurationSpecifications` (GAS-22), and `:157` `ApplyInfiniteSpecs` (GAS-24).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:79`
    `AbstractEffectContainer.TickIsDue`, shared by every container type.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/NonStackingEffectContainer.cs:39`
    `NonStackingEffectContainer.TickPeriodic`.

#### Tag requirements

A removal requirement now means "remove when met". An empty group asks for nothing, and either side, target or source,
is enough. The check was inverted and AND-ed, so an effect left while its requirement wasn't met, and application
demanded that the removal group pass. Ongoing requirements are checked in the pass that follows an application, at the
next tick since GAS-P3, and again when a tick is due, since tags change without any effect being applied.

- **IDs:** GAS-6, GAS-10 (PF-002).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/EffectTagRequirements.cs:63`
    `EffectTagRequirements.CheckRemovalRequirements`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:189`
    `GameplayEffect.ValidateRemovalRequirements`, and `ValidateApplicationRequirements` (`:178`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:624`
    `EffectSystemComponent.HandleGameplayEffects`, and the re-check in `TickEffectShelf` at `:683`.

#### Contained effects, records and the cycle guard

OnRemove contained effects land on the target, as OnApply and OnTick ones do; they went to the source. An effect
applied again inside its own application is refused, with one warning per entity and effect; one that contained itself
recursed until the stack overflowed. `RecordEffectApplied` and `RecordEffectRemoved` are called where the callbacks
fire; nothing called them. An instant effect's removal workers get every impact it made, not just the last. A shelved
effect grants its own asset tag; it granted its source's, so a target carried the tag of everything that had hit it.
The `GameplayEffectImpact` copy constructor keeps the affiliation and accepts an impact with no packets.

- **IDs:** GAS-8, GAS-28, GAS-14, ED-14.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:94`
    `EffectSystemComponent.ApplyGameplayEffect`. The guard is at `:112`, the record at `:147`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:124`
    `AbstractEffectContainer.OnRemove` (GAS-8), and `GetGrantedTags` (`:160`, GAS-14).
  - `Assets/PlayForge/Runtime/PlayForge/Workers/Core/WorkerContext.cs:110` `EffectWorkerContext.Impacts`, filled by
    `ApplyInstantGameplayEffect` (`Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:234`).
- **New:** `EffectWorkerContext.Impacts`.
- **Watch for:** an effect can no longer apply itself inside its own application.

### Attributes

#### Combine and UseAverage (Decision 13)

Elements set to Combine add up. Each resolves on its own, scalers included, and the blueprint's value is the sum;
bounds, rounding and retention group come from the first. `AttributeBlueprint.Combine` did nothing, so the first
element stood alone, and the loop that called it took the first in itself and each other element twice. Every combined
part's scalers are regulated, so a part scaled from another attribute refreshes with it. A set's `UseAverage` averages
the resolved values; it took the first. `Ignore` keeps the first; it threw.

- **IDs:** GAS-27 (part 4), Decision 13, GAS-40.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeBlueprint.cs:85`
    `AttributeBlueprint.Combine`, `Average` (`:95`), and `GetActiveValue` (`:59`), which sums the parts.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:388`
    `AttributeSetMeta.InitializeAttributeSystem` (Combine at `:400`) and `InitializeAggregatePolicy` (`:422`, GAS-40).
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:133`
    `AttributeSystemComponent.ProvideAttribute`, which regulates every part (`:143`).
- **New:** `AttributeBlueprint.Parts`, `AttributeBlueprint.Average`.
- **Fixed in Phase 3:** GAS-44, an attribute set that included itself recursed without end. See
  [Phase 3](#phase-3).

#### Derived refreshes (Decision 14)

A derived attribute's refresh fired `OnAttributeChanged` alone: no post-change worker saw it, AutoScaleWithBase never
followed a derived Base, and the frame summary never heard of it. It now takes the post-change half of the pipeline
(`EnforceScaling`, post-change workers, `AttributePostChange`) and is recorded as received and dealt, self to self.
It's carried by the attribute's root, typed `DisallowImpact`, so a worker that checks impact types passes it by.

- **IDs:** GAS-27 (part 5), Decision 14.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:566`
  `AttributeSystemComponent.RefreshRegulatedAttributes`. The new half starts at `:587`.

#### Random scalers keep their roll (Decision 12)

`RandomizedScaler` rolls once per effect spec and keeps the roll with what the spec captures, so a buff holds its roll
and every tick of one application deals the same. It rolled on every evaluation, and a modifier is evaluated on every
recompute, so a random buff re-rolled whenever its attribute changed. `RollOncePerSpec`, on by default, switches back.

- **IDs:** GAS-27 (part 1), Decision 12.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/RandomizedScaler.cs:28`
  `RandomizedScaler.Initialize`, and `Roll` (`:40`).
- **New:** `RandomizedScaler.RollOncePerSpec`.

### Abilities and targeting

#### Target resolution (GAS-46)

Targeting files a targeting packet under `Target`, or under the behaviour's implicit tag. Everything that reads a target
(Apply Effects, Apply/Remove Effects, Check Range, the after-targeting checks, a process spawned with the ability's
data) asks `Target Real` for the entity, and nothing wrote `Target Real`. So an ability authored the documented way
applied nothing; the tests set `Target Real` by hand, which hid it. When targeting ends, the activation now files the
entity that the packet points at under `Target Real`, unless something is there already. A Runtime GAS's own packet now
points back at it, as a Mono GAS's does.

- **IDs:** GAS-46, the newest runtime fix, found while writing ED-16's docs.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:137`
    `AbilityDataPacket.ResolveTarget`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:137`, in
    `AbilitySpecContainer.AwaitAbility`, followed by `ValidateAllActivationRequirements` at `:139`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:82`, in the constructor the
    others call.
- **New:** `AbilityDataPacket.ResolveTarget(Tag)`.
- **Watch for:** target activation rules and target tag requirements now find a target, so they run and can refuse an
  activation.

#### Claims and the active list

A claim now goes to the container's own slot, under the policy the activation asked for. It went to the first slot
that held the same ability, and under the definition's policy, where `EndActivation` never looked, so the active list
drifted from what was running. Overlapping activations share the slot until the last one under that policy ends.
Removing an ability takes it off the active list before tags compile, and still lets whatever was queued behind it go.

- **IDs:** GAS-21, TS-T, GAS-20.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:912`
    `AbilitySystemComponent.ClaimActive`, then `ReleaseClaim` (`:931`) and `EndActivation` (`:947`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:323`
    `AbilitySystemComponent.RemoveAbility`.
- **New:** `AbilitySpecContainer.IsActiveUnder(EAbilityActivationPolicy)`.

#### Activation checks

The default Cost rule checks what the cost leaves, `Current + change >= 0`. It compared the value with the signed
magnitude, so every cost passed and an effect that adds to the attribute was refused. An ability's source tag
requirements ("can't use while stunned") are enforced; they were authored and never read.

- **IDs:** GAS-7, GAS-11 (PF-003).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/Validation/CommonValidations.cs:11` `CostValidation.Validate`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpec.cs:36`
    `AbilitySpec.ValidateSourceActivationRequirements`.
- **Watch for:** costs now block activation.
- **Fixed in Phase 3:** GAS-49. Both hubs gave Starting Abilities before building attributes, so the Cost rule
  refused an Activate Immediately ability that had a cost. See [Phase 3](#phase-3).

#### Targeting for a runtime GAS

Targeting and range checks read targeting packets, which both hubs have, not GameObjects, which a runtime GAS lacks.
Validating a runtime target threw, and a range check with a runtime GAS at either end always failed. The four built-in
targeting tasks measure from the caster's targeting packet. A runtime GAS's position is that packet's, which whoever
drives the entity keeps current, and a physics query still finds Mono targets only. `AutoTargetTask` takes every
collider in range; its buffer was `MaxTargets` long, so 0 (every match) found nothing, and at 1 the caster's own
collider could fill it.

- **IDs:** GAS-29, GAS-38, GAS-43.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/AbstractTargetingAbilityTask.cs:70`
    `AbstractTargetingAbilityTask.TargetIsValid(ITarget)`.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/Validation/CommonValidations.cs:134` `CheckRangeValidation.Validate`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/Authored/AutoTargetTask.cs:47`
    `AutoTargetTask.Activate`, and `GatherCandidates` (`:76`). `AutoTargetByAffinityTask`,
    `SelectDirectionTargetTask` and `SelectGasTargetInRangeTask` have the same GAS-38 change.

#### Disjoint and homing shots (Decision 10)

A disjoint now re-aims each homing shot at a `DisjointTarget` placeholder where its target stood, which takes nothing.
It re-aimed the shot at the entity it was already aimed at, so the shot hit anyway. A shot withdraws its targeted intent
when it lands, ends, or is re-aimed, and a hub records an intent once, so a target's list no longer only grows. Under
Decision 10 a disjoint stops homing shots only, as in Dota; a skill shot registers no intent and lands as usual. That
part is documentation only. A targeted process with no `Target Real` warns and ends its run; it threw.

- **IDs:** GAS-26, GAS-36, Decision 10, GAS-31.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Authored/CompositeBehaviours/ProjectileDisjoint/Core/DisjointProxyTaskBehaviour.cs:30`
    `DisjointProxyTaskBehaviour.RunAsync`. The class summary at `:9` states Decision 10.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/Authored/AbstractTargetedMonoProcess.cs:34`
    `AbstractTargetedMonoProcess.RunProcess`, `SetTarget` (`:58`), and `WhenInitialize` (`:18`, GAS-31).
- **New:** `ITarget.WithdrawTargetedIntent`, a default no-op that both hubs implement
  (`Assets/PlayForge/Runtime/PlayForge/GAS/Interfaces/ITarget.cs:11`).

### Items

Items can be given and taken off cleanly. `ItemSystemComponent` never created its shelf, so `GiveItem` threw (Phase 0).
`ClearAllItems` counted up while removing, so it skipped every other item and dropped those with their effects still
on. Unequipping removed the effect by asset, from every source; it now removes only what this item applied.
`ItemSpec.IsActive` was always false, which dropped every action an item's effect queued. An item with no Active
Ability, which is any passive item, threw on equip with its effects already on. Tags compiled before `IsEquipped`
cleared, so they outlived the item.

- **IDs:** GAS-3, GAS-32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:319`
    `ItemSystemComponent.ClearAllItems`. The shelf is created at `:11` (GAS-3).
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:100` `ItemSpecContainer.Unequip`, then
    `RemoveGrantedEffects` (`:155`) and `ApplyActiveAbility` (`:174`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:596`
    `EffectSystemComponent.RemoveGameplayEffect(GameplayEffect, IEffectOrigin)`.
- **New:** `ITarget.RemoveGameplayEffect(GameplayEffect, IEffectOrigin)`, a default method that falls back to the old
  overload so other implementers still compile (`Assets/PlayForge/Runtime/PlayForge/GAS/Interfaces/ITarget.cs:30`).
  `ItemSpec.IsEquipped`.
- **Fixed in Phase 3:** GAS-48, unequipping removed the Active Ability even when equipping hadn't granted it. See
  [Phase 3](#phase-3).
- **Fixed in Phase 3:** GAS-45, an Item's `AllowDuplicates` was never read, and it is gone. See
  [Phase 3](#phase-3).
- **Moved to Phase 5:** GAS-51, item stacking is declared but not built. FEAT-14 builds it.

### Tags

Tag queries count weights in every match mode, and both hubs ask the tag cache, which has the weights. They asked
through the tag list, which counted each tag once, and a hierarchical query counted distinct tags while an exact one
counted weight. The three `CountMatches` overloads share one match, so `SameRoot` is written once. `Tag.Child`,
`GetParent`, `GetRoot`, `GetAncestors` and the implicit `string` to `Tag` conversion return the plain tag of that name.
They returned hash-padded deterministic tags, so a `SameRoot` query with a root tag never found its children. Tag workers
tick once a frame, at the end of frame; both hubs also ticked them in Update. From Phase 0, compiling granted tags walks
the sources' lists instead of allocating iterators.

- **IDs:** GAS-16, GAS-17, GAS-19, GAS-P2 (PF-007).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagQuery.cs:98` `TagQuery.CountMatches(TagCache)`, and `Matches`
    (`:135`). The hubs call it from `QueryTags`
    (`Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:354`).
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:94` `Tag.Child`, and the implicit conversion at `:348`.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/GrantedTagCompiler.cs:14` `GrantedTagCompiler`.
- **New:** `GrantedTagCompiler` (internal).
- **Watch for:** code that matched the implicit conversion against `Tag.GenerateAsUnique` of the same name no longer
  matches. Tag workers run at half their old rate.

### Workers

An impact worker set to `InlineAndDeferred` runs both halves; the cache had no case for it and ran neither. On
`RelativeOperationContextImpactWorker` that setting runs deferred, since both halves do the same work and lifesteal
healed double. A worker group hands workers only to systems the source has. A set's workers are given out once,
through `InitWorkers`; `AttributeSet.Initialize` gave them out too, so each ran twice. The frame snapshot carries the
applied and removed effects, deaths and ability injections, which `FrameComplete` listeners never saw. From Phase 0, an
idle frame allocates nothing: `ActionQueue` keeps a count, and the snapshot is built only for a listener.

- **IDs:** GAS-27 (parts 2 and 3), GAS-41, GAS-18, GAS-39, GAS-P1 (PF-006).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Core/ImpactWorkerCache.cs:65`
    `ImpactWorkerCache.RunImpactData`. The new case is at `:100`.
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Authored/RelativeOperationContextImpactWorker.cs:31`
    `RelativeOperationContextImpactWorker.Execution` (GAS-41).
  - `Assets/PlayForge/Runtime/PlayForge/Workers/Core/FrameSummary.cs:392` `FrameSummarySnapshot.Fill`, which takes
    them (GAS-39). `StandardWorkerGroup.ProvideWorkersTo`
    (`Assets/PlayForge/Runtime/PlayForge/Workers/Core/StandardWorkerGroup.cs:26`) has the guard.
- How raw deltas and the system's own changes reach workers is under Effects (Decision 9) and Attributes (Decision 14).

### The two GAS hubs

Until GAS-S1 gave `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` one core, in Phase 3, these fixes landed
in both copies of about 400 lines. The runtime hub steps `UpdateAndLate`, so its end of frame (workers, death) runs; it
stepped in Update only. Both hubs create `Callbacks` with the object, so a caller can subscribe to
`OnSystemInitialized` before a Mono GAS initializes. A runtime GAS still initializes inside its constructor. A runtime
identity copied from an `EntityIdentity` keeps its starting abilities, starting items and
`AllowDuplicateEquippedItems`. Max abilities, items and equipped items default to 99; a new asset's were 0, and a
runtime identity's were null and threw. A hub stepped by hand, outside ProcessControl, can die.

- **IDs:** GAS-1, GAS-13 (PF-013), GAS-9, GAS-23, GAS-42. The runtime hub's targeting packet is GAS-46 and GAS-38,
  above.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:67`, the constructor
    (`UpdateAndLate` at `:75`), and `RuntimeEntityIdentity(EntityIdentity)` at `:326` (GAS-9).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem_EndOfFrame.cs:52` and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.EndOfFrame.cs:44`, each hub's
    `HandleDestruction` (GAS-42).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Identity/EntityIdentity.cs:30`, the defaults (GAS-23).
- **Fixed in Phase 3:** GAS-S1, one core for both hubs, and GAS-47, the Mono GAS wrote the affiliation from its
  registration data onto its shared Entity asset. See [Phase 3](#phase-3).

## Editor

### Help buttons open the package's docs (Decision 18)

The 50 help buttons linked to `docs.playforge.dev`, a domain PlayForge doesn't own, and three named the wrong page.
They now open the package's own `Documentation/*.md` pages, through one table of topics, in the system's app for
Markdown, and only on a click (Asset Store rule 2.5.1.d). Six reference pages are new, one per asset type.

- **IDs:** ED-16, Decision 18 (Q33).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDocs.cs:83` `ForgeDocs.Open`, with the `Topics`
    table at `:17` and `DocumentationFolder` at `:92`, which finds the folder in a package or under `Assets`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:148`
    `BasePlayForgeEditor.GetDocumentationTopic`, read by `CreateEditorHeader` at `:448`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDrawerStyles.cs:419` `SectionConfig.HelpTopic`.
- **New:** `ForgeDocs`; `Documentation/ABILITIES.md`, `EFFECTS.md`, `ITEMS.md`, `ENTITIES.md`, `ATTRIBUTES.md`,
  `ATTRIBUTE_SETS.md`.
- **Renamed:** the abstract `GetDocumentationUrl()` is now the virtual no-op `GetDocumentationTopic()`, and
  `SectionConfig.HelpUrl` is `HelpTopic`. Topics are uniform slugs such as `abilities/leveling`.

### Delete moves the asset to the trash (Decision 18)

The Forge's Delete and the asset browser's default delete both go through `ForgeAssetDeletion`. It lists the project's
assets, prefabs and scenes that refer to the asset, asks, and moves it to the trash, where it can be restored. Both
deleted for good, without looking.

- **IDs:** ED-8, Decision 18 (Q16).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetDeletion.cs:25`
    `ForgeAssetDeletion.MoveToTrash`, and `FindUsers` (`:43`). Tests answer the question through `Confirm` (`:21`).
  - Caller: The Forge's Move to Trash, `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:610`.
    The Asset Browser's Delete called it too, until ED-61 took the browser.
- **New:** `ForgeAssetDeletion`.

### Showing an asset never writes its tag (Decision 18)

The five asset editors rewrote the asset tag from the name every time they built or refreshed, with no Undo and no
dirty mark. Showing now only shows: a stored tag that differs from the name's turns amber and offers Update. A rename
records the name and the tag it gives in one undoable step. Item and Effect preview the tag while the name is typed.

- **IDs:** ED-12 (its Phase 2 part; the row stays open for Phase 4's Undo, multi-editing and prefab overrides).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:552`
    `BasePlayForgeEditor.ShowAssetTag`, and `StoreAssetTag` (`:741`).
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:160`, the rename callback,
    and `SyncAssetTag` (`:652`). The other four editors follow the same shape.
- **New:** `BasePlayForgeEditor.ShowAssetTag` and `StoreAssetTag`, and a `SyncAssetTag` in each asset editor.

### One pair of tag display settings

"Display full tag path" and "Group tags by root" live in `TagDisplaySettings`, which the tag field, the View tab's tag
list and the Settings tab all read and write. The tag field returned before reading the full-path setting, Settings
showed grouping off while the field defaulted it on, and the View tab had switches of its own.

- **IDs:** ED-25.
- **Read:**
  - `Assets/PlayForge/Editor/Settings/PlayForgePreferences.cs:79`
    `TagDisplaySettings`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagDrawer.cs:179` `TagDrawer.GetDisplayText`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:648` `TagText`, which The Forge's
    tag list reads.
- **New:** `TagDisplaySettings` (internal, now in `PlayForgePreferences.cs`).

### Menu and window titles (Decision 18)

The menu moved from `PlayForge/` to `Window/PlayForge/`, where the Asset Store expects it (rule 2.5.1.a). Each window's
title matches its menu item: The Forge (it said "PlayForge Manager"), GAS Debugger, Process Control, Tag Debugger. Run
Golden Tests left the menu; the Test Runner runs the scenarios (SP-18).

- **IDs:** ED-19, Decision 18 (Q31, Q32).
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:64`
  `TheForge.ShowWindow`, with `WindowTitle` at `:25`. The Runtime Debugger, which replaced the three debuggers
  (ED-4), has its menu item at
  `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:109`.

### The Forge: settings, sorting, analysis and templates

- **ED-6.** The inspector delay fields saved a value in range and clamped one outside it; the range check was
  inverted. Phase 4 removed both delays, with the sticky header they timed (ED-11). Reset All resets every setting The
  Forge writes, keeps templates, and rebuilds the tab it was pressed on; it went to Analysis. "Auto-refresh on window
  focus" works: focus refreshes after the project has changed. Nothing read it.
  Phase 4 replaced Reset All with Use Defaults on the Preferences page,
  `Assets/PlayForge/Editor/Settings/PlayForgePreferences.cs:68` `UseDefaults` (ED-21), and the focus refresh with a
  window that follows the project, `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:140`
  `QueueReload` (ED-27).
- **ED-7.** Columns sort in natural order, so 9 comes before 10.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:660` `Sorted`, which The Forge's list
  uses.
- **ED-10.** Analysis offers only the groups that have an analyser (Effects and Abilities), and a capped list ends with
  "…and N more". ED-27 took the Analysis tab out until items and entities score too (Q7); its `BuildAnalysisTab` and
  `CreateMoreLabel` are in `TheForge.AnalysisTab.cs` as of `4dffd79`.
- **ED-22.** `TemplateRegistry` kept one EditorPrefs entry for every project on the machine, and `Save` drops the
  templates whose assets the current project lacks, so saving in one project wiped every other's. Each project now
  has its own key, from `PlayerSettings.productGUID`. The first load migrates from the shared key and leaves it alone.
  ED-21 moved the templates into the project's settings, which take a project's EditorPrefs entry once:
  `Assets/PlayForge/Editor/Settings/PlayForgeSettings.cs:279` `TakeMachineSettings`.

### Inspector fixes

- **ED-23.** An Item's Local Data can be edited; `ItemEditor` drew no section for it.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/ItemEditor.cs:387` `BuildLocalDataSection`.
- **ED-24.** `ParticipateInSiblingCascade` is drawn; it was hidden and drawn nowhere, though ProcessControl reads it.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/AbstractMonoProcessEditor.cs:87`, in
  `CreateInspectorGUI`.
- **ED-26.** The GAS Debugger removes its frame-summary hooks when it closes. They kept sampling every entity and
  stacked when it reopened. The Runtime Debugger listens to the entity it shows alone (ED-4):
  `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:152` `ShowEntity`, and `:184` `ForgetShownEntity`.
- **ED-29.** Allow Duplicate Equipped Items opens with its own value.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:440`, in
  `BuildItemsSection`.
- **ED-30.** Local Data's ScriptableObject and MonoBehaviour rows each bind their own field.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Other/DataWrapperDrawer.cs:128`, in `UpdateValueField`.

### Removed from the editor

- `QuickFillWindow`, an IMGUI window nothing opened, and `AbilityEditor.m_AbilityIcon`, which nothing assigned. Four
  editor `.meta` files lost default references to UXML that exists nowhere (ED-28).
- `BootstrapperEditor`, which held `fesgas_bootstrapper.uxml` in a field it never used; that UXML named types that no
  longer exist. Also the unreferenced `forge_abilityDrawer.uxml`. The Bootstrapper keeps the same default inspector
  (SP-21).
- Stray `Debug.Log` calls (ED-17), Phase 0's three compiler warnings (ED-1), and two unused usings that compiled only
  with `com.unity.collab-proxy` (SP-19).

## Samples and packaging

### The samples move to `Samples~` (Decision 16)

`Samples/` was neither `Samples~` nor under an assembly definition, so in a UPM install its 15 scripts never compiled,
and Unity warned about each one on every compile. The samples are now `Samples~/Demos` and `Samples~/Code Examples`,
listed in `package.json`, and Package Manager imports one on request. Unity doesn't import a `~` folder, so to edit a
sample in this repo, import it first. The Asset Store export copies them in on the packaging branch.

- **IDs:** SP-20, Decision 16. SP-11 moved the TaskSequence and group process examples, about 3,000 lines, out of the
  runtime into Code Examples, and deleted `TestAOEMonoProcess` and `TestMultiProcess`.
- **Read:**
  - `Assets/PlayForge/package.json:15`, the `samples` list.
  - `Assets/PlayForge/Samples~/Demos/FarEmerald.PlayForge.Samples.asmdef`, which compiles only where the Input System
    is installed, through `versionDefines` (SP-3).
  - `Assets/PlayForge/Samples~/Code Examples/FarEmerald.PlayForge.Examples.asmdef` (SP-11).
- **New:** both sample assemblies, and a README in each sample (SP-7).

### The Demos render in URP (Decision 17)

The Demos' ten Standard materials drew magenta under URP. They now use `PlayForge/Samples/Demo Lit`, which keeps
Standard's property names, so each material keeps its values. In the Built-in pipeline it borrows Standard's own
passes and draws what Standard drew. Under URP its own pass lights the surface by the main light, with no shadows. It
declares the URP globals it reads instead of including URP's files, so it compiles in a project without URP. It
declares all of Standard's properties, since an undeclared `_SrcBlend` or `_DstBlend` reads 0 and draws black.

- **IDs:** SP-23, Decision 17.
- **Read:** `Assets/PlayForge/Samples~/Demos/Demo/Shaders/DemoLit.shader:42`, the URP SubShader, and `:113`, the
  Built-in one.
- **New:** `DemoLit.shader`.

### A play session starts clean with domain reloading off (Decision 17)

With domain reloading off, statics outlive play mode. What a session leaves behind is now cleared at
`SubsystemRegistration`, as a domain reload would clear it. The attribute registry is cleared: last session's attribute
kept its name, so this session's attribute of that name was turned away. So are the sequence runs-by-token map, the
activation id counter, the pipeline config registry, and ProcessControl's and GameRoot's `Instance`. `TagRegistry` is
kept on purpose: static tag fields register once per domain, as their types initialise, and wouldn't register again.

- **IDs:** SP-22, PC-26, Decision 17. The Asset Store requires this from Editor 6.6 (rule 2.5.h).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/AttributeRegistry.cs:14` `AttributeRegistry.Clear`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:71`
    `TaskSequenceRuntime.ClearRuns`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilityActivationHandle.cs:23`
    `AbilityActivationHandle.ResetIds`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/ConfigRegistry/PipelineConfigRegistry.cs:20`
    `PipelineConfigRegistry.Clear`.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagRegistry.cs:23`, the note on why the tag registry stays.

### Sample content

- **SP-1.** 39 sample assets named the assembly of 16 framework types as `Assembly-CSharp`, from before the runtime
  had its own assembly, so their `[SerializeReference]` values didn't resolve. They name `FarEmerald.PlayForge`.
- **SP-2.** DemoScene lost 621 components of `TestSequences`, a script with no class.
- **SP-3.** The Demo's EventSystem uses `InputSystemUIInputModule`; the legacy module threw under the Input System.
- **SP-4.** The samples use no asset outside the package. The UI Toolkit panel settings and theme were copied in with
  their GUIDs, and the material packs' materials gave way to plain ones.
- **SP-5, Decision 15.** QuickOutline, whose header reserves all rights, is replaced by the Demo's own outline:
  `Assets/PlayForge/Samples~/Demos/Demo/Scripts/DemoOutline.cs:13` `DemoOutline`, and `DemoOutline.shader`.
- **SP-6.** Swarm Defender's debug hotkeys and `TestIntEffect` fields are gone.
- **SP-7.** Placeholder descriptions are filled in, and `EFECT_Speed_Override` is `EFFECT_Speed_Override`, with its
  GUID kept.

### The Demo replaces the Demos (Phase 7, Decision 37)

The entries above describe the Demos and Swarm Defender as they were at `e91fe34`. Phase 7 replaced them with one
sample, the Demo: a hero arena of five heroes, fourteen items, an AI hero and a training dummy, every piece an asset,
using every asset feature. It needs nothing beyond PlayForge and renders in the Built-in pipeline and URP, as every
shape is made as the scene plays. `git show e91fe34:<path>` shows the old files.

- **IDs:** SP-25, SP-26, Decisions 37 to 39; the framework gaps it found, GAS-65 to GAS-70 and FEAT-24, FEAT-26 and
  FEAT-27.
- **Read:**
  - `Assets/PlayForge/Samples~/Demo/README.md`, which maps each feature to the assets that show it.
  - `Assets/PlayForge/Samples~/Demo/Scripts/Forge`, the demo's own workers, tasks, sequences, cue and process.
  - `Assets/PlayForge/Samples~/Demo/Scripts/Game/DemoUnit.cs` `DemoUnit.Create`, how a hero is put in.
  - `Assets/PlayForge/Samples~/Demo/Scripts/Tests/DemoTests.cs`, the scene played: every ability cast, the damage
    formulas, items, levels, Burning's overflow, the AI.
- **New:** the Demo sample. The synced sequence examples moved from the Demos to Code Examples. Proving Grounds'
  smoke suite sweeps the Demo's assets.

### Package and repo

- **The package (Decision 2).** `Assets/PlayForge/package.json` makes PlayForge the local UPM package
  `com.faremerald.playforge` 0.1.0, for Unity 6000.0. It depends on `com.unity.ugui`. A package can't declare a git
  dependency, so its description asks each project to add UniTask pinned to 2.5.11 (SP-8); this repo's
  `Packages/manifest.json:3` does.
- **SP-12.** `BuildVerification.BuildWindowsPlayer` builds an empty scene of its own when the build settings enable
  none; it built whichever scene the AssetDatabase listed first. The class itself is new in this range (`455dd44`).
  `Assets/PlayForge/Editor/Tools/BuildVerification.cs:21`.
- **SP-13, Decision 6.** `LICENSE` now points to the Unity Asset Store EULA; the MIT licence is gone.
- **SP-14.** `Assets/PlayForge/CHANGELOG.md` is new, and the project's product name and version are PlayForge 0.1.0.
- **SP-21.** The editor's UXML and USS import cleanly into a fresh project (see Removed from the editor).
- **SP-9, SP-10, SP-17 (Decision 7).** Stray files are gone. `.gitignore` covers `**/.claude/worktrees/`, `.idea/`,
  `/UIElementsSchema/` and the two material packs, which stay on disk untracked. History wasn't rewritten.

## Tests

- **The golden runner** is in `Assets/PlayForge/Tests`:
  - `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs:47`, the `Scenarios` table: S1 to S12 are the originals,
    S13 to S22 came from Arcade Fighter, and S23 to S25 are new. From S13 each name carries the tracker ID it pins.
    `RunAll` (`:79`) runs them all and exits batch mode with 0 or 1; `Run` (`:91`) runs one by name.
  - `Assets/PlayForge/Tests/Golden/GoldenScenarioTests.cs:19` `GoldenScenarioTests.Scenario` runs each scenario as a
    Test Runner case in the `FarEmerald.PlayForge.Tests.Golden` assembly (SP-18). That assembly compiles only where
    tests are included: in a project's `Assets`, or for a package listed in `testables`.
  - The `PlayForge/Run Golden Tests` menu item is gone (ED-19). In batch mode,
    `-executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll` still works.
  - Changed expectations: S6 is now `S6_CommittedHistoryLandsAfterModifiers` and expects 85 (Decision 4), at
    `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs:491`. S15 (`:825`) expects 42.5 for its raw delta
    (Decision 9). No scenario is masked as expected-red any more (SP-16).
- **PlayForge Proving Grounds** holds most of the refactor's tests. It is a separate Unity project at
  `C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds`, in its own local git repo. It consumes this repo's
  `Assets/PlayForge` as the local package `com.faremerald.playforge`, so its tests run against the code here. Its tests
  are in `Assets/ProvingGrounds/Tests`:
  - `Framework/ProcessControl`, `Framework/TaskSequence` and `Framework/GAS` hold PlayMode tests by area, run under a
    live ProcessControl. A test that pins a bug is named after its tracker ID, for example
    `PauseTests.PC_11_A_pause_takes_effect_at_once` or
    `AbilityActivationTests.GAS_46_Apply_Effects_reaches_the_target_targeting_found`, so a search for `GAS_46` finds
    it.
  - `Editor` holds the EditMode tests for the editor code and the framework outside play mode, `DomainReloadTests`
    (SP-22) among them. `EditorSmokeTests` is Phase 4's gate (Q36): in each editor theme, every inspector and window
    opens without an error, on new assets and on the Demos' assets, and one Undo takes back every edit an inspector
    makes. Its README section says how it reaches collapsed parts and puts the theme back.
  - `Golden` runs the 25 golden scenarios on both hubs. `Harness` and `Planted` test the harness itself; every planted
    test must fail. The harness is in `Assets/ProvingGrounds/Harness`.
  - A test for a bug not fixed yet carries `[Category("Red")]` (Decision 8), and `-testCategory "!Red"` is the green
    gate. The project's `README.md` and the plan's Environment section have the commands.

## First read: the newest high-impact spots

Newest first. Each row is a good place to start reading.

| What | IDs | Where | Commit |
|---|---|---|---|
| An ability made in code has a stage list | GAS-63 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityBehaviour.cs:29` | `e91fe34` |
| A template's push searches the project once | ED-44 | `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePropagator.cs:216` | `024cf09` |
| Ending a process costs the same however many there are | PC-34 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:237` | `4a226e6` |
| An area ability strikes every target | GAS-62 | `Assets/PlayForge/Runtime/PlayForge/Utils/ProcessTaskUtil.cs:54` | `fff120f` |
| Rollback: abilities kept through a load, timers saved in full | GAS-60, GAS-61 | `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:304` | `2c8dff3` |
| Moving an effect between entities, and possession | FEAT-23 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:860` | `21da7cb` |
| An entity's time scale from an attribute, and time magic | FEAT-22 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:175` | `0cbdb9a` |
| The Debugger's Timeline: the ability graph and the effect recorder | FEAT-12 | `Assets/PlayForge/Editor/Debugger/TimelineRecorder.cs:57` | `514fb70` |
| The Debugger in a narrow column | ED-43 | `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:535` | `73c4208` |
| The Profiler modules, and a frame's costliest processes | FEAT-18 | `Assets/PlayForge/Editor/Debugger/ForgeProfilerModules.cs:18` | `f2341a2` |
| Profiler counters | FEAT-18 | `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeCounters.cs:13` | `67bc9d0` |
| The step loop's, tags', recomputes', stages' and each type's markers | FEAT-18 | `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeProfiler.cs:65` | `d2de8ec` |
| Worlds side by side, and with no scene | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:171` | `b9ea337` |
| Waits that ask once a tick | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ForgeTick.cs:141` | `d88583a` |
| Abilities in their caster's world | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:33` | `c5e9cb8` |
| A world's own time | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ForgeTick.cs:96` | `4a603ae` |
| The plain world | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:14` | `1eb436a` |
| A process's own control | FEAT-10 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:145` | `40f257e` |
| Clear's defaults | ED-42 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/SectionClearUtility.cs:22` | `4d110da` |
| Targeting input | FEAT-11 | `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeInput.cs:31` | `ed60c62` |
| Gameplay cues | FEAT-1 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:765` | `2b623ed` |
| Ability triggers | FEAT-4 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:518` | `b4e0cbd` |
| SetByCaller magnitudes, and a sequence made in code | FEAT-2, FEAT-21 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffectSpec.cs:51` | `c24fa00` |
| Copying an entity, and a level's max before its level | FEAT-20, GAS-59 | `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:469` | `7e6dae3` |
| Profiler markers, and activation by asset or tag with a reason | FEAT-18, FEAT-17 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:698` | `c7f7ef1` |
| An Effect Stacks scaler, and the Combat lab | FEAT-19 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/EffectStacksScaler.cs:11` | `2944804` |
| A warning for usage effects no stage applies, and the Tactics lab | ED-41 | `Assets/PlayForge/Editor/Problems/ForgeRules.cs:217` | `6129b13` |
| Save and load | FEAT-9 | `Assets/PlayForge/Runtime/PlayForge/Save/ForgeSave.cs:264` | `eee91a6` |
| Item stacking, level-up rescaling, and the Roguelite lab | FEAT-14, GAS-58 | `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:120` | `643f5c0` |
| A pool's object goes back to its pool, and the Bullet heaven lab | PC-33 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/MonoWrapperProcess.cs:108` | `f66e04d` |
| The input buffer, stages that branch, and the Duel lab | FEAT-6, TS-7 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:815` | `b0d6492` |
| The MOBA hero kit lab, GAS-57, and While active heard at once | GAS-57, FEAT-5 | `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Authored/RelativeOperationContextImpactWorker.cs:100` | `26aa233` |
| Cancel and block abilities by tag, and While active | FEAT-5 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:466` | `cfab32a` |
| Abilities granted by effects, and the chance to apply | FEAT-8 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:357` | `60683cd` |
| The Status-effect lab, and overflow at max stacks | FEAT-8 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:570` | `13eac5b` |
| The fixed-rate tick | FEAT-7 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:405` | `4058098` |
| A clock for each entity | FEAT-7, TS-6 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EntityClock.cs:12` | `48c2c8e` |
| One clock and one random source | FEAT-13 | `Assets/PlayForge/Runtime/PlayForge/Utils/ForgeTime.cs:48` | `c500d5e` |
| Tag-change events | FEAT-3, GAS-56 | `Assets/PlayForge/Runtime/PlayForge/Tag/TagCache.cs:558` | `3dc3129` |
| The Register builder | FEAT-15 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRegistration.cs:112` | `6586536` |
| The Debugger's foot rows | ED-40 | `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:255` | `520c433` |
| Cleanse and immunity name granted tags | FEAT-8 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:275` | `520c433` |
| The Debugger, as the boards draw it | ED-4, ED-5 | `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Tags.cs:160` | `c934980` |
| Cleanse and immunity | FEAT-8 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:187` | `04d0742` |
| The debugger acts, and loose tags | ED-40, FEAT-16 | `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.Entities.cs:447` | `5dd7332` |
| One Runtime Debugger | ED-4, ED-5 | `Assets/PlayForge/Editor/Debugger/RuntimeDebugger.cs:202` | `d220ff4` |
| Templates work as prefabs do | ED-13 | `Assets/PlayForge/Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePush.cs:37` | `6733220` |
| The Forge is a list and a detail | ED-27, ED-9 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:403` | `59f5ae9` |
| One validator and the Problems view | ED-15 | `Assets/PlayForge/Editor/Problems/ForgeValidator.cs:41` | `4dffd79` |
| Settings belong to the project | ED-21 | `Assets/PlayForge/Editor/Settings/PlayForgeSettings.cs:96` | `90a711b` |
| Every asset inspector edits a multiple selection | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/EachAsset.cs:17` | `fc92e9d` |
| The scaler field reads as the scaler board | ED-32, ED-33 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/MagnitudeField.cs:51` | `4cc9be9` |
| Scaler assets, and levels that map exactly | ED-33 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/SharedScaler.cs:33` | `e44d1ed` |
| One Import window | ED-32, ED-35 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/ForgeImportWindow.cs:60` | `c300449` |
| The Effect inspector follows the style doc, with the requirements table | ED-37 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:94` | `6090980` |
| An effect makes more than one impact, each with its own delivery | ED-2, ED-3 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:46` | `ccb4981` |
| The Ability inspector follows the style doc | ED-37 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:330` | `031c2d4` |
| Opening an attribute set writes nothing | ED-39 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Attribute/AttributeSetElementDrawer.cs:159` | `22824ac` |
| Every asset reference is Unity's object field | ED-38 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Base/AbstractForgeObjectDrawer.cs:20` | `3cfa570` |
| An empty set element gives nothing; Use Minimum takes the lowest | GAS-54, GAS-55 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:374` | `c59f86a` |
| The Attribute Set inspector shows what it gives | ED-37 | `Assets/PlayForge/Editor/UI/Kit/ForgeAttributeTable.cs:43` | `86ead05` |
| The Entity inspector follows the style doc | ED-37 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:103` | `a06a4fb` |
| The Item inspector follows the style doc | ED-37 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/ItemEditor.cs:93` | `cf70e8f` |
| The Attribute inspector lists what uses it | ED-15 | `Assets/PlayForge/Editor/UI/Kit/ForgeUsedBy.cs:38` | `ed6295a` |
| The asset header stays at the top | ED-36, Decision 29 | `Assets/PlayForge/Editor/UI/Kit/ForgeAssetHeader.cs:132` | `d851e30` |
| The editor shows no emoji | ED-20 | `Assets/PlayForge/Editor/UI/Kit/ForgeGlyph.cs:143` | `6c0c3fe` |
| One style sheet and a kit of controls | ED-20, Decision 20 | `Assets/PlayForge/Editor/UI/PlayForge.uss:8` | `f5832df` |
| A drawer keeps its collapse state per asset | ED-18 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/DrawerCollapseState.cs:48` | `1ae0833` |
| The inspector keeps no scroll view of its own | ED-11 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:355` | `88d0f3f` |
| An inspector listens to the editor only while a picker is open | ED-11 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:88` | `850f8fb` |
| The Impact and Effect drawers follow what they show | ED-11 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Assets/Effect/GameplayEffectImpactDrawer.cs:143` | `c5d5480` |
| The Item and Ability inspectors edit a multiple selection | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:251` | `1a203c1` |
| A type field's drawer writes nothing while drawing | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Base/AbstractTypeRefDrawer.cs:67` | `5134867` |
| A rebuilt drawer edits every selected asset | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagRequirementDrawers.cs:90` | `96b4ba6` |
| Each unlink question counts the children it would change | ED-31 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs:269` | `523f57d` |
| Link, Unlink and template assignment undo | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:239` | `097384f` |
| Opening a scaler writes nothing | ED-34, Decision 28 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Scaler/ScalerFields.cs:232` | `7cc83c0` |
| Every asset inspector's own fields undo | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:228` | `62ed48e` |
| The Attribute inspector undoes every edit and edits a multiple selection | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AttributeEditor.cs:139` | `e1d1dd0` |
| A task waiting in a delay allocates nothing a frame | TS-11 | `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:55` | `ec008f7` |
| A frame whose summary has a listener allocates nothing | GAS-P10, Decision 26 | `Assets/PlayForge/Runtime/PlayForge/Workers/Core/FrameSummary.cs:347` | `11a0f66` |
| Interrupting every running ability ends each | GAS-53 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:881` | `473ea8e` |
| A runtime entity's abilities end when it does | GAS-52 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/AbstractRuntimeProcess.cs:119` | `3102975` |
| An ability asked for inside its own activation starts next frame | TS-8, Decision 25 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:619` | `c45f4f1` |
| Sequences and abilities end the frame their last task does | TS-Z, Decision 24 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:54` | `04c3db1` |
| A tick, a recompute and a tag query allocate nothing | GAS-P4 | `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffectSpec.cs:192` | `b50a80b` |
| Ending an effect costs the same however many end with it | GAS-P5 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:719` | `7ce68aa` |
| An application costs the same however much the entity carries | GAS-P3 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:71` | `1242251` |
| Both GAS hubs forward to one core | GAS-S1 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:72` | `5e7f077` |
| An attribute set reached twice counts once | GAS-44 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:63` | `57ea4ff` |
| Tick On Application alone lands its change | GAS-50 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:405` | `e40b3b6` |
| Opening abilities wait for the entity's attributes | GAS-49 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:225` | `999e779` |
| Unequipping takes back only the item's grant | GAS-48 | `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:199` | `27c2325` |
| Each entity keeps its own affiliation | GAS-47 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/GASCore.cs:81` | `fdc1886` |
| Targeting's packet resolves to the entity, so Apply Effects finds its target | GAS-46 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:137` | `fe4271c` |
| Help buttons open the package's own docs through one table | ED-16, Decision 18 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDocs.cs:83` | `67db441` |
| Demo Lit draws the Demos under URP and Built-in | SP-23, Decision 17 | `Assets/PlayForge/Samples~/Demos/Demo/Shaders/DemoLit.shader:42` | `954a11a` |
| Statics clear at the start of a play session | SP-22, PC-26 | `Assets/PlayForge/Runtime/PlayForge/Utils/AttributeRegistry.cs:14` | `86e9e11` |
| Showing an asset never writes its tag; a rename is one Undo | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:552` | `4d71636` |
| Delete names what uses the asset and moves it to the trash | ED-8 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetDeletion.cs:25` | `e89a9f7` |
| The samples live in `Samples~` and import on request | SP-20, Decision 16 | `Assets/PlayForge/package.json:15` | `8d17bc0` |
| A derived refresh runs the post-change pipeline | GAS-27, Decision 14 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:566` | `550d07b` |
| Combined elements add up | GAS-27, Decision 13 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeBlueprint.cs:85` | `5244d99` |
| The hierarchy follows transforms; SceneRoot spawns are GameRoot's children | PC-28, Decision 11 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessWorld.cs:1468` | `5f6c0eb` |
| Commits land after the modifiers | GAS-15, Decision 4 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:485` | `66b33ad` |
| Pause holds a process in place | PC-7, PC-11, PC-12, PC-27, Decision 5 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:254` | `23f52aa`, `666beda` |
