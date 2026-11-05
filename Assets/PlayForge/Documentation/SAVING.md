# PlayForge — Saving and Loading

`ForgeSave` saves an entity at rest and loads it back (FEAT-9). `ForgeSave.Capture` takes what lasts on an entity
into a `ForgeSnapshot`. `ForgeSave.Restore` puts it back, on the entity it came from or on a fresh one of the same
kind. A snapshot is plain serializable types, so a game writes it where it likes, through `JsonUtility` or its own
serializer.

```csharp
// Save
ForgeSnapshot save = ForgeSave.Capture(gas, catalog);
string json = JsonUtility.ToJson(save);

// Load
ForgeSnapshot loaded = JsonUtility.FromJson<ForgeSnapshot>(json);
ForgeSave.Restore(gas, loaded, catalog);
```

Only a hub built from an entity can be saved or loaded: a `GameplayAbilitySystem` with its Entity asset, or a
`RuntimeGameplayAbilitySystem` made from an identity.

## The Asset Catalog

A save can't hold references to assets, because a reference doesn't last from one session to the next. It names each
asset by an ID instead, and an **Asset Catalog** finds the asset by that ID. The catalog lists attributes, effects,
abilities and items.

1. Create one with **Create → PlayForge → Asset Catalog**.
2. In the catalog's inspector, open the menu in its header and choose **Add Every PlayForge Asset**. It names each
   PlayForge asset in the project by its GUID, which stays the same when the asset is renamed or moved. Run it again
   after you add assets.
3. Pass the catalog to every capture and restore.

Code can add entries while the game runs, with `catalog.Add(id, asset)`. An ID or an asset can be listed only once.

A capture leaves out an asset the catalog doesn't name, and logs a warning. A restore does the same for an ID the
catalog can't find.

## What a save holds

- Each attribute's permanent commits, as a total per retention group. Modifiers aren't saved: they come back with the
  effects that carry them.
- The levels the entity keeps: its own, and its abilities' and items'.
- The abilities it knows of its own, in their slots, at their levels.
- Its items in order, each with its level and stacks, worn or not.
- Its effects, each with what applied it, its stacks, its time left and the time to its next tick. Stacks with
  durations of their own keep their own times.
- Its loose tags and its time scale.

An ability that an effect or a worn item lends isn't saved on its own. It comes back with the effect or the item.

A save leaves out:

- the entity's Local Data;
- its sides, which come from its asset and its registration;
- what an effect captured as it landed, such as a source's attribute or a roll made once. The effect captures it
  again as it comes back.

## What a load does

1. Running abilities are interrupted, and the input buffer empties.
2. The entity lets go of its effects, items and abilities, and the actions queued this frame are dropped.
3. The save's abilities, items, effects, levels and commits come back.
4. The tags compile once, and `OnRestored` fires.

A load tells nothing as it goes:

- No effect is told as applied or removed, and no attribute as changed.
- Nothing of an effect's application runs again: not its workers, its commits on application, or its contained
  effects.
- A worn item's effects come back from the save, so wearing it again applies none.

The one tag compile tells what the load changed, as any compile does. Then
`gas.GetCallbacks().OnRestored` fires once. Listen to it to refresh what shows the entity, such as a health bar or a
hotbar.

The entity's attribute set has to define the attributes the save names. An attribute the catalog names but the save
doesn't list loses its commits, since the entity had none when it was saved.

Load between the entity's steps. A restore called from inside the entity's step or its end of frame, as from one of its
callbacks, is refused with a warning, because it would empty the lists the step is going through. Load on the next
frame instead.

## Effects from other entities

An effect knows what applied it: an entity, or one of its abilities or items. When that is another entity, the save
needs a name for it that lasts. Give `Capture` and `Restore` an `IForgeEntityIds`, which maps the game's own IDs to its
entities:

```csharp
class Roster : IForgeEntityIds
{
    public string IdOf(IGameplayAbilitySystem entity) => /* the game's ID for it, or null */;
    public IGameplayAbilitySystem Find(string id) => /* the entity, or null if it's gone */;
}

var save = ForgeSave.Capture(hero, catalog, roster);
ForgeSave.Restore(hero, save, catalog, roster);
```

Without IDs, or when the other entity is gone, the effect comes back as the entity's own. When several entities load
together, restore the ones whose abilities and items applied effects first, so the others find them.

## Versions

`ForgeSnapshot.Version` names the save's format, and `ForgeSave.Version` the format this PlayForge writes. A restore
refuses a save with a newer format.
