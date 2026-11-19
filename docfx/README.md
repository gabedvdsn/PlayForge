# PlayForge's documentation site

DocFX builds the site from what the package already holds, with no copies. The guides come from the pages the package
ships, `Assets/PlayForge/Documentation`, and the API reference from the package's source and its comments. The public
`gabedvdsn/PlayForge-Docs` repository holds the built site, and its GitHub Pages serves it at
https://gabedvdsn.github.io/PlayForge-Docs/.

- **Build and look:** `python docfx/build.py --serve`, then open http://localhost:8080. It needs the .NET 8 SDK,
  Python 3, the Unity install the project uses, and the project opened in Unity once, which compiles the packages
  the API reference reads against into its Library.
- **Publish:** `bash docfx/publish.sh` after a commit that changes the guides or the package's public API. It builds
  the site from a copy of HEAD, so edits not yet committed never reach it, and pushes it to PlayForge-Docs.

## How a build goes

`build.py` runs four steps:

1. **References:** it copies the assemblies the package compiles against into `obj/refs`: Unity's own, from its install
   (`UNITY_EDITOR_DATA` names another), and the packages', from the project's Library.
2. **Metadata:** DocFX reads the runtime, the Creator Lab, the editor and the test kit as one compilation into `obj/api`:
   a page for each public type.
3. **Prepare** (`prepare.py`) writes the generated pages, all ignored by git:
   - `guides/`: each guide a folder, with its opening at `index.md` and each `##` section a page of its own. A section
     with two or more `###` parts gives each part a page too, in a folder named for the section, under a short page of
     the section's opening and a list of its parts. Links between guides go to the page holding the heading they name,
     and the contents list each guide's sections and parts.
   - Figures: a `<!-- screenshot: name -->` line in a guide becomes `images/editor/name.png` where that image exists, and
     `diagrams/diagrams.json` places each Mermaid diagram of `diagrams/`, or an image, on its page, after its opening
     paragraph. The images and diagrams are the site's alone; the package ships the comments, which show nowhere.
   - Links between the guides and the API: a type or member a guide names in backticks, as `GameplayEffect` or
     `ForgeLog.MinimumLevel`, links to its page, once on each page. Each type's page says which guide sections
     name it (`apidoc/`, DocFX overwrite files), or else which guide covers its area.
   - `api/`: the metadata, each item given its assembly, from where its source is, with no links to the source; the
     contents grouped by area; and the reference's opening page, from `api.md`.
4. **Build:** DocFX makes the site in `_site`.

The build stops on a link to a guide or a heading that isn't there, a guide the site doesn't list, a type in a folder no
area names, or a help topic with no page.

## Changing it

- **A new guide** goes in `GUIDE_GROUPS` in `prepare.py`, and in `index.md`'s table as `guides/<name>/index.md`: its
  file's name in lowercase, with a hyphen for each underscore.
- **A new folder of source** goes in `AREAS` in `prepare.py`, which says which part of the API and which area its
  types are listed under, and which guide covers them.
- **Help buttons:** `ForgeDocs.UrlOf` opens a topic's section as its own page, named as the site names its heading, so
  a heading a help topic names keeps its words, or its topic moves with it. The build checks each topic's page.
- **Screenshots:** Proving Grounds' **Window → PlayForge → Docs → Capture Screenshots** draws each editor tool into a PNG,
  with the Demo's assets; from the command line, `-executeMethod
  FarEmerald.PlayForge.ProvingGrounds.DocsCapture.DocsScreenshots.CaptureAndQuit -docsOut "<repo>/docfx/images/editor"`
  without `-batchmode`, as the editor draws no window in batch mode. A guide asks for one with its comment, above.
- **DocFX** 2.81.0 is pinned in `.config/dotnet-tools.json`.
