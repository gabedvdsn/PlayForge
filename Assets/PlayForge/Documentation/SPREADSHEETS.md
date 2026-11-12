# PlayForge — Spreadsheets

Games balance in spreadsheets: a column of damage by level, a row of stats for each item. PlayForge writes its numbers
by level and its items' stats to CSV, which any spreadsheet opens, and reads a sheet back, listing each change before it
makes it (TW-27). The Forge's window menu (its tab's ⋮) holds them under **Spreadsheets**:

- **Export Numbers…** writes a sheet of every number by level in the active Forge Project; **Export Selected
  Numbers…** the selected assets' alone.
- **Export Item Stats…** writes a sheet of every item's stats; **Export Selected Item Stats…** the selected items'.
- **Import…** reads a sheet back, of either kind, and opens the import window.

A sheet is UTF-8, marked so, as Excel and Google Sheets open it. In code: `ForgeSpreadsheets.NumbersOf(assets)` and
`StatsOf(items)` give a sheet's text, and `ForgeSpreadsheets.Read(csv)` what it would change.

## Numbers by level

A row for each number whose kind is a **Table** or **By Level** ([SCALERS.md](SCALERS.md)), of every asset, and of each
Scaler asset, whose table its fields share:

| Asset Tag | Name | Field | Kind | Level 1 | Level 2 | Level 3 |
|---|---|---|---|---|---|---|
| Effect.LagunaBlade | Laguna Blade | Impact Specification › Magnitude | Table | -450 | -600 | -750 |
| AttributeSet.Hero | Hero | Attributes › Health › Base | Table | 500 | 560 | 620 |

- **Field** is the number's place in the asset, its parts as the inspector names them; a list's element is named by the
  attribute it names, as an attribute set's Health, or else by its place from 1, as Additional Impacts 2.
- The levels are **what the number reads** at each level: its values combined with its number as its field says, so a
  table of 450, 600 and 750 read times -1 is -450, -600 and -750. Reading one back sets the values so it reads the row.
- A number that reads its number alone, or a kind of another sort, as × Attribute, has no row.

## Item stats

A row for each item, and a column for each attribute its granted effects add to:

| Asset Tag | Name | Armor | Health |
|---|---|---|---|
| Item.Ring | Ring | 5 | 10/15/20 |
| Item.Band | Band | | |

A cell is the number the item's effect adds to the attribute, or one for each level, as 10/15/20. An item whose effects
add to an attribute twice leaves that cell empty: set each in a sheet of numbers. Reading a sheet back sets the effect's
number: a constant's, or a Table's values; one for each level makes a constant a Table. An empty cell changes nothing.

## Reading a sheet back

A row names its asset both ways: by its **asset tag**, which stays as files move and are renamed, and by its **name**,
which reads well in a spreadsheet. A row with a tag finds the asset with that tag; a row with none finds the one asset
of that name. These are reported, and their rows left:

- a tag no asset has, or a name no asset has, or one that two assets share (give that row its tag);
- a field the asset hasn't got, or a kind a sheet can't set;
- an item with no effect adding to a column's attribute;
- a cell that isn't a number;
- two rows setting one shared effect's number to different values.

The **import window** lists each change: the asset, the field, what it reads now and what it will. Each is ticked;
untick one to leave it. The rows that can't be read sit under a fold. **Import** makes the ticked changes as one Undo
step. A sheet read back just as it was written changes nothing.
