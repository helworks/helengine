using System.Text.Json;

namespace helengine.vfx.captions;

/// <summary>Persists reusable styles while resolving font assets relative to their preset file.</summary>
public static class CaptionStyleFile {
    /// <summary>Loads and validates a style, resolving relative texture/font paths.</summary>
    public static CaptionStyle Read(string path) {
        CaptionStyle style = JsonSerializer.Deserialize<CaptionStyle>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new FormatException("Caption preset is empty.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(style.FontFile)) {
            style.FontFile = Path.GetFullPath(style.FontFile, directory);
        }
        if (!string.IsNullOrWhiteSpace(style.AtlasFile)) {
            style.AtlasFile = Path.GetFullPath(style.AtlasFile, directory);
        }
        style.Validate();
        return style;
    }

    /// <summary>Saves an editable JSON preset with readable indentation.</summary>
    public static void Write(CaptionStyle style, string path) {
        ArgumentNullException.ThrowIfNull(style);
        style.Validate();
        File.WriteAllText(path, JsonSerializer.Serialize(style, new JsonSerializerOptions { WriteIndented = true }));
    }
}
