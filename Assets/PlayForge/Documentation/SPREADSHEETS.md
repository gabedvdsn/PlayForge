# PlayForge — Spreadsheets

Games are balanced in spreadsheets: a column of damage by level, a row of stats for each item. PlayForge writes its
numbers by level and its items' stats to CSV, which any spreadsheet opens. It also reads a sheet back, and lists each
change before it makes it.

The Forge's window menu (its tab's ⋮) holds these commands under **Spreadsheets**:

| Command | What it does |
|---|---|
| **Export Numbers…** | Writes a sheet of every number by level in the active Forge Project. |
| **Export Selected Numbers…** | Writes a sheet of the selected assets' numbers alone. |
| **Export Item Stats…** | Writes a sheet of every item's stats in the active Forge Project. |
| **Export Selected Item Stats…** | Writes a sheet of the selected items' stats. |
| **Import…** | Reads a sheet of either kind back, and opens the import window. |

A sheet is UTF-8, marked as such with a byte order mark, so Excel and Google Sheets open it as UTF-8.

In code, `ForgeSpreadsheets.NumbersOf(assets)` and `StatsOf(items)` give a sheet's text.
`ForgeSpreadsheets.Read(csv)` gives what a sheet would change.

## Numbers by level

A sheet of numbers by level has a row for each number whose kind is a **Table** or **By Level**
([SCALERS.md](SCALERS.md)). It holds the numbers of every asset, and of each Scaler asset, whose table its fields share:

| Asset Tag | Name | Field | Kind | Level 1 | Level 2 | Level 3 |
|---|---|---|---|---|---|---|
| Effect.LagunaBlade | Laguna Blade | Impact Specification › Magnitude | Table | -450 | -600 | -750 |
| AttributeSet.Hero | Hero | Attributes › Health › Base | Table | 500 | 560 | 620 |

| Column | What it holds |
|---|---|
| **Asset Tag**, **Name** | The asset the number is in. A row read back finds its asset by them ([Reading a sheet back](#reading-a-sheet-back)). |
| **Field** | The number's place in the asset, its parts named as the inspector names them. |
| **Kind** | The number's kind, **Table** or **By Level**. |
| **Level 1**, **Level 2** and on | What the number reads at each level. |

In **Field**, a list's element is named by the attribute it names, as an attribute set's Health. An element that names
no attribute is named by its place from 1, as Additional Impacts 2.

The levels hold **what the number reads** at each level: its values combined with its number, as its field says. So a
table of 450, 600 and 750, read times -1, is -450, -600 and -750. Reading a row back sets the values so the number reads
what the row says.

A number that reads its number alone has no row. Neither has a number of another kind, as × Attribute.

## Item stats

A sheet of item stats has a row for each item, and a column for each attribute its granted effects add to:

| Asset Tag | Name | Armor | Health |
|---|---|---|---|
| Item.Ring | Ring | 5 | 10/15/20 |
| Item.Band | Band | | |

- A cell is the number the item's effect adds to the attribute, or one for each level, as 10/15/20.
- An item whose effects add to an attribute twice leaves that cell empty. Set each of those numbers in a sheet of
  numbers instead.

When you read a sheet back:

- A cell sets the effect's number: a constant's value, or a Table's values.
- A cell with one number for each level makes a constant a Table.
- An empty cell changes nothing.

## Reading a sheet back

Reading a sheet back finds the asset each row names, and lists the changes the sheet makes before it makes them.

A row names its asset in two ways:

- by its **asset tag**, which stays the same as files move and are renamed;
- by its **name**, which reads well in a spreadsheet.

A row with a tag finds the asset with that tag. A row with no tag finds the one asset of that name.

The import reports these problems, and leaves their rows out:

- a tag no asset has, a name no asset has, or a name two assets share (give that row its tag);
- a field the asset hasn't got, or a kind a sheet can't set;
- an item with no effect adding to a column's attribute;
- a cell that isn't a number;
- two rows setting one shared effect's number to different values.

To import a sheet:

1. In The Forge's window menu, choose **Spreadsheets** > **Import…**, and pick the CSV file.
2. The **import window** lists each change: the asset, the field, what it reads now and what it will read. The rows that
   can't be read sit under a fold.
3. Each change is ticked. Untick one to leave it.
4. Click **Import** to make the ticked changes, as one Undo step.

A sheet read back just as it was written changes nothing.
