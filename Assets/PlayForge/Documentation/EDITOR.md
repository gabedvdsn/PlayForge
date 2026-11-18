# PlayForge — The Editor Tools

PlayForge's editor tools make a game's assets, check them and watch them at work: The Forge lists a Forge Project's
assets beside their inspectors, the inspectors and their fields edit them, the Problems view says what's wrong with
them, and the Debugger shows the entities and processes as the game plays. This page covers each tool in depth, with
the editor types behind it, in `FarEmerald.PlayForge.Editor`, and the Creator Lab's in `FarEmerald.PlayForge.Lab.Editor`.
[GETTING_STARTED.md](GETTING_STARTED.md#tooling-tour)'s Tooling tour is the short version.

## Menus and shortcuts

This section lists where each tool opens from.

| Where | Opens |
|---|---|
| **Window → PlayForge → The Forge** | The Forge ([The Forge](#the-forge)) |
| **Window → PlayForge → Debugger** | The Debugger ([The Debugger](#the-debugger)) |
| **Window → PlayForge → Problems** | The Problems view ([The Problems view and validation](#the-problems-view-and-validation)) |
| **Window → PlayForge → Creator Lab** | The Creator Lab, in a new scene, played ([The Creator Lab](#the-creator-lab)) |
| **Project Settings → PlayForge** | The settings a project shares ([Preferences and Project Settings](#preferences-and-project-settings)) |
| **Preferences → PlayForge** | Your own preferences |
| **Alt+Shift+F** | The Forge, with its search focused, from anywhere in the editor |

The shortcut is **PlayForge/The Forge** in Unity's Shortcuts Manager, where it can be rebound.

**Create → PlayForge**, in the Project window, lists **Forge Project**; then **Ability**, **Gameplay Effect**,
**Item**, **Attribute**, **Attribute Set** and **Entity**; then **Scaler**, **Requirement Template**, **Value
Pipeline**, **Effect Modifier**, **Crowd Control Rules**, **Tag Library** and **Cue Library**; then **Lab Rules** and
**Lab Driver**. Unity's Create menu makes the asset in the folder the Project window shows. The Forge's **Create**
makes it where its project's settings put its type, named and tagged ([New assets and presets](#new-assets-and-presets)).

The Forge's window menu, its tab's ⋮, holds **Problems…**, **Project Settings…**, **Preferences…**,
**Spreadsheets** and **Documentation**. The Debugger's holds **Samples a Second**.

## The Forge

The Forge (`TheForge`) is PlayForge's main window: the active Forge Project's assets in a list, beside a detail pane
that shows the one selected. This section covers its toolbar, its list, its detail pane, the asset menu and its
keys. The Forge follows the project as assets are made, edited, moved and deleted, so it has no Refresh.

While the Unity project has no Forge Project, The Forge asks for the first in place of its list: **Make your first
Forge Project**, a **Name**, and **Create Forge Project** ([Forge Projects](#forge-projects)).

### The toolbar

From left to right:

- **Back** and **Forward** go over the assets the detail pane has shown, as a browser's do, up to 50 back. Going to
  an asset the list hides clears the search and turns its type's filter on.
- The project menu, named for the active Forge Project, switches between the projects, the active one checked. **No
  Project** shows the assets in no project, where no project sits at the root of `Assets`. **New Project…** makes a
  project, and **Show in Project** selects the active one's asset. The menu reads **No Project** while none is
  active, and **All Assets** while the Unity project has none.
- **Create** makes a new asset of any type The Forge lists ([New assets and presets](#new-assets-and-presets)).
- A filter for each PlayForge asset type, by its icon: Ability, Gameplay Effect, Item, Attribute, Attribute Set and
  Entity. On, it shows its type alone; off again, every type.
- **More** holds the filters of the other types The Forge lists: Scaler Asset, Requirement Template, Value Pipeline,
  Effect Modifier and Crowd Control Rules. It shows the name of the one chosen.
- **Tags** shows the project's tags in place of its assets ([The Tags view](#the-tags-view)).
- The search shows the assets whose name, file name or asset tag holds its text.
- The problem count, **No problems** or as **3 problems**, opens the Problems view.
- The help button (**?**), at the toolbar's end, opens this guide's section on The Forge on the documentation site.

The status bar under the list counts what it shows, of how many, in which project, and the problems, as "12 of 40
assets in Arena · 3 problems".

### The list

Each column sorts by a click on its title, in natural order, so 9 comes before 10, then by name:

| Column | Shows |
|---|---|
| **Name** | The asset's type icon and its name: its own, or its file's where it has none. Its tooltip is the asset's path |
| **Type** | Its type |
| **Asset Tag** | Its asset tag, for an ability, effect, item, entity or attribute set |
| **Levels** | The levels it uses, as 1–5, for an asset with levels of its own |
| **Summary** | Its one line, as its inspector's header shows it ([The header](#the-header)); the whole line in its tooltip |
| (unnamed) | A warning glyph where the validator finds problems in the asset, in the destructive color for an error; its tooltip says them |

A click shows the asset in the detail pane. Several can be selected, to move, copy or trash together, and the first
shows. A double click shows the asset in the Project window, and a right click opens the asset menu. The list's
width, its columns' widths and the columns its header's menu hides are kept for each user, and its sort through a
domain reload.

`ForgeAssetLines.Of(asset)` gives an asset's one line in code.

### The detail pane

The detail pane shows the selected asset's **Summary** above its inspector. The summary holds the asset's
description, where it has one; then a line for each section of its inspector, with the section's one-line summary,
as **Leveling** Levels 1–5; for a Scaler asset, its **Kind**, what it **Reads** and its **Used By**; for a
Requirement Template, what it **Requires** and what it **Must Not Have**; and last, **Problems**, what the validator
finds. It follows the asset's edits.

The inspector under it is the asset's own, as the Inspector shows it ([Inspectors](#inspectors)). In The Forge, its
header's **Open in The Forge** is **Show in Project**, and an ability's, effect's, item's, entity's, attribute's or
attribute set's header shows the asset's file name under it, as the list shows its name alone.

### The asset menu

The same actions open from a right-click on the list and from the ⋮ in an inspector's header, which adds the asset's
own ([The header](#the-header)):

- **Show in Project** selects the asset in the Project window.
- **Open in a Window** shows its inspector in a window of its own.
- **Rename…** asks for its new name ([Renaming, duplicating and deleting](#renaming-duplicating-and-deleting)).
- **Duplicate** makes a copy beside it, named as a copy, as "Fireball Copy", with the tag that name gives, and shows
  the copy.
- **Move to Project** and **Copy to Project** move or copy every selected asset into another Forge Project, each into
  its type's folder there.
- **Move to Trash** moves every selected asset to the trash, asking first.

The others act on the first selected asset.

### The window menu

The window menu, the tab's ⋮, holds **Problems…**, **Project Settings…** and **Preferences…**, then
**Spreadsheets**: **Export Numbers…**, **Export Selected Numbers…**, **Export Item Stats…**, **Export Selected Item
Stats…** and **Import…** ([Spreadsheets](#spreadsheets)); then **Documentation**, which opens the documentation site
in the browser: **The Forge**, this guide's section on it; **The Editor Tools**, this guide; **All Guides**; and
**API Reference**, a page for each public type.

### Keys

| Key | Does |
|---|---|
| **Alt+Shift+F** | Opens The Forge with its search focused |
| **Alt+Left**, **Alt+Right** | Back and Forward, except while a text field has the keyboard. The mouse's back and forward buttons do the same |
| **F2** | Renames the selected asset; in the Tags view, the selected tag |
| **Delete**, or **Cmd+Backspace** on a Mac | Moves the selected assets to the trash, asking first |
| **Enter**, **Escape** | In The Forge's dialogs, New, New Project and Rename, does the dialog's action, or cancels |

In code, `TheForge.OpenToAsset(asset)` opens The Forge on an asset, its type's filter on and the asset selected, as
an inspector's **Open in The Forge** does, and `TheForge.ShowWindow()` opens it as the menu does.

## Forge Projects

A Forge Project (`ForgeProject`) holds one game's PlayForge assets and its settings: it's an asset, and the assets in
its folder are the project's. This section covers the project asset and its folder, which assets belong to which
project, the active project, and moving assets between projects.

### The project and its folder

**New Project…**, in The Forge's project menu, asks for a **Name** and makes a project in a folder of its own under
the PlayForge Folder, as `Assets/PlayForge/Arena/Arena.asset`, and makes it the active one. With it, it makes:

- the project's Game Root entity, in its `GameRoot` folder: the game's own entity, on the System side, which the
  project's GameRoot runs as;
- the Bootstrapper, GameRoot and ProcessControl prefabs, in the PlayForge Folder's `Bootstrap` folder, where they're
  missing. A Bootstrapper that plays no project plays the new one.

The name starts as the game's product name, from Player Settings, without its spaces. Bootstrap, the prefabs' folder,
can't be a project's name. **Create Forge Project**, where there's none, does the same. **Create → PlayForge → Forge Project**
makes the project's asset alone, in the folder the Project window shows.

The **PlayForge Folder** is `Assets/PlayForge` until Project Settings → PlayForge sets another. Where it's the folder
PlayForge's own code is in, or a folder inside it, projects and Bootstrap go in a folder beside the code's, named for
it, as `Assets/PlayForge Projects`, so the code's folder never holds a project's files.

A type's new assets go in its folder within the project's, which is made as the first one is: Abilities, Effects,
Items, Attributes, AttributeSets, Entities, Scalers, Requirements, Pipelines, Modifiers and Rules, until Project
Settings → PlayForge says otherwise. A type The Forge doesn't list, as a Tag Library, goes in the project's folder
itself.

The project's inspector (`ForgeProjectEditor`) shows its **Description**, its **Settings**, its **Game Root Entity**
and **Keep Catalog Current**, and a line in place of its catalog's entries, as "Names 166 assets for saves, kept
current as they're made, moved in and deleted". A project is the asset catalog its saves read: each PlayForge asset
made, copied or moved into its folder is named in it by its GUID as it lands, a deleted file's entries go, and an
asset moved out stays named, so a save made before still finds it. A project also catches up on the assets it doesn't
name yet as it's imported, and once each editor session. With **Keep Catalog Current** off, the catalog is left as it
is ([SAVING.md](SAVING.md#the-asset-catalog)).

### Which assets belong to which

An asset belongs to the innermost project whose folder holds it, so one project's folder can hold another's. A
project at the root of `Assets` holds every asset no other project does. An asset in no project's folder is in no
project.

The Problems view checks each asset against its own project's settings: its roles for an ability's attribute costs,
its Cue Library for a Cue Tag cue. A game as it runs reads its settings from `ForgeProject.Current`: the project code
gives `ForgeProject.Use`, or else the one its GameRoot names, and never the editor's active one, so Play mode reads
what a build does.

### The active project

The active project is the one The Forge shows, chosen in its project menu, or in the Creator Lab's **Project**. Each
user keeps their own, for each Unity project. Until one is chosen, it's the project at the root of `Assets` where
there is one, or none, where The Forge shows the assets in no project. While the Unity project has no Forge Project,
every asset counts as the active project's.

The active project decides:

- what The Forge lists, what its Tags view and its counts of uses read, and what the Creator Lab's library holds;
- what each pick of assets offers: an asset field's picker, **Set Template...**, a number field's **Scaler Assets**,
  the Import window's sources, **Import Values…** and **Pick an Unlinked Effect...**; and which tags a tag picker
  offers;
- where a new asset goes, and whose settings Project Settings → PlayForge shows and edits.

### Moving and copying

**Move to Project** and **Copy to Project**, in the asset menu, list the projects other than the active one: each
selected asset goes into its type's folder in the one chosen, with a number added to its file's name where another
file has it. A moved asset is named in its new project's catalog and stays named in its old one's; a copy is named in
its new project's.

## New assets and presets

The Forge's **Create** makes an asset of any type it lists, blank or from a preset, ready to fill in. This section
covers the New Asset window, what a new asset starts as, PlayForge's presets and a project's own templates.

### Create

**Create** lists each type The Forge lists. A type with presets opens a submenu: **Blank**, then PlayForge's presets,
then, after a line, the project's own templates. Each item opens the New Asset window (`ForgeNewAssetWindow`), titled
for what it makes, as **New Damage Effect**:

- **Name**: the new asset's name, which gives its asset tag. It starts as "New" and the preset's name or the type's.
- **Folder**: the type's folder in the active project; **Choose…** picks another in `Assets`.
- A line that says the file it will be, as `File: IronSword.asset`.
- **Create** makes it, and The Forge shows it; **Cancel** closes the window. Enter and Escape do the same.

A field's **New** button opens the same window, its name starting as the holder's and the field's, as "Goblin
Attribute Set" ([Asset fields](#asset-fields)). `ForgeNewAssetWindow.Show(type, onCreated)` opens it from code, and
`CreateAsset()` makes the asset as **Create** does.

### What a new asset starts as

1. A copy of its type's **New Asset** default, where Project Settings → PlayForge names one.
2. Filled by its preset, or a copy of its template.
3. Named, with the asset tag its name gives: the type, then the name's words capitalized and joined, its punctuation
   left out, as `Item.IronSword` for Iron Sword. An attribute takes no asset tag; a Scaler asset's name is its
   scaler's, and a Requirement Template's its requirements'.
4. Saved in the folder, its file its name, with its spaces and any character a file can't have on any platform left
   out, and a number added where the folder has the name already.

The first asset made while the Unity project has no Forge Project makes one, named for the game, in a folder of its
own under the PlayForge Folder.

### PlayForge's presets

| Create | Makes |
|---|---|
| **Gameplay Effect → Damage** | An instant −10 to the attribute playing the project's Health role, on those of other sides |
| **Gameplay Effect → Damage over Time** | 5 seconds of −4 Health, a tick a second, on those of other sides. Applied again, it replaces the one before |
| **Gameplay Effect → Buff** | +10 to an attribute you choose, for 10 seconds, on its own side. Applied again, it replaces the one before |
| **Gameplay Effect → Debuff** | −10 to an attribute you choose, for 10 seconds, on those of other sides. Applied again, it replaces the one before |
| **Gameplay Effect → Stun** | 1.5 seconds under the tag of the first rule in the project's Crowd Control that stops every ability, as Stunned |
| **Gameplay Effect → Aura** | An infinite effect with an Aura component, which reaches its holder's allies within 5; the aura's effect is yours to choose |
| **Item → On-Hit** | An item with an On Hit entry, which lands on the one hit; its effects are yours to name |
| **Item → Active** | An item that grants an ability of its own, named for the item: one stage that applies its effects, and a Cost and a Cooldown effect, named for it and taking its levels |
| **Ability → Passive** | An ability that activates as it's granted, with implicit targeting, so the effects its one stage applies land on its holder |
| **Entity → Hero** | An entity with an attribute set of its own, named for it, as "Goblin Attributes" for Goblin: the attributes playing the project's roles, Health first, at 100, the rest at 0 |

An Active item's Cost and Cooldown are copies of the project's **Cost Effect** and **Cooldown Effect** where Project
Settings → PlayForge names them, and otherwise a Cost of −10 to an attribute you choose and a Cooldown of 5 seconds.
The assets a preset makes besides its own go in their types' folders, or in the folder the window names where that
isn't the new asset's type's folder.

`ForgePresets` holds the presets in code: `For(type)` lists a type's, PlayForge's then the project's templates, each a
`ForgePreset`: its `Name` in the menu, the `Type` it makes, the `AssetName` a new one starts as, its `Description`,
and, for a template, the `Template` it copies; and `Make(preset, name)` makes an asset from one. As a preset fills a new asset it
reaches a `ForgePresetMaking`: the new asset's `Name` and `Folder`, the `Project` and its `Health` attribute, `Make<T>`
to make another asset it needs, and `Then` to act once the new asset is saved.

### The project's templates

Project Settings → PlayForge → **Templates** lists assets of the project's own that a new asset can start as a copy
of. Create lists each under its type, by its name, after PlayForge's presets. A new one is a plain copy that takes its
own name and tag: it doesn't follow its template afterwards, as a child of **Set Template...** does
([Templates](#templates)).

An ability's **Cost** and **Cooldown** fields take the project's **Cost Effect** and **Cooldown Effect** from their
right-click menu ([Asset fields](#asset-fields)).

## Templates

An asset can take another of its type as its template, as a prefab instance takes its prefab: it's the template's
child, takes the template's fields when the template is pushed, and keeps the fields it overrides. This section
covers setting a template, pushing it, overrides and what a push leaves. It's apart from the project's templates that
Create copies ([New assets and presets](#new-assets-and-presets)).

### Setting a template

**Set Template...**, in the asset menu of an inspector's header, picks the template from a searchable list of the
active project's other assets of the type. An asset that takes this one as its template, itself or through others,
isn't listed, so no loop can be made. **Unassign Template "Name"** takes the template away. Either is one Undo step,
and starts the asset with no overrides. The line under the asset's name in its header names its template.

### Pushing

**Push to Its Children**, in the template's asset menu, read as **Push to Its Child** or **Push to Its 3 Children**,
gives each child the template's fields, and each of their children theirs, as one Undo step. Saving a template changes
no child: only a push does.

- A plain field takes the template's value, unless the child overrides it.
- A `[SerializeReference]` field, as an ability task or a worker, is copied whole, a new object of its own, unless the
  child overrides it.
- A list is merged: the elements the template added since the last push are added, those it removed since are
  removed, and those the child added itself stay.
- A push leaves each asset's identity, its name, description, textures and asset tag, and its own template. It leaves
  an ability's, effect's, item's or entity's level link too.

### Overrides

An edit to a child's field, in its inspector, through Import or through a script's `SerializedObject`, makes the field
the child's own, an override. Its inspector marks it with a bar at its left and its label in bold, as Unity marks a
prefab instance's override. An edit inside a `[SerializeReference]` object overrides the whole object; an edit inside
a list overrides nothing, as lists merge; and an edit to a field a push leaves, as the name, overrides nothing. Undo of
the edit takes its mark back too.

A marked field's right-click menu has **Revert to Template "Name"**, which takes the template's value and makes the
field the template's again, as one Undo step.

### In code

`ForgeTemplatePush` does what the menus do: `ChildrenOf(template)`, `PushToChildren(template)`, which gives back the
assets it changed, and `RevertToTemplate(property)`. `ForgeTemplatePropagator` is the push's engine, which records no
Undo of its own: `PropagateToChild(template, child)`, `PropagateToAllChildren(template)`, `FindChildAssets(template)`,
`ChildrenByTemplate(type)`, `IsUsedAsTemplate(asset)`, `WouldPropagationModifyAny(template, children)`,
`UpdateOverrides(child)`, which marks overridden the fields that differ from the template and clears the rest, and
`CaptureInitialListSnapshots(child)`, which records the template's lists as the child's starting point, so a push adds
only what the template adds after it. `ForgeTemplateConfig` says which fields a push leaves: `IsExcluded(path, type)`,
`GetAllExclusions(type)`, and `SupportsTemplates(type)`, which is true for an ability, effect, item, entity or
attribute set. The asset's side is `BaseForgeAsset`'s: `Template`, `HasTemplate`, `SetTemplate`, `ClearTemplate` and
its overrides.

## Renaming, duplicating and deleting

This section covers what a rename changes, and what Duplicate and Move to Trash do.

### Renaming

An asset is renamed from the asset menu's **Rename…**, F2 on The Forge's list, or its inspector's **Name**. The Rename
window (`ForgeRenameWindow`), titled for the asset's type, as **Rename Ability**, holds the asset's name selected, to
type over; **Rename**, or Enter, renames it, and **Cancel**, or Escape, doesn't. A rename is one Undo step:

- An ability, effect, item, entity or attribute set takes the name and the asset tag it gives. Every PlayForge asset
  that stored the old tag, as a requirement's tag query, a trigger or a filter, stores the new one, unless another
  asset still has the old tag as its own, as the one it was duplicated from.
- An attribute takes the name. A Scaler asset's name goes to its scaler, and a Requirement Template's to its
  requirements, which the lists that offer them show.
- The asset's file takes the name, its spaces and any character a file can't have left out, as a new asset's file
  does, with a number added where another file in its folder has the name. Undo and Redo bring the file back with the
  name.
- An asset with no name of its own, as a Value Pipeline, is named by its file: renaming renames the file, its spaces
  kept, which Undo doesn't take back.

In an inspector, leaving the **Name** field of an ability, effect, item, entity or attribute set renames it this way,
its file with it. Each one's tag comes from its own name, so a selection of several can't be renamed at once.

`ForgeRenames` renames in code: `CanRename(asset)`, true for an asset that's its file's main asset; `NameOf(asset)`,
its name as The Forge lists it; and `Rename(asset, name)`. `ForgeRenameWindow.Show(asset)` opens the window.

A tag is renamed in The Forge's Tags view ([The Tags view](#the-tags-view)).

### Duplicating

**Duplicate** copies an asset beside it, named as a copy, as "Fireball Copy", with the tag that name gives. From The
Forge's list, The Forge shows the copy; from an inspector's header, The Forge shows it where The Forge has the
keyboard, and the Project window selects it otherwise.

### Deleting

**Move to Trash**, or Delete on The Forge's list, asks once for every asset selected, naming the assets, prefabs and
scenes that refer to any of them, up to eight, and counting the rest: their references will break. It then moves the
assets to the trash, where they can be restored.

## The Tags view

The Forge's **Tags** shows the active project's tags in place of its assets: those its assets and its Tag Library
use, those no asset uses, and those code exposes. This section covers the view in brief; [TAGS.md](TAGS.md) covers
tags, their contexts, the tag picker and the Tag Library.

- The list's columns each sort: **Tag**, by its leaf, or its whole path while **Show Full Tag Paths** is on; **For**,
  the contexts its uses are in; and **Uses**, how many of the project's assets use it. The search finds a tag by its
  path.
- A tag's detail shows its name, its whole path, what it's **For** and its **Uses**; then **Declared For**, the
  contexts the Tag Library declares it for, so their fields' pickers offer it, those its code declares checked and
  locked; then each asset that uses it, with the fields it's in, a row that opens the asset in The Forge.
- **Rename…**, in a row's right-click menu, or F2, asks for the tag's new **Path**, then renames the tag and the tags
  under its path in every PlayForge asset and Tag Library entry that names them, as one Undo step. A path the
  registry can't take, one of PlayForge's own tags' text, or one another tag has, is refused, and so is a tag that's
  an asset's own, which follows its asset's name, or one of PlayForge's own, which code writes.

`ForgeTagRegistry` knows the tags in use: `GetAllTags()`, `GetAllTagRecords()`, each a
`ForgeTagRegistry.TagUsageRecord`, `GetTagUsage(tag)`, `GetTagsForContext(...)` and `GetAllContexts()`, each a
`ForgeTagRegistry.TagContext`, with each context's uses a `ForgeTagRegistry.ContextUsage` and each use a
`ForgeTagRegistry.FieldUsageInfo`: the asset and the field. It reads the project again after an import of an `.asset`
file, which `TagRegistryAssetPostprocessor` tells it; `MarkDirty()` and `RefreshCache()` ask it to. A Tag Library's
inspector (`ForgeTagLibraryEditor`) is the shared header, then its entries and its contexts.

## Inspectors

PlayForge draws an inspector of its own for each of its assets, the same in the Inspector and in The Forge's detail
pane. This section covers the layout they share, each one's sections, and `BasePlayForgeEditor`, the base of the six
asset inspectors. The fields inside them are covered in [Fields and drawers](#fields-and-drawers).

Each takes Unity's own controls and PlayForge's style sheet, in the editor's light or dark theme. Each asset type has
an icon in a color of its own, which is where the type's color shows.

### The header

An inspector opens with a header that stays at the top of the Inspector while the content scrolls under it:

- The type's icon, or the asset's own texture where it has one: the one under its Primary tag, else its first.
- The asset's name, and under it its type, its asset tag and its template, as "Gameplay Effect · Effect.Burning".
- Its one line, where it has one: an ability's effects by their first impact at each level, then its costs and its
  cooldown, as "-450/-600/-750 Health; 280/420/680 Mana; 70/60/50 s"; an item's, what it changes while equipped and
  the ability it grants, as "+20 Armor; Blink"; an entity's, its first three attributes as it starts and its starting
  abilities; an effect's, its first impact at each level. A * marks a number that reads what the editor can't give
  it, as a target.
- Its problem count, as **2 problems**, while the validator finds any: its tooltip lists them, and a click opens the
  Problems view on the asset.
- **Open in The Forge**, which shows the asset there. In The Forge's detail pane it's **Show in Project**, with the
  asset's file name under it.
- **Help** (?), which opens the asset type's guide.
- The asset menu (⋮): **Import from Another Asset...** ([Import and Clear](#import-and-clear)); **Set Template...**,
  **Unassign Template "Name"** and **Push to Its Children** ([Templates](#templates)); then The Forge's asset menu
  ([The asset menu](#the-asset-menu)).

With several assets selected, the header says how many, and leaves out what acts on one asset: the line, **Open in
The Forge** and the template's items. The inspectors of the other assets, a Scaler asset's, a Value Pipeline's, a
Tag Library's and the rest, open with the same header, with no **Help** and no template's items; for a type The Forge
doesn't list, **Open in The Forge** is **Show in Project**.

### Sections

Under the header, an inspector's content is in sections, each drawn as Unity's foldout headers are: its arrow, its
title, and, while it's collapsed, its one-line summary, as **Leveling** Levels 1–5. A click on the header opens or
closes it, and each inspector type keeps which of its sections are open, so collapsing **Tags** on one ability keeps
it collapsed on the next. A section's actions sit on its header and always show, where it has them:

- **Import** opens the Import window for the section's fields ([Import and Clear](#import-and-clear)).
- **Clear** resets them to a new asset's values, and empties its lists, at once; Undo takes it back.
- **Help** (?) opens the part of the asset's guide that covers the section, as the Leveling section of
  [ABILITIES.md](ABILITIES.md#leveling) for an ability's **Leveling**.

The help buttons open PlayForge's documentation site in the browser. It's built from the pages of the package's
`Documentation` folder, which ship with it, to read offline.

The sections come in one order across the inspectors: **Identity**, **Leveling** and **Tags**, then the type's own,
then **Components**, **Workers** and **Local Data**, and last **Used By**:

| Inspector | Sections |
|---|---|
| `AbilityEditor` | Identity, Leveling, Tags, Activation, Stages, Requirements, Components, Workers, Local Data, Used By ([ABILITIES.md](ABILITIES.md)) |
| `GameplayEffectEditor` | Identity, Leveling, Tags, Duration, Impact, Requirements, Components, Workers, Local Data, Used By ([EFFECTS.md](EFFECTS.md)) |
| `ItemEditor` | Identity, Leveling, Tags, Effects, Abilities, Components, Workers, Local Data, Used By ([ITEMS.md](ITEMS.md)) |
| `EntityIdentityEditor` | Identity, Leveling, Tags, Attributes, Abilities, Items, Death, Components, Workers, Local Data, Used By ([ENTITIES.md](ENTITIES.md)) |
| `AttributeSetEditor` | Identity, Leveling, Attributes, Sub-Sets, Collision Settings, All Attributes, Workers, Local Data, Used By ([ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md)) |
| `AttributeEditor` | Identity, Local Data, Used By ([ATTRIBUTES.md](ATTRIBUTES.md)) |

Each of the six edits a selection of several assets at once, the parts that hold each asset's own objects, as an
ability's components, asking for one.

- **Identity** holds the asset's **Name**, its **Description**, and for an ability, effect, item, entity or attribute
  set the asset tag its name gives, which can be selected and copied. Where the stored tag isn't the one the name
  gives, the tag shows in warning text, with **Update**, which stores it.
- **Used By** lists the PlayForge assets whose data refers to the asset, each by its type icon and name, in name
  order, and follows the project as it changes. A row opens its asset in The Forge.
- A template's child marks each field it overrides ([Templates](#templates)).
- An ability's inspector shows under its **Components** list the warnings and errors a component rule finds
  ([A rule of your own](#a-rule-of-your-own)).

### The other inspectors

| Inspector | Shows |
|---|---|
| `ScalerAssetEditor` | A Scaler asset: its **Name**, which renames it as the menus and The Forge list it, its **Description**, its **Values**, **Try**, and **Used By**: each field that shares it, by asset and field, a row opening the asset in The Forge ([SCALERS.md](SCALERS.md#scaler-assets)) |
| `ValuePipelineEditor` | A Value Pipeline: its **Description**, a line of its stages, its **Stages**, and **Try** ([VALUE_PIPELINES.md](VALUE_PIPELINES.md#trying-a-pipeline)) |
| `EffectModifierEditor` | An Effect Modifier: its **Description**, a line that says its rule, as "Outgoing, effects tagged Status.Chill: …", and its **Rule**. `EffectModifierEditor.Describe(rule)` gives the line |
| `CueLibraryEditor` | A Cue Library: its **Routes**, and **Raised**, each cue tag the project's assets raise through it ([CUES.md](CUES.md#raised)) |
| `CrowdControlRulesEditor`, `RequirementTemplateEditor` | Crowd Control Rules and a Requirement Template: the header, then their fields |
| `ForgeProjectEditor`, `ForgeTagLibraryEditor` | A Forge Project ([Forge Projects](#forge-projects)) and a Tag Library ([The Tags view](#the-tags-view)) |
| `AbstractMonoProcessEditor` | Every MonoBehaviour process, made from `AbstractMonoProcess`, a `GameplayAbilitySystem` among them: a **Process** section of its **Lifecycle**, its **Timing**, its **Priority Method** while it steps, its **Priority** while the method is Manual, its **Instantiator** and **Participate In Sibling Cascade**, each choice's tooltip saying what it does; then the component's own fields. It edits several at once, and shows a prefab instance's overrides ([PROCESSES.md](PROCESSES.md)) |

### `BasePlayForgeEditor`

`BasePlayForgeEditor` is the base of the six asset inspectors, a `UnityEditor.Editor`. It builds the header and hands
the content to `BuildInspectorContent`, where an inspector adds its sections with `CreateSection`, each section's
open state kept for the inspector's type. Its overridable members say what the header and its menu offer:
`GetDocumentationTopic`, the guide **Help** opens; `ShowImportButton`, **Import from Another Asset...**;
`ShowTemplateButton`, the template's items; and `SupportsMultipleObjects`, whether it edits several assets at once.
`OnAssetChanged` hears each change to the asset, an Undo among them. `BasePlayForgeEditor.TemplatesFor(asset)` lists
the assets an asset can take as its template, as **Set Template...** offers them.

## Import and Clear

A section's **Import** copies chosen fields into the asset from another, and its **Clear** resets the section; the
asset menu's **Import from Another Asset...** imports the whole asset. This section covers the Import window
(`ForgeImportWindow`) and Clear (`SectionClearUtility`).

### The Import window

The window is titled for what it imports, as **Import Leveling**, names the asset it imports into, and goes in three
steps:

1. **Choose a source**: the type's **Defaults**, the assets Project Settings → PlayForge lists first for it, then the
   active project's other assets of the type, by name. The search finds a source by its name or its asset tag. A
   Workers section's Import lists any PlayForge asset with the same fields at the same paths, whatever its type, so an
   item's workers are a source for an ability's.
2. **Choose what to import**: a row for each field that differs, checked, with its value now and the source's.
   Uncheck a field to leave it. A list is replaced, unless its row's **Add** is on, which adds the source's items after
   its own. **Show matching** lists the fields that are the same as well; otherwise a line names them.
3. **Import N Changes** imports the checked fields as one Undo step, and closes the window.

**Import from Another Asset...** offers every field but those that make the asset itself: its name, description,
textures and asset tag, its template, and its level link.

`ForgeImportWindow.ShowFor(serializedObject, what, paths)` opens the window for fields of your own choosing, and
`ForgeImportWindow.ShowForAsset(serializedObject)` for a whole asset. `Choose(source)` picks the source, `Changes`
lists what the import would change, each a `ForgeImportWindow.Field`, its `Path`, `Label`, whether it `IsList`, and
whether it's included (`Include`) or added to (`Append`), and `Import()` imports them.

### Clear

**Clear** sets each of the section's fields to a new asset's value and empties its lists, at once, with no question,
as one Undo step. `SectionClearUtility.Clear(serializedObject, sectionName, paths)` does the same in code.

## Fields and drawers

PlayForge's property drawers draw its fields wherever they're shown: in its own inspectors, in the Inspector of a
component, and in an inspector of your own built with UI Toolkit. Most draw in UI Toolkit alone. This section covers
the kinds of field they make, then lists the drawers.

### Asset fields

A field that holds a PlayForge asset, a Value Pipeline, Crowd Control Rules or an Effect Modifier is Unity's object
field, with a button beside it:

- **Open**, while it names an asset, shows the asset in a window of its own, so the asset that holds the field stays
  in the Inspector.
- **New**, while it's empty, opens the New Asset window, its name starting as the holder's and the field's, as
  "Goblin Attribute Set", then makes the asset where The Forge makes its type and puts it in the field.

While the Unity project has a Forge Project, the field's picker offers the active project's assets alone, in
PlayForge's dropdown.

An effect field has a line under it that says how the effect takes its levels: **Linked to** the asset that holds the
field, with its levels; linked to another asset, with **Link Here**; or **Not linked**, with **Link**. **Link** and
**Link Here** make the effect take the holder's levels, as one Undo step, and an effect **New** makes takes them too.
Its right-click menu holds:

- on an ability's **Cost** and **Cooldown**, **Copy the Cost Effect Template**, which makes a copy of the project's
  Cost Effect named for the ability, as "Fireball Cost Effect", taking its levels, and **Use the Cost Effect
  Template**, which puts the project's Cost Effect itself in the field; the Cooldown's alike;
- **Pick an Unlinked Effect...**, a pick of the project's effects that take no asset's levels;
- **New Linked Effect...**, which names a new effect in the New Asset window and makes it take the holder's levels.

### Kind fields

A field that holds an object of one of several classes, as an ability task, a worker, a component, a cue handler or a
purge's filter, shows the kind it holds in its box: the name its `[ForgeMenu]` gives, or its class's, spaced out,
without the ending its kind shares, as **Apply Effects** for `ApplyEffectsAbilityTask`. Pressing the box opens
PlayForge's dropdown of the kinds it can hold, under the submenus their `[ForgeMenu]` paths name, as **Effects** for
Apply Effects, with **None** first where the field may be empty and the kind it holds checked. Its search finds a kind
in any submenu, and its Context says the kind's description.

- Picking a kind makes a new object of it for each selected asset, as one Undo step, and the kind's own fields show
  under it, a field marked `[ShownWhen]` only while the field it names says so.
- A class Unity can't find, as after it's renamed, shows as missing, and keeps its data.
- Workers, components, cue handlers and the other kinds that fold close to one line of their values, and keep their
  fold for the session as the object moves in its list.
- A component kind an asset can hold once isn't offered while its list holds one.

### Tag fields

A tag field shows its tag by its display name, its parents in secondary text and its leaf in strong text, or its
whole path while **Show Full Tag Paths** is on. Pressing it opens the tag picker: the tags its context offers in the
active project, with **None**, and **New Tag…** where the field may make one. Its right-click menu holds **None**,
**New Tag…**, **Make It a Child Of…** and **Copy Path**. [TAGS.md](TAGS.md) covers tag fields in full.

### Lists

PlayForge's lists are Unity's reorderable lists. A list whose field holds its objects by reference, as a stage's tasks
or a worker group's workers, adds an empty element, for its kind to be picked. A list whose elements hold such
objects, as a Group's members, adds a copy of the last element, with copies of its objects of its own. As an element
is removed or dragged, each keeps its fold.

### PlayForge's dropdown

Every picker, a kind field's, the tag picker, an asset field's, **Set Template...**, a sequence reference's, and a
number field's kind menu and field menu, opens PlayForge's dropdown: a title, a search field, the choices in their submenus, and at its foot a
Context that says more of the choice the pointer or the keys are on. It grows no wider than **Menu Width** nor taller
than **Menu Height** (Preferences → PlayForge), and a longer list scrolls. Up and Down move, Enter picks, Right opens
a submenu and Left goes back out of one, and Escape closes it.

### The drawers

The base classes:

- `AbstractForgeObjectDrawer<T>`: an asset field, with **Open** and **New**. A drawer made from it adds its own items
  to the field's right-click menu.
- `AbstractTypeRefDrawer<T>`: a kind field. A drawer made from it chooses which kinds the picker offers, how they're
  named, and whether the field may be empty.
- `AbstractGenericDrawer<T>`: a kind field that folds to a line of its values.
- `AbstractWorkerDrawer<T>`: a folding kind field that's always of some kind, as a worker.
- `AbstractComponentDrawer<T>`: a component, which offers a kind an asset can hold once only while its list holds
  none.
- `InlineFieldsDrawer`: a field's own fields, drawn where it's held, with no foldout of their own.

| Drawer | Draws |
|---|---|
| `AbilityDrawer`, `GameplayEffectDrawer`, `ItemDrawer`, `EntityDrawer`, `AttributeDrawer`, `AttributeSetDrawer` | A reference to an ability, effect, item, entity, attribute or attribute set |
| `ValuePipelineDrawer`, `CrowdControlRulesDrawer`, `EffectModifierDrawer` | A reference to a Value Pipeline, Crowd Control Rules or an Effect Modifier |
| `LevelProviderDrawer` | A `[LinkedSource]` field: Unity's object field of the assets that give levels, and a line of the levels the one named gives |
| `AbilityTaskDrawer` | An ability task (`AbstractAbilityTask`) |
| `TargetingTaskDrawer` | An ability's targeting task (`AbstractTargetingAbilityTask`) |
| `AssetSetterDrawer` | An ability's asset loader (`AbstractSetAssetsTask`) |
| `AbilityValidationRuleDrawer` | An ability's check (`IAbilityValidationRule`), each list offered the checks of its side, the caster's or the target's, and those of either |
| `ChargeRechargeDrawer` | A Charges component's **Recharge**: Each Charge, All At Once, or one of your own |
| `SequenceInjectionDrawer` | An Inject task's injection (`ISequenceInjection`) |
| `IProxyStagePolicyDrawer` | An ability stage's policy (`IAbilityProxyStagePolicy`) |
| `ClockChoiceDrawer` | A clock picked in an inspector, as a Run Sequence task's **Clock**: Caster's, Game's, Unscaled, Scaled, or one of your own |
| `PurgeFilterDrawer` | A purge's filter (`AbstractPurgeFilter`), as Except Tagged |
| `StackBehaviourDrawer` | A stacking effect's stack behaviour (`AbstractStackingConfig`) |
| `RespawnPlaceDrawer` | A Respawn's place: Home, Where It Died, Point, or one of your own |
| `ProcessInstantiatorDrawer` | A mono process's instantiator (`AbstractMonoProcessInstantiator`), or None |
| `CueHandlerDrawer` | A cue's handler (`AbstractCueHandler`) |
| `EffectChangeDrawer` | An Effect Modifier's change (`AbstractEffectChange`) |
| `ValueStageDrawer` | A Value Pipeline's stage (`AbstractValueStage`) |
| `AttributeWorkerDrawer`, `FocusedAttributeWorkerDrawer`, `RelativeAttributeWorkerDrawer` | An attribute worker, of each kind |
| `ImpactWorkerDrawer`, `ContextImpactWorkerDrawer`, `EffectWorkerDrawer`, `TagWorkerDrawer`, `AnalysisWorkerDrawer` | An impact, effect, tag or analysis worker ([WORKERS.md](WORKERS.md)) |
| `EffectComponentDrawer`, `AbilityComponentDrawer`, `EntityComponentDrawer`, `ItemComponentDrawer` | An effect's, ability's, entity's or item's component |
| `AuraEntryDrawer`, `EffectAreaDrawer` | An Aura component's aura and an Area component's area, drawn as the component's own fields |
| `StandardWorkerGroupDrawer` | A worker group: its four lists of workers |
| `TagWorkerRequirementsDrawer`, `TagWorkerRequirementPacketDrawer` | A tag worker's requirements: a row for each, its tag, its policy and the weight it requires |
| `AbilityBehaviourDrawer` | An ability's behaviour (`AbilityBehaviour`) drawn whole, where an asset or a component of your own holds one: its targeting, its asset loaders, what cancels it and what it blocks, and its stages |
| `AbilityTaskBehaviourStageDrawer` | An ability's stage: a row named by its place, as Stage 1, with **Critical** while a task commits the ability to it, folding to a line of what it holds; open, its policy, its usage effects, its condition, what cancels it and what it blocks, then its tasks |
| `AbilityStageConditionDrawer` | A stage's condition: its tag, then, once it names one, where the stage looks for it and whether it runs while the tag is missing instead |
| `AttributeCostDrawer` | An attribute cost: the attribute it spends, or the role whose attribute it spends, and how much |
| `CrowdControlRuleDrawer` | A crowd control rule: its tag, what it stops, and the tags of the abilities it stops where it stops some |
| `TaskSequenceReferenceDrawer` | A sequence reference: a pick of the project's `[TaskSequenceMethod]` methods by name, None first, each class's under its name where more than one class holds them. A method the project no longer has shows as missing |
| `GameplayEffectDurationDrawer` | An effect's duration: its policy, then what the policy uses |
| `GameplayEffectImpactDrawer` | An effect's impact: its attribute, operation and magnitude, its delivery, its impact types and whom it affects, its additional impacts with **Add Impact**, and its contained effects |
| `AttributeImpactEntryDrawer` | An additional impact: a row of its attribute and a remove button, folding to a line, as "Mana −10 · Continuous". Its delivery offers what the effect's duration can make, and says so where it can't |
| `ContainedEffectPacketDrawer` | A contained effect on one row: when it's applied, then the effect |
| `AttributeTransferDrawer` | A transfer: when it moves, who gives and of what, who gets and in what, which of the values, and how much |
| `EffectPurgeDrawer` | An effect's Purges, a foldout of its fields, its arrow in line with the lists' around it |
| `AttributeSetElementDrawer` | An attribute set element: a row of its attribute, folding to a line of what it starts at; open, what it starts at at each level, its Base and Current, its bounds, its constraints, its retention group and its collision. A typed magnitude is kept within its bounds |
| `AttributeOverflowDataDrawer` | An attribute's bounds: its policy, then the floor and the ceiling it reads; a bound's right-click menu sets it to no limit |
| `AttributeConstraintsDrawer` | An attribute's constraints: **Auto Clamp**, **Scale With Base** and **Rounding**, with **Snap Interval** under Rounding while it snaps |
| `AttributeValueDrawer` | An attribute value: its Current and Base side by side |
| `AttributeRoleDrawer` | A project's role on one row: its tag, then the attribute that plays it |
| `StartingItemDrawer` | An entity's starting item on one row: the item, then **Equip**, which equips it as it's given |
| `TagDrawer` | A tag field ([Tag fields](#tag-fields)) |
| `TagQueryDrawer` | A tag query: its tag, which tags match it, then how many it counts, as a comparison and a number on one row |
| `EffectTagRequirementsDrawer` | An effect's tag requirements on one side, the target's or the source's, as a table: Applying, While On and Removed By, each with what it requires and what it must not have. A cell opens its list under the table, with its group's template |
| `AbilityTagRequirementsDrawer` | An ability's tag requirements as the same table: the caster's, the caster's while the ability runs, and the target's |
| `AvoidRequireTagGroupDrawer` | A requirement group of its own, as a targeting task's or a Requirement Template's, as the same table of one row |
| `ChanceRollDrawer` | How a chance rolls: **Pseudo Random**, **Own Luck**, and **Luck** while Own Luck is on |
| `DataWrapperDrawer` | A Local Data row: its key, its value's kind and its value, each a column of its own |
| `ForgeFlagsDrawer` | A flags field of PlayForge's, `EPurgeRelation`, `EEffectCategory`, `ESpreadMoment` or `ECombatLogRecords`, which stores only the flags picked, and writes nothing as it's drawn |
| `TextureItemDrawer` | An asset's texture on one row: its tag, then the texture |
| `AlignedFoldoutDrawer` | PlayForge's classes with no drawer of their own, as an ability's definition and tags, an On Hit entry, a cue's placement or a project's settings: a foldout of their fields, its arrow in line with the lists' around it, a field marked `[ForgeShowWhen]` shown only while the field it names says so. It draws in an IMGUI inspector too |

The number fields' drawers are listed in [Number fields](#number-fields).

## Number fields

Every number a PlayForge asset authors is a number field, a `ScalableFloat` or a `ScalableInt`, which
`ScalableFloatDrawer` and `ScalableIntDrawer` draw. This section covers the field in the editor, its menu and the
windows it opens, and the Try sections; [SCALERS.md](SCALERS.md#a-magnitude-field) covers the kinds and what each
reads.

### The field

A number field shows its name, its kind, **Constant** or another, its number or the range it gives, as "6–8 s", and
its field menu (⋮).

- The kind menu files the kinds by category, and offers only those the field's context can work out, as its code
  declares with `[NumberContext]`. Fields of several selected assets whose kinds differ show the kind as mixed, and
  choosing one gives it to each.
- A kind other than Constant shows a line of what the field reads, as "0.3 × Target's Health (base)", then
  **Combine**, how the field joins its number with the kind, a whole-number field's **Rounding**, and the kind's own
  settings, on a shaded block under the field.
- A field that reads its scaler and has none says it reads 0, with **Make It Constant**. A kind the field's context
  can't work out says so, and the field reads its number.
- A field whose unit is "%" shows and takes its number as a percentage. One with limits keeps its constant within
  them, on a slider where they're a range.

The field menu holds **Save as Scaler Asset…**, **Scaler Assets**, a submenu of the active project's Scaler assets to
share, the one shared checked, **Import Values…**, **Quick Fill…** and **Make Constant**. Each change is one Undo step,
and reaches each selected asset's own field. A scaler field of its own, as a Group member's scaler, has the same menu.

### Its windows

- **Save as Scaler Asset** (`SaveScalerAssetWindow`): a **Name**, starting as the asset's and the field's, as
  "Burning Duration", and a **Folder**, the Scaler type's in the active project, which **Choose…** changes. **Save and
  Use** saves the field's scaler as a Scaler asset and makes the field share it. It waits for one selected asset.
- **Import Values** (`ScalerValuesWindow`): the active project's Scaler assets, then the fields of its other assets
  with values per level. Pick one to compare its values with the field's, level by level, with both curves, then
  **Copy Values**, as one Undo step.
- **Quick Fill** (`QuickFillWizard`): fills the field's values, or the points of one spread over levels, from a
  pattern, a `QuickFillWizard.EFillPattern`: **Same Value**, **Straight Line**, **Slow Then Fast**, **Fast Then
  Slow**, **Steps**, **Add Each Level**, **Multiply Each Level**, **Ease In and Out** or **Custom Curve**. Its
  rounding, a `QuickFillWizard.ERounding`, is **None**, **Whole Numbers** or **Nearest Step**. A preview shows the
  values as the table will, with their curve, and **Apply** writes them as one Undo step.

`ScalerActions` does in code what the kind menu and the field menu do, each one Undo step that reaches every selected
asset: `SetKind`, `MakeConstant`, `CanMakeConstant`, `UseScalerAsset`, `SaveAsScalerAsset` and `CopyValues`.

### Try

A Scaler asset's inspector and a Value Pipeline's have a **Try** section, which reads the asset at each level of a
range, 1 to 5 to start, against a stand-in caster and target and the values a caller sets, each typed or filled from an
**Entity**. What's typed is kept for the session, never in the asset
([SCALERS.md](SCALERS.md#scaler-assets), [VALUE_PIPELINES.md](VALUE_PIPELINES.md#trying-a-pipeline)).

`ScalerTry` does the same in code. Its `From` and `To` are the levels; its `Caster` and `Target` are each a
`ScalerTry.Side`, holding a `ScalerTry.AttributeRow` for each attribute a read asks of it, given with `Set`, taken
with `Lack`, or filled from an entity with `FillFrom`; and its `Caller` holds a `ScalerTry.CallerRow` for each key,
given with `SetCaller` and taken with `UnsetCaller`. `Read(scaler)` gives a `ScalerTry.Reading`: the value at each
level, and the parts behind each. `ValuePipelineTry` puts a `Value` through a pipeline, its chances coming up or
missing as `ChancesComeUp` says, against its `Numbers`, a `ScalerTry`; `Read(pipeline)` gives a
`ValuePipelineTry.Reading`, a `ValuePipelineTry.Run` for each level, with each stage's step, what came out, and the
stop that stopped the hit, where one did.

### The drawers

| Drawer | Draws |
|---|---|
| `ScalableFloatDrawer`, `ScalableIntDrawer` | Every `ScalableFloat` and `ScalableInt`: the number field |
| `ScalerDrawer` | A scaler field on its own, as an effect's **Delta Time Scaler** or a Group member's scaler: its kind, the range it gives and its settings, with a number field's menu |
| `ScalerBehaviourDrawer` | A scaler's behaviour (`AbstractScalerBehaviour`); the list's − removes one |
| `VarianceDrawer` | A Randomized scaler's variance: Percentage, Flat, Multiplier, or one of your own |
| `ConditionDrawer` | A condition (`GameplayCondition`), as a Conditional's **When**: its kind, a project's own among them, its line, then its fields |
| `LevelThresholdDrawer` | A Threshold scaler's threshold: its level and its tier's name on one row, then what the scaler reads from there |
| `MagnitudeModifierGroupMemberDrawer`, `CachedMagnitudeModifierGroupMemberDrawer` | A Group's member: how its result joins the members' before it, then its scaler |
| `NumberSiblingFieldDrawer` | The field a Relative to a Field number reads, picked from the other number fields of its object; a field that reads it back stays out, so a loop can't be picked |

## The Problems view and validation

PlayForge's validator checks every PlayForge asset in the Unity project as it changes, and keeps what it finds; the
Problems view lists it. This section covers the view, the severities, PlayForge's rules, the validator, and adding a
rule of your own.

### The Problems view

The Problems view (`ForgeProblemsWindow`) opens from **Window → PlayForge → Problems**, The Forge's problem count and
window menu, and an inspector header's problem count, which opens it searching for that asset. It follows the
validator, so it has no Refresh.

- The toolbar holds **List** and **By Rule**, which switch between its two views; **Errors**, **Warnings** and
  **Notes**, each a filter that counts its problems, as **2 Errors**; and the search, which finds a problem by its
  asset or its words.
- **List** has a row for each problem, the errors first and the notes last, then by asset: its severity, its asset,
  by type icon and name, and what's wrong. The columns sort. A click selects the asset and shows it in the Project
  window; a double click opens it in a window of its own.
- **By Rule** groups the problems under the rule each breaks, a rule with errors first. A problem that one step fixes
  has a quick fix, a button named for the step, as **Store the Tag**, and its rule has **Fix All**, or **Fix 2** where
  only two of its problems have one. Each is one Undo step. A rule's arrow folds it.
- The status bar says how many problems there are, in how many assets, the notes beside them, how many are shown, and,
  in By Rule, how many have a quick fix.

`ForgeProblemsWindow.ShowWindow()` opens the view, and `ForgeProblemsWindow.ShowFor(asset)` opens it on an asset.

### Severities

A problem's severity is a `ForgeProblemSeverity`:

- **Error**: something that stops working, shown in the destructive color.
- **Warning**: likely a mistake.
- **Note**: what may be meant, as a trigger on an event that code sends. The view lists notes under their own filter,
  and the counts, the view's, The Forge's and an inspector header's, leave them out.

### PlayForge's rules

Each rule finds what an inspector warns about in place, in the same words. By Rule names each:

| Rule | Finds | Severity | Quick fix |
|---|---|---|---|
| Data whose class is missing | An asset that holds data whose class Unity can't find, as after a class is renamed or removed | Error | — |
| Assets with no name | A PlayForge asset with no name | Warning | — |
| Asset tags their names don't give | A stored asset tag that isn't the one the asset's name gives | Warning | **Store the Tag**, which carries the new tag to what stored the old |
| Links with no level provider | An asset set to take a level provider's levels, with none named | Warning | **Use Own Levels** |
| Impacts that don't land as set | An effect's first impact whose settings read another way than they seem, or an additional impact whose delivery the effect's duration can't make | Warning | — |
| Fields with no scaler to read | A number field that reads its scaler and has none; a Shared kind with no Scaler asset; a Group whose members multiply with none that adds | Warning | **Make It Constant**, for the first |
| Numbers whose kind can't be read where they are | A number whose kind reads what its field doesn't have, as a target; a relative number that names no field, or a field its object hasn't got | Warning | — |
| Scalers two fields share | Two fields that hold one scaler, as after a list's + copied an element, so an edit to one changes both | Warning | **Give It Its Own** |
| Attribute sets and their elements | A set that gives no attributes; an element with no attribute; a magnitude outside its element's bounds | Warning | **Remove the Element**; **Clamp to Bounds** |
| The project's roles | An attribute cost that names nothing to spend, or a role no attribute plays in its project; an entity whose starting abilities spend attributes its set doesn't give; an entity that stays when it dies, whose set has no attribute playing the Health role and no death watcher, so only code can kill it | Warning | — |
| Cue tags that play nowhere | A Cue Tag cue with no tag, whose project names no Cue Library, or whose tag the library routes nowhere | Note | — |
| Stacks read twice | A stacking effect's periodic impact or transfer that reads its own stacks, which already ticks once a stack; an effect that reads an item's stacks that no item applies | Warning | **Count Stacks Once**, where the number's kind is Effect Stacks itself |
| Filters that never match | An On Hit's, a Lifesteal's, a trigger's or a Change Impact's impact type that no effect in the project deals | Warning | **Remove the Type**, where the filter keeps a type that's dealt |
| Events nothing sends | An ability trigger on a gameplay event that neither PlayForge nor any asset in the project sends | Note | — |
| Charges that never run out | An ability's Charges, counted, on a built-in recharge, with no Cooldown to bring them back | Warning | — |
| Toggles that never turn off | An ability's Toggle of an Instant effect | Warning | — |
| Uses that never count | An item's Uses, with no Active Ability to use | Warning | — |
| Respawns that never revive | An entity's Respawn, whose Death doesn't stay | Warning | — |
| Transfers that never move | An effect's transfer that names no attribute to give or to get, or whose delivery the duration can't make | Warning | — |

Three of the rules, Stacks read twice, Filters that never match and Events nothing sends, read what the project's
assets give each other (`ForgeLint`): the impact types its effects and impact workers deal, the gameplay events its
thresholds and stop stages send, and the effects its items apply. `ForgeLint.Forget()` has it gather them again on
its next check, as after a script changes assets in bulk.

### The validator

`ForgeValidator` runs every `ForgeValidationRule` on each asset it checks: every PlayForge asset, Scaler asset,
Requirement Template, Value Pipeline, Effect Modifier and Crowd Control Rules (`Checks(asset)`). It follows the
project, so nothing polls: an edit checks its asset again; an import checks what it brought; and a delete, a move, or
an edit to what one asset gives others, as the impact types an effect deals or a Cue Library's routes, has every asset
checked again as they're next asked for. A rule that throws doesn't keep the others from checking.

- `Check(asset)` runs every rule on an asset, saved or not.
- `ProblemsOf(asset)` and `NotesOf(asset)` give what it found in one asset; `All` and `Notes`, in every one.
- `Changed` is raised as what it knows changes.

Each problem is a `ForgeProblem`: its `Asset`, its `Severity`, its `Message`, the field it's about as a
`PropertyPath`, the `Rule` that found it, and its `Fix`, where one step fixes it: a `ForgeQuickFix`, its `Name`, as
its button says, and the step it applies, `Apply`.

On a build server, run it in batch mode:

```
Unity -batchmode -quit -projectPath <project> -executeMethod FarEmerald.PlayForge.Editor.ForgeValidator.LogAll
```

`LogAll` checks every asset afresh and writes each problem to the Console, an error as an error and a warning as a
warning, each starting "[PlayForge problems]" and its asset's path; then each note as a plain line, so a build that
fails on warnings passes them; then how many there are.

### A rule of your own

A rule of your own derives from `ForgeValidationRule`, in an editor assembly, with a public constructor that takes
nothing, and the validator finds it. It overrides:

- `Check(asset, problems)`, which adds a `ForgeProblem` for each problem it finds in the asset;
- `Name`, the name By Rule groups its problems under: its class's name, spaced out, without "Rule", unless it says;
- `Reads(asset)`, true for an asset it reads to check others, so an edit to that asset has every asset checked again.

A problem one step fixes carries the step as its `Fix`, writing through a `SerializedObject` of the asset or
recording Undo, so the Undo step the view opens around it takes it back. A rule about one kind of component derives
from `ForgeComponentRule<TComponent>`, which hears each component of its kind an asset holds, with its place in the
list; it's an `IForgeComponentRule`, whose warnings and errors an ability's inspector shows under its Components list.
[EXTENDING.md](EXTENDING.md) covers writing rules.

```csharp
using System.Collections.Generic;
using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Editor;
using Object = UnityEngine.Object;

public class AbilityDescriptionRule : ForgeValidationRule
{
    public override string Name => "Abilities with no description";

    public override void Check(Object asset, List<ForgeProblem> problems)
    {
        if (asset is Ability ability && string.IsNullOrWhiteSpace(ability.GetDescription()))
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, "It has no description."));
    }
}
```

## The Debugger

The Debugger (`RuntimeDebugger`, **Window → PlayForge → Debugger**) shows the game as it plays, and changes its
entities as it shows them. This section covers its four views, Entities, Processes, Tags and Timeline, and the
Profiler's PlayForge modules.

Outside Play mode it says so, with **Enter Play Mode**. In Play mode it follows ProcessControl: its lists change as
processes register and leave, and their values change in place, as often as **Samples a Second**, in its window menu,
says: 2, 4, 10 or 30 times a second, 4 to start. While it reads them, ProcessControl times each process's steps.

The toolbar switches between the views and holds the search and each view's own tools. The status bar holds
ProcessControl's state, the processes and how many run, wait or are being created, the entities by hub, and on its
right what the window's last action did, as "Applied Burning" or "Burning was refused". In a narrow window, as a
docked column, each view's list sits above its detail and the frame count moves to the status bar. Each detail's
sections keep their fold for the session.

### Entities

**Entities** lists the hubs ProcessControl runs, `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` alike, by
**Name**, **Hub**, Mono or Runtime, level (**Lvl**) and **Effects**, the number on it. A MonoBehaviour hub goes by its
GameObject's name, which tells two of one asset apart. The toolbar's hub filter shows **Both hubs**, **Mono hubs** or
**Runtime hubs**. With **Follow Selection** on, selecting an entity's GameObject, or a child of it, in the scene
shows that entity. A double click selects a MonoBehaviour hub's GameObject.

The detail's header names the entity, as "Entity · Goblin Raider · Mono hub · Level 3", with **Select in Scene** and
**Open Asset**, which opens its asset in a window of its own. Its sections each have a table, a row a click selects,
and a foot row that acts on the row selected and adds to the table:

- **Attributes**: each attribute's **Base** and **Current**, the current's tooltip breaking it down by retention
  group, and what **Changed by** it: each source of what it took in the last second, as "Burning -8/s", then each
  effect that holds a modifier on it. The foot row's **Base** and **Current** set the selected attribute's values on
  Enter, as a change the entity makes on its own account, an unworkable one
  ([WORKERS.md](WORKERS.md#workable-and-unworkable)); a double click on a value goes to its field.
- **Effects**: each effect on the entity, the time it has **Left**, its **Stacks**, and where it's **From**, as
  "Player · Fireball". **Remove** takes the selected one off, where its source applied it; an effect picked in the
  foot row's field, with **Apply**, lands from the entity itself.
- **Abilities**: each ability's level (**Lvl**), its **State**, as Stage 2/3, Active, Cooldown or Ready, and the
  **Cooldown** it has left. **Activate** sets the selected one off as the entity would, and **Interrupt** stops its
  run; an ability picked in the foot row's field, with **Level** and **Grant**, is granted at that level.
- **Tags**: each tag the entity holds, its **Count**, by weight, and what it's **From**. **Remove** takes back the
  selected tag where it's a loose one; a tag typed in the foot row's field, or picked from the project's with its
  button, is granted as a loose tag with **Add Tag**, or Enter.
- **Combat Log**: the entity's combat log, newest first, as its project writes it. A click on an entry shows its
  hit's run through its pipeline and its steps ([ENTITIES.md](ENTITIES.md#combat-log)).

### Processes

**Processes** lists every process ProcessControl holds, each column sorting: its **ID**, its name (**Process**), its
**Handler**, its **Lifecycle**, the update loops it steps in (**Timing**), its **State**, with the state it moves to
next where something has asked, as "Running → Terminated", its **Lifetime**, and its average time a **Step** over the
last second. The toolbar's filter shows **All states**, **Running** or **Waiting**, and its **ProcessControl** menu
sets ProcessControl's own state, from **Ready** to **Terminated Immediately**.

The detail names the process, its ID and whether it's a Mono or a Runtime process, with **Select in Scene** for a
Mono one. **Pause**, **Resume**, **Terminate**, as its lifecycle allows, and **Terminate Now** act on it and the
processes it handles. Its sections say how it runs: **Process**, its lifecycle, step timing, step priority and handler,
with **Show**, which selects the handler's process in the list; **State**, its state, the state it's queued to move
to, and its **Run**: going, held by a pause, not started, cancelled, so **Resume** starts a new one, or ended; and
**Time**, its lifetime, its time running, its step time and its peak step ([PROCESSES.md](PROCESSES.md)).

### Tags

**Tags** shows every tag the listed entities hold, as a tree of their paths, each with how many **Entities** hold it
and their **Weight**, a parent counting its children's. The hub filter applies. A tag's detail lists who holds it
under **Held By**: each **Entity**, its **Weight**, what grants it, **Granted by**, and that one's **Source**: its
attribute set, an effect and who applied it, an ability's asset tag or its passive or active tags, an item, the
entity's own asset tag, affiliation or granted tags, or a loose tag, granted directly.

### Timeline

**Timeline** shows what the entities' abilities and effects did over the last seconds. While the Debugger is open in
Play mode, it records each ability's runs, stage by stage, each refused activation, each effect's time on the shelf,
each refused effect, and an effect journal of the entities it lists, whatever view shows; it starts afresh each time
Play mode does. The toolbar's **Record** stops and starts it, **Clear** forgets what it recorded, and **Last 10 s**
picks how far back the strips reach: 5, 10, 30 or 60 seconds.

Its list has a track for each entity's ability or effect, the newest first, its number of **Runs**, and a strip of its
spans: a bar for a run or an effect's time on the shelf, and a tick for an instant effect or a refusal. The search finds
a track by its entity or its asset.

- An ability's track shows its **Stages**, each with its policy and its tasks, and when the selected run reached it
  and how long it stayed, or that it didn't; then its **Runs**, the latest first, each with its **Start**, its
  **Length**, the **Stages** it reached, and its **Outcome**: Running, Ended, Stopped, or the refusal and why. A
  click on a run shows its stages.
- An effect's track shows its **Applications**, each with its **Start**, **End** and **Length**, its refusals among
  them, then its **Journal**: each landing, tick, hit and removal of the effect on the entity, and each worker that
  ran, newest first. A click on an entry shows its parts: a number's trace, a hit's breakdown, or what a worker queued
  and what came of it ([ENTITIES.md](ENTITIES.md#effect-journal)).

A track keeps its last 100 spans, and the journal its last 1,000 entries.

### Profiler modules

Two modules chart PlayForge's counters in Unity's Profiler; turn them on in its list of modules.

- **PlayForge Processes** (`PlayForgeProcessesProfilerModule`) charts Processes Running, Processes Waiting, Processes
  Created, Registrations, Terminations, Steps, State Changes, Step Time, Sequences Running and Stages Started. Its
  details list the selected frame's costliest processes, each by ID, type and step time, which PlayForge writes into
  the capture as the Profiler records, so a saved capture shows them too.
- **PlayForge GAS** (`PlayForgeGASProfilerModule`) charts Mono Entities, Runtime Entities, Effects Active, Effects
  Applied, Effects Removed, Impacts, Activations, Refused Activations, Tag Compiles and Recomputes.

[GETTING_STARTED.md](GETTING_STARTED.md#profiling) lists PlayForge's profiler markers and counters.

## Preferences and Project Settings

PlayForge has two settings pages: **Project Settings → PlayForge**, the settings a project shares, kept in assets
under version control, and **Preferences → PlayForge**, each user's own. This section covers both, and the logging
level.

### Project Settings → PlayForge

The page opens with which PlayForge this is, its version, package name and the Unity it needs, as its `package.json`
says, and a row of links: **Guides**, the documentation site; **Editor Tools**, this guide; **API Reference**; and
**Changelog**, the package's `CHANGELOG.md`. Each site link's tooltip is its address. Then its settings:

- **PlayForge Folder**: where a new Forge Project goes, each in a folder of its own, and the Bootstrap prefabs;
  `Assets/PlayForge` to start. **…** picks a folder in `Assets`. It's the Unity project's own setting, in
  `ProjectSettings/PlayForge.asset`.

The rest is the active Forge Project's, kept in its asset, so each game has its own. With no project active, the page
says so: choose one in The Forge, or, while the Unity project has none, **Create Forge Project** makes one named for the
game.

- **Forge Project**: what the project's game reads as it runs, through its GameRoot. **Roles**, the attribute
  playing each role, by its tag ([ATTRIBUTES.md](ATTRIBUTES.md#roles)); **Crowd Control**, the rules an entity follows
  where it names none ([ABILITIES.md](ABILITIES.md#crowd-control)); **Cue Library** ([CUES.md](CUES.md#routes));
  **Resolve Order** ([EFFECTS.md](EFFECTS.md#resolve-order)); **Combat Log** ([ENTITIES.md](ENTITIES.md#combat-log));
  **Purge Levels** ([EFFECTS.md](EFFECTS.md#purging)); and **Workers**, every entity's
  ([WORKERS.md](WORKERS.md#where-workers-come-from)). **Fill Roles by Name** gives each of PlayForge's roles no
  attribute plays, Health, Luck and Movement Speed, the project's attribute of that name, Move Speed for Movement Speed
  too, as one Undo step.
- **New Assets**: each type's folder within the project's, which The Forge and a field's **New** put its new assets
  in; **…** picks one inside the project's folder. **Use Default Folders** puts each back.
- **Defaults**: for each type, its **New Asset**, which a new asset of the type starts as a copy of, and its
  **Import**, the assets the Import window lists first.
- **Cost and Cooldown**: the **Cost Effect** and **Cooldown Effect**, which an ability's **Cost** and **Cooldown**
  fields offer to copy or use, and an Active item copies.
- **Templates**: the project's own assets that Create offers to start a new asset as a copy of.

Each edit is saved in the project's asset, and Undo takes it back.

`PlayForgeSettings` reads and writes the project's authoring settings: `FolderFor(type)`, `NewAssetDefault(type)`,
`ImportDefaults(type)`, `UsageEffect(key)` and `SetUsageEffect(key, effect)`, `SetNewAssets`, `SetDefaults`,
`UseDefaultFolders()` and `DefaultFolder(type)`. `UseProject(project)` reads another project's in place of the active
one's, and gives back the one it replaced. `EffectTemplateRegistry` gets and sets the Cost and Cooldown effects by
their keys, "Cost" and "Cooldown". The settings themselves are the project's `ForgeProjectSettings`, the authoring
ones its `ForgeAuthoringSettings`.

### Preferences → PlayForge

- **Tags**: **Show Full Tag Paths**, off to start, shows a tag's whole path in strong text, in place of its parents in
  secondary text before its leaf. **Group Tags by Root**, on to start, groups the tag picker's tags under their root.
- **Menus**: **Menu Width**, 360 to start, from 220 to 1200, and **Menu Height**, 420 to start, from 160 to 1600: the
  most a PlayForge dropdown grows to, a picker's or a field's menu. A longer list scrolls.
- **Logging**: **Minimum Log Level**, the quietest message PlayForge logs: **Verbose** every trace, **Info** notable
  events, **Warning** and **Error** problems alone, and **None** nothing. Info to start.
- **Use Defaults** puts every preference back, the tables' column widths and hidden columns and the log level among
  them.

Preferences are kept in EditorPrefs, for each user. `PlayForgePreferences` holds them, and its `UseDefaults()` does
what **Use Defaults** does. `ForgeLogSettingsBridge` keeps the log level and gives it to `ForgeLog.MinimumLevel` after each domain reload:
its `CurrentLevel`, `SetLevel(level)` and `ResetLevel()`. A build logs Info unless code sets `ForgeLog.MinimumLevel`
([GETTING_STARTED.md](GETTING_STARTED.md#logging)). A double click on a message `ForgeLog` wrote, in the Console, opens
the code that logged it, not `ForgeLog`.

## Spreadsheets

Spreadsheets write a project's numbers by level, or its items' stats, to CSV, and read a sheet back. This section
names the editor's parts; [SPREADSHEETS.md](SPREADSHEETS.md) covers them in full.

The Forge's window menu holds them under **Spreadsheets**: **Export Numbers…** and **Export Selected Numbers…**,
**Export Item Stats…** and **Export Selected Item Stats…**, and **Import…**, which lists a sheet's changes in a window
of their own (`ForgeSpreadsheetWindow`) before **Import** makes those ticked as one Undo step. In code,
`ForgeSpreadsheets` writes and reads sheets: `NumbersOf(assets)` and `StatsOf(items)` give a sheet's text, and
`Read(csv)` what it would change, a `SpreadsheetImport` of `SpreadsheetChange`s, whose `Apply()` makes those chosen.

## The Creator Lab

The Creator Lab is a scene for trying a project's own assets: units, test dummies, and its time in your hands. This
section names its editor parts, in `FarEmerald.PlayForge.Lab.Editor`; [CREATOR_LAB.md](CREATOR_LAB.md) covers the lab.

- `CreatorLabMenu` opens it from **Window → PlayForge → Creator Lab**: a new scene, the scenes open before saved if
  you want, played. `CreatorLabMenu.Open(play)` does the same in code; in Play mode it makes the lab in the scene
  playing.
- `EditorLabLibrary` is the lab's library in the editor: the active Forge Project's assets by kind, or every asset in
  the project's own folders while it has no Forge Project. Its **Project** switch changes the active project, as The
  Forge's does. Given folders, it plays their assets alone.
- `LabRulesEditor` and `LabDriverEditor` are the **Lab Rules** and **Lab Driver** assets' inspectors: the shared
  header, then their fields.

The lab's assemblies are left out where `PLAYFORGE_NO_LAB` is defined.

## Build verification

`BuildVerification` proves that PlayForge's runtime compiles and links in a player, with no editor code in it, as a
build server would check. This section covers running it.

```
Unity -batchmode -nographics -projectPath <project> -executeMethod FarEmerald.PlayForge.Editor.BuildVerification.BuildWindowsPlayer
```

`BuildWindowsPlayer` builds the scenes enabled in Build Settings, or, with none enabled, an empty scene it makes for
the build and deletes after, as a 64-bit Windows player in `Build/Verification`. It then exits Unity: 0 where the build
succeeded, 1 where it failed, and 3 where it threw. `ForgeValidator.LogAll` checks the assets the same way
([The validator](#the-validator)).

## Editor helpers

The types in this section are public pieces PlayForge's editor builds on, which an editor script of your own can use
too.

| Type | Does |
|---|---|
| `ForgeDrawerStyles` | Builds inspector parts. `CreateCollapsibleSection` makes a section, with its Import, Clear and Help, from a `ForgeDrawerStyles.SectionConfig`, its name, title, help topic, and the serialized object and property paths Import and Clear act on, and gives a `ForgeDrawerStyles.SectionResult`: its header, content, summary and buttons. It also makes fields and rows. `ForgeDrawerStyles.Colors` and `ForgeDrawerStyles.Icons` hold its colors and text icons, and `ForgeDrawerStyles.HeaderConfig` and `ForgeDrawerStyles.HeaderResult` belong to `CreateMainHeader`, a header PlayForge's own inspectors don't use |
| `PropertyFieldExtensions` | `OnPopulated`: runs code once a `PropertyField` has built its contents |
| `SerializedPropertyExtensions` | `GetFieldInfo`, `GetAttribute<T>` and `HasAttribute<T>`: the field a serialized property is, found through managed references along its path, and its attributes |
| `TypePickerCache` | The concrete classes a type allows, kept for the session, which a kind field's picker lists |
| `AttributeStartValues` | What each attribute an attribute set gives starts at, at each level, worked out as the runtime does: `Of(set)` gives an `AttributeStartValues.Result`, with an `AttributeStartValues.Start` for each attribute. The Attribute Set and Entity inspectors' tables read it |
| `NumberFields`, `NumberLimits` | What the number fields know: which properties are numbers, a field's unit, limits and context, the kinds a field's menu offers and where it files them, and what a number's kind reads, `NumberFields.ScalersIn` walking into the scalers it holds as a `NumberFields.Walk` says |
| `ScalerActions` | The number field's kind menu and field menu in code ([Number fields](#number-fields)) |
| `ForgeAssetLines` | An asset's one line, as its header and The Forge's **Summary** show it |
