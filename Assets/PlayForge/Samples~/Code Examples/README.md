# PlayForge Code Examples

Annotated examples of PlayForge's process and task sequence APIs, written to be read. They compile into their own
assembly, `FarEmerald.PlayForge.Examples`, and need nothing beyond PlayForge.

| File | What it shows |
| --- | --- |
| `TaskSequenceExamples.cs` | Task sequence features one at a time: stages, tasks, delays, conditions and data packets. Each method marked `[TaskSequenceMethod]` appears in an ability's sequence dropdown, so you can run it from an ability. |
| `TaskSequenceExamples_Mechanics.cs` | The same features applied to gameplay mechanics in the style of MOBA and ARPG games. |
| `GroupProcessExamples.cs` | Processes run as a group, with the Synchronous lifecycle. Its summary lists the scene setup it needs. |

The synced sequence examples ship in the **Demos** sample instead, because the Demo uses them.
