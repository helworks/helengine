using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Reads and writes the shared-artifact manifest stored under the managed toolchain root.
/// </summary>
public sealed class InstalledArtifactStore {
    /// <summary>
    /// Gets the JSON serializer options used for the artifact manifest file.
    /// </summary>
    static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Stores the managed shared toolchain root that owns the artifact manifest.
    /// </summary>
    string SharedToolchainRoot { get; }

    /// <summary>
    /// Gets the absolute artifact manifest file path under the managed shared toolchain root.
    /// </summary>
    public string ManifestFilePath { get; }

    /// <summary>
    /// Initializes one installed-artifact store for the supplied toolchain root.
    /// </summary>
    /// <param name="sharedToolchainRoot">Managed shared toolchain root that contains the artifact manifest.</param>
    public InstalledArtifactStore(string sharedToolchainRoot) {
        SharedToolchainRoot = sharedToolchainRoot;
        ManifestFilePath = Path.Combine(sharedToolchainRoot, "installed-artifacts.json");
    }

    /// <summary>
    /// Loads the installed shared artifacts from disk.
    /// </summary>
    /// <returns>Installed shared artifacts currently stored under the managed toolchain root.</returns>
    public IReadOnlyList<InstalledArtifact> Load() {
        if (!File.Exists(ManifestFilePath)) {
            return Array.Empty<InstalledArtifact>();
        }

        string json = File.ReadAllText(ManifestFilePath);
        InstalledArtifactManifest manifest = JsonSerializer.Deserialize<InstalledArtifactManifest>(json, JsonSerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize installed artifact manifest at {ManifestFilePath}.");
        return manifest.Artifacts;
    }

    /// <summary>
    /// Saves the supplied installed shared artifacts to disk.
    /// </summary>
    /// <param name="artifacts">Installed shared artifacts that should be persisted.</param>
    public void Save(IEnumerable<InstalledArtifact> artifacts) {
        Directory.CreateDirectory(SharedToolchainRoot);
        InstalledArtifactManifest manifest = new InstalledArtifactManifest {
            Artifacts = artifacts.ToList(),
            LastUpdated = DateTime.UtcNow
        };
        string json = JsonFormatting.SerializeWithIndent(manifest, JsonSerializerOptions);
        File.WriteAllText(ManifestFilePath, json);
    }
}
