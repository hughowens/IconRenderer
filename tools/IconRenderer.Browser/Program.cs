using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using IconRenderer;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;

namespace IconRenderer.Browser;

internal static class Program
{
    private static void Main()
    {
var app = new IconBrowserApp();
app.Run();
    }
}

internal sealed class IconBrowserApp : IDisposable
{
    private readonly FieldInfo[] _iconFields = typeof(Icons)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(field => !field.IsLiteral && field.FieldType == typeof(string))
        .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    private readonly IReadOnlyList<IconPreset> _builtInPresets;
    private FieldInfo[] _filteredIconFields = [];
    private readonly HashSet<string> _multiSelection = new(StringComparer.Ordinal);
    private readonly List<IconPreset> _customPresets = [];
    private readonly Dictionary<string, Texture2D> _galleryTextures = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _galleryTextureLru = [];

    private string _filter = string.Empty;
    private string _selectedIcon = "HeartPlus";
    private string _activePresetName = "Manual selection";
    private string _newPresetName = string.Empty;
    private string _outputFolderPrefix = string.Empty;
    private string _status = "Ready";
    private Vector3 _foregroundColor = Vector3.One;
    private Vector3 _backgroundColor = new(0.16f, 0.22f, 0.30f);
    private Vector3 _canvasColor = new(0.10f, 0.11f, 0.14f);
    private Vector3 _customForegroundOutline = new(1f, 0.82f, 0.2f);
    private Vector3 _customBackgroundOutline = new(0.95f, 0.35f, 0.2f);
    private bool _useCanvasColor = true;
    private IconBackgroundShape _backgroundShape = IconBackgroundShape.Rounded;
    private IconBorderColor _foregroundOutlineSource = IconBorderColor.Foreground;
    private IconBorderColor _backgroundOutlineSource = IconBorderColor.Custom;
    private float _cornerRadius = 64;
    private float _foregroundOutlineWidth;
    private float _backgroundOutlineWidth = 8;
    private int _iconSize = 256;
    private int _columns = 1;
    private int _rows = 1;
    private int _spacing = 8;
    private int _padding = 16;
    private bool _previewDirty = true;
    private bool _scrollIconListToTop;
    private bool _imguiInitialized;
    private Texture2D _previewTexture;
    private PresetRenderQueue? _renderQueue;

    public IconBrowserApp()
    {
        _filteredIconFields = _iconFields;
        _builtInPresets = IconPresetCatalog.Create(_iconFields);
        LoadCustomPresets();
    }

    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(1440, 900, "Game Icons - Raylib + ImGui Browser");
        try
        {
            Raylib.SetWindowMinSize(900, 600);
            Raylib.SetTargetFPS(60);
            rlImGui.Setup(true);
            _imguiInitialized = true;

            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);

                rlImGui.Begin();
                DrawPresetToolbar();
                DrawIconList();
                DrawSettings();
                if (_previewDirty)
                    RefreshPreview();
                DrawPreview();
                rlImGui.End();

