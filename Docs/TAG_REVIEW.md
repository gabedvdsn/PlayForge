# The tag review (FN-10)

The user's note of 2026-11-13 (Decision 70): go through the tag contexts as they are, which exist, whether each is apt,
how they're assigned to fields and whether each field has the right ones; the tags `Tags.cs` writes, and how robust they
are; and how a project's own hard-coded tags reach the tag registry with nothing done by hand.

It was made by reading the code, with nothing run in Unity. The unique tags' padded names were worked out by
re-implementing `GenerateDeterministicTag` and matched against names stored in assets. These were checked again against
the code before this page was written: a unique tag's name padded to 64 characters and `IsChildOf` matching by name
(`Tag.cs:83-88, 223-233`, `TagRegistry.cs:564-607`); the core tags made with `GenerateAsUnique` from short texts
(`Tags.cs`); the five asset types the registry scans (`ForgeTagRegistry.cs:286-294`); contexts matched by substring
(`ForgeTagRegistry.cs:353-363`); and code tags read from `Tags` alone (`ForgeTagRegistry.cs:280-284`). Paths are
relative to `Assets/PlayForge/` unless marked **PG:** (PlayForge Proving Grounds) or **AF:** (Arcade Fighter).

## The rows it makes

Each fix is a row under The tag review (TAG) in `Docs/REFACTOR_PLAN.md`.

| ID | Fix | Waits on |
|---|---|---|
| TAG-1 | A hierarchy for unique tags: a New-Tag tag can't have children today (§5.1) | |
| TAG-2 | Core tags kept apart from designers' tags of the same text (§3.3.1-2) | |
| TAG-3 | Each field's contexts made right: the writer and the reader of a kind of tag share one, and the ~25 unmarked fields get theirs (§2.2-2.4); Data split into its kinds | TAG-6 |
| TAG-4 | Contexts matched exactly, not by substring, and a field with several contexts no longer files its pick under each (§5.2-5.4) | |
| TAG-5 | A project's code tags found with nothing done by hand (§4) | |
| TAG-6 | The contexts pruned: the 18 no field names, the overlaps merged, the unclear names renamed, with a pass over the libraries' strings (§1) | |
| TAG-7 | Every PlayForge asset type scanned, and the fields the walk skips, through a SerializedObject walk as ForgeLint's (§2.4) | |
| TAG-8 | `Tags.cs` made sound: contexts for the five with none, the unused Data keys pruned or hidden, the docs fixed, long paths refused, and a test that pins the padded names and each tag's context (§3) | TAG-1, TAG-2 |

## Decisions it needed

Decided by Decision 71, the user's choices, the first of each pair below: hierarchy by display path, a warning in New
Tag, a convention scan, and the contexts pruned and renamed. Decision 73 then made TAG-5 opt-in: a code tag reaches the
pickers only marked `[ForgeTagExposed]`, which replaces `[ForgeContextDefault]`, and every tag `Tags.cs` writes is
marked.

1. **TAG-1, a hierarchy for unique tags.** Match a unique tag's hierarchy by its display path, what the designer sees,
   with no asset touched; or make New Tag's tags plain, with a pass that renames the padded ones; or pad a child from
   its parent's padded name, so the name's prefix matches.
2. **TAG-2, core tags against designers'.** New Tag warns that a text is a core tag's and offers it; or the core tags
   move under a namespace, as PlayForge.Health, with a pass over the assets that store them.
3. **TAG-5, code tags.** A convention scan of every public static Tag field and property in the project's assemblies,
   filed under its `[ForgeContextDefault]` or a Code context, with an opt-out; or an opt-in attribute on a class; or a
   generated Code Tags library.
4. **TAG-6, the contexts.** Prune and rename them, with a pass over the libraries; or keep them, and fix only the fields
   and the matching.

## The main problems, ranked

1. **A tag made in New Tag can't have children.** New Tag and "Make It a Child Of…" make *unique* tags, which pad
   every name to 64 characters. `IsChildOf` matches by name prefix and needs the child's name to be longer, so a New-Tag
   "Status.Debuff.Burn" is never under a New-Tag "Status.Debuff". IncludeChildren queries, crowd control, Removes/Blocks
   Effects Tagged, triggers ("or a tag under it"), resolve order and the linter all miss. The same goes for a project
   event under a core event (Died.ByFire under `Tags.DIED`). The tests build hierarchies only with plain `Tag.Generate`
   (§5.1).
2. **`Tags.cs` names are short and global, so they alias designers' tags.** New Tag "Health", "Target", "Blocked",
   "None", "Data" or "Duration" gives exactly the core tag, with no warning. Code that writes `Tag.Generate("Health")`
   gets a different tag that reads the same (§3).
3. **Fields that write a kind of tag and fields that read it carry different contexts.** This affects Local Data keys,
   set-by-caller keys, events, crowd control, stage conditions, and the effect and ability tag filters, so a picker
   doesn't offer what the other side wrote. About 25 authored tag fields have no context at all, and their pickers
   offer every tag (§2).
4. **The registry scans five asset types.** Items, Requirement Templates (ED-79), Crowd Control Rules, the Forge
   Project's roles, resolve order and combat log, Effect Modifiers, Scaler assets, Lab Rules and prefabs are never read.
   Neither are interface-typed or non-`[Serializable]` `[SerializeReference]` fields (activation rules, targeting).
   A tag used only there leaves every picker at the next rescan, which any `.asset` save triggers (§2).
5. **Contexts match by substring.** "Impact" pickers offer every retention group ("Impact Retention"). "Category" would
   take "Requirement Category", and "Attribute" takes "AttributeSet". A field with several contexts files its pick
   under all of them (§5.2–5.3).
