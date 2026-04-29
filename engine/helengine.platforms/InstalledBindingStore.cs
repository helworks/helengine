using System.Text.Json;

namespace helengine.platforms;

/// <summary>
/// Reads the launcher-style installed-binding manifest from one shared toolchain root.
/// </summary>
public sealed class InstalledBindingStore {
    /// <summary>
    /// Stores the shared toolchain root that owns the installed-binding manifest.
    /// </summary>
    string SharedToolchainRootPath { get; }

    /// <summary>
    /// Initializes one installed-binding store for the supplied shared toolchain root.
    /// </summary>
    /// <param name="sharedToolchainRootPath">Shared toolchain root that owns the installed-binding manifest.</param>
    public InstalledBindingStore(string sharedToolchainRootPath) {
        SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
    }

    /// <summary>
    /// Gets the absolute installed-binding manifest file path under the shared toolchain root.
    /// </summary>
    public string ManifestFilePath {
        get {
            return Path.Combine(SharedToolchainRootPath, "installed-bindings.json");
        }
    }

    /// <summary>
    /// Determines whether the installed-binding manifest exists under the shared toolchain root.
    /// </summary>
    /// <returns><c>true</c> when the manifest exists; otherwise <c>false</c>.</returns>
    public bool Exists() {
        return File.Exists(ManifestFilePath);
    }

    /// <summary>
    /// Loads the installed engine-platform bindings from disk.
    /// </summary>
    /// <returns>Installed engine-platform bindings stored in the manifest.</returns>
    public InstalledBindingManifest Load() {
        using FileStream stream = File.OpenRead(ManifestFilePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("bindings", out JsonElement bindingsElement) || bindingsElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException($"Installed binding manifest at {ManifestFilePath} does not contain a bindings array.");
        }

        List<InstalledEnginePlatformBinding> bindings = new();
        foreach (JsonElement bindingElement in bindingsElement.EnumerateArray()) {
            string engineVersion = bindingElement.GetProperty("engineVersion").GetString() ?? throw new InvalidOperationException($"Installed binding manifest at {ManifestFilePath} contains a binding without engineVersion.");
            string platformId = bindingElement.GetProperty("platformId").GetString() ?? throw new InvalidOperationException($"Installed binding manifest at {ManifestFilePath} contains a binding without platformId.");
            bindings.Add(new InstalledEnginePlatformBinding(engineVersion, platformId));
        }

        return new InstalledBindingManifest(bindings);
    }
}
