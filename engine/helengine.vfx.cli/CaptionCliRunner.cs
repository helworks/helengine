using System.Globalization;
using helengine.vfx.captions;

namespace helengine.vfx.cli;

/// <summary>Handles the caption subcommand alongside the existing shader effect export commands.</summary>
public static class CaptionCliRunner {
    /// <summary>Imports a transcript, resolves a preset and streams a transparent sequence.</summary>
    public static int Run(string[] args) {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal)) {
            Console.WriteLine("helengine.vfx.cli captions --input <srt|json> --out <empty-folder> [--preset <0..5>] [--style <json>] [--width 1080] [--height 1920] [--fps 30] [--duration seconds] [--font <ttf|otf>] [--atlas <json>]");
            IReadOnlyList<CaptionStyle> presets = CaptionPresets.Create();
            for (int index = 0; index < presets.Count; index++) {
                Console.WriteLine($"  {index}: {presets[index].Name}");
            }
            return 0;
        }
        try {
            Dictionary<string, string> values = ParseOptions(args);
            if (!values.TryGetValue("--input", out string input) || !values.TryGetValue("--out", out string output)) {
                throw new ArgumentException("Captions require --input <srt|json> and --out <empty-folder>.");
            }
            IReadOnlyList<CaptionStyle> presets = CaptionPresets.Create();
            int presetIndex = values.TryGetValue("--preset", out string presetValue) ? int.Parse(presetValue, CultureInfo.InvariantCulture) : 0;
            if (presetIndex < 0 || presetIndex >= presets.Count) {
                throw new ArgumentException("Preset index must be 0..5; run 'captions --help' to list styles.");
            }
            CaptionStyle style = values.TryGetValue("--style", out string stylePath) ? CaptionStyleFile.Read(stylePath) : presets[presetIndex];
            if (values.TryGetValue("--font", out string font)) {
                style.FontFile = Path.GetFullPath(font);
                style.AtlasFile = null;
            }
            if (values.TryGetValue("--atlas", out string atlas)) {
                style.AtlasFile = Path.GetFullPath(atlas);
            }
            var options = new CaptionExportOptions();
            if (values.TryGetValue("--width", out string width)) {
                options.Width = int.Parse(width, CultureInfo.InvariantCulture);
            }
            if (values.TryGetValue("--height", out string height)) {
                options.Height = int.Parse(height, CultureInfo.InvariantCulture);
            }
            if (values.TryGetValue("--fps", out string fps)) {
                options.Fps = double.Parse(fps, CultureInfo.InvariantCulture);
            }
            if (values.TryGetValue("--duration", out string duration)) {
                options.Duration = double.Parse(duration, CultureInfo.InvariantCulture);
            }
            CaptionDocument document = CaptionImporter.Read(input);
            int count = CaptionSequenceExporter.Export(document, style, options, output);
            Console.WriteLine($"Wrote {count} transparent PNG frames to '{Path.GetFullPath(output)}'.");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is IOException || exception is System.Text.Json.JsonException || exception is OverflowException) {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>Rejects unknown, missing and repeated arguments before allocating renderer resources.</summary>
    static Dictionary<string, string> ParseOptions(string[] args) {
        var allowed = new HashSet<string>(new[] { "--input", "--out", "--preset", "--style", "--width", "--height", "--fps", "--duration", "--font", "--atlas" }, StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2) {
            if (!allowed.Contains(args[index])) {
                throw new ArgumentException($"Unknown caption option '{args[index]}'.");
            }
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[index], args[index + 1])) {
                throw new ArgumentException($"Caption option '{args[index]}' needs one value and cannot be repeated.");
            }
        }
        return values;
    }
}
