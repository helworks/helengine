namespace helengine.platforms;

/// <summary>
/// Loads available platform descriptors from one engine-level platform manifest.
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
    /// Attempts to load the available platforms for the supplied engine version from the platform manifest.
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
    /// Attempts to load one platform descriptor for the supplied engine version and platform id from the platform manifest.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose platform descriptor should be loaded.</param>
    /// <param name="platformId">Stable platform identifier to select.</param>
    /// <param name="platform">Resolved platform when installation state exists.</param>
    /// <returns><c>true</c> when the requested platform exists in the manifest; otherwise <c>false</c>.</returns>
    public bool TryLoadPlatform(string engineVersion, string platformId, out AvailablePlatformDescriptor platform) {
        platform = new AvailablePlatformDescriptor(string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(SharedToolchainRootPath)) {
            return false;
        }
        if (string.IsNullOrWhiteSpace(engineVersion)) {
            return false;
        }
        if (string.IsNullOrWhiteSpace(platformId)) {
            return false;
        }

        PlatformInstallationStore store = new PlatformInstallationStore(SharedToolchainRootPath);
        if (!store.Exists()) {
            return false;
        }

        PlatformInstallationManifest manifest = store.Load();
        for (int index = 0; index < manifest.Platforms.Count; index++) {
            PlatformInstallationEntry entry = manifest.Platforms[index];
            if (!string.Equals(entry.PlatformId, platformId, StringComparison.Ordinal)) {
                continue;
            }

            if (!string.Equals(entry.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                continue;
            }

            platform = BuildPlatformDescriptor(SharedToolchainRootPath, entry);
            return true;
        }

        return false;
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
            if (!string.Equals(entry.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                continue;
            }

            if (!seenPlatformIds.Add(entry.PlatformId)) {
                continue;
            }

            platforms.Add(BuildPlatformDescriptor(manifestRootPath, entry));
        }

        return platforms;
    }

    /// <summary>
    /// Builds one platform descriptor from one manifest entry.
    /// </summary>
    /// <param name="manifestRootPath">Directory that owns the installation manifest.</param>
    /// <param name="entry">Platform entry loaded from the manifest.</param>
    /// <returns>Resolved platform descriptor.</returns>
    static AvailablePlatformDescriptor BuildPlatformDescriptor(string manifestRootPath, PlatformInstallationEntry entry) {
        string resolvedPluginManifestPath = ResolvePayloadPath(manifestRootPath, entry.PluginManifestPath);
        PlatformPluginManifestDocument pluginManifest = LoadPluginManifest(resolvedPluginManifestPath);
        string resolvedBuilderAssemblyPath = ResolveBuilderAssemblyPath(manifestRootPath, entry, pluginManifest);
        string resolvedPlayerSourceRootPath = ResolvePayloadPath(manifestRootPath, entry.PlayerSourceRootPath);
        string resolvedGeneratedCoreCppRootPath = ResolvePayloadPath(manifestRootPath, entry.GeneratedCoreCppRootPath);
        string resolvedCodegenToolPath = ResolvePayloadPath(manifestRootPath, entry.CodegenToolPath);
        IReadOnlyList<string> resolvedGeneratedCoreProjectPaths = ResolveGeneratedCoreProjectPaths(manifestRootPath, entry, pluginManifest);
        bool isInstalled = IsInstalled(resolvedBuilderAssemblyPath, resolvedPlayerSourceRootPath, resolvedGeneratedCoreCppRootPath, resolvedCodegenToolPath);

        return new AvailablePlatformDescriptor(
            entry.PlatformId,
            entry.DisplayName,
            resolvedBuilderAssemblyPath,
            resolvedPlayerSourceRootPath,
            isInstalled,
            resolvedGeneratedCoreCppRootPath,
            resolvedCodegenToolPath,
            resolvedGeneratedCoreProjectPaths);
    }

    /// <summary>
    /// Loads one validated plugin manifest when the installation entry declares one.
    /// </summary>
    /// <param name="resolvedPluginManifestPath">Resolved plugin-manifest path from the installation entry.</param>
    /// <returns>Validated plugin manifest document when one is declared; otherwise <c>null</c>.</returns>
    static PlatformPluginManifestDocument LoadPluginManifest(string resolvedPluginManifestPath) {
        if (string.IsNullOrWhiteSpace(resolvedPluginManifestPath)) {
            return null;
        }

        return PlatformPluginManifestDocument.Load(resolvedPluginManifestPath);
    }

    /// <summary>
    /// Resolves the builder assembly path, preferring the installation entry and then the metadata-only plugin manifest.
    /// </summary>
    /// <param name="manifestRootPath">Directory that owns the installation manifest.</param>
    /// <param name="entry">Platform installation entry currently being resolved.</param>
    /// <param name="pluginManifest">Validated plugin manifest when one was declared.</param>
    /// <returns>Resolved builder assembly path.</returns>
    static string ResolveBuilderAssemblyPath(string manifestRootPath, PlatformInstallationEntry entry, PlatformPluginManifestDocument pluginManifest) {
        if (!string.IsNullOrWhiteSpace(entry.BuilderAssemblyPath)) {
            return ResolvePayloadPath(manifestRootPath, entry.BuilderAssemblyPath);
        }
        if (pluginManifest == null || string.IsNullOrWhiteSpace(pluginManifest.BuilderAssemblyPath)) {
            return string.Empty;
        }

        string pluginManifestDirectoryPath = Path.GetDirectoryName(ResolvePayloadPath(manifestRootPath, entry.PluginManifestPath)) ?? manifestRootPath;
        return ResolvePayloadPath(pluginManifestDirectoryPath, pluginManifest.BuilderAssemblyPath);
    }

    /// <summary>
    /// Resolves the generated-core managed project paths declared by one plugin manifest.
    /// </summary>
    /// <param name="manifestRootPath">Directory that owns the installation manifest.</param>
    /// <param name="entry">Platform installation entry currently being resolved.</param>
    /// <param name="pluginManifest">Validated plugin manifest when one was declared.</param>
    /// <returns>Resolved generated-core managed project paths.</returns>
    static IReadOnlyList<string> ResolveGeneratedCoreProjectPaths(string manifestRootPath, PlatformInstallationEntry entry, PlatformPluginManifestDocument pluginManifest) {
        if (pluginManifest == null || pluginManifest.GeneratedCoreProjectPaths.Count == 0) {
            return Array.Empty<string>();
        }

        string pluginManifestDirectoryPath = Path.GetDirectoryName(ResolvePayloadPath(manifestRootPath, entry.PluginManifestPath)) ?? manifestRootPath;
        List<string> resolvedProjectPaths = new(pluginManifest.GeneratedCoreProjectPaths.Count);
        for (int index = 0; index < pluginManifest.GeneratedCoreProjectPaths.Count; index++) {
            resolvedProjectPaths.Add(ResolvePayloadPath(pluginManifestDirectoryPath, pluginManifest.GeneratedCoreProjectPaths[index]));
        }

        return resolvedProjectPaths;
    }

    /// <summary>
    /// Resolves one payload path relative to the manifest root when the path is not already rooted.
    /// </summary>
    /// <param name="manifestRootPath">Directory that owns the platform manifest file.</param>
    /// <param name="payloadPath">Payload path from the platform descriptor.</param>
    /// <returns>Absolute payload path.</returns>
    static string ResolvePayloadPath(string manifestRootPath, string payloadPath) {
        if (string.IsNullOrWhiteSpace(payloadPath)) {
            return string.Empty;
        }
        if (Path.IsPathRooted(payloadPath)) {
            return Path.GetFullPath(payloadPath);
        }

        return Path.GetFullPath(Path.Combine(manifestRootPath, payloadPath));
    }

    /// <summary>
    /// Returns true when the resolved platform payload exists on disk.
    /// </summary>
    /// <param name="builderAssemblyPath">Resolved builder assembly path.</param>
    /// <param name="playerSourceRootPath">Resolved player source root path.</param>
    /// <returns>True when either payload exists on disk; otherwise false.</returns>
    static bool IsInstalled(string builderAssemblyPath, string playerSourceRootPath, string generatedCoreCppRootPath, string codegenToolPath) {
        if (!string.IsNullOrWhiteSpace(builderAssemblyPath) && File.Exists(builderAssemblyPath)) {
            return !string.IsNullOrWhiteSpace(playerSourceRootPath) && Directory.Exists(playerSourceRootPath)
                && (string.IsNullOrWhiteSpace(generatedCoreCppRootPath) || Directory.Exists(generatedCoreCppRootPath))
                && (string.IsNullOrWhiteSpace(codegenToolPath) || File.Exists(codegenToolPath));
        }

        return !string.IsNullOrWhiteSpace(playerSourceRootPath)
            && Directory.Exists(playerSourceRootPath)
            && (string.IsNullOrWhiteSpace(generatedCoreCppRootPath) || Directory.Exists(generatedCoreCppRootPath))
            && (string.IsNullOrWhiteSpace(codegenToolPath) || File.Exists(codegenToolPath));
    }
}
