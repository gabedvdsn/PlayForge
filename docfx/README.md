# PlayForge's documentation site

DocFX builds the site from the guides the package ships, `Assets/PlayForge/Documentation`, with no copies:
`guides/toc.yml` orders them and `index.md` is the landing page. The public `gabedvdsn/PlayForge-Docs` repository holds
the built site, and its GitHub Pages serves it at https://gabedvdsn.github.io/PlayForge-Docs/.

- **Build and look:** `dotnet tool restore`, then `dotnet docfx docfx/docfx.json --serve`, and open
  http://localhost:8080.
- **Publish:** `bash docfx/publish.sh` after a guide changes. It builds the site and pushes it to PlayForge-Docs.
- **A new guide** goes in `guides/toc.yml` and `index.md`, linked by its path under `Assets/PlayForge/Documentation`.
- **Help buttons:** `ForgeDocs.UrlOf` opens a topic's page at its heading's anchor, so a heading a help topic names
  keeps its words, or its topic moves with it.
- **DocFX** 2.81.0 is pinned in `.config/dotnet-tools.json` and runs on the .NET 8 SDK.
