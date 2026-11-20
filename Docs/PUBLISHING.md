# PlayForge — Asset Store Publishing Checklist

Status: **NOT YET SUBMITTED**. PlayForge ships under the Unity Asset Store EULA. Remaining work is marked ☐;
completed groundwork ☑.

## ☑/☐ 1. Samples

- ☑ The samples live in `Samples~` and are listed in `package.json`, so Package Manager imports them on request:
  **Battle**, a battle that plays itself; **Demo**, a hero arena of five heroes, fourteen items, an AI hero and a
  training dummy; **Survivors**, a horde shooter of hundreds of enemies at once, eight abilities that cast themselves,
  a dash and fourteen passives (FEAT-40, FEAT-41); **Additional Content**, recipes for common rules made of
  PlayForge's own assets, with hit feedback and a sparring scene; **Task Sequence Examples**; and **Process Group
  Examples**.
- ☑ A clean import into a fresh project shows zero warnings and zero missing types, and importing the samples into
  it shows zero missing scripts, references and types. Proving Grounds' `Tools/clean-import.py` checks both, the
  samples with `--samples`. The samples ship no tests (EP-24): theirs live in Proving Grounds'
  `Assets/ProvingGrounds/SampleTests~`, which its gates and CI copy in beside each sample they import. Until then
  they compiled only where the project has the Test Framework (SP-29, found by this check on 2026-11-14).
- ☑ **Demo** and **Survivors** need nothing beyond PlayForge: they read the Input System where the project has it, and
  the Input Manager otherwise, so they compile and play in any project (Phase 7, FEAT-40).
- ☐ A polished, self-contained demo scene. The Demo (Phase 7, Decision 37) is self-contained, renders in the
  Built-in pipeline and URP, and its PlayMode tests play every hero's abilities, but nobody has played it by hand
  yet. Survivors draws as the Demo does, with primitives in the pipeline's default material and unlit lines, and its
  PlayMode tests play each ability, each enemy kind, the cards and 400 enemies at once; nobody has played it by hand
  yet, nor in a URP project.

## ☑ 2. The export: embed UniTask, include the samples

A `.unitypackage` exports `Assets/Plugins/PlayForge` and nothing else, which a buyer's import puts in the same place,
so the Forge Projects PlayForge makes in `Assets/PlayForge` are never in its folder (EP-12, Decision 89). A package
under Assets resolves no dependency and imports no folder whose name ends in `~`. Proving Grounds' `Tools/export-unitypackage.py` builds the package as a buyer
gets it, in a throwaway project, so PlayForge's own repository never changes and Decision 19 holds (Decision 87):

1. UniTask 2.5.11's source, from GitHub's archive of its tag (`src/UniTask/Assets/Plugins/UniTask`), goes in
   `PlayForge/ThirdParty/UniTask/`, and its licence in `PlayForge/Third-Party Notices.txt`. The embedded copy's
   assembly is also named `UniTask`, so no assembly definition changes.
2. Each sample is copied out of `Samples~` into `PlayForge/Samples/`.
3. The internal docs live in the repository's `Docs/`, this checklist among them, outside the package, so none ships;
   `Tests/` and the test kit ship.
4. The throwaway project compiles it, with `com.unity.ugui` and `com.unity.profiling.core`, and exports
   `PlayForge-<version>.unitypackage`. A fresh project imports that, and the gate is the clean import's: no compile
   error or warning, no missing type, and no missing script, reference or type in the samples. `--tests` runs the
   golden tests there, and `--build` builds a Windows player from it.

The throwaway project stays, at `%TEMP%\claude\export\build` unless `--out` says otherwise, for the Asset Store Tools to
upload `Assets/Plugins/PlayForge` from. Under **Set your dependencies**, turn on **Include Package Manifest** and pick
`com.unity.ugui` and `com.unity.profiling.core`, which `package.json` names for a UPM install, so a buyer's import
installs them. The notice tells buyers to delete `PlayForge/ThirdParty/UniTask` where UniTask already comes from Package
Manager.

The UPM install, UniTask pinned to `#2.5.11` in the manifest, stays the one for day-to-day development.

## ☑ 3. Third-party notices

Third-party content: **UniTask (MIT, © Cysharp, Inc.)**, which the export embeds. Its `Third-Party Notices.txt` names
it, where it is, and carries its licence word for word from the archive's `LICENSE`. QuickOutline, whose licence
reserved all rights, is gone; the Demo has an outline of its own.

## ☑ 4. Code readiness (keep it true)

