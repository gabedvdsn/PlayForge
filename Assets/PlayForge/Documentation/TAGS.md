# PlayForge — Tags

A **tag** is a name in a tree, such as `State.Stunned` under `State`. PlayForge uses tags to name whatever it matches by
name.

PlayForge names these with tags:

- what an entity carries, such as Stunned while a stun lasts;
- what an ability, an effect or an item is, by its asset tag and its context tags;
- an entity's sides;
- a gameplay event, a kind of impact, a cue and an attribute's role;
- the keys of an activation's data, and of an asset's Local Data.

Requirements, crowd control, conditions and workers read the tags an entity carries.

This page covers what a tag is, and how a project makes, declares and finds its tags in the editor. It also covers how
an entity holds its tags and tells their changes, and what reads them.

## What a tag is

A tag, a `Tag`, is a path of names joined by dots, parents first. `Status.Debuff.Burn` is under `Status.Debuff`, which
is under `Status`, its root.

A `Tag` is a struct that an asset saves as two strings:

| Field | Description |
|---|---|
| `Name` | What tells the tag apart. Two tags are equal, by `==` or `Equals`, exactly when their names are. |
| `DisplayName` | How the tag reads. `ToString()` gives it, or the name where it has none, and PlayForge's windows show a tag this way. |

A tag is of one of three makes:

