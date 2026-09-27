using System.Globalization;
using System.Drawing;
using System.Text;
using System.Xml.Linq;
using Svg;

namespace IconRenderer;

/// <summary>Specifies the color source used by a foreground or background outline.</summary>
public enum IconBorderColor
{
    /// <summary>Use the icon's foreground color.</summary>
    Foreground,
    /// <summary>Use the background layer's color.</summary>
    Background,
    /// <summary>Use the separate custom border color argument.</summary>
    Custom
}

/// <summary>Helpers for recoloring game-icons.net SVGs and adding a background layer.</summary>
public static class SvgIcon
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> EmbeddedResources = new(CreateResourceIndex);
    private static readonly HashSet<string> DrawableElementNames = new(StringComparer.Ordinal)
    {
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text"
    };

    /// <summary>Recolors the foreground of an SVG while preserving explicitly unpainted areas.</summary>
    public static string Recolor(string svg, string foregroundColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foregroundColor);
        var document = Parse(svg);
        foreach (var element in document.Root!.DescendantsAndSelf())
        {
            SetPaint(element, "fill", foregroundColor);
            SetPaint(element, "stroke", foregroundColor);
        }

        return Serialize(document);
    }

    /// <summary>Adds a full-viewBox rectangular background, recolors the foreground, and optionally outlines it.</summary>
    /// <remarks><paramref name="borderWidth"/> outlines the icon foreground; <paramref name="backgroundBorderWidth"/> independently outlines the background shape.</remarks>
    public static string WithSquareBackground(
        string svg,
        string backgroundColor,
        string foregroundColor = "#ffffff",
        double borderWidth = 0,
        IconBorderColor borderColorSource = IconBorderColor.Foreground,
        string? customBorderColor = null,
        double backgroundBorderWidth = 0,
        IconBorderColor backgroundBorderColorSource = IconBorderColor.Background,
        string? customBackgroundBorderColor = null) =>
        WithBackground(svg, backgroundColor, foregroundColor, BackgroundShape.Square,
            borderWidth: borderWidth, borderColorSource: borderColorSource, customBorderColor: customBorderColor,
            backgroundBorderWidth: backgroundBorderWidth, backgroundBorderColorSource: backgroundBorderColorSource,
            customBackgroundBorderColor: customBackgroundBorderColor);

    /// <summary>Adds a circular background, recolors the foreground, and optionally outlines it.</summary>
    /// <remarks><paramref name="borderWidth"/> outlines the icon foreground; <paramref name="backgroundBorderWidth"/> independently outlines the background shape.</remarks>
    public static string WithCircularBackground(
        string svg,
        string backgroundColor,
        string foregroundColor = "#ffffff",
        double borderWidth = 0,
        IconBorderColor borderColorSource = IconBorderColor.Foreground,
        string? customBorderColor = null,
        double backgroundBorderWidth = 0,
        IconBorderColor backgroundBorderColorSource = IconBorderColor.Background,
        string? customBackgroundBorderColor = null) =>
        WithBackground(svg, backgroundColor, foregroundColor, BackgroundShape.Circle,
            borderWidth: borderWidth, borderColorSource: borderColorSource, customBorderColor: customBorderColor,
            backgroundBorderWidth: backgroundBorderWidth, backgroundBorderColorSource: backgroundBorderColorSource,
            customBackgroundBorderColor: customBackgroundBorderColor);

    /// <summary>Adds a rounded rectangular background, recolors the foreground, and optionally outlines it.</summary>
    /// <remarks><paramref name="borderWidth"/> outlines the icon foreground; <paramref name="backgroundBorderWidth"/> independently outlines the background shape.</remarks>
    public static string WithRoundedBackground(
        string svg,
        string backgroundColor,
        string foregroundColor = "#ffffff",
        double cornerRadius = 64,
        double borderWidth = 0,
        IconBorderColor borderColorSource = IconBorderColor.Foreground,
        string? customBorderColor = null,
        double backgroundBorderWidth = 0,
        IconBorderColor backgroundBorderColorSource = IconBorderColor.Background,
        string? customBackgroundBorderColor = null) =>
        WithBackground(svg, backgroundColor, foregroundColor, BackgroundShape.Rounded, cornerRadius,
            borderWidth, borderColorSource, customBorderColor,
            backgroundBorderWidth, backgroundBorderColorSource, customBackgroundBorderColor);

    /// <summary>Adds an outline to the foreground shapes in an SVG string.</summary>
    public static string WithForegroundOutline(string svg, double borderWidth, string borderColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(borderColor);
        ValidateOutlineWidth(borderWidth, nameof(borderWidth));
        var document = Parse(svg);
        if (borderWidth > 0)
            AddForegroundBorder(document.Root!, borderWidth, borderColor);
        return Serialize(document);
    }

    /// <summary>
    /// Renders an SVG string to a bitmap using the document's intrinsic dimensions.
    /// The returned bitmap is owned by the caller and must be disposed.
    /// </summary>
    /// <remarks>
    /// Rendering uses SVG.NET and System.Drawing/GDI+. On non-Windows platforms, SVG.NET may
    /// require a compatible GDI+ implementation such as libgdiplus.
    /// </remarks>
    public static Bitmap RenderToBitmap(string svg)
    {
        using var stream = CreateSvgStream(svg);
        var document = SvgDocument.Open<SvgDocument>(stream);
        return document.Draw()
            ?? throw new InvalidOperationException("The SVG document did not produce a bitmap.");
    }

    /// <summary>
    /// Renders an SVG string to a bitmap at the requested dimensions.
    /// The returned bitmap is owned by the caller and must be disposed.
    /// </summary>
    /// <remarks>
    /// Rendering uses SVG.NET and System.Drawing/GDI+. On non-Windows platforms, SVG.NET may
    /// require a compatible GDI+ implementation such as libgdiplus.
    /// </remarks>
    public static Bitmap RenderToBitmap(string svg, int width, int height)
    {
        ValidateBitmapDimensions(width, height);
        using var stream = CreateSvgStream(svg);
        var document = SvgDocument.Open<SvgDocument>(stream);
        return document.Draw(width, height)
            ?? throw new InvalidOperationException("The SVG document did not produce a bitmap.");
    }

    /// <summary>Renders an SVG string to a square bitmap at the requested size.</summary>
    public static Bitmap RenderSquareToBitmap(string svg, int size) => RenderToBitmap(svg, size, size);

    private static MemoryStream CreateSvgStream(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);
        return new MemoryStream(Encoding.UTF8.GetBytes(svg));
    }

    private static void ValidateBitmapDimensions(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Bitmap width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Bitmap height must be positive.");
    }

    private static string WithBackground(
        string svg,
        string backgroundColor,
        string foregroundColor,
        BackgroundShape shape,
        double cornerRadius = 0,
        double borderWidth = 0,
        IconBorderColor borderColorSource = IconBorderColor.Foreground,
        string? customBorderColor = null,
        double backgroundBorderWidth = 0,
        IconBorderColor backgroundBorderColorSource = IconBorderColor.Background,
        string? customBackgroundBorderColor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundColor);
        ArgumentException.ThrowIfNullOrWhiteSpace(foregroundColor);
        if (cornerRadius < 0 || double.IsNaN(cornerRadius) || double.IsInfinity(cornerRadius))
            throw new ArgumentOutOfRangeException(nameof(cornerRadius), "Corner radius must be a finite, non-negative number.");
        ValidateOutlineWidth(borderWidth, nameof(borderWidth));
        ValidateOutlineWidth(backgroundBorderWidth, nameof(backgroundBorderWidth));
        if (!Enum.IsDefined(borderColorSource))
            throw new ArgumentOutOfRangeException(nameof(borderColorSource));
        if (borderWidth > 0 && borderColorSource == IconBorderColor.Custom)
            ArgumentException.ThrowIfNullOrWhiteSpace(customBorderColor);
        if (!Enum.IsDefined(backgroundBorderColorSource))
            throw new ArgumentOutOfRangeException(nameof(backgroundBorderColorSource));
        if (backgroundBorderWidth > 0 && backgroundBorderColorSource == IconBorderColor.Custom)
            ArgumentException.ThrowIfNullOrWhiteSpace(customBackgroundBorderColor);

        var document = Parse(svg);
        var root = document.Root!;
        var (x, y, width, height) = GetViewBox(root);
        if (borderWidth >= Math.Min(width, height) && borderWidth > 0)
            throw new ArgumentOutOfRangeException(nameof(borderWidth), "Foreground outline width must be smaller than the SVG viewBox dimensions.");
        if (backgroundBorderWidth >= Math.Min(width, height) && backgroundBorderWidth > 0)
            throw new ArgumentOutOfRangeException(nameof(backgroundBorderWidth), "Background outline width must be smaller than the SVG viewBox dimensions.");

        var borderColor = borderWidth == 0 ? null : borderColorSource switch
        {
            IconBorderColor.Foreground => foregroundColor,
            IconBorderColor.Background => backgroundColor,
            IconBorderColor.Custom => customBorderColor!,
            _ => throw new ArgumentOutOfRangeException(nameof(borderColorSource))
        };
        var backgroundBorderColor = backgroundBorderWidth == 0 ? null : backgroundBorderColorSource switch
        {
            IconBorderColor.Foreground => foregroundColor,
            IconBorderColor.Background => backgroundColor,
            IconBorderColor.Custom => customBackgroundBorderColor!,
            _ => throw new ArgumentOutOfRangeException(nameof(backgroundBorderColorSource))
        };

        foreach (var element in root.DescendantsAndSelf())
        {
            SetPaint(element, "fill", foregroundColor);
            SetPaint(element, "stroke", foregroundColor);
        }

        if (borderWidth > 0)
            AddForegroundBorder(root, borderWidth, borderColor!);

        XElement background = shape switch
        {
            BackgroundShape.Circle => new XElement(SvgNamespace + "circle",
                new XAttribute("cx", Format(x + width / 2)),
                new XAttribute("cy", Format(y + height / 2)),
                new XAttribute("r", Format(Math.Min(width, height) / 2)),
                new XAttribute("fill", backgroundColor)),
            BackgroundShape.Rounded => new XElement(SvgNamespace + "rect",
                new XAttribute("x", Format(x)),
                new XAttribute("y", Format(y)),
                new XAttribute("width", Format(width)),
                new XAttribute("height", Format(height)),
                new XAttribute("rx", Format(Math.Min(cornerRadius, Math.Min(width, height) / 2))),
                new XAttribute("fill", backgroundColor)),
            _ => new XElement(SvgNamespace + "rect",
                new XAttribute("x", Format(x)),
                new XAttribute("y", Format(y)),
                new XAttribute("width", Format(width)),
                new XAttribute("height", Format(height)),
                new XAttribute("fill", backgroundColor))
        };

        root.AddFirst(background);
        if (backgroundBorderWidth > 0)
        {
            background.AddAfterSelf(CreateBackgroundOutline(
                shape, x, y, width, height, cornerRadius, backgroundBorderWidth, backgroundBorderColor!));
        }
        return Serialize(document);
    }

    private static XElement CreateBackgroundOutline(
        BackgroundShape shape,
        double x,
        double y,
        double width,
        double height,
        double cornerRadius,
        double borderWidth,
        string color)
    {
        var halfWidth = borderWidth / 2;
        XElement outline = shape switch
        {
            BackgroundShape.Circle => new XElement(SvgNamespace + "circle",
                new XAttribute("cx", Format(x + width / 2)),
                new XAttribute("cy", Format(y + height / 2)),
                new XAttribute("r", Format(Math.Min(width, height) / 2 - halfWidth))),
            BackgroundShape.Rounded => new XElement(SvgNamespace + "rect",
                new XAttribute("x", Format(x + halfWidth)),
                new XAttribute("y", Format(y + halfWidth)),
                new XAttribute("width", Format(width - borderWidth)),
                new XAttribute("height", Format(height - borderWidth)),
                new XAttribute("rx", Format(Math.Max(0, Math.Min(cornerRadius, Math.Min(width, height) / 2) - halfWidth))),
                new XAttribute("ry", Format(Math.Max(0, Math.Min(cornerRadius, Math.Min(width, height) / 2) - halfWidth)))),
            _ => new XElement(SvgNamespace + "rect",
                new XAttribute("x", Format(x + halfWidth)),
                new XAttribute("y", Format(y + halfWidth)),
                new XAttribute("width", Format(width - borderWidth)),
                new XAttribute("height", Format(height - borderWidth)))
        };

        outline.SetAttributeValue("fill", "none");
        outline.SetAttributeValue("stroke", color);
        outline.SetAttributeValue("stroke-width", Format(borderWidth));
        outline.SetAttributeValue("stroke-linejoin", "round");
        return outline;
    }

    private static void ValidateOutlineWidth(double width, string parameterName)
    {
        if (width < 0 || !double.IsFinite(width))
            throw new ArgumentOutOfRangeException(parameterName, "Outline width must be a finite, non-negative number.");
    }

    private static void AddForegroundBorder(XElement root, double borderWidth, string color)
    {
        foreach (var element in root.DescendantsAndSelf().Where(element => DrawableElementNames.Contains(element.Name.LocalName)))
        {
            element.SetAttributeValue("stroke", color);
            element.SetAttributeValue("stroke-width", Format(borderWidth));
            element.SetAttributeValue("stroke-linejoin", "round");
            element.SetAttributeValue("stroke-linecap", "round");
            SetStyleProperty(element, "stroke", color);
            SetStyleProperty(element, "stroke-width", Format(borderWidth));
            SetStyleProperty(element, "stroke-linejoin", "round");
            SetStyleProperty(element, "stroke-linecap", "round");
        }
    }

    private static void SetStyleProperty(XElement element, string property, string value)
    {
        var declarations = ((string?)element.Attribute("style") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var found = false;
        for (var index = 0; index < declarations.Count; index++)
        {
            var declaration = declarations[index];
            var separator = declaration.IndexOf(':');
            if (separator < 0 || !string.Equals(declaration[..separator].Trim(), property, StringComparison.OrdinalIgnoreCase))
                continue;
            declarations[index] = $"{property}:{value}";
            found = true;
        }

        if (!found)
            declarations.Add($"{property}:{value}");
        element.SetAttributeValue("style", string.Join(';', declarations));
    }

    private static XDocument Parse(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);
        var document = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name.LocalName != "svg")
            throw new ArgumentException("The input must be an SVG document.", nameof(svg));
        return document;
    }

    private static (double X, double Y, double Width, double Height) GetViewBox(XElement root)
    {
        var viewBox = (string?)root.Attribute("viewBox");
        if (viewBox is not null)
        {
            var values = viewBox.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 4 && values.All(value =>
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                    double.IsFinite(parsed)))
            {
                var x = double.Parse(values[0], CultureInfo.InvariantCulture);
                var y = double.Parse(values[1], CultureInfo.InvariantCulture);
                var width = double.Parse(values[2], CultureInfo.InvariantCulture);
                var height = double.Parse(values[3], CultureInfo.InvariantCulture);
                if (width > 0 && height > 0)
                    return (x, y, width, height);
            }
        }

        if (TryDimension((string?)root.Attribute("width"), out var widthValue) &&
            TryDimension((string?)root.Attribute("height"), out var heightValue) &&
            widthValue > 0 && heightValue > 0)
            return (0, 0, widthValue, heightValue);

        throw new ArgumentException("SVG must have a valid viewBox or numeric width and height.", nameof(root));
    }

    private static bool TryDimension(string? value, out double result)
    {
        result = 0;
        if (value is null)
            return false;
        var numericPart = new string(value.TakeWhile(character =>
            char.IsDigit(character) || character is '.' or '-' or '+' or 'e' or 'E').ToArray());
        return double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
               double.IsFinite(result);
    }

    private static void SetPaint(XElement element, string property, string color)
    {
        var attribute = element.Attribute(property);
        if (attribute is not null && !string.Equals(attribute.Value, "none", StringComparison.OrdinalIgnoreCase))
            attribute.Value = color;

        var style = (string?)element.Attribute("style");
        if (style is null)
            return;

        var declarations = style.Split(';');
        var changed = false;
        for (var index = 0; index < declarations.Length; index++)
        {
            var separator = declarations[index].IndexOf(':');
            if (separator < 0 || !string.Equals(declarations[index][..separator].Trim(), property, StringComparison.OrdinalIgnoreCase))
                continue;
            var existingValue = declarations[index][(separator + 1)..].Trim();
            if (string.Equals(existingValue, "none", StringComparison.OrdinalIgnoreCase))
                continue;
            declarations[index] = declarations[index][..(separator + 1)] + color;
            changed = true;
        }

        if (changed)
            element.SetAttributeValue("style", string.Join(';', declarations));
    }

    private static string Format(double value) => value.ToString("0.################", CultureInfo.InvariantCulture);

    private static string Serialize(XDocument document) => document.ToString(SaveOptions.DisableFormatting);

    internal static string LoadEmbedded(string fileName, int matchingFileIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!EmbeddedResources.Value.TryGetValue(fileName, out var resourceNames) ||
            matchingFileIndex < 0 || matchingFileIndex >= resourceNames.Length)
            throw new InvalidOperationException($"Embedded icon '{fileName}' was not found.");

        using var stream = typeof(Icons).Assembly.GetManifestResourceStream(resourceNames[matchingFileIndex])
            ?? throw new InvalidOperationException($"Embedded icon '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, string[]> CreateResourceIndex()
    {
        var resources = typeof(Icons).Assembly.GetManifestResourceNames()
            .OrderBy(name => name, StringComparer.Ordinal)
            .GroupBy(name => name[(Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\')) + 1)..], StringComparer.Ordinal);
        return resources.ToDictionary(
            group => group.Key,
            group => group.ToArray(),
            StringComparer.Ordinal);
    }

    private enum BackgroundShape
    {
        Square,
        Circle,
        Rounded
    }
}
