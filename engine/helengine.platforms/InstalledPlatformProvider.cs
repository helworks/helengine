namespace helengine.platforms;

/// <summary>
/// Loads available platforms from one launcher-style shared toolchain root.
/// </summary>
public sealed class InstalledPlatformProvider : IAvailablePlatformProvider {
    /// <summary>
    /// Stores the shared toolchain root that should be queried for installed bindings.
    /// </summary>
    string SharedToolchainRootPath { get; }

    /// <summary>
    /// Initializes one installed-platform provider for the supplied shared toolchain root.
    /// </summary>
    /// <param name="sharedToolchainRootPath">Shared toolchain root that contains the installed-binding manifest.</param>
    public InstalledPlatformProvider(string sharedToolchainRootPath) {
        SharedToolchainRootPath = sharedToolchainRootPath ?? string.Empty;
    }

    /// <summary>
    /// Attempts to load the available platforms for the supplied engine version from the installed-binding manifest.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <param name="platforms">Resolved platforms when installed-binding state exists.</param>
    /// <returns><c>true</c> when installed-binding state exists; otherwise <c>false</c>.</returns>
    public bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms) {
        platforms = Array.Empty<AvailablePlatformDescriptor>();

        if (string.IsNullOrWhiteSpace(SharedToolchainRootPath)) {
            return false;
        }

        InstalledBindingStore store = new InstalledBindingStore(SharedToolchainRootPath);
        if (!store.Exists()) {
            return false;
        }

        InstalledBindingManifest manifest = store.Load();
        platforms = BuildMatchingPlatforms(engineVersion, manifest.Bindings);
        return true;
    }

    /// <summary>
    /// Builds the ordered distinct available-platform list for one engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose bindings should be filtered.</param>
    /// <param name="bindings">Bindings loaded from the installed-binding manifest.</param>
    /// <returns>Ordered distinct platforms matching the supplied engine version.</returns>
    static IReadOnlyList<AvailablePlatformDescriptor> BuildMatchingPlatforms(string engineVersion, IReadOnlyList<InstalledEnginePlatformBinding> bindings) {
        List<AvailablePlatformDescriptor> platforms = new();
        HashSet<string> seenPlatformIds = new(StringComparer.Ordinal);

        foreach (InstalledEnginePlatformBinding binding in bindings) {
            if (!string.Equals(binding.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                continue;
            }

            if (!seenPlatformIds.Add(binding.PlatformId)) {
                continue;
            }

            platforms.Add(new AvailablePlatformDescriptor(binding.PlatformId, binding.PlatformId));
        }

        return platforms;
    }
}
