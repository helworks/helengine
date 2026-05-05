using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor-owned asset cook service writes runtime cooked assets and startup-scene metadata.
/// </summary>
public sealed class EditorPlatformAssetCookServiceTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string BuildRootPath;

    public EditorPlatformAssetCookServiceTests() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-cook-tests", Guid.NewGuid().ToString("N"));
        ProjectRootPath = workspaceRootPath;
        BuildRootPath = Path.Combine(workspaceRootPath, "Build");
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache", "shader-cache"));
        Directory.CreateDirectory(BuildRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void Cook_scene_build_outputs_runtime_hasset_and_sets_startup_scene_from_order() {
        string startupSceneId = "Scenes/MainMenu.helen";
        string secondarySceneId = "Scenes/Level01.helen";
        string sourceModelRelativePath = "Models/Sponza.obj";
        string sourceModelPath = Path.Combine(ProjectRootPath, "assets", sourceModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath)!);
        File.WriteAllText(sourceModelPath, "o Sponza\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        WriteSceneAsset(
            startupSceneId,
            new[] {
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = sourceModelRelativePath,
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                }
            });
        WriteSceneAsset(secondarySceneId, Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            new IAssetImporterRegistration[] {
                new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" })
            },
            null);

        PlatformBuildManifest manifest = service.Cook(
            new helengine.baseplatform.Definitions.PlatformDefinition(
                "windows",
                "Windows",
                Array.Empty<helengine.baseplatform.Definitions.PlatformBuildProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformGraphicsProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformAssetRequirementDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMaterialSchemaDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentCompatibilityDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformCodegenProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformStorageProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMediaProfileDefinition>()),
            new[] { startupSceneId, secondarySceneId },
            BuildRootPath,
            new[] { "windows" });

        Assert.Equal(startupSceneId, manifest.StartupSceneId);
        Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "main.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "startup.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "scenes", "Scenes", "Level01.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath);
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags && entry.Value == "0");
    }

    /// <summary>
    /// Verifies the committed point-shadow rendering scene cooks successfully with the installed Windows builder metadata.
    /// </summary>
    [Fact]
    public void Cook_when_using_committed_point_shadow_scene_with_windows_builder_metadata_succeeds() {
        string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
        CopyDirectory(Path.Combine(sourceProjectRootPath, "assets"), Path.Combine(ProjectRootPath, "assets"));
        string sourceCacheRootPath = Path.Combine(sourceProjectRootPath, "cache");
        if (Directory.Exists(sourceCacheRootPath)) {
            CopyDirectory(sourceCacheRootPath, Path.Combine(ProjectRootPath, "cache"));
        }

        EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(sourceProjectRootPath, "project.heproj"));
        AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
        EditorPlatformAssetBuilderLoader builderLoader = new();
        helengine.baseplatform.Builders.IPlatformAssetBuilder builder = builderLoader.Load(platformDescriptor.BuilderAssemblyPath);
        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            bootstrap.RequiredEngineVersion,
            bootstrap.ProjectName,
            bootstrap.ProjectVersion,
            Array.Empty<IAssetImporterRegistration>(),
            null);

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            ["Scenes/rendering/point-shadow.helen"],
            BuildRootPath,
            ["windows"],
            builder,
            "debug",
            "directx11");

        Assert.Equal("Scenes/rendering/point-shadow.helen", manifest.StartupSceneId);
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "main.hasset")));
    }

    void WriteSceneAsset(string sceneId, SceneAssetReference[] assetReferences) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = "root-entity",
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            }
        };

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Copies one directory tree into the temporary test workspace while preserving relative paths.
    /// </summary>
    /// <param name="sourceRootPath">Source directory tree to copy.</param>
    /// <param name="destinationRootPath">Destination directory root.</param>
    static void CopyDirectory(string sourceRootPath, string destinationRootPath) {
        Directory.CreateDirectory(destinationRootPath);
        string[] sourceFilePaths = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
        Array.Sort(sourceFilePaths, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < sourceFilePaths.Length; index++) {
            string sourceFilePath = sourceFilePaths[index];
            string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
            string destinationPath = Path.Combine(destinationRootPath, relativePath);
            string destinationDirectoryPath = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(destinationDirectoryPath);
            File.Copy(sourceFilePath, destinationPath, true);
        }
    }
}
