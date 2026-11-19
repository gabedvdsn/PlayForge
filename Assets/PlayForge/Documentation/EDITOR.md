# PlayForge — The Editor Tools

PlayForge's editor tools make a game's assets, check them and watch them at work. This guide covers each tool in depth,
with the editor types behind it.

| Tool | What it does |
|---|---|
| [The Forge](#the-forge) | Lists a Forge Project's assets beside their inspectors. |
| [Inspectors](#inspectors) and their [fields](#fields-and-drawers) | Edit the assets. |
| [The Problems view](#the-problems-view-and-validation) | Says what's wrong with the assets. |
| [The Debugger](#the-debugger) | Shows the entities and processes as the game plays. |

The editor types are in `FarEmerald.PlayForge.Editor`, and the Creator Lab's are in `FarEmerald.PlayForge.Lab.Editor`.
The Tooling tour in [GETTING_STARTED.md](GETTING_STARTED.md#tooling-tour) is the short version.

## Menus and shortcuts

This section lists where each tool opens from: Unity's menus, a keyboard shortcut, and the tools' own window menus.

### Opening the tools

Each PlayForge tool opens from Unity's **Window** menu, from a settings page, or with a shortcut.

| Where | Opens |
|---|---|
| **Window → PlayForge → The Forge** | The Forge ([The Forge](#the-forge)) |
| **Window → PlayForge → Debugger** | The Debugger ([The Debugger](#the-debugger)) |
| **Window → PlayForge → Problems** | The Problems view ([The Problems view and validation](#the-problems-view-and-validation)) |
| **Window → PlayForge → Creator Lab** | The Creator Lab, in a new scene that it plays ([The Creator Lab](#the-creator-lab)) |
| **Project Settings → PlayForge** | The settings a project shares ([Preferences and Project Settings](#preferences-and-project-settings)) |
| **Preferences → PlayForge** | Your own preferences |
| **Alt+Shift+F** | The Forge, with its search focused, from anywhere in the editor |

> [!TIP]
> To rebind **Alt+Shift+F**, find **PlayForge/The Forge** in Unity's Shortcuts Manager.

### The Create menu

**Create → PlayForge**, in the Project window, makes PlayForge's assets. It lists them in four groups:

- **Forge Project**.
- **Ability**, **Gameplay Effect**, **Item**, **Attribute**, **Attribute Set** and **Entity**.
- **Scaler**, **Requirement Template**, **Value Pipeline**, **Effect Modifier**, **Crowd Control Rules**, **Tag Library**
  and **Cue Library**.
- **Lab Rules** and **Lab Driver**.

Unity's Create menu and The Forge's **Create** put a new asset in different places:

| Menu | Where it makes the asset |
|---|---|
| **Create → PlayForge**, in the Project window | In the folder the Project window shows. |
| The Forge's **Create** | Where its project's settings put the asset's type, named and tagged ([New assets and presets](#new-assets-and-presets)). |

### Window menus

Two tools add items to their window menu, the ⋮ on the window's tab.

| Window | Its window menu holds |
|---|---|
| The Forge | **Problems…**, **Project Settings…**, **Preferences…**, **Spreadsheets** and **Documentation** ([The window menu](#the-window-menu)) |
| The Debugger | **Samples a Second** ([The Debugger](#the-debugger)) |

## The Forge

The Forge (`TheForge`) is PlayForge's main window. It lists the active Forge Project's assets beside a detail pane that
shows the one selected.

<!-- screenshot: the-forge -->

Open it from **Window → PlayForge → The Forge**, or press **Alt+Shift+F** to open it with its search focused. The Forge
follows the project as assets are made, edited, moved and deleted, so it has no Refresh.

In code, `TheForge.ShowWindow()` opens The Forge as the menu does. `TheForge.OpenToAsset(asset)` opens it on an asset,
with its type's filter on and the asset selected, as an inspector's **Open in The Forge** does.

This section covers The Forge's toolbar, its list, its detail pane, the asset menu, the window menu and its keys.

### Your first Forge Project

While the Unity project has no Forge Project, The Forge asks you to make the first one, in place of its list
([Forge Projects](#forge-projects)).

<!-- screenshot: the-forge-first-project -->

1. Open The Forge. In place of its list, it shows **Make your first Forge Project**.
2. Enter a **Name**.
3. Select **Create Forge Project**.

This makes the project as **New Project…** does ([The project and its folder](#the-project-and-its-folder)).

### The toolbar

The toolbar runs along the top of The Forge.

<!-- screenshot: the-forge-toolbar -->

From left to right, it holds these controls:

| Control | What it does |
|---|---|
| **Back**, **Forward** | Go over the assets the detail pane has shown, as a browser's buttons do, up to 50 back. Going to an asset the list hides clears the search and turns its type's filter on. |
| Project menu | Switches between the Forge Projects. It's named for the active one. |
| **Create** | Makes a new asset of any type The Forge lists ([New assets and presets](#new-assets-and-presets)). |
| Type filters | A filter for each PlayForge asset type, shown by its icon: Ability, Gameplay Effect, Item, Attribute, Attribute Set and Entity. On, a filter shows its type alone. Off again, the list shows every type. |
| **More** | Holds the filters of the other types The Forge lists: Scaler Asset, Requirement Template, Value Pipeline, Effect Modifier and Crowd Control Rules. It shows the name of the one chosen. |
| **Tags** | Shows the project's tags in place of its assets ([The Tags view](#the-tags-view)). |
| Search | Shows the assets whose name, file name or asset tag holds its text. |
| Problem count | Reads **No problems**, or a count such as **3 problems**. It opens the Problems view. |
| Help (**?**) | Sits at the toolbar's end. It opens this guide's section on The Forge on the documentation site. |

#### The project menu

The project menu switches the active Forge Project. It reads the active project's name, **No Project** while none is
active, and **All Assets** while the Unity project has none.

| Item | What it does |
|---|---|
| Each project | Makes that project the active one. The active one is checked. |
| **No Project** | Shows the assets in no project. It's listed where no project sits at the root of `Assets`. |
| **New Project…** | Makes a project ([The project and its folder](#the-project-and-its-folder)). |
| **Show in Project** | Selects the active project's asset. |

### The list

The list shows the active project's assets that the toolbar's filters and search let through, one row each.

| Column | Shows |
|---|---|
| **Name** | The asset's type icon and its name: its own, or its file's where it has none. Its tooltip is the asset's path. |
| **Type** | Its type. |
| **Asset Tag** | Its asset tag, for an ability, effect, item, entity or attribute set. |
| **Levels** | The levels it uses, such as 1–5, for an asset with levels of its own. |
| **Summary** | Its one line, as its inspector's header shows it ([The header](#the-header)). Its tooltip holds the whole line. |
| (unnamed) | A warning glyph where the validator finds problems in the asset, in the destructive color for an error. Its tooltip says the problems. |

In code, `ForgeAssetLines.Of(asset)` gives an asset's one line.

#### Sorting

A click on a column's title sorts the list by that column. It sorts in natural order, so 9 comes before 10, then by name.

#### Selecting assets

| Action | Result |
|---|---|
| Click | Shows the asset in the detail pane. |
| Select several | Selects them to move, copy or trash together. The first one shows in the detail pane. |
| Double-click | Shows the asset in the Project window. |
| Right-click | Opens the asset menu ([The asset menu](#the-asset-menu)). |

#### What the list keeps

The list's width, its columns' widths and the columns its header's menu hides are kept for each user. Its sort is kept
through a domain reload.

#### The status bar

The status bar under the list counts what the list shows, of how many, in which project, and the problems. For example:
"12 of 40 assets in Arena · 3 problems".

### The detail pane

The detail pane shows the selected asset's **Summary** above its inspector.

<!-- screenshot: the-forge-ability -->

#### The summary

The summary follows the asset's edits. From the top, it holds these lines:

| Line | Shows |
|---|---|
| Description | The asset's description, where it has one. |
| A line for each inspector section | The section's one-line summary, such as **Leveling** Levels 1–5. |
| **Kind**, **Reads**, **Used By** | For a Scaler asset: its kind, what it reads, and what uses it. |
| **Requires**, **Must Not Have** | For a Requirement Template: what it requires, and what it must not have. |
| **Problems** | Last: what the validator finds. |

#### The inspector

The inspector under the summary is the asset's own, as the Inspector shows it ([Inspectors](#inspectors)). In The Forge
it differs in two ways:

- Its header's **Open in The Forge** is **Show in Project**.
- The header of an ability, effect, item, entity, attribute or attribute set shows the asset's file name under it,
  since the list shows its name alone.

### The asset menu

The asset menu acts on the selected assets. It opens from a right-click on The Forge's list, and from the ⋮ in an
inspector's header, which adds the asset's own items ([The header](#the-header)).

<!-- screenshot: the-forge-asset-menu -->

| Item | What it does |
|---|---|
| **Show in Project** | Selects the asset in the Project window. |
| **Open in a Window** | Shows its inspector in a window of its own. |
| **Rename…** | Asks for its new name ([Renaming, duplicating and deleting](#renaming-duplicating-and-deleting)). |
| **Duplicate** | Makes a copy beside it, and shows the copy. The copy is named as a copy, such as "Fireball Copy", with the tag that name gives. |
| **Move to Project**, **Copy to Project** | Move or copy every selected asset into another Forge Project, each into its type's folder there. |
| **Move to Trash** | Moves every selected asset to the trash, asking first. |

**Move to Project**, **Copy to Project** and **Move to Trash** act on every selected asset. The others act on the first
selected asset.

### The window menu

The window menu is the ⋮ on The Forge's tab. It opens PlayForge's other windows, its spreadsheets and its documentation.

<!-- screenshot: the-forge-window-menu -->

| Item | What it does |
|---|---|
| **Problems…** | Opens the Problems view. |
| **Project Settings…** | Opens Project Settings → PlayForge. |
| **Preferences…** | Opens Preferences → PlayForge. |
| **Spreadsheets** → **Export Numbers…**, **Export Selected Numbers…**, **Export Item Stats…**, **Export Selected Item Stats…**, **Import…** | Write numbers and item stats to a spreadsheet, and read one back ([Spreadsheets](#spreadsheets)). |
| **Documentation** → **The Forge** | Opens this guide's section on The Forge. |
| **Documentation** → **The Editor Tools** | Opens this guide. |
| **Documentation** → **All Guides** | Opens the documentation site's guides. |
| **Documentation** → **API Reference** | Opens the API reference, a page for each public type. |

Each **Documentation** item opens the documentation site in the browser.

### Keys

These keys work in The Forge and its dialogs.

| Key | What it does |
|---|---|
| **Alt+Shift+F** | Opens The Forge with its search focused. |
| **Alt+Left**, **Alt+Right** | Back and Forward, except while a text field has the keyboard. The mouse's back and forward buttons do the same. |
| **F2** | Renames the selected asset. In the Tags view, it renames the selected tag. |
| **Delete**, or **Cmd+Backspace** on a Mac | Moves the selected assets to the trash, asking first. |
| **Enter**, **Escape** | In The Forge's dialogs (New, New Project and Rename), does the dialog's action, or cancels. |

## Forge Projects

A Forge Project (`ForgeProject`) holds one game's PlayForge assets and its settings. The project is an asset, and the
assets in its folder are the project's.

This section covers the project asset and its folder, which assets belong to which project, the active project, and
moving assets between projects.

### The project and its folder

A Forge Project lives in a folder of its own under the PlayForge Folder, with a folder inside for each type's assets.

#### Make a Forge Project

1. In The Forge's project menu, choose **New Project…**.
2. Enter a **Name**. It starts as the game's product name, from Player Settings, without its spaces.
3. Select **Create**.

<!-- screenshot: new-forge-project-window -->

PlayForge makes the project in a folder of its own under the PlayForge Folder, such as
`Assets/PlayForge/Arena/Arena.asset`, and makes it the active one. With it, PlayForge makes:

- the project's Game Root entity, in its `GameRoot` folder. It's the game's own entity, on the System side, which the
  project's GameRoot runs as.
- the Bootstrapper, GameRoot and ProcessControl prefabs, in the PlayForge Folder's `Bootstrap` folder, where they're
  missing. A Bootstrapper that plays no project plays the new one.

Bootstrap, the prefabs' folder, can't be a project's name.

Two other commands make a project:

| Command | What it makes |
|---|---|
| **Create Forge Project**, where the Unity project has none | The same as **New Project…**. |
| **Create → PlayForge → Forge Project** | The project's asset alone, in the folder the Project window shows. |

#### The PlayForge Folder

The **PlayForge Folder** is `Assets/PlayForge` until Project Settings → PlayForge sets another.

Where it's the folder PlayForge's own code is in, or a folder inside it, projects and Bootstrap go in a folder beside
the code's, named for it, such as `Assets/PlayForge Projects`. That way the code's folder never holds a project's files.

#### Type folders

A type's new assets go in its folder within the project's folder. The folder is made as the first asset of the type is.
Until Project Settings → PlayForge says otherwise, the folders are:

| Type | Folder |
|---|---|
| Ability | Abilities |
| Gameplay Effect | Effects |
| Item | Items |
| Attribute | Attributes |
| Attribute Set | AttributeSets |
| Entity | Entities |
| Scaler Asset | Scalers |
| Requirement Template | Requirements |
| Value Pipeline | Pipelines |
| Effect Modifier | Modifiers |
| Crowd Control Rules | Rules |

A type The Forge doesn't list, such as a Tag Library, goes in the project's folder itself.

### The project's inspector

The project's inspector (`ForgeProjectEditor`) shows the project's fields, and a line in place of its catalog's entries.

<!-- screenshot: forge-project-inspector -->

| Property | Description |
|---|---|
| **Description** | What the project is for. |
| **Settings** | The project's settings, which its game reads as it runs: its attributes' roles, and the crowd control rules an entity follows where it names none ([Preferences and Project Settings](#preferences-and-project-settings)). |
| **Game Root Entity** | The entity the game's GameRoot runs as. The project's `GameRoot` folder holds it. |
| **Keep Catalog Current** | Keeps the catalog current as assets are made, copied, moved and deleted. |

The catalog's line reads, for example, "Names 166 assets for saves, kept current as they're made, moved in and deleted".

#### The catalog

A project is the asset catalog its saves read ([SAVING.md](SAVING.md#the-asset-catalog)).

- Each PlayForge asset made, copied or moved into the project's folder is named in the catalog by its GUID, as it lands.
- A deleted file's entries go.
- An asset moved out stays named, so a save made before still finds it.
- A project also catches up on the assets it doesn't name yet as it's imported, and once each editor session.

With **Keep Catalog Current** off, the catalog is left as it is.

### Which assets belong to which

Each asset belongs to one project, or to none, by the folder it's in.

- An asset belongs to the innermost project whose folder holds it, so one project's folder can hold another's.
- A project at the root of `Assets` holds every asset no other project does.
- An asset in no project's folder is in no project.

#### Whose settings apply

The Problems view checks each asset against its own project's settings. For example, it reads the project's roles for
an ability's attribute costs, and its Cue Library for a Cue Tag cue.

A game as it runs reads its settings from `ForgeProject.Current`. That's the project code passes to `ForgeProject.Use`,
or else the one the game's GameRoot names.

> [!NOTE]
> A running game never reads the editor's active project, so Play mode reads what a build does.

### The active project

The active project is the one The Forge shows. You choose it in The Forge's project menu, or in the Creator Lab's
**Project**.

- Each user keeps their own active project, for each Unity project.
- Until one is chosen, it's the project at the root of `Assets` where there is one. Otherwise it's none, and The Forge
  shows the assets in no project.
- While the Unity project has no Forge Project, every asset counts as the active project's.

#### What the active project decides

| Area | What the active project decides |
|---|---|
| Lists | What The Forge lists, what its Tags view and its counts of uses read, and what the Creator Lab's library holds. |
| Picks of assets | What each pick of assets offers: an asset field's picker, **Set Template...**, a number field's **Scaler Assets**, the Import window's sources, **Import Values…** and **Pick an Unlinked Effect...**. |
| Tag pickers | Which tags a tag picker offers. |
| New assets | Where a new asset goes. |
| Settings | Whose settings Project Settings → PlayForge shows and edits. |

### Moving and copying

**Move to Project** and **Copy to Project**, in the asset menu, put assets into another Forge Project.

1. Select the assets in The Forge's list.
2. Right-click them, and choose **Move to Project** or **Copy to Project**.
3. Choose a project. The menu lists the projects other than the active one.

Each selected asset goes into its type's folder in the chosen project. A number is added to its file's name where another
file has the name.

| Action | The catalogs |
|---|---|
| Move | The asset is named in its new project's catalog, and stays named in its old one's. |
| Copy | The copy is named in its new project's catalog. |

## New assets and presets

The Forge's **Create** makes an asset of any type it lists, blank or from a preset, ready to fill in. This section covers
the New Asset window, what a new asset starts as, PlayForge's presets and a project's own templates.

### Create

**Create**, on The Forge's toolbar, lists each type The Forge lists. Each item opens the New Asset window.

A type with presets opens a submenu. It holds:

- **Blank**;
- PlayForge's presets;
- after a line, the project's own templates.

<!-- screenshot: the-forge-create-menu -->

#### Make an asset from a preset

1. In The Forge's toolbar, select **Create**.
2. Choose the asset's type, then a preset, or **Blank**. The New Asset window opens.
3. Enter a **Name**. To put the asset somewhere other than its type's folder, select **Choose…** beside **Folder**.
4. Select **Create**. The Forge shows the new asset.

### The New Asset window

The New Asset window (`ForgeNewAssetWindow`) names a new asset and says where it goes. It's titled for what it makes,
such as **New Damage Effect**.

<!-- screenshot: new-asset-window -->

| Control | What it does |
|---|---|
| **Name** | The new asset's name, which gives its asset tag. It starts as "New" and the preset's name or the type's. |
| **Folder** | The type's folder in the active project. **Choose…** picks another in `Assets`. |
| File line | Says the file it will be, such as `File: IronSword.asset`. |
| **Create** | Makes the asset, and The Forge shows it. Enter does the same. |
| **Cancel** | Closes the window. Escape does the same. |

A field's **New** button opens the same window, its name starting as the holder's and the field's, such as "Goblin
Attribute Set" ([Asset fields](#asset-fields)).

In code, `ForgeNewAssetWindow.Show(type, onCreated)` opens the window, and `CreateAsset()` makes the asset as **Create**
does.

### What a new asset starts as

A new asset is made in four steps:

1. It starts as a copy of its type's **New Asset** default, where Project Settings → PlayForge names one.
2. Its preset fills it, or it becomes a copy of its template.
3. It's named, with the asset tag its name gives. The tag is the type, then the name's words capitalized and joined,
   punctuation left out, such as `Item.IronSword` for Iron Sword.
   - An attribute takes no asset tag.
   - A Scaler asset's name is its scaler's, and a Requirement Template's is its requirements'.
4. It's saved in the folder, its file named for it. Its spaces, and any character a file can't have on any platform, are
   left out. A number is added where the folder has the name already.

The first asset made while the Unity project has no Forge Project makes one, named for the game, in a folder of its own
under the PlayForge Folder.

### PlayForge's presets

PlayForge's presets fill a new effect, item, ability or entity with a common setup. **Create** lists them under their
types.

| Preset | What it makes |
|---|---|
| **Gameplay Effect → Damage** | An instant −10 to the attribute playing the project's Health role, on those of other sides. |
| **Gameplay Effect → Damage over Time** | 5 seconds of −4 Health, a tick a second, on those of other sides. Applied again, it replaces the one before. |
| **Gameplay Effect → Buff** | +10 to an attribute you choose, for 10 seconds, on its own side. Applied again, it replaces the one before. |
| **Gameplay Effect → Debuff** | −10 to an attribute you choose, for 10 seconds, on those of other sides. Applied again, it replaces the one before. |
| **Gameplay Effect → Stun** | 1.5 seconds under the tag of the first rule in the project's Crowd Control that stops every ability, such as Stunned. |
| **Gameplay Effect → Aura** | An infinite effect with an Aura component, which reaches its holder's allies within 5. The aura's effect is yours to choose. |
| **Item → On-Hit** | An item with an On Hit entry, which lands on the one hit. Its effects are yours to name. |
| **Item → Active** | An item that grants an ability of its own, named for the item: one stage that applies its effects, and a Cost and a Cooldown effect, named for it and taking its levels. |
| **Ability → Passive** | An ability that activates as it's granted, with implicit targeting, so the effects its one stage applies land on its holder. |
| **Entity → Hero** | An entity with an attribute set of its own, named for it, such as "Goblin Attributes" for Goblin. The set holds the attributes playing the project's roles, Health first, at 100, the rest at 0. |

#### An Active item's Cost and Cooldown

An Active item's Cost and Cooldown are copies of the project's **Cost Effect** and **Cooldown Effect**, where Project
Settings → PlayForge names them. Otherwise they're a Cost of −10 to an attribute you choose, and a Cooldown of 5 seconds.

#### Where a preset's other assets go

The assets a preset makes besides its own go in their types' folders. Where the window names a folder that isn't the new
asset's type's folder, they go in that folder instead.

#### Presets in code

`ForgePresets` holds the presets in code.

| Member | Description |
|---|---|
| `For(type)` | Lists a type's presets: PlayForge's, then the project's templates. Each is a `ForgePreset`. |
| `Make(preset, name)` | Makes an asset from a preset. |

A `ForgePreset` holds:

| Property | Description |
|---|---|
| `Name` | Its name in the menu. |
| `Type` | The type it makes. |
| `AssetName` | The name a new one starts as. |
| `Description` | What it makes. |
| `Template` | For a template, the asset it copies. |

As a preset fills a new asset, it reaches a `ForgePresetMaking`:

| Member | Description |
|---|---|
| `Name`, `Folder` | The new asset's name and folder. |
| `Project` | The project. |
| `Health` | The project's Health attribute. |
| `Make<T>` | Makes another asset the preset needs. |
| `Then` | Acts once the new asset is saved. |

### The project's templates

A project can keep assets of its own that new assets start as copies of.

Project Settings → PlayForge → **Templates** lists them. **Create** lists each under its type, by its name, after
PlayForge's presets.

A new asset made from one is a plain copy that takes its own name and tag. It doesn't follow its template afterwards, as
a child of **Set Template...** does ([Templates](#templates)).

An ability's **Cost** and **Cooldown** fields take the project's **Cost Effect** and **Cooldown Effect** from their
right-click menu ([Asset fields](#asset-fields)).

## Templates

An asset can take another asset of its type as its template, as a prefab instance takes its prefab. The asset is the
template's child: it takes the template's fields when the template is pushed, and keeps the fields it overrides.

This section covers setting a template, pushing it, overrides and what a push leaves. It's apart from the project's
templates that **Create** copies ([New assets and presets](#new-assets-and-presets)).

### Setting a template

You set an asset's template from the asset menu in its inspector's header.

1. In the asset's inspector header, open the asset menu (⋮).
2. Choose **Set Template...**.
3. Pick the template from the searchable list of the active project's other assets of the type.

An asset that takes this one as its template, itself or through others, isn't listed, so no loop can be made.

**Unassign Template "Name"**, in the same menu, takes the template away. Either is one Undo step, and starts the asset
with no overrides.

The line under the asset's name in its header names its template.

### Pushing

A push gives a template's children its fields.

> [!NOTE]
> Saving a template changes no child: only a push does.

1. In the template's inspector header, open the asset menu (⋮).
2. Choose **Push to Its Children**. It reads **Push to Its Child** or **Push to Its 3 Children**, by how many children it
   has.

The push gives each child the template's fields, and each of their children theirs, as one Undo step.

#### What a push does to each field

| Field | What a push does |
|---|---|
| A plain field | Takes the template's value, unless the child overrides it. |
| A `[SerializeReference]` field, such as an ability task or a worker | Is copied whole, as a new object of its own, unless the child overrides it. |
| A list | Is merged. The elements the template added since the last push are added, and those it removed since are removed. Those the child added itself stay. |

#### What a push leaves

A push leaves each asset's identity: its name, description, textures and asset tag, and its own template. It leaves an
ability's, effect's, item's or entity's level link too.

### Overrides

An override is a field a child holds as its own, which a push leaves as it is.

An edit to a child's field makes the field an override. The edit can be in the child's inspector, through Import, or
through a script's `SerializedObject`.

The inspector marks an override with a bar at its left and its label in bold, as Unity marks a prefab instance's
override. Undo of the edit takes its mark back too.

<!-- screenshot: template-overrides -->

Some edits override more than the field, or nothing:

| Edit | What it overrides |
|---|---|
| An edit inside a `[SerializeReference]` object | The whole object. |
| An edit inside a list | Nothing, as lists merge. |
| An edit to a field a push leaves, such as the name | Nothing. |

#### Revert an override

1. Right-click the marked field.
2. Choose **Revert to Template "Name"**.

The field takes the template's value and is the template's again, as one Undo step.

### In code

Three editor types handle templates in code, and `BaseForgeAsset` holds an asset's side.

#### `ForgeTemplatePush`

`ForgeTemplatePush` does what the menus do.

| Member | Description |
|---|---|
| `ChildrenOf(template)` | The assets that name the template as their template. |
| `PushToChildren(template)` | Pushes the template to its children, and gives back the assets it changed. |
| `RevertToTemplate(property)` | Takes a child's field back to its template's value. |

#### `ForgeTemplatePropagator`

`ForgeTemplatePropagator` is the push's engine. It records no Undo of its own.

| Member | Description |
|---|---|
| `PropagateToChild(template, child)` | Gives one child the template's fields. |
| `PropagateToAllChildren(template)` | Gives each child the template's fields, then their children. |
| `FindChildAssets(template)` | The assets that use the template as their template. |
| `ChildrenByTemplate(type)` | Every asset of a type, by the template it names. |
| `IsUsedAsTemplate(asset)` | Whether any asset uses the asset as its template. |
| `WouldPropagationModifyAny(template, children)` | Whether a push would change at least one child. |
| `UpdateOverrides(child)` | Marks overridden the fields that differ from the template, and clears the rest. |
| `CaptureInitialListSnapshots(child)` | Records the template's lists as the child's starting point, so a push adds only what the template adds after it. |

#### `ForgeTemplateConfig`

`ForgeTemplateConfig` says which fields a push leaves.

| Member | Description |
|---|---|
| `IsExcluded(path, type)` | Whether a push leaves the field at a property path, for an asset type. |
| `GetAllExclusions(type)` | Every path a push leaves, for an asset type. |
| `SupportsTemplates(type)` | True for an ability, effect, item, entity or attribute set. |

#### The asset's side

The asset's side is `BaseForgeAsset`'s: `Template`, `HasTemplate`, `SetTemplate`, `ClearTemplate` and its overrides.

## Renaming, duplicating and deleting

This section covers what a rename changes, and what **Duplicate** and **Move to Trash** do.

### Renaming

You rename an asset from the asset menu's **Rename…**, with F2 on The Forge's list, or in its inspector's **Name**.

To rename an asset in The Forge:

1. Select the asset in The Forge's list. Press F2, or choose **Rename…** in the asset menu.
2. The Rename window (`ForgeRenameWindow`) opens, titled for the asset's type, such as **Rename Ability**. It holds the
   asset's name selected, to type over.
3. Type the new name.
4. Select **Rename**, or press Enter. **Cancel**, or Escape, leaves the name as it was.

<!-- screenshot: rename-window -->

A rename is one Undo step.

#### What a rename changes

| Asset | What a rename changes |
|---|---|
| An ability, effect, item, entity or attribute set | It takes the name, and the asset tag the name gives. |
| An attribute | It takes the name. |
| A Scaler asset | The name goes to its scaler, which the lists that offer it show. |
| A Requirement Template | The name goes to its requirements, which the lists that offer them show. |
| An asset with no name of its own, such as a Value Pipeline | It's named by its file, so renaming renames the file, its spaces kept. Undo doesn't take that back. |

#### The old tag

Where a rename changes an asset tag, every PlayForge asset that stored the old tag stores the new one. That includes a
requirement's tag query, a trigger or a filter.

The exception is where another asset still has the old tag as its own, such as the one it was duplicated from.

#### The file

The asset's file takes the name too, as a new asset's file does. Its spaces, and any character a file can't have, are
left out. A number is added where another file in its folder has the name.

Undo and Redo bring the file back with the name.

#### Renaming in the inspector

In an inspector, leaving the **Name** field of an ability, effect, item, entity or attribute set renames it this way,
its file with it. Each one's tag comes from its own name, so a selection of several can't be renamed at once.

#### Renaming in code

`ForgeRenames` renames in code.

| Member | Description |
|---|---|
| `CanRename(asset)` | True for an asset that's its file's main asset. |
| `NameOf(asset)` | Its name, as The Forge lists it. |
| `Rename(asset, name)` | Renames it. |

`ForgeRenameWindow.Show(asset)` opens the Rename window.

A tag is renamed in The Forge's Tags view ([The Tags view](#the-tags-view)).

### Duplicating

**Duplicate** copies an asset beside it. The copy is named as a copy, such as "Fireball Copy", with the tag that name
gives.

Where the copy shows depends on where you chose **Duplicate**:

| Chosen from | The copy |
|---|---|
| The Forge's list | The Forge shows it. |
| An inspector's header, while The Forge has the keyboard | The Forge shows it. |
| An inspector's header, otherwise | The Project window selects it. |

### Deleting

**Move to Trash** moves assets to the trash, where they can be restored.

1. Select the assets in The Forge's list.
2. Press Delete, or choose **Move to Trash** in the asset menu.
3. Read the question. It asks once for every asset selected, and names the assets, prefabs and scenes that refer to any
   of them: their references will break.
4. Select **Move to Trash** to move the assets to the trash.

The question names up to eight of the assets, prefabs and scenes that refer to them, and counts the rest.

<!-- screenshot: move-to-trash-dialog -->

## The Tags view

The Forge's **Tags** shows the active project's tags in place of its assets. It lists those its assets and its Tag
Library use, those no asset uses, and those code exposes.

<!-- screenshot: the-forge-tags -->

This section covers the view in brief. [TAGS.md](TAGS.md) covers tags, their contexts, the tag picker and the Tag
Library.

### The tag list

The tag list takes the asset list's place while the toolbar's **Tags** is on. Each of its columns sorts.

| Column | Shows |
|---|---|
| **Tag** | The tag, by its leaf, or by its whole path while **Show Full Tag Paths** is on. |
| **For** | The contexts its uses are in. |
| **Uses** | How many of the project's assets use it. |

The search finds a tag by its path.

### A tag's detail

A tag's detail, beside the list, shows the tag, what it's declared for and what uses it.

| Part | Shows |
|---|---|
| Name and path | The tag's name and its whole path. |
| **For** | What it's for. |
| **Uses** | Its uses. |
| **Declared For** | The contexts the Tag Library declares it for, so their fields' pickers offer it. Those its code declares are checked and locked. |
| Its users | Each asset that uses it, with the fields it's in. A row opens the asset in The Forge. |

### Renaming a tag

A tag rename changes the tag, and the tags under its path, everywhere PlayForge stores them.

1. In The Forge's toolbar, select **Tags**.
2. Select the tag. Press F2, or right-click its row and choose **Rename…**.
3. Enter the tag's new **Path**, and select **Rename**.

PlayForge renames the tag and the tags under its path in every PlayForge asset and Tag Library entry that names them,
as one Undo step.

#### Renames PlayForge refuses

PlayForge refuses a new path that:

- the registry can't take;
- is one of PlayForge's own tags' text; or
- another tag has.

It also refuses to rename a tag that's an asset's own, which follows its asset's name, or one of PlayForge's own, which
code writes.

### Tags in code

`ForgeTagRegistry` knows the tags in use.

| Member | Gives |
|---|---|
| `GetAllTags()` | Every tag in use. |
| `GetAllTagRecords()` | A record of each tag, each a `ForgeTagRegistry.TagUsageRecord`. |
| `GetTagUsage(tag)` | One tag's record. |
| `GetTagsForContext(...)` | The tags of a context. |
| `GetAllContexts()` | The contexts, each a `ForgeTagRegistry.TagContext`. |

A record holds each context's uses as a `ForgeTagRegistry.ContextUsage`, and each use as a
`ForgeTagRegistry.FieldUsageInfo`: the asset and the field.

The registry reads the project again after an import of an `.asset` file, which `TagRegistryAssetPostprocessor` tells
it. `MarkDirty()` and `RefreshCache()` ask it to read again.

A Tag Library's inspector (`ForgeTagLibraryEditor`) shows the shared header, then the library's entries and its
contexts.

## Inspectors

PlayForge draws an inspector of its own for each of its assets. It's the same in the Inspector and in The Forge's
detail pane.

<!-- screenshot: inspector-effect -->

This section covers the layout the inspectors share, each one's sections, and `BasePlayForgeEditor`, the base of the six
asset inspectors. [Fields and drawers](#fields-and-drawers) covers the fields inside them.

Each inspector takes Unity's own controls and PlayForge's style sheet, in the editor's light or dark theme. Each asset
type has an icon in a color of its own, which is where the type's color shows.

### The header

An inspector opens with a header that stays at the top of the Inspector while the content scrolls under it.

<!-- screenshot: inspector-header -->

| Part | What it shows or does |
|---|---|
| Icon | The type's icon, or the asset's own texture where it has one: the one under its Primary tag, else its first. |
| Name | The asset's name. Under it are its type, its asset tag and its template, such as "Gameplay Effect · Effect.Burning". |
| One line | The asset's one line, where it has one. |
| Problem count | A count such as **2 problems**, while the validator finds any. Its tooltip lists them, and a click opens the Problems view on the asset. |
| **Open in The Forge** | Shows the asset there. In The Forge's detail pane, it's **Show in Project**, with the asset's file name under it. |
| **Help** (?) | Opens the asset type's guide. |
| Asset menu (⋮) | **Import from Another Asset...** ([Import and Clear](#import-and-clear)); **Set Template...**, **Unassign Template "Name"** and **Push to Its Children** ([Templates](#templates)); then The Forge's asset menu ([The asset menu](#the-asset-menu)). |

#### The one line

The one line sums up the asset, by its type:

| Asset | Its one line |
|---|---|
| Ability | Its effects by their first impact at each level, then its costs and its cooldown, such as "-450/-600/-750 Health; 280/420/680 Mana; 70/60/50 s". |
| Item | What it changes while equipped, and the ability it grants, such as "+20 Armor; Blink". |
| Entity | Its first three attributes as it starts, and its starting abilities. |
| Effect | Its first impact at each level. |

A * marks a number that reads what the editor can't give it, such as a target.

#### Several assets selected

With several assets selected, the header says how many. It leaves out what acts on one asset: the line,
**Open in The Forge** and the template's items.

#### Other assets' headers

The inspectors of the other assets, such as a Scaler asset's, a Value Pipeline's or a Tag Library's, open with the same
header. They have no **Help** and no template's items.

For a type The Forge doesn't list, **Open in The Forge** is **Show in Project**.

### Sections

Under the header, an inspector's content is in sections. Each is drawn as Unity's foldout headers are: its arrow, its
title, and, while it's collapsed, its one-line summary, such as **Leveling** Levels 1–5.

<!-- screenshot: inspector-sections -->

A click on a section's header opens or closes it. Each inspector type keeps which of its sections are open, so
collapsing **Tags** on one ability keeps it collapsed on the next.

#### Section buttons

A section's buttons sit on its header and always show, where it has them.

| Button | What it does |
|---|---|
| **Import** | Opens the Import window for the section's fields ([Import and Clear](#import-and-clear)). |
| **Clear** | Resets the section's fields to a new asset's values, and empties its lists, at once. Undo takes it back. |
| **Help** (?) | Opens the part of the asset's guide that covers the section, such as the Leveling section of [ABILITIES.md](ABILITIES.md#leveling) for an ability's **Leveling**. |

The help buttons open PlayForge's documentation site in the browser. The site is built from the pages of the package's
`Documentation` folder, which ship with it, to read offline.

### The six asset inspectors

Six inspectors edit PlayForge's main assets, with their sections in one order: **Identity**, **Leveling** and
**Tags**, then the type's own, then **Components**, **Workers** and **Local Data**, and last **Used By**.

| Inspector | Sections |
|---|---|
| `AbilityEditor` | Identity, Leveling, Tags, Activation, Stages, Requirements, Components, Workers, Local Data, Used By ([ABILITIES.md](ABILITIES.md)) |
| `GameplayEffectEditor` | Identity, Leveling, Tags, Duration, Impact, Requirements, Components, Workers, Local Data, Used By ([EFFECTS.md](EFFECTS.md)) |
| `ItemEditor` | Identity, Leveling, Tags, Effects, Abilities, Components, Workers, Local Data, Used By ([ITEMS.md](ITEMS.md)) |
| `EntityIdentityEditor` | Identity, Leveling, Tags, Attributes, Abilities, Items, Death, Components, Workers, Local Data, Used By ([ENTITIES.md](ENTITIES.md)) |
| `AttributeSetEditor` | Identity, Leveling, Attributes, Sub-Sets, Collision Settings, All Attributes, Workers, Local Data, Used By ([ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md)) |
| `AttributeEditor` | Identity, Local Data, Used By ([ATTRIBUTES.md](ATTRIBUTES.md)) |

Each of the six edits several selected assets at once. The parts that hold each asset's own objects, such as an
ability's components, ask you to select one asset instead.

#### Identity and Used By

| Section | What it holds |
|---|---|
| **Identity** | The asset's **Name** and its **Description**. For an ability, effect, item, entity or attribute set, it also shows the asset tag its name gives, which can be selected and copied. |
| **Used By** | The PlayForge assets whose data refers to the asset, each by its type icon and name, in name order. It follows the project as it changes, and a row opens its asset in The Forge. |

Where the stored tag isn't the one the name gives, **Identity** shows the tag in warning text, with **Update**, which
stores it.

#### Other marks

- A template's child marks each field it overrides ([Templates](#templates)).
- An ability's inspector shows the warnings and errors a component rule finds, under its **Components** list
  ([A rule of your own](#a-rule-of-your-own)).

### The other inspectors

PlayForge's other assets, and every MonoBehaviour process, have inspectors of their own too.

| Inspector | Shows |
|---|---|
| `ScalerAssetEditor` | A Scaler asset: its **Name**, which renames it as the menus and The Forge list it; its **Description**; its **Values**; **Try**; and **Used By**, each field that shares it, by asset and field, a row opening the asset in The Forge ([SCALERS.md](SCALERS.md#scaler-assets)). |
| `ValuePipelineEditor` | A Value Pipeline: its **Description**, a line of its stages, its **Stages**, and **Try** ([VALUE_PIPELINES.md](VALUE_PIPELINES.md#trying-a-pipeline)). |
| `EffectModifierEditor` | An Effect Modifier: its **Description**, a line that says its rule, such as "Outgoing, effects tagged Status.Chill: …", and its **Rule**. `EffectModifierEditor.Describe(rule)` gives the line. |
| `CueLibraryEditor` | A Cue Library: its **Routes**, and **Raised**, each cue tag the project's assets raise through it ([CUES.md](CUES.md#raised)). |
| `CrowdControlRulesEditor`, `RequirementTemplateEditor` | Crowd Control Rules and a Requirement Template: the header, then their fields. |
| `ForgeProjectEditor`, `ForgeTagLibraryEditor` | A Forge Project ([Forge Projects](#forge-projects)) and a Tag Library ([The Tags view](#the-tags-view)). |
| `AbstractMonoProcessEditor` | Every MonoBehaviour process, made from `AbstractMonoProcess`, a `GameplayAbilitySystem` among them. |

#### The process inspector

`AbstractMonoProcessEditor` draws every MonoBehaviour process. It edits several at once, and shows a prefab instance's
overrides ([PROCESSES.md](PROCESSES.md)).

<!-- screenshot: process-inspector -->

It shows a **Process** section, then the component's own fields. Each choice's tooltip says what it does.

| Property | Description |
|---|---|
| **Lifecycle** | How the process runs and ends. |
| **Timing** | The update loops it steps in. |
| **Priority Method** | Where it steps among the other processes. Shown while it steps. |
| **Priority** | Its step priority: a lower one steps earlier. Shown while the method is Manual. |
| **Instantiator** | Makes and destroys the process's object. With none, `Object.Instantiate` and `Object.Destroy` do. |
| **Participate In Sibling Cascade** | Pauses and ends it as its siblings do. Off, a sibling's pause or end leaves it be. |

### `BasePlayForgeEditor`

`BasePlayForgeEditor` is the base of the six asset inspectors, a `UnityEditor.Editor`.

It builds the header, and hands the content to `BuildInspectorContent`. There an inspector adds its sections with
`CreateSection`, each section's open state kept for the inspector's type.

Its overridable members say what the header and its menu offer:

| Member | Description |
|---|---|
| `GetDocumentationTopic` | The guide **Help** opens. |
| `ShowImportButton` | Whether the menu offers **Import from Another Asset...**. |
| `ShowTemplateButton` | Whether the menu offers the template's items. |
| `SupportsMultipleObjects` | Whether it edits several assets at once. |

`OnAssetChanged` hears each change to the asset, an Undo among them.

`BasePlayForgeEditor.TemplatesFor(asset)` lists the assets an asset can take as its template, as **Set Template...**
offers them.

## Import and Clear

A section's **Import** copies chosen fields into the asset from another, and its **Clear** resets the section. The asset
menu's **Import from Another Asset...** imports the whole asset.

This section covers the Import window (`ForgeImportWindow`) and Clear (`SectionClearUtility`).

### The Import window

The Import window copies chosen fields into an asset from another. It's titled for what it imports, such as
**Import Leveling**, and names the asset it imports into.

<!-- screenshot: import-window -->

It goes in three steps:

1. **Choose a source**. The type's **Defaults** come first: the assets Project Settings → PlayForge lists first for it.
   Then come the active project's other assets of the type, by name.
2. **Choose what to import**. Each field that differs has a row, checked, with its value now and the source's. Uncheck
   a field to leave it.
3. Select **Import N Changes**. It imports the checked fields as one Undo step, and closes the window.

#### Choosing a source

- The search finds a source by its name or its asset tag.
- A Workers section's Import lists any PlayForge asset with the same fields at the same paths, whatever its type. So an
  item's workers are a source for an ability's.

#### Choosing what to import

- A list is replaced, unless its row's **Add** is on. With **Add** on, the source's items go after its own.
- **Show matching** lists the fields that are the same as well. Otherwise, a line names them.

#### Importing a whole asset

**Import from Another Asset...** offers every field but those that make the asset itself: its name, description,
textures and asset tag, its template, and its level link.

#### The Import window in code

| Member | Description |
|---|---|
| `ForgeImportWindow.ShowFor(serializedObject, what, paths)` | Opens the window for fields of your own choosing. |
| `ForgeImportWindow.ShowForAsset(serializedObject)` | Opens it for a whole asset. |
| `Choose(source)` | Picks the source. |
| `Changes` | Lists what the import would change, each a `ForgeImportWindow.Field`. |
| `Import()` | Imports them. |

Each `ForgeImportWindow.Field` has its `Path`, its `Label`, whether it `IsList`, and whether it's included (`Include`) or
added to (`Append`).

### Clear

A section's **Clear** resets the section's fields at once, with no question.

It sets each of the section's fields to a new asset's value and empties its lists, as one Undo step.
`SectionClearUtility.Clear(serializedObject, sectionName, paths)` does the same in code.

## Fields and drawers

PlayForge's property drawers draw its fields wherever they're shown: in its own inspectors, in the Inspector of a
component, and in an inspector of your own built with UI Toolkit. Most draw in UI Toolkit alone.

This section covers the kinds of field they make, then lists the drawers.

### Asset fields

An asset field holds a PlayForge asset, a Value Pipeline, Crowd Control Rules or an Effect Modifier. It's Unity's object
field, with a button beside it.

<!-- screenshot: asset-field -->

| Button | Shown | What it does |
|---|---|---|
| **Open** | While the field names an asset | Shows the asset in a window of its own, so the asset that holds the field stays in the Inspector. |
| **New** | While the field is empty | Opens the New Asset window, its name starting as the holder's and the field's, such as "Goblin Attribute Set". Then it makes the asset where The Forge makes its type, and puts it in the field. |

While the Unity project has a Forge Project, the field's picker offers the active project's assets alone, in PlayForge's
dropdown.

#### An effect field's levels

An effect field has a line under it that says how the effect takes its levels.

<!-- screenshot: effect-field-levels -->

| The line says | Button |
|---|---|
| **Linked to** the asset that holds the field, with its levels | — |
| Linked to another asset | **Link Here** |
| **Not linked** | **Link** |

**Link** and **Link Here** make the effect take the holder's levels, as one Undo step. An effect **New** makes takes them
too.

#### An effect field's right-click menu

| Item | What it does |
|---|---|
| **Copy the Cost Effect Template** | On an ability's **Cost**: makes a copy of the project's Cost Effect named for the ability, such as "Fireball Cost Effect", taking its levels. |
| **Use the Cost Effect Template** | On an ability's **Cost**: puts the project's Cost Effect itself in the field. |
| **Pick an Unlinked Effect...** | A pick of the project's effects that take no asset's levels. |
| **New Linked Effect...** | Names a new effect in the New Asset window, and makes it take the holder's levels. |

An ability's **Cooldown** has the same two items for the project's Cooldown Effect.

### Kind fields

A kind field holds an object of one of several classes, such as an ability task, a worker, a component, a cue handler or
a purge's filter.

Its box shows the kind it holds: the name its `[ForgeMenu]` gives, or its class's name spaced out, without the ending its
kind shares. For example, `ApplyEffectsAbilityTask` shows as **Apply Effects**.

<!-- screenshot: kind-field -->

#### Picking a kind

Pressing the box opens PlayForge's dropdown of the kinds the field can hold. They sit under the submenus their
`[ForgeMenu]` paths name, such as **Effects** for Apply Effects.

- **None** comes first, where the field may be empty.
- The kind the field holds is checked.
- The search finds a kind in any submenu.
- The Context says the kind's description.

Picking a kind makes a new object of it for each selected asset, as one Undo step.

#### What the field shows

- The kind's own fields show under it. A field marked `[ShownWhen]` shows only while the field it names says so.
- A class Unity can't find, such as after it's renamed, shows as missing, and keeps its data.
- Workers, components, cue handlers and the other kinds that fold show one line of their values when closed. They keep
  their fold for the session as the object moves in its list.
- A component kind an asset can hold once isn't offered while its list holds one.

### Tag fields

A tag field shows its tag by its display name: its parents in secondary text and its leaf in strong text. While
**Show Full Tag Paths** is on, it shows the whole path.

<!-- screenshot: tag-field -->

Pressing the field opens the tag picker. It offers the tags the field's context offers in the active project, with
**None**, and **New Tag…** where the field may make one.

Its right-click menu holds **None**, **New Tag…**, **Make It a Child Of…** and **Copy Path**. [TAGS.md](TAGS.md) covers
tag fields in full.

### Lists

PlayForge's lists are Unity's reorderable lists. What a list adds depends on what it holds:

| List | What it adds |
|---|---|
| A list whose field holds its objects by reference, such as a stage's tasks or a worker group's workers | An empty element, for its kind to be picked. |
| A list whose elements hold such objects, such as a Group's members | A copy of the last element, with copies of its objects of its own. |

As an element is removed or dragged, each element keeps its fold.

### PlayForge's dropdown

PlayForge's dropdown is the menu every PlayForge picker opens: a kind field's, the tag picker, an asset field's,
**Set Template...**, a sequence reference's, and a number field's kind menu and field menu.

It holds a title, a search field, and the choices in their submenus. At its foot, a Context says more of the choice the
pointer or the keys are on.

<!-- screenshot: forge-dropdown -->

It grows no wider than **Menu Width** nor taller than **Menu Height** (Preferences → PlayForge). A longer list scrolls.

| Key | What it does |
|---|---|
| **Up**, **Down** | Move between the choices. |
| **Enter** | Picks the choice. |
| **Right** | Opens a submenu. |
| **Left** | Goes back out of a submenu. |
| **Escape** | Closes the dropdown. |

### The drawers

PlayForge's drawers build on these base classes.

| Base class | Draws |
|---|---|
| `AbstractForgeObjectDrawer<T>` | An asset field, with **Open** and **New**. A drawer made from it adds its own items to the field's right-click menu. |
| `AbstractTypeRefDrawer<T>` | A kind field. A drawer made from it chooses which kinds the picker offers, how they're named, and whether the field may be empty. |
| `AbstractGenericDrawer<T>` | A kind field that folds to a line of its values. |
| `AbstractWorkerDrawer<T>` | A folding kind field that's always of some kind, such as a worker. |
| `AbstractComponentDrawer<T>` | A component. It offers a kind an asset can hold once only while its list holds none. |
| `InlineFieldsDrawer` | A field's own fields, drawn where it's held, with no foldout of their own. |

The drawers themselves are grouped by what they draw: [Asset reference drawers](#asset-reference-drawers),
[Ability drawers](#ability-drawers), [Effect drawers](#effect-drawers),
[Attribute and entity drawers](#attribute-and-entity-drawers),
[Worker and component drawers](#worker-and-component-drawers), [Tag drawers](#tag-drawers) and
[Other drawers](#other-drawers).

The number fields' drawers are listed in [Number fields](#number-fields).

### Asset reference drawers

These drawers draw a field that names an asset.

| Drawer | Draws |
|---|---|
| `AbilityDrawer`, `GameplayEffectDrawer`, `ItemDrawer`, `EntityDrawer`, `AttributeDrawer`, `AttributeSetDrawer` | A reference to an ability, effect, item, entity, attribute or attribute set. |
| `ValuePipelineDrawer`, `CrowdControlRulesDrawer`, `EffectModifierDrawer` | A reference to a Value Pipeline, Crowd Control Rules or an Effect Modifier. |
| `LevelProviderDrawer` | A `[LinkedSource]` field: Unity's object field of the assets that give levels, and a line of the levels the one named gives. |

### Ability drawers

These drawers draw an ability's parts: its behaviour, stages, tasks, checks and costs.

| Drawer | Draws |
|---|---|
| `AbilityBehaviourDrawer` | An ability's behaviour (`AbilityBehaviour`) drawn whole, where an asset or a component of your own holds one: its targeting, its asset loaders, what cancels it and what it blocks, and its stages. |
| `AbilityTaskBehaviourStageDrawer` | An ability's stage: a row named by its place, such as Stage 1, with **Critical** while a task commits the ability to it. It folds to a line of what it holds. Open, it shows its policy, its usage effects, its condition, what cancels it and what it blocks, then its tasks. |
| `AbilityStageConditionDrawer` | A stage's condition: its tag. Once it names one, it shows where the stage looks for it, and whether the stage runs while the tag is missing instead. |
| `AbilityTaskDrawer` | An ability task (`AbstractAbilityTask`). |
| `TargetingTaskDrawer` | An ability's targeting task (`AbstractTargetingAbilityTask`). |
| `AssetSetterDrawer` | An ability's asset loader (`AbstractSetAssetsTask`). |
| `AbilityValidationRuleDrawer` | An ability's check (`IAbilityValidationRule`). Each list is offered the checks of its side, the caster's or the target's, and those of either. |
| `IProxyStagePolicyDrawer` | An ability stage's policy (`IAbilityProxyStagePolicy`). |
| `SequenceInjectionDrawer` | An Inject task's injection (`ISequenceInjection`). |
| `TaskSequenceReferenceDrawer` | A sequence reference: a pick of the project's `[TaskSequenceMethod]` methods by name, None first. Each class's methods sit under its name where more than one class holds them. A method the project no longer has shows as missing. |
| `ClockChoiceDrawer` | A clock picked in an inspector, such as a Run Sequence task's **Clock**: Caster's, Game's, Unscaled, Scaled, or one of your own. |
| `ChargeRechargeDrawer` | A Charges component's **Recharge**: Each Charge, All At Once, or one of your own. |
| `AttributeCostDrawer` | An attribute cost: the attribute it spends, or the role whose attribute it spends, and how much. |
| `CrowdControlRuleDrawer` | A crowd control rule: its tag, what it stops, and the tags of the abilities it stops where it stops some. |

### Effect drawers

These drawers draw an effect's parts, and an Effect Modifier's change.

| Drawer | Draws |
|---|---|
| `GameplayEffectDurationDrawer` | An effect's duration: its policy, then what the policy uses. |
| `GameplayEffectImpactDrawer` | An effect's impact: its attribute, operation and magnitude, its delivery, its impact types and whom it affects. Then its additional impacts, with **Add Impact**, and its contained effects. |
| `AttributeImpactEntryDrawer` | An additional impact: a row of its attribute and a remove button, folding to a line, such as "Mana −10 · Continuous". Its delivery offers what the effect's duration can make, and says so where it can't. |
| `ContainedEffectPacketDrawer` | A contained effect on one row: when it's applied, then the effect. |
| `AttributeTransferDrawer` | A transfer: when it moves, who gives and of what, who gets and in what, which of the values, and how much. |
| `EffectPurgeDrawer` | An effect's Purges: a foldout of its fields, its arrow in line with the lists' around it. |
| `PurgeFilterDrawer` | A purge's filter (`AbstractPurgeFilter`), such as Except Tagged. |
| `StackBehaviourDrawer` | A stacking effect's stack behaviour (`AbstractStackingConfig`). |
| `EffectChangeDrawer` | An Effect Modifier's change (`AbstractEffectChange`). |

### Attribute and entity drawers

These drawers draw attributes, attribute set elements, roles and an entity's parts.

| Drawer | Draws |
|---|---|
| `AttributeSetElementDrawer` | An attribute set element: a row of its attribute, folding to a line of what it starts at. Open, it shows what it starts at at each level, its Base and Current, its bounds, its constraints, its retention group and its collision. A typed magnitude is kept within its bounds. |
| `AttributeOverflowDataDrawer` | An attribute's bounds: its policy, then the floor and the ceiling it reads. A bound's right-click menu sets it to no limit. |
| `AttributeConstraintsDrawer` | An attribute's constraints: **Auto Clamp**, **Scale With Base** and **Rounding**, with **Snap Interval** under Rounding while it snaps. |
| `AttributeValueDrawer` | An attribute value: its Current and Base side by side. |
| `AttributeRoleDrawer` | A project's role on one row: its tag, then the attribute that plays it. |
| `StartingItemDrawer` | An entity's starting item on one row: the item, then **Equip**, which equips it as it's given. |
| `RespawnPlaceDrawer` | A Respawn's place: Home, Where It Died, Point, or one of your own. |

### Worker and component drawers

These drawers draw workers, worker groups and components.

| Drawer | Draws |
|---|---|
| `AttributeWorkerDrawer`, `FocusedAttributeWorkerDrawer`, `RelativeAttributeWorkerDrawer` | An attribute worker, of each kind. |
| `ImpactWorkerDrawer`, `ContextImpactWorkerDrawer`, `EffectWorkerDrawer`, `TagWorkerDrawer`, `AnalysisWorkerDrawer` | An impact, effect, tag or analysis worker ([WORKERS.md](WORKERS.md)). |
| `StandardWorkerGroupDrawer` | A worker group: its four lists of workers. |
| `TagWorkerRequirementsDrawer`, `TagWorkerRequirementPacketDrawer` | A tag worker's requirements: a row for each, with its tag, its policy and the weight it requires. |
| `EffectComponentDrawer`, `AbilityComponentDrawer`, `EntityComponentDrawer`, `ItemComponentDrawer` | An effect's, ability's, entity's or item's component. |
| `AuraEntryDrawer`, `EffectAreaDrawer` | An Aura component's aura and an Area component's area, drawn as the component's own fields. |

### Tag drawers

These drawers draw tags, tag queries and tag requirements.

| Drawer | Draws |
|---|---|
| `TagDrawer` | A tag field ([Tag fields](#tag-fields)). |
| `TagQueryDrawer` | A tag query: its tag and which tags match it, then how many it counts, as a comparison and a number on one row. |
| `EffectTagRequirementsDrawer` | An effect's tag requirements on one side, the target's or the source's, as a table. Its rows are Applying, While On and Removed By, each with what it requires and what it must not have. A cell opens its list under the table, with its group's template. |
| `AbilityTagRequirementsDrawer` | An ability's tag requirements as the same table: the caster's, the caster's while the ability runs, and the target's. |
| `AvoidRequireTagGroupDrawer` | A requirement group of its own, such as a targeting task's or a Requirement Template's, as the same table of one row. |

### Other drawers

These drawers draw the rest of PlayForge's fields, and the classes with no drawer of their own.

| Drawer | Draws |
|---|---|
| `ProcessInstantiatorDrawer` | A mono process's instantiator (`AbstractMonoProcessInstantiator`), or None. |
| `CueHandlerDrawer` | A cue's handler (`AbstractCueHandler`). |
| `ValueStageDrawer` | A Value Pipeline's stage (`AbstractValueStage`). |
| `ChanceRollDrawer` | How a chance rolls: **Pseudo Random**, **Own Luck**, and **Luck** while Own Luck is on. |
| `DataWrapperDrawer` | A Local Data row: its key, its value's kind and its value, each a column of its own. |
| `ForgeFlagsDrawer` | A flags field of PlayForge's: `EPurgeRelation`, `EEffectCategory`, `ESpreadMoment` or `ECombatLogRecords`. It stores only the flags picked, and writes nothing as it's drawn. |
| `TextureItemDrawer` | An asset's texture on one row: its tag, then the texture. |
| `AlignedFoldoutDrawer` | PlayForge's classes with no drawer of their own, such as an ability's definition and tags, an On Hit entry, a cue's placement or a project's settings. It draws a foldout of their fields, its arrow in line with the lists' around it. A field marked `[ForgeShowWhen]` shows only while the field it names says so. It draws in an IMGUI inspector too. |

## Number fields

Every number a PlayForge asset authors is a number field, a `ScalableFloat` or a `ScalableInt`, which
`ScalableFloatDrawer` and `ScalableIntDrawer` draw.

This section covers the field in the editor, its menu and the windows it opens, and the Try sections.
[SCALERS.md](SCALERS.md#a-magnitude-field) covers the kinds and what each reads.

### The field

A number field shows its name, its kind (**Constant** or another), its number or the range it gives, such as "6–8 s",
and its field menu (⋮).

<!-- screenshot: number-field -->

#### The kind menu

- The kind menu files the kinds by category.
- It offers only those the field's context can work out, as its code declares with `[NumberContext]`.
- Where fields of several selected assets have different kinds, the kind shows as mixed. Choosing one gives it to each.

#### Kinds other than Constant

A kind other than Constant shows these on a shaded block under the field:

- a line of what the field reads, such as "0.3 × Target's Health (base)";
- **Combine**, how the field joins its number with the kind;
- a whole-number field's **Rounding**;
- the kind's own settings.

#### Messages and limits

- A field that reads its scaler and has none says it reads 0, with **Make It Constant**.
- A kind the field's context can't work out says so, and the field reads its number.
- A field whose unit is "%" shows and takes its number as a percentage.
- A field with limits keeps its constant within them, on a slider where they're a range.

#### The field menu

The field menu is the ⋮ beside the number.

<!-- screenshot: number-field-menu -->

| Item | What it does |
|---|---|
| **Save as Scaler Asset…** | Saves the field's scaler as a Scaler asset, to share ([Its windows](#its-windows)). |
| **Scaler Assets** | A submenu of the active project's Scaler assets to share. The one shared is checked. |
| **Import Values…** | Copies values per level from a Scaler asset or another field. |
| **Quick Fill…** | Fills the field's values from a pattern. |
| **Make Constant** | Makes the field's kind Constant. |

Each change is one Undo step, and reaches each selected asset's own field. A scaler field of its own, such as a Group
member's scaler, has the same menu.

### Its windows

The field menu opens three windows: Save as Scaler Asset, Import Values and Quick Fill.

#### Save as Scaler Asset

**Save as Scaler Asset** (`SaveScalerAssetWindow`) saves the field's scaler as a Scaler asset, and makes the field share
it. It waits for one selected asset.

<!-- screenshot: save-as-scaler-asset-window -->

| Control | What it does |
|---|---|
| **Name** | The Scaler asset's name. It starts as the asset's and the field's, such as "Burning Duration". |
| **Folder** | The Scaler type's folder in the active project. **Choose…** changes it. |
| **Save and Use** | Saves the field's scaler as a Scaler asset, and makes the field share it. |

#### Import Values

**Import Values** (`ScalerValuesWindow`) copies values per level into the field.

<!-- screenshot: import-values-window -->

1. Choose **Import Values…** in the field menu.
2. Pick a source. The window lists the active project's Scaler assets, then the fields of its other assets with values
   per level.
3. Compare its values with the field's, level by level, with both curves.
4. Select **Copy Values**. It copies them as one Undo step.

#### Quick Fill

**Quick Fill** (`QuickFillWizard`) fills the field's values, or the points of one spread over levels, from a pattern.

<!-- screenshot: quick-fill-window -->

| Setting | Options |
|---|---|
| Pattern, a `QuickFillWizard.EFillPattern` | **Same Value**, **Straight Line**, **Slow Then Fast**, **Fast Then Slow**, **Steps**, **Add Each Level**, **Multiply Each Level**, **Ease In and Out** or **Custom Curve**. |
| Rounding, a `QuickFillWizard.ERounding` | **None**, **Whole Numbers** or **Nearest Step**. |

A preview shows the values as the table will, with their curve. **Apply** writes them as one Undo step.

#### Number field actions in code

`ScalerActions` does in code what the kind menu and the field menu do. Each call is one Undo step that reaches every
selected asset: `SetKind`, `MakeConstant`, `CanMakeConstant`, `UseScalerAsset`, `SaveAsScalerAsset` and `CopyValues`.

### Try

A Scaler asset's inspector and a Value Pipeline's have a **Try** section, which reads the asset at each level of a range.

- The range is 1 to 5 to start.
- It reads against a stand-in caster and target, and the values a caller sets.
- Each value is typed, or filled from an **Entity**.

What's typed is kept for the session, never in the asset ([SCALERS.md](SCALERS.md#scaler-assets),
[VALUE_PIPELINES.md](VALUE_PIPELINES.md#trying-a-pipeline)).

<!-- screenshot: try-section -->

#### `ScalerTry`

`ScalerTry` does the same in code.

| Member | Description |
|---|---|
| `From`, `To` | The levels. |
| `Caster`, `Target` | Each a `ScalerTry.Side`, holding a `ScalerTry.AttributeRow` for each attribute a read asks of it. |
| `Caller` | A `ScalerTry.CallerRow` for each key. |
| `SetCaller`, `UnsetCaller` | Give a caller key, or take it. |
| `Read(scaler)` | Gives a `ScalerTry.Reading`: the value at each level, and the parts behind each. |

A side's attribute is given with `Set`, taken with `Lack`, or filled from an entity with `FillFrom`.

#### `ValuePipelineTry`

`ValuePipelineTry` puts a `Value` through a pipeline, against its `Numbers`, a `ScalerTry`. Its chances come up or miss as
`ChancesComeUp` says.

`Read(pipeline)` gives a `ValuePipelineTry.Reading`: a `ValuePipelineTry.Run` for each level. A run holds each stage's
step, what came out, and the stop that stopped the hit, where one did.

### The drawers

These drawers draw number fields and the scalers in them.

| Drawer | Draws |
|---|---|
| `ScalableFloatDrawer`, `ScalableIntDrawer` | Every `ScalableFloat` and `ScalableInt`: the number field. |
| `ScalerDrawer` | A scaler field on its own, such as an effect's **Delta Time Scaler** or a Group member's scaler: its kind, the range it gives and its settings, with a number field's menu. |
| `ScalerBehaviourDrawer` | A scaler's behaviour (`AbstractScalerBehaviour`). The list's − removes one. |
| `VarianceDrawer` | A Randomized scaler's variance: Percentage, Flat, Multiplier, or one of your own. |
| `ConditionDrawer` | A condition (`GameplayCondition`), such as a Conditional's **When**: its kind, a project's own among them, its line, then its fields. |
| `LevelThresholdDrawer` | A Threshold scaler's threshold: its level and its tier's name on one row, then what the scaler reads from there. |
| `MagnitudeModifierGroupMemberDrawer`, `CachedMagnitudeModifierGroupMemberDrawer` | A Group's member: how its result joins the members' before it, then its scaler. |
| `NumberSiblingFieldDrawer` | The field a Relative to a Field number reads, picked from the other number fields of its object. A field that reads it back stays out, so a loop can't be picked. |

## The Problems view and validation

PlayForge's validator checks every PlayForge asset in the Unity project as it changes, and keeps what it finds. The
Problems view lists it.

This section covers the view, fixing problems, the severities, PlayForge's rules, the validator, and adding a rule of
your own.

### The Problems view

The Problems view (`ForgeProblemsWindow`) lists what the validator finds. It follows the validator, so it has no Refresh.

<!-- screenshot: problems -->

#### Opening the view

| From | Opens |
|---|---|
| **Window → PlayForge → Problems** | The view. |
| The Forge's problem count, or its window menu | The view. |
| An inspector header's problem count | The view, searching for that asset. |

In code, `ForgeProblemsWindow.ShowWindow()` opens the view, and `ForgeProblemsWindow.ShowFor(asset)` opens it on an
asset.

#### Toolbar

| Control | What it does |
|---|---|
| **List**, **By Rule** | Switch between the view's two views. |
| **Errors**, **Warnings**, **Notes** | Each is a filter that counts its problems, such as **2 Errors**. |
| Search | Finds a problem by its asset or its words. |

#### List view

**List** has a row for each problem: the errors first and the notes last, then by asset. A row shows its severity, its
asset by type icon and name, and what's wrong. The columns sort.

| Action | Result |
|---|---|
| Click | Selects the asset and shows it in the Project window. |
| Double-click | Opens the asset in a window of its own. |

#### By Rule view

**By Rule** groups the problems under the rule each breaks, a rule with errors first. A rule's arrow folds it.

<!-- screenshot: problems-view-by-rule -->

A problem that one step fixes has a quick fix: a button named for the step, such as **Store the Tag**. Its rule has
**Fix All**, or **Fix 2** where only two of its problems have one. Each is one Undo step.

#### Status bar

The status bar says how many problems there are, in how many assets, and the notes beside them. It also says how many
are shown, and, in By Rule, how many have a quick fix.

### Fixing problems

A problem that one step fixes has a quick fix in the Problems view, which you apply with one click.

To fix a problem with a quick fix:

1. Open the Problems view from **Window → PlayForge → Problems**, or from a problem count.
2. Select **By Rule**.
3. Find the problem under the rule it breaks.
4. Select its quick fix, the button named for the step, such as **Store the Tag**.

To fix every problem of a rule that has a quick fix, select the rule's **Fix All**, or **Fix 2** and so on. Each is one
Undo step. [PlayForge's rules](#playforges-rules) lists each rule's quick fix.

### Severities

A problem's severity is a `ForgeProblemSeverity`, which says how much the problem matters.

| Severity | Meaning |
|---|---|
| **Error** | Something that stops working, shown in the destructive color. |
| **Warning** | Likely a mistake. |
| **Note** | What may be meant, such as a trigger on an event that code sends. |

The view lists notes under their own filter. The counts leave notes out: the view's, The Forge's and an inspector
header's.

### PlayForge's rules

Each of PlayForge's rules finds what an inspector warns about in place, in the same words. By Rule names each:

| Rule | Finds | Severity | Quick fix |
|---|---|---|---|
| Data whose class is missing | An asset that holds data whose class Unity can't find, as after a class is renamed or removed | Error | — |
| Assets with no name | A PlayForge asset with no name | Warning | — |
| Asset tags their names don't give | A stored asset tag that isn't the one the asset's name gives | Warning | **Store the Tag**, which carries the new tag to what stored the old |
| Links with no level provider | An asset set to take a level provider's levels, with none named | Warning | **Use Own Levels** |
| Impacts that don't land as set | An effect's first impact whose settings read another way than they seem, or an additional impact whose delivery the effect's duration can't make | Warning | — |
| Fields with no scaler to read | A number field that reads its scaler and has none; a Shared kind with no Scaler asset; a Group whose members multiply with none that adds | Warning | **Make It Constant**, for the first |
| Numbers whose kind can't be read where they are | A number whose kind reads what its field doesn't have, such as a target; a relative number that names no field, or a field its object hasn't got | Warning | — |
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

#### Rules that read across assets

Three of the rules read what the project's assets give each other (`ForgeLint`): Stacks read twice, Filters that never
match and Events nothing sends. They read:

- the impact types the project's effects and impact workers deal;
- the gameplay events its thresholds and stop stages send;
- the effects its items apply.

`ForgeLint.Forget()` has it gather them again on its next check, such as after a script changes assets in bulk.

### The validator

`ForgeValidator` runs every `ForgeValidationRule` on each asset it checks. It checks every PlayForge asset, Scaler asset,
Requirement Template, Value Pipeline, Effect Modifier and Crowd Control Rules (`Checks(asset)`).

#### When it checks

The validator follows the project, so nothing polls.

- An edit checks its asset again.
- An import checks what it brought.
- A delete or a move has every asset checked again, as they're next asked for.
- So does an edit to what one asset gives others, such as the impact types an effect deals or a Cue Library's routes.

A rule that throws doesn't keep the others from checking.

#### Members

| Member | Description |
|---|---|
| `Check(asset)` | Runs every rule on an asset, saved or not. |
| `ProblemsOf(asset)`, `NotesOf(asset)` | What it found in one asset. |
| `All`, `Notes` | What it found in every asset. |
| `Changed` | Raised as what it knows changes. |

#### Problems and quick fixes

Each problem is a `ForgeProblem`.

| Property | Description |
|---|---|
| `Asset` | The asset it's in. |
| `Severity` | Its severity. |
| `Message` | What's wrong. |
| `PropertyPath` | The field it's about. |
| `Rule` | The rule that found it. |
| `Fix` | Where one step fixes it, a `ForgeQuickFix`. |

A `ForgeQuickFix` has its `Name`, as its button says, and the step it applies, `Apply`.

#### On a build server

On a build server, run the validator in batch mode:

```
Unity -batchmode -quit -projectPath <project> -executeMethod FarEmerald.PlayForge.Editor.ForgeValidator.LogAll
```

`LogAll` checks every asset afresh, and writes to the Console, in this order:

1. each problem, an error as an error and a warning as a warning, each starting "[PlayForge problems]" and its asset's
   path;
2. each note as a plain line, so a build that fails on warnings passes them;
3. how many there are.

### A rule of your own

A rule of your own adds a check to the validator.

1. In an editor assembly, write a class that derives from `ForgeValidationRule`, with a public constructor that takes
   nothing.
2. Override `Check(asset, problems)`.
3. Override `Name` and `Reads(asset)` where their defaults don't fit.

The validator finds the rule.

| Member | Description |
|---|---|
| `Check(asset, problems)` | Adds a `ForgeProblem` for each problem it finds in the asset. |
| `Name` | The name By Rule groups its problems under. Unless the rule says, it's its class's name, spaced out, without "Rule". |
| `Reads(asset)` | True for an asset it reads to check others, so an edit to that asset has every asset checked again. |

#### Example

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

#### A quick fix of your own

A problem one step fixes carries the step as its `Fix`. The step writes through a `SerializedObject` of the asset, or
records Undo, so the Undo step the view opens around it takes it back.

#### A rule about components

A rule about one kind of component derives from `ForgeComponentRule<TComponent>`. It hears each component of its kind an
asset holds, with its place in the list.

It's an `IForgeComponentRule`, whose warnings and errors an ability's inspector shows under its Components list.
[EXTENDING.md](EXTENDING.md) covers writing rules.

## The Debugger

The Debugger (`RuntimeDebugger`, **Window → PlayForge → Debugger**) shows the game as it plays, and changes its entities
as it shows them. This section covers its four views, Entities, Processes, Tags and Timeline, and the Profiler's
PlayForge modules.

<!-- screenshot: debugger -->

Outside Play mode, the Debugger says so, with **Enter Play Mode**.

In Play mode, it follows ProcessControl: its lists change as processes register and leave. Their values change in place,
as often as **Samples a Second**, in its window menu, says: 2, 4, 10 or 30 times a second, 4 to start.

While the Debugger reads them, ProcessControl times each process's steps.

### The toolbar and status bar

The Debugger's toolbar and status bar frame each of its views.

| Bar | Holds |
|---|---|
| Toolbar | The views to switch between, the search, each view's own tools, and, in Play mode, the frame count. |
| Status bar | ProcessControl's state; the processes, and how many run, wait or are being created; and the entities by hub. On its right, it says what the window's last action did, such as "Applied Burning" or "Burning was refused". |

In a narrow window, such as a docked column, each view's list sits above its detail, and the frame count moves to the
status bar.

Each detail's sections keep their fold for the session.

### Entities

**Entities** lists the hubs ProcessControl runs, `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` alike.

<!-- screenshot: debugger-entities -->

| Column | Shows |
|---|---|
| **Name** | The entity's name. A MonoBehaviour hub goes by its GameObject's name, which tells two of one asset apart. |
| **Hub** | Mono or Runtime. |
| **Lvl** | Its level. |
| **Effects** | The number of effects on it. |

#### Finding an entity

- The toolbar's hub filter shows **Both hubs**, **Mono hubs** or **Runtime hubs**.
- With **Follow Selection** on, selecting an entity's GameObject, or a child of it, in the scene shows that entity.
- A double click selects a MonoBehaviour hub's GameObject.

#### The entity's detail

The detail's header names the entity, such as "Entity · Goblin Raider · Mono hub · Level 3". It has **Select in Scene**,
and **Open Asset**, which opens its asset in a window of its own.

Each of its sections has a table. A click selects a row, and a foot row acts on the row selected and adds to the table.

#### The Attributes section

**Attributes** shows each of the entity's attributes.

| Column | Shows |
|---|---|
| **Base**, **Current** | The attribute's values. The current's tooltip breaks it down by retention group. |
| **Changed by** | Each source of what it took in the last second, such as "Burning -8/s", then each effect that holds a modifier on it. |

To set an attribute's value:

1. Select the attribute's row.
2. Type the value in the foot row's **Base** or **Current**.
3. Press Enter.

The change is one the entity makes on its own account, an unworkable one
([WORKERS.md](WORKERS.md#workable-and-unworkable)). A double click on a value goes to its field.

#### The Effects section

**Effects** shows each effect on the entity.

| Column | Shows |
|---|---|
| **Left** | The time it has left. |
| **Stacks** | Its stacks. |
| **From** | Where it's from, such as "Player · Fireball". |

**Remove** takes the selected effect off, where its source applied it.

To apply an effect:

1. Pick the effect in the foot row's field.
2. Select **Apply**. The effect lands from the entity itself.

#### The Abilities section

**Abilities** shows each of the entity's abilities.

| Column | Shows |
|---|---|
| **Lvl** | The ability's level. |
| **State** | Its state, such as Stage 2/3, Active, Cooldown or Ready. |
| **Cooldown** | The cooldown it has left. |

**Activate** sets the selected ability off as the entity would, and **Interrupt** stops its run.

To grant an ability:

1. Pick the ability in the foot row's field.
2. Set its **Level**.
3. Select **Grant**. The ability is granted at that level.

#### The Tags section

**Tags** shows each tag the entity holds.

| Column | Shows |
|---|---|
| **Count** | Its count, by weight. |
| **From** | What it's from. |

**Remove** takes back the selected tag, where it's a loose one.

To grant a loose tag:

1. Type a tag in the foot row's field, or pick one from the project's with its button.
2. Select **Add Tag**, or press Enter.

#### The Combat Log section

**Combat Log** shows the entity's combat log, newest first, as its project writes it. A click on an entry shows its hit's
run through its pipeline and its steps ([ENTITIES.md](ENTITIES.md#combat-log)).

### Processes

**Processes** lists every process ProcessControl holds. Each column sorts.

<!-- screenshot: debugger-processes -->

| Column | Shows |
|---|---|
| **ID** | Its ID. |
| **Process** | Its name. |
| **Handler** | Its handler. |
| **Lifecycle** | Its lifecycle. |
| **Timing** | The update loops it steps in. |
| **State** | Its state, with the state it moves to next where something has asked, such as "Running → Terminated". |
| **Lifetime** | Its lifetime. |
| **Step** | Its average time a step, over the last second. |

The toolbar's filter shows **All states**, **Running** or **Waiting**. Its **ProcessControl** menu sets ProcessControl's
own state, from **Ready** to **Terminated Immediately**.

#### The process's detail

The detail names the process, its ID, and whether it's a Mono or a Runtime process. A Mono one has **Select in Scene**.

| Button | What it does |
|---|---|
| **Pause** | Pauses it, and the processes it handles. |
| **Resume** | Resumes it, and the processes it handles. |
| **Terminate** | Ends it, and the processes it handles, as its lifecycle allows. |
| **Terminate Now** | Ends it, and the processes it handles, at once. |

Its sections say how it runs:

| Section | Shows |
|---|---|
| **Process** | Its lifecycle, step timing, step priority and handler, with **Show**, which selects the handler's process in the list. |
| **State** | Its state, the state it's queued to move to, and its **Run**. |
| **Time** | Its lifetime, its time running, its step time and its peak step. |

A **Run** is going, held by a pause, not started, cancelled, or ended. Once it's cancelled, **Resume** starts a new one.
[PROCESSES.md](PROCESSES.md) covers processes.

### Tags

**Tags** shows every tag the listed entities hold, as a tree of their paths. The hub filter applies.

<!-- screenshot: debugger-tags -->

| Column | Shows |
|---|---|
| **Entities** | How many entities hold it. |
| **Weight** | Their weight. A parent counts its children's. |

#### Held By

A tag's detail lists who holds it under **Held By**.

| Column | Shows |
|---|---|
| **Entity** | The entity that holds it. |
| **Weight** | Its weight. |
| **Granted by** | What grants it. |
| **Source** | The source of what grants it. |

A **Source** is one of these:

- its attribute set;
- an effect, and who applied it;
- an ability's asset tag, or its passive or active tags;
- an item;
- the entity's own asset tag, affiliation or granted tags;
- a loose tag, granted directly.

### Timeline

**Timeline** shows what the entities' abilities and effects did over the last seconds.

<!-- screenshot: debugger-timeline -->

#### What it records

While the Debugger is open in Play mode, the Timeline records, whatever view shows:

- each ability's runs, stage by stage;
- each refused activation;
- each effect's time on the shelf;
- each refused effect;
- an effect journal of the entities it lists.

It starts afresh each time Play mode does.

#### Timeline tools

| Control | What it does |
|---|---|
| **Record** | Stops and starts recording. |
| **Clear** | Forgets what it recorded. |
| **Last 10 s** | Picks how far back the strips reach: 5, 10, 30 or 60 seconds. |

#### Tracks

The Timeline's list has a track for each entity's ability or effect, the newest first. A track shows its number of
**Runs**, and a strip of its spans:

- a bar for a run, or for an effect's time on the shelf;
- a tick for an instant effect or a refusal.

The search finds a track by its entity or its asset. A track keeps its last 100 spans.

#### An ability's track

An ability's track shows its **Stages**: each with its policy and its tasks, when the selected run reached it and how long
it stayed, or that it didn't.

Then it shows its **Runs**, the latest first. A click on a run shows its stages.

| Column | Shows |
|---|---|
| **Start** | When the run started. |
| **Length** | How long it ran. |
| **Stages** | The stages it reached. |
| **Outcome** | Running, Ended, Stopped, or the refusal and why. |

#### An effect's track

An effect's track shows its **Applications**, its refusals among them, each with its **Start**, **End** and **Length**.

Then it shows its **Journal**, newest first: each landing, tick, hit and removal of the effect on the entity, and each
worker that ran. The journal keeps its last 1,000 entries.

A click on an entry shows its parts: a number's trace, a hit's breakdown, or what a worker queued and what came of it
([ENTITIES.md](ENTITIES.md#effect-journal)).

### Profiler modules

Two modules chart PlayForge's counters in Unity's Profiler. Turn them on in its list of modules.

<!-- screenshot: profiler-modules -->

| Module | Charts |
|---|---|
| **PlayForge Processes** (`PlayForgeProcessesProfilerModule`) | Processes Running, Processes Waiting, Processes Created, Registrations, Terminations, Steps, State Changes, Step Time, Sequences Running and Stages Started. |
| **PlayForge GAS** (`PlayForgeGASProfilerModule`) | Mono Entities, Runtime Entities, Effects Active, Effects Applied, Effects Removed, Impacts, Activations, Refused Activations, Tag Compiles and Recomputes. |

The details of **PlayForge Processes** list the selected frame's costliest processes, each by ID, type and step time.
PlayForge writes them into the capture as the Profiler records, so a saved capture shows them too.

[GETTING_STARTED.md](GETTING_STARTED.md#profiling) lists PlayForge's profiler markers and counters.

## Preferences and Project Settings

PlayForge has two settings pages. **Project Settings → PlayForge** holds the settings a project shares, kept in assets
under version control. **Preferences → PlayForge** holds each user's own.

This section covers both, and the logging level.

### Project Settings → PlayForge

**Project Settings → PlayForge** holds the settings a project shares: the PlayForge Folder, and the active Forge
Project's settings.

<!-- screenshot: project-settings -->

#### About this PlayForge

The page opens with which PlayForge this is: its version, package name and the Unity it needs, as its `package.json`
says. A row of links follows. Each site link's tooltip is its address.

| Link | Opens |
|---|---|
| **Guides** | The documentation site. |
| **Editor Tools** | This guide. |
| **API Reference** | The API reference. |
| **Changelog** | The package's `CHANGELOG.md`. |

#### The folder setting

The **PlayForge Folder** is the Unity project's own setting, kept in `ProjectSettings/PlayForge.asset`.

| Property | Description |
|---|---|
| **PlayForge Folder** | Where a new Forge Project goes, each in a folder of its own, and the Bootstrap prefabs. It's `Assets/PlayForge` to start. **…** picks a folder in `Assets`. |

#### The active project's settings

The rest of the page is the active Forge Project's, kept in its asset, so each game has its own. Each edit is saved in
the project's asset, and Undo takes it back.

With no project active, the page says so. Choose one in The Forge. While the Unity project has none,
**Create Forge Project** makes one named for the game.

#### Forge Project

**Forge Project** holds what the project's game reads as it runs, through its GameRoot.

| Property | Description |
|---|---|
| **Roles** | The attribute playing each role, by its tag ([ATTRIBUTES.md](ATTRIBUTES.md#roles)). |
| **Crowd Control** | The rules an entity follows where it names none ([ABILITIES.md](ABILITIES.md#crowd-control)). |
| **Cue Library** | The project's Cue Library ([CUES.md](CUES.md#routes)). |
| **Resolve Order** | The order an area's effects land on those it takes, by the tags they carry ([EFFECTS.md](EFFECTS.md#resolve-order)). |
| **Combat Log** | What each entity's combat log writes, and how many entries it keeps ([ENTITIES.md](ENTITIES.md#combat-log)). |
| **Purge Levels** | The tiers a purge reaches, and what a death keeps ([EFFECTS.md](EFFECTS.md#purging)). |
| **Workers** | Every entity's workers ([WORKERS.md](WORKERS.md#where-workers-come-from)). |

**Fill Roles by Name** gives each of PlayForge's roles no attribute plays the project's attribute of that name, as one
Undo step. The roles are Health, Luck and Movement Speed, and Movement Speed takes Move Speed too.

#### New Assets

**New Assets** sets each type's folder within the project's folder. The Forge and a field's **New** put the type's new
assets there.

| Control | What it does |
|---|---|
| A type's folder | Where its new assets go. **…** picks one inside the project's folder. |
| **Use Default Folders** | Puts each folder back to its default. |

#### Defaults

**Defaults** sets two things for each type.

| Property | Description |
|---|---|
| **New Asset** | The asset a new asset of the type starts as a copy of. |
| **Import** | The assets the Import window lists first. |

#### Cost and Cooldown

**Cost and Cooldown** names the project's Cost and Cooldown effects.

| Property | Description |
|---|---|
| **Cost Effect** | The effect an ability's **Cost** field offers to copy or use, and an Active item copies. |
| **Cooldown Effect** | The effect an ability's **Cooldown** field offers to copy or use, and an Active item copies. |

#### The Templates list

The page's last group, **Templates**, lists the project's own templates.

| Property | Description |
|---|---|
| **Templates** | The project's own assets that **Create** offers to start a new asset as a copy of. |

#### Project settings in code

`PlayForgeSettings` reads and writes the project's authoring settings.

| Member | Description |
|---|---|
| `FolderFor(type)` | The folder a new asset of the type goes in. |
| `NewAssetDefault(type)` | The asset a new asset of the type starts as a copy of. |
| `ImportDefaults(type)` | The assets Import lists first for the type. |
| `UsageEffect(key)`, `SetUsageEffect(key, effect)` | Get or set the Cost or Cooldown effect, by "Cost" or "Cooldown". |
| `SetNewAssets` | Sets where a type's new assets go. |
| `SetDefaults` | Sets a type's defaults. |
| `UseDefaultFolders()` | Puts every type's folder back to its default. |
| `DefaultFolder(type)` | The folder a type's new assets go in until a setting says otherwise. |
| `UseProject(project)` | Reads another project's settings in place of the active one's, and gives back the one it replaced. |

`EffectTemplateRegistry` gets and sets the Cost and Cooldown effects by their keys, "Cost" and "Cooldown". The settings
themselves are the project's `ForgeProjectSettings`, and the authoring ones its `ForgeAuthoringSettings`.

### Preferences → PlayForge

**Preferences → PlayForge** holds each user's own preferences, kept in EditorPrefs.

<!-- screenshot: preferences -->

#### Tag preferences

The **Tags** group sets how tag fields and the tag picker show tags.

| Property | Description |
|---|---|
| **Show Full Tag Paths** | Shows a tag's whole path in strong text, in place of its parents in secondary text before its leaf. Off to start. |
| **Group Tags by Root** | Groups the tag picker's tags under their root. On to start. |

#### Menu preferences

The **Menus** group sets the most a PlayForge dropdown grows to, a picker's or a field's menu. A longer list scrolls.

| Property | Description |
|---|---|
| **Menu Width** | 360 to start, from 220 to 1200. |
| **Menu Height** | 420 to start, from 160 to 1600. |

#### Logging preferences

The **Logging** group sets how much PlayForge logs.

| Property | Description |
|---|---|
| **Minimum Log Level** | The quietest message PlayForge logs. Info to start. |

| Level | Logs |
|---|---|
| **Verbose** | Every trace. |
| **Info** | Notable events. |
| **Warning**, **Error** | Problems alone. |
| **None** | Nothing. |

#### Use Defaults

**Use Defaults** puts every preference back, the tables' column widths and hidden columns and the log level among them.

`PlayForgePreferences` holds the preferences, and its `UseDefaults()` does what **Use Defaults** does.

### The log level

The log level is the quietest message PlayForge logs, which **Minimum Log Level** sets.

- `ForgeLogSettingsBridge` keeps the log level, and gives it to `ForgeLog.MinimumLevel` after each domain reload. Its
  members are `CurrentLevel`, `SetLevel(level)` and `ResetLevel()`.
- A build logs Info unless code sets `ForgeLog.MinimumLevel` ([GETTING_STARTED.md](GETTING_STARTED.md#logging)).
- A double click on a message `ForgeLog` wrote, in the Console, opens the code that logged it, not `ForgeLog`.

## Spreadsheets

Spreadsheets write a project's numbers by level, or its items' stats, to CSV, and read a sheet back. This section names
the editor's parts. [SPREADSHEETS.md](SPREADSHEETS.md) covers them in full.

### The Spreadsheets menu

The Forge's window menu holds the spreadsheet commands under **Spreadsheets**.

| Item | What it does |
|---|---|
| **Export Numbers…** | Writes the project's numbers by level. |
| **Export Selected Numbers…** | Writes the selected assets' numbers. |
| **Export Item Stats…** | Writes the project's items' stats. |
| **Export Selected Item Stats…** | Writes the selected items' stats. |
| **Import…** | Lists a sheet's changes in a window of their own (`ForgeSpreadsheetWindow`). There, **Import** makes those ticked, as one Undo step. |

<!-- screenshot: spreadsheet-import-window -->

### Spreadsheets in code

`ForgeSpreadsheets` writes and reads sheets in code.

| Member | Description |
|---|---|
| `NumbersOf(assets)` | Gives a sheet's text of the assets' numbers. |
| `StatsOf(items)` | Gives a sheet's text of the items' stats. |
| `Read(csv)` | Gives what a sheet would change: a `SpreadsheetImport` of `SpreadsheetChange`s. |

A `SpreadsheetImport`'s `Apply()` makes those chosen.

## The Creator Lab

The Creator Lab is a scene for trying a project's own assets: units, test dummies, and its time in your hands. This
section names its editor parts, in `FarEmerald.PlayForge.Lab.Editor`. [CREATOR_LAB.md](CREATOR_LAB.md) covers the lab.

<!-- screenshot: creator-lab -->

The lab's assemblies are left out where `PLAYFORGE_NO_LAB` is defined.

### Opening the lab

`CreatorLabMenu` opens the lab from **Window → PlayForge → Creator Lab**.

It makes a new scene and plays it. First it saves the scenes open before, if you want.

`CreatorLabMenu.Open(play)` does the same in code. In Play mode, it makes the lab in the scene playing.

### The lab's library

`EditorLabLibrary` is the lab's library in the editor.

- It holds the active Forge Project's assets by kind. While the Unity project has no Forge Project, it holds every asset
  in the project's own folders.
- Its **Project** switch changes the active project, as The Forge's does.
- Given folders, it plays their assets alone.

### The lab's inspectors

`LabRulesEditor` and `LabDriverEditor` are the inspectors of the **Lab Rules** and **Lab Driver** assets. Each shows the
shared header, then the asset's fields.

## Build verification

`BuildVerification` proves that PlayForge's runtime compiles and links in a player, with no editor code in it, as a build
server would check. This section covers running it.

### Running a verification build

Run `BuildWindowsPlayer` from the command line, in batch mode:

```
Unity -batchmode -nographics -projectPath <project> -executeMethod FarEmerald.PlayForge.Editor.BuildVerification.BuildWindowsPlayer
```

`ForgeValidator.LogAll` checks the assets the same way ([The validator](#the-validator)).

### What it builds

`BuildWindowsPlayer` builds a 64-bit Windows player in `Build/Verification`.

- It builds the scenes enabled in Build Settings.
- With none enabled, it makes an empty scene for the build, and deletes it after.

It then exits Unity with a code that says how the build went:

| Exit code | Meaning |
|---|---|
| 0 | The build succeeded. |
| 1 | The build failed. |
| 3 | The build threw. |

## Editor helpers

The types in this section are public pieces PlayForge's editor builds on, which an editor script of your own can use too.

### Inspector building blocks

These types build inspector parts, and read what a serialized field is.

| Type | Does |
|---|---|
| `ForgeDrawerStyles` | Builds inspector parts: sections, fields and rows. |
| `PropertyFieldExtensions` | `OnPopulated`: runs code once a `PropertyField` has built its contents. |
| `SerializedPropertyExtensions` | `GetFieldInfo`, `GetAttribute<T>` and `HasAttribute<T>`: the field a serialized property is, found through managed references along its path, and its attributes. |
| `TypePickerCache` | The concrete classes a type allows, kept for the session, which a kind field's picker lists. |

#### `ForgeDrawerStyles`

`ForgeDrawerStyles` builds inspector parts. Besides sections, it also makes fields and rows.

| Member | Description |
|---|---|
| `CreateCollapsibleSection` | Makes a section, with its Import, Clear and Help, from a `ForgeDrawerStyles.SectionConfig`. It gives a `ForgeDrawerStyles.SectionResult`: the section's header, content, summary and buttons. |
| `ForgeDrawerStyles.SectionConfig` | A section's name, title and help topic, and the serialized object and property paths Import and Clear act on. |
| `ForgeDrawerStyles.Colors`, `ForgeDrawerStyles.Icons` | Its colors and text icons. |
| `CreateMainHeader` | A header PlayForge's own inspectors don't use. `ForgeDrawerStyles.HeaderConfig` and `ForgeDrawerStyles.HeaderResult` belong to it. |

### Asset and number helpers

These types work out what an asset or a number field holds, as the editor shows it.

| Type | Does |
|---|---|
| `AttributeStartValues` | What each attribute an attribute set gives starts at, at each level, worked out as the runtime does. `Of(set)` gives an `AttributeStartValues.Result`, with an `AttributeStartValues.Start` for each attribute. The Attribute Set and Entity inspectors' tables read it. |
| `NumberFields`, `NumberLimits` | What the number fields know: which properties are numbers, a field's unit, limits and context, the kinds a field's menu offers and where it files them, and what a number's kind reads. `NumberFields.ScalersIn` walks into the scalers it holds, as a `NumberFields.Walk` says. |
| `ScalerActions` | The number field's kind menu and field menu in code ([Number fields](#number-fields)). |
| `ForgeAssetLines` | An asset's one line, as its header and The Forge's **Summary** show it. |
