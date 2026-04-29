using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Reads and writes the installed engine-platform binding manifest stored under the managed toolchain root.
/// </summary>
public sealed class InstalledBindingStore {
    /// <summary>
    /// Gets the JSON serializer options used for the binding manifest file.
    /// </summary>
    static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Stores the managed shared toolchain root that owns the binding manifest.
    /// </summary>
    string SharedToolchainRoot { get; }

    /// <summary>
    /// Gets the absolute binding manifest file path under the managed shared toolchain root.
    /// </summary>
    public string ManifestFilePath { get; }

    /// <summary>
    /// Initializes one installed-binding store for the supplied toolchain root.
    /// </summary>
    /// <param name="sharedToolchainRoot">Managed shared toolchain root that contains the binding manifest.</param>
    public InstalledBindingStore(string sharedToolchainRoot) {
        SharedToolchainRoot = sharedToolchainRoot;
        ManifestFilePath = Path.Combine(sharedToolchainRoot, "installed-bindings.json");
    }

    /// <summary>
    /// Loads the installed engine-platform bindings from disk.
    /// </summary>
    /// <returns>Installed engine-platform bindings currently stored under the managed toolchain root.</returns>
    public IReadOnlyList<InstalledEnginePlatformBinding> Load() {
        if (!File.Exists(ManifestFilePath)) {
            return Array.Empty<InstalledEnginePlatformBinding>();
        }

        string json = File.ReadAllText(ManifestFilePath);
        InstalledBindingManifest manifest = JsonSerializer.Deserialize<InstalledBindingManifest>(json, JsonSerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize installed binding manifest at {ManifestFilePath}.");
        return manifest.Bindings;
    }

    /// <summary>
    /// Saves the supplied installed engine-platform bindings to disk.
    /// </summary>
    /// <param name="bindings">Installed engine-platform bindings that should be persisted.</param>
    public void Save(IEnumerable<InstalledEnginePlatformBinding> bindings) {
        Directory.CreateDirectory(SharedToolchainRoot);
        InstalledBindingManifest manifest = new InstalledBindingManifest {
            Bindings = bindings.ToList(),
            LastUpdated = DateTime.UtcNow
        };
        string json = JsonFormatting.SerializeWithIndent(manifest, JsonSerializerOptions);
        File.WriteAllText(ManifestFilePath, json);
    }
}
