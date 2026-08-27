using helengine;
using helengine.baseplatform.Results;
using System.Reflection;

namespace helengine.editor.tests;

/// <summary>
/// Verifies editor and runtime source no longer exposes superseded build and scene compatibility APIs.
/// </summary>
public sealed class CurrentOnlyBuildAndSceneApiContractTests {
    /// <summary>
    /// Ensures the runtime scene resolver has only its current content-manager constructor.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_exposes_only_content_manager_constructor() {
        Assert.Single(typeof(RuntimeSceneAssetReferenceResolver).GetConstructors());
    }

    /// <summary>
    /// Ensures editor build config source does not invoke the deleted legacy profile normalizer.
    /// </summary>
    [Fact]
    public void EditorBuildConfigService_source_does_not_normalize_legacy_profile_ids() {
        string source = File.ReadAllText(ResolveSourcePath("engine", "helengine.editor", "managers", "project", "EditorBuildConfigService.cs"));

        Assert.DoesNotContain("EditorLegacyBuildProfileIdNormalizer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeLocalBuildProfileId", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures modifier stack source no longer reads or writes superseded per-platform tessellation fields.
    /// </summary>
    [Fact]
    public void MeshComponentModifierStackService_source_does_not_use_superseded_tessellation_fields() {
        string source = File.ReadAllText(ResolveSourcePath("engine", "helengine.editor", "managers", "scene", "MeshComponentModifierStackService.cs"));

        Assert.DoesNotContain("TessellationSettingsService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadLegacyTessellationStack", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SynchronizeLegacyTessellationMembers", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the scene-packager result accepts complete typed shader dependencies without an ID-only compatibility path.
    /// </summary>
    [Fact]
    public void EditorPlatformBuildScenePackagerResult_exposes_typed_dependencies_only() {
        ConstructorInfo constructor = Assert.Single(typeof(EditorPlatformBuildScenePackagerResult).GetConstructors());
        ParameterInfo[] parameters = constructor.GetParameters();

        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(IReadOnlyList<PlatformShaderDependency>));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(IReadOnlyList<string>));
        Assert.Null(typeof(EditorPlatformBuildScenePackagerResult).GetProperty("ReferencedShaderAssetIds"));

        string packagerSource = File.ReadAllText(ResolveSourcePath("engine", "helengine.editor", "managers", "project", "EditorWindowsBuildScenePackager.cs"));
        string transformSource = File.ReadAllText(ResolveSourcePath("engine", "helengine.editor", "managers", "project", "SceneComponentPackagingTransformService.cs"));
        Assert.DoesNotContain("ReferencedShaderAssetIds", packagerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RememberReferencedShaderAssetId", packagerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferencedShaderAssetIds", transformSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RememberReferencedShaderAssetId", transformSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures production editor code has no separate per-platform tessellation settings service.
    /// </summary>
    [Fact]
    public void EditorProduction_uses_modifier_stack_without_superseded_tessellation_service() {
        string editorRoot = ResolveSourcePath("engine", "helengine.editor");
        string[] productionSources = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);

        foreach (string sourcePath in productionSources) {
            string source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("MeshComponentTessellationSettingsService", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Resolves one source path from the repository root discovered beside the test assembly.
    /// </summary>
    /// <param name="segments">Repository-relative path segments.</param>
    /// <returns>Absolute source path.</returns>
    static string ResolveSourcePath(params string[] segments) {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string markerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(markerPath)) {
                return Path.Combine(new[] { currentPath }.Concat(segments).ToArray());
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }
}