                ProcessRenderQueue();
                Raylib.EndDrawing();
            }
        }
        finally
        {
            Dispose();
            Raylib.CloseWindow();
        }
    }

    private void DrawPresetToolbar()
    {
        const float toolbarHeight = 72;
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Raylib.GetScreenWidth(), toolbarHeight), ImGuiCond.Always);
        if (!ImGui.Begin("Preset toolbar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                                                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("Preset");
        ImGui.SameLine();
        var names = GetPresetNames();
        var selectedIndex = Math.Max(0, names.IndexOf(_activePresetName));
        ImGui.SetNextItemWidth(250);
        if (ImGui.Combo("##active-preset", ref selectedIndex, names.ToArray(), names.Count))
            ApplyPreset(names[selectedIndex]);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(190);
        ImGui.InputText("##new-preset-name", ref _newPresetName, 128);
        ImGui.SameLine();
        if (ImGui.Button("Save selection"))
        {
            if (_multiSelection.Count == 0)
                _status = "Select one or more icons before saving a preset.";
            else
                SaveNamedPreset();
        }

        ImGui.SameLine();
        if (_renderQueue is null)
        {
            var renderCount = GetActivePresetIconNames().Count;
            if (ImGui.Button($"Render preset ({renderCount:N0})") && renderCount > 0)
                StartPresetRendering();
        }
        else if (ImGui.Button($"Cancel ({_renderQueue.Index}/{_renderQueue.IconNames.Length})"))
        {
            _renderQueue = null;
            _status = "Preset render cancelled.";
        }

        ImGui.SameLine();
        if (FindCustomPreset(_activePresetName) is not null && ImGui.Button("Delete preset"))
            DeleteActivePreset();

        ImGui.End();
    }

    private List<string> GetPresetNames() =>
        ["Manual selection", .. _builtInPresets.Select(preset => preset.Name), .. _customPresets.Select(preset => preset.Name)];

    private IconPreset? FindPreset(string name) =>
        _builtInPresets.FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? FindCustomPreset(name);

    private IconPreset? FindCustomPreset(string name) =>
        _customPresets.FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> GetActivePresetIconNames() =>
        FindPreset(_activePresetName)?.IconNames ?? _multiSelection.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    private void ApplyPreset(string presetName)
    {
        _activePresetName = presetName;
        var preset = FindPreset(presetName);
        if (preset is null)
        {
            RefreshFilteredIcons();
            return;
        }

        _multiSelection.Clear();
        foreach (var iconName in preset.IconNames)
            _multiSelection.Add(iconName);
        RefreshFilteredIcons();
        _scrollIconListToTop = true;
        if (_multiSelection.Count > 0)
        {
            _selectedIcon = _multiSelection.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).First();
            _previewDirty = true;
        }
    }

    private void SaveNamedPreset()
    {
        var name = _newPresetName.Trim();
        if (name.Length == 0)
        {
            _status = "Enter a name for the preset.";
            return;
        }
        if (_builtInPresets.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            _status = "That name is reserved for a built-in preset.";
            return;
        }

        var preset = new IconPreset(name,
            _multiSelection.OrderBy(icon => icon, StringComparer.OrdinalIgnoreCase).ToArray());
        var existingIndex = _customPresets.FindIndex(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            _customPresets[existingIndex] = preset;
        else
            _customPresets.Add(preset);

        _activePresetName = name;
        _newPresetName = string.Empty;
        if (PersistCustomPresets())
            _status = $"Saved preset '{name}' ({preset.IconNames.Count:N0} icons).";
    }

    private void DeleteActivePreset()
    {
        _customPresets.RemoveAll(preset => string.Equals(preset.Name, _activePresetName, StringComparison.OrdinalIgnoreCase));
        _activePresetName = "Manual selection";
        RefreshFilteredIcons();
        if (PersistCustomPresets())
            _status = "Deleted custom preset.";
    }

    private void LoadCustomPresets()
    {
        try
        {
            var path = GetPresetFilePath();
            if (!File.Exists(path))
                return;

            var saved = JsonSerializer.Deserialize<List<SavedIconPreset>>(File.ReadAllText(path)) ?? [];
            var knownIcons = _iconFields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var item in saved)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Name) ||
                    _builtInPresets.Any(preset => string.Equals(preset.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var icons = (item.IconNames ?? []).Where(knownIcons.Contains).Distinct(StringComparer.Ordinal).ToArray();
                if (icons.Length > 0)
                    _customPresets.Add(new IconPreset(item.Name, icons));
            }
        }
        catch (Exception exception)
        {
            _status = $"Could not load saved presets: {exception.Message}";
        }
    }

    private bool PersistCustomPresets()
    {
        try
        {
            var path = GetPresetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var saved = _customPresets.Select(preset => new SavedIconPreset(preset.Name, preset.IconNames.ToList())).ToArray();
            File.WriteAllText(path, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception exception)
        {
            _status = $"Could not save presets: {exception.Message}";
            return false;
        }
    }

    private static string GetPresetFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IconRenderer", "icon-presets.json");

    private void StartPresetRendering()
    {
        var icons = GetActivePresetIconNames().Distinct(StringComparer.Ordinal).ToArray();
        if (icons.Length == 0)
        {
            _status = "Select a preset or at least one icon to render.";
            return;
        }

        var presetName = _activePresetName == "Manual selection" ? "Manual selection" : _activePresetName;
        var folderPrefix = MakeSafeFolderPrefix(_outputFolderPrefix);
        var folderName = string.IsNullOrEmpty(folderPrefix)
            ? MakeSafeFolderName(presetName)
            : $"{folderPrefix}-{MakeSafeFolderName(presetName)}";
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "icon-renders", "presets", folderName);
        Directory.CreateDirectory(outputDirectory);
        _renderQueue = new PresetRenderQueue(presetName, icons, outputDirectory, CaptureRenderSettings());
        _status = $"Rendering {icons.Length:N0} icons from '{presetName}'…";
    }

    private void ProcessRenderQueue()
    {
        if (_renderQueue is not { } queue)
            return;

        try
        {
            var iconName = queue.IconNames[queue.Index];
            using var bitmap = RenderIconBitmap(iconName, queue.Settings);
            bitmap.Save(Path.Combine(queue.OutputDirectory, $"{iconName}.png"), ImageFormat.Png);
        }
        catch (Exception exception)
        {
            queue.FailureCount++;
            queue.LastError = exception.Message;
        }

        queue.Index++;
        if (queue.Index >= queue.IconNames.Length)
        {
            _renderQueue = null;
            _status = queue.FailureCount == 0
                ? $"Rendered {queue.IconNames.Length:N0} icons to {queue.OutputDirectory}"
                : $"Rendered {queue.IconNames.Length - queue.FailureCount:N0}/{queue.IconNames.Length:N0}; last error: {queue.LastError}";
        }
    }

    private static string MakeSafeFolderName(string name)
    {
        var safe = System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return safe.Length == 0 ? "preset" : safe;
    }

    private static string MakeSafeFolderPrefix(string prefix) =>
        System.Text.RegularExpressions.Regex.Replace(prefix.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private void DrawIconList()
    {
        const float listWidth = 280;
        const float toolbarHeight = 76;
        ImGui.SetNextWindowPos(new Vector2(0, toolbarHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(listWidth, Raylib.GetScreenHeight() - toolbarHeight), ImGuiCond.Always);
        if (!ImGui.Begin("Icons", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (ImGui.InputText("Search", ref _filter, 256))
            RefreshFilteredIcons();

        var matches = _filteredIconFields;
        ImGui.TextDisabled($"{_multiSelection.Count:N0} selected  |  {matches.Length:N0} of {_iconFields.Length:N0} icons");
        ImGui.TextDisabled("Check icons for a preset; click a name to preview.");
        ImGui.Separator();
        if (ImGui.BeginChild("icon-list"))
        {
            if (_scrollIconListToTop)
            {
                ImGui.SetScrollY(0);
                _scrollIconListToTop = false;
            }

            foreach (var field in matches)
            {
                ImGui.PushID(field.Name);
                var selectedForPreset = _multiSelection.Contains(field.Name);
                if (ImGui.Checkbox("##preset-selection", ref selectedForPreset))
                {
                    if (selectedForPreset)
                        _multiSelection.Add(field.Name);
                    else
                        _multiSelection.Remove(field.Name);
                    _activePresetName = "Manual selection";
                    RefreshFilteredIcons();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(field.Name, field.Name == _selectedIcon))
                {
                    _selectedIcon = field.Name;
                    _previewDirty = true;
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawSettings()
    {
        const float settingsWidth = 360;
        const float toolbarHeight = 76;
        var left = 284f;
        ImGui.SetNextWindowPos(new Vector2(left, toolbarHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(settingsWidth, Raylib.GetScreenHeight() - toolbarHeight), ImGuiCond.Always);
        if (!ImGui.Begin("Render settings", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.TextWrapped(_selectedIcon);
        ImGui.Separator();
        var changed = false;

        ImGui.Text("Foreground");
        changed |= ImGui.ColorEdit3("Icon color", ref _foregroundColor);
        changed |= ImGui.SliderInt("Icon size", ref _iconSize, 64, 768, "%d px");

        ImGui.Separator();
        ImGui.Text("Background");
        changed |= ComboEnum("Shape", ref _backgroundShape);
        if (_backgroundShape != IconBackgroundShape.None)
        {
            changed |= ImGui.ColorEdit3("Background color", ref _backgroundColor);
            if (_backgroundShape == IconBackgroundShape.Rounded)
                changed |= ImGui.SliderFloat("Corner radius", ref _cornerRadius, 0, 256, "%.0f");
        }

        ImGui.Separator();
        ImGui.Text("Foreground outline");
        changed |= ImGui.SliderFloat("Width##foreground", ref _foregroundOutlineWidth, 0, 32, "%.1f");
        changed |= ComboEnum("Color source##foreground", ref _foregroundOutlineSource);
        if (_foregroundOutlineSource == IconBorderColor.Custom)
            changed |= ImGui.ColorEdit3("Custom color##foreground", ref _customForegroundOutline);

        ImGui.Separator();
        ImGui.Text("Background outline");
        changed |= ImGui.SliderFloat("Width##background", ref _backgroundOutlineWidth, 0, 32, "%.1f");
        changed |= ComboEnum("Color source##background", ref _backgroundOutlineSource);
        if (_backgroundOutlineSource == IconBorderColor.Custom)
            changed |= ImGui.ColorEdit3("Custom color##background", ref _customBackgroundOutline);
        if (_backgroundShape == IconBackgroundShape.None && _backgroundOutlineWidth > 0)
            ImGui.TextDisabled("Choose a background shape to show its outline.");

        ImGui.Separator();
        ImGui.Text("Tiling");
        changed |= ImGui.SliderInt("Columns", ref _columns, 1, 8);
        changed |= ImGui.SliderInt("Rows", ref _rows, 1, 8);
        changed |= ImGui.SliderInt("Spacing", ref _spacing, 0, 64, "%d px");
        changed |= ImGui.SliderInt("Padding", ref _padding, 0, 64, "%d px");
        changed |= ImGui.Checkbox("Canvas color", ref _useCanvasColor);
        if (_useCanvasColor)
            changed |= ImGui.ColorEdit3("Canvas", ref _canvasColor);

        ImGui.InputText("Preset output folder prefix", ref _outputFolderPrefix, 64);
        if (ImGui.Button("Save PNG", new Vector2(-1, 32)))
            SaveCurrentPng();
        ImGui.TextWrapped(_status);
        if (changed)
        {
            _previewDirty = true;
            ClearGalleryTextureCache();
        }

        ImGui.End();
    }

    private void RefreshFilteredIcons()
    {
        var preset = FindPreset(_activePresetName);
        IEnumerable<string> preferredNames = preset is null ? _multiSelection : preset.IconNames;
        var preferred = preferredNames.ToHashSet(StringComparer.Ordinal);
        _filteredIconFields = _iconFields
            .Where(field => _filter.Length == 0 || field.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(field => preferred.Contains(field.Name))
            .ThenBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void DrawPreview()
    {
        const float toolbarHeight = 76;
        var previewX = 648f;
        var previewWidth = Math.Max(300, Raylib.GetScreenWidth() - previewX);
        ImGui.SetNextWindowPos(new Vector2(previewX, toolbarHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(previewWidth, Raylib.GetScreenHeight() - toolbarHeight), ImGuiCond.Always);
        if (!ImGui.Begin("Preview", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("preview-tabs"))
        {
            if (ImGui.BeginTabItem("Selected icon"))
            {
                DrawSelectedIconPreview();
                ImGui.EndTabItem();
            }

            var presetCount = GetActivePresetIconNames().Count;
            if (ImGui.BeginTabItem($"Preset preview ({presetCount:N0})"))
            {
                DrawPresetPreview();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
        ImGui.End();
    }

    private void DrawSelectedIconPreview()
    {
        ImGui.Text(_selectedIcon);
        ImGui.TextDisabled($"{_iconSize} px per tile  |  {_columns} × {_rows}");
        ImGui.Separator();
        if (_previewTexture.Id == 0)
            return;

        var available = ImGui.GetContentRegionAvail();
        var scale = Math.Min(available.X / _previewTexture.Width, (available.Y - 8) / _previewTexture.Height);
        scale = Math.Max(0.05f, scale);
        var width = Math.Max(1, (int)(_previewTexture.Width * scale));
        var height = Math.Max(1, (int)(_previewTexture.Height * scale));
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), (available.X - width) / 2));
        rlImGui.ImageSize(_previewTexture, width, height);
    }

    private void DrawPresetPreview()
    {
        var iconNames = GetActivePresetIconNames();
        ImGui.TextWrapped($"{_activePresetName} — {iconNames.Count:N0} icons using the current appearance settings");
        ImGui.TextDisabled("Preview only; no files are written. Select a thumbnail to open that icon in the single-icon tab.");
        ImGui.Separator();

        if (iconNames.Count == 0)
        {
            ImGui.TextDisabled("This preset has no icons. Select a preset or check icons in the list.");
            return;
        }

        if (!ImGui.BeginChild("preset-gallery"))
        {
            ImGui.EndChild();
            return;
        }

        const float cellWidth = 132;
        const float cellHeight = 128;
        const int thumbnailSize = 96;
        var available = ImGui.GetContentRegionAvail();
        var columnCount = Math.Max(1, Math.Min(_columns, (int)((available.X + _spacing) / (cellWidth + _spacing))));
        var rowCount = (iconNames.Count + columnCount - 1) / columnCount;
        var originX = ImGui.GetCursorPosX();
        var originY = ImGui.GetCursorPosY();
        var firstVisibleRow = Math.Max(0, (int)((ImGui.GetScrollY() - originY - _padding) / cellHeight));
        var visibleRowCount = Math.Max(1, (int)Math.Ceiling(available.Y / cellHeight) + 1);
        var lastVisibleRow = Math.Min(rowCount, firstVisibleRow + visibleRowCount);

        ImGui.SetCursorPosY(originY + _padding + firstVisibleRow * cellHeight);
        for (var row = firstVisibleRow; row < lastVisibleRow; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                var iconIndex = row * columnCount + column;
                if (iconIndex >= iconNames.Count)
                    break;

                var iconName = iconNames[iconIndex];
                var cellX = originX + _padding + column * (cellWidth + _spacing);
                var cellY = originY + _padding + row * cellHeight;
                ImGui.SetCursorPos(new Vector2(cellX, cellY));
                ImGui.PushID(iconName);
                try
                {
                    var texture = GetGalleryTexture(iconName, thumbnailSize);
                    rlImGui.ImageSize(texture, thumbnailSize, thumbnailSize);
                    if (ImGui.Selectable(iconName, iconName == _selectedIcon, ImGuiSelectableFlags.None,
                            new Vector2(cellWidth - 8, 20)))
                    {
                        _selectedIcon = iconName;
                        _previewDirty = true;
                    }
                }
                catch (Exception exception)
                {
                    ImGui.Dummy(new Vector2(thumbnailSize, thumbnailSize));
                    ImGui.TextDisabled("Preview error");
                    _status = $"Preset thumbnail '{iconName}' failed: {exception.Message}";
                }
                ImGui.PopID();
            }
        }

        ImGui.SetCursorPos(new Vector2(originX, originY + _padding * 2 + rowCount * cellHeight));
        ImGui.Dummy(new Vector2(1, 1));
        ImGui.EndChild();
    }

    private Texture2D GetGalleryTexture(string iconName, int thumbnailSize)
    {
        if (_galleryTextures.TryGetValue(iconName, out var cached))
        {
            _galleryTextureLru.Remove(iconName);
            _galleryTextureLru.AddLast(iconName);
            return cached;
        }

        var settings = CaptureRenderSettings() with
        {
            IconSize = thumbnailSize,
            Columns = 1,
            Rows = 1,
            Spacing = 0,
            Padding = 0
        };
        using var bitmap = RenderIconBitmap(iconName, settings);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var texture = CreateTextureFromPng(stream.ToArray());

        _galleryTextures.Add(iconName, texture);
        _galleryTextureLru.AddLast(iconName);
        while (_galleryTextureLru.Count > 72)
        {
            var oldest = _galleryTextureLru.First!.Value;
            _galleryTextureLru.RemoveFirst();
            Raylib.UnloadTexture(_galleryTextures[oldest]);
            _galleryTextures.Remove(oldest);
        }

        return texture;
    }

    private void ClearGalleryTextureCache()
    {
        foreach (var texture in _galleryTextures.Values)
            Raylib.UnloadTexture(texture);
        _galleryTextures.Clear();
        _galleryTextureLru.Clear();
    }

    private static Texture2D CreateTextureFromPng(byte[] pngData)
    {
        var image = Raylib.LoadImageFromMemory(".png", pngData);
        try
        {
            return Raylib.LoadTextureFromImage(image);
        }
        finally
        {
            Raylib.UnloadImage(image);
        }
    }

    private void RefreshPreview()
    {
        _previewDirty = false;
        try
        {
            using var bitmap = RenderCurrentBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            var texture = CreateTextureFromPng(stream.ToArray());
            if (_previewTexture.Id != 0)
                Raylib.UnloadTexture(_previewTexture);
            _previewTexture = texture;
            _status = $"Preview updated ({bitmap.Width} × {bitmap.Height})";
        }
        catch (Exception exception)
        {
            _status = $"Preview error: {exception.Message}";
        }
    }

    private BrowserRenderSettings CaptureRenderSettings() => new(
        ToHex(_foregroundColor), ToHex(_backgroundColor), ToHex(_canvasColor), _useCanvasColor,
        _backgroundShape, _foregroundOutlineSource, _backgroundOutlineSource,
        _cornerRadius, _foregroundOutlineWidth, _backgroundOutlineWidth,
        ToHex(_customForegroundOutline), ToHex(_customBackgroundOutline),
        _iconSize, _columns, _rows, _spacing, _padding);

    private Bitmap RenderCurrentBitmap() => RenderIconBitmap(_selectedIcon, CaptureRenderSettings());

    private static Bitmap RenderIconBitmap(string iconName, BrowserRenderSettings settings)
    {
        var field = typeof(Icons).GetField(iconName, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            ?? throw new ArgumentException($"Unknown icon '{iconName}'.", nameof(iconName));
        var svg = (string)field.GetValue(null)!;
        var foreground = settings.ForegroundColor;
        var background = settings.BackgroundColor;

        if (settings.BackgroundShape == IconBackgroundShape.None)
        {
            svg = SvgIcon.Recolor(svg, foreground);
            if (settings.ForegroundOutlineWidth > 0)
                svg = SvgIcon.WithForegroundOutline(svg, settings.ForegroundOutlineWidth, ResolveOutlineColor(
                    settings.ForegroundOutlineSource, foreground, background, settings.CustomForegroundOutline));
        }
        else
        {
            var foregroundOutlineColor = ResolveOutlineColor(
                settings.ForegroundOutlineSource, foreground, background, settings.CustomForegroundOutline);
            var backgroundOutlineColor = ResolveOutlineColor(
                settings.BackgroundOutlineSource, foreground, background, settings.CustomBackgroundOutline);
            svg = settings.BackgroundShape switch
            {
                IconBackgroundShape.Square => SvgIcon.WithSquareBackground(
                    svg, background, foreground, settings.ForegroundOutlineWidth, settings.ForegroundOutlineSource,
                    foregroundOutlineColor, settings.BackgroundOutlineWidth, settings.BackgroundOutlineSource, backgroundOutlineColor),
                IconBackgroundShape.Circular => SvgIcon.WithCircularBackground(
                    svg, background, foreground, settings.ForegroundOutlineWidth, settings.ForegroundOutlineSource,
                    foregroundOutlineColor, settings.BackgroundOutlineWidth, settings.BackgroundOutlineSource, backgroundOutlineColor),
                IconBackgroundShape.Rounded => SvgIcon.WithRoundedBackground(
                    svg, background, foreground, settings.CornerRadius, settings.ForegroundOutlineWidth,
                    settings.ForegroundOutlineSource, foregroundOutlineColor, settings.BackgroundOutlineWidth,
                    settings.BackgroundOutlineSource, backgroundOutlineColor),
                _ => svg
            };
        }

        using var icon = SvgIcon.RenderToBitmap(svg, settings.IconSize, settings.IconSize);
        var outputWidth = checked(settings.Padding * 2 + settings.Columns * settings.IconSize + (settings.Columns - 1) * settings.Spacing);
        var outputHeight = checked(settings.Padding * 2 + settings.Rows * settings.IconSize + (settings.Rows - 1) * settings.Spacing);
        var output = new Bitmap(outputWidth, outputHeight);
        using var graphics = Graphics.FromImage(output);
        graphics.Clear(settings.UseCanvasColor ? System.Drawing.ColorTranslator.FromHtml(settings.CanvasColor) : System.Drawing.Color.Transparent);
        for (var row = 0; row < settings.Rows; row++)
        for (var column = 0; column < settings.Columns; column++)
            graphics.DrawImage(icon, settings.Padding + column * (settings.IconSize + settings.Spacing),
                settings.Padding + row * (settings.IconSize + settings.Spacing));
        return output;
    }

    private void SaveCurrentPng()
    {
        try
        {
            var outputDirectory = Path.Combine(Environment.CurrentDirectory, "icon-renders");
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, $"{_selectedIcon}.png");
            using var bitmap = RenderIconBitmap(_selectedIcon, CaptureRenderSettings());
            bitmap.Save(path, ImageFormat.Png);
            _status = $"Saved {path}";
        }
        catch (Exception exception)
        {
            _status = $"Save error: {exception.Message}";
        }
    }

    private static string ResolveOutlineColor(IconBorderColor source, string foreground, string background, string custom) =>
        source switch
        {
            IconBorderColor.Foreground => foreground,
            IconBorderColor.Background => background,
            IconBorderColor.Custom => custom,
            _ => foreground
        };

    private static bool ComboEnum<TEnum>(string label, ref TEnum value) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var names = Enum.GetNames<TEnum>();
        var index = Array.IndexOf(values, value);
        if (ImGui.Combo(label, ref index, names, names.Length))
        {
            value = values[index];
            return true;
        }

        return false;
    }

    private static string ToHex(Vector3 color)
    {
        static int Channel(float value) => (int)Math.Clamp(MathF.Round(value * 255), 0, 255);
        return $"#{Channel(color.X):X2}{Channel(color.Y):X2}{Channel(color.Z):X2}";
    }

    private sealed record BrowserRenderSettings(
        string ForegroundColor,
        string BackgroundColor,
        string CanvasColor,
        bool UseCanvasColor,
        IconBackgroundShape BackgroundShape,
        IconBorderColor ForegroundOutlineSource,
        IconBorderColor BackgroundOutlineSource,
        float CornerRadius,
        float ForegroundOutlineWidth,
        float BackgroundOutlineWidth,
        string CustomForegroundOutline,
        string CustomBackgroundOutline,
        int IconSize,
        int Columns,
        int Rows,
        int Spacing,
        int Padding);

    private sealed record SavedIconPreset(string Name, List<string> IconNames);

    private sealed class PresetRenderQueue(
        string presetName,
        string[] iconNames,
        string outputDirectory,
        BrowserRenderSettings settings)
    {
        public string PresetName { get; } = presetName;
        public string[] IconNames { get; } = iconNames;
        public string OutputDirectory { get; } = outputDirectory;
        public BrowserRenderSettings Settings { get; } = settings;
        public int Index { get; set; }
        public int FailureCount { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        ClearGalleryTextureCache();
        if (_previewTexture.Id != 0)
        {
            Raylib.UnloadTexture(_previewTexture);
            _previewTexture = default;
        }

        if (_imguiInitialized)
        {
            rlImGui.Shutdown();
            _imguiInitialized = false;
        }
    }
}
