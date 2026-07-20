# PlayForge — Asset Store Publishing Checklist

Status: **NOT YET SUBMITTED**. Code-side readiness is done (asmdefs, no editor leaks in
Runtime, log gating, docs). Remaining work is marked ☐; completed groundwork ☑.

## ☐ 1. Samples (the gating item)

- ☐ Build a polished, self-contained demo scene under `Samples/` (Built-in RP, no
  external packages beyond UniTask). Current `Samples/` content is dev-grade.
- ☐ Verify sample scenes open with zero missing scripts/references in a clean import.
- ☐ Check whether `Samples/InputHandling` needs `com.unity.inputsystem`; if yes, either
  make it optional (`#if ENABLE_INPUT_SYSTEM`) or document the dependency.

## ☐ 2. Embed UniTask (do this at packaging time, not before)

UniTask is currently a UPM git dependency — a `.unitypackage` will NOT include it, and
buyers would hit instant compile errors. At packaging time:

1. Copy the UniTask source from
   `https://github.com/Cysharp/UniTask` (path `src/UniTask/Assets/Plugins/UniTask`)
   into `Assets/PlayForge/ThirdParty/UniTask/`.
2. Remove `"com.cysharp.unitask"` from `Packages/manifest.json`.
3. No asmdef change needed — the embedded copy's asmdef is also named `UniTask`.
4. Verify compile + golden tests.
5. Document for buyers: *"If you already use UniTask via UPM, delete
   `PlayForge/ThirdParty/UniTask` after import."*

Keep the UPM install for day-to-day development (updates, cleanliness); embed only on
the packaging branch.

## ☑/☐ 3. Third-party notices

Third-party content: **UniTask (MIT, © Cysharp, Inc.)** — the only remaining dependency
(SerializedCollections was removed 2026-07-16). When UniTask is embedded:

- ☐ Add `Assets/PlayForge/Third-Party Notices.txt`:

```
Third-Party Notices
This package contains third-party software components governed by the license(s) below.

Component: UniTask (https://github.com/Cysharp/UniTask)
License: MIT
Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.

<insert full MIT license text from the UniTask repository LICENSE file>
```

## ☑ 4. Code readiness (done — keep true)

- ☑ asmdefs: `FarEmerald.PlayForge` (runtime), `.Editor`, `.Tests` (both editor-only).
- ☑ Zero unguarded `UnityEditor` references in Runtime; editor tools live in `Editor/`.
- ☑ Runtime log noise gated: `ForgeLog` + Forge → Settings → Logging. Intentional debug
  surfaces (ProcessControl inspector toggles, per-sequence logging flags, TagCache dump
  methods, example files) left as authored.
- ☑ Golden tests (`PlayForge → Run Golden Tests`) green.
- ☐ Re-verify both before each submission.

## ☐ 5. Dress rehearsal (mandatory before submitting)

1. Export `Assets/PlayForge` (+ nothing else) as a `.unitypackage`.
2. Import into a **brand-new** Unity 6 project.
3. Zero console errors AND warnings on import; no import-time log spam.
4. Run golden tests; open The Forge, GAS Debugger, a Visualizer.
5. Open + play the sample scene.
6. Make a **Windows standalone build** (and IL2CPP if claiming support) — must succeed.

## ☐ 6. Submission mechanics

- ☐ Publisher account; support email/website.
- ☐ Key images: icon 160×160, card 420×280, cover 1950×1300; ≥5 screenshots (The Forge
  tabs, GAS Debugger, Visualizer are the strongest material); optional video.
- ☐ Description; tags; state "Built-in RP samples; runtime is render-pipeline-agnostic".
- ☐ Upload from the OLDEST supported Unity version (claim Unity 6.0+ → upload from 6000.0.x).
- ☐ `CHANGELOG.md` + version `1.0.0`.
- ☐ Decide whether `Tests/` and `Documentation/IMPACT_MODEL_REFACTOR.md` (internal design
  history) ship; recommendation: ship Tests (credibility), drop the refactor doc from
  the export.
