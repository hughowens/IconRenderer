namespace IconRenderer;

/// <summary>Describes one icon-rendering operation for use by the rendering utility.</summary>
public sealed class IconRenderRequest : IconRenderSettings
{
    /// <summary>Static icon field name, for example <c>HeartPlus</c>.</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>Destination image file path.</summary>
    public string OutputPath { get; set; } = string.Empty;
}

/// <summary>Shared render settings used by single-icon and multi-icon requests.</summary>
public class IconRenderSettings
{
    /// <summary>Pixel width of an individual icon tile.</summary>
    public int Width { get; set; } = 256;

    /// <summary>Pixel height of an individual icon tile.</summary>
    public int Height { get; set; } = 256;

    /// <summary>Image resolution in dots per inch.</summary>
    public int Dpi { get; set; } = 96;

    /// <summary>Output bitmap format.</summary>
    public IconRenderFormat Format { get; set; } = IconRenderFormat.Png;

    /// <summary>Color applied to the icon foreground.</summary>
    public string ForegroundColor { get; set; } = "#ffffff";

    /// <summary>Background shape and color options.</summary>
    public IconBackgroundOptions Background { get; set; } = new();

    /// <summary>Outline options for the icon foreground, independent of the background outline.</summary>
    public IconOutlineOptions ForegroundOutline { get; set; } = new();

    /// <summary>Outline options for the background shape, independent of the foreground outline.</summary>
    public IconOutlineOptions BackgroundOutline { get; set; } = new();

    /// <summary>Options for repeating this icon in a tiled output image.</summary>
    public IconTilingOptions Tiling { get; set; } = new();
}

/// <summary>Applies one set of render settings to many icons.</summary>
public sealed class IconRenderBatchRequest : IconRenderSettings
{
    /// <summary>Static icon field names to render with the shared settings.</summary>
    public List<string> IconNames { get; set; } = [];

    /// <summary>Directory for output images, relative to the request file unless rooted.</summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>Output filename pattern. Supports <c>{iconName}</c>, <c>{format}</c>, and zero-based <c>{index}</c>.</summary>
    public string OutputFileNamePattern { get; set; } = "{iconName}.{format}";
}

/// <summary>Supported bitmap file formats for an icon-render request.</summary>
public enum IconRenderFormat
{
    Png,
    Jpeg,
    Bmp
}

/// <summary>Background layer options for an icon-render request.</summary>
public sealed class IconBackgroundOptions
{
    /// <summary>Shape of the background layer; <see cref="IconBackgroundShape.None"/> leaves it transparent.</summary>
    public IconBackgroundShape Shape { get; set; } = IconBackgroundShape.None;

    /// <summary>Background fill color.</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>Corner radius in SVG viewBox units for a rounded background.</summary>
    public double CornerRadius { get; set; } = 64;
}

/// <summary>Available background layer shapes.</summary>
public enum IconBackgroundShape
{
    None,
    Square,
    Circular,
    Rounded
}

/// <summary>Options for outlining either the icon foreground or its background layer.</summary>
public sealed class IconOutlineOptions
{
    /// <summary>Outline width in SVG viewBox units. Zero disables the outline.</summary>
    public double Width { get; set; }

    /// <summary>Color source for this outline.</summary>
    public IconOutlineColorSource ColorSource { get; set; } = IconOutlineColorSource.Foreground;

    /// <summary>Used when <see cref="ColorSource"/> is <see cref="IconOutlineColorSource.Custom"/>.</summary>
    public string CustomColor { get; set; } = "#ffffff";
}

/// <summary>Sources from which an outline color can be selected.</summary>
public enum IconOutlineColorSource
{
    Foreground,
    Background,
    Custom
}

/// <summary>Layout options for repeating an icon in a tiled output image.</summary>
public sealed class IconTilingOptions
{
    /// <summary>Number of tile columns.</summary>
    public int Columns { get; set; } = 1;

    /// <summary>Number of tile rows.</summary>
    public int Rows { get; set; } = 1;

    /// <summary>Pixel spacing between tiles.</summary>
    public int Spacing { get; set; }

    /// <summary>Pixel padding around the outside of the tiled image.</summary>
    public int Padding { get; set; }

    /// <summary>Optional fill color for the entire tiled output canvas.</summary>
    public string? CanvasColor { get; set; }
}
