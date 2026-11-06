# PlayForge editor style

These are the rules Phase 4's redesign follows (Decision 20). They come from the UI interview (Decision 18) and from
the style mockups, a private design canvas the user owns: https://claude.ai/artifact/PSDAMf8GYvhzZDb1uoF5gr. Every
value below is one the mockups use. The Q numbers are the interview's questions.

## Principles

- **Native first.** A PlayForge window uses Unity 6's own controls, neutrals and density (Q2), so it looks like it
  shipped with the editor.
- **Distinct through what it shows, not through decoration.** PlayForge is recognizable by how it lays out a GAS:
  section summaries, the requirements table, problems in place, and a list that shows what uses what. Ornament never
  does that job.
- **Legible in both themes** (Q10). Every text color passes 4.5:1 on its background, in Dark and in Light.
- **Easy to work with.** One label column, one row height, controls that are always visible, and nothing that moves
  when the pointer passes over it.

## Never

- Colored borders, stripes or accent dividers on sections, cards or rows.
- Tinted, rounded or floating containers, and drop shadows. A scaler's block is the one exception (Decision 36, under
  Inspectors).
- Small, bold, letter-spaced or uppercase headings.
- Gradients and emoji.
- Controls that appear only on hover (Q18).
- Color as the only signal. Type icons differ in shape, and a problem carries a glyph and a count.
- Toasts and pop-up messages. A message sits next to what it is about.

## Color

Unity's neutrals carry the whole window.

| Token | Dark | Light | Used for |
|---|---|---|---|
| Panel | `#383838` | `#C8C8C8` | Window and inspector background |
| Tab strip | `#282828` | `#A5A5A5` | Behind the window tabs |
| Toolbar | `#3C3C3C` | `#CBCBCB` | The active tab, toolbars, column headers |
| Header | `#3E3E3E` | `#D5D5D5` | Section headers and the asset header |
| Line | `#232323` | `#999999` | Header tops, outlines, column edges |
| Inner line | `#2E2E2E` | `#B5B5B5` | Rules inside a table or a list |
| Field | `#2A2A2A` | `#F0F0F0` | Text, number, object and tag fields |
| Field border | `#212121` | `#A5A5A5` | |
| Button | `#585858` | `#E4E4E4` | Buttons and dropdowns |
| Button border | `#303030` | `#A0A0A0` | |
| List header | `#353535` | `#CDCDCD` | List headers and footers, stage rows |
| List body | `#414141` | `#DEDEDE` | Inside a reorderable list |
| Alternate row | `#3B3B3B` | `#CDCDCD` | Every other row of a long list |
| Help box | `#414141` | `#DCDCDC` | Info and warning boxes, bordered `#2B2B2B` and `#ABABAB` |
| Selection | `#2C5D87` | `#3A72B0` | The selected row, with white text |
| Selection, unfocused | `#4D4D4D` | `#AEAEAE` | A selected row that doesn't have the keyboard, as in a detail's table beside a list that keeps the blue |
| Toggle on | `#46607C` | `#9DB7D8` | A pressed toolbar toggle |
| Text | `#C4C4C4` | `#1F1F1F` | Labels |
| Strong text | `#D2D2D2` | `#090909` | Values, titles and names |
| Secondary text | `#B0B0B0` | `#4A4A4A` | Units, summaries, a tag's parents, "None" |
| Button text | `#EEEEEE` | `#090909` | |
| Link | `#81B4FF` | `#0A4FA8` | |

The only other colors:

- **Asset types** color their type icon and nothing else (Q2, Q11). No row, border or background takes a type's
  color.

  | Type | Dark | Light |
  |---|---|---|
  | Ability | `#80ADE1` | `#1757A0` |
  | Effect | `#A979E0` | `#4F277E` |
  | Entity | `#D26BAC` | `#B61B7D` |
  | Item | `#4AD077` | `#1D6033` |
  | Attribute | `#A8E8E6` | `#1F5F5D` |
  | Attribute Set | `#88C8D8` | `#13687E` |

- **Warning.** The triangle is `#DBB468` in Dark and `#D9A21B` in Light. Warning text is `#DBB468` in Dark and
  `#6B4C10` in Light: the palette's `#83601B` is too faint for 12 px text on Light's neutrals.
- **Destructive.** `#EA8A85` in Dark and `#902E28` in Light, on a destructive action's icon and nowhere else.

## Type and spacing

- Inter, 12 px, on an 18 px line: Unity 6's editor font. An asset's name in its header is 13 px semibold, and a
  section title is 12 px semibold. No text is smaller than 12 px.
- Field rows are 18 px, 2 px apart. Section headers are 22 px, list rows 20 px and toolbars 22 px.
- An inspector has one label column, 148 px at the mockups' width; in code it follows `EditorGUIUtility.labelWidth`.
  A section's content indents 18 px, past the foldout arrow.
- Fields, buttons and outlines take Unity's 3 px radius, and checkboxes 2 px. Nothing else is rounded.

## Text

- **Values, not sentences.** A cost reads "Mana 20/25/30/35", not "Takes 20 Mana at level 1, 35 at level 4".
- **Short, objective and exactly to the point.** A line of text states a fact the fields don't already show. It
  never explains the UI, narrates, or recommends.
- **Compact ranges and series:** "Levels 1–4", "1–12", "20/25/30/35". A unit follows a field in full, as in
  "6 seconds", and is short in a summary or a table, as in "6 s".
- **A message is one sentence:** the problem and what it causes, as in "Nothing grants Status.Wet, so this removal
  never happens."

## Inspectors

