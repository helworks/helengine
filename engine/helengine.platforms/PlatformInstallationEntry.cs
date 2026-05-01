namespace helengine.platforms;

/// <summary>
/// Describes one installed platform link pointing at a per-platform descriptor file.
/// </summary>
public sealed class PlatformInstallationEntry {
    /// <summary>
    /// Initializes one installation entry.
    /// </summary>
    /// <param name="platformDescriptorPath">Path to the per-platform descriptor file.</param>
    /// <exception cref="ArgumentException">Thrown when the descriptor path is missing.</exception>
    public PlatformInstallationEntry(string platformDescriptorPath) {
        if (string.IsNullOrWhiteSpace(platformDescriptorPath)) {
            throw new ArgumentException("Platform descriptor path is required.", nameof(platformDescriptorPath));
        }

        PlatformDescriptorPath = platformDescriptorPath;
    }

    /// <summary>
    /// Gets the path to the per-platform descriptor file.
    /// </summary>
    public string PlatformDescriptorPath { get; }
}
