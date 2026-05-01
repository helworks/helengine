namespace helengine.platforms;

/// <summary>
/// Loads available platform descriptors from an installation manifest that links to per-platform descriptor files.
/// </summary>
public sealed class PlatformInstallationResolver {
    /// <summary>
    /// Stores the root directory that owns the installation manifest and its linked descriptor files.
    /// </summary>
    string SharedToolchainRootPath { get; }

    /// <summary>
    /// Initializes one installation resolver for the supplied shared toolchain root.
    /// </summary>
    /// <param name="sharedToolchainRootPath">Shared toolchain root that owns the installation manifest.</param>
    public PlatformInstallationResolver(string sharedToolchainRootPath) {
        SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
    }

    /// <summary>
    /// Attempts to load the available platforms for the supplied engine version from the installation manifest.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <param name="platforms">Resolved platforms when installation state exists.</param>
    /// <returns><c>true</c> when installation state exists; otherwise <c>false</c>.</returns>
    public bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms) {
        platforms = Array.Empty<AvailablePlatformDescriptor>();

        if (string.IsNullOrWhiteSpace(SharedToolchainRootPath)) {
            return false;
        }

        PlatformInstallationStore store = new PlatformInstallationStore(SharedToolchainRootPath);
        if (!store.Exists()) {
            return false;
        }

        PlatformInstallationManifest manifest = store.Load();
        platforms = BuildMatchingPlatforms(engineVersion, manifest.Platforms, SharedToolchainRootPath);
        return true;
    }

    /// <summary>
    /// Builds the ordered distinct available-platform list for one engine version from one installation manifest.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose descriptors should be filtered.</param>
    /// <param name="entries">Installation entries loaded from the manifest.</param>
    /// <param name="manifestRootPath">Directory that owns the installation manifest.</param>
    /// <returns>Ordered distinct platforms matching the supplied engine version.</returns>
    static IReadOnlyList<AvailablePlatformDescriptor> BuildMatchingPlatforms(
        string engineVersion,
        IReadOnlyList<PlatformInstallationEntry> entries,
        string manifestRootPath) {
        List<AvailablePlatformDescriptor> platforms = new(entries.Count);
        HashSet<string> seenPlatformIds = new(StringComparer.Ordinal);

        for (int index = 0; index < entries.Count; index++) {
            PlatformInstallationEntry entry = entries[index];
            string descriptorFilePath = ResolveDescriptorFilePath(manifestRootPath, entry.PlatformDescriptorPath);
            PlatformDescriptorStore descriptorStore = new PlatformDescriptorStore(descriptorFilePath);
            if (!descriptorStore.Exists()) {
                throw new InvalidOperationException($"Platform descriptor at {descriptorStore.DescriptorFilePath} does not exist.");
            }

            PlatformDescriptorDocument descriptor = descriptorStore.Load();
            if (!string.Equals(descriptor.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                continue;
            }

            if (!seenPlatformIds.Add(descriptor.PlatformId)) {
                continue;
            }

            string descriptorDirectoryPath = Path.GetDirectoryName(descriptorStore.DescriptorFilePath) ?? string.Empty;
            platforms.Add(new AvailablePlatformDescriptor(
                descriptor.PlatformId,
                descriptor.DisplayName,
                ResolvePayloadPath(descriptorDirectoryPath, descriptor.BuilderAssemblyPath),
                ResolvePayloadPath(descriptorDirectoryPath, descriptor.PlayerSourceRootPath)));
        }

        return platforms;
    }

    /// <summary>
    /// Resolves one descriptor file path relative to the manifest root when necessary.
    /// </summary>
    /// <param name="manifestRootPath">Directory that owns the installation manifest.</param>
    /// <param name="descriptorPath">Descriptor file path from the installation manifest.</param>
    /// <returns>Absolute descriptor file path.</returns>
    static string ResolveDescriptorFilePath(string manifestRootPath, string descriptorPath) {
        if (Path.IsPathRooted(descriptorPath)) {
            return Path.GetFullPath(descriptorPath);
        }

        return Path.GetFullPath(Path.Combine(manifestRootPath, descriptorPath));
    }

    /// <summary>
    /// Resolves one payload path relative to the descriptor file when the path is not already rooted.
    /// </summary>
    /// <param name="descriptorDirectoryPath">Directory that owns the platform descriptor file.</param>
    /// <param name="payloadPath">Payload path from the platform descriptor.</param>
    /// <returns>Absolute payload path.</returns>
    static string ResolvePayloadPath(string descriptorDirectoryPath, string payloadPath) {
        if (Path.IsPathRooted(payloadPath)) {
            return Path.GetFullPath(payloadPath);
        }

        return Path.GetFullPath(Path.Combine(descriptorDirectoryPath, payloadPath));
    }
}
