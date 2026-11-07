# Demo Builder

An editor tool that builds the Demo sample's assets: its attributes, attribute sets, effects, abilities, items,
entities, prefabs, roster, UI settings and scene, each made the way its inspector fills it in. It isn't part of the
package. It's for whoever changes the demo's content: change the builder, run it, and copy the result back.

It updates each asset in place where one is already there, so a rebuild keeps every GUID, and the scene's and the
assets' references to each other hold.

## Running it

1. In a Unity project that has PlayForge (Proving Grounds, say), copy `Assets/PlayForge/Samples~/Demo` to
   `Assets/PlayForgeDemo`. The builder writes there.
2. Copy this folder's `.cs` files to an `Editor` folder of their own, with an assembly definition that references
   `FarEmerald.PlayForge`, `FarEmerald.PlayForge.Editor`, `FarEmerald.PlayForge.Samples.Demo` and
   `FarEmerald.PlayForge.Samples.Demo.Editor`, for the Editor platform only.
3. Run **Window → PlayForge → Demo → Rebuild the Demo (Authors)**, or in batch mode:
   `-executeMethod FarEmerald.PlayForge.Samples.Demo.Building.DemoBuilder.Build`.
4. Copy `Assets/PlayForgeDemo` back over `Samples~/Demo`, metas and all, and delete the builder's copy.
5. Run the Demo's PlayMode tests (`FarEmerald.PlayForge.Samples.Demo.Tests`), and Proving Grounds' editor smoke suite,
   which opens every Demo asset in its inspector.

## Where things are

| File | What it builds |
|---|---|
| `DemoBuilder.cs` | The entry point, saving in place, tags, and the scaler and damage helpers. |
| `DemoBuilder.Basics.cs` | Attributes, the Attack Growth scaler, the cues' looks, the effect templates, the attribute sets and the shared effects. |
| `DemoBuilder.Abilities.cs` | The five heroes' abilities, their effects, costs and cooldowns, and the passive. |
| `DemoBuilder.Items.cs` | The fourteen items, from the Stat Item template where they give stats. |
| `DemoBuilder.Scene.cs` | The heroes and the dummy, the roster, the UI settings and the scene. |

The icons come last, from the sample's own **Make Placeholder Icons**.
