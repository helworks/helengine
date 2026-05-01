using System.Text.Json;

namespace helengine.platforms;

/// <summary>
/// Reads one platform repository descriptor from a per-platform JSON file.
/// </summary>
public sealed class PlatformDescriptorStore {
    /// <summary>
    /// Gets the absolute descriptor file path.
    /// </summary>
    public string DescriptorFilePath { get; }

    /// <summary>
    /// Initializes one platform descriptor store for the supplied descriptor file.
    /// </summary>
    /// <param name="descriptorFilePath">Absolute or relative descriptor file path.</param>
    public PlatformDescriptorStore(string descriptorFilePath) {
        if (string.IsNullOrWhiteSpace(descriptorFilePath)) {
            throw new ArgumentException("Descriptor file path is required.", nameof(descriptorFilePath));
        }

        DescriptorFilePath = Path.GetFullPath(descriptorFilePath);
    }

    /// <summary>
    /// Determines whether the descriptor file exists.
    /// </summary>
    /// <returns><c>true</c> when the descriptor file exists; otherwise <c>false</c>.</returns>
    public bool Exists() {
        return File.Exists(DescriptorFilePath);
    }

    /// <summary>
    /// Loads the platform descriptor document from disk.
    /// </summary>
    /// <returns>Platform descriptor document stored in the file.</returns>
    public PlatformDescriptorDocument Load() {
        using FileStream stream = File.OpenRead(DescriptorFilePath);
        using JsonDocument document = JsonDocument.Parse(stream);

        string engineVersion = document.RootElement.GetProperty("engineVersion").GetString() ?? throw new InvalidOperationException($"Platform descriptor at {DescriptorFilePath} contains a missing engineVersion.");
        string platformId = document.RootElement.GetProperty("platformId").GetString() ?? throw new InvalidOperationException($"Platform descriptor at {DescriptorFilePath} contains a missing platformId.");
        string displayName = document.RootElement.GetProperty("displayName").GetString() ?? throw new InvalidOperationException($"Platform descriptor at {DescriptorFilePath} contains a missing displayName.");
        string builderAssemblyPath = document.RootElement.GetProperty("builderAssemblyPath").GetString() ?? throw new InvalidOperationException($"Platform descriptor at {DescriptorFilePath} contains a missing builderAssemblyPath.");
        string playerSourceRootPath = document.RootElement.GetProperty("playerSourceRootPath").GetString() ?? throw new InvalidOperationException($"Platform descriptor at {DescriptorFilePath} contains a missing playerSourceRootPath.");

        return new PlatformDescriptorDocument(engineVersion, platformId, displayName, builderAssemblyPath, playerSourceRootPath);
    }
}
