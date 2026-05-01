using System.Text.Json;

namespace helengine.platforms;

/// <summary>
/// Reads one engine-level platform manifest that declares known platform payload roots.
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
    /// Loads the platform manifest from disk.
    /// </summary>
    /// <returns>Platform manifest containing platform entries.</returns>
    public PlatformInstallationManifest Load() {
        using FileStream stream = File.OpenRead(ManifestFilePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("platforms", out JsonElement platformsElement) || platformsElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} does not contain a platforms array.");
        }

        List<PlatformInstallationEntry> platforms = new();
        foreach (JsonElement platformElement in platformsElement.EnumerateArray()) {
            string engineVersion = platformElement.GetProperty("engineVersion").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without engineVersion.");
            string platformId = platformElement.GetProperty("platformId").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without platformId.");
            string displayName = platformElement.GetProperty("displayName").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without displayName.");
            string builderAssemblyPath = platformElement.TryGetProperty("builderAssemblyPath", out JsonElement builderAssemblyPathElement) ? builderAssemblyPathElement.GetString() ?? string.Empty : string.Empty;
            string playerSourceRootPath = platformElement.GetProperty("playerSourceRootPath").GetString() ?? throw new InvalidOperationException($"Installation manifest at {ManifestFilePath} contains a platform without playerSourceRootPath.");

            platforms.Add(new PlatformInstallationEntry(engineVersion, platformId, displayName, builderAssemblyPath, playerSourceRootPath));
        }

        return new PlatformInstallationManifest(platforms);
    }
}
