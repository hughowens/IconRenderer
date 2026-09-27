# IconRenderer

> **Icon collection:** [game-icons.net](https://game-icons.net/) — 4,180 embedded SVG icons.

A .NET library embedding 4,180 game-icons.net SVGs, SVG color/background helpers, bitmap rendering, a JSON-driven rendering CLI, and a Raylib/ImGui browser.

## Solution layout

- `src/IconRenderer` — library source and embedded SVG collection.
- `tools/IconRenderer.Cli` — command-line renderer for JSON request files.
- `tools/IconRenderer.Browser` — Raylib-cs/ImGui icon browser and preset renderer.
- `tests/IconRenderer.Tests` — tests that render example PNGs.

## Build and test

```sh
dotnet build IconRenderer.sln
dotnet test IconRenderer.sln
```

## Run the tools

```sh
dotnet run --project tools/IconRenderer.Cli -- requests.json
dotnet run --project tools/IconRenderer.Browser
```

See the tool-specific READMEs for request formats, browser controls, and output locations. Bitmap rendering uses SVG.NET/System.Drawing; the browser additionally requires a desktop graphics environment.

## Licensing and attribution

The embedded SVG icons are from [game-icons.net](https://game-icons.net/) and are provided under the [Creative Commons Attribution 3.0 (CC BY 3.0)](https://creativecommons.org/licenses/by/3.0/) license, except icons specifically marked CC0 in the included [icon credits and license file](src/IconRenderer/icons/license.txt). When using the icons, preserve the required attribution: “Icons made by {author}”. The included file lists contributors and any CC0 exceptions.

The remaining IconRenderer source code is released under the MIT License. These terms do not replace the separate licenses for the icons or third-party dependencies.
