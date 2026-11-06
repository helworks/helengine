using System.Reflection;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies optional runtime module assemblies declared by configured generated-core projects are discovered explicitly instead of depending on what the editor happens to have loaded.
/// </summary>
public sealed class GeneratedRuntimeModuleDiscoveryTests {
    /// <summary>
    /// Verifies the manifest of a configured generated-core project is discovered and activated by its component type.
    /// </summary>
    [Fact]
    public void ManifestFromConfiguredProject_isDiscoveredWithoutPriorLoad() {
        string projectPath = Path.Combine(ResolveRepositoryRootPath(), "engine", "helengine.timeline.runtime", "helengine.timeline.runtime.csproj");

        IReadOnlyList<Assembly> assemblies = EditorGeneratedCoreRegenerationService.ResolveGeneratedRuntimeModuleManifestAssemblies(
            null,
            [projectPath]);
        Assembly timelineAssembly = assemblies.Single(assembly => assembly.GetName().Name == "helengine.timeline.runtime");
        Type timelinePlayerType = timelineAssembly.GetType("helengine.timeline.runtime.TimelinePlayerComponent", true);

        IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> manifests = EditorGeneratedCoreRegenerationService.DiscoverGeneratedRuntimeModuleManifests(assemblies);
        IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> activeManifests = EditorGeneratedCoreRegenerationService.ResolveActiveGeneratedRuntimeModuleManifests(
            manifests,
            [timelinePlayerType]);

        Assert.Contains(activeManifests, manifest => manifest.ModuleId == "timeline-runtime-module");
    }

    /// <summary>
    /// Verifies a configured project without a loadable assembly next to the editor contributes no manifest assembly instead of failing discovery.
    /// </summary>
    [Fact]
    public void ConfiguredProjectWithoutEditorAssembly_isSkipped() {
        string projectPath = Path.Combine(Path.GetTempPath(), "helengine.not.shipped.with.editor", "helengine.not.shipped.with.editor.csproj");

        IReadOnlyList<Assembly> assemblies = EditorGeneratedCoreRegenerationService.ResolveGeneratedRuntimeModuleManifestAssemblies(
            null,
            [projectPath]);

        Assert.DoesNotContain(assemblies, assembly => assembly.GetName().Name == "helengine.not.shipped.with.editor");
    }

    /// <summary>
    /// Resolves the active helengine checkout from the test source independently of artifact placement.
    /// </summary>
    /// <param name="sourcePath">Compiler-supplied source file in the active checkout.</param>
    /// <returns>Absolute repository root path.</returns>
    static string ResolveRepositoryRootPath([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") {
        string currentPath = Path.GetDirectoryName(sourcePath);
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            if (File.Exists(Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj"))) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the test source location.");
    }
}