- **The asset header** holds the 32 px type icon, the asset's name, its type and asset tag, and its problem count.
  Then come Open in The Forge (Show in Project, inside The Forge, with the asset's file name under it), Help and the
  asset menu. It stays at the top of
  the inspector while the content scrolls under it (Decision 29, the user's call after seeing it scroll, which
  replaces Q17's answer).
- **Sections are Unity foldout headers**: the arrow and the bold title, then Import, Clear and Help, always visible
  (Q18). A collapsed header shows a one-line summary of its section, such as "Levels 1–5" or "Removed by
  Status.Wet".
- **Every asset type keeps one section order** (Q9): Identity, Leveling, Tags, the type's own sections, then
  Requirements, Workers and Local Data. An Effect's own sections are Duration, Impact and Cues (FEAT-1), and an
  Ability's are Activation and Stages.
- **Fields** are Unity's text and number fields, with any unit after the field in secondary text, as in
  "6 seconds". Choices use Unity's popup. A field that depends on another indents 14 px under it, as Tick Every does
  under Periodic Ticks.
- **Object fields** are Unity's, showing the type icon. PlayForge's actions on one, New and Open, are plain buttons
  beside it (Q15). Template import, the link filter and create-linked go in its right-click menu (Q18).
- **Tag fields** show a tag's parents in secondary text and its leaf in strong text, unless the full-path setting
  is on. A tag reads by its display name everywhere: a unique tag's name is padded with a generated suffix.
- **Lists** are Unity's reorderable lists: a header with the count, drag handles, and the + and − footer.
- **Requirements** are one table: rows for applying, while on and removal, and columns for "Requires" and "Must not
  have". A toggle switches it between the target and the source. Each cell is a button that opens the tag picker,
  and an empty one reads "None".
- **Impact entries** fold, with their delivery named on the right of the row (Q19). Open, an entry says when it
  commits and how often.
- **Messages** are Unity help boxes in the flow, under the field they are about. A problem also counts in the asset
  header, shows as a glyph in The Forge's list, and counts in The Forge's toolbar and status bar (Q8).
- **Destructive actions** say what they do, as in "Move to Trash", and send the asset to the trash (Q16). Only their
  icon takes the destructive color.
- **Clear** empties its section at once, with no confirmation, and one Undo takes it back (Decision 28).
- **Import** is one window for sections, scalers, requirements and workers: choose a source, then the fields that
  differ, each pre-checked, then import as one Undo step. A list is replaced unless Add to the List is chosen
  (Decision 28).
- **Scalers** show their per-level table with a read-only curve preview beside it, wherever a scaler appears: in a
  field, in a Scaler asset and in the Import window (Decision 28). A scaler's settings sit on one block under its
  field, indented, on a light shade with a hairline under it, so they read as the scaler's among the fields around
  them. It's the style's one shaded container, which the user chose for the scaler alone (Decision 36). A kind shows
  only what it reads. Its Values are Per Level or Spread Over Levels, side by side, and Per Level follows the asset's
  level provider's levels unless the scaler takes its own.

## The Forge

- A list-and-detail view that embeds the inspector (Q5).
- **The toolbar** holds Create, the type filters (each with its type icon), the search field, the problem count and
  the window menu. The window's shortcut opens it with the search field focused (Q29). There is no Refresh button,
  because the view follows the project as it changes (Q30).
- **The list** has sortable columns for the name (with its type icon), the type, the asset tag and the levels, and a
  last column for the problem glyph. Rows alternate between the panel and alternate-row colors, and the selected row
  takes Unity's selection blue. A row shows the asset's name alone; its file name is in its header.
- **The tag view** lists the project's tags, with sortable columns for the tag, what it's for and its uses.
- **The detail pane** shows the selected asset's inspector, opening on a Summary of what it does and what uses it
  (Q6).
- **The status bar** holds the asset count and the problem count.

## The Debugger

- One window with four views, Entities, Processes, Tags and Timeline, switched in its toolbar (Q4, FEAT-12).
- **It reads as clearly in a docked column as in a full window** (the user, 2026-11-04). The mockups draw it at
  1280 px and at 360 px.
  - Below 720 px, the list sits above the detail, with a splitter between them, and the toolbar takes two rows: the
    views across the first, the search and filters on the second. The frame count moves to the status bar.
  - The detail's sections stand in one column, as an inspector's do, and sit two abreast only when the detail is at
    least 720 px wide.
  - A table short of room hides its lowest-priority columns before any column clips to nothing, and the detail shows
    what the list hides. The Processes list keeps ID, Process, State and Step; Held By drops Source.
  - Header buttons that don't fit move to a row under the header. A section's foot row keeps its actions, and its
    picker shrinks before a button does.
- **Each section's foot row** acts on the section's selected row, which takes the unfocused selection while the list
  keeps the blue. Attributes edits Base and Current; Effects removes or applies; Abilities activates, interrupts or
  grants; Tags adds a tag or removes a loose one. An action that doesn't apply stays in place, disabled (Q27).

## Columns and overflow

- A column clips its own text with an ellipsis. Nothing draws across a column's edge.
- The columns on the right keep their width and always show in full. When a row is short of room, the widest
  flexible column gives way first (the asset tag, in The Forge), and then the name.
- In a row laid out as a line, the text on the left can shrink to nothing and ends in an ellipsis, and the values on
  the right never shrink.
- An inspector's labels truncate the same way, so a label never covers its field.

## Icons

- Icons are inline vector glyphs on Unity's 16 px grid, in the text color. Never emoji.
- Actions use Unity's built-in icon where Unity has one (Q12).
- The six type icons are placeholders until the commissioned set exists, and the user plans to revisit them. They
  differ in shape as well as color.

## In USS

- Every value here becomes a variable in one style sheet, with a Dark and a Light set, which the kit of C# controls
  reads (Q13).
- The mockups' inline styles belong to the mockups. A control takes its look from the style sheet, never from styles
  set in C#.
