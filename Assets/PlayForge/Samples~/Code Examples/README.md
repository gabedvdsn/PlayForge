# PlayForge Code Examples

Annotated examples of PlayForge's process and task sequence APIs, written to be read. They compile into their own
assembly, `FarEmerald.PlayForge.Examples`, and need nothing beyond PlayForge.

| File | What it shows |
| --- | --- |
| `TaskSequenceExamples.cs` | Task sequence features one at a time: stages, tasks, delays, conditions and data packets. Each method marked `[TaskSequenceMethod]` appears in an ability's sequence dropdown, so you can run it from an ability. |
| `TaskSequenceExamples_Mechanics.cs` | The same features applied to gameplay mechanics in the style of MOBA and ARPG games. |
| `GroupProcessExamples.cs` | Processes run as a group, with the Synchronous lifecycle. Its summary lists the scene setup it needs. |
| `SyncSequenceExamples.cs` | Synced sequences: processes you drop into a scene that tick, patrol, chase, bob, orbit, pulse and burn, each a `LazyMonoProcess` with the Synchronous lifecycle. Its summary lists the setup. |
