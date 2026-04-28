using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Tracks installed engine builds and shared platform artifacts under the managed launcher install roots.
/// </summary>
public sealed class EngineInstallManager {
    /// <summary>
    /// Gets the JSON serializer options used for the engine install manifest.
    /// </summary>
    static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Stores the resolved managed install roots used by the launcher.
    /// </summary>
    LauncherInstallRoots InstallRootsValue { get; }

    /// <summary>
    /// Stores the engine manifest file path under the managed engine install root.
    /// </summary>
    string EngineManifestFilePath { get; }

    /// <summary>
    /// Stores the shared-artifact manifest service bound to the managed toolchain root.
    /// </summary>
    InstalledArtifactStore ArtifactStore { get; }

    /// <summary>
    /// Stores the installed-binding manifest service bound to the managed toolchain root.
    /// </summary>
    InstalledBindingStore BindingStore { get; }

    /// <summary>
    /// Stores the installed engine entries currently loaded in memory.
    /// </summary>
    List<EngineInstall> Installs { get; } = new();

    /// <summary>
    /// Stores the installed shared artifacts currently loaded in memory.
    /// </summary>
    List<InstalledArtifact> Artifacts { get; } = new();

    /// <summary>
    /// Stores the installed engine-platform bindings currently loaded in memory.
    /// </summary>
    List<InstalledEnginePlatformBinding> Bindings { get; } = new();

    /// <summary>
    /// Creates one engine install manager using the persisted launcher install roots.
    /// </summary>
    public EngineInstallManager()
        : this(new LauncherInstallRootResolver(new WindowsLauncherInstallRootLocator())) {
    }

    /// <summary>
    /// Creates one engine install manager using the supplied root resolver.
    /// </summary>
    /// <param name="rootResolver">Resolver that supplies the managed engine and toolchain roots.</param>
    public EngineInstallManager(LauncherInstallRootResolver rootResolver)
        : this(rootResolver.Resolve()) {
    }

    /// <summary>
    /// Creates one engine install manager using the supplied managed roots.
    /// </summary>
    /// <param name="installRoots">Resolved managed roots used to store engine and toolchain manifests.</param>
    public EngineInstallManager(LauncherInstallRoots installRoots) {
        InstallRootsValue = installRoots;
        EngineManifestFilePath = Path.Combine(installRoots.EngineInstallRoot, "engines.json");
        ArtifactStore = new InstalledArtifactStore(installRoots.SharedToolchainRoot);
        BindingStore = new InstalledBindingStore(installRoots.SharedToolchainRoot);

        Directory.CreateDirectory(installRoots.EngineInstallRoot);
        Directory.CreateDirectory(installRoots.SharedToolchainRoot);
        Load();
    }

    /// <summary>
    /// Gets the resolved managed install roots used by the launcher.
    /// </summary>
    public LauncherInstallRoots InstallRoots => InstallRootsValue;

    /// <summary>
    /// Gets the installed engine entries currently known to the launcher.
    /// </summary>
    public IReadOnlyList<EngineInstall> InstalledEngines => Installs;

    /// <summary>
    /// Gets the installed shared artifacts currently known to the launcher.
    /// </summary>
    public IReadOnlyList<InstalledArtifact> InstalledArtifacts => Artifacts;

    /// <summary>
    /// Gets the installed engine-platform bindings currently known to the launcher.
    /// </summary>
    public IReadOnlyList<InstalledEnginePlatformBinding> InstalledBindings => Bindings;

    /// <summary>
    /// Reloads engine installs, shared artifacts, and engine-platform bindings from the managed manifests.
    /// </summary>
    public void Load() {
        Installs.Clear();
        Installs.AddRange(LoadInstalledEngines());

        Artifacts.Clear();
        Artifacts.AddRange(ArtifactStore.Load());

        Bindings.Clear();
        Bindings.AddRange(BindingStore.Load());
    }

    /// <summary>
    /// Saves the installed engine manifest under the managed engine install root.
    /// </summary>
    public void Save() {
        Directory.CreateDirectory(InstallRootsValue.EngineInstallRoot);
        EngineInstallManifest manifest = new EngineInstallManifest {
            Engines = Installs.ToList(),
            LastUpdated = DateTime.UtcNow
        };
        string json = JsonFormatting.SerializeWithIndent(manifest, JsonSerializerOptions);
        File.WriteAllText(EngineManifestFilePath, json);
    }

