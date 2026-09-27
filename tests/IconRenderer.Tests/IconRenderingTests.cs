using System.Drawing.Imaging;
using IconRenderer;
using Xunit;

namespace IconRenderer.Tests;

public sealed class IconRenderingTests
{
    [Fact]
    public void RenderSelectedIconsToPngFiles()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "rendered-icons");
        Directory.CreateDirectory(outputDirectory);

        var squareWithForegroundBorder = SvgIcon.WithSquareBackground(
            Icons.HeartPlus, "#34495e", "#ffffff", borderWidth: 12,
            borderColorSource: IconBorderColor.Foreground,
            backgroundBorderWidth: 8, backgroundBorderColorSource: IconBorderColor.Custom,
            customBackgroundBorderColor: "#f39c12");
        var circleWithBackgroundBorder = SvgIcon.WithCircularBackground(
            Icons.SkullWithSyringe, "#c0392b", "#ffffff", borderWidth: 12,
            borderColorSource: IconBorderColor.Background,
            backgroundBorderWidth: 8, backgroundBorderColorSource: IconBorderColor.Custom,
            customBackgroundBorderColor: "#2ecc71");
        var roundedWithCustomBorder = SvgIcon.WithRoundedBackground(
            Icons.Violin, "#2980b9", "#ffffff", cornerRadius: 72, borderWidth: 12,
            borderColorSource: IconBorderColor.Custom, customBorderColor: "#f1c40f",
            backgroundBorderWidth: 8, backgroundBorderColorSource: IconBorderColor.Background);

        Assert.Contains("stroke=\"#ffffff\"", squareWithForegroundBorder);
        Assert.Contains("stroke=\"#c0392b\"", circleWithBackgroundBorder);
        Assert.Contains("stroke=\"#f1c40f\"", roundedWithCustomBorder);
        Assert.Contains("stroke=\"#f39c12\"", squareWithForegroundBorder);
        Assert.Contains("stroke=\"#2ecc71\"", circleWithBackgroundBorder);
        Assert.Contains("stroke=\"#2980b9\"", roundedWithCustomBorder);

        WritePng(outputDirectory, "heart-plus-square.png", squareWithForegroundBorder);
        WritePng(outputDirectory, "skull-with-syringe-circle.png", circleWithBackgroundBorder);
        WritePng(outputDirectory, "violin-rounded.png", roundedWithCustomBorder);
        WritePng(outputDirectory, "dragon-head-transparent.png",
            SvgIcon.Recolor(Icons.DragonHead, "#27ae60"));

        var files = Directory.GetFiles(outputDirectory, "*.png");
        Assert.Equal(4, files.Length);
        Assert.All(files, file => Assert.True(new FileInfo(file).Length > 0, $"Expected rendered image: {file}"));
    }

    private static void WritePng(string outputDirectory, string fileName, string svg)
    {
        using var bitmap = SvgIcon.RenderSquareToBitmap(svg, 256);
        bitmap.Save(Path.Combine(outputDirectory, fileName), ImageFormat.Png);
    }
}