6. **Code tags reach pickers only from `Tags`.** `[ForgeContextDefault]` can only go on properties, so
   `static readonly Tag` fields (Arcade Fighter's `CombatTags`, the Demo's `DemoSequences`) can't be marked.
   `RegisterContextDefaultSource` is called nowhere. The linter and the runtime registry also read `typeof(Tags)` alone
   (§4).
7. **18 of the 31 contexts are named by no field.** Several overlap, contexts of two different kinds are mixed in one
   list, and four constants' names differ from their stored values (§1).

---

## 1. The contexts (`Runtime/PlayForge/Core/ForgeContext.cs`)

There are 65 `[ForgeTagContext]` uses, all in Runtime. There are none in Editor or Samples~, and none in Proving
Grounds' or Arcade Fighter's own code. In the table, "Fields" counts uses that name the context (of which "shared" also
name others), and "Tags.cs" counts `[ForgeContextDefault]` properties.

| Constant (line) | Value | Its doc comment says | Fields | Tags.cs | Note |
|---|---|---|---|---|---|
| Ability (18) | "Ability" | Tags used in Ability assets | 0 | 0 | Only the registry's guess for unmarked fields (ForgeTagRegistry.cs:996) |
| Effect (21) | "Effect" | …GameplayEffect assets | 0 | 0 | Same |
| Entity (24) | "Entity" | …EntityIdentity assets | 0 | 0 | Same |
| Attribute (27) | "Attribute" | …Attribute assets | 0 | 0 | Same. A substring of "AttributeSet" |
| AttributeSet (30) | "AttributeSet" | …AttributeSet assets | 0 | 0 | Same. There's no Item context, though items carry tags |
| Data (32) | "Data" | *(none)* | 13 | 44 | Holds four kinds of key: packet keys, Local Data, worker keys, and process roles / set-by-caller |
| AssetIdentifier (39) | "Asset Identifier" | identify an asset (AssetTag field) | 9 (3 shared) | 0 | |
| ContextIdentifier (41) | "Context Identifier" | *(none)* | 14 (1 shared) | 0 | Reads as "the identifier of a context". It means an asset's Context Tags, and overlaps Category |
| Granted (44) | "Granted" | granted to entities/targets | 9 (3 shared) | 0 | |
| Required (47) | "Required" | required for activation/application | 2 (both shared) | 0 | Says where a tag is used, not what it is; avoid lists get it too |
| Blocked (50) | "Blocked" | block activation/application | 0 | 0 | Unused. `Tags.BLOCKED` is an *event* of the same word |
| Visibility (53) | "Visibility" | visibility/targeting conditions | 2 | 2 | Overlaps Targeting |
| Affiliation (56) | "Affiliation" | affiliation/team grouping | 3 | 1 (SYSTEM) | |
| Category (59) | "Category" | categorization/filtering | 0 | 0 | Unused. Overlaps ContextIdentifier, and is a substring of RequirementCategory |
| RequirementCategory (62) | "Requirement Category" | a template's category (ED-79) | 1 | 0 | Its asset type isn't scanned (§2.4) |
| Targeting (65) | "Targeting" | targeting conditions | 0 | 0 | Unused |
| Texture (68) | "Texture" | "categorization/filtering" | 1 | 2 | Doc copied from Category. It's a texture slot |
| Worker (75) | "Worker" | workers/processors | 0 | 0 | Unused. Worker keys use Data |
| Modifier (78) | "Modifier" | modifiers | 0 | 0 | Unused |
| Condition (81) | "Condition" | conditions/requirements | 0 | 0 | Unused. Overlaps Required |
| Stage (84) | "Stage" | stages/phases | 0 | 0 | Unused, yet it fits RecordLandedAs and a stage's Condition, which have no context |
| Trigger (87) | "Trigger" | triggers/events | 1 | 9 | `AbilityTrigger.Tag` and a threshold's Event lack it |
| Cost (90) | "Cost" | cost definitions | 0 | 0 | Unused |
| Cooldown (93) | "Cooldown" | cooldown definitions | 0 | 0 | Unused |
| Impact (96) | "Impact" | impact/damage calculations | 9 | 3 | A substring of "Impact Retention" |
| Duration (99) | "Duration" | duration-related logic | 0 | 0 | Unused. `Tags.DURATION` is a Data key |
| RetentionGroup (102) | **"Impact Retention"** | retention groups | 5 | 3 | Name ≠ value |
| Role (105) | "Role" | the part an attribute plays (TW-29) | 2 | 3 | "Role" also means process roles (Data) and `EAttributeRole` |
| Universal (112) | "Universal" | anywhere (no filtering) | 0 | 0 | Given to a library entry with no contexts; every picker includes it |
| System (115) | "System" | internal/system use | 0 | 1 | Only `Tags.SYSTEM` |
| Debug (118) | "Debug" | debugging | 0 | 2 | Only `Tags.SYSTEM` and `Tags.DEBUG` |

**Flags**

- **Unused by any field (18):** the five asset-type contexts, Blocked, Category, Targeting, Worker, Modifier,
  Condition, Stage, Cost, Cooldown, Duration, Universal, System and Debug. The Contexts menu still offers all 31
  (`ForgeTagContextsField.cs:55,80` → `ForgeTagContexts.All`, `ForgeTagContexts.cs:19-31`), so a tag can be declared
  for a context no field reads.
