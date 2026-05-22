using System.Text.Json;

namespace helengine.platforms;

/// <summary>
/// Loads and validates one metadata-only external platform plugin manifest.
/// </summary>
public sealed class PlatformPluginManifestDocument {
    /// <summary>
    /// Initializes one validated plugin manifest document.
    /// </summary>
    /// <param name="platformId">Stable platform identifier declared by the plugin.</param>
    /// <param name="displayName">Readable platform name declared by the plugin.</param>
    /// <param name="builderAssemblyPath">Builder assembly path declared by the plugin when provided.</param>
    public PlatformPluginManifestDocument(string platformId, string displayName, string builderAssemblyPath) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Platform display name is required.", nameof(displayName));
        }

        PlatformId = platformId;
        DisplayName = displayName;
        BuilderAssemblyPath = builderAssemblyPath ?? string.Empty;
    }

    /// <summary>
    /// Gets the stable platform identifier declared by the plugin.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the readable platform name declared by the plugin.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the builder assembly path declared by the plugin when provided.
    /// </summary>
    public string BuilderAssemblyPath { get; }

    /// <summary>
    /// Loads and validates one metadata-only platform plugin manifest from disk.
    /// </summary>
    /// <param name="manifestFilePath">Absolute plugin-manifest file path.</param>
    /// <returns>Validated plugin manifest document.</returns>
    public static PlatformPluginManifestDocument Load(string manifestFilePath) {
        if (string.IsNullOrWhiteSpace(manifestFilePath)) {
            throw new ArgumentException("Plugin manifest path must be provided.", nameof(manifestFilePath));
        }
        if (!File.Exists(manifestFilePath)) {
            throw new FileNotFoundException($"Platform plugin manifest '{manifestFilePath}' was not found.", manifestFilePath);
        }

        using FileStream stream = File.OpenRead(manifestFilePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement rootElement = document.RootElement;
        if (rootElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException($"Platform plugin manifest '{manifestFilePath}' must contain one JSON object root.");
        }
        if (rootElement.TryGetProperty("runtimePayloadTypes", out _)) {
            throw new InvalidOperationException("External platform plugin manifests must not declare runtime payload CLR types.");
        } else if (rootElement.TryGetProperty("serializerHooks", out _)) {
            throw new InvalidOperationException("External platform plugin manifests must not declare serializer hooks into helengine.");
        }

        string platformId = rootElement.GetProperty("platformId").GetString() ?? throw new InvalidOperationException($"Platform plugin manifest '{manifestFilePath}' must declare platformId.");
        string displayName = rootElement.GetProperty("displayName").GetString() ?? throw new InvalidOperationException($"Platform plugin manifest '{manifestFilePath}' must declare displayName.");
        string builderAssemblyPath = rootElement.TryGetProperty("builderAssemblyPath", out JsonElement builderAssemblyPathElement)
            ? builderAssemblyPathElement.GetString() ?? string.Empty
            : string.Empty;

        return new PlatformPluginManifestDocument(platformId, displayName, builderAssemblyPath);
    }
}
