using System.Drawing.Imaging;
using System.Text.Json;

namespace helengine.vfx.captions;

/// <summary>Streams PNG RGBA frames to a new folder without loading a whole video into memory.</summary>
public static class CaptionSequenceExporter {
    /// <summary>Writes zero-based numbered PNGs and a manifest; cancellation preserves completed frames for inspection.</summary>
    public static int Export(CaptionDocument document, CaptionStyle style, CaptionExportOptions options, string outputFolder,
        IProgress<int> progress = null, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        CaptionStyle snapshot = style.Copy();
        var settings = new CaptionExportOptions { Width = options.Width, Height = options.Height, Fps = options.Fps, Duration = options.Duration };
        int frameCount = settings.GetFrameCount(document);
        using var renderer = new CaptionRenderer(document, snapshot);
        string destination = Path.GetFullPath(outputFolder);
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) {
            throw new IOException("Choose a new or empty output folder; caption export does not overwrite files.");
        }
        Directory.CreateDirectory(destination);
        // CreateNew reserves this folder against simultaneous caption exports. The final manifest only exists on success.
        string markerPath = Path.Combine(destination, "export-in-progress.json");
        using (var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            JsonSerializer.Serialize(marker, new { Status = "incomplete", settings.Width, settings.Height, settings.Fps, ExpectedFrameCount = frameCount });
        }
        for (int index = 0; index < frameCount; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            using Bitmap frame = renderer.Render(index / settings.Fps, settings.Width, settings.Height);
            string path = Path.Combine(destination, $"caption.{index:D6}.png");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                frame.Save(stream, ImageFormat.Png);
            }
            progress?.Report((int)((index + 1L) * 100 / frameCount));
        }
        cancellationToken.ThrowIfCancellationRequested();
        string manifestPath = Path.Combine(destination, "sequence.json");
        using (var manifest = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            JsonSerializer.Serialize(manifest, new {
                Version = 1,
                Format = "png",
                PixelFormat = "RGBA",
                Alpha = "straight",
                FramePattern = "caption.%06d.png",
                StartFrame = 0,
                StartTime = 0,
                FrameCount = frameCount,
                settings.Width,
                settings.Height,
                settings.Fps,
                Duration = frameCount / settings.Fps,
                CaptionEnd = document.Duration,
                HasWordTimestamps = document.Cues.Any(cue => cue.Words.Count > 0),
                Style = snapshot
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        File.Delete(markerPath);
        return frameCount;
    }
}