- **Two kinds of context are mixed.** Some name *what a tag is*: Granted, Affiliation, Impact, Trigger, Role,
  Impact Retention, Asset/Context Identifier, Visibility, Texture, Data and Requirement Category. Others name *where it
  is used*: the asset types, Required, Blocked, Condition, Stage, Cost, Cooldown, Duration, Worker, Modifier, Targeting
  and Category. A picker needs the first kind. The second kind splits one kind of tag (required in one field, granted
  in another) into separate keys, which only the substring match joins back up.
- **Overlaps:**
  - Category and ContextIdentifier
  - Required, Condition and Blocked
  - Visibility and Targeting
  - Worker and Data
  - the Duration context and the `Tags.DURATION` key
  - three meanings of Role
  - Trigger and "event", which is the word every tooltip uses
- **Unclear names:**
  - "Context Identifier"
  - "Data", which holds four kinds of key
  - "Impact", which means impact types
  - "Texture", which is a texture slot
  - "Trigger", which is a gameplay event
  - RetentionGroup = "Impact Retention"
- **Names that differ from their values:** AssetIdentifier, ContextIdentifier, RetentionGroup and
  RequirementCategory. Tag Library entries store the value as a free string (`ForgeTagLibrary.cs:28-29`), and its
  tooltip says "ForgeContext constants", which invites someone to type "RetentionGroup".
- **Stale doc header:** it uses `ForgeTags.Effect`, a class that doesn't exist, on a `string` field
  (`ForgeContext.cs:3-10`). `ForgeContext` is in `FarEmerald.PlayForge.Extended`, while `Tags` is in
  `FarEmerald.PlayForge`.

---

## 2. Contexts on fields

### 2.1 How a field gets its context

- **With `[ForgeTagContext(...)]`** (`ForgeAttributes.cs:17-46`): the attribute on a list field applies to its
  elements, and several contexts mean their union. The drawer reads it from `fieldInfo` (`TagDrawer.cs:69-83`), then
  asks `ForgeTagRegistry.GetTagsForContext` (`TagDrawer.cs:92`).
- **With no attribute:**
  - The picker offers every tag (`ForgeTagRegistry.cs:339-340`).
  - A pick registers under no context (`TagDrawer.cs:152-153`).
  - The next rescan files the tag under a context guessed from the asset type and the field's name
    (`ForgeTagRegistry.cs:991-1017`): "grant" gives Granted, "target" gives Targeting, "type" gives Category, and so
    on. The same field therefore files a tag one way when it's picked and another way after a rescan.
- **A tag's own contexts** come from Tag Library declarations (ED-83, `ForgeTagContexts.cs:45-69`) and from
  `[ForgeContextDefault]` on a `Tags` property.

### 2.2 The 65 marked fields

| Context(s) | Fields (file:line) | Verdict |
|---|---|---|
| Impact (9) | OnHit.ImpactTypes (OnHit.cs:42); Lifesteal.ImpactTypes (Lifesteal.cs:43); AbstractContextImpactWorker.ImpactType, .WorkImpactType (AbstractContextImpactWorker.cs:23,51); AbstractFocusedAttributeWorker.ImpactTypes (:44); ChangeImpactChange.ImpactTypes (EffectModifiers.cs:140); AbilityTrigger.ImpactTypes (AbilityTrigger.cs:73); AttributeImpactEntry.ImpactTypes (:75); GameplayEffectImpact.ImpactTypes (:147) | Right. Their pickers also show retention groups (§5.2) |
| Impact Retention (5) | GroupImpactKind.Group (:24); AttributeSet.RetentionGroup (:228); RetentionGroups.Groups (:36); AttributeImpactEntry.RetentionGroupOverride (:72); GameplayEffectDefinition.RetentionGroup (GameplayEffect.cs:297) | Right |
| Affiliation (3) | EntityIdentity.Affiliation (:16); SideChange.Sides (SideChanges.cs:33); GameplayEffectImpact.Affiliations (:135) | Right. `Tags.SYSTEM` appears in all three |
| Role (2) | AttributeRole.Role (ForgeProjectSettings.cs:16); AttributeCost.Role (:18) | Right. A project's own roles live in the Forge Project, which isn't scanned |
| Visibility (2) | ItemDefinition.Visibility (:17); GameplayEffectDefinition.Visibility (GameplayEffect.cs:295) | Right |
| Texture (1) | TextureItem.Tag (TextureItem.cs:11) | Right |
| Requirement Category (1) | RequirementTemplate.Category (:32) | Right context. Templates aren't scanned, so after any rescan the picker doesn't offer "the categories templates use", which ED-79 promised. ED-79's test checks only the attribute (PG: RequirementTemplateTests.cs:47-52) |
| Trigger (1) | CombatLogSettings.Events (CombatLog.cs:39) | Right |
| Granted (6 alone) | ItemTags.PassiveGrantedTags (:18); EntityIdentity.GrantedTags (:21); GrantTagsChange.Tags (EffectModifiers.cs:243); AbilityTags.PassiveGrantedTags, .ActiveGrantedTags (:18,22); GameplayEffectTags.GrantedTags (:307) | Right |
| Asset Identifier (alone) | ItemTags.AssetTag (:10); EntityIdentity.AssetTag (:19); AbilityTags.AssetTag (:13); AttributeSet.AssetTag (:17); GameplayEffectTags.AssetTag (:303) | Right |
| Asset Identifier (alone) | **EffectModifierRule.EffectTags** (EffectModifiers.cs:32) | **Missing ContextIdentifier.** `Picks` matches the asset tag *or a context tag* (EffectModifiers.cs:42-56), as its tooltip says |
| Context Identifier (alone) | ItemTags/EntityIdentity/AbilityTags/GameplayEffectTags.ContextTags; AbstractFocusedAttributeWorker.ValidContextTags (:53) | Right. ValidContextTags compares context tags (ForgeHelper.cs:213-224) |
| Context Identifier (alone) | **GameplayEffectTags.RemovesEffectsTagged (:313), .BlocksEffectsTagged (:319); EntityDeath.EndsEffectsTagged (:40); TrackedImpactKind.Tagged (:70)** | **Wrong.** They're matched by `GameplayEffectTags.Names`, which takes the asset tag, context tags *or granted tags* (GameplayEffect.cs:322-346; EffectSystemComponent.cs:402,417,955; TrackedImpactKind.cs:110, whose tooltip says so). They need [AssetIdentifier, ContextIdentifier, Granted], which AssetPartKind.Tagged has for the same matcher |
| Context Identifier (alone) | **AbilityBehaviour.CancelledBy, .Blocks (:36,42); AbilityTaskBehaviourStage.CancelledBy, .Blocks (:78,84)** | **Missing AssetIdentifier.** `AbilityTags.Names` takes the asset tag or a context tag (AbilityTags.cs:40-58), as their own docs say |
| Asset Id + Context Id + Granted | AssetPartKind.Tagged (:128) | Right |
| Required + Granted + Asset Id | TagQuery.Tag (TagQuery.cs:18) | Mostly right. The entity's tag cache also holds its affiliation (GASCore.cs:565), so Affiliation is missing. "Required" also labels avoid lists |
| Required + Granted + Asset Id | **AbstractTagValidationRule.Tag** (CommonValidations.cs:125) | **Partly wrong.** `GetValue` falls back to the ability's Local Data under the tag (:128-146), and CheckRangeValidation uses it as a range key, which is a Data key. It's never scanned either (§2.4) |
| Data (13) | SetAssets/Effects/Entities/Attributes/AttributeSets/Abilities/Tags tasks' Tag (DataLoadingAbilityTasks.cs:23,42,61,80,99,117,135); AutoTargetFromDataTask.SourceKey (:23) | Right (packet keys) |
| Data | LocalDataKind.Key (:31) | Right kind, but the Local Data entries it reads (`DataWrapper.Key`) have no context (§2.3) |
| Data | WorkerNumberKind.Key (:36); AssetPartKind.WorkerKey (:141); AttributeChangeEffectWorker.Key (:41) | These work, but worker keys share a picker with 44 packet keys that no worker answers |
| Data | ProcessAttribute.Role (AbstractGameplayMonoProcess.cs:223) | Matches `PROJECTILE_SPEED`. It is also written as a set-by-caller key (:93-100), while `SetByCallerScaler.Key` has no context |

