# IconRenderer CLI

Render one or more icons from JSON request files:

```sh
dotnet run --project tools/IconRenderer.Cli -- requests.json
```

The file can contain one JSON object, an array of request objects, or newline-delimited JSON objects. Relative output paths are resolved against the request file's directory. Icon names are the flat static field names in `Icons`, such as `HeartPlus`.

Use `iconNames` in place of `iconName` to apply one shared set of render options to multiple icons. Each icon is written to its own output image:

```json
{
  "iconNames": ["HeartPlus", "Violin", "DragonHead"],
  "outputDirectory": "shared-style-icons",
  "outputFileNamePattern": "{iconName}-{index}.{format}",
  "width": 192,
  "height": 192,
  "format": "png",
  "foregroundColor": "#ffffff",
  "background": { "shape": "circular", "color": "#34495e" },
  "backgroundOutline": {
    "width": 8,
    "colorSource": "custom",
    "customColor": "#f1c40f"
  }
}
```

The output filename pattern supports `{iconName}`, `{format}`, and zero-based `{index}` placeholders. Batch requests use the same rendering, tiling, and output settings as single-icon requests.

Example single-icon request array:

```json
[
  {
    "iconName": "HeartPlus",
    "outputPath": "output/heart-plus.png",
    "width": 256,
    "height": 256,
    "dpi": 96,
    "format": "png",
    "foregroundColor": "#ffffff",
    "background": {
      "shape": "rounded",
      "color": "#34495e",
      "cornerRadius": 64
    },
    "foregroundOutline": {
      "width": 5,
      "colorSource": "background"
    },
    "backgroundOutline": {
      "width": 8,
      "colorSource": "custom",
      "customColor": "#f1c40f"
    },
    "tiling": {
      "columns": 2,
      "rows": 2,
      "spacing": 12,
      "padding": 16,
      "canvasColor": "#202020"
    }
  }
]
```

The output image dimensions are calculated from the icon width/height, tile rows and columns, spacing, and padding. The returned image is rendered through SVG.NET/System.Drawing; non-Windows systems may require compatible GDI+ support.
