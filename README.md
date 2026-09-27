# IconRenderer

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