### 2.3 Authored tag fields with no context (Runtime)

| Field (file:line) | What it holds | Should be |
|---|---|---|
| AbilityTrigger.Tag (AbilityTrigger.cs:64) | the event, or the tag gained | Trigger (+ Granted for Tag Added). Its picker offers every tag |
| ThresholdAnalysisWorker.Event (AbstractAnalysisWorker.cs:123) | the event it sends | Trigger |
| CrowdControlRule.Tag (CrowdControlRules.cs:22) | the tag its holder carries, as Stunned | Granted |
| CrowdControlRule.AbilityTags (:28) | an ability's asset or context tag | Asset Id + Context Id |
| AbilityDefinition.IgnoresCrowdControl (AbilityDefinition.cs:50) | crowd control rules' tags | Granted |
| ResolveOrderEntry.Tag (ResolveOrder.cs:15) | a tag an entity carries | Granted (+ Asset Id) |
| TagWorkerRequirementPacket.Tag (AbstractTagWorker.cs:99) | require/avoid weight on the holder | Granted |
| AbstractContextImpactWorker.ImpactAbilityContextTags (:44) | the source's context tags | Context Id (its twin ValidContextTags has it) |
| DataWrapper.Key (Ability.cs:308): every asset's Local Data | a Local Data key | Data (or a Local Data context); LocalDataKind.Key reads these |
| DataWrapper.tagValue (Ability.cs:318) | a tag value | No context is fine |
| SetByCallerScaler.Key (SetByCallerScaler.cs:31) | a set-by-caller key | Data, or a Set By Caller context shared with ProcessAttribute.Role |
| TagAttributePair.Key (AbstractGameplayMonoProcess.cs:202) | a process role | Same as ProcessAttribute.Role |
| CreateMonoProcessAbilityTask.DataTag (:19) | a packet key | Data |
| AbilityBehaviour.ImplicitTag (AbilityBehaviour.cs:23) | a packet key | Data |
| ApplyEffectsAbilityTask.RecordLandedAs (:19) | a flag in the activation's data | Stage (or Data), shared with the next row |
| AbilityStageCondition.Tag (AbilityBehaviour.cs:117) | that flag, or a tag on the caster or target | Stage + Granted |
| GameplayPipelineConfiguration.AdditionalGrantedTags; EffectImpactConfig.AffiliationList, .ImpactTypes (AbstractEffectConfig.cs:143,176,179) | pipeline config | Granted; Affiliation; Impact |
| LabRules.Stops, .Hides, .Shields (Runtime/Lab/LabRules.cs:82,85,88) | states | Granted |
| LabRules.AttackRangeKey, .CastOrderKey, .CastWhenKey, .ReachKey (:92,102,105,108) | Local Data keys | Data |
| SetTagsTask.Tags (DataLoadingAbilityTasks.cs:136) | tag values | No context is fine. But `[SerializeReference]` on a `Tag[]` of structs is suspect; check that it serializes |

