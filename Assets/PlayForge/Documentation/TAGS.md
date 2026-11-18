# PlayForge — Tags

A **tag** is a name in a tree, as `State.Stunned` under `State`, and PlayForge names with tags whatever it matches by
name: what an entity carries, as Stunned while a stun lasts; what an ability, an effect or an item is, by its asset tag
and its context tags; an entity's sides; a gameplay event, a kind of impact, a cue and an attribute's role; and the keys
of an activation's data and of an asset's Local Data. Requirements, crowd control, conditions and workers read the tags
an entity carries. This page covers what a tag is, how a project makes, declares and finds its tags in the editor, how
an entity holds its tags and tells their changes, and what reads them.

## What a tag is

A tag, a `Tag`, is a path of names joined by dots, parents first: `Status.Debuff.Burn` is under `Status.Debuff`, which
is under `Status`, its root. It's a struct an asset saves as two strings:

- `Name`, which tells it apart: two tags are equal, by `==` or `Equals`, exactly when their names are.
- `DisplayName`, how it reads. `ToString()` gives it, or the name where it has none, and PlayForge's windows show a tag
  so.

A tag is of one of three makes:

- **Unique**, as New Tag makes one ([New Tag](#new-tag)): its name is its path padded to 64 characters by a hash of
  it, as `Status.Debuff.BurnQx3…`, and its display name is the path. One path always gives one name, so the padding
  only keeps it apart from the plain tag of its path. PlayForge's own tags are unique
  ([PlayForge's own tags](#playforges-own-tags)).
- **An asset's own tag**, which its Name gives, as `Ability.FireBall` for an ability named Fire Ball
  ([ABILITIES.md](ABILITIES.md#identity)): its name is its whole path, and its display name its leaf, FireBall.
- **Plain**, as code makes one of a text, `Tag.Generate("Status.Debuff.Burn")`: its name and its display name are both
  the path.

`IsValid` says whether a tag has a name; an empty tag field holds `default(Tag)`, which has none.

### Its place in the tree

A tag's `Path` is what its family goes by: a unique tag's display name, and any other tag's name. From it come `Depth`,
1 for a root, and `IsRoot`; `GetLeafName()`, Burn; `GetParentPath()`, Status.Debuff; `GetAncestorPaths()`, from its
parent to its root; and `GetSegments()`. `IsChildOf(tag)` asks whether its path is under another tag's, at any depth,
and `IsDirectChildOf(tag)` one level under; `IsParentOf(tag)` asks the reverse; `MatchesOrIsChildOf` and
`MatchesOrIsParentOf` take the tag itself too; and `SharesAncestorWith` asks whether two tags have one root.

A family goes by paths, and equality by names. Status.Debuff.Burn is under Status.Debuff whichever makes the two are,
but the plain Status.Debuff and the unique one are two tags, and neither is under the other.

### Showing a tag

Show a tag by its display name, with `ToString()`, `GetName()` or an interpolated string, `$"{tag}"`. Its `Name` is
padded where it's unique, and a tag converts to its `Name` where a string is asked for, as in `string text = tag;` or
`"Stunned by " + tag`.

### The registry

`TagRegistry` keeps the tags made as the game runs, by name: making a tag registers it, and a Bootstrapper registers
PlayForge's own as it starts, `TagRegistry.Initialize()`. `TagRegistry.Resolve(name)` gives the registered tag of a
name, with its display name, or else the plain tag of the name, as a saved name is read back.
`TagRegistry.IsValidPath(path)` says whether a text can be a tag's path: names of letters, digits, underscores and
spaces, joined by single dots, with no dot at either end. Its lookups of a family, `GetDirectChildren`,
`GetAllDescendants`, `GetParent` and `GetAncestors`, go by the registered names, and so miss a unique tag's family: read
that from the tag's own `Path`, with `IsChildOf`.

## PlayForge's own tags

`Tags` holds PlayForge's own tags, each a static property, unique, that the editor's pickers offer for its kind. Each
reads as its text, as `Tags.DIED` reads Died and `Tags.ROLE_HEALTH` Health.

| What they are | Tags | Context |
|---|---|---|
| The gameplay events the core sends | `IMPACT_DEALT`, `IMPACT_TAKEN`, `KILL`, `DIED`, `REVIVED`, `BLOCKED`, `EVADED`, `HIT_BLOCKED`, `HIT_EVADED` | Event |
| The roles PlayForge's features read | `ROLE_HEALTH`, `ROLE_LUCK`, `ROLE_MOVEMENT_SPEED` | Attribute Role |
| Impact types | `ALWAYS_ALLOW`, `NEVER_ALLOW`, `RETURNED` | Impact Type |
| Retention groups | `IGNORE`, `DEFAULT`, `BONUS` | Retention Group |
| An icon's slot | `PRIMARY` | Texture Slot |
| Whether an effect or an item shows | `ALWAYS_VISIBLE`, `NOT_VISIBLE` | Visibility |
| The role a projectile reads its speed by | `PROJECTILE_SPEED` | Set By Caller Key |
| The keys the core reads and writes an activation's or a process's data under | `SOURCE`, `TARGET`, `TARGET_REAL`, `TARGET_POS`, `POSITION`, `ROTATION`, `TRANSFORM`, `EFFECTS`, `EVENT_SUBJECT`, `EVENT_IMPACT`, `CUE`, `ASSET`, `SPEC` and more | Data Key |
| Keys for a project's own coded behaviours, of which PlayForge's code reads none | `INDEX`, `RADIUS`, `DURATION`, `DISTANCE`, `CENTER` and more | Data Key |
| A side | `SYSTEM` | Affiliation |
| The rest | `GAME_ROOT`, `NONE`, `DEBUG`, `FAILED_TO_INITIALIZE`, `FAILED_WHILE_ACTIVE` | Code |

`Tags.IgnoreRetention`, `DefaultRetention`, `AllowImpact` and `DisallowImpact` are other names for `IGNORE`, `DEFAULT`,
`ALWAYS_ALLOW` and `NEVER_ALLOW`.

The events are what an ability's triggers hear ([ABILITIES.md](ABILITIES.md#activation)), the roles what a Forge
Project fills with its attributes ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), and the keys what a cue's data packet holds
([CUES.md](CUES.md#data-packets)). A path New Tag is given that is one of these tags' text, as Died or Health, would
make that very tag, so New Tag says so and offers PlayForge's own with **Use It**; the Tags view renames none of them.

## Contexts

A context is a kind of tag, as Granted, Event or Local Data Key, and a tag field takes the kinds its `ForgeTagContext`
attribute names: its picker offers the tags of those kinds, and the Universal ones. A tag is of a context where:

- a field of that context uses it in one of the project's PlayForge assets; a field of several contexts makes it of
  each, alone, so each one's fields offer it;
- a Tag Library declares it for the context ([The Tag Library](#the-tag-library));
- the code that writes it exposes it for the context ([Tags written in code](#tags-written-in-code)).

`ForgeContext` names PlayForge's contexts, each a constant whose value is its name, as `ForgeContext.LocalDataKey` is
"Local Data Key":

| Context | Its tags | Fields that take them |
|---|---|---|
| Asset Tag | an asset's own tag | an asset's **Asset Tag**; with Context Tag, what names abilities or effects by their tags, as **Cancelled By**, **Blocks**, a crowd control rule's **Ability Tags** and a purge's **Kinds** |
| Context Tag | an asset's context tags, as Spell or Fire | **Context Tags**, an item's **Set** |
| Granted | a tag an entity carries while something grants it, as Stunned | **Granted Tags**, **Passive Tags**, **Active Tags**, a requirement's tag, a crowd control rule's **Tag**, the **Grant Tags** and **Wait For Tag** tasks, a tag worker's requirements |
| Affiliation | a side, as Team.Red | an entity's **Affiliation**, an effect's **Affiliations**, a Side Change's **Sides** |
| Visibility | whether an effect or an item shows | **Visibility** |
| Impact Type | a change's kind, as Damage.Fire | **Impact Types**, and the workers' type filters |
| Retention Group | a group an attribute keeps its changes under | **Retention Group** |
| Event | a gameplay event, as Died | a trigger's tag, the **Send Event** and **Wait For Event** tasks, a Threshold's **Event**, the combat log's **Events** |
| Attribute Role | a part an attribute plays, as Health | the Forge Project's **Roles**, an attribute cost's **Role** |
| Texture Slot | an icon's slot, as Primary | **Textures**, **Origin Texture** |
| Cue | a cue tag, as Cue.Hit.Fire | a Cue Tag cue, the Cue Library's routes, a Cue Receiver's **Variant** |
| Data Key | a key in an activation's or a process's data | the Set loaders' keys, **Implicit Tag** |
| Local Data Key | a key in an asset's Local Data | a Local Data row's **Key**, the Local Data number's **Key** |
| Worker Key | a key a worker answers a number under | the Worker number's **Key** |
| Set By Caller Key | a key a caller sets a value under | the Set By Caller number's **Key**, a process attribute's |
| Stage Flag | a flag a stage records in the activation's data | **Record As**, **Record Landed As**, a stage's **Condition** |
| Requirement Category | a Requirement Template's category | a template's **Category** |
| Code | a tag code exposes for no context | fields that name no context |
| Universal | a tag a Tag Library declares for no context | every field but one that leaves them out |

A field that names no context offers every tag, and the tags it holds are of no context.

### The project's own contexts

A contexts field, as New Tag's **Contexts** and the Tags view's **Declared For**, shows the contexts chosen, or None.
Pressing it opens every context, PlayForge's, those the project's fields name and the project's own, Universal first
and the rest by name, those chosen checked: choosing one adds it or takes it away. **New Context…**, last, names a
context of the project's own and chooses it: the first Tag Library keeps it, and every contexts field offers it from
then on. It refuses an empty name, and one a context has already, whatever its case. A field of the project's own takes
the context's tags with `[ForgeTagContext("Weather")]`, its name as written.

## The Tag Library

A **Tag Library** asset, `ForgeTagLibrary`, declares tags for contexts, so those contexts' pickers offer them before any
asset uses them, and keeps the project's own contexts. `Create → PlayForge → Tag Library` makes one, and PlayForge makes
one in the active Forge Project's folder as a tag is first declared, or a context first made, while there's none.

Its inspector, `ForgeTagLibraryEditor`, is the shared header over its two lists:

- **Entries**, each a `ForgeTagLibrary.Entry`, a declaration: **Name**, the tag's name, padded where the tag is unique;
  **Display Name**, how it reads, where that differs from its name; **Contexts**, those it's declared for, an empty list
  declaring it Universal; and **Note**, what it's for.
- **Contexts**: the project's own contexts.

The Tags view's **Declared For** and New Tag's **Contexts** write the entries, each change one Undo step, so a library
is seldom edited by hand; one edited by hand is read again as it's saved. A declaration goes into the library that
declares the tag already, or else into the first, by its path.

Every Tag Library in the Unity project is read, wherever it sits, and the contexts each keeps are every Forge Project's.
The tags it declares are offered by the pickers of the Forge Project whose folder holds it, and by another project's
only where that project's own assets use them.

## Tags written in code

Code makes a tag of a path with `Tag.GenerateAsUnique`, and offers it to the editor's pickers with `[ForgeTagExposed]`.

- `Tag.GenerateAsUnique("State.Stunned")` gives the unique tag of the path, the very tag New Tag makes of it,
  PlayForge's own among them: `Tag.GenerateAsUnique("Died")` is `Tags.DIED`.
  `TagRegistry.FromSegments("State", "Stunned")` makes the same tag of its parts.
- `Tag.Generate("State.Stunned")`, and a string where a tag is asked for, as `Tag tag = "State.Stunned";`, give the
  plain tag of the path, which no unique tag equals: use them for tags that code alone makes and reads.
  `Tag.Generate(name, displayName)` makes a tag of a name and a display name of its own.
- `tag.Child("Burn")`, `TagRegistry.CreateChild(tag, "Burn")`, `GetParent()` and `GetAncestors()` give plain tags of
  their paths, and so does `GetRoot()`, but of a root, which gives the tag itself.
- `TagRegistry.TagUtil` makes the names: `GenerateDeterministicTag(text, prefix, length)` a unique tag's, the prefix
  and the text padded to the length, 32 to 64, by a SHA-256 hash of them, or cut short there; and
  `GenerateUniqueRandomTag()` a random tag no registered tag has. `TagUtil.rngGenerationBufferSize`, 64, is the length
  New Tag pads to.

### Exposing them to the pickers

A static `Tag` field or property of the project's code, public or not, marked with `ForgeTagExposed` is offered by the
pickers of the contexts it names, as `[ForgeTagExposed(ForgeContext.Granted)]`, or, naming none, under Code, which the
fields that name no context offer. A code tag not marked stays out of the pickers, so a project chooses which of its
tags its designers see. The Tags view shows the contexts it's exposed for in **Declared For**, checked and locked, and
can declare it for others besides.

The editor finds the marks once each domain reload, in the player assemblies that reference PlayForge, test assemblies
left out. A tag written in an editor assembly reaches the pickers once its type is registered as the editor loads, as
from an `[InitializeOnLoad]` class: `ForgeTagRegistry.RegisterContextDefaultSource(typeof(EditorTags))`, its marked
members alone.

```csharp
using FarEmerald.PlayForge;

public static class GameTags
{
    // The tag New Tag makes of State.Stunned, offered by the fields that take granted tags
    [ForgeTagExposed(ForgeContext.Granted)]
    public static readonly Tag Stunned = Tag.GenerateAsUnique("State.Stunned");

    // Offered by the project's own fields of its Weather context
    [ForgeTagExposed("Weather")]
    public static readonly Tag Rain = Tag.GenerateAsUnique("Weather.Rain");

    // Exposed for no context: under Code
    [ForgeTagExposed]
    public static Tag Cheats { get; } = Tag.GenerateAsUnique("Game.Cheats");

    // Not exposed: a key the game's code alone writes and reads
    public static readonly Tag Combo = Tag.Generate("Game.Combo");
}
```

## Tag field attributes

The attributes a `Tag` field, or a list of tags, takes say what its tag field offers and does.

- `ForgeTagContext`, as `[ForgeTagContext(ForgeContext.Granted)]` or `[ForgeTagContext("Weather")]`, names the
  contexts the field takes, PlayForge's or the project's own, and makes the tags it holds of each. Its
  `IncludeUniversal`, true unless set, offers the Universal tags too, and its `AllowCreate`, true unless set, offers
  **New Tag…** and **Make It a Child Of…**. A field without one offers every tag.
- `ForgeTagNoCreate` offers neither, as `AllowCreate = false` does.
- `ForgeTagExcludeUniversal` leaves the Universal tags out, as `IncludeUniversal = false` does.
- `ForgeTagReadOnly` shows the tag greyed, with no picker and no menu; code still sets it.
- `ForgeTagTooltip`, as `[ForgeTagTooltip("...")]`, gives the field its tooltip, in place of Unity's `[Tooltip]`,
  which the field reads where it has none.
- `ForgeTagExposed` goes on a static tag of code, not on an asset's field
  ([Tags written in code](#tags-written-in-code)).

`ForgeTagDefault`, `ForgeTagRequired`, `ForgeTagPrefix`, `ForgeTagGroup`, `ForgeTagLockedCountAttribute` and
`ForgeTagLockedIndicesAttribute` are declared for a field as well, for a default tag, a tag the field must have, the
prefixes it takes, a group of fields and a list's locked entries, but no drawer reads them, so they change nothing in
the inspector; and `ForgeTagDefault` can't be written on a field at all, as a `Tag` can't be an attribute's argument.
`AvoidRequireTagGroupColor`, on the requirement groups' fields, gives each a colour that no drawer reads.

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

// Refuses its ability while its caster is out of the weather it names
[Serializable]
public class NeedsWeatherAbilityComponent : AbstractAbilityComponent
{
    [Tooltip("The weather its caster must be under, or one beneath it")]
    [ForgeTagContext("Weather", AllowCreate = false)]
    [ForgeTagExcludeUniversal]
    public Tag Weather;

    public override string CanStart(AbilityComponentContext ctx)
    {
        var tags = ctx.Source?.GetTagCache();
        if (!Weather.IsValid || tags == null || tags.HasTag(Weather, ETagMatchMode.IncludeChildren)) return null;
        return $"Needs {Weather}";
    }
}
```

## The tag field

Each `Tag` field, alone or in a list, draws as a tag field, `TagDrawer`, which shows its tag by its display name and
picks another from the tags its contexts offer. The field shows its label, or none in a list, then the tag's parents
in secondary text and its leaf in strong text, or its whole path while **Show Full Tag Paths** is on; None for an empty
field, and a dash where the selected assets' tags differ.

### The picker

Pressing the field opens its picker: **None** first, then **New Tag…** where the field may make tags, then the tags it
offers by their paths, in natural order, each under a folder of its root while **Group Tags by Root** is on and the root
has more than one tag. The field's tag is checked. The search finds a tag by any part of its path, and while a tag is
highlighted the picker's Context says the contexts it's for, or that it's for none yet.

A field offers the tags of its contexts ([Contexts](#contexts)) and the Universal ones, of the active Forge Project:
PlayForge's own and those code exposes, those the project's assets use, and those a Tag Library in its folder declares.
A tag only another project uses, it leaves out. A pick writes the tag, its name and its display name, into each
selected asset, as one Undo step.

### Its menu

The field's right-click menu holds **None**, which empties it; **New Tag…**; **Make It a Child Of…**, which picks
another tag and puts the field's leaf under it, as Burn under Status.Debuff makes Status.Debuff.Burn; and **Copy Path**,
which copies the tag as the field reads it. New Tag… and Make It a Child Of… show where the field may make tags.

### New Tag

**New Tag** asks for the new tag's **Path**, which starts as the field's tag's parents, and its **Contexts**, which
start as the field's. **Create**, or Enter, makes the tag, unique, and gives it to the field; a context chosen beyond
the field's declares the tag for it in the Tag Library, so that context's fields offer it too. It refuses an empty path;
one of anything but letters, digits, underscores and spaces in names joined by single dots; and one of 64 characters or
more, which the padding would cut short, so it could be another tag's. A path that is one of PlayForge's own tags' text,
as Died or Health, would make that very tag: New Tag says so, and **Use It** gives the field PlayForge's own.

### Preferences

**Preferences → PlayForge** holds each user's own, under **Tags**: **Show Full Tag Paths** (off), a tag's whole path in
strong text in place of its parents before its leaf, in the tag fields and The Forge's Tags view; and
**Group Tags by Root** (on), the picker's tags under their roots. Its **Menu Width** and **Menu Height** are the most
the picker grows to.

## The Tags view

The Forge's **Tags**, in its toolbar (`Window → PlayForge → The Forge`), lists the active Forge Project's tags and what
uses each, declares a tag for contexts, and renames it. The list holds the tags the pickers offer, each by its leaf, or
its whole path while **Show Full Tag Paths** is on; **For**, the contexts it's for; and **Uses**, how many of the
project's assets use it, a Tag Library among them. Each column sorts, and the search finds a tag by any part of its
path.

A tag selected shows in the detail pane: its leaf, with its whole path under it, and its padded name, where it's
unique, on hover; its **For** and its **Uses**; **Declared For**, the contexts its Tag Library entry declares it for,
with those its code exposes it for checked and locked; and a row for each of the project's assets that uses it, with the
fields that do, which shows that asset in The Forge. Choosing a context in **Declared For** declares the tag for it at
once, one Undo step; taking every one away takes the declaration away, so the tag is for the contexts its uses are in
alone.

### Renaming a tag

A row's **Rename…**, in its right-click menu, or F2, asks for a tag's new **Path**, then renames the tag, and every tag
under its path with it, in every ability, effect, item, entity, attribute set, attribute, Scaler asset, Value Pipeline
and Requirement Template that stores one, and in the Tag Library entries that declare them, as one Undo step. A unique
tag stays unique, and a plain one plain. Nothing else follows: code, scenes and prefabs, a Forge Project's settings,
Crowd Control Rules, Effect Modifiers, a Cue Library and the Creator Lab's rules keep the old name.

It refuses a path a tag has already and one of PlayForge's own tags' text, and it renames no tag that is, or has under
it, one of PlayForge's own or an asset's own tag.

### Asset tags follow their names

An asset's own tag follows its name. Renaming an ability, an effect, an item, an entity or an attribute set, by its
**Name**, F2 in The Forge or its asset menu's **Rename…**, stores the tag its new name gives and carries it into each
asset a tag's rename reaches that stored the old one ([Renaming a tag](#renaming-a-tag)), unless another asset still has
the old tag as its own, as the asset it was duplicated from does. Where an asset's stored tag isn't the one its name
gives, its Identity section shows it in warning text with **Update**, and the Problems view warns of it, under "Asset
tags their names don't give", with the quick fix **Store the Tag**; each stores the tag and carries it the same way.

## How an entity holds tags

An entity carries the tags its sources grant it, each with a weight: the number of grants of it the entity holds now.
Its sources, as its tags are compiled:

- its identity: its asset tag, its sides and its **Granted Tags**. Its sides are its **Affiliation**, or those its
  registration or `SetAffiliation` gave it, or a Side Change's while one lasts ([ENTITIES.md](ENTITIES.md#tags));
- its attribute set's asset tag, and each sub-set's;
- each effect on its shelf: its asset tag, its **Granted Tags**, and those its modifiers add
  ([EFFECTS.md](EFFECTS.md#tags)). An Instant effect grants its Granted Tags only as it applies;
- each ability it knows: its asset tag and its **Passive Tags**, and its **Active Tags** while an activation of it runs
  ([ABILITIES.md](ABILITIES.md#tags));
- each item it holds: its asset tag, and its **Granted Tags** while it's equipped ([ITEMS.md](ITEMS.md#tags));
- its loose tags.

An asset keeps its tags in its Tags section, an ability's an `AbilityTags`, an effect's a `GameplayEffectTags` and an
item's an `ItemTags`, and an entity's are fields of its `EntityIdentity`. **Context Tags** describe an asset, and are
granted to no one. A tag two sources grant weighs 2, as two burns that each grant Status.Burning, and stays until both
are gone. An effect grants its tags once for each container on its shelf, not once a stack: a burn stacked five times in
one container grants Status.Burning once.

The entity compiles its tags as a source comes, goes or changes: as an ability is learned, starts or ends, an item is
given, equipped or taken, a loose tag comes or goes, its sides change, and an effect lands or comes off. A compile
rebuilds the weights from the sources and tells, as it ends, only what it changed
([Hearing tags change](#hearing-tags-change)).

### Loose tags

A loose tag is one granted to the entity directly, with nothing behind it, as Unreal's loose gameplay tags are.
`gas.AddLooseTag(tag)` grants one, each grant counted, and `gas.RemoveLooseTag(tag)` takes one grant back, false where
the entity has none; `gas.GetLooseTags()` lists them, a tag once for each grant. A save keeps them
([SAVING.md](SAVING.md#what-a-save-holds)). An ability's **Grant Tags** task grants loose tags while its stage runs
([ABILITIES.md](ABILITIES.md#stages)), and the Debugger's Entities view adds them, and takes them back, by hand.

### Reading them

`gas.GetTagCache()` gives the entity's `TagCache`, its tags and their weights; a `GameplayAbilitySystem` component's is
null until it initializes.

- `GetWeight(tag)`: the weight of that very tag, **−1 where the entity hasn't it**, not 0.
  `TryGetWeight(tag, out weight)` says whether it has it, and `gas.GetTagWeight(tag)` is `GetWeight`.
- `HasTagExact(tag)`, and `HasTag(tag, mode)` by a match mode ([Querying tags](#querying-tags)); `HasAnyTag`,
  `HasAllTags` and `HasNoneOfTags` ask it of several, Exact unless told.
- `GetMatchingWeight(tag, mode)`: the weights of the tags that match, added up, 0 where none does; in Exact mode it is
  `GetWeight`, −1 for an absent tag. `GetMatchingTags(tag, mode)` gives the tags held that match.
- `GetAppliedTags()`: each tag held, once. The list is the cache's own, reused until a tag comes or goes: read it, and
  copy it to keep it. `gas.GetAppliedTags()` is the same list.

`AddTag`, `AddTags`, `RemoveTag`, `RemoveTags`, `RemoveTagCompletely` and `RemoveMatchingTags` change the weights
directly, as an Instant effect's tags are granted for the moment it applies, and are told as they happen; but the next
compile rebuilds the weights from their sources and drops them, so a tag meant to last is a loose tag. `ResetWeights()`
empties the weights, telling each tag's going, and `ResetCache()` resolves the active tag workers as well;
`BeginRebuild()` and `EndRebuild()` frame a compile. `LogWeights()`, `LogActiveWorkers()` and
`GetHierarchyDebugString()` write the cache out, for debugging.

### What holds and what grants

- `ITagHandler` is what carries tags, which every entity is: `GetAppliedTags()`, `GetTagWeight(tag)`,
  `QueryTags(query)`, against its weights, and `CompileGrantedTags()`, which compiles them now. A tag worker reads its
  entity through it.
- `ITagSource` is what grants tags: `GetGrantedTags()`. An asset gives what it grants of itself, as an ability its asset
  tag and its Passive Tags; what holds it on an entity gives what it grants there, as a known ability its Active Tags
  while it runs, and an effect on the shelf its asset tag with the rest.

The Debugger's Entities view shows an entity's tags with what grants each, and its Tags view every tag the entities
hold, as a tree, with who holds the one selected ([GETTING_STARTED.md](GETTING_STARTED.md#tooling-tour)).

## Hearing tags change

An entity's `TagCache` tells each change to a tag's weight, to listeners on that tag and on the tags above it. A change
is a `TagChange`: its `Tag`, and its weight `Before` and `After`, each 0 where the entity doesn't hold it, never −1;
`Added` says the tag came, and `Removed` that it went.

- `tags.Listen(tag, onChange, kind)` hears the tag, and every tag under its path, as a listener on Status hears
  Status.Burning, and gives a `TagListener`, whose `Dispose()` stops it. Its `ETagChangeKind` says what it hears:
  `AddedOrRemoved`, the default, a tag coming and going, or `AnyCount`, every change to a weight. A listener's `Tag`,
  `Kind` and `Listening` say what it hears and whether it still does.
- `tags.TagChanged` hears every change to every tag.

A change made straight to the weights is told as it happens. A compile is told as it ends, with only what it changed,
so a tag held throughout is told nothing, and a load's one compile tells what the load changed
([SAVING.md](SAVING.md#what-a-load-does)). A listener that throws is logged, and stops neither the change nor the other
listeners; one disposed while a change is told hears no more of it, and one added then hears the next. The entities'
guide listens for a stun too ([ENTITIES.md](ENTITIES.md#tags)).

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

public static class StatusDisplay
{
    // The tags New Tag makes of these paths
    static readonly Tag Stunned = Tag.GenerateAsUnique("State.Stunned");
    static readonly Tag Combo = Tag.GenerateAsUnique("Combo.Points");

    // Shows the icon while the entity holds State.Stunned or a tag under it; dispose the listener to stop
    public static TagListener ShowWhileStunned(IGameplayAbilitySystem gas, GameObject icon)
    {
        var tags = gas.GetTagCache();
        icon.SetActive(tags.HasTag(Stunned, ETagMatchMode.IncludeChildren));
        return tags.Listen(Stunned, _ => icon.SetActive(tags.HasTag(Stunned, ETagMatchMode.IncludeChildren)));
    }

    // Logs each change to the weight of Combo.Points, and of each tag under it
    public static TagListener LogCombo(IGameplayAbilitySystem gas) =>
        gas.GetTagCache().Listen(Combo, change => ForgeLog.Info($"{change.Tag}: {change.Before} -> {change.After}"),
            ETagChangeKind.AnyCount);
}
```

## Querying tags

A match mode, `ETagMatchMode`, says which of an entity's tags answer for the tag it's asked about.

- **Exact**: the tag itself.
- **Include Children**: the tag, or one under its path, as Status.Debuff.Burn answers for Status.Debuff.
- **Include Parents**: the tag, or one above its path, as Status.Debuff answers for Status.Debuff.Burn.
- **Same Root**: any tag of its root, as Status.Buff answers for Status.Debuff.Burn.

A `TagQuery` asks one thing of an entity's tags, as a requirement's entry and a Tag condition do: its `Tag`; its
`MatchMode`, Exact unless set; and how many must match, its `Operator`, an `EComparisonOperator`, against its
`Magnitude`. Greater Than 0, as a query starts, asks for the tag, and Equal 0 for its absence. Its inspector,
`TagQueryDrawer`, shows its **Tag**, its **Match** and its **Count**, the comparison and the number.
`TagQuery.HasTag(tag, mode)`, `DoesNotHaveTag(tag, mode)`, `HasExactCount(tag, count, mode)` and
`HasTagOrChildren(tag)` make the common ones.

What a query counts depends on what it's asked of:

- `Validate(cache)`, as `gas.QueryTags(query)` asks, counts weights: Exact reads the tag's weight, and the other modes
  add up the weights of the tags that match.
- `Validate(list)`, of a list of tags as `GetAppliedTags()` gives, counts the tags that match, each once.

`CountMatches` gives the count either way.

```csharp
using FarEmerald.PlayForge;

public static class StatusChecks
{
    static readonly Tag Debuff = Tag.GenerateAsUnique("Status.Debuff");
    static readonly Tag Bleed = Tag.GenerateAsUnique("Status.Debuff.Bleed");

    // Status.Debuff, or any tag under it
    public static bool IsDebuffed(IGameplayAbilitySystem gas) =>
        gas.GetTagCache().HasTag(Debuff, ETagMatchMode.IncludeChildren);

    // Bleed granted three times or more, by its weight
    static readonly TagQuery Bleeding = new TagQuery
    {
        Tag = Bleed,
        Operator = EComparisonOperator.GreaterOrEqual,
        Magnitude = 3,
    };

    public static bool BleedsHard(IGameplayAbilitySystem gas) => gas.QueryTags(Bleeding);
}
```

## Requirements

Tag requirements are the tags an entity must have, and must not have, for an ability to activate and run, or for an
effect to land, go on and come off. Each is a group, an `AvoidRequireTagGroup`, of two lists of tag queries:

- `RequireTags`, the inspector's **Requires**: each must pass.
- `AvoidTags`, its **Must not have**: none may pass.

A group passes when each query it requires passes and none it must not have does, so an empty group passes. Its
**Name**, optional, tells it apart where it's imported.

Groups come in sets, each an `AbstractTagRequirements`, which says whether any of its groups asks anything
(`HasAnyRequirements`), how many queries they hold (`TotalRequirementCount`) and whether one names a template
(`HasLinkedTemplates`):

- An effect has two `EffectTagRequirements`, one on its target and one on its source, each of three groups:
  **Applying** (`ApplicationRequirements`), which must pass for the effect to land; **While on**
  (`OngoingRequirements`), without which its ticks don't execute; and **Removed by** (`RemovalRequirements`), which
  takes it off once met, and asks nothing while empty ([EFFECTS.md](EFFECTS.md#requirements)).
- An ability has an `AbilityTagRequirements`: **Caster** (`SourceRequirements`), checked as it's asked to activate and
  again after targeting; **While active** (`WhileActiveRequirements`), the caster's for as long as it runs, checked as
  it's asked to activate and as the caster's tags change, which interrupts it as it fails; and **Target**
  (`TargetRequirements`), checked after targeting, where there's a target ([ABILITIES.md](ABILITIES.md#requirements)).
- A targeting task has a lone group, its **Requirements**, which the target it takes must pass.

A set draws as a table, `EffectTagRequirementsDrawer` or `AbilityTagRequirementsDrawer`: a row for each group, with a
cell for what it requires and a cell for what it must not have, each listing its tags or saying None. A lone group draws
as a table of one row, `AvoidRequireTagGroupDrawer`. A cell opens its list under the table, with the group's **Name**,
**Template** and **Sync With Template**, and closes it as it's pressed again.

### What a row counts

A row reads its entity's tags as a list, each tag once, whatever its weight, so its numbers count tags: Greater Than 0
asks for the tag, and Greater Than 1 under Include Children for two different tags under it. The ability's
**While active** row alone is read against the weights, as a Tag condition, the Tag Weight check and a tag worker are
([Conditions and checks](#conditions-and-checks)); to ask for stacks of one tag, as three grants of Combo.Points, use
one of those.

### Templates

A group's **Template** names a Requirement Template, `RequirementTemplate`
(`Create → PlayForge → Requirement Template`): a group of its own, its **Requirements**, with a **Description** and a
**Category**, a tag of the Requirement Category context, which groups templates. Its inspector,
`RequirementTemplateEditor`, is the shared header over those fields. **Sync With Template** marks the group as
following its template (`ShouldSyncFromTemplate`): `TrySyncFromTemplate()` copies the template's tags into a group that
follows it, `LinkToTemplate(template)` links one and copies it, and `UnlinkFromTemplate()` lets it go, keeping its
tags. A group checks its own tags.

### In code

`AddRequirement(tag, mode)` and `AddAvoidance(tag, mode)` add a query of the tag's presence, and
`AddRequirementWithChildren(tag)` and `AddAvoidanceWithChildren(tag)` one under Include Children; `RemoveTag(tag)` takes
a tag's queries out, and `Clear()` all of them. `Validate` passes or fails a group against a cache or a list, as a
query does, and `ValidateDetailed` says why, an `AvoidRequireTagGroup.ValidationResult`: its `FailedRequirements`, the
queries it requires that failed; its `TriggeredAvoidances`, those it must not have that passed; and `GetSummary()`.
`GetAllReferencedTags()` lists the tags a group names, `Clone()` copies it, and `CopyFrom(group)` takes another's.

```csharp
using FarEmerald.PlayForge;

public static class Landing
{
    static readonly Tag Grounded = Tag.GenerateAsUnique("State.Grounded");
    static readonly Tag Disabled = Tag.GenerateAsUnique("State.Disabled");

    // Grounded, and nothing under State.Disabled
    static readonly AvoidRequireTagGroup CanLand = Make();

    static AvoidRequireTagGroup Make()
    {
        var group = new AvoidRequireTagGroup { Name = "Can Land" };
        group.AddRequirement(Grounded);
        group.AddAvoidanceWithChildren(Disabled);
        return group;
    }

    public static bool Allows(IGameplayAbilitySystem gas, out string why)
    {
        var result = CanLand.ValidateDetailed(gas.GetTagCache());
        why = result.IsValid ? null : result.GetSummary();
        return result.IsValid;
    }
}
```

## Crowd control

Crowd control stops abilities by the tags their caster carries, so no ability has to list Stunned among the tags it
must not have. A Crowd Control Rules asset, `CrowdControlRules` (`Create → PlayForge → Crowd Control Rules`), holds its
**Rules**, each a `CrowdControlRule`: the **Tag** its holder carries, as Stunned, and what it **Stops**, an
`ECrowdControlStops`: **Every Ability**, as a stun, or **Abilities Tagged**, those whose asset tag or a context tag is
one of its **Ability Tags** or under one, as Silenced stops spells.

While the caster carries a rule's tag, or one under it, an ability the rule stops is refused, as Crowd Control, the
refusal's `Rule` naming the tag, and one running is interrupted. An ability's **Ignores Crowd Control** names the rules
it ignores, by their tags, a tag ignoring those under it too. An entity follows its own rules, its **Crowd Control**, or
else its Forge Project's: `gas.GetCrowdControl()` gives them, and `rules.Stopping(ability, tags)` the first rule that
stops an ability, or null. [ABILITIES.md](ABILITIES.md#crowd-control) has the rest.

## Conditions and checks

Besides requirements, conditions, ability checks, tasks and triggers read tags: an entity's, or an asset's.

- The **Tag** condition, `TagCondition`, holds where the caster or the target, as its **On** says, answers its
  **Query**, a tag query counted by weight, so a tag granted twice counts twice. Where the number's context lacks that
  entity, as a target's tag with no target, it doesn't hold. A Conditional number, an effect's Apply, Ongoing and
  Removal Conditions, an On Hit and an ability's Condition check read conditions
  ([SCALERS.md](SCALERS.md#a-magnitude-field)).
- The **Tag Weight** check, `TagWeightValidation`, among an ability's Caster or Target Checks, compares the checked
  entity's weight of its **Tag** with its **Weight**, by its **Comparison**, an entity without the tag holding it at
  0. A check of your own that reads a tag derives from `AbstractTagValidationRule`, whose `GetValue` gives its tag's
  weight on the checked entity, or, where that entity holds none of it, the ability's Local Data under the tag
  ([ABILITIES.md](ABILITIES.md#requirements)).
- A **Tag Added** trigger activates its ability as the entity gains a tag, or one under it, that it didn't have; a
  **Wait For Tag** task waits for its caster or its target to gain a tag, that very tag, or to lose it; and a stage's
  **Condition** runs the stage by a tag, in the activation's data or on the caster or the target
  ([ABILITIES.md](ABILITIES.md#stages)).
- An effect's **Removes Effects Tagged** and **Blocks Effects Tagged**, the Has Effect check's **Tagged** and the Asset
  Part number's Effects Tagged match effects, not entities: an effect answers for a tag by its asset tag, a context tag
  or a tag it grants, each the tag or under it ([EFFECTS.md](EFFECTS.md#tags)). `GameplayEffectTags.Names(tags, effect)`
  asks it in code, as `AbilityTags.Names(tags, ability)` asks whether an ability's asset tag or a context tag is one of
  the tags or under one, as Cancelled By and Blocks do.

## Tag workers

A tag worker, `AbstractTagWorker`, acts while its entity's tags meet its requirements, as flames shown while
Status.Burning weighs 3 or more. It's one of a Worker Group's **Tag** workers, its kind picked from the list, and its
entity holds it as it holds the group ([WORKERS.md](WORKERS.md#where-workers-come-from)).

Its **Tag Requirements** (`TagWorkerRequirements`) list entries, each a `TagWorkerRequirementPacket`: a tag, that very
tag; a policy, an `ERequireAvoidPolicy`, **Require**, which passes while the tag's weight is the entry's number or more,
or **Avoid**, while it's below it; and that number, read at the entity's level. The requirements are met where every
entry passes, and always where there are none. An absent tag weighs −1 here, as `GetWeight` gives it, so Require 1,
and Require 0 too, ask for the tag, and Avoid 1 for its absence.

Its hooks, each a virtual no-op, run at its entity's end of frame:

- `Activate(system)`, once, as its requirements come to be met;
- `Tick(system)` while they hold, after the activations: every frame at a **Tick Pause** of 0, every n + 1 frames at
  n, and never below 0, as at −1, the default; the pause is a number read at the entity's level;
- `Resolve(system)`, once, as they no longer hold, or at once where the worker is taken away while active, as its
  item is unequipped.

The entity reads its workers' requirements at the end of a frame in which its tags changed, a worker came or went, or
its last reading started or stopped one, and queues each activation and resolution on its action queue, ahead of the
ticks ([WORKERS.md](WORKERS.md#the-action-queue)). A worker whose `ValidateWorkFor(handler)` reads more than the
tags, as an attribute, overrides `EvaluateEveryFrame` too, to be read every frame. `TagWorkerActivateAction` and
`TagWorkerResolveAction` are the actions queued, at `ActionPriority.TagWorker`; the callbacks' `OnTagWorkerActivated`
and `OnTagWorkerResolved` hear each, and the frame's `FrameSummary` keeps them. `tags.IsWorkerActive(worker)`,
`ActiveWorkers` and `RegisteredWorkers` say which run on an entity.

One worker serves every entity whose group holds it, each tracking it apart, so it keeps nothing of one entity in its
fields; given by two sources, as two copies of an item, it runs once, and stays until the last takes it back. It
draws as `TagWorkerDrawer` draws a worker of any kind, its **Tag Requirements** a list (`TagWorkerRequirementsDrawer`)
of rows, each the tag, the policy and the number (`TagWorkerRequirementPacketDrawer`).

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

// Shows a child of its entity's object while the worker's Tag Requirements are met, as flames while
// Status.Burning weighs 3 or more
[Serializable]
public sealed class ShowWhileTagged : AbstractTagWorker
{
    [Tooltip("The child shown, by name, at any depth")]
    public string Child = "Flames";

    public override void Activate(IGameplayAbilitySystem system) => Show(system, true);

    public override void Resolve(IGameplayAbilitySystem system) => Show(system, false);

    private void Show(IGameplayAbilitySystem system, bool shown)
    {
        var body = system?.ToGASObject();
        if (body == null) return;
        foreach (var each in body.GetComponentsInChildren<Transform>(true))
            if (each.name == Child) each.gameObject.SetActive(shown);
    }
}
```

## The editor's tag registry

`ForgeTagRegistry`, in the editor, is what the tag pickers, the Tags view and New Tag know of the project's tags: each
tag, and each use of it, by context. It reads, as the editor loads, and again after any `.asset` file is imported, moved
or deleted (`TagRegistryAssetPostprocessor`):

- the code tags marked `[ForgeTagExposed]`: in `Tags`, in the player assemblies that reference PlayForge, and in each
  type given to `RegisterContextDefaultSource`;
- every Tag Library;
- every `Tag` field, list and array of the project's abilities, effects, items, entities, attributes, attribute sets,
  Scaler assets, Requirement Templates, Value Pipelines, Effect Modifiers, Crowd Control Rules, Forge Projects and
  Creator Lab rules, through their nested classes and `[SerializeReference]` fields, as their components and workers,
  each use under its field's contexts.

A tag that only a Cue Library, a scene, a prefab or an asset of the project's own type holds is offered nowhere until a
PlayForge asset uses it or a Tag Library declares it.

Tools of the project's own read it: `GetAllTags()`, `GetTagsForContext(contexts, includeUniversal)`,
`GetTagUsage(tag)`, `GetAllTagRecords()`, `GetAllContexts()` and `GetSystemDefaultTags()`, the code tags; `MarkDirty()`
has it read again as it's next asked, and `RefreshCache()` at once. A tag's uses are its
`ForgeTagRegistry.TagUsageRecord`, by context, each a `ForgeTagRegistry.ContextUsage` of uses, each a
`ForgeTagRegistry.FieldUsageInfo`: its `Asset`, its `FieldPath`, and whether it's code's (`IsSystemDefault`). A context
is a `ForgeTagRegistry.TagContext`, whose `ContextKey` is Uncontextualized for a field that names none. The tag field
tells the registry each pick, `RegisterTagUsage` and `UnregisterTagUsage`, so a picker knows it at once.

## Traps

Tags invite a few mistakes, each below with its way out.

- **Text makes a plain tag.** `"State.Stunned"` where a tag is asked for, and `Tag.Generate`, give the plain tag of the
  path, but New Tag made a unique one, and the two are different tags: neither equals the other, answers a query for it
  in any mode but Same Root, or is heard by the other's listener. Write `Tag.GenerateAsUnique("State.Stunned")`, or
  expose the tag from code so that designers pick that very one.
- **An absent tag weighs −1.** `GetWeight`, `GetTagWeight`, and `GetMatchingWeight` in Exact mode give −1 for a tag the
  entity hasn't got, not 0. Ask for more than 0, or use `TryGetWeight`.
- **A tag added to the cache doesn't last.** `TagCache.AddTag` is dropped at the next compile, as an ability starts or
  an item changes. Grant a lasting tag with `AddLooseTag`.
- **A requirement row counts tags, not weights.** An Exact entry's Greater Than 1 never passes in a row but While
  active. Ask for stacks with the Tag Weight check, a Tag condition or a tag worker.
- **A tag shows by `ToString()`.** Its `Name` is padded where it's unique, and it's the `Name` a tag converts to where
  a string is asked for.
- **A tag's relatives are plain.** `Child`, `GetParent`, `GetAncestors` and `GetRoot` make plain tags, which no
  unique tag equals. Compare families by path, with `IsChildOf` or a match mode.
- **A rename reaches assets, not code.** The Tags view renames what assets store, so a tag the project's code writes
  keeps its old text there: rename it in the code too.
- **The applied list is the cache's.** `GetAppliedTags()` changes as tags come and go; copy it to keep it.