    /// <summary>
    /// Adds or refreshes one locally discovered engine install and persists the updated manifest.
    /// </summary>
    /// <param name="folderPath">Folder path that contains the engine install.</param>
    /// <param name="versionInfo">Detected version information for the engine install.</param>
    /// <returns>Installed engine entry tracked by the launcher.</returns>
    public EngineInstall AddLocalInstall(string folderPath, EngineVersionInfo versionInfo) {
        string fullPath = Path.GetFullPath(folderPath);
        int existingInstallIndex = FindExistingInstallIndex(fullPath);
        if (existingInstallIndex >= 0) {
            EngineInstall existing = Installs[existingInstallIndex];
            existing.Version = versionInfo.DisplayVersion;
            existing.DetectedFrom = versionInfo.AssemblyName;
            existing.InstalledAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(versionInfo.FriendlyName)) {
                existing.Name = versionInfo.FriendlyName;
            }

            Save();
            return existing;
        }

        EngineInstall install = new EngineInstall {
            InstallPath = fullPath,
            Version = versionInfo.DisplayVersion,
            DetectedFrom = versionInfo.AssemblyName,
            InstalledAt = DateTime.Now,
            Source = "local",
            Name = string.IsNullOrWhiteSpace(versionInfo.FriendlyName) ? string.Empty : versionInfo.FriendlyName
        };

        Installs.Insert(0, install);
        Save();
        return install;
    }

    /// <summary>
    /// Replaces the installed engine list and persists it under the managed engine install root.
    /// </summary>
    /// <param name="newInstalls">Installed engine entries that should replace the current list.</param>
    public void ReplaceInstalls(IEnumerable<EngineInstall> newInstalls) {
        Installs.Clear();
        Installs.AddRange(newInstalls);
        Save();
    }

    /// <summary>
    /// Replaces the installed shared artifacts and persists them under the managed shared toolchain root.
    /// </summary>
    /// <param name="newArtifacts">Installed shared artifacts that should replace the current list.</param>
    public void ReplaceInstalledArtifacts(IEnumerable<InstalledArtifact> newArtifacts) {
        Artifacts.Clear();
        Artifacts.AddRange(newArtifacts);
        ArtifactStore.Save(Artifacts);
    }

    /// <summary>
    /// Replaces the installed engine-platform bindings and persists them under the managed shared toolchain root.
    /// </summary>
    /// <param name="newBindings">Installed engine-platform bindings that should replace the current list.</param>
    public void ReplaceInstalledBindings(IEnumerable<InstalledEnginePlatformBinding> newBindings) {
        Bindings.Clear();
        Bindings.AddRange(newBindings);
        BindingStore.Save(Bindings);
    }

    /// <summary>
    /// Loads the installed engine entries from the managed engine manifest.
    /// </summary>
    /// <returns>Installed engine entries stored under the managed engine install root.</returns>
    IReadOnlyList<EngineInstall> LoadInstalledEngines() {
        if (!File.Exists(EngineManifestFilePath)) {
            return Array.Empty<EngineInstall>();
        }

        string json = File.ReadAllText(EngineManifestFilePath);
        EngineInstallManifest manifest = JsonSerializer.Deserialize<EngineInstallManifest>(json, JsonSerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize engine install manifest at {EngineManifestFilePath}.");
        return manifest.Engines;
    }

    /// <summary>
    /// Finds the zero-based index of one existing installed engine entry that already points to the supplied install path.
    /// </summary>
    /// <param name="fullPath">Normalized absolute engine install path to match.</param>
    /// <returns>Matching installed engine index or <c>-1</c> when the path is not yet tracked.</returns>
    int FindExistingInstallIndex(string fullPath) {
        for (int index = 0; index < Installs.Count; index++) {
            if (PathsEqual(Installs[index].InstallPath, fullPath)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Compares two install paths after normalizing them to absolute directory paths.
    /// </summary>
    /// <param name="firstPath">First path to compare.</param>
    /// <param name="secondPath">Second path to compare.</param>
    /// <returns><c>true</c> when both normalized paths point to the same directory; otherwise <c>false</c>.</returns>
    static bool PathsEqual(string firstPath, string secondPath) {
        return string.Equals(
            Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