These are runtime-only and not authored, so they don't need a context:
- `SequenceAnnotation.AppliedEffectsTag`
- `AssetLoaderInfo.DataTag`
- `RuntimeEntityIdentity`'s fields
- `CachedAttributeValue` / `RetainedGroup`
- `CompositeBehaviourPacket.Command`
- `AbilityReader.target`
- the readonly event and lab structs

### 2.4 Where the two ends disagree, and what is never scanned

| Kind | Written by | Read by | Effect |
|---|---|---|---|
| Local Data keys | DataWrapper.Key (none) | LocalDataKind.Key (Data); AbstractTagValidationRule.Tag | An asset's own keys, such as the Demo Volley's "Demo.Volley.Radius", aren't offered where they're read |
| Set-by-caller | ProcessAttribute.Role (Data) | SetByCallerScaler.Key (none) | |
| Events | ThresholdAnalysisWorker.Event (none); the core (`Tags` Trigger) | AbilityTrigger.Tag (none); CombatLog.Events (Trigger) | A project's events never reach Trigger pickers |
| Crowd control | CrowdControlRule.Tag (none); effects' granted tags (Granted) | IgnoresCrowdControl (none) | |
| Stage flags | RecordLandedAs (none) | AbilityStageCondition.Tag (none) | The Stage context goes unused |
| Effects by tag | asset tag, context tags, granted tags | Removes/Blocks/EndsEffectsTagged, TrackedImpactKind (Context Id); AssetPartKind (all three); EffectModifierRule (Asset Id) | One matcher, three different context sets |
| Abilities by tag | asset tag, context tags | CancelledBy/Blocks (Context Id); CrowdControlRule.AbilityTags (none) | |
| Packet keys | `Tags` Data; SetAssets tasks (Data) | AutoTargetFromData (Data); CreateMonoProcess.DataTag, ImplicitTag (none) | |

**Only five asset types are scanned.** `ForgeTagRegistry.ScanTypes` (`ForgeTagRegistry.cs:286-294`) lists Ability,
GameplayEffect, EntityIdentity, Attribute and AttributeSet. These are never scanned:
- **Item** (`Item.cs:12`, a BaseForgeLevelProvider)
- **RequirementTemplate**
- **CrowdControlRules**
- **ForgeProject** (roles, resolve order, combat log events)
- **EffectModifier**
- **ScalerAsset**
- **ValuePipeline**
- **LabRules**
- **prefabs** (process attributes)

The validator reads more: `ForgeValidator.cs:136-142` covers every BaseForgeAsset, ScalerAsset and RequirementTemplate,
and `ForgeLint.cs:100-106` adds ValuePipeline and EffectModifier.

A pick in one of these types is registered in memory only (`TagDrawer.cs:152-153`). It's dropped at the next rescan,
which any `.asset` import triggers (`ForgeTagRegistry.cs:1171-1196`), including saving that same asset. New Tag
declares the tag in a library only when its contexts differ from the field's (`ForgeNewTagWindow.cs:100`).

**Some fields inside scanned assets are skipped too.** The walk follows each field's declared type
(`ForgeTagRegistry.cs:940-974, 1057-1089`), so it skips:
- **interface-typed `[SerializeReference]` fields:**
  - `Ability.SourceActivationRules` and `.TargetActivationRules` (`Ability.cs:18-27`)
  - `StagePolicy`
  - `InjectAbilityTask.Injection`
- **fields typed by a base class without `[Serializable]`:** `AbilityBehaviour.Targeting` is
  `AbstractTargetingAbilityTask` (`AbstractTargetingAbilityTask.cs:9`), so `AutoTargetFromDataTask.SourceKey` is never
  scanned.

ForgeLint walks a `SerializedObject` instead, which sees all of these (`ForgeLint.cs:153-179`).

---

## 3. `Tags.cs` (`Runtime/PlayForge/Tag/Tags.cs`)

### 3.1 What it defines

It defines 72 static `Tag` properties (lines 17-176). `Tags.Direct.cs` adds four aliases: IgnoreRetention,
DefaultRetention, AllowImpact and DisallowImpact. All 72 display texts are distinct, including case-insensitively. In
the table, "used" counts references in Runtime, Editor and Samples~ code outside `Tags.cs`.

| Section (lines) | Tags (display text) [context] | Read by code |
|---|---|---|
| Special (17-34) | SYSTEM "System" [System, Affiliation, Debug]; GAME_ROOT "Game Root" [none]; NONE "None" [none]; ALLOW "Always Allow", DISALLOW "Never Allow", RETURNED "Returned" [Impact]; DEBUG "Debug" [Debug, Texture] | SYSTEM and GAME_ROOT are unused. NONE has 3 uses. DEBUG appears only in a sample, as a data key (`Samples~/Code Examples/TaskSequenceExamples_Mechanics.cs:154`) |
| Base (40-50) | IGNORE "Ignore", DEFAULT "Default", ADDITIONAL "Bonus" [Impact Retention]; PRIMARY "Primary" [Texture] | Used; ADDITIONAL only in samples and assets |
| Classification (56-60) | ALWAYS_VISIBLE "Always Visible", NOT_VISIBLE "Not Visible" [Visibility] | ALWAYS_VISIBLE is unused. NOT_VISIBLE is used by the Lab and samples only |
| Data (66-82, 126-156, 166) | 44 [Data] keys: Transform, Position, Rotation, Derivation, Affiliation, Source, Target, TargetReal, TargetPosition, EventSubject, EventImpact, Data, Assets, Effects, Abilities, Entities, Attributes, AttributeSets, Tags, ProjectileSpeed… | **24 are read by no code:** GameplayAbilitySystem, Index, Object, GameObject, Process, Delta, Radius, Duration, Distance, Center, Origin, Dimensions, Vector, Vector2, Vector3, Height, WaitFor, Iterations, Valid, Invalid, Camera, ProcessRelay, Container, UI |
| Roles (90-96) | ROLE_HEALTH "Health", ROLE_LUCK "Luck", ROLE_MOVEMENT_SPEED "Movement Speed" [Role] | Used |
| Events (101-125) | Impact Dealt, Impact Taken, Kill, Died, Revived, Blocked, Evaded, Hit Blocked, Hit Evaded [Trigger] | Used |
| Functional (163-166) | TARGETED_INTENT "TargetedIntent" [none]; PROJECTILE_SPEED "ProjectileSpeed" [Data] | Both used. TARGETED_INTENT is a packet key with no context; PROJECTILE_SPEED's doc calls it a role |
| Report (173-176) | FAILED_TO_INITIALIZE, FAILED_WHILE_ACTIVE [none] | Process statuses (`AbstractGameplayMonoProcess.cs:194-195`) |

