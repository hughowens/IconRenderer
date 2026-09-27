# Icon rendering tests

Run the render test from the repository root:

```sh
dotnet test tests/IconRenderer.Tests/IconRenderer.Tests.csproj
```

The test writes four 256 × 256 PNG examples to `tests/IconRenderer.Tests/bin/<Configuration>/net9.0/rendered-icons/`:

- `heart-plus-square.png`
- `skull-with-syringe-circle.png`
- `violin-rounded.png`
- `dragon-head-transparent.png`

The three background examples demonstrate foreground outlines, plus independent background-shape outlines with custom and background-derived colors.

Rendering uses SVG.NET and `System.Drawing`/GDI+. On non-Windows platforms, a compatible GDI+ implementation may be required.
