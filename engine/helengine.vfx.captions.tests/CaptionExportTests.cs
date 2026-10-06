using System.Text.Json;
using helengine.vfx.cli;

namespace helengine.vfx.captions.tests;

/// <summary>Checks compositing metadata, silence alignment, output safety and the VFX caption command.</summary>
public sealed class CaptionExportTests {
    /// <summary>Exported numbered PNGs retain leading/trailing silence and a fractional-FPS manifest.</summary>
    [Fact]
    public void ExportPreservesAbsoluteTimelineAndAlpha() {
        using var workspace = new CaptionTestWorkspace();
        var document = new CaptionDocument(new[] { new CaptionCue("Olá", 0.5, 1) });
        string output = Path.Combine(workspace.DirectoryPath, "frames");
        int count = CaptionSequenceExporter.Export(document, CaptionRendererTests.SmallStyle(),
            new CaptionExportOptions { Width = 320, Height = 180, Fps = 4, Duration = 1.5 }, output);
        Assert.Equal(6, count);
        Assert.Equal(6, Directory.GetFiles(output, "*.png").Length);
        using var first = new Bitmap(Path.Combine(output, "caption.000000.png"));
        using var spoken = new Bitmap(Path.Combine(output, "caption.000002.png"));
        using var trailing = new Bitmap(Path.Combine(output, "caption.000004.png"));
        Assert.False(CaptionRendererTests.HasVisiblePixels(first));
        Assert.True(CaptionRendererTests.HasVisiblePixels(spoken));
        Assert.False(CaptionRendererTests.HasVisiblePixels(trailing));
        Assert.Equal(0, spoken.GetPixel(0, 0).A);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "sequence.json")));
        Assert.Equal(6, manifest.RootElement.GetProperty("FrameCount").GetInt32());
        Assert.Equal("straight", manifest.RootElement.GetProperty("Alpha").GetString());
        Assert.Equal(4, manifest.RootElement.GetProperty("Fps").GetDouble());
        Assert.False(File.Exists(Path.Combine(output, "export-in-progress.json")));
    }

    /// <summary>Existing destination data is never replaced and a pre-cancelled export creates no output.</summary>
    [Fact]
    public void ExportRejectsOverwriteAndHonorsCancellation() {
        using var workspace = new CaptionTestWorkspace();
        var document = new CaptionDocument(new[] { new CaptionCue("caption", 0, 1) });
        File.WriteAllText(Path.Combine(workspace.DirectoryPath, "keep.txt"), "user file");
        Assert.Throws<IOException>(() => CaptionSequenceExporter.Export(document, CaptionRendererTests.SmallStyle(), new CaptionExportOptions(), workspace.DirectoryPath));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(workspace.DirectoryPath, "keep.txt")));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string output = Path.Combine(workspace.DirectoryPath, "cancelled");
        Assert.Throws<OperationCanceledException>(() => CaptionSequenceExporter.Export(document, CaptionRendererTests.SmallStyle(), new CaptionExportOptions(), output, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(output));
    }

    /// <summary>Cancellation during a run leaves one inspectable frame and never claims a completed sequence.</summary>
    [Fact]
    public void CancelledExportKeepsPartialStatusWithoutFinalManifest() {
        using var workspace = new CaptionTestWorkspace();
        using var cancellation = new CancellationTokenSource();
        var document = new CaptionDocument(new[] { new CaptionCue("caption", 0, 1) });
        string output = Path.Combine(workspace.DirectoryPath, "partial");
        Assert.Throws<OperationCanceledException>(() => CaptionSequenceExporter.Export(document, CaptionRendererTests.SmallStyle(),
            new CaptionExportOptions { Width = 320, Height = 180, Fps = 4 }, output, new CaptionCancelProgress(cancellation), cancellation.Token));
        Assert.Single(Directory.GetFiles(output, "*.png"));
        Assert.True(File.Exists(Path.Combine(output, "export-in-progress.json")));
        Assert.False(File.Exists(Path.Combine(output, "sequence.json")));
    }

    /// <summary>Frame planning retains fractional rates and rejects infinite or excessive allocations.</summary>
    [Fact]
    public void FramePlanningValidatesFractionalRatesAndSizes() {
        var document = new CaptionDocument(new[] { new CaptionCue("caption", 0, 1) });
        Assert.Equal(30, new CaptionExportOptions { Fps = 29.97 }.GetFrameCount(document));
        Assert.Throws<ArgumentException>(() => new CaptionExportOptions { Fps = double.NaN }.GetFrameCount(document));
        Assert.Throws<ArgumentException>(() => new CaptionExportOptions { Duration = double.PositiveInfinity }.GetFrameCount(document));
        Assert.Throws<ArgumentException>(() => new CaptionExportOptions { Duration = 1000000 }.GetFrameCount(document));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaptionExportOptions { Width = 99999 }.GetFrameCount(document));
    }

    /// <summary>The caption command supports JSON presets and returns failure for unknown/missing options.</summary>
    [Fact]
    public void CaptionCommandRunsAnActualTransparentExport() {
        using var workspace = new CaptionTestWorkspace();
        string transcript = Path.Combine(workspace.DirectoryPath, "caption.srt");
        File.WriteAllText(transcript, "1\n00:00:00,000 --> 00:00:01,000\nOlá mundo");
        string preset = Path.Combine(workspace.DirectoryPath, "style.json");
        CaptionStyleFile.Write(CaptionRendererTests.SmallStyle(), preset);
        string output = Path.Combine(workspace.DirectoryPath, "cli-frames");
        Assert.Equal(0, CaptionCliRunner.Run(new[] { "--input", transcript, "--out", output, "--style", preset, "--width", "320", "--height", "180", "--fps", "2" }));
        Assert.Equal(2, Directory.GetFiles(output, "*.png").Length);
        Assert.Equal(1, CaptionCliRunner.Run(new[] { "--unknown", "value" }));
        Assert.Equal(1, CaptionCliRunner.Run(new[] { "--input" }));
        Assert.Equal(1, CaptionCliRunner.Run(new[] { "--input", transcript, "--out", output, "--preset", "100" }));
    }

    /// <summary>Saved presets resolve relative font assets against the preset's directory.</summary>
    [Fact]
    public void PresetFileResolvesAssetsAndRejectsInvalidColors() {
        using var workspace = new CaptionTestWorkspace();
        CaptionStyle style = CaptionRendererTests.SmallStyle();
        style.AtlasFile = "font.json";
        string path = Path.Combine(workspace.DirectoryPath, "preset.json");
        CaptionStyleFile.Write(style, path);
        Assert.Equal(Path.Combine(workspace.DirectoryPath, "font.json"), CaptionStyleFile.Read(path).AtlasFile);
        style.TextColor = "yellow";
        Assert.Throws<FormatException>(() => style.Validate());
    }
}
