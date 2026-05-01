using System.Text.Json;

namespace helengine.platforms;

/// <summary>
/// Reads one installation manifest that points at per-platform descriptor files.
/// </summary>
public sealed class PlatformInstallationStore {
    /// <summary>
    /// Stores the root directory that owns the installation manifest.
    /// </summary>
    string SharedToolchainRootPath { get; }

    /// <summary>
    /// Initializes one installation manifest store for the supplied root directory.
    /// </summary>
    /// <param name="sharedToolchainRootPath">Directory that owns the installation manifest file.</param>
    public PlatformInstallationStore(string sharedToolchainRootPath) {
        SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
    }

    /// <summary>
    /// Gets the absolute installation manifest file path.
    /// </summary>
    public string ManifestFilePath {
        get {
            return Path.Combine(SharedToolchainRootPath, "platforms.json");
        }
    }

    /// <summary>
    /// Determines whether the installation manifest exists.
    /// </summary>
    /// <returns><c>true</c> when the manifest exists; otherwise <c>false</c>.</returns>
    public bool Exists() {
        return File.Exists(ManifestFilePath);
    }

    /// <summary>
    /// Loads the installation manifest from disk.
    /// </summary>
    /// <returns>Installation manifest containing per-platform descriptor links.</returns>
    public PlatformInstallationManifest Load() {
        using FileStream stream = File.OpenRead(ManifestFilePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("platforms", out JsonElement platformsElement) || platformsElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} does not contain a platforms array.");
        }

        List<PlatformInstallationEntry> platforms = new();
        foreach (JsonElement platformElement in platformsElement.EnumerateArray()) {
            string platformDescriptorPath = platformElement.GetProperty("platformDescriptorPath").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without platformDescriptorPath.");
            platforms.Add(new PlatformInstallationEntry(platformDescriptorPath));
        }

        return new PlatformInstallationManifest(platforms);
    }
}
