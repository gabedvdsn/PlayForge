# PlayForge — Saving and Loading

`ForgeSave` saves an entity at rest and loads it back.

- `ForgeSave.Capture` takes what lasts on an entity into a `ForgeSnapshot`.
- `ForgeSave.Restore` puts it back, on the entity it came from or on a fresh one of the same kind.

A snapshot is plain serializable types, so a game writes it where it likes, through `JsonUtility` or its own
serializer.

```csharp
// Save
ForgeSnapshot save = ForgeSave.Capture(gas, catalog);
string json = JsonUtility.ToJson(save);

// Load
ForgeSnapshot loaded = JsonUtility.FromJson<ForgeSnapshot>(json);
ForgeSave.Restore(gas, loaded, catalog);
```

Only a hub built from an entity can be saved or loaded:

- a `GameplayAbilitySystem` with its Entity asset;
- a `RuntimeGameplayAbilitySystem` made from an identity.

## The Asset Catalog

An **Asset Catalog** names the assets a save refers to, and finds them again.

A save can't hold references to assets, because a reference doesn't last from one session to the next. Instead, it
names each asset by an ID, and the catalog finds the asset by that ID.

The catalog lists attributes, effects, abilities and items. A **Forge Project** is the catalog of its own assets, those
in its folder.

To give your saves a catalog:

1. Use your Forge Project, or create one with **Create → PlayForge → Forge Project**.
2. Its catalog keeps itself current:
   - Each PlayForge asset made, copied or moved into the project's folder is named as it lands, by its GUID.
   - The GUID stays the same when the asset is renamed or moved.
   - A deleted asset's entry goes.
   - An asset moved out stays named, so a save made before still finds it.
   - The project catches up on assets it doesn't name yet, as it's imported and once each editor session. So a
     project made before PlayForge kept catalogs catches up too.
3. Pass the project to every capture and restore.

> [!NOTE]
> A capture leaves out an asset the catalog doesn't name, and logs a warning. A restore does the same for an ID the
> catalog can't find.

### Adding assets by hand

You can stop the catalog keeping itself current, and name its assets yourself.

**Keep Catalog Current**, on the project, turns the catalog's own upkeep off. Then name the assets from the project's
inspector:

1. Open the menu in the inspector's header.
2. Choose **Add Its Assets**, which names each PlayForge asset in the project's folder by its GUID.

**Add Every PlayForge Asset**, in the same menu, names every one in the Unity project. Run either again after you add
assets.

Code can add entries while the game runs, with `catalog.Add(id, asset)`. An ID or an asset can be listed only once.

## What a save holds

A save holds what lasts on the entity.

| Part | What the save holds |
| --- | --- |
| Attributes | Each attribute's permanent commits, as a total per retention group. Modifiers aren't saved: they come back with the effects that carry them. |
| Levels | The levels the entity keeps: its own, and its abilities' and items'. Its own is saved as the entity's own, so a save of one kind loads onto another kind as that one's level. |
| Abilities | The abilities it knows of its own, in their slots, at their levels. |
| Items | Its items in order, each with its level and stacks, worn or not. |
| Effects | Each effect, with what applied it, its stacks, its time left and the time to its next tick, and the magnitudes its caller set. |
| Tags and time scale | Its loose tags, and the time scale its code set. Its Time Scale attribute's is read again from the attribute. |

An effect's times are saved exactly:

- Stacks with durations of their own keep their own times.
- The times are kept in full, as the effect's timers keep them. So a restored effect keeps time with one that never
  went through a save, to the last digit.

An ability that an effect or a worn item lends isn't saved on its own. It comes back with the effect or the item.

### What a save leaves out

A save leaves out three things:

- the entity's Local Data;
- its sides, which come from its asset and its registration;
- what an effect captured as it landed, such as a source's attribute or a roll made once. The effect captures it again
  as it comes back.

## What a load does

A load puts the save back in four steps.

1. Running abilities are interrupted, and the input buffer empties.
2. The entity lets go of its effects, items and abilities, and the actions queued this frame are dropped.
   - An ability it holds in the slot the save gives it stays as it is. So another entity's effect that it applied still
     names it.
3. The save's abilities, items, effects, levels and commits come back.
4. The tags compile once, and `OnRestored` fires.

A load tells nothing as it goes:

- No effect is told as applied or removed, and no attribute as changed.
- Nothing of an effect's application runs again: not its workers, its commits on application, or its contained
  effects.
- A worn item's effects come back from the save, so wearing it again applies none.

The one tag compile tells what the load changed, as any compile does. Then `gas.GetCallbacks().OnRestored` fires once.
Listen to it to refresh what shows the entity, such as a health bar or a hotbar.

### Before you load

A load needs the right attributes, and a moment between the entity's steps.

- The entity's attribute set has to define the attributes the save names.
- An attribute the catalog names but the save doesn't list loses its commits, since the entity had none when it was
  saved.

Load between the entity's steps. A restore called from inside the entity's step or its end of frame, as from one of its
callbacks, is refused with a warning. It would empty the lists the step is going through, so load on the next frame
instead.

## Effects from other entities

An effect knows what applied it: an entity, or one of its abilities or items. When that is another entity, the save
needs a name for it that lasts.

Give `Capture` and `Restore` an `IForgeEntityIds`, which maps the game's own IDs to its entities:

```csharp
class Roster : IForgeEntityIds
{
    public string IdOf(IGameplayAbilitySystem entity) => /* the game's ID for it, or null */;
    public IGameplayAbilitySystem Find(string id) => /* the entity, or null if it's gone */;
}

var save = ForgeSave.Capture(hero, catalog, roster);
ForgeSave.Restore(hero, save, catalog, roster);
```

- Without IDs, or when the other entity is gone, the effect comes back as the entity's own.
- An entity whose abilities are in their saved slots keeps them through its load. So several entities can load in any
  order, and their effects on each other still name the abilities that applied them.
- Items aren't kept that way. When several entities load together, restore those whose items applied effects first,
  so the others find them.

## Rolling back

A rollback saves each step, and on a late input loads a step back and runs again.

The run again lands where the first did, to the last digit of every save, given both of these:

- a fixed tick: ProcessControl's Fixed Tick Rate, or a world of its own stepped by a fixed time;
- the random source seeded for each step (`ForgeRandom.Use`).

> [!TIP]
> Keep the saves in memory, as `ForgeSnapshot`s. `JsonUtility` can move the last digit of a double as it reads one
> back.

A load ends the abilities running. So an ability that runs over several steps isn't replayed by a load into its
middle, but one that ends as it activates is.

## Copying an entity

`ForgeSave.Copy(from, onto)` copies what lasts on one entity onto another, as a mirror clone takes its original's state.

It's a capture and a restore with no catalog and no IDs, since both entities are in the same session. So it copies
assets made as the game runs too.

- An effect another entity applied stays that entity's.
- The entity's own level goes onto the other's own.

```csharp
var clone = new RuntimeGameplayAbilitySystem(identity);
ProcessControl.Register(clone).Run();
ForgeSave.Copy(original, clone);
```

## Versions

Each save names the format it was written in.

| Member | What it names |
| --- | --- |
| `ForgeSnapshot.Version` | The save's format. |
| `ForgeSave.Version` | The format this PlayForge writes. |

A restore refuses a save with a newer format.
