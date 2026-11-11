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
abilities and items. A **Forge Project** is the catalog of its own assets, those in its folder.

1. Use your Forge Project, or create one with **Create → PlayForge → Forge Project**.
2. In the project's inspector, open the menu in its header and choose **Add Its Assets**. It names each PlayForge asset
   in the project's folder by its GUID, which stays the same when the asset is renamed or moved; **Add Every PlayForge
   Asset** names every one in the Unity project. Run it again after you add assets.
3. Pass the project to every capture and restore.

Code can add entries while the game runs, with `catalog.Add(id, asset)`. An ID or an asset can be listed only once.

A capture leaves out an asset the catalog doesn't name, and logs a warning. A restore does the same for an ID the
catalog can't find.

## What a save holds

- Each attribute's permanent commits, as a total per retention group. Modifiers aren't saved: they come back with the
  effects that carry them.
- The levels the entity keeps: its own, and its abilities' and items'. Its own is saved as the entity's own, so a save
  of one kind loads onto another kind as that one's level.
- The abilities it knows of its own, in their slots, at their levels.
- Its items in order, each with its level and stacks, worn or not.
- Its effects, each with what applied it, its stacks, its time left and the time to its next tick, and the magnitudes
  its caller set. Stacks with durations of their own keep their own times. The times are kept in full, as the effect's
  timers keep them, so a restored effect keeps time with one that never was, to the last digit.
- Its loose tags and its time scale.

An ability that an effect or a worn item lends isn't saved on its own. It comes back with the effect or the item.

A save leaves out:

- the entity's Local Data;
- its sides, which come from its asset and its registration;
- what an effect captured as it landed, such as a source's attribute or a roll made once. The effect captures it
  again as it comes back.

## What a load does

1. Running abilities are interrupted, and the input buffer empties.
2. The entity lets go of its effects, items and abilities, and the actions queued this frame are dropped. An ability
   it holds in the slot the save gives it stays as it is, so another entity's effect that it applied still names it.
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

Without IDs, or when the other entity is gone, the effect comes back as the entity's own. An entity whose abilities are
in their saved slots keeps them through its load, so several entities can load in any order and their effects on each
other still name the abilities that applied them. Items aren't kept that way: when several entities load together,
restore those whose items applied effects first, so the others find them.

## Rolling back

A rollback saves each step, and on a late input loads a step back and runs again. With a fixed tick (ProcessControl's
Fixed Tick Rate, or a world of its own stepped by a fixed time) and the random source seeded for each step
(`ForgeRandom.Use`), the run again lands where the first did, to the last digit of every save. Keep the saves in
memory, as `ForgeSnapshot`s: `JsonUtility` can move the last digit of a double as it reads one back. A load ends the
abilities running, so an ability that runs over several steps isn't replayed by a load into its middle; one that
ends as it activates is.

## Copying an entity

`ForgeSave.Copy(from, onto)` copies what lasts on one entity onto another, as a mirror clone takes its original's
state. It's a capture and a restore with no catalog and no IDs, since both entities are in the same session, so it
copies assets made as the game runs too. An effect another entity applied stays that entity's, and the entity's own
level goes onto the other's own.

```csharp
var clone = new RuntimeGameplayAbilitySystem(identity);
ProcessControl.Register(clone).Run();
ForgeSave.Copy(original, clone);
```

## Versions

`ForgeSnapshot.Version` names the save's format, and `ForgeSave.Version` the format this PlayForge writes. A restore
refuses a save with a newer format.