### 3.2 How they're made

Each tag is made by `Tag.GenerateAsUnique(text)` (`Tag.cs:83-88`), which calls
`TagRegistry.TagUtil.GenerateDeterministicTag(text, "", 64)` (`TagRegistry.cs:564-607`):

- The **name** is the text, padded to exactly 64 characters with characters chosen from SHA-256(text).
- The **display name** is the text itself.
- The result is memoized under `input + prefix` and registered in the runtime TagRegistry.
- Each tag is made once, as the type initializes (comment at lines 9-10, GAS-P4).

So a core tag's identity is a 64-character string, such as "HealthwlgkU1_U0ptONBl…". New Tag produces the same string
for the same text (`ForgeNewTagWindow.cs:97`). Asset tags, by contrast, are plain "Type.Name" strings
(`ForgeAssetCreation.cs:123-136, 181-189`), and project code and tests use plain `Tag.Generate`.

### 3.3 Robustness problems

1. **Core tags alias designers' tags.** New Tag "Health", "Kill", "Blocked", "Target", "Source", "Position", "None",
   "Data", "Debug", "Primary", "Default" or "Ignore" *is* the core tag. Nothing warns: `ForgeNewTagWindow.ProblemOf`
   (106-111) checks only the path's characters. So a designer's granted "Blocked" state is the core Blocked event, and a
   packet key "Target" that a Set Assets task writes is the key the activation's target is stored under. The core names
   carry no namespace, such as "PlayForge.", to keep them apart.
2. **The same text can be two tags, and nothing says so.**
   - `Tag.Generate("Health")` (plain, as Proving Grounds, the Demo and Arcade Fighter all write) reads "Health" but
     isn't `Tags.ROLE_HEALTH`. The picker then shows two "Health" rows (`ForgeTagPicker.cs:101-107` removes duplicates
     by name, not display text).
   - A designer's New-Tag "State.Staggered" isn't AF's `CombatTags.Staggered`, which is plain. The effect simply never
     matches.
