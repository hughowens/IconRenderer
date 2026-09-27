using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using IconRenderer;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: IconRenderer.Cli <requests.json>");
    return 2;
}

var requestsFile = Path.GetFullPath(args[0]);
if (!File.Exists(requestsFile))
{
    Console.Error.WriteLine($"Request file not found: {requestsFile}");
    return 2;
}

IReadOnlyList<IconRenderRequest> requests;
try
{
    requests = ReadRequests(File.ReadAllText(requestsFile), Path.GetDirectoryName(requestsFile)!);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Could not read render requests: {exception.Message}");
    return 2;
}

var failures = 0;
for (var index = 0; index < requests.Count; index++)
{
    var request = requests[index];
    try
    {
        var outputFile = Render(request, Path.GetDirectoryName(requestsFile)!);
        Console.WriteLine($"Rendered {request.IconName} -> {outputFile}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"Request {index + 1} ({request.IconName}): {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static IReadOnlyList<IconRenderRequest> ReadRequests(string json, string requestsDirectory)
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    try
    {
        using var document = JsonDocument.Parse(json);
        var requests = new List<IconRenderRequest>();
        switch (document.RootElement.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var element in document.RootElement.EnumerateArray())
                    AddRequest(element, requests, options, requestsDirectory);
                break;
            case JsonValueKind.Object:
                AddRequest(document.RootElement, requests, options, requestsDirectory);
                break;
            default:
                throw new InvalidDataException("The JSON root must be a request object or an array of request objects.");
        }

        if (requests.Count == 0)
            throw new InvalidDataException("The request file contains no render requests.");
        return requests;
    }
    catch (JsonException)
    {
        // Also accept newline-delimited JSON objects, convenient for generated request files.
        var requests = new List<IconRenderRequest>();
        var lines = json.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
                continue;
            try
            {
                using var lineDocument = JsonDocument.Parse(line);
                AddRequest(lineDocument.RootElement, requests, options, requestsDirectory);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Invalid JSON request on line {lineIndex + 1}.", exception);
            }
        }

        if (requests.Count == 0)
            throw new InvalidDataException("The request file contains no render requests.");
        return requests;
    }
}

static void AddRequest(
    JsonElement element,
    List<IconRenderRequest> requests,
    JsonSerializerOptions options,
    string requestsDirectory)
{
    if (element.ValueKind != JsonValueKind.Object)
        throw new InvalidDataException("Each render request must be a JSON object.");

    var isBatchRequest = element.EnumerateObject()
        .Any(property => string.Equals(property.Name, "iconNames", StringComparison.OrdinalIgnoreCase));
    if (!isBatchRequest)
    {
        requests.Add(element.Deserialize<IconRenderRequest>(options)
            ?? throw new InvalidDataException("The render request was null."));
        return;
    }

    var batch = element.Deserialize<IconRenderBatchRequest>(options)
        ?? throw new InvalidDataException("The batch render request was null.");
    if (batch.IconNames is null || batch.IconNames.Count == 0)
        throw new InvalidDataException("A batch request must contain at least one icon name.");
    ArgumentException.ThrowIfNullOrWhiteSpace(batch.OutputDirectory);
    ArgumentException.ThrowIfNullOrWhiteSpace(batch.OutputFileNamePattern);

    for (var index = 0; index < batch.IconNames.Count; index++)
    {
        var iconName = batch.IconNames[index];
        ArgumentException.ThrowIfNullOrWhiteSpace(iconName);
        var fileName = batch.OutputFileNamePattern
            .Replace("{iconName}", iconName, StringComparison.OrdinalIgnoreCase)
            .Replace("{format}", batch.Format.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        var outputPath = Path.Combine(batch.OutputDirectory, fileName);
        requests.Add(new IconRenderRequest
        {
            IconName = iconName,
            OutputPath = outputPath,
            Width = batch.Width,
            Height = batch.Height,
            Dpi = batch.Dpi,
            Format = batch.Format,
            ForegroundColor = batch.ForegroundColor,
            Background = batch.Background,
            ForegroundOutline = batch.ForegroundOutline,
            BackgroundOutline = batch.BackgroundOutline,
            Tiling = batch.Tiling
        });
    }
}

static string Render(IconRenderRequest request, string requestsDirectory)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(request.IconName);
    ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
    if (request.Width <= 0 || request.Height <= 0 || request.Dpi <= 0)
        throw new ArgumentOutOfRangeException(nameof(request), "Width, height, and DPI must be positive.");
    ArgumentNullException.ThrowIfNull(request.Background);
    ArgumentNullException.ThrowIfNull(request.ForegroundOutline);
    ArgumentNullException.ThrowIfNull(request.BackgroundOutline);
    ArgumentNullException.ThrowIfNull(request.Tiling);
    ValidateOutlineOptions(request.ForegroundOutline, nameof(request.ForegroundOutline));
    ValidateOutlineOptions(request.BackgroundOutline, nameof(request.BackgroundOutline));
    if (request.Background.CornerRadius < 0 || !double.IsFinite(request.Background.CornerRadius))
        throw new ArgumentOutOfRangeException(nameof(request.Background.CornerRadius), "Corner radius must be finite and non-negative.");
    if (request.Tiling.Columns <= 0 || request.Tiling.Rows <= 0 ||
        request.Tiling.Spacing < 0 || request.Tiling.Padding < 0)
        throw new ArgumentOutOfRangeException(nameof(request.Tiling), "Tile rows and columns must be positive; spacing and padding cannot be negative.");
    if (request.Tiling.CanvasColor is not null && string.IsNullOrWhiteSpace(request.Tiling.CanvasColor))
        throw new ArgumentException("Canvas color cannot be empty when specified.", nameof(request.Tiling.CanvasColor));

    var iconField = typeof(Icons).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .FirstOrDefault(field => !field.IsLiteral && field.FieldType == typeof(string) &&
                                 string.Equals(field.Name, request.IconName, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown icon '{request.IconName}'. Use a static icon field name such as 'HeartPlus'.");
    var svg = (string)iconField.GetValue(null)!;

    ArgumentException.ThrowIfNullOrWhiteSpace(request.ForegroundColor);
    var backgroundColor = request.Background.Color;
    var hasBackground = request.Background.Shape != IconBackgroundShape.None;
    if (hasBackground)
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundColor);
    else if (request.BackgroundOutline.Width > 0)
        throw new ArgumentException("A background outline requires a background shape.");

    var foregroundOutlineColor = ResolveOutlineColor(request.ForegroundOutline, request.ForegroundColor, backgroundColor);
    if (!hasBackground)
    {
        svg = SvgIcon.Recolor(svg, request.ForegroundColor);
        if (request.ForegroundOutline.Width > 0)
            svg = SvgIcon.WithForegroundOutline(svg, request.ForegroundOutline.Width, foregroundOutlineColor);
    }
    else
    {
        var backgroundOutlineColor = ResolveOutlineColor(request.BackgroundOutline, request.ForegroundColor, backgroundColor);
        var foregroundSource = ToLibraryBorderSource(request.ForegroundOutline.ColorSource);
        var backgroundSource = ToLibraryBorderSource(request.BackgroundOutline.ColorSource);
        svg = request.Background.Shape switch
        {
            IconBackgroundShape.Square => SvgIcon.WithSquareBackground(
                svg, backgroundColor, request.ForegroundColor,
                request.ForegroundOutline.Width, foregroundSource, foregroundOutlineColor,
                request.BackgroundOutline.Width, backgroundSource, backgroundOutlineColor),
            IconBackgroundShape.Circular => SvgIcon.WithCircularBackground(
                svg, backgroundColor, request.ForegroundColor,
                request.ForegroundOutline.Width, foregroundSource, foregroundOutlineColor,
                request.BackgroundOutline.Width, backgroundSource, backgroundOutlineColor),
            IconBackgroundShape.Rounded => SvgIcon.WithRoundedBackground(
                svg, backgroundColor, request.ForegroundColor, request.Background.CornerRadius,
                request.ForegroundOutline.Width, foregroundSource, foregroundOutlineColor,
                request.BackgroundOutline.Width, backgroundSource, backgroundOutlineColor),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Background.Shape))
        };
    }

    var outputWidth = checked(request.Width * request.Tiling.Columns +
                              request.Tiling.Spacing * (request.Tiling.Columns - 1) + request.Tiling.Padding * 2);
    var outputHeight = checked(request.Height * request.Tiling.Rows +
                               request.Tiling.Spacing * (request.Tiling.Rows - 1) + request.Tiling.Padding * 2);
    var outputFile = Path.IsPathRooted(request.OutputPath)
        ? request.OutputPath
        : Path.GetFullPath(Path.Combine(requestsDirectory, request.OutputPath));
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

    using var canvas = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);
    canvas.SetResolution(request.Dpi, request.Dpi);
    using (var graphics = Graphics.FromImage(canvas))
    {
        graphics.Clear(request.Tiling.CanvasColor is not null
            ? ColorTranslator.FromHtml(request.Tiling.CanvasColor)
            : request.Format == IconRenderFormat.Jpeg ? Color.White : Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var tile = SvgIcon.RenderToBitmap(svg, request.Width, request.Height);
        for (var row = 0; row < request.Tiling.Rows; row++)
        for (var column = 0; column < request.Tiling.Columns; column++)
        {
            var x = request.Tiling.Padding + column * (request.Width + request.Tiling.Spacing);
            var y = request.Tiling.Padding + row * (request.Height + request.Tiling.Spacing);
            graphics.DrawImage(tile, x, y, request.Width, request.Height);
        }
    }

    canvas.Save(outputFile, request.Format switch
    {
        IconRenderFormat.Png => ImageFormat.Png,
        IconRenderFormat.Jpeg => ImageFormat.Jpeg,
        IconRenderFormat.Bmp => ImageFormat.Bmp,
        _ => throw new ArgumentOutOfRangeException(nameof(request.Format))
    });
    return outputFile;
}