| Make | Where it comes from | Its name | Its display name |
|---|---|---|---|
| **Unique** | New Tag makes one ([New Tag](#new-tag)). PlayForge's own tags are unique ([PlayForge's own tags](#playforges-own-tags)). | Its path, padded to 64 characters by a hash of it, as `Status.Debuff.BurnQx3…` | Its path |
| **An asset's own tag** | The asset's Name gives it, as `Ability.FireBall` for an ability named Fire Ball ([ABILITIES.md](ABILITIES.md#identity)). | Its whole path | Its leaf, FireBall |
| **Plain** | Code makes one of a text, as `Tag.Generate("Status.Debuff.Burn")`. | Its path | Its path |

One path always gives one unique name. So the padding only keeps a unique tag apart from the plain tag of its path.

`IsValid` says whether a tag has a name. An empty tag field holds `default(Tag)`, which has none.

### Its place in the tree

A tag's place in the tree comes from its `Path`, the text its family goes by. A unique tag's path is its display name,
and any other tag's path is its name.

These members read the path, here of `Status.Debuff.Burn`:

| Member | What it gives |
|---|---|
| `Depth` | Its depth in the tree, 1 for a root. |
| `IsRoot` | Whether it's a root. |
| `GetLeafName()` | Its leaf: Burn. |
| `GetParentPath()` | Its parent's path: Status.Debuff. |
| `GetAncestorPaths()` | Its ancestors' paths, from its parent to its root. |
| `GetSegments()` | The names in its path. |

These methods compare two tags by their paths:

| Method | What it asks |
|---|---|
| `IsChildOf(tag)` | Whether its path is under another tag's, at any depth. |
| `IsDirectChildOf(tag)` | Whether its path is one level under another tag's. |
| `IsParentOf(tag)` | The reverse of `IsChildOf`: whether another tag's path is under its own. |
| `MatchesOrIsChildOf` | As `IsChildOf`, but the tag itself matches too. |
| `MatchesOrIsParentOf` | As `IsParentOf`, but the tag itself matches too. |
| `SharesAncestorWith` | Whether the two tags have one root. |

A family goes by paths, but equality goes by names. So Status.Debuff.Burn is under Status.Debuff, whichever makes the
two are.

> [!NOTE]
> The plain Status.Debuff and the unique one are two tags, and neither is under the other.

### Showing a tag

Show a tag by its display name, with `ToString()`, `GetName()` or an interpolated string, `$"{tag}"`.

Don't show its `Name`, which is padded where the tag is unique. A tag converts to its `Name` where a string is asked
for, as in `string text = tag;` or `"Stunned by " + tag`.

### The registry

`TagRegistry` keeps the tags made as the game runs, by name. Making a tag registers it, and a Bootstrapper registers
PlayForge's own tags as it starts, with `TagRegistry.Initialize()`.

| Method | What it does |
|---|---|
| `TagRegistry.Resolve(name)` | Gives the registered tag of a name, with its display name, or else the plain tag of the name. A saved name is read back this way. |
| `TagRegistry.IsValidPath(path)` | Says whether a text can be a tag's path: names of letters, digits, underscores and spaces, joined by single dots, with no dot at either end. |
| `GetDirectChildren`, `GetAllDescendants`, `GetParent`, `GetAncestors` | The registry's lookups of a tag's family, by the registered names. |

> [!NOTE]
> The registry's family lookups go by the registered names, so they miss a unique tag's family. Read that from the
> tag's own `Path`, with `IsChildOf`.

## PlayForge's own tags

`Tags` holds PlayForge's own tags, each a unique tag in a static property. The editor's pickers offer each for its
kind.

Each reads as its text: `Tags.DIED` reads Died, and `Tags.ROLE_HEALTH` reads Health.

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

Four of them have other names:

| Other name | Same tag as |
|---|---|
| `Tags.IgnoreRetention` | `IGNORE` |
| `DefaultRetention` | `DEFAULT` |
| `AllowImpact` | `ALWAYS_ALLOW` |
| `DisallowImpact` | `NEVER_ALLOW` |

Three of the kinds come up in other guides:

- The events are what an ability's triggers hear ([ABILITIES.md](ABILITIES.md#activation)).
- The roles are what a Forge Project fills with its attributes ([ATTRIBUTES.md](ATTRIBUTES.md#roles)).
- The keys are what a cue's data packet holds ([CUES.md](CUES.md#data-packets)).

A path given to New Tag that is one of these tags' text, as Died or Health, would make that very tag. So New Tag says
so, and offers PlayForge's own with **Use It**. The Tags view renames none of them.

## Contexts

A context is a kind of tag, such as Granted, Event or Local Data Key. A tag field takes the kinds its
`ForgeTagContext` attribute names, and its picker offers the tags of those kinds and the Universal ones.

A tag is of a context where:

- a field of that context uses it in one of the project's PlayForge assets;
- a Tag Library declares it for the context ([The Tag Library](#the-tag-library));
- the code that writes it exposes it for the context ([Tags written in code](#tags-written-in-code)).

A field of several contexts makes the tag it holds of each of them alone, so each one's fields offer it.

A field that names no context offers every tag, and the tags it holds are of no context.

### PlayForge's contexts

`ForgeContext` names PlayForge's contexts. Each is a constant whose value is its name: `ForgeContext.LocalDataKey` is
"Local Data Key".

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

### The project's own contexts

A project can make contexts of its own, through any contexts field. A contexts field, such as New Tag's **Contexts**
or the Tags view's **Declared For**, shows the contexts chosen, or None.

Pressing the field opens every context: PlayForge's, those the project's fields name, and the project's own. Universal
comes first and the rest by name, with those chosen checked. Choosing one adds it, or takes it away.

To make a context of the project's own:

1. Press a contexts field.
2. Choose **New Context…**, last in the list.
3. Name the context.

The field chooses the new context, and the first Tag Library keeps it. Every contexts field offers it from then on.
**New Context…** refuses an empty name, and a name a context has already, whatever its case.

A field of the project's own takes the context's tags with `[ForgeTagContext("Weather")]`, the context's name as
written.

## The Tag Library

A **Tag Library** asset, `ForgeTagLibrary`, declares tags for contexts, so those contexts' pickers offer them before
any asset uses them. It also keeps the project's own contexts.

`Create → PlayForge → Tag Library` makes one. PlayForge also makes one in the active Forge Project's folder while
there's none, as a tag is first declared or a context first made.

Its inspector, `ForgeTagLibraryEditor`, is the shared header over two lists:

| Property | Description |
|---|---|
| **Entries** | The library's declarations, each a `ForgeTagLibrary.Entry`. |
| **Contexts** | The project's own contexts. |

Each entry declares one tag:

| Property | Description |
|---|---|
| **Name** | The tag's name, padded where the tag is unique. |
| **Display Name** | How the tag reads, where that differs from its name. |
| **Contexts** | The contexts the tag is declared for. An empty list declares it Universal. |
| **Note** | What the tag is for. |

You seldom edit a library by hand. The Tags view's **Declared For** and New Tag's **Contexts** write its entries, each
change one Undo step. A library edited by hand is read again as it's saved.

A declaration goes into the library that declares the tag already, or else into the first, by its path.

Every Tag Library in the Unity project is read, wherever it sits, and the contexts each keeps are every Forge
Project's.

The tags a library declares are offered by the pickers of the Forge Project whose folder holds it. Another project's
pickers offer them only where that project's own assets use them.

## Tags written in code

Code makes a tag of a path with `Tag.GenerateAsUnique`, and offers it to the editor's pickers with
`[ForgeTagExposed]`.

These make tags in code:

| Method | What it gives |
|---|---|
| `Tag.GenerateAsUnique("State.Stunned")` | The unique tag of the path: the very tag New Tag makes of it. PlayForge's own are among them: `Tag.GenerateAsUnique("Died")` is `Tags.DIED`. |
| `TagRegistry.FromSegments("State", "Stunned")` | The same unique tag, made of its parts. |
| `Tag.Generate("State.Stunned")` | The plain tag of the path, which no unique tag equals. |
| A string where a tag is asked for, as `Tag tag = "State.Stunned";` | The plain tag of the path, as `Tag.Generate` gives. |
| `Tag.Generate(name, displayName)` | A tag of a name, with a display name of its own. |
| `tag.Child("Burn")`, `TagRegistry.CreateChild(tag, "Burn")` | The plain tag of the child's path. |
| `GetParent()`, `GetAncestors()` | The plain tags of their paths. |
| `GetRoot()` | The plain tag of the root's path. Of a root, it gives the tag itself. |

Use plain tags for tags that code alone makes and reads.

`TagRegistry.TagUtil` makes the names:

- `GenerateDeterministicTag(text, prefix, length)` makes a unique tag's name. It pads the prefix and the text to the
  length, 32 to 64, by a SHA-256 hash of them, or cuts them short there.
- `GenerateUniqueRandomTag()` makes a random tag that no registered tag has.
- `TagUtil.rngGenerationBufferSize`, 64, is the length New Tag pads to.

### Exposing them to the pickers

Mark a static `Tag` field or property of the project's code with `ForgeTagExposed` to offer it in the editor's tag
pickers. The field or property can be public or not.

Where the tag is offered depends on the contexts the mark names:

- Naming contexts, as `[ForgeTagExposed(ForgeContext.Granted)]`, it's offered by the pickers of those contexts.
- Naming none, it's offered under Code, which the fields that name no context offer.

A code tag that isn't marked stays out of the pickers. So a project chooses which of its tags its designers see.

The Tags view shows the contexts a tag is exposed for in **Declared For**, checked and locked. It can declare the tag
for other contexts besides.

The editor finds the marks once each domain reload, in the player assemblies that reference PlayForge. It leaves test
assemblies out.

A tag written in an editor assembly reaches the pickers once its type is registered as the editor loads. Register it
from an `[InitializeOnLoad]` class, for example, with
`ForgeTagRegistry.RegisterContextDefaultSource(typeof(EditorTags))`. Only the type's marked members are read.

This class exposes two tags for contexts and one under Code, and keeps one out of the pickers:

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

Attributes on a `Tag` field, or on a list of tags, say what its tag field offers and does.

| Attribute | What it does |
|---|---|
| `ForgeTagContext` | Names the contexts the field takes, PlayForge's or the project's own, as `[ForgeTagContext(ForgeContext.Granted)]` or `[ForgeTagContext("Weather")]`. It makes the tags the field holds of each. A field without one offers every tag. |
| `ForgeTagNoCreate` | Offers neither **New Tag…** nor **Make It a Child Of…**, as `AllowCreate = false` does. |
| `ForgeTagExcludeUniversal` | Leaves the Universal tags out, as `IncludeUniversal = false` does. |
| `ForgeTagReadOnly` | Shows the tag greyed, with no picker and no menu. Code still sets it. |
| `ForgeTagTooltip` | Gives the field its tooltip, as `[ForgeTagTooltip("...")]`, in place of Unity's `[Tooltip]`. The field reads `[Tooltip]` where it has none. |
| `ForgeTagExposed` | Goes on a static tag of code, not on an asset's field ([Tags written in code](#tags-written-in-code)). |

`ForgeTagContext` has two settings, each true unless set:

| Setting | What it does |
|---|---|
| `IncludeUniversal` | Offers the Universal tags too. |
| `AllowCreate` | Offers **New Tag…** and **Make It a Child Of…**. |

This ability component takes a tag of the project's Weather context. Its field can't make tags, and leaves the
Universal tags out:

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

### Attributes no drawer reads

Six more attributes are declared for a tag field, but no drawer reads them. So they change nothing in the inspector:

| Attribute | Declared for |
|---|---|
| `ForgeTagDefault` | A default tag |
| `ForgeTagRequired` | A tag the field must have |
| `ForgeTagPrefix` | The prefixes the field takes |
| `ForgeTagGroup` | A group of fields |
| `ForgeTagLockedCountAttribute`, `ForgeTagLockedIndicesAttribute` | A list's locked entries |

`ForgeTagDefault` can't be written on a field at all, since a `Tag` can't be an attribute's argument.

`AvoidRequireTagGroupColor`, on the requirement groups' fields, gives each a colour that no drawer reads.

## The tag field

Each `Tag` field, alone or in a list, draws as a tag field, `TagDrawer`. It shows its tag by its display name, and
picks another from the tags its contexts offer.

The field shows its label, or none in a list. Then it shows the tag's parents in secondary text and its leaf in strong
text, or its whole path while **Show Full Tag Paths** is on.

An empty field shows None, and a dash shows where the selected assets' tags differ.

### The picker

Pressing a tag field opens its picker, which lists, in order:

1. **None**;
2. **New Tag…**, where the field may make tags;
3. the tags the field offers, by their paths, in natural order.

While **Group Tags by Root** is on, each tag sits under a folder of its root, where the root has more than one tag. The
field's tag is checked.

The search finds a tag by any part of its path. While a tag is highlighted, the picker's Context says the contexts it's
for, or that it's for none yet.

A pick writes the tag, its name and its display name, into each selected asset, as one Undo step.

#### What a field offers

A field offers the tags of its contexts ([Contexts](#contexts)), and the Universal ones, of the active Forge Project:

- PlayForge's own, and those code exposes;
- those the project's assets use;
- those a Tag Library in its folder declares.

It leaves out a tag that only another project uses.

### Its menu

A tag field's right-click menu holds four items:

| Item | What it does |
|---|---|
| **None** | Empties the field. |
| **New Tag…** | Makes a new tag for the field ([New Tag](#new-tag)). |
| **Make It a Child Of…** | Picks another tag, and puts the field's leaf under it: Burn under Status.Debuff makes Status.Debuff.Burn. |
| **Copy Path** | Copies the tag as the field reads it. |

**New Tag…** and **Make It a Child Of…** show only where the field may make tags.

### New Tag

**New Tag** makes a unique tag, and gives it to the field. It asks for two things:

| Property | Description |
|---|---|
| **Path** | The new tag's path. It starts as the field's tag's parents. |
| **Contexts** | The contexts the tag is for. They start as the field's. |

**Create**, or Enter, makes the tag and gives it to the field. A context chosen beyond the field's declares the tag for
that context in the Tag Library, so that context's fields offer it too.

New Tag refuses a path that is:

- empty;
- of anything but letters, digits, underscores and spaces, in names joined by single dots;
- 64 characters or more, which the padding would cut short, so it could be another tag's.

A path that is one of PlayForge's own tags' text, as Died or Health, would make that very tag. New Tag says so, and
**Use It** gives the field PlayForge's own.

### Preferences

**Preferences → PlayForge** holds each user's own tag preferences, under **Tags**.

| Property | Description |
|---|---|
| **Show Full Tag Paths** | Off by default. Shows a tag's whole path in strong text, in place of its parents before its leaf. It applies in the tag fields and The Forge's Tags view. |
| **Group Tags by Root** | On by default. Shows the picker's tags under their roots. |

The page's **Menu Width** and **Menu Height** are the most the picker grows to.

## The Tags view

The Forge's **Tags** view lists the active Forge Project's tags and what uses each. In it you declare a tag for
contexts, and rename it.

Open The Forge with `Window → PlayForge → The Forge`, and choose **Tags** in its toolbar.

Each row of the list shows a tag the pickers offer:

- the tag, by its leaf, or by its whole path while **Show Full Tag Paths** is on;
- **For**, the contexts it's for;
- **Uses**, how many of the project's assets use it, a Tag Library among them.

Each column sorts, and the search finds a tag by any part of its path.

### The detail pane

Selecting a tag in the Tags view shows it in the detail pane, which holds:

- its leaf, with its whole path under it, and, where it's unique, its padded name on hover;
- its **For** and its **Uses**;
- **Declared For**, the contexts its Tag Library entry declares it for, with those its code exposes it for checked and
  locked;
- a row for each of the project's assets that uses it, with the fields that do. The row shows that asset in The Forge.

Choosing a context in **Declared For** declares the tag for it at once, as one Undo step. Taking every context away
takes the declaration away, so the tag is for the contexts its uses are in alone.

### Renaming a tag

The Tags view renames a tag, and every tag under its path, in the assets that store them.

To rename a tag:

1. Choose **Rename…** in its row's right-click menu, or press F2.
2. Enter the tag's new **Path**.

The rename is one Undo step. It reaches every ability, effect, item, entity, attribute set, attribute, Scaler asset,
Value Pipeline and Requirement Template that stores one of the tags, and the Tag Library entries that declare them.

A unique tag stays unique, and a plain one stays plain.

Nothing else follows. These keep the old name:

- code;
- scenes and prefabs;
- a Forge Project's settings;
- Crowd Control Rules and Effect Modifiers;
- a Cue Library;
- the Creator Lab's rules.

A rename refuses a path a tag has already, and one of PlayForge's own tags' text. It renames no tag that is, or has
under it, one of PlayForge's own or an asset's own tag.

### Asset tags follow their names

An asset's own tag follows its name.

You rename an ability, an effect, an item, an entity or an attribute set in any of three ways:

- by its **Name**;
- with F2 in The Forge;
- with its asset menu's **Rename…**.

The rename stores the tag the new name gives. It carries the new tag into each asset a tag's rename reaches that stored
the old one ([Renaming a tag](#renaming-a-tag)).

It carries nothing if another asset still has the old tag as its own, as the asset it was duplicated from does.

An asset's stored tag can differ from the one its name gives. Then:

- its Identity section shows the stored tag in warning text, with **Update**;
- the Problems view warns of it under "Asset tags their names don't give", with the quick fix **Store the Tag**.

**Update** and **Store the Tag** each store the tag, and carry it the same way.

## How an entity holds tags

An entity carries the tags its sources grant it, each with a weight. A tag's weight is the number of grants of it that
the entity holds now.

### Where its tags come from

An entity's tags come from its sources, which it reads as its tags are compiled:

| Source | The tags it grants |
|---|---|
| Its identity | Its asset tag, its sides and its **Granted Tags** ([ENTITIES.md](ENTITIES.md#tags)). |
| Its attribute set | The set's asset tag, and each sub-set's. |
| Each effect on its shelf | The effect's asset tag, its **Granted Tags**, and those its modifiers add ([EFFECTS.md](EFFECTS.md#tags)). An Instant effect grants its Granted Tags only as it applies. |
| Each ability it knows | The ability's asset tag and its **Passive Tags**, and its **Active Tags** while an activation of it runs ([ABILITIES.md](ABILITIES.md#tags)). |
| Each item it holds | The item's asset tag, and its **Granted Tags** while it's equipped ([ITEMS.md](ITEMS.md#tags)). |
| Its loose tags | Each loose tag ([Loose tags](#loose-tags)). |

An entity's sides are its **Affiliation**, or those its registration or `SetAffiliation` gave it, or a Side Change's
while one lasts.

An asset keeps its tags in its Tags section:

- an ability's is an `AbilityTags`;
- an effect's is a `GameplayEffectTags`;
- an item's is an `ItemTags`;
- an entity's are fields of its `EntityIdentity`.

**Context Tags** describe an asset, and are granted to no one.

### Weights and compiles

A tag's weight counts its grants, and a compile rebuilds the weights from the entity's sources.

A tag two sources grant weighs 2, as with two burns that each grant Status.Burning. It stays until both are gone.

An effect grants its tags once for each container on its shelf, not once a stack. A burn stacked five times in one
container grants Status.Burning once.

The entity compiles its tags as a source comes, goes or changes:

- an ability is learned, starts or ends;
- an item is given, equipped or taken;
- a loose tag comes or goes;
- its sides change;
- an effect lands or comes off.

A compile rebuilds the weights from the sources. As it ends, it tells only what it changed
([Hearing tags change](#hearing-tags-change)).

### Loose tags

A loose tag is one granted to the entity directly, with nothing behind it, as Unreal's loose gameplay tags are.

| Method | What it does |
|---|---|
| `gas.AddLooseTag(tag)` | Grants a loose tag. Each grant is counted. |
| `gas.RemoveLooseTag(tag)` | Takes one grant back, or returns false where the entity has none. |
| `gas.GetLooseTags()` | Lists the loose tags, a tag once for each grant. |

A save keeps an entity's loose tags ([SAVING.md](SAVING.md#what-a-save-holds)).

An ability's **Grant Tags** task grants loose tags while its stage runs ([ABILITIES.md](ABILITIES.md#stages)). The
Debugger's Entities view adds them, and takes them back, by hand.

### Reading them

An entity's `TagCache` holds its tags and their weights, and `gas.GetTagCache()` gives it. A `GameplayAbilitySystem`
component's cache is null until the component initializes.

| Method | What it does |
|---|---|
| `GetWeight(tag)` | Gives the weight of that very tag: **−1 where the entity hasn't it**, not 0. `gas.GetTagWeight(tag)` is `GetWeight`. |
| `TryGetWeight(tag, out weight)` | Says whether the entity has the tag, and gives its weight. |
| `HasTagExact(tag)` | Says whether the entity has that very tag. |
| `HasTag(tag, mode)` | Says whether it has the tag, by a match mode ([Querying tags](#querying-tags)). |
| `HasAnyTag`, `HasAllTags`, `HasNoneOfTags` | Ask the same of several tags, Exact unless told. |
| `GetMatchingWeight(tag, mode)` | Adds up the weights of the tags that match, 0 where none does. In Exact mode it is `GetWeight`: −1 for an absent tag. |
| `GetMatchingTags(tag, mode)` | Gives the tags held that match. |
| `GetAppliedTags()` | Gives each tag held, once. `gas.GetAppliedTags()` is the same list. |

> [!NOTE]
> The list `GetAppliedTags()` gives is the cache's own, reused until a tag comes or goes. Read it, and copy it to keep
> it.

#### Changing the weights directly

`AddTag`, `AddTags`, `RemoveTag`, `RemoveTags`, `RemoveTagCompletely` and `RemoveMatchingTags` change the weights
directly. An Instant effect's tags are granted this way, for the moment it applies. Each change is told as it happens.

The next compile rebuilds the weights from their sources, and drops these changes. So a tag meant to last is a loose
tag ([Loose tags](#loose-tags)).

| Method | What it does |
|---|---|
| `ResetWeights()` | Empties the weights, telling each tag's going. |
| `ResetCache()` | Empties the weights, and resolves the active tag workers as well. |
| `BeginRebuild()`, `EndRebuild()` | Frame a compile. |
| `LogWeights()`, `LogActiveWorkers()`, `GetHierarchyDebugString()` | Write the cache out, for debugging. |

### What holds and what grants

Two interfaces describe what carries tags and what grants them: `ITagHandler` and `ITagSource`.

`ITagHandler` is what carries tags, which every entity is. A tag worker reads its entity through it.

| Method | What it does |
|---|---|
| `GetAppliedTags()` | Gives the tags it carries. |
| `GetTagWeight(tag)` | Gives a tag's weight. |
| `QueryTags(query)` | Asks a tag query, against its weights. |
| `CompileGrantedTags()` | Compiles its tags now. |

`ITagSource` is what grants tags, through `GetGrantedTags()`:

- An asset gives what it grants of itself, as an ability its asset tag and its Passive Tags.
- What holds the asset on an entity gives what it grants there. A known ability gives its Active Tags while it runs,
  and an effect on the shelf its asset tag with the rest.

The Debugger shows tags as the game runs ([GETTING_STARTED.md](GETTING_STARTED.md#tooling-tour)):

- its Entities view shows an entity's tags, with what grants each;
- its Tags view shows every tag the entities hold, as a tree, with who holds the one selected.

## Hearing tags change

An entity's `TagCache` tells each change to a tag's weight. It tells the listeners on that tag, and on the tags above
it.

A change is a `TagChange`:

| Member | Description |
|---|---|
| `Tag` | The tag whose weight changed. |
| `Before`, `After` | Its weight before and after. Each is 0 where the entity doesn't hold the tag, never −1. |
| `Added` | Says the tag came. |
| `Removed` | Says the tag went. |

There are two ways to listen:

| Member | What it hears |
|---|---|
| `tags.Listen(tag, onChange, kind)` | The tag, and every tag under its path: a listener on Status hears Status.Burning. It gives a `TagListener`, whose `Dispose()` stops it. |
| `tags.TagChanged` | Every change to every tag. |

A listener's `ETagChangeKind` says what it hears:

| Option | What it hears |
|---|---|
| `AddedOrRemoved` | The default: a tag coming and going. |
| `AnyCount` | Every change to a weight. |

A listener's `Tag`, `Kind` and `Listening` say what it hears, and whether it still does.

These listeners show an icon while an entity is stunned, and log each change to a combo's weight:

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

The entities' guide listens for a stun too ([ENTITIES.md](ENTITIES.md#tags)).

### When a change is told

When a change to a tag's weight is told depends on how it was made:

- A change made straight to the weights is told as it happens.
- A compile is told as it ends, with only what it changed. So a tag held throughout is told nothing.
- A load's one compile tells what the load changed ([SAVING.md](SAVING.md#what-a-load-does)).

A listener that throws is logged, and stops neither the change nor the other listeners.

A listener disposed while a change is told hears no more of it. One added then hears the next change.

## Querying tags

A match mode, `ETagMatchMode`, says which of an entity's tags answer for the tag it's asked about.

| Option | Which tags answer |
|---|---|
| **Exact** | The tag itself. |
| **Include Children** | The tag, or one under its path: Status.Debuff.Burn answers for Status.Debuff. |
| **Include Parents** | The tag, or one above its path: Status.Debuff answers for Status.Debuff.Burn. |
| **Same Root** | Any tag of its root: Status.Buff answers for Status.Debuff.Burn. |

### Tag queries

A `TagQuery` asks one thing of an entity's tags, as a requirement's entry and a Tag condition do.

| Field | Description |
|---|---|
| `Tag` | The tag it asks about. |
| `MatchMode` | Which tags answer for it, Exact unless set. |
| `Operator` | How the count of matches compares with `Magnitude`, an `EComparisonOperator`. |
| `Magnitude` | The number the count is compared with. |

Greater Than 0, as a query starts, asks for the tag. Equal 0 asks for its absence.

Its inspector, `TagQueryDrawer`, shows its **Tag**, its **Match** and its **Count**. **Count** is the comparison and
the number.

These make the common queries:

| Method | What it asks |
|---|---|
| `TagQuery.HasTag(tag, mode)` | Whether the tag is there. |
| `DoesNotHaveTag(tag, mode)` | Whether the tag is absent. |
| `HasExactCount(tag, count, mode)` | Whether the count is exactly a number. |
| `HasTagOrChildren(tag)` | Whether the tag, or one under it, is there. |

### What a query counts

What a query counts depends on what it's asked of:

| Method | What it counts |
|---|---|
| `Validate(cache)` | Weights, as `gas.QueryTags(query)` asks. Exact reads the tag's weight, and the other modes add up the weights of the tags that match. |
| `Validate(list)` | The tags that match in a list of tags, as `GetAppliedTags()` gives, each once. |

`CountMatches` gives the count either way.

These checks ask for a debuff by a match mode, and for a bleed by its weight:

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
effect to land, go on and come off.

Each is a group, an `AvoidRequireTagGroup`, of two lists of tag queries:

| Property | Description |
|---|---|
| **Requires** (`RequireTags`) | Each query must pass. |
| **Must not have** (`AvoidTags`) | No query may pass. |
| **Name** | Optional. Tells the group apart where it's imported. |

A group passes when each query it requires passes and none it must not have does. So an empty group passes.

### Requirement sets

Groups come in sets, each an `AbstractTagRequirements`, which says:

- whether any of its groups asks anything (`HasAnyRequirements`);
- how many queries they hold (`TotalRequirementCount`);
- whether one names a template (`HasLinkedTemplates`).

An effect has two `EffectTagRequirements`, one on its target and one on its source. Each has three groups
([EFFECTS.md](EFFECTS.md#requirements)):

| Property | Description |
|---|---|
| **Applying** (`ApplicationRequirements`) | Must pass for the effect to land. |
| **While on** (`OngoingRequirements`) | Without it, the effect's ticks don't execute. |
| **Removed by** (`RemovalRequirements`) | Takes the effect off once met. It asks nothing while empty. |

An ability has an `AbilityTagRequirements` ([ABILITIES.md](ABILITIES.md#requirements)):

| Property | Description |
|---|---|
| **Caster** (`SourceRequirements`) | Checked as the ability is asked to activate, and again after targeting. |
| **While active** (`WhileActiveRequirements`) | The caster's, for as long as the ability runs. Checked as it's asked to activate, and as the caster's tags change. It interrupts the ability as it fails. |
| **Target** (`TargetRequirements`) | Checked after targeting, where there's a target. |

A targeting task has a lone group, its **Requirements**, which the target it takes must pass.

#### How a set draws

A set draws as a table, `EffectTagRequirementsDrawer` or `AbilityTagRequirementsDrawer`. Each group is a row, with a
cell for what it requires and a cell for what it must not have. Each cell lists its tags, or says None.

A lone group draws as a table of one row, `AvoidRequireTagGroupDrawer`.

Pressing a cell opens its list under the table, with the group's **Name**, **Template** and **Sync With Template**.
Pressing it again closes the list.

### What a row counts

A requirement row reads its entity's tags as a list: each tag once, whatever its weight. So a row's numbers count tags:

- Greater Than 0 asks for the tag.
- Greater Than 1 under Include Children asks for two different tags under it.

The ability's **While active** row alone is read against the weights. A Tag condition, the Tag Weight check and a tag
worker read weights too ([Conditions and checks](#conditions-and-checks)).

> [!TIP]
> To ask for stacks of one tag, as three grants of Combo.Points, use a Tag condition, the Tag Weight check or a tag
> worker.

### Templates

A Requirement Template, `RequirementTemplate`, is a group of requirements that other groups can follow. Make one with
`Create → PlayForge → Requirement Template`.

| Property | Description |
|---|---|
| **Requirements** | The template's own group. |
| **Description** | The template's description. |
| **Category** | A tag of the Requirement Category context, which groups templates. |

Its inspector, `RequirementTemplateEditor`, is the shared header over those fields.

A group's **Template** names a template. **Sync With Template** marks the group as following it
(`ShouldSyncFromTemplate`).

| Method | What it does |
|---|---|
| `TrySyncFromTemplate()` | Copies the template's tags into a group that follows it. |
| `LinkToTemplate(template)` | Links a template, and copies it. |
| `UnlinkFromTemplate()` | Lets the template go, keeping its tags. |

A group checks its own tags.

### In code

You can build and check an `AvoidRequireTagGroup` in code.

| Method | What it does |
|---|---|
| `AddRequirement(tag, mode)` | Adds a query of the tag's presence to **Requires**. |
| `AddAvoidance(tag, mode)` | Adds a query of the tag's presence to **Must not have**. |
| `AddRequirementWithChildren(tag)`, `AddAvoidanceWithChildren(tag)` | Add the same queries, under Include Children. |
| `RemoveTag(tag)` | Takes a tag's queries out. |
| `Clear()` | Takes every query out. |
| `Validate` | Passes or fails the group against a cache or a list, as a query does. |
| `ValidateDetailed` | Says why, as an `AvoidRequireTagGroup.ValidationResult`. |
| `GetAllReferencedTags()` | Lists the tags the group names. |
| `Clone()` | Copies the group. |
| `CopyFrom(group)` | Takes another group's name and queries. |

A `ValidationResult` holds:

- `FailedRequirements`, the queries the group requires that failed;
- `TriggeredAvoidances`, the queries it must not have that passed;
- `GetSummary()`, which gives the result as a line of text.

This group asks for Grounded and nothing under State.Disabled, and says why it fails:

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

Crowd control stops abilities by the tags their caster carries. So no ability has to list Stunned among the tags it
must not have.

A Crowd Control Rules asset, `CrowdControlRules`, holds its **Rules**. Make one with
`Create → PlayForge → Crowd Control Rules`.

Each rule is a `CrowdControlRule`:

| Property | Description |
|---|---|
| **Tag** | The tag its holder carries, as Stunned. |
| **Stops** | What it stops, an `ECrowdControlStops`:<br>**Every Ability**, as a stun does.<br>**Abilities Tagged**: those whose asset tag or a context tag is one of its **Ability Tags**, or under one, as Silenced stops spells. |
| **Ability Tags** | The tags **Abilities Tagged** looks for. |

While the caster carries a rule's tag, or one under it:

- an ability the rule stops is refused, as Crowd Control, the refusal's `Rule` naming the tag;
- an ability the rule stops that's running is interrupted.

An ability's **Ignores Crowd Control** names the rules it ignores, by their tags. Each tag there also ignores the rules
whose tags are under it.

An entity follows its own rules, its **Crowd Control**, or else its Forge Project's.

| Method | What it does |
|---|---|
| `gas.GetCrowdControl()` | Gives the rules an entity follows. |
| `rules.Stopping(ability, tags)` | Gives the first rule that stops an ability, or null. |

[ABILITIES.md](ABILITIES.md#crowd-control) has the rest.

## Conditions and checks

Besides requirements, conditions, ability checks, tasks and triggers read tags: an entity's, or an asset's.

### The Tag condition

The **Tag** condition, `TagCondition`, holds where an entity answers its tag query.

| Property | Description |
|---|---|
| **On** | The entity it reads: the caster or the target. |
| **Query** | A tag query, counted by weight. A tag granted twice counts twice. |

Where the number's context lacks that entity, the condition doesn't hold, as with a target's tag and no target.

These read conditions ([SCALERS.md](SCALERS.md#a-magnitude-field)):

- a Conditional number;
- an effect's Apply, Ongoing and Removal Conditions;
- an On Hit;
- an ability's Condition check.

### The Tag Weight check

The **Tag Weight** check, `TagWeightValidation`, compares a tag's weight on the checked entity with a number. It's one
of an ability's Caster or Target Checks.

| Property | Description |
|---|---|
| **Tag** | The tag whose weight it reads. An entity without the tag holds it at 0. |
| **Weight** | The number it compares the weight with. |
| **Comparison** | How it compares the two. |

A check of your own that reads a tag derives from `AbstractTagValidationRule` ([ABILITIES.md](ABILITIES.md#requirements)).
Its `GetValue` gives its tag's weight on the checked entity. Where that entity holds none of the tag, it gives the
ability's Local Data under the tag.

### Triggers, tasks and stages

An ability's triggers, tasks and stages can act on a tag ([ABILITIES.md](ABILITIES.md#stages)):

- A **Tag Added** trigger activates its ability as the entity gains a tag, or one under it, that it didn't have.
- A **Wait For Tag** task waits for its caster or its target to gain a tag, that very tag, or to lose it.
- A stage's **Condition** runs the stage by a tag, in the activation's data or on the caster or the target.

### Matching effects and abilities

Some fields match effects by their tags, not entities:

- an effect's **Removes Effects Tagged** and **Blocks Effects Tagged**;
- the Has Effect check's **Tagged**;
- the Asset Part number's Effects Tagged.

An effect answers for a tag by its asset tag, a context tag or a tag it grants, each the tag or under it
([EFFECTS.md](EFFECTS.md#tags)). `GameplayEffectTags.Names(tags, effect)` asks this in code.

`AbilityTags.Names(tags, ability)` asks the same of an ability: whether its asset tag or a context tag is one of the
tags, or under one. Cancelled By and Blocks match abilities this way.

## Tag workers

A tag worker, `AbstractTagWorker`, acts while its entity's tags meet its requirements, as flames shown while
Status.Burning weighs 3 or more.

A worker is one of a Worker Group's **Tag** workers, its kind picked from the list. Its entity holds it as it holds
the group ([WORKERS.md](WORKERS.md#where-workers-come-from)).

### A worker's requirements

A tag worker's **Tag Requirements** (`TagWorkerRequirements`) say when it acts. They list entries, each a
`TagWorkerRequirementPacket`:

| Part | Description |
|---|---|
| A tag | That very tag. |
| A policy | An `ERequireAvoidPolicy`:<br>**Require** passes while the tag's weight is the entry's number or more.<br>**Avoid** passes while it's below it. |
| A number | The weight that decides, read at the entity's level. |

The requirements are met where every entry passes, and always where there are none.

> [!NOTE]
> An absent tag weighs −1 here, as `GetWeight` gives it. So Require 1, and Require 0 too, ask for the tag, and Avoid 1
> asks for its absence.

### A worker's hooks

A tag worker's hooks run at its entity's end of frame. Each is a virtual no-op.

| Method | When it runs |
|---|---|
| `Activate(system)` | Once, as its requirements come to be met. |
| `Tick(system)` | While they hold, after the activations, as its **Tick Pause** says. |
| `Resolve(system)` | Once, as they no longer hold. Or at once where the worker is taken away while active, as when its item is unequipped. |

**Tick Pause** is a number read at the entity's level:

- At 0, the worker ticks every frame.
- At n, it ticks every n + 1 frames.
- Below 0, as at −1, the default, it never ticks.

This worker shows a child of its entity's object while its requirements are met:

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

### When requirements are read

The entity reads its workers' requirements at the end of a frame in which:

- its tags changed;
- a worker came or went;
- its last reading started or stopped one.

It queues each activation and resolution on its action queue, ahead of the ticks
([WORKERS.md](WORKERS.md#the-action-queue)).

A worker whose `ValidateWorkFor(handler)` reads more than the tags, as an attribute, overrides `EvaluateEveryFrame`
too. It's then read every frame.

| Member | What it is |
|---|---|
| `TagWorkerActivateAction`, `TagWorkerResolveAction` | The actions queued, at `ActionPriority.TagWorker`. |
| `OnTagWorkerActivated`, `OnTagWorkerResolved` | The callbacks that hear each. |
| `FrameSummary` | The frame's summary, which keeps them. |
| `tags.IsWorkerActive(worker)`, `ActiveWorkers`, `RegisteredWorkers` | Say which workers run on an entity. |

### One worker for many entities

One tag worker serves every entity whose group holds it. Each entity tracks it apart, so the worker keeps nothing of
one entity in its fields.

Given by two sources, as two copies of an item, a worker runs once. It stays until the last source takes it back.

A tag worker draws as `TagWorkerDrawer` draws a worker of any kind. Its **Tag Requirements** draw as a list
(`TagWorkerRequirementsDrawer`) of rows, each the tag, the policy and the number (`TagWorkerRequirementPacketDrawer`).

## The editor's tag registry

`ForgeTagRegistry`, in the editor, is what the tag pickers, the Tags view and New Tag know of the project's tags. It
knows each tag, and each use of it, by context.

### What the registry reads

The editor's tag registry reads the project's tags as the editor loads. It reads them again after any `.asset` file is
imported, moved or deleted (`TagRegistryAssetPostprocessor`).

It reads:

- the code tags marked `[ForgeTagExposed]`: in `Tags`, in the player assemblies that reference PlayForge, and in each
  type given to `RegisterContextDefaultSource`;
- every Tag Library;
- every `Tag` field, list and array of the project's PlayForge assets, each use under its field's contexts.

The assets it reads are the project's abilities, effects, items, entities, attributes, attribute sets, Scaler assets,
Requirement Templates, Value Pipelines, Effect Modifiers, Crowd Control Rules, Forge Projects and Creator Lab rules. It
reads through their nested classes and `[SerializeReference]` fields, as their components and workers.

> [!NOTE]
> A tag that only a Cue Library, a scene, a prefab or an asset of the project's own type holds is offered nowhere. It's
> offered once a PlayForge asset uses it, or a Tag Library declares it.

### Reading the registry in code

Tools of the project's own can read the editor's tag registry.

| Method | What it does |
|---|---|
| `GetAllTags()` | Gives every tag in the project. |
| `GetTagsForContext(contexts, includeUniversal)` | Gives the tags of the contexts, and the Universal ones where asked. |
| `GetTagUsage(tag)` | Gives a tag's uses. |
| `GetAllTagRecords()` | Gives every tag's uses. |
| `GetAllContexts()` | Gives every context in use. |
| `GetSystemDefaultTags()` | Gives the code tags. |
| `MarkDirty()` | Has the registry read again as it's next asked. |
| `RefreshCache()` | Has the registry read again at once. |

A tag's uses are its `ForgeTagRegistry.TagUsageRecord`, by context:

- each context's uses are a `ForgeTagRegistry.ContextUsage`;
- each use is a `ForgeTagRegistry.FieldUsageInfo`: its `Asset`, its `FieldPath`, and whether it's code's
  (`IsSystemDefault`).

A context is a `ForgeTagRegistry.TagContext`. Its `ContextKey` is Uncontextualized for a field that names none.

The tag field tells the registry each pick, with `RegisterTagUsage` and `UnregisterTagUsage`. So a picker knows of it
at once.

## Traps

Tags invite a few mistakes. Each is below, with its way out.

- **Text makes a plain tag.** `"State.Stunned"` where a tag is asked for, and `Tag.Generate`, give the plain tag of the
  path. New Tag made a unique one, and the two are different tags:
  - Neither equals the other.
  - Neither answers a query for the other, in any mode but Same Root.
  - Neither is heard by the other's listener.

  Write `Tag.GenerateAsUnique("State.Stunned")`, or expose the tag from code so that designers pick that very one.
- **An absent tag weighs −1.** `GetWeight`, `GetTagWeight`, and `GetMatchingWeight` in Exact mode give −1 for a tag the
  entity hasn't got, not 0. Ask for more than 0, or use `TryGetWeight`.
- **A tag added to the cache doesn't last.** `TagCache.AddTag` is dropped at the next compile, as an ability starts or
  an item changes. Grant a lasting tag with `AddLooseTag`.
- **A requirement row counts tags, not weights.** An Exact entry's Greater Than 1 never passes in a row but While
  active. Ask for stacks with the Tag Weight check, a Tag condition or a tag worker.
- **A tag shows by `ToString()`.** Its `Name` is padded where it's unique. It's the `Name` a tag converts to where a
  string is asked for.
- **A tag's relatives are plain.** `Child`, `GetParent`, `GetAncestors` and `GetRoot` make plain tags, which no unique
  tag equals. Compare families by path, with `IsChildOf` or a match mode.
- **A rename reaches assets, not code.** The Tags view renames what assets store, so a tag the project's code writes
  keeps its old text there. Rename it in the code too.
- **The applied list is the cache's.** `GetAppliedTags()` changes as tags come and go. Copy it to keep it.
