# PlayForge — Asset Store Publishing Checklist

Status: **NOT YET SUBMITTED**. PlayForge ships under the Unity Asset Store EULA. Remaining work is marked ☐;
completed groundwork ☑.

## ☑/☐ 1. Samples

- ☑ The samples live in `Samples~` and are listed in `package.json`, so Package Manager imports them on request:
  **Demos** (the Demo scene, Swarm Defender and the data they share, imported together) and **Code Examples**.
- ☑ A clean import into a fresh project shows zero warnings and zero missing types, and importing both samples into
  it shows zero missing scripts, references and types. Proving Grounds' `Tools/clean-import.py` checks both, the
  samples with `--samples`.
- ☑ **Demos** needs the Input System package. Its assembly compiles only where that package is installed, so importing
  it without the package leaves it inert instead of breaking the project's compile.
- ☐ A polished, self-contained demo scene (Built-in RP, no external packages beyond UniTask). The Demo imports and
  checks clean, but it is dev-grade, and nobody has played it since its repairs.

## ☐ 2. The packaging branch: embed UniTask, include the samples

A `.unitypackage` exports `Assets/PlayForge` and nothing else. On the packaging branch, and not before:

1. Copy UniTask 2.5.11's source from `https://github.com/Cysharp/UniTask` (path
   `src/UniTask/Assets/Plugins/UniTask`, tag `2.5.11`) into `Assets/PlayForge/ThirdParty/UniTask/`, and remove
   `com.cysharp.unitask` from `Packages/manifest.json`. The embedded copy's assembly is also named `UniTask`, so no
   assembly definition changes.
2. Unity skips folders whose names end in `~`, so an export leaves `Samples~` out. Copy each sample from `Samples~`
   into `Assets/PlayForge/Samples/` for the export.
3. Verify the compile and the golden tests.
4. Tell buyers: *"If you already use UniTask via UPM, delete `PlayForge/ThirdParty/UniTask` after import."*

Keep the UPM install, pinned to `#2.5.11`, for day-to-day development.

## ☐ 3. Third-party notices

Third-party content: **UniTask (MIT, © Cysharp, Inc.)**, once it's embedded. QuickOutline, whose licence reserved all
rights, is gone; the Demo has an outline of its own.

- ☐ Add `Assets/PlayForge/Third-Party Notices.txt` when UniTask is embedded:

```
Third-Party Notices
This package contains third-party software components governed by the license(s) below.

Component: UniTask (https://github.com/Cysharp/UniTask)
License: MIT
Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.

<insert full MIT license text from the UniTask repository LICENSE file>
```

## ☑ 4. Code readiness (keep it true)

- ☑ Assembly definitions: `FarEmerald.PlayForge` (runtime), `.Editor` and `.Tests` (both editor-only). The samples
  have `FarEmerald.PlayForge.Samples` and `FarEmerald.PlayForge.Examples`.
- ☑ Zero unguarded `UnityEditor` references in Runtime; editor tools live in `Editor/`.
- ☑ Runtime log noise gated: `ForgeLog`, set in The Forge → Settings → Logging.
- ☑ Golden tests (`PlayForge → Run Golden Tests`): 25 scenarios, all green.
- ☐ Re-verify all of this before each submission.

## ☐ 5. Dress rehearsal (mandatory before submitting)

1. On the packaging branch, export `Assets/PlayForge` (and nothing else) as a `.unitypackage`.
2. Import it into a **brand-new** Unity 6 project.
3. Zero console errors and warnings on import, and no import-time log spam. `Tools/clean-import.py` checks the UPM
   import the same way.
4. Run the golden tests, and open The Forge, the GAS Debugger, the Tag Debugger and Process Control.
5. Open and play each sample scene.
6. Make a **Windows standalone build** (and an IL2CPP one if claiming support); it must succeed. Headless:
   `Unity -batchmode -nographics -projectPath <proj> -executeMethod FarEmerald.PlayForge.Extended.Editor.BuildVerification.BuildWindowsPlayer`.
   With no scene enabled in the build settings, it builds an empty scene it makes for the purpose.

## ☐ 6. Submission mechanics

- ☐ Publisher account; support email/website.
- ☐ Key images: icon 160×160, card 420×280, cover 1950×1300; ≥5 screenshots (The Forge
  tabs, GAS Debugger, Visualizer are the strongest material); optional video.
- ☐ Description; tags; state "Built-in RP samples; runtime is render-pipeline-agnostic".
- ☐ Upload from the OLDEST supported Unity version (claim Unity 6.0+ → upload from 6000.0.x).
- ☐ Settle which Editor submits (the plan's open decision 17). The [submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines)
  (updated 2026-10-23) require URP or HDRP support of anything submitted from 6.5 or newer (rule 1.3.c), and the
  Demos are Built-in RP (SP-23). From 6.6, every package must also work with domain reloading off (rule 2.5.h, SP-22).
- ☐ Menus under an existing menu such as `Window/`, or else `Tools/` (rule 2.5.1.a). Every item sits under a
  top-level `PlayForge/` menu today (ED-19).
- ☐ Version `1.0.0` in `package.json`, with its entry in `CHANGELOG.md`.
- ☐ Decide whether `Tests/` and `Documentation/IMPACT_MODEL_REFACTOR.md` (internal design
  history) ship; recommendation: ship Tests (credibility), drop the refactor doc from
  the export.