3. **The text is the identity.** Changing the text (ADDITIONAL's "Bonus", or the PascalCase names) or the padding
   algorithm (PaddingChars, N, the hash) makes a new tag. Assets keep the old 64-character name: IGNORE alone has 152
   references. No golden test pins any padded name.
4. **Long paths are truncated, and the memo key is ambiguous.**
   - A text of 64 or more characters is cut to 64, so two long paths that share their first 64 characters become one
     tag (`TagRegistry.cs:580-585`). For example, "Status.Debuff.Elemental.Fire.Burning.DamageOverTime.Stacking.Tier1"
     and "…Tier2" become the same tag (checked). New Tag doesn't refuse long paths.
   - The memo key `input + prefix` (`TagRegistry.cs:568`) is ambiguous and ignores N.
   - Neither affects `Tags.cs` today.
5. **A core tag can't be a parent** (§5.1). Nothing a designer makes in New Tag sits under a `Tags` tag. In code,
   `Tags.DIED.Child("Fire")` gives "Died‹pad›.Fire", whose display name shows the padding (`Tag.cs:94-104`), against
   ED-67.
6. **Contexts:**
   - Five tags have no context, so the editor never offers them: GAME_ROOT, NONE, TARGETED_INTENT,
     FAILED_TO_INITIALIZE and FAILED_WHILE_ACTIVE.
   - SYSTEM appears in every Affiliation picker, and DEBUG in every Texture picker.
   - The 24 unused Data keys crowd every Data picker, worker keys and Local Data keys included.
   - PROJECTILE_SPEED's doc says it's a role, but its context is Data.
7. **Docs and names:**
   - FAILED_* reuse TARGETED_INTENT's doc comment ("the act of targeting", lines 172-176).
   - Several properties don't match their text: ADDITIONAL/"Bonus", PARENT_TRANSFORM/"Transform",
     SUBJECT/"EventSubject", IMPACT/"EventImpact", ALLOW/"Always Allow".
   - The styles are mixed: "Impact Dealt" against "TargetPosition".
8. **There are two kinds of "no tag".** `NONE` is a valid tag and `default(Tag)` isn't.
   `BaseForgeLevelProvider.GetAssetTag` returns NONE (`BaseForgeLevelProvider.cs:13`). `AbilityReader` tests
   `== Tags.NONE`, while its field starts as default (`AbilityReader.cs:10,14`).
9. **Static initialization:**
   - It's safe: no initializer reads another, so order doesn't matter, and nothing is lost to threads (the memo and
     the registry lock).
   - The one side effect is registration. `TagRegistry.Reset()` runs after every Proving Grounds and TestKit test
     (PG: Harness/World.cs:338; `TestKit/ForgeTestWorld.cs:398`) and leaves the core tags unregistered until
     `Initialize()` (`Bootstrapper.cs:49`).
   - Until then, `TagRegistry.Resolve` returns a tag whose display name is the padded name (`TagRegistry.cs:229-234`).

### 3.4 Tests

- Nothing pins `Tags.cs`. No test reflects over `Tags` to check that names are distinct, that each tag has a context,
  or that the padded names stay stable.
- Proving Grounds exercises 24 of the 72 through behaviour: TARGET_REAL has 36 references, ROLE_HEALTH 14, and the
  events.
- The hierarchy tests use plain `Tag.Generate` only (PG: Tests/Framework/GAS/TagTests.cs:66-122). The unique-tag test
  (ED-67, :209-235) checks only display text.
- The registry has two tests: ED-83's (PG: TheForgeTests.cs:988-1059) and ED-79's attribute check.
- Nothing tests `GetTagsForContext` matching, which assets the scan covers, or code tags.

---

## 4. Code tags and the editor registry

### 4.1 Today

Three places read code tags, and each reads `typeof(Tags)` only:

- **`ForgeTagRegistry.ScanContextDefaults`** (`ForgeTagRegistry.cs:280-284, 672-751`) reads static Tag members of
  `Tags` that carry `[ForgeContextDefault]`, plus any type passed to `RegisterContextDefaultSource` (1149-1156).
  Nothing calls that method in PlayForge, Proving Grounds or Arcade Fighter, and its list resets at every domain
  reload.
- **`ForgeLint.CoreTags`** (`ForgeLint.cs:194-200`) supplies the impact types and events the linter counts as dealt
  or sent.
- **`TagRegistry.RegisterBuiltInTags`** (`TagRegistry.cs:96-119`) is the runtime side. Runtime isn't the gap, though:
  a code tag registers as it's made (`Tag.cs:65-70`).

`[ForgeContextDefault]` is declared with `AttributeTargets.Property` (`ForgeAttributes.cs:148`). A `static readonly Tag`
field therefore can't carry it, and the field loop in `ScanTypeForContextDefaults` (725-748) can never find one.

**What a project does by hand now.** A code tag reaches a picker only by one of these routes:
- an asset of a scanned type already uses it (chicken and egg);
- a Tag Library entry has exactly the tag's name. That's easy for a plain name, but a unique name is a 64-character
  string;
- an `[InitializeOnLoad]` editor class calls `RegisterContextDefaultSource(typeof(MyTags))`, and the fields are
  rewritten as properties with `[ForgeContextDefault]`.

These code tags miss out today:
- AF's `CombatTags`: states, teams and impact types, as `static readonly` fields (AF: Scripts/Combat/CombatTags.cs).
- The Demo's Local Data keys (`Samples~/Demo/Scripts/Forge/DemoSequences.cs:15-23`).
- PG's `RecipeBook.Enraged` (Recipes/RecipeBook.cs:83).
- `DisjointProxyTaskBehaviour.IS_DISJOINTABLE`, a packet key no designer can pick (`DisjointProxyTaskBehaviour.cs:17`).

Meanwhile the linter warns that a filter on an impact type only code deals "never matches" (`ForgeLint.cs:321-386`).
No document explains any of this; the Documentation folder has no page on tags.

### 4.2 Ways to make it automatic

**A. Convention scan: nothing by hand.** On each domain reload:
- walk the player assemblies that reference PlayForge, tests excluded
  (`CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)`, matched to the loaded assemblies);
- take every public static Tag field and property;
- give each the contexts of its `[ForgeContextDefault]` (widened to fields), or else a "Code" context, which unmarked
  pickers and The Forge show.

Declared For (ED-83) then adds contexts with no code change.

- **For:** truly nothing to do, and it catches CombatTags, DemoSequences and RecipeBook as they stand.
- **Against:**
  - Reading a member runs that type's static constructor in the editor, with game statics and side effects; catch and
    skip.
  - An expression-bodied property makes a tag on every read.
  - It picks up public tags not meant for designers, unless they're filtered.
  - Without an attribute, the contexts are unknown.
  - It costs a pass per reload; cache the member list and re-read only the values.

**B. Opt-in marker.**
- Put `[ForgeTagSource(defaultContexts)]` on a static class, found with `TypeCache.GetTypesWithAttribute`. Each of its
  public static Tag members takes its own `[ForgeContextDefault]` or the class default.
- Add `TypeCache.GetFieldsWithAttribute<ForgeContextDefault>()` to find stray fields.
- Mark `Tags` itself.
- **For:** explicit, cheap (TypeCache is prebuilt), puts contexts where they belong, and runs no unexpected statics.
- **Against:** one attribute per class, so not quite "nothing by hand". A class that mixes kinds, as CombatTags mixes
  states, teams and impact types, still needs contexts on each member.

**C. Generated Tag Library.**
- After A or B, write a "Code Tags" library with entries {Name, DisplayName, Contexts, Note "CombatTags.Staggered"}.
- Write only when the set changes, and prune entries whose member is gone.
- **For:** it reuses the existing path (ScanTagLibraries, Declared For, The Forge), it's visible and diffable, and
  designers can adjust contexts there.
- **Against:**
  - Asset churn and merge conflicts.
  - A write is an import, which re-dirties the registry; it needs a guard against loops.
  - Contexts end up in two places.
  - FEAT-34's filter hides a library outside the active project (`ForgeProjects.cs:86-97`), so the library must live
    in the project folder or be exempted.
  - A rename leaves stale names in assets.

**D. A Roslyn source generator** that emits a manifest per assembly would work at compile time and at runtime. It's
heavy for this.

**What changes in any case:**
- `ForgeTagRegistry.cs:280-284` and `672-751`: discovery replaces `ContextDefaultSourceTypes`.
  `RegisterContextDefaultSource` can stay as an extra.
- `ForgeAttributes.cs:148`: Field as well as Property.
- `ForgeLint.cs:194-200` and `TagRegistry.cs:96-119`: the same source list. Runtime can't use TypeCache, so it needs
  plain reflection or a baked list.
- `ForgeNewTagWindow.Create` (87-103): offer the existing tag when a path matches a code tag's display text, because of
  the plain/unique split (§3.3.2).

**Suggested:** A, with a "Code" context and per-member `[ForgeContextDefault]` on fields, plus an opt-out attribute to
hide internal keys. Contexts are then set through Declared For, which already persists them, so C isn't needed.

---

## 5. Other fragility in the tag and context system

1. **Unique tags have no children** (`Tag.cs:223-233`; `ForgeNewTagWindow.cs:97`; `TagDrawer.cs:122`). Checked:
   "Status.Debuff" becomes "Status.Debuff7zBvlt5J…" (64 characters) and "Status.Debuff.Burn" becomes
   "Status.Debuff.BurnOJZd…" (64). The child is no longer, and doesn't start with the parent's name. These all fail for
   hierarchies made in New Tag:
   - every IncludeChildren or IncludeParents match;
   - `GetParent`/`GetAncestors`, which return plain tags;
   - the TagCache's listeners on a parent (`TagCache.cs:675-680`);
   - `ForgeLint.Under`.

   Meanwhile the Debugger's tree is built from display paths (ED-60) and shows them nested. "Make It a Child Of…" also
   builds from the *display* path, so under an asset tag (display "CullingBlade", name "Effect.CullingBlade") it makes
   "CullingBlade.X", which sits under nothing. The samples and test data hold no padded hierarchy: every 64-character
   name in them is a `Tags.cs` tag, so nothing exercises this.
2. **Matching by substring** (`ForgeTagRegistry.cs:353-363`, `kvp.Key.Contains(ctxStr)`):
   - Impact pickers offer Ignore, Default, Bonus and every retention group.
   - A future Category field would take Requirement Category, undoing ED-79.
   - Attribute would take AttributeSet (latent: no field names Attribute).
   - A project context such as "Data Key" would capture Data.
   - The match is ordinal and case-sensitive, so "impact" written by a project matches nothing.
3. **Fields with several contexts leak their picks.** A tag picked in TagQuery.Tag is filed under
   "Asset Identifier+Granted+Required", which the substring match then finds from every Granted, Asset Identifier and
   Required picker. So an effect's asset tag, once picked in a requirement, appears in every Granted Tags picker.
4. **Registration depends on the path.**
   - A pick registers under the field's attribute, or under none.
   - A rescan uses the attribute or a guess from the field's name (`ForgeTagRegistry.cs:978-1017`).
   - Unregistering takes the asset out of the whole context, not just that one field (`:493-514`, `:235-238`).
5. **Display texts can be ambiguous.** An asset tag reads as its bare name, so an ability and its effect read alike:
   PG's Dota data has "GodsStrength" for both Ability.GodsStrength and Effect.GodsStrength. With the plain/unique split
   (§3.3.2), a picker can show two identical rows.
6. **The same thing is typed two ways.**
   - Libraries are found by `t:{typeof(ForgeTagLibrary)}`, namespace-qualified (`ForgeTagRegistry.cs:782`), and by
     `t:{nameof(ForgeTagLibrary)}` (`ForgeTagContexts.cs:86`). ED-83's test, which depends on the first form, is
     recorded as verified, so it appears to work in 6000.0.
   - Context values differ from constant names (§1).
   - A test asserts the literal "Requirement Category" (PG: RequirementTemplateTests.cs:51).
   - Changing a context's value orphans every library declaration of it, since entries store plain strings.
7. **Dead or misleading API:**
   - `ForgeTagDefault(Tag)` can't be used, because no attribute argument can be a struct.
   - `ForgeTagRequired`, `ForgeTagPrefix` and `ForgeTagGroup` are never read.
   - `ForgeTagRegistry.GetAssetDisplayName` (1095-1139), `GetSystemDefaultTagsForContext` and `GetAllContextKeys` are
     unused.
   - No field uses `AllowCreate`, `IncludeUniversal`, `ForgeTagNoCreate`, `ForgeTagExcludeUniversal` or
     `ForgeTagReadOnly`.
8. **Every save rescans everything.** Any `.asset` import dirties the registry, and the next picker opening rescans,
   by reflection, every asset of the five types plus every library (`ForgeTagRegistry.cs:612-645`).
9. **The runtime registry pads parents.** `TagRegistry.RegisterInternal` registers a missing parent as
   `GenerateAsUnique(parentPath)` (`TagRegistry.cs:189-192`), so its tree holds padded parents beside the plain ones
   `GetParent` returns. `FromSegments` (623-629) still makes unique tags, contrary to GAS-17.
10. **`IsValidPath` allows spaces** (`TagRegistry.cs:647`), while New Tag's message says "letters, numbers and
    underscores" (`ForgeNewTagWindow.cs:109`). `Tags.cs` uses spaces.
