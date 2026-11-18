# PlayForge

PlayForge is a gameplay framework for Unity 6, modelled on Unreal's Gameplay Ability System. Every ability, effect,
attribute, tag and item is an asset you build in the inspector, and one hub on each entity runs them all.

## Start here

[Getting Started](guides/getting-started/index.md) sets PlayForge up in a project, makes an entity, and gives it an ability
that changes an attribute through an effect.

## The guides

| Area | Guides |
| --- | --- |
| Gameplay | [Entities](guides/entities/index.md) · [Attributes](guides/attributes/index.md) · [Attribute Sets](guides/attribute-sets/index.md) · [Effects](guides/effects/index.md) · [Effect & Attribute Pipeline](guides/effect-pipeline/index.md) · [Value Pipelines](guides/value-pipelines/index.md) · [Scalers](guides/scalers/index.md) · [Abilities](guides/abilities/index.md) · [Items](guides/items/index.md) · [Tags](guides/tags/index.md) · [Cues](guides/cues/index.md) · [Workers](guides/workers/index.md) |
| Running it | [Processes](guides/processes/index.md) · [Task Sequences](guides/task-sequences/index.md) · [Time, Input and Logging](guides/services/index.md) · [Saving and Loading](guides/saving/index.md) · [Spreadsheets](guides/spreadsheets/index.md) |
| Tools | [The Editor Tools](guides/editor/index.md) · [The Creator Lab](guides/creator-lab/index.md) · [Testing Your Content](guides/testing/index.md) |
| Going further | [Extending PlayForge](guides/extending/index.md): a game's own components, workers, tasks, scalers, cues, rules and tools · [Recipes](guides/recipes/index.md): common rules built from PlayForge's own assets, field by field |

These guides are the ones the package ships in `PlayForge/Documentation`, each section here a page of its own, and each
help button in PlayForge's inspectors opens its section. A guide's **Built-in kinds** page lists the components,
tasks, workers, cues, scalers and the rest that PlayForge ships for its pickers, each with what it does.

## The API reference

[The API reference](api/index.md) has a page for every public type of PlayForge: its runtime, the Creator Lab, its
editor and its test kit, grouped by area. Each type's page links the guide to its area and the guide sections that
name it, and each guide links the types it names to their pages.