- ☑ Assembly definitions: `FarEmerald.PlayForge` and `FarEmerald.PlayForge.Lab` (runtime); `.Editor`, `.Lab.Editor`,
  `.Tests` and `.Tests.Golden` (editor-only); `FarEmerald.PlayForge.Testing`, the test kit. The samples have
  `FarEmerald.PlayForge.Samples.Battle`, `.Samples.Demo` (and `.Samples.Demo.Editor`), `.Samples.Survivors`,
  `.Samples.TaskSequences`, `.Samples.ProcessGroups` and `FarEmerald.PlayForge.AdditionalContent`.
- ☑ Zero unguarded `UnityEditor` references in Runtime; editor tools live in `Editor/`.
- ☑ Runtime log noise gated: `ForgeLog`, set in Preferences → PlayForge → Logging.
- ☑ Golden tests (the Test Runner's EditMode tab, `FarEmerald.PlayForge.Tests.Golden`): 25 scenarios, all green.
- ☐ Re-verify all of this before each submission.

## ☐ 5. Dress rehearsal (mandatory before submitting)

`python Tools/export-unitypackage.py --tests --build`, in Proving Grounds, does the first four. After it,
`python Tools/smoke-check.py` does 5 and 6 as far as a machine can, in the check project, the package where a buyer's
import puts it (SP-39). It opens the windows from their menus and plays each sample scene for 10 seconds, failing on
anything logged. The rest is by hand. The four passed again on 2026-11-19 on PlayForge a3e5715e, after FEAT-41 and
SP-40: a 2.6 MB package, no warning on import, 0 missing in the samples, the golden tests 25 of 25, and the player built
with 0 errors and 0 warnings. The smoke passed there too: the three windows opened and the four sample scenes played
with no error. Additional Content warned, as it should, that the fresh project has no TextMeshPro Essential Resources.

1. ☑ Export `Assets/Plugins/PlayForge` (and nothing else) as a `.unitypackage`.
2. ☑ Import it into a **brand-new** Unity 6 project.
3. ☑ Zero console errors and warnings on import, and no import-time log spam; no missing script, reference or type in
   the samples. `Tools/clean-import.py` checks the UPM import the same way.
4. ☑ Run the golden tests, and make a **Windows standalone build** (an IL2CPP one too, if claiming support): with no
   scene enabled in the build settings, `BuildVerification` builds an empty scene it makes for the purpose.
5. ☐ In the check project, `%TEMP%\claude\export\check`, open The Forge and the Debugger. `smoke-check.py` opens them
   and Problems from `Window/PlayForge`; a look at them by hand remains.
6. ☐ Open and play each sample scene. `smoke-check.py` plays each for 10 seconds; playing them by hand remains, the
   Demo and Survivors above all.

## ☐ 6. Submission mechanics

- ☐ Publisher account; support email/website.
- ☑ Online documentation at https://gabedvdsn.github.io/PlayForge-Docs/ (EP-68, EP-69): the guides and the API
  reference. `bash docfx/publish.sh` builds it from HEAD and publishes it again, after a commit that
  changes a guide or the public API. The listing's description links to it.
- ☐ Key images: icon 160×160, card 420×280, cover 1950×1300; ≥5 screenshots (The Forge
  and the Debugger are the strongest material); optional video. The screenshots to upload, in order, are
  `Docs/Store/gallery/` (EP-81). The trailer to voice over is `Docs/Store/video/`, out of git, with its script in
  `Docs/Store/VIDEO_SCRIPT.md`.
- ☐ Description; tags; state "samples render in the Built-in RP and URP; the runtime is render-pipeline-agnostic". A
  draft of the name, summary, description, technical details and keywords is in `Docs/Store/LISTING.md` (SP-39), for
  the user's word.
- ☐ Upload from the OLDEST supported Unity version (claim Unity 6.0+ → upload from 6000.0.x).
- ☑ Which Editor submits: 6000.0, the oldest supported (the plan's decision 17).
- ☑ The samples render in URP (SP-23), and the package works with domain reloading off (SP-22). The
  [submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines) (updated 2026-10-23) require
  them of anything submitted from 6.5 (rule 1.3.c) and 6.6 (rule 2.5.h). Decision 17 does both anyway.
- ☑ Menus under an existing menu (rule 2.5.1.a): every item sits under `Window/PlayForge/` (ED-19).
- ☑ Version `1.0.0` in `package.json`, with its entry in `CHANGELOG.md`, a buyer's: a short one, its date set as
  it's submitted. The detailed history, change by change, is the repository's `Docs/HISTORY.md` (Decision 87).
- ☑ `Tests/` and the test kit ship; `Documentation/IMPACT_MODEL_REFACTOR.md`, internal design history, and this
  checklist don't (Decision 87).