static string ResolveOutlineColor(IconOutlineOptions outline, string foregroundColor, string backgroundColor) =>
    outline.ColorSource switch
    {
        IconOutlineColorSource.Foreground => foregroundColor,
        IconOutlineColorSource.Background => backgroundColor,
        IconOutlineColorSource.Custom => outline.CustomColor,
        _ => throw new ArgumentOutOfRangeException(nameof(outline.ColorSource))
    };

static IconBorderColor ToLibraryBorderSource(IconOutlineColorSource source) => source switch
{
    IconOutlineColorSource.Foreground => IconBorderColor.Foreground,
    IconOutlineColorSource.Background => IconBorderColor.Background,
    IconOutlineColorSource.Custom => IconBorderColor.Custom,
    _ => throw new ArgumentOutOfRangeException(nameof(source))
};

static void ValidateOutlineOptions(IconOutlineOptions outline, string parameterName)
{
    if (outline.Width < 0 || !double.IsFinite(outline.Width))
        throw new ArgumentOutOfRangeException(parameterName, "Outline width must be finite and non-negative.");
    if (!Enum.IsDefined(outline.ColorSource))
        throw new ArgumentOutOfRangeException(parameterName, "Outline color source is not supported.");
    if (outline.Width > 0 && outline.ColorSource == IconOutlineColorSource.Custom)
        ArgumentException.ThrowIfNullOrWhiteSpace(outline.CustomColor);
}
